using System;
using _Core._Player;
using Game.Outfit;
using TMPro;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// Skin colour gate: running through it turns the doll's body this colour (DollSkin).
    /// Put two under a HurdleGroup for a left/right choice (SkinGatePair). Green when it's the level's Target Skin.
    /// Levels can override each gate's colour per entry (LevelData.Entry.skinGates).
    /// </summary>
    public class SkinGate : TrackHurdle
    {
        [Serializable]
        public struct Setting
        {
            [Tooltip("Shown on the gate, e.g. 'Tan'.")]
            public string colorName;
            public Color color;
        }

        [SerializeField] private string colorName = "Tan";
        [SerializeField] private Color color = new(0.87f, 0.68f, 0.52f);

        [Header("Look")]
        [Tooltip("Renderers tinted with the skin colour (the swatch on the gate).")]
        [SerializeField] private Renderer[] swatches;
        [Tooltip("Optional label, e.g. 'TAN'.")]
        [SerializeField] private TMP_Text label;

        private bool _hasTarget;
        private Color _target;
        private MaterialPropertyBlock _block;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        protected override bool AllowRuntimeObjects => false;

        public Color Color => color;
        public string ColorName => colorName;
        public bool IsTargetChoice => _hasTarget && MakeupColor.Same(color, _target);

        protected override void Awake()
        {
            base.Awake();
            Refresh();
        }

        /// <summary>Per-level colour (optional) and the level's target skin (green/red).</summary>
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
            var skin = player.GetComponentInChildren<DollSkin>();
            if (skin == null)
                skin = player.gameObject.AddComponent<DollSkin>(); // finds Gbody*/Ghead* renderers itself
            skin.SetColor(color);
        }

        private void Refresh()
        {
            // No target skin = free choice: both gates show green.
            SetChoiceMatch(!_hasTarget || IsTargetChoice);

            if (label != null)
                label.text = string.IsNullOrEmpty(colorName) ? "" : colorName.ToUpperInvariant();

            if (swatches == null)
                return;
            _block ??= new MaterialPropertyBlock();
            foreach (var r in swatches)
            {
                if (r == null)
                    continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _block.SetColor(ColorId, color);
                r.SetPropertyBlock(_block);
            }
        }

        private void OnValidate()
        {
            if (label != null)
                label.text = string.IsNullOrEmpty(colorName) ? "" : colorName.ToUpperInvariant();
        }
    }
}
