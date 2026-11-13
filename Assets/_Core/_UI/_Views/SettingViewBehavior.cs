using _Core.UI;
using Core.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    public class SettingViewBehavior : UIScreen
    {
        [SerializeField] private Button musicButton;
        [SerializeField] private Button soundsButton;
        [SerializeField] private Button vibrationButton;
        [SerializeField] private Button closeButton;

        
        protected override void Initialize()
        {
            base.Initialize();
            
            UIPanelButtonUtility.Bind(musicButton, OnMusicButtonClick);
            UIPanelButtonUtility.Bind(soundsButton, OnSoundsButtonClick);
            UIPanelButtonUtility.Bind(closeButton, OnCloseButtonClick);
            UIPanelButtonUtility.Bind(vibrationButton, OnVibrationButtonClick);
        }

        private void OnMusicButtonClick()
        {
            
        }

        private void OnSoundsButtonClick()
        {
            
        }

        private void OnVibrationButtonClick()
        {
            
        }

        private void OnCloseButtonClick()
        {
            
        }
    }
}
