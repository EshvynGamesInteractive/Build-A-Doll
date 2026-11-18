using System.Collections.Generic;
using _Core._Player;
using Core._GameEvent;
using UnityEngine;

namespace Game.Player.Features
{
    /// <summary>
    /// Recolours the doll's body, her clothes or her pets (colour gates, paint pools, spray cans,
    /// pet colour gates). Uses property blocks, so material assets never change. Colours are cleared
    /// on ResetRun. Clothes put on after painting keep their own colour.
    /// </summary>
    [DisallowMultipleComponent]
    public class DollPaint : MonoBehaviour
    {
        [Tooltip("Body / skin renderers repainted by body colour gates and paint pools.")]
        [SerializeField] private Renderer[] bodyRenderers;

        [Tooltip("How close two colours must be to count as the same (for spray cans that ignore a colour).")]
        [SerializeField, Range(0f, 0.5f)] private float sameColorTolerance = 0.08f;

        private readonly HashSet<Renderer> _painted = new();
        private readonly List<Renderer> _buffer = new();
        private RunnerController _runner;

        private void Awake() => _runner = GetComponentInParent<RunnerController>();

        private void OnEnable() => GameEvents.GameplayEvents.ResetRun.Register(ResetColors);

        private void OnDisable() => GameEvents.GameplayEvents.ResetRun.Unregister(ResetColors);

        public void Paint(PaintTarget target, Color color) => Paint(target, color, false, default);

        /// <summary>Paints 'target'. With 'skipSameColor', renderers that already are 'skipColor' are left alone.</summary>
        public void Paint(PaintTarget target, Color color, bool skipSameColor, Color skipColor)
        {
            _buffer.Clear();
            CollectRenderers(target, _buffer);

            foreach (var renderer in _buffer)
            {
                if (renderer == null)
                    continue;

                if (skipSameColor && IsSameColor(RendererTint.Get(renderer), skipColor))
                    continue;

                RendererTint.Set(renderer, color);
                _painted.Add(renderer);
            }
        }

        public void ResetColors()
        {
            foreach (var renderer in _painted)
                RendererTint.Clear(renderer);
            _painted.Clear();
        }

        private void CollectRenderers(PaintTarget target, List<Renderer> results)
        {
            switch (target)
            {
                case PaintTarget.Body:
                    if (bodyRenderers != null)
                        results.AddRange(bodyRenderers);
                    break;
                case PaintTarget.Clothes:
                    if (_runner != null && _runner.DollOutfit != null)
                        _runner.DollOutfit.GetItemRenderers(results);
                    break;
                case PaintTarget.Pet:
                    if (_runner.TryGetFeature(out RunnerCompanions companions))
                        companions.GetRenderers(results);
                    if (_runner.TryGetFeature(out RunnerMount mount))
                        mount.GetRenderers(results);
                    break;
            }
        }

        private bool IsSameColor(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) <= sameColorTolerance &&
            Mathf.Abs(a.g - b.g) <= sameColorTolerance &&
            Mathf.Abs(a.b - b.b) <= sameColorTolerance;
    }
}
