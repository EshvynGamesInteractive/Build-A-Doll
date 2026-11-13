using Core._GameEvent;
using _Core.UI;
using Core.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    public class LevelFailedViewBehavior : UIScreen
    {
        [SerializeField] private Button retryCurrentLevelButton;
        [SerializeField] private Button homeButton;

        
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
