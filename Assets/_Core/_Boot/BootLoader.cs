using System.Collections;
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
    /// The level and player are set up by LevelManager when the Gameplay scene reports in.
    /// </summary>
    public class BootLoader : MonoBehaviour
    {
        [SerializeField] private string sceneToLoad = GameConstants.GamePlaySceneName;

        [Tooltip("Splash stays at least this long, even if the scene loads faster.")]
        [SerializeField, Min(0f)] private float loadingTime = 3f;

        [Tooltip("Turn off to start loading from code (call StartLoading).")]
        [SerializeField] private bool loadOnStart = true;

        private bool _isLoading;

        private void Start()
        {
            if (loadOnStart)
                StartLoading();
        }

        public void StartLoading()
        {
            if (_isLoading)
                return;

            _isLoading = true;
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

            GameEvents.UIEvents.ShowByType.Raise(ScreenType.Gameplay);
            _isLoading = false;
        }
    }
}
