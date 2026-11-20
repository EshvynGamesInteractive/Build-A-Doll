using System.Collections.Generic;
using Core._GameEvent;
using UnityEngine;

namespace Game.Outfit
{
    /// <summary>
    /// The doll's skin colour. Tints the body (and by default head) meshes with a MaterialPropertyBlock, so the shared
    /// atlas material is never changed. White = the texture's own colour. Set back to Start Color on ResetRun.
    /// Renderers are found by name (Gbody*, Ghead*) under this object when the list is empty.
    /// A Skin Gate adds this to the runner itself if the doll prefab doesn't have it.
    /// </summary>
    public class DollSkin : MonoBehaviour
    {
        [Tooltip("Body/head renderers to tint. Empty = found by the name prefixes below.")]
        [SerializeField] private List<Renderer> skinRenderers = new();
        [SerializeField] private string[] namePrefixes = { "Gbody", "Ghead" };

        [Tooltip("Colour at the start of every run. White = untinted.")]
        [SerializeField] private Color startColor = Color.white;

        [Tooltip("Seconds to blend to a new colour (0 = instant).")]
        [SerializeField, Min(0f)] private float blendTime = 0.35f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private MaterialPropertyBlock _block;
        private Color _current, _from, _to;
        private float _t = 1f;

        public Color Color => _to;

        private void Awake()
        {
            if (skinRenderers.Count == 0)
                FindRenderers();
            _current = _from = _to = startColor;
        }

        private void OnEnable() => GameEvents.GameplayEvents.ResetRun.Register(ResetSkin);
        private void OnDisable() => GameEvents.GameplayEvents.ResetRun.Unregister(ResetSkin);
        private void Start() => Apply(_current);

        /// <summary>Colour she starts every run with (and keeps after ResetRun), e.g. the demo doll's target skin.</summary>
        public void SetStartColor(Color color)
        {
            startColor = color;
            SetColor(color, true);
        }

        public void SetColor(Color color, bool instant = false)
        {
            _from = _current;
            _to = color;
            _t = instant || blendTime <= 0f ? 1f : 0f;
            if (_t >= 1f)
                Apply(color);
        }

        private void ResetSkin() => SetColor(startColor, true);

        private void Update()
        {
            if (_t >= 1f)
                return;
            _t = Mathf.Min(1f, _t + Time.deltaTime / blendTime);
            Apply(Color.Lerp(_from, _to, _t));
        }

        private void Apply(Color color)
        {
            _current = color;
            _block ??= new MaterialPropertyBlock();
            foreach (var r in skinRenderers)
            {
                if (r == null)
                    continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                _block.SetColor(ColorId, color);
                r.SetPropertyBlock(_block);
            }
        }

        [ContextMenu("Find Skin Renderers")]
        private void FindRenderers()
        {
            skinRenderers.Clear();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                foreach (var prefix in namePrefixes)
                {
                    if (!string.IsNullOrEmpty(prefix) && r.name.StartsWith(prefix))
                    {
                        skinRenderers.Add(r);
                        break;
                    }
                }
            }
        }
    }
}
