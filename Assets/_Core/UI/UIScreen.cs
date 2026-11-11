using UnityEngine;

namespace Core.UI
{
    public abstract class UIScreen : MonoBehaviour, IUIScreen
    {
        [SerializeField] private ScreenType screenType;
        public GameObject container;

        public ScreenType ScreenType => screenType;

        private void Start()
        {
            Initialize();
        }

        protected virtual void Initialize()
        {
            
        }

        public virtual void Show()
        {
            container?.SetActive(true);
        }

        public virtual void Hide()
        {
            container?.SetActive(false);
        }
    }
}
