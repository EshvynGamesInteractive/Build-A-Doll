using _Core.UI;
using Core._GameEvent;
using Core.Utilities;
using TMPro;
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
            UIPanelButtonUtility.Bind(vibrationButton, OnVibrationButtonClick);
            UIPanelButtonUtility.Bind(closeButton, GameEvents.UIEvents.CloseSettings.Raise);
            
            RefreshLabels();
        }

        private void OnMusicButtonClick()
        {
            var toggle = Toggle(GameConstants.MusicKey);
            GameEvents.AudioEvents.SetMusicVolume.Raise(toggle ? 1f : 0f);
            RefreshLabels();
        }

        private void OnSoundsButtonClick()
        {
            var toggle = Toggle(GameConstants.SoundKey);
            GameEvents.AudioEvents.SetSoundVolume.Raise(toggle ? 1f : 0f);
            RefreshLabels();
            if (toggle)
                GameEvents.AudioEvents.ButtonClicked.Raise();
        }

        private void OnVibrationButtonClick()
        {
            if (Toggle(GameConstants.VibrateKey))
                GameSettings.Vibrate();
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            SetLabel(musicButton, "Music", GameConstants.MusicKey);
            SetLabel(soundsButton, "Sounds", GameConstants.SoundKey);
            SetLabel(vibrationButton, "Vibration", GameConstants.VibrateKey);
        }

        private static void SetLabel(Button button, string label, string key)
        {
            if (button != null && button.GetComponentInChildren<TMP_Text>(true) is { } text)
                text.text = label + (GameSettings.IsEnabled(key) ? " (On)" : " (Off)");
        }

        private static bool Toggle(string key)
        {
            var enabled = !GameSettings.IsEnabled(key);
            PlayerPrefs.SetInt(key, enabled ? 1 : 0);
            PlayerPrefs.Save();
            return enabled;
        }
    }
}
