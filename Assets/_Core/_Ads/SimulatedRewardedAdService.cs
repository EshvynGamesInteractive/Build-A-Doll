using System;
using System.Collections;
using UnityEngine;

namespace Game.Ads
{
    /// <summary>
    /// Stand-in until an ad SDK is added: "plays" an ad for a moment and grants the reward.
    /// Put it on a persistent object in Bootstrap; swap it for your SDK's IRewardedAdService later.
    /// </summary>
    public class SimulatedRewardedAdService : MonoBehaviour, IRewardedAdService
    {
        [Tooltip("Seconds the fake ad lasts (real time, so it works while the game is paused).")]
        [SerializeField, Min(0f)] private float adSeconds = 1f;

        [Tooltip("Off = every ad fails, to test the no-reward path.")]
        [SerializeField] private bool grantReward = true;

        private void OnEnable() => RewardedAds.Register(this);

        private void OnDisable() => RewardedAds.Unregister(this);

        public bool IsReady(string placement) => true;

        public void Show(string placement, Action<bool> onFinished) => StartCoroutine(Play(placement, onFinished));

        private IEnumerator Play(string placement, Action<bool> onFinished)
        {
            Debug.Log($"[Ads] Simulated rewarded ad '{placement}'.");
            yield return new WaitForSecondsRealtime(adSeconds);
            onFinished?.Invoke(grantReward);
        }
    }
}
