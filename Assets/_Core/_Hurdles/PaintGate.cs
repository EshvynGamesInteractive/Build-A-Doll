using System;
using _Core._Player;
using Game.Outfit;
using TMPro;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// A gate that paints makeup on the doll's face when she runs through it: lips, eye shadow or blush, in one colour.
    /// Shows green when it's the target look's colour for that zone, red otherwise.
    /// Put two under a HurdleGroup for a left/right choice (see PaintGatePair). Levels can override each gate per entry.
    /// </summary>
    public class PaintGate : TrackHurdle
    {
        [Serializable]
        public struct Setting
        {
            public MakeupZone zone;
            [Tooltip("Shown on the gate, e.g. 'Pink'.")]
            public string colorName;
            public Color color;
        }

        [SerializeField] private MakeupZone zone = MakeupZone.Lips;
        [SerializeField] private string colorName = "Pink";
        [SerializeField] private Color color = new(1f, 0.4f, 0.85f);

        [Header("Look")]
        [Tooltip("Optional renderers tinted with the paint colour (e.g. a brush tip, a paint panel on the gate).")]
        [SerializeField] private Renderer[] swatches;
        [Tooltip("Optional. Left empty, a label is created in front of the gate.")]
        [SerializeField] private TMP_Text label;
        [SerializeField] private float labelHeight = 0.3f;
        [SerializeField, Min(0.1f)] private float labelFontSize = 10f;

        private TargetLook _look;
        private MaterialPropertyBlock _block;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public MakeupZone Zone => zone;
        public Color Color => color;
        public string ColorName => colorName;

        protected override void Awake()
        {
            base.Awake();
            if (label == null)
                label = GateLabel.Create(transform, labelHeight, labelFontSize, color);
            Refresh();
        }

        public void Configure(Setting setting, TargetLook look)
        {
            zone = setting.zone;
            colorName = setting.colorName;
            color = setting.color;
            Configure(look);
        }

        /// <summary>Keeps the prefab's zone and colour; only says which target look to judge green / red against.</summary>
        public void Configure(TargetLook look)
        {
            _look = look;
            Refresh();
        }

        /// <summary>True when the target look wants exactly this colour on this zone.</summary>
        public bool HelpsTarget(TargetLook look) =>
            look != null && look.TryGetMakeup(zone, out var wanted) && MakeupColor.Same(wanted.color, color);

        protected override void OnPlayerHit(RunnerController player)
        {
            var makeup = player.GetComponentInChildren<DollMakeup>();
            if (makeup == null)
            {
                makeup = player.gameObject.AddComponent<DollMakeup>(); // makes default zones on the head
                makeup.RaiseEvents = !player.IsAI;
            }
            makeup.Paint(zone, color);
        }

        public string Text => $"{(string.IsNullOrEmpty(colorName) ? "" : colorName.ToUpperInvariant() + " ")}{MakeupColor.ZoneName(zone)}";

        private void Refresh()
        {
            SetChoiceMatch(HelpsTarget(_look));

            if (label != null)
            {
                label.text = Text;
                label.color = color;
            }

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
            if (label == null)
                return;
            label.text = Text;
            label.color = color;
        }
    }
}
