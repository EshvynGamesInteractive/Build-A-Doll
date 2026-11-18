using System;
using UnityEngine;

namespace Game.Ads
{
    /// <summary>Where the game asks for rewarded ads. The active IRewardedAdService registers itself here.</summary>
    public static class RewardedAds
    {
        private static IRewardedAdService _service;

        public static void Register(IRewardedAdService service) => _service = service;

        public static void Unregister(IRewardedAdService service)
        {
            if (ReferenceEquals(_service, service))
                _service = null;
        }

        public static bool IsReady(string placement) => _service != null && _service.IsReady(placement);

        public static void Show(string placement, Action<bool> onFinished)
        {
            if (_service == null)
            {
                Debug.LogWarning($"No rewarded ad service is registered, so '{placement}' gave no reward. Add SimulatedRewardedAdService (or your SDK's service) to Bootstrap.");
                onFinished?.Invoke(false);
                return;
            }

            _service.Show(placement, onFinished);
        }
    }
}
