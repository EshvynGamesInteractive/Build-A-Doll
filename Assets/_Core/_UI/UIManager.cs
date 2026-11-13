using _Core._UI;
using _Core.UI;
using Core._GameEvent;
using UnityEngine;

namespace Core.UI
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [SerializeField] private UIScreen[] screens;

        private ScreenType _settingsReturn = ScreenType.MainMenu;
        private float? _resumeTimeScale;

        public ScreenType? Current { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            GameEvents.UIEvents.ShowByType.Register(Show);
            GameEvents.UIEvents.CloseSettings.Register(CloseSettings);
        }

        private void OnDisable()
        {
            GameEvents.UIEvents.ShowByType.Unregister(Show);
            GameEvents.UIEvents.CloseSettings.Unregister(CloseSettings);
            SetPaused(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Show(ScreenType type)
        {
            if (type == ScreenType.Settings && Current != ScreenType.Settings)
                _settingsReturn = Current ?? ScreenType.MainMenu;

            Current = type;
            SetPaused(type is ScreenType.PausePanel or ScreenType.Settings or ScreenType.MainMenu);

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

        private void CloseSettings() => Show(_settingsReturn);

        private void SetPaused(bool paused)
        {
            if (paused && !_resumeTimeScale.HasValue)
            {
                _resumeTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else if (!paused && _resumeTimeScale.HasValue)
            {
                Time.timeScale = _resumeTimeScale.Value;
                _resumeTimeScale = null;
            }
        }
    }
}
