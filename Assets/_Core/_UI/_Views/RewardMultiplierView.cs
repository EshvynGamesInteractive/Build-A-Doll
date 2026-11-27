using System;
using Core._GameEvent;
using Core.Utilities;
using Game.Ads;
using Game.Level;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    /// <summary>
    /// Level Complete reward arc (the "Spin Wheel" art: a half circle x2 · x3 · x5 · x4 · x2 with a pin).
    /// The pin swings left and right over the arc; the green button shows completion coins × the segment under the pin.
    /// Pressing it plays a rewarded ad (placement "level_complete_spin"); when the reward is earned the pin stops and the
    /// wallet gets topped up to that total. Ad closed / failed = the pin swings again.
    /// Segments run left to right, each Arc / count degrees wide; pin pivot = centre of the arc, 0° = straight up.
    /// Everything is built in the LevelCompletePanel prefab (SpinWheel > Needle, Multiplier > CoinsText).
    /// </summary>
    public class RewardMultiplierView : MonoBehaviour
    {
        [Serializable]
        public class Segment
        {
            public string label = "x2";
            [Min(1)] public int multiplier = 2;
        }

        [Tooltip("The pin. Pivot at the arc's centre (bottom middle).")]
        [SerializeField] private RectTransform needle;
        [Tooltip("Green \"watch ad\" button.")]
        [SerializeField] private Button claimButton;
        [Tooltip("Coins shown on the button (completion coins × multiplier under the pin).")]
        [SerializeField] private TMP_Text rewardText;

        [Tooltip("Left to right, as drawn on the art.")]
        [SerializeField] private Segment[] segments =
        {
            new() { label = "x2", multiplier = 2 },
            new() { label = "x3", multiplier = 3 },
            new() { label = "x5", multiplier = 5 },
            new() { label = "x4", multiplier = 4 },
            new() { label = "x2", multiplier = 2 }
        };

        [Tooltip("Degrees the segments cover (180 = half circle).")]
        [SerializeField, Range(10f, 360f)] private float arc = 180f;
        [Tooltip("The pin stops this many degrees short of each end.")]
        [SerializeField, Min(0f)] private float edgePadding = 6f;
        [Tooltip("Seconds for one swing left → right → left.")]
        [SerializeField, Min(0.2f)] private float swingPeriod = 1.6f;
        [SerializeField] private string placement = RewardedAds.LevelCompleteSpin;
        [Tooltip("{0} = coins.")]
        [SerializeField] private string rewardFormat = "{0}";

        /// <summary>Raised with the total reward once the ad reward is granted.</summary>
        public event Action<int> Claimed;

        private int _baseCoins = 100;
        private bool _baseAlreadyGranted;
        private bool _swinging;
        private bool _claimed;
        private bool _waitingForAd;
        private float _time;
        private int _segment;

        public bool IsClaimed => _claimed;

        private void Awake() => UIPanelButtonUtility.Bind(claimButton, OnClaimPressed);

        /// <summary>Level Complete opened: start swinging for this level's completion coins.
        /// 'alreadyGranted' = the completion coins are already in the wallet (first pass), so only the extra is added.</summary>
        public void Prepare(int completionCoins, bool alreadyGranted)
        {
            _baseCoins = Mathf.Max(1, completionCoins);
            _baseAlreadyGranted = alreadyGranted;
            _claimed = false;
            _waitingForAd = false;
            _swinging = true;
            _time = 0f;
            if (claimButton != null)
                claimButton.interactable = true;
            UpdateNeedle();
        }

        private void Update()
        {
            if (!_swinging)
                return;
            _time += Time.unscaledDeltaTime;
            UpdateNeedle();
        }

        private void UpdateNeedle()
        {
            if (segments == null || segments.Length == 0)
                return;

            var half = arc * 0.5f;
            var limit = Mathf.Max(0f, half - edgePadding);
            // Eased swing: slows down at both ends like a real pendulum.
            var s = 0.5f - 0.5f * Mathf.Cos(_time / swingPeriod * Mathf.PI * 2f);
            var angle = Mathf.Lerp(-limit, limit, s); // degrees right of straight up
            if (needle != null)
                needle.localEulerAngles = new Vector3(0f, 0f, -angle);

            _segment = Mathf.Clamp(Mathf.FloorToInt((angle + half) / arc * segments.Length), 0, segments.Length - 1);
            if (rewardText != null)
                rewardText.text = string.Format(rewardFormat, Total);
        }

        private int Total => _baseCoins * (segments != null && segments.Length > 0 ? segments[_segment].multiplier : 1);

        private void OnClaimPressed()
        {
            if (_claimed || _waitingForAd)
                return;

            _swinging = false; // the pin stops where it is: that's the reward the ad pays out
            _waitingForAd = true;
            if (claimButton != null)
                claimButton.interactable = false;
            RewardedAds.Show(placement, Grant, Resume);
        }

        private void Resume()
        {
            _waitingForAd = false;
            if (_claimed)
                return;
            _swinging = true;
            if (claimButton != null)
                claimButton.interactable = true;
        }

        private void Grant()
        {
            _waitingForAd = false;
            if (_claimed)
                return;

            _claimed = true;
            _swinging = false;
            var total = Total;
            var extra = _baseAlreadyGranted ? total - _baseCoins : total;
            LevelProgress.PendingLevelCoins = 0; // the multiplied total replaces the Get payout
            if (extra > 0)
            {
                LevelProgress.AddCoins(extra);
                GameEvents.GameplayEvents.SpinRewardGranted.Raise(extra);
            }
            if (rewardText != null)
                rewardText.text = string.Format(rewardFormat, total);
            Claimed?.Invoke(total);
        }
    }
}
