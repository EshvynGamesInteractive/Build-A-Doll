using System;
using System.Collections.Generic;
using _Core._Player;
using Core._GameEvent;
using Game.Ads;
using UnityEngine;

namespace Game.Player.Features
{
    /// <summary>
    /// States track elements put the doll in: on fire, dirty, shielded... Each status has its own
    /// settings (visual, duration, slow-down, coin drain, shield) and can offer a rewarded removal
    /// ("REMOVE FIRE?"). Everything ends on ResetRun.
    /// </summary>
    [DisallowMultipleComponent]
    public class DollStatus : MonoBehaviour, IHazardShield
    {
        [Serializable]
        private class StatusSettings
        {
            public StatusType type;

            [Tooltip("Shown while the status is on (fire particles, dirt, shield bubble). A child of the doll, off in the prefab.")]
            public GameObject visual;

            [Tooltip("Animator bool set while on (e.g. OnFire). Empty = none.")]
            public string animatorBool;

            [Tooltip("Seconds it lasts. 0 = until cleared (extinguisher, rewarded fix, offer).")]
            [Min(0f)] public float duration;

            [Tooltip("Run speed multiplier while on.")]
            [Range(0.1f, 2f)] public float speedFactor = 1f;

            [Tooltip("Coins lost per second while on.")]
            [Min(0f)] public float coinsLostPerSecond;

            [Tooltip("Harmful statuses can't start while a shield is up, and a rewarded fix clears them.")]
            public bool isHarmful = true;

            [Tooltip("Blocks harmful track elements while on (shield).")]
            public bool blocksHazards;

            [Tooltip("With Blocks Hazards: the status ends after blocking one hit.")]
            public bool endsOnBlock;

            [Header("Rewarded removal (player only)")]
            [Tooltip("Pop-up shown when it starts, e.g. \"REMOVE FIRE?\". Empty = no offer.")]
            public string removeOfferTitle;

            public Sprite removeOfferIcon;
            public string adPlacement = "remove_status";
        }

        [SerializeField] private StatusSettings[] statuses;

        private readonly Dictionary<StatusType, float> _remaining = new();
        private readonly Dictionary<StatusType, float> _coinDebt = new();
        private readonly List<StatusType> _buffer = new();
        private RunnerController _runner;

        public bool Has(StatusType type) => _remaining.ContainsKey(type);

        private void Awake()
        {
            _runner = GetComponentInParent<RunnerController>();

            if (statuses == null)
                return;

            foreach (var settings in statuses)
            {
                if (settings != null && settings.visual != null)
                    settings.visual.SetActive(false);
            }
        }

        private void OnEnable() => GameEvents.GameplayEvents.ResetRun.Register(ClearAll);

        private void OnDisable() => GameEvents.GameplayEvents.ResetRun.Unregister(ClearAll);

        public void Apply(StatusType type)
        {
            var settings = Find(type);
            if (settings == null)
            {
                Debug.LogWarning($"'{name}' has no settings for status {type}, so it was ignored.", this);
                return;
            }

            if (settings.isHarmful && IsBlocking())
                return;

            var isNew = !Has(type);
            _remaining[type] = settings.duration > 0f ? settings.duration : float.PositiveInfinity;
            if (!isNew)
                return;

            SetVisible(settings, true);
            OfferRemoval(settings);
        }

        public void Clear(StatusType type)
        {
            if (!_remaining.Remove(type))
                return;

            _coinDebt.Remove(type);
            var settings = Find(type);
            if (settings != null)
                SetVisible(settings, false);
        }

        /// <summary>Ends every harmful status (fire, dirt...), keeps the good ones (shield).</summary>
        public void ClearHarmful()
        {
            _buffer.Clear();
            foreach (var type in _remaining.Keys)
            {
                var settings = Find(type);
                if (settings == null || settings.isHarmful)
                    _buffer.Add(type);
            }

            foreach (var type in _buffer)
                Clear(type);
        }

        public void ClearAll()
        {
            _buffer.Clear();
            _buffer.AddRange(_remaining.Keys);
            foreach (var type in _buffer)
                Clear(type);
        }

        public bool TryBlock()
        {
            foreach (var type in _remaining.Keys)
            {
                var settings = Find(type);
                if (settings == null || !settings.blocksHazards)
                    continue;

                if (settings.endsOnBlock)
                    Clear(type); // safe: returns straight after
                return true;
            }

            return false;
        }

        private bool IsBlocking()
        {
            foreach (var type in _remaining.Keys)
            {
                var settings = Find(type);
                if (settings != null && settings.blocksHazards)
                    return true;
            }

            return false;
        }

        private void Update()
        {
            if (_remaining.Count == 0)
                return;

            var deltaTime = Time.deltaTime;
            _buffer.Clear();
            _buffer.AddRange(_remaining.Keys);

            foreach (var type in _buffer)
            {
                var settings = Find(type);
                if (settings != null && settings.coinsLostPerSecond > 0f)
                    DrainCoins(type, settings.coinsLostPerSecond * deltaTime);

                var left = _remaining[type] - deltaTime;
                if (left <= 0f)
                    Clear(type);
                else
                    _remaining[type] = left;
            }
        }

        // Coins are whole numbers: the fraction is carried over until it adds up to one.
        private void DrainCoins(StatusType type, float amount)
        {
            var debt = (_coinDebt.TryGetValue(type, out var carried) ? carried : 0f) + amount;
            var whole = Mathf.FloorToInt(debt);
            if (whole > 0 && _runner != null && _runner.Score != null)
            {
                _runner.Score.AddCoins(-whole);
                debt -= whole;
            }

            _coinDebt[type] = debt;
        }

        private void SetVisible(StatusSettings settings, bool on)
        {
            if (settings.visual != null)
                settings.visual.SetActive(on);

            if (_runner == null)
                return;

            if (on && !Mathf.Approximately(settings.speedFactor, 1f))
                _runner.SetSpeedFactor(settings, settings.speedFactor);
            else
                _runner.ClearSpeedFactor(settings);

            if (_runner.Animator != null)
                _runner.Animator.SetBool(settings.animatorBool, on);

            if (!_runner.IsAI)
                GameEvents.TrackEvents.StatusChanged.Raise(settings.type, on);
        }

        private void OfferRemoval(StatusSettings settings)
        {
            if (_runner == null || _runner.IsAI || string.IsNullOrEmpty(settings.removeOfferTitle))
                return;

            var type = settings.type;
            RewardedOffer.Request(settings.removeOfferTitle, settings.removeOfferIcon, settings.adPlacement, granted =>
            {
                if (granted && this != null)
                    Clear(type);
            });
        }

        private StatusSettings Find(StatusType type)
        {
            if (statuses == null)
                return null;

            foreach (var settings in statuses)
            {
                if (settings != null && settings.type == type)
                    return settings;
            }

            return null;
        }
    }
}
