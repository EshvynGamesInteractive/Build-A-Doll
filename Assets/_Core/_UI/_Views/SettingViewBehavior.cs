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
        [SerializeField] private Button privacyPolicyButton;
        [SerializeField] private Button rateUsButton;

        [SerializeField] private Sprite buttonOnIcon;
        [SerializeField] private Sprite buttonOffIcon;
        
        [SerializeField] private Image musicButtonImage;
        [SerializeField] private Image soundsButtonImage;
        [SerializeField] private Image vibrationButtonImage;
        

        protected override void Initialize()
        {
            base.Initialize();
            UIPanelButtonUtility.Bind(musicButton, OnMusicButtonClick);
            UIPanelButtonUtility.Bind(soundsButton, OnSoundsButtonClick);
            UIPanelButtonUtility.Bind(vibrationButton, OnVibrationButtonClick);
            UIPanelButtonUtility.Bind(closeButton, GameEvents.UIEvents.CloseSettings.Raise);
            UIPanelButtonUtility.Bind(privacyPolicyButton, OnPrivacyPolicyButtonClick);
            UIPanelButtonUtility.Bind(rateUsButton, OnRateUsButtonClick);
            
            RefreshLabels();
        }

        public override void Show()
        {
            base.Show();
            RefreshLabels();
        }

        // Opens the Privacy Policy panel over Settings (Visit there opens the page; Accept / Close come back here).
        private void OnPrivacyPolicyButtonClick() =>
            GameEvents.UIEvents.ShowByType.Raise(ScreenType.PrivacyPolicy);

        private void OnRateUsButtonClick() =>
            OpenLink(GameConstants.RateUsUrl);

        private void OpenLink(string configuredUrl)
        {
            Application.OpenURL(configuredUrl);
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
            SetIcon(musicButtonImage, GameConstants.MusicKey);
            SetIcon(soundsButtonImage, GameConstants.SoundKey);
            SetIcon(vibrationButtonImage, GameConstants.VibrateKey);
        }

        private void SetIcon(Image image, string key)
        {
            if (image == null)
                return;

            var icon = GameSettings.IsEnabled(key) ? buttonOnIcon : buttonOffIcon;
            if (icon != null)
            {
                image.overrideSprite = null;
                image.sprite = icon;
            }
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
