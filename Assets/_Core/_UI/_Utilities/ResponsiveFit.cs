using UnityEngine;

namespace Core.Utilities
{
    /// <summary>Uniformly fits a fixed-size UI group inside its parent without stretching its artwork.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ResponsiveFit : MonoBehaviour
    {
        [SerializeField] private Vector2 padding = new(64f, 64f);
        private RectTransform _rect;

        private void OnEnable()
        {
            _rect = (RectTransform)transform;
            Fit();
        }

        private void LateUpdate() => Fit();

        private void Fit()
        {
            if (_rect == null || _rect.parent is not RectTransform parent)
                return;
            var size = _rect.rect.size;
            if (size.x <= 0f || size.y <= 0f)
                return;
            var scale = Mathf.Clamp(Mathf.Min((parent.rect.width - padding.x) / size.x,
                (parent.rect.height - padding.y) / size.y), 0.01f, 1f);
            var target = new Vector3(scale, scale, 1f);
            if (_rect.localScale != target)
                _rect.localScale = target;
        }
    }
}
