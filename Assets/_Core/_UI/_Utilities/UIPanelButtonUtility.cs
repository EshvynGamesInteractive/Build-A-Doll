using Core._GameEvent;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Core.Utilities
{
    public static class UIPanelButtonUtility
    {
        public static void Bind(Button button, UnityAction action)
        {
            if (!button)
                return;

            button.onClick.AddListener(() =>
            {
                GameEvents.AudioEvents.ButtonClicked.Raise();
                action();
            });
        }
    }
}
