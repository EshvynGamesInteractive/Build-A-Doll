using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace _Core.UI._Views
{
    public class SplashViewBehavior : UIScreen
    {
        [SerializeField] private Slider progressBar;
        [SerializeField] private Image titleImage;
        [SerializeField] private List<Image> dotImages = new();

        protected override void Initialize()
        {
            base.Initialize();
            
        }
    }
}
