using _Core._Player;
using Game.Hurdles.Effects;
using TMPro;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// A track element built from effects: put IRunnerEffect components (BodyScaleEffect, PaintEffect,
    /// StatusEffect...) on the same object and they all run when a runner touches it.
    /// Gates: body enhancer / regulator, colour, eyes, egg, roll, pet colour (pair two under a HurdleGroup).
    /// Pickups: wearables, scissors, new body material, kitchen items. Also paint pools, spray cans, fire,
    /// poop / dirt, bombs, crates, extinguishers, jump / slide / side-run patches and terrain patches.
    /// </summary>
    public class EffectHurdle : TrackHurdle, IAiChoice, IAiObstacle
    {
        public enum Display
        {
            [Tooltip("Green when every effect that has a say (IGateInfo) is good.")]
            Auto,
            Good,
            Bad
        }

        [Header("Effect")]
        [Tooltip("Shows the red or green visual. Auto = asks the effects.")]
        [SerializeField] private Display display = Display.Auto;

        [Tooltip("Harmful (fire, dirt, bomb, crate): a shield or invulnerable ride ignores it, and the AI tries to get past it.")]
        [SerializeField] private bool isHazard;

        [Tooltip("How the AI gets past it when it's a hazard.")]
        [SerializeField] private PenaltyItem.AiAvoidance aiAvoid = PenaltyItem.AiAvoidance.Auto;

        [Header("Label (optional)")]
        [Tooltip("World-space text on the gate, set up in the prefab. Shows the value, e.g. \"Head x2\".")]
        [SerializeField] private TMP_Text label;

        [Tooltip("Text for the label. Empty = taken from the effects (e.g. BodyScaleEffect).")]
        [SerializeField] private string labelText;

        private IRunnerEffect[] _effects;

        public bool IsGoodChoice { get; private set; }
        public bool OffersChoice => !isHazard;
        public bool AvoidByAi => isHazard;
        public PenaltyItem.AiAvoidance AiAvoid => aiAvoid;
        protected override bool IsHazard => isHazard;

        protected override void Awake()
        {
            base.Awake();
            _effects = GetComponents<IRunnerEffect>();
            if (_effects.Length == 0)
                Debug.LogWarning($"'{name}' has no effect components, so touching it does nothing.", this);
            RefreshDisplay();
        }

        /// <summary>Re-reads the effects' label and good / bad state, e.g. after the level configured them.</summary>
        public void RefreshDisplay()
        {
            IsGoodChoice = ResolveGood();
            SetChoiceMatch(IsGoodChoice);
            if (label != null)
                label.text = BuildLabel();
        }

        protected override void OnPlayerHit(RunnerController player) => ApplyEffects(player);

        protected void ApplyEffects(RunnerController player)
        {
            foreach (var effect in _effects)
                effect.Apply(player);
        }

        private bool ResolveGood()
        {
            if (display != Display.Auto)
                return display == Display.Good;

            foreach (var info in GetComponents<IGateInfo>())
            {
                if (!info.IsGood)
                    return false;
            }

            return true;
        }

        private string BuildLabel()
        {
            if (!string.IsNullOrEmpty(labelText))
                return labelText;

            foreach (var info in GetComponents<IGateInfo>())
            {
                if (!string.IsNullOrEmpty(info.Label))
                    return info.Label;
            }

            return string.Empty;
        }

        private void OnValidate()
        {
            if (label != null && !Application.isPlaying)
                label.text = BuildLabel();
        }
    }
}
