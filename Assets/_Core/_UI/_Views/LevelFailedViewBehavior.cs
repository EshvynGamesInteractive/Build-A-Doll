using Core._GameEvent;
using _Core.UI;
using Core.Utilities;
using Game.Ads;
using Game.Level;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    public class LevelFailedViewBehavior : UIScreen
    {
        [SerializeField] private Button retryCurrentLevelButton;
        [Tooltip("Optional: back to the level map. Not in the current art.")]
        [SerializeField] private Button homeButton;
        [Tooltip("\"Watch Video To Skip Level\": rewarded ad → the level counts as passed (no coins) → map / next level.")]
        [SerializeField] private Button skipLevelButton;
        [Tooltip("On: after skipping, the level map opens with the next level as the current one. Off: the next level loads straight away.")]
        [SerializeField] private bool skipOpensLevelMap = true;
        [SerializeField] private string skipPlacement = RewardedAds.SkipLevel;
        [SerializeField] private TMP_Text outfitMatchText;

        private bool _waitingForAd;

        private void OnEnable() => GameEvents.GameplayEvents.LevelFailed.Register(OnLevelFailed);

        private void OnDisable() => GameEvents.GameplayEvents.LevelFailed.Unregister(OnLevelFailed);

        private void OnLevelFailed(LevelResult result)
        {
            _waitingForAd = false;
            SetButtonsInteractable(true);
            SetOutfitMatch(result);
        }

        private void SetOutfitMatch(LevelResult result)
        {
            if (outfitMatchText == null)
                return;

            var show = result.Level.HasTarget || result.Level.IsFreeStyle;
            outfitMatchText.gameObject.SetActive(show);
            if (result.Level.IsFreeStyle)
                outfitMatchText.text = $"Style {result.Score}%";
            else if (result.Level.HasTarget)
                outfitMatchText.text = $"Outfit Match {result.Match.Percent}%";
        }

        protected override void Initialize()
        {
            base.Initialize();

            UIPanelButtonUtility.Bind(retryCurrentLevelButton, OnRetryCurrentLevelButtonClick);
            UIPanelButtonUtility.Bind(homeButton, OnHomeButtonClick);
            UIPanelButtonUtility.Bind(skipLevelButton, OnSkipLevelButtonClick);
        }

        private void OnRetryCurrentLevelButtonClick()
        {
            if (_waitingForAd)
                return;
            GameEvents.GameplayEvents.RequestRestartLevel.Raise();
        }

        private void OnHomeButtonClick()
        {
            if (_waitingForAd)
                return;
            GameEvents.UIEvents.ShowByType.Raise(ScreenType.MainMenu);
        }

        private void OnSkipLevelButtonClick()
        {
            if (_waitingForAd)
                return;
            _waitingForAd = true;
            SetButtonsInteractable(false);
            RewardedAds.Show(skipPlacement, OnSkipRewarded, OnSkipAdFailed);
        }

        private void OnSkipRewarded()
        {
            _waitingForAd = false;
            SetButtonsInteractable(true);
            GameEvents.GameplayEvents.RequestSkipLevel.Raise();
            if (skipOpensLevelMap)
                GameEvents.UIEvents.ShowByType.Raise(ScreenType.MainMenu);
            else
                GameEvents.GameplayEvents.RequestNextLevel.Raise();
        }

        private void OnSkipAdFailed()
        {
            _waitingForAd = false;
            SetButtonsInteractable(true);
        }

        private void SetButtonsInteractable(bool on)
        {
            if (retryCurrentLevelButton != null)
                retryCurrentLevelButton.interactable = on;
            if (homeButton != null)
                homeButton.interactable = on;
            if (skipLevelButton != null)
                skipLevelButton.interactable = on;
        }
    }
}
