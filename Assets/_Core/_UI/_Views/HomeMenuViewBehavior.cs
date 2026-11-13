using _Core.UI;
using Core._GameEvent;
using Core.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    public class HomeMenuViewBehavior : UIScreen
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;

        protected override void Initialize()
        {
            UIPanelButtonUtility.Bind(playButton, GameEvents.GameplayEvents.RequestPlaySavedLevel.Raise);
            UIPanelButtonUtility.Bind(settingsButton, () => GameEvents.UIEvents.ShowByType.Raise(ScreenType.Settings));
        }
    }
}
