using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// Animates a gate's gradient sprite (or a ring).
    ///  Rise:  the sprite drifts upward and fades in and out, looping; extra copies follow half a cycle
    ///         behind so the flow never stops (the "energy rising" look of runner-game gates).
    ///  Pulse: the sprite gently grows/shrinks and brightens/dims (good for rings).
    /// Put it on the SpriteRenderer object (e.g. "AnimatedGradient").
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteGradientAnimator : MonoBehaviour
    {
        public enum Mode
        {
            Rise,
            Pulse
        }

        [SerializeField] private Mode mode = Mode.Rise;

        [Tooltip("Drawn this many steps above other sprites at the same spot (e.g. the gate's static gradient), so it isn't hidden behind them.")]
        [SerializeField] private int sortingOrderBoost = 1;

        [Tooltip("Pushed this far towards the camera side (-Z) so it sits in front of the static gradient.")]
        [SerializeField] private float frontOffset = 0.02f;

        [Tooltip("Seconds per loop.")]
        [SerializeField, Min(0.05f)] private float duration = 1.2f;

        [Header("Rise")]
        [Tooltip("How far up it drifts per loop (in the parent's units).")]
        [SerializeField] private float riseDistance = 0.6f;

        [Tooltip("How many copies are in the flow at once.")]
        [SerializeField, Range(1, 3)] private int layers = 2;

        [Header("Pulse")]
        [SerializeField, Range(0f, 0.5f)] private float pulseScale = 0.08f;
        [SerializeField, Range(0f, 1f)] private float minAlpha = 0.55f;

        private SpriteRenderer _renderer;
        private SpriteRenderer[] _flow;
        private Vector3 _basePosition;
        private Vector3 _baseScale;
        private Color _baseColor;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _renderer.sortingOrder += sortingOrderBoost;
            transform.localPosition += Vector3.back * frontOffset;
            _basePosition = transform.localPosition;
            _baseScale = transform.localScale;
            _baseColor = _renderer.color;

            _flow = new SpriteRenderer[mode == Mode.Rise ? layers : 1];
            _flow[0] = _renderer;
            for (var i = 1; i < _flow.Length; i++)
                _flow[i] = MakeCopy(i);
        }

        // A plain copy of this sprite next to it (same parent), driven by this component.
        private SpriteRenderer MakeCopy(int index)
        {
            var go = new GameObject($"{name}_Flow{index}");
            go.layer = gameObject.layer;
            go.transform.SetParent(transform.parent, false);
            go.transform.localRotation = transform.localRotation;
            go.transform.localScale = _baseScale;

            var copy = go.AddComponent<SpriteRenderer>();
            copy.sprite = _renderer.sprite;
            copy.sharedMaterial = _renderer.sharedMaterial;
            copy.color = _baseColor;
            copy.flipX = _renderer.flipX;
            copy.flipY = _renderer.flipY;
            copy.drawMode = _renderer.drawMode;
            copy.size = _renderer.size;
            copy.sortingLayerID = _renderer.sortingLayerID;
            copy.sortingOrder = _renderer.sortingOrder;
            return copy;
        }

        private void Update()
        {
            var t = Time.time / duration;

            if (mode == Mode.Pulse)
            {
                var wave = Mathf.Sin(t * Mathf.PI * 2f);
                transform.localScale = _baseScale * (1f + pulseScale * wave);
                SetAlpha(_renderer, Mathf.Lerp(minAlpha, 1f, (wave + 1f) * 0.5f));
                return;
            }

            for (var i = 0; i < _flow.Length; i++)
            {
                var sprite = _flow[i];
                if (sprite == null)
                    continue;

                var phase = Mathf.Repeat(t + (float)i / _flow.Length, 1f);
                sprite.transform.localPosition = _basePosition + Vector3.up * (riseDistance * phase);
                SetAlpha(sprite, Mathf.Sin(phase * Mathf.PI)); // fades in, then out
            }
        }

        private void SetAlpha(SpriteRenderer sprite, float alpha)
        {
            var color = _baseColor;
            color.a *= alpha;
            sprite.color = color;
        }

        private void OnDisable()
        {
            if (_renderer == null)
                return;

            transform.localPosition = _basePosition;
            transform.localScale = _baseScale;
            _renderer.color = _baseColor;
        }

        private void OnDestroy()
        {
            if (_flow == null)
                return;

            for (var i = 1; i < _flow.Length; i++)
            {
                if (_flow[i] != null)
                    Destroy(_flow[i].gameObject);
            }
        }
    }
}
