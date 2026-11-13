
using _Core.UI;
using Core._GameEvent;
using UnityEngine;

namespace _Core._UI
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [SerializeField] private UIScreen[] screens;

        private ScreenType settingsReturn = ScreenType.MainMenu;
        private float? resumeTimeScale;

        private ScreenType? Current { get; set; }

        private void Awake()
        {
            // Singleton check
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Keep root parent alive between scenes
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (Instance != this)
                return;

            GameEvents.UIEvents.ShowByType.Register(Show);
            GameEvents.UIEvents.CloseSettings.Register(CloseSettings);
        }

        private void OnDisable()
        {
            if (Instance != this)
                return;

            GameEvents.UIEvents.ShowByType.Unregister(Show);
            GameEvents.UIEvents.CloseSettings.Unregister(CloseSettings);

            SetPaused(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Show(ScreenType type)
        {
            if (type == ScreenType.Settings && Current != ScreenType.Settings)
                settingsReturn = Current ?? ScreenType.MainMenu;

            Current = type;

            SetPaused(
                type is ScreenType.PausePanel
                    or ScreenType.Settings
                    or ScreenType.MainMenu
            );

            foreach (var screen in screens)
            {
                if (screen == null)
                    continue;

                if (screen.ScreenType == type)
                    screen.Show();
                else
                    screen.Hide();
            }
        }

        private void CloseSettings() => Show(settingsReturn);

        private void SetPaused(bool paused)
        {
            if (paused && !resumeTimeScale.HasValue)
            {
                resumeTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else if (!paused && resumeTimeScale.HasValue)
            {
                Time.timeScale = resumeTimeScale.Value;
                resumeTimeScale = null;
            }
        }
    }
}
