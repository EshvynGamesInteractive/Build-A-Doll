using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Core.Utilities
{
    public static class UIPanelButtonUtility
    {
        public static Button FindButton(Component owner, string buttonName)
        {
            if (!owner)
                return null;

            foreach (var button in owner.GetComponentsInChildren<Button>(true))
            {
                if (button.name == buttonName)
                    return button;
            }

            Debug.LogWarning($"Button '{buttonName}' was not found under '{owner.name}'.", owner);
            return null;
        }

        public static void Bind(Button button, UnityAction action)
        {
            if (!button)
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }
}
