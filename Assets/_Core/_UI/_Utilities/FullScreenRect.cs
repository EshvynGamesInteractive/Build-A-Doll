using UnityEngine;

namespace Core.Utilities
{
    /// <summary>Extends a background or input blocker across the root canvas, outside safe-area padding.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class FullScreenRect : MonoBehaviour
    {
        private RectTransform _rect;
        private Canvas _canvas;
        private readonly Vector3[] _corners = new Vector3[4];

        private void OnEnable()
        {
            _rect = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            Fit();
        }

        private void LateUpdate() => Fit();

        private void Fit()
        {
            if (_canvas == null || _rect.parent is not RectTransform parent)
                return;

            ((RectTransform)_canvas.transform).GetWorldCorners(_corners);
            var min = parent.InverseTransformPoint(_corners[0]);
            var max = parent.InverseTransformPoint(_corners[2]);
            _rect.anchorMin = Vector2.zero;
            _rect.anchorMax = Vector2.one;
            _rect.offsetMin = (Vector2)min - parent.rect.min;
            _rect.offsetMax = (Vector2)max - parent.rect.max;
        }
    }
}
