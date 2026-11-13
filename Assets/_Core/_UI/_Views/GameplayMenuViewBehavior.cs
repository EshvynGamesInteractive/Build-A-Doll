using _Core.UI;
using Core._GameEvent;
using Core.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    public class GameplayMenuViewBehavior : UIScreen
    {
        [SerializeField] private Button pauseButton;

        protected override void Initialize()
        {
            UIPanelButtonUtility.Bind(pauseButton, () => GameEvents.UIEvents.ShowByType.Raise(ScreenType.PausePanel));
        }
    }
}
