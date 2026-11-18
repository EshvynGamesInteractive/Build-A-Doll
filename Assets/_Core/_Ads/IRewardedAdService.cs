using System;

namespace Game.Ads
{
    /// <summary>
    /// Plays rewarded ads. Implement it over your ad SDK (e.g. LevelPlay) and register it with
    /// RewardedAds.Register; the game only talks to this interface.
    /// </summary>
    public interface IRewardedAdService
    {
        bool IsReady(string placement);

        /// <summary>Shows an ad. Calls onFinished(true) only when the reward was earned.</summary>
        void Show(string placement, Action<bool> onFinished);
    }
}
