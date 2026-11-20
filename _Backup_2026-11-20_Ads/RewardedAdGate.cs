using _Core._Player;
using Game.Ads;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// Rewarded-ad gate: running through it offers an ad; when the reward is earned (GameEvents.AdEvents.RewardedAdRewarded)
    /// everything a hurdle cut off this run (e.g. her hair, by scissors) is put back.
    /// Does nothing if nothing was cut. Player only (the AI doesn't watch ads).
    /// </summary>
    public class RewardedAdGate : TrackHurdle
    {
        [Tooltip("Placement name passed to the ad service.")]
        [SerializeField] private string placement = RewardedAds.RestoreHair;

        protected override bool AllowRuntimeObjects => false;

        protected override void Awake()
        {
            base.Awake();
            SetChoiceMatch(true); // always shows the green (good) visual
        }

        protected override void OnPlayerHit(RunnerController player)
        {
            if (player.IsAI)
                return;

            var outfit = player.DollOutfit;
            if (outfit == null || !outfit.HasCutItems)
                return;

            RewardedAds.Show(placement, () =>
            {
                if (outfit != null)
                    outfit.RestoreCutItems();
            });
        }
    }
}
