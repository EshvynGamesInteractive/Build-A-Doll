using System.Collections;
using _Core._UI;
using _Core.UI;
using Core._GameEvent;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Core.Boot
{
    /// <summary>
    /// Lives in the Bootstrap scene. Shows the splash, loads the Gameplay scene in the background
    /// and switches to it once BOTH the minimum loading time has passed and the scene is ready.
    /// Progress goes out through UIEvents.LoadingProgress for the splash bar.
    /// Home (main menu) only shows once the Gameplay scene has reported in (GameplaySceneReady),
    /// so Play always has a scene to build the level in.
    /// Flow: Splash -> Gameplay scene loaded -> (first launch: Privacy Policy, Accept) -> Home -> Play -> level + player + demo doll -> Gameplay UI
    /// -> demo intro (pop, hold, move to corner) -> run starts.
    /// </summary>
    public class BootLoader : MonoBehaviour
    {
        [SerializeField] private string sceneToLoad = GameConstants.GamePlaySceneName;

        [Tooltip("Splash stays at least this long, even if the scene loads faster.")]
        [SerializeField, Min(0f)] private float loadingTime = 3f;

        [Tooltip("Turn off to start loading from code (call StartLoading).")]
        [SerializeField] private bool loadOnStart = true;

        [Tooltip("Longest wait for the Gameplay scene to report in before Home shows anyway.")]
        [SerializeField, Min(0f)] private float sceneReadyTimeout = 5f;

        private bool _isLoading;
        private bool _sceneReady;

        private void OnEnable() => GameEvents.GameplayEvents.GameplaySceneReady.Register(OnGameplaySceneReady);

        private void OnDisable() => GameEvents.GameplayEvents.GameplaySceneReady.Unregister(OnGameplaySceneReady);

        private void OnGameplaySceneReady(Game.Level.GameplayScene scene) => _sceneReady = true;

        private void Start()
        {
            if (loadOnStart)
                StartLoading();
        }

        private static bool PrivacyAccepted() => _Core._UI._Views.PrivacyPolicyViewBehavior.IsAccepted;

        public void StartLoading()
        {
            if (_isLoading)
                return;

            _isLoading = true;
            _sceneReady = false;
            GameEvents.UIEvents.ShowByType.Raise(ScreenType.SplashScreen);
            StartCoroutine(LoadRoutine());
        }

        private IEnumerator LoadRoutine()
        {
            var operation = SceneManager.LoadSceneAsync(sceneToLoad);
            if (operation == null)
            {
                Debug.LogError($"Couldn't load scene '{sceneToLoad}'. Is it in File > Build Profiles > Scene List?", this);
                _isLoading = false;
                yield break;
            }

            // Hold the scene back until the splash time is over.
            operation.allowSceneActivation = false;

            // Unity reports 0.9 when the scene is loaded and only waiting for activation.
            for (var elapsed = 0f; elapsed < loadingTime || operation.progress < 0.9f; elapsed += Time.unscaledDeltaTime)
            {
                var timeProgress = loadingTime > 0f ? elapsed / loadingTime : 1f;
                var loadProgress = operation.progress / 0.9f;
                GameEvents.UIEvents.LoadingProgress.Raise(Mathf.Clamp01(Mathf.Min(timeProgress, loadProgress)));
                yield return null;
            }

            GameEvents.UIEvents.LoadingProgress.Raise(1f);
            operation.allowSceneActivation = true;
            yield return operation;

            // GameplayScene raises GameplaySceneReady in its Start; wait for it so Play can't be pressed too early.
            for (var waited = 0f; !_sceneReady && waited < sceneReadyTimeout; waited += Time.unscaledDeltaTime)
                yield return null;

            if (!_sceneReady)
                Debug.LogWarning($"'{sceneToLoad}' never raised GameplaySceneReady. Is there a {nameof(Game.Level.GameplayScene)} in it?", this);

            // First launch: the privacy policy has to be accepted before Home.
            if (!PrivacyAccepted())
            {
                GameEvents.UIEvents.ShowByType.Raise(ScreenType.PrivacyPolicy);
                while (!PrivacyAccepted())
                    yield return null;
            }

            _isLoading = false;
            GameEvents.UIEvents.ShowByType.Raise(ScreenType.MainMenu);
        }
    }
}
