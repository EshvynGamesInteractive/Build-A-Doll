using System.Collections.Generic;
using Core._GameEvent;
using UnityEngine;
using UnityEngine.UI;

namespace _Core.UI._Views
{
    public class SplashViewBehavior : UIScreen
    {
        [SerializeField] private Slider progressBar;
        [SerializeField] private Image titleImage;
        [SerializeField] private List<Image> dotImages = new();

        private void OnEnable() => GameEvents.UIEvents.LoadingProgress.Register(SetProgress);

        private void OnDisable() => GameEvents.UIEvents.LoadingProgress.Unregister(SetProgress);

        protected override void Initialize()
        {
            base.Initialize();
            SetProgress(0f);
        }

        private void SetProgress(float progress)
        {
            if (progressBar != null)
                progressBar.normalizedValue = progress;
        }
    }
}
