using UnityEngine;

namespace Core.Utilities
{
    /// <summary>Reserves space for a top or bottom banner inside the device safe area.</summary>
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class BannerLayout : MonoBehaviour
    {
        public enum Placement { Bottom, Top }

        [SerializeField] private RectTransform contentArea;
        [SerializeField] private RectTransform adBar;
        [SerializeField] private Placement placement;
        [SerializeField, Min(0f)] private float gap;

        private RectTransform _rect;

        private void OnEnable()
        {
            _rect = (RectTransform)transform;
            Refresh();
        }

        // Also detects SetActive, adaptive banner height, rotation and Inspector changes.
        private void Update() => Refresh();

        public void SetBannerVisible(bool visible)
        {
            if (adBar != null)
                adBar.gameObject.SetActive(visible);
            Refresh();
        }

        public void SetBannerAtTop(bool top)
        {
            placement = top ? Placement.Top : Placement.Bottom;
            Refresh();
        }

        /// <summary>Use the actual screen-pixel height reported by the ad SDK, not dp / points.</summary>
        public void SetBannerHeightPixels(float pixels)
        {
            if (adBar == null || float.IsNaN(pixels) || float.IsInfinity(pixels))
                return;
            var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            var scale = canvas != null ? Mathf.Max(0.001f, canvas.scaleFactor) : 1f;
            adBar.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0f, pixels) / scale);
            Refresh();
        }

        public void Refresh()
        {
            if (_rect == null)
                _rect = (RectTransform)transform;
            if (contentArea == null || adBar == null || _rect.rect.height <= 0f)
                return;

            var top = placement == Placement.Top;
            var anchor = new Vector2(0f, top ? 1f : 0f);
            adBar.anchorMin = anchor;
            adBar.anchorMax = new Vector2(1f, anchor.y);
            adBar.pivot = new Vector2(0.5f, anchor.y);
            adBar.anchoredPosition = Vector2.zero;
            adBar.sizeDelta = new Vector2(0f, adBar.sizeDelta.y);

            var height = adBar.gameObject.activeSelf
                ? Mathf.Max(0f, adBar.rect.height * Mathf.Abs(adBar.localScale.y)) + Mathf.Max(0f, gap)
                : 0f;
            var inset = Mathf.Clamp01(height / _rect.rect.height);
            contentArea.anchorMin = new Vector2(0f, top ? 0f : inset);
            contentArea.anchorMax = new Vector2(1f, top ? 1f - inset : 1f);
            contentArea.offsetMin = Vector2.zero;
            contentArea.offsetMax = Vector2.zero;
        }
    }
}
