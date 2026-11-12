using _Core.UI;
using Core._GameEvent;
using UnityEngine;

namespace Core.UI
{
    /// <summary>
    /// Shows one screen at a time: on UIEvents.ShowByType the screen of that type is shown
    /// and the others are hidden. Screens are built in the Editor and listed here.
    /// A type with no screen (e.g. Gameplay) just hides them all.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [SerializeField] private UIScreen[] screens;

        public ScreenType? Current { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void OnEnable() => GameEvents.UIEvents.ShowByType.Register(Show);

        private void OnDisable() => GameEvents.UIEvents.ShowByType.Unregister(Show);

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Show(ScreenType type)
        {
            Current = type;

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
    }
}
