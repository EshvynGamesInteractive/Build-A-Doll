using _Core.UI;
using Core.Utilities;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    /// <summary>
    /// Makeup screen (own panel since 25 Nov 2026, was MakeupKitPaletteContainer inside GameplayMenu).
    /// The PaintToolView on the same root does the painting; it opens this screen through the UIManager when a makeup
    /// station / paint gate starts and goes back to the Gameplay HUD when the bar is full.
    /// Container = MakeupKitPaletteContainer (title, progress bar, brush, Free / Coins / AdWatch colour buttons).
    /// </summary>
    public class MakeupPanelViewBehavior : UIScreen
    {
        [Tooltip("Empty = the PaintToolView on this object.")]
        [SerializeField] private PaintToolView paintTool;
        [Tooltip("Top-left retry button: wipes the paint so far and starts again with the same colour.")]
        [SerializeField] private Button restartButton;

        protected override void Initialize()
        {
            if (paintTool == null)
                paintTool = GetComponent<PaintToolView>();
            if (paintTool == null)
                Debug.LogWarning($"{name}: no {nameof(PaintToolView)} found, the makeup screen can't paint.", this);

            UIPanelButtonUtility.Bind(restartButton, () =>
            {
                if (paintTool != null)
                    paintTool.RestartPainting();
            });
        }
    }
}
