using System.Collections.Generic;
using Core._GameEvent;
using UnityEngine;
using UnityEngine.UI;

namespace _Core.UI._Views
{
    /// <summary>
    /// Splash / loading screen: progress bar fed by BootLoader (UIEvents.LoadingProgress),
    /// "Loading" dots lighting up one after another, title gently bobbing.
    /// Runs on unscaled time.
    /// </summary>
    public class SplashViewBehavior : UIScreen
    {
        [SerializeField] private Slider progressBar;
        [SerializeField] private Image titleImage;
        [SerializeField] private List<Image> dotImages = new();

        [Tooltip("Seconds per dot step.")]
        [SerializeField, Min(0.05f)] private float dotStep = 0.3f;
        [SerializeField, Range(0f, 1f)] private float dotOffAlpha = 0.25f;
        [Tooltip("Title scale pulse (0 = off).")]
        [SerializeField, Range(0f, 0.2f)] private float titlePulse = 0.04f;

        private Vector3 _titleScale = Vector3.one;
        private float _time;

        private void Awake()
        {
            if (titleImage != null)
                _titleScale = titleImage.rectTransform.localScale;
        }

        private void OnEnable() => GameEvents.UIEvents.LoadingProgress.Register(SetProgress);

        private void OnDisable() => GameEvents.UIEvents.LoadingProgress.Unregister(SetProgress);

        protected override void Initialize()
        {
            base.Initialize();
            SetProgress(0f);
        }

        public override void Show()
        {
            base.Show();
            _time = 0f;
        }

        private void Update()
        {
            if (container != null && !container.activeInHierarchy)
                return;

            _time += Time.unscaledDeltaTime;

            if (dotImages.Count > 0)
            {
                // 0 dots, 1, 2, … all, then again.
                var step = Mathf.FloorToInt(_time / dotStep) % (dotImages.Count + 1);
                for (var i = 0; i < dotImages.Count; i++)
                {
                    var dot = dotImages[i];
                    if (dot == null) continue;
                    var c = dot.color;
                    c.a = i < step ? 1f : dotOffAlpha;
                    dot.color = c;
                }
            }

            if (titleImage != null && titlePulse > 0f)
                titleImage.rectTransform.localScale = _titleScale * (1f + Mathf.Sin(_time * 3f) * titlePulse);
        }

        private void SetProgress(float progress)
        {
            if (progressBar != null)
                progressBar.normalizedValue = progress;
        }
    }
}
