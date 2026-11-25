using _Core.UI;
using Core._GameEvent;
using UnityEngine;

namespace _Core._UI
{
    /// <summary>
    /// Shows one screen at a time, with the comparison panel layered over the gameplay HUD.
    /// Every panel is its own prefab under the Canvas
    /// with a UIScreen on its root; the root stays active and only its Container is shown / hidden.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [SerializeField] private UIScreen[] screens;

        private ScreenType settingsReturn = ScreenType.MainMenu;
        private float? resumeTimeScale;

        private ScreenType? Current { get; set; }

        /// <summary>The screen shown right now (null before the first one).</summary>
        public ScreenType? CurrentScreen => Current;

        /// <summary>True when the UIManager exists and is showing this screen.</summary>
        public static bool IsShowing(ScreenType type) => Instance != null && Instance.Current == type;

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

            var found = false;
            foreach (var screen in screens)
            {
                if (screen == null)
                    continue;

                var isRequestedScreen = screen.ScreenType == type;
                var keepGameplayHud = type == ScreenType.ComparisonPanel && screen.ScreenType == ScreenType.Gameplay;
                if (isRequestedScreen || keepGameplayHud)
                {
                    screen.Show();
                    found |= isRequestedScreen;
                }
                else
                {
                    screen.Hide();
                }
            }

            if (!found)
                Debug.LogWarning($"[UIManager] No screen of type {type} in the Screens list (run Build A Doll > UI > Split Gameplay UI Into Panels).", this);
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
