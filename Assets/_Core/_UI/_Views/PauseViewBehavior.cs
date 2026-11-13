using Core._GameEvent;
using _Core.UI;
using Core.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    public class PauseViewBehavior : UIScreen
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button levelRestartButton;
        [SerializeField] private Button homeButton;
        [SerializeField] private Button settingsButton;
        
        
        protected override void Initialize()
        {
            base.Initialize();
            
            UIPanelButtonUtility.Bind(resumeButton, OnResumeButtonClick);
            UIPanelButtonUtility.Bind(levelRestartButton, OnLevelRestartButtonClick);
            UIPanelButtonUtility.Bind(homeButton, OnHomeButtonClick);
            UIPanelButtonUtility.Bind(settingsButton, OnSettingsButtonClick);
        }

        private void OnResumeButtonClick() => GameEvents.UIEvents.ShowByType.Raise(ScreenType.Gameplay);

        private void OnLevelRestartButtonClick() => GameEvents.GameplayEvents.RequestRestartLevel.Raise();

        private void OnHomeButtonClick() => GameEvents.UIEvents.ShowByType.Raise(ScreenType.MainMenu);

        private void OnSettingsButtonClick() => GameEvents.UIEvents.ShowByType.Raise(ScreenType.Settings);
    }
}
