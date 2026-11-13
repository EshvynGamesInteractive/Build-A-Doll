using Core._GameEvent;
using _Core.UI;
using Core.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    public class LevelCompleteViewBehavior : UIScreen
    {
        [SerializeField] private Button retryButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private Button nextLevelButton;
        
        
        protected override void Initialize()
        {
            base.Initialize();
            
            UIPanelButtonUtility.Bind(retryButton, OnRetryCurrentLevelButtonClick);
            UIPanelButtonUtility.Bind(homeButton, OnHomeButtonClick);
            UIPanelButtonUtility.Bind(nextLevelButton, OnNextLevelButtonClick);
        }

        private void OnRetryCurrentLevelButtonClick() => GameEvents.GameplayEvents.RequestRestartLevel.Raise();

        private void OnHomeButtonClick() => GameEvents.UIEvents.ShowByType.Raise(ScreenType.MainMenu);

        private void OnNextLevelButtonClick() => GameEvents.GameplayEvents.RequestNextLevel.Raise();
    }
}
