using System;
using _Core._Player;
using Game.Outfit;
using TMPro;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// Dress colour gate: running through it recolours what she's wearing as her dress (the Dress slot, or Top + Bottom),
    /// e.g. pink / blue. The tint stays until she picks a new item for that slot. Put two under a HurdleGroup for a
    /// left/right choice (DressColorGatePair). Green when it's the level's Target Dress Colour (or always, with no target).
    /// Levels can override each gate's colour per entry (LevelData.Entry.dressColorGates).
    /// </summary>
    public class DressColorGate : TrackHurdle
    {
        [Serializable]
        public struct Setting
        {
            [Tooltip("Shown on the gate, e.g. 'Pink'.")]
            public string colorName;
            public Color color;
        }

        [SerializeField] private string colorName = "Pink";
        [SerializeField] private Color color = new(1f, 0.45f, 0.75f);
        [Tooltip("Slots recoloured if she wears them: the dress, or top and bottom.")]
        [SerializeField] private OutfitSlot[] slots = { OutfitSlot.Dress, OutfitSlot.Top, OutfitSlot.Bottom };

        [Header("Look")]
        [Tooltip("Renderers tinted with the colour (the swatch on the gate).")]
        [SerializeField] private Renderer[] swatches;
        [SerializeField] private TMP_Text label;

        private bool _hasTarget;
        private Color _target;
        private MaterialPropertyBlock _block;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        protected override bool AllowRuntimeObjects => false;

        public Color Color => color;
        public bool IsTargetChoice => _hasTarget && MakeupColor.Same(color, _target);

        protected override void Awake()
        {
            base.Awake();
            Refresh();
        }

        public void Configure(Setting? setting, bool hasTarget, Color target)
        {
            if (setting.HasValue)
            {
                colorName = setting.Value.colorName;
                color = setting.Value.color;
            }

            _hasTarget = hasTarget;
            _target = target;
            Refresh();
        }

        protected override void OnPlayerHit(RunnerController player)
        {
            var outfit = player.DollOutfit;
            if (outfit == null)
                return;
            foreach (var slot in slots)
                outfit.SetColor(slot, color);
        }

        private void Refresh()
        {
            SetChoiceMatch(!_hasTarget || IsTargetChoice); // no target colour = free choice, both green

            if (label != null)
                label.text = string.IsNullOrEmpty(colorName) ? "" : colorName.ToUpperInvariant();

            if (swatches == null)
                return;
            _block ??= new MaterialPropertyBlock();
            foreach (var r in swatches)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _block.SetColor(ColorId, color);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
