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
        private ScreenType? overlayReturn;
        private float? resumeTimeScale;

        private ScreenType? Current { get; set; }

        /// <summary>The screen shown right now (null before the first one).</summary>
        public ScreenType? CurrentScreen => Current;

        /// <summary>True when the UIManager exists and is showing this screen.</summary>
        public static bool IsShowing(ScreenType type) => Instance != null && Instance.Current == type;

        /// <summary>
        /// Overlays open on top of the current screen: that screen stays visible underneath and comes back
        /// when the overlay closes (UIEvents.CloseOverlay).
        /// </summary>
        public static bool IsOverlay(ScreenType type) =>
            type is ScreenType.NoInternetView or ScreenType.PrivacyPolicy;

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
            GameEvents.UIEvents.CloseOverlay.Register(CloseOverlay);
        }

        private void OnDisable()
        {
            if (Instance != this)
                return;

            GameEvents.UIEvents.ShowByType.Unregister(Show);
            GameEvents.UIEvents.CloseSettings.Unregister(CloseSettings);
            GameEvents.UIEvents.CloseOverlay.Unregister(CloseOverlay);

            SetPaused(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Show(ScreenType type)
        {
            // Coming back from an overlay must not overwrite where Settings / the overlay return to.
            var fromOverlay = Current.HasValue && IsOverlay(Current.Value);

            if (type == ScreenType.Settings && Current != ScreenType.Settings && !fromOverlay)
                settingsReturn = Current ?? ScreenType.MainMenu;

            if (IsOverlay(type))
            {
                if (!fromOverlay)
                    overlayReturn = Current;
            }
            else
            {
                overlayReturn = null;
            }

            Current = type;

            SetPaused(
                type is ScreenType.PausePanel
                    or ScreenType.Settings
                    or ScreenType.MainMenu
                    or ScreenType.NoInternetView
                    or ScreenType.PrivacyPolicy
            );

            var found = false;
            foreach (var screen in screens)
            {
                if (screen == null)
                    continue;

                var isRequestedScreen = screen.ScreenType == type;
                var keepGameplayHud = (type == ScreenType.ComparisonPanel || (IsOverlay(type) && overlayReturn == ScreenType.ComparisonPanel))
                                      && screen.ScreenType == ScreenType.Gameplay;
                var keepUnderOverlay = IsOverlay(type) && overlayReturn == screen.ScreenType;
                if (isRequestedScreen || keepGameplayHud || keepUnderOverlay)
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

        private void CloseOverlay()
        {
            if (Current.HasValue && IsOverlay(Current.Value))
                Show(overlayReturn ?? ScreenType.MainMenu);
        }

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
