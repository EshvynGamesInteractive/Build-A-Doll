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

        private void OnRetryCurrentLevelButtonClick()
        {
            
        }

        private void OnHomeButtonClick()
        {
            
        }

        private void OnNextLevelButtonClick()
        {
            
        }
    }
}
