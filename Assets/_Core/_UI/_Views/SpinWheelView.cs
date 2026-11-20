using System;
using System.Collections.Generic;
using Core._GameEvent;
using DG.Tweening;
using Game.Ads;
using Game.Level;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    /// <summary>
    /// Level Complete reward wheel. SPIN → rewarded ad (placement "level_complete_spin") → when the reward is earned the
    /// wheel spins (DOTween) and stops on a random segment (weighted); that segment's multiplier × the level's completion
    /// coins goes into the wallet (GameplayEvents.SpinRewardGranted). One spin per level complete.
    /// Segment i sits i × (360 / count) degrees clockwise from the top; the pin is at the top. UI is authored in the prefab
    /// (Build A Doll > UI > Add Spin Wheel To Level Complete builds it).
    /// </summary>
    public class SpinWheelView : MonoBehaviour
    {
        [Serializable]
        public class Segment
        {
            public string label = "x2";
            [Min(1)] public int multiplier = 2;
            [Tooltip("Relative chance of stopping here.")]
            [Min(0f)] public float weight = 1f;
        }

        [SerializeField] private RectTransform wheel;
        [SerializeField] private Button spinButton;
        [SerializeField] private TMP_Text spinButtonText;
        [Tooltip("Shows the reward when the wheel stops (hidden before).")]
        [SerializeField] private TMP_Text resultText;

        [SerializeField] private Segment[] segments =
        {
            new() { label = "x2", multiplier = 2, weight = 30f },
            new() { label = "x3", multiplier = 3, weight = 15f },
            new() { label = "x2", multiplier = 2, weight = 30f },
            new() { label = "x5", multiplier = 5, weight = 4f },
            new() { label = "x2", multiplier = 2, weight = 30f },
            new() { label = "x3", multiplier = 3, weight = 15f },
            new() { label = "x2", multiplier = 2, weight = 30f },
            new() { label = "x4", multiplier = 4, weight = 8f }
        };

        [Header("Spin")]
        [SerializeField, Min(0.5f)] private float spinTime = 3.5f;
        [SerializeField, Min(1)] private int fullTurns = 5;
        [SerializeField] private string placement = RewardedAds.LevelCompleteSpin;

        [Header("Texts")]
        [SerializeField] private string spinLabel = "SPIN";
        [SerializeField] private string spinningLabel = "...";
        [SerializeField] private string doneLabel = "CLAIMED";
        [Tooltip("{0} = coins won, {1} = segment label.")]
        [SerializeField] private string resultFormat = "{1}!  +{0}";

        private int _baseCoins = 50;
        private bool _used;
        private int _pending = -1;
        private Tween _tween;

        public IReadOnlyList<Segment> Segments => segments;

        private void Awake()
        {
            if (spinButton != null)
                spinButton.onClick.AddListener(OnSpinPressed);
        }

        /// <summary>Called when the Level Complete panel fills in: resets the wheel for this level's reward.</summary>
        public void Prepare(int completionCoins)
        {
            KillTween(false);
            _baseCoins = Mathf.Max(1, completionCoins);
            _used = false;
            _pending = -1;
            if (wheel != null) wheel.localEulerAngles = Vector3.zero;
            SetButton(true, spinLabel);
            if (resultText != null) resultText.gameObject.SetActive(false);
        }

        private void OnSpinPressed()
        {
            if (_used || _tween != null)
                return;

            SetButton(false, spinningLabel);
            RewardedAds.Show(placement, StartSpin, () => SetButton(true, spinLabel));
        }

        private void StartSpin()
        {
            if (_used || segments == null || segments.Length == 0 || wheel == null)
                return;

            _used = true;
            var index = PickSegment();
            _pending = index;
            var step = 360f / segments.Length;
            // Rotating the wheel anticlockwise by i × step brings segment i under the pin at the top.
            var target = fullTurns * 360f + index * step + UnityEngine.Random.Range(-0.35f, 0.35f) * step;
            _tween = wheel.DOLocalRotate(new Vector3(0f, 0f, target), spinTime, RotateMode.FastBeyond360)
                .SetEase(Ease.OutQuart)
                .SetUpdate(true) // works even if the game is paused behind the panel
                .OnComplete(() => Grant(index));
        }

        private int PickSegment()
        {
            var total = 0f;
            foreach (var s in segments) total += Mathf.Max(0f, s.weight);
            if (total <= 0f) return UnityEngine.Random.Range(0, segments.Length);

            var roll = UnityEngine.Random.value * total;
            for (var i = 0; i < segments.Length; i++)
            {
                roll -= Mathf.Max(0f, segments[i].weight);
                if (roll <= 0f) return i;
            }
            return segments.Length - 1;
        }

        private void Grant(int index)
        {
            _tween = null;
            _pending = -1;
            var segment = segments[index];
            var coins = _baseCoins * segment.multiplier;
            LevelProgress.AddCoins(coins);
            GameEvents.GameplayEvents.SpinRewardGranted.Raise(coins);

            SetButton(false, doneLabel);
            if (resultText != null)
            {
                resultText.text = string.Format(resultFormat, coins, segment.label);
                resultText.gameObject.SetActive(true);
            }
        }

        private void SetButton(bool interactable, string label)
        {
            if (spinButton != null) spinButton.interactable = interactable;
            if (spinButtonText != null) spinButtonText.text = label;
        }

        // Panel closed mid-spin: finish it so the reward isn't lost.
        private void OnDisable() => KillTween(true);

        private void KillTween(bool grantPending)
        {
            if (_tween != null && _tween.IsActive())
                _tween.Kill();
            _tween = null;
            if (grantPending && _pending >= 0)
                Grant(_pending);
        }
    }
}
