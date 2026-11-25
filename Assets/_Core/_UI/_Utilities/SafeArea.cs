using UnityEngine;

namespace Core.Utilities
{
    /// <summary>Fits a direct child of the full-screen Canvas to the device's safe area.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastArea;
        private Vector2Int _lastScreenSize;

        private void OnEnable()
        {
            _rect = (RectTransform)transform;
            Apply(true);
        }

        private void Update() => Apply(false);

        private void Apply(bool force)
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            var area = Screen.safeArea;
            if (size.x <= 0 || size.y <= 0 || area.width <= 0f || area.height <= 0f)
                return;
            if (!force && size == _lastScreenSize && area == _lastArea)
                return;

            _lastScreenSize = size;
            _lastArea = area;
            _rect.anchorMin = new Vector2(Mathf.Clamp01(area.xMin / size.x), Mathf.Clamp01(area.yMin / size.y));
            _rect.anchorMax = new Vector2(Mathf.Clamp01(area.xMax / size.x), Mathf.Clamp01(area.yMax / size.y));
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
