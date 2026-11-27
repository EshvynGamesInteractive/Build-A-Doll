using System.Collections;
using _Core._UI._Views;
using Core._GameEvent;
using UnityEngine;
using UnityEngine.Networking;

namespace _Core._UI
{
    /// <summary>
    /// Watches the internet connection and opens the No Internet panel (overlay, game paused) when it's gone.
    /// Checks only on menu screens (Home, Level Complete / Failed, Settings, Target Look) and when the app
    /// comes back from the background — never during the splash, the privacy policy or the run itself.
    /// Closes the panel by itself when the connection is back; OK on the panel checks again right away.
    /// Created automatically at startup (DontDestroyOnLoad) unless one already exists in a scene.
    /// </summary>
    public class InternetMonitor : MonoBehaviour
    {
        public static InternetMonitor Instance { get; private set; }

        [Tooltip("Turn off to never show the No Internet panel.")]
        [SerializeField] private bool requireInternet = true;

        [Tooltip("Seconds between checks while a menu screen is shown.")]
        [SerializeField, Min(1f)] private float checkInterval = 5f;

        [Tooltip("Also make a small web request (not just the network state). Catches Wi-Fi without internet.")]
        [SerializeField] private bool pingServer = true;

        [SerializeField] private string pingUrl = "https://clients3.google.com/generate_204";

        [SerializeField, Min(1)] private int pingTimeoutSeconds = 4;

        private bool _checking;
        private bool _retryPending;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (Instance != null || FindAnyObjectByType<InternetMonitor>() != null)
                return;
            new GameObject(nameof(InternetMonitor)).AddComponent<InternetMonitor>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable() => GameEvents.UIEvents.RetryInternet.Register(OnRetry);

        private void OnDisable() => GameEvents.UIEvents.RetryInternet.Unregister(OnRetry);

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Start() => StartCoroutine(Loop());

        private void OnApplicationPause(bool paused)
        {
            if (!paused && isActiveAndEnabled)
                StartCoroutine(CheckNow(false));
        }

        private IEnumerator Loop()
        {
            var wait = new WaitForSecondsRealtime(checkInterval);
            while (true)
            {
                yield return CheckNow(false);
                yield return wait;
            }
        }

        private void OnRetry()
        {
            _retryPending = true;
            StartCoroutine(CheckNow(true));
        }

        /// <summary>Is a screen showing where the panel may pop up (or is it already up)?</summary>
        private static bool CanShowNow(out bool panelUp)
        {
            panelUp = UIManager.IsShowing(ScreenType.NoInternetView);
            var current = UIManager.Instance != null ? UIManager.Instance.CurrentScreen : null;
            return panelUp || current is ScreenType.MainMenu or ScreenType.LevelCompletePanel
                or ScreenType.LevelFailedPanel or ScreenType.Settings or ScreenType.TargetLook;
        }

        private IEnumerator CheckNow(bool fromRetry)
        {
            if (!requireInternet)
            {
                if (UIManager.IsShowing(ScreenType.NoInternetView))
                    GameEvents.UIEvents.CloseOverlay.Raise();
                yield break;
            }

            if (_checking || !CanShowNow(out _))
                yield break;

            _checking = true;
            var online = Application.internetReachability != NetworkReachability.NotReachable;
            if (online && pingServer && !string.IsNullOrEmpty(pingUrl))
            {
                using var request = UnityWebRequest.Head(pingUrl);
                request.timeout = pingTimeoutSeconds;
                yield return request.SendWebRequest();
                online = request.result == UnityWebRequest.Result.Success;
            }
            _checking = false;

            if (!CanShowNow(out var panelUp))
                yield break;

            if (online)
            {
                if (panelUp)
                    GameEvents.UIEvents.CloseOverlay.Raise();
            }
            else if (!panelUp)
            {
                GameEvents.UIEvents.ShowByType.Raise(ScreenType.NoInternetView);
            }

            if (fromRetry || _retryPending)
            {
                _retryPending = false;
                if (!online)
                {
                    var view = FindAnyObjectByType<NoInternetViewBehavior>(FindObjectsInactive.Include);
                    if (view != null)
                        view.RetryFailed();
                }
            }
        }
    }
}
