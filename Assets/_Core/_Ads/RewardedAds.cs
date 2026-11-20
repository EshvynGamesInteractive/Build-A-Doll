using System;
using Core._GameEvent;
using UnityEngine;

namespace Game.Ads
{
    /// <summary>
    /// What an ad plugin (LevelPlay etc.) has to provide. Not implemented yet: the plugin is skipped for now.
    /// When it's added, write a class that implements this and set RewardedAds.Service = it at startup.
    /// </summary>
    public interface IRewardedAdService
    {
        bool IsReady(string placement);

        /// <summary>Show the ad; call onClosed(true) if the player earned the reward, onClosed(false) otherwise.</summary>
        void Show(string placement, Action<bool> onClosed);
    }

    /// <summary>
    /// Entry point for rewarded ads. Call RewardedAds.Show("placement", onRewarded).
    /// Raises GameEvents.AdEvents.RewardedAdRequested, then RewardedAdRewarded (reward earned) or RewardedAdFailed.
    /// With no Service set (no plugin yet) the reward is granted straight away when GrantWithoutService is on,
    /// so the rest of the game can be built and tested.
    /// </summary>
    public static class RewardedAds
    {
        public const string RestoreHair = "restore_hair";
        public const string BonusCoins = "bonus_coins";
        public const string LevelCompleteSpin = "level_complete_spin";

        /// <summary>The ad plugin. Null until one is integrated.</summary>
        public static IRewardedAdService Service { get; set; }

        /// <summary>No plugin: true = grant the reward at once (testing), false = fail.</summary>
        public static bool GrantWithoutService { get; set; } = true;

        public static bool IsReady(string placement) => Service?.IsReady(placement) ?? GrantWithoutService;

        public static void Show(string placement, Action onRewarded, Action onFailed = null)
        {
            GameEvents.AdEvents.RewardedAdRequested.Raise(placement);

            if (Service == null)
            {
                Debug.Log($"[Ads] No rewarded ad plugin yet: '{placement}' {(GrantWithoutService ? "rewarded straight away (test mode)" : "failed")}.");
                Finish(placement, GrantWithoutService, onRewarded, onFailed);
                return;
            }

            if (!Service.IsReady(placement))
            {
                Finish(placement, false, onRewarded, onFailed);
                return;
            }

            Service.Show(placement, rewarded => Finish(placement, rewarded, onRewarded, onFailed));
        }

        private static void Finish(string placement, bool rewarded, Action onRewarded, Action onFailed)
        {
            if (rewarded)
            {
                GameEvents.AdEvents.RewardedAdRewarded.Raise(placement);
                onRewarded?.Invoke();
            }
            else
            {
                GameEvents.AdEvents.RewardedAdFailed.Raise(placement);
                onFailed?.Invoke();
            }
        }
    }
}
