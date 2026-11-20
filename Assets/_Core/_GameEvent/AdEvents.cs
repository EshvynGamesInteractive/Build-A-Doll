namespace Core._GameEvent
{
    public abstract partial class GameEvents
    {
        /// <summary>Rewarded ads. Raised by Game.Ads.RewardedAds; the ad plugin itself plugs in through IRewardedAdService.</summary>
        public static class AdEvents
        {
            /// <summary>Notification: a rewarded ad was asked for. Arg: placement (e.g. "restore_hair").</summary>
            public static readonly GameEvent<string> RewardedAdRequested = new();

            /// <summary>Notification: the player watched the ad and earned the reward. Arg: placement. Give the reward here.</summary>
            public static readonly GameEvent<string> RewardedAdRewarded = new();

            /// <summary>Notification: no reward (ad not ready, failed, or closed early). Arg: placement.</summary>
            public static readonly GameEvent<string> RewardedAdFailed = new();
        }
    }
}
