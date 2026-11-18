using _Core._Player;
using Core._GameEvent;
using Game.Player;
using Game.Player.Features;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// Base for anything on the track the doll runs through: item gates, money, obstacles.
    /// Detects the player with a trigger collider, fires once, and comes back on ResetRun.
    /// Subclasses only decide what happens to the player in OnPlayerHit.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class TrackHurdle : MonoBehaviour
    {
        [Tooltip("Shown while the hurdle can still be hit. Leave empty to keep everything visible.")]
        [SerializeField] private GameObject activeVisual;

        [SerializeField] private GameObject redVisual;
        [SerializeField] private GameObject greenVisual;

        [Tooltip("Optional effect spawned where the player hit it.")]
        [SerializeField] private ParticleSystem hitEffect;

        [SerializeField] private AudioClip hitSound;

        private Collider _trigger;
        private bool _showGreen;

        protected bool HasChoiceVisuals => redVisual != null && greenVisual != null;
        protected bool ShowsGreen => HasChoiceVisuals && _showGreen;
        private HurdleGroup _group;

        public bool IsUsed { get; private set; }

        /// <summary>Raised after the hit was handled, e.g. for a collection animation.</summary>
        public event System.Action<RunnerController> PlayerHit;

        /// <summary>Harmful hurdles are ignored while the doll has a shield (or an invulnerable ride).</summary>
        protected virtual bool IsHazard => false;

        protected virtual void Awake()
        {
            _trigger = GetComponent<Collider>();
            _trigger.isTrigger = true;

            // Trigger events need a Rigidbody on one side; a kinematic one costs nothing.
            if (!TryGetComponent<Rigidbody>(out var body))
                body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            _group = GetComponentInParent<HurdleGroup>();
        }

        protected virtual void OnEnable() => GameEvents.GameplayEvents.ResetRun.Register(ResetHurdle);

        protected virtual void OnDisable() => GameEvents.GameplayEvents.ResetRun.Unregister(ResetHurdle);

        private void OnTriggerEnter(Collider other)
        {
            if (IsUsed)
                return;

            var player = other.GetComponentInParent<RunnerController>();
            if (player == null)
                return;

            if (_group != null && !_group.TryClaim(this))
                return;

            Use();

            if (IsHazard && IsShielded(player))
                return;

            OnPlayerHit(player);
            PlayerHit?.Invoke(player);

            if (hitEffect != null)
                Instantiate(hitEffect, other.ClosestPoint(transform.position), Quaternion.identity).Play();

            if (!player.IsAI && hitSound != null)
                GameEvents.AudioEvents.PlaySfx.Raise(hitSound);
        }

        private static bool IsShielded(RunnerController player)
        {
            foreach (var shield in player.GetComponentsInChildren<IHazardShield>())
            {
                if (shield.TryBlock())
                    return true;
            }

            return false;
        }

        /// <summary>What this hurdle does to the player.</summary>
        protected abstract void OnPlayerHit(RunnerController player);

        /// <summary>Turns the hurdle off without effect, e.g. when the other gate of a pair was taken.</summary>
        public void Use()
        {
            IsUsed = true;
            _trigger.enabled = false;
            RefreshVisual();
        }

        public virtual void ResetHurdle()
        {
            IsUsed = false;
            _trigger.enabled = true;
            RefreshVisual();
        }

        protected void SetChoiceMatch(bool matches)
        {
            _showGreen = matches;
            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (HasChoiceVisuals)
            {
                redVisual.SetActive(!IsUsed && !ShowsGreen);
                greenVisual.SetActive(!IsUsed && ShowsGreen);
            }

            if (activeVisual != null && (!HasChoiceVisuals || (activeVisual != redVisual && activeVisual != greenVisual)))
                activeVisual.SetActive(!IsUsed);

            OnVisualRefreshed();
        }

        /// <summary>Called after the visuals were switched (used, reset, red/green changed).</summary>
        protected virtual void OnVisualRefreshed() { }
    }
}
