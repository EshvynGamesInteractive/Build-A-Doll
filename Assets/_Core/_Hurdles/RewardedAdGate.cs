using _Core._Player;
using Core._GameEvent;
using Game.Ads;
using TMPro;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// Rewarded-ad gate with two rewards (player only; the AI doesn't watch ads):
    /// - Something was cut off this run (e.g. hair, by scissors): "HAIR BACK": the reward puts it back.
    /// - Nothing was cut: "+50": the reward is coins.
    /// The sign switches by itself when her hair is cut or given back (GameplayEvents.CutItemsChanged).
    /// The reward is given when GameEvents.AdEvents.RewardedAdRewarded fires (via RewardedAds.Show).
    /// </summary>
    public class RewardedAdGate : TrackHurdle
    {
        [Header("Hair back (something was cut)")]
        [SerializeField] private string restorePlacement = RewardedAds.RestoreHair;
        [SerializeField] private string restoreText = "<size=140%>AD</size>\nHAIR BACK";

        [Header("Coins (nothing was cut)")]
        [SerializeField] private string coinsPlacement = RewardedAds.BonusCoins;
        [SerializeField, Min(0)] private int coinReward = 50;
        [Tooltip("{0} = Coin Reward.")]
        [SerializeField] private string coinsText = "<size=140%>AD</size>\n+{0}";

        [Header("Look")]
        [Tooltip("The sign on the gate (TextMeshPro).")]
        [SerializeField] private TMP_Text sign;

        private bool _hasCut;

        protected override bool AllowRuntimeObjects => false;

        protected override void Awake()
        {
            base.Awake();
            SetChoiceMatch(true); // always shows the green (good) visual
            RefreshSign();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            GameEvents.GameplayEvents.CutItemsChanged.Register(OnCutItemsChanged);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            GameEvents.GameplayEvents.CutItemsChanged.Unregister(OnCutItemsChanged);
        }

        public override void ResetHurdle()
        {
            base.ResetHurdle();
            _hasCut = false;
            RefreshSign();
        }

        private void OnCutItemsChanged(bool hasCut)
        {
            _hasCut = hasCut;
            RefreshSign();
        }

        private void RefreshSign()
        {
            if (sign != null)
                sign.text = _hasCut ? restoreText : string.Format(coinsText, coinReward);
        }

        protected override void OnPlayerHit(RunnerController player)
        {
            if (player.IsAI)
                return;

            var outfit = player.DollOutfit;
            if (outfit != null && outfit.HasCutItems)
            {
                RewardedAds.Show(restorePlacement, () =>
                {
                    if (outfit != null)
                        outfit.RestoreCutItems();
                });
                return;
            }

            var score = player.Score;
            RewardedAds.Show(coinsPlacement, () =>
            {
                if (score != null)
                    score.AddCoins(coinReward, true);
            });
        }
    }
}
