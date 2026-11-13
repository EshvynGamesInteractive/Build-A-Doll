using Core._GameEvent;
using _Core.UI;
using Core.Utilities;
using Game.Level;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    public class LevelFailedViewBehavior : UIScreen
    {
        [SerializeField] private Button retryCurrentLevelButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private TMP_Text outfitMatchText;

        private void OnEnable() => GameEvents.GameplayEvents.LevelFailed.Register(SetOutfitMatch);

        private void OnDisable() => GameEvents.GameplayEvents.LevelFailed.Unregister(SetOutfitMatch);

        private void SetOutfitMatch(LevelResult result)
        {
            if (outfitMatchText == null)
                return;

            outfitMatchText.gameObject.SetActive(result.Level.HasTarget);
            if (result.Level.HasTarget)
                outfitMatchText.text = $"Outfit Match {result.Match.Percent}%";
        }

        protected override void Initialize()
        {
            base.Initialize();
            
            UIPanelButtonUtility.Bind(retryCurrentLevelButton, OnRetryCurrentLevelButtonClick);
            UIPanelButtonUtility.Bind(homeButton, OnHomeButtonClick);
        }

        private void OnRetryCurrentLevelButtonClick() => GameEvents.GameplayEvents.RequestRestartLevel.Raise();

        private void OnHomeButtonClick() => GameEvents.UIEvents.ShowByType.Raise(ScreenType.MainMenu);
    }
}
