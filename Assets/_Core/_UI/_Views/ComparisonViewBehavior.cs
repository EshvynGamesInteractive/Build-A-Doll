using _Core.UI;
using Core._GameEvent;
using UnityEngine;

namespace _Core._UI._Views
{
    /// <summary>
    /// Finish screen (own panel since 25 Nov 2026, was part of GameplayMenu): the YOU / AI labels over the dolls and
    /// the full-screen touch blocker. The ComparisonHud on the same root drives them.
    /// Opens over the visible, non-interactive gameplay HUD when the finish sequence blocks input,
    /// then hands back to the Gameplay HUD
    /// when it ends; LevelManager then shows Level Complete / Level Failed.
    /// </summary>
    [RequireComponent(typeof(ComparisonHud))]
    public class ComparisonViewBehavior : UIScreen
    {
        private void OnEnable() => GameEvents.GameplayEvents.InputBlocked.Register(OnInputBlocked);

        private void OnDisable() => GameEvents.GameplayEvents.InputBlocked.Unregister(OnInputBlocked);

        private void OnInputBlocked(bool blocked)
        {
            var ui = UIManager.Instance;
            if (ui == null)
                return;

            if (blocked && ui.CurrentScreen == ScreenType.Gameplay)
                GameEvents.UIEvents.ShowByType.Raise(ScreenType.ComparisonPanel);
            else if (!blocked && ui.CurrentScreen == ScreenType.ComparisonPanel)
                GameEvents.UIEvents.ShowByType.Raise(ScreenType.Gameplay);
        }
    }
}
