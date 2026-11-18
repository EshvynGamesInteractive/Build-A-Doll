using _Core._Player;
using Game.Player.Features;
using UnityEngine;

namespace Game.Hurdles.Effects
{
    /// <summary>
    /// Colour gate, pet colour gate, paint pool or spray can: repaints the body, the clothes or the pets.
    /// A spray can can ignore a colour (renderers already that colour are left alone).
    /// Needs a DollPaint on the doll.
    /// </summary>
    public class PaintEffect : MonoBehaviour, IRunnerEffect, IGateInfo
    {
        [SerializeField] private PaintTarget target = PaintTarget.Clothes;
        [SerializeField] private Color color = new(1f, 0.4f, 0.7f);

        [Tooltip("Spray can: anything that is already this colour is left alone.")]
        [SerializeField] private bool ignoreColor;
        [SerializeField] private Color ignoredColor = Color.white;

        [Tooltip("Text for the gate label, e.g. \"PINK\". Empty = none.")]
        [SerializeField] private string label;
        [SerializeField] private bool isGood = true;

        [Tooltip("Optional: renderers on the gate / pool / can that show the paint colour (set up in the prefab).")]
        [SerializeField] private Renderer[] swatches;

        public string Label => label;
        public bool IsGood => isGood;

        private void Awake()
        {
            if (swatches == null)
                return;

            foreach (var swatch in swatches)
                RendererTint.Set(swatch, color);
        }

        public void Apply(RunnerController runner)
        {
            if (runner.TryGetFeature(out DollPaint paint))
                paint.Paint(target, color, ignoreColor, ignoredColor);
        }
    }
}
