using System.Collections;
using Game.Outfit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    /// <summary>
    /// One floating label over a doll on the finish stage (put it on a UI object you made in the prefab).
    /// Shows the item being compared (frame tinted green = she has it, red = she doesn't) and her match
    /// percentage, counting up. ComparisonHud moves it over the doll's head.
    /// The object's pivot is the point that sits just above her head (bottom-centre works well).
    /// </summary>
    public class ComparisonLabel : MonoBehaviour
    {
        [SerializeField] private TMP_Text percentText;
        [Tooltip("Optional: tinted green / red for each item.")]
        [SerializeField] private Graphic frame;
        [Tooltip("Optional: shows the icon of the item she wears in that slot (or the target item, faded, when she has none).")]
        [SerializeField] private Image icon;
        [Tooltip("Optional: hidden until the first item is compared (e.g. the frame + icon group).")]
        [SerializeField] private GameObject itemGroup;
        [SerializeField] private Slider progressBar;

        [SerializeField] private Color matchColor = new(0.35f, 0.85f, 0.35f, 1f);
        [SerializeField] private Color missColor = new(0.95f, 0.3f, 0.3f, 1f);
        [SerializeField, Range(0f, 1f)] private float missingItemAlpha = 0.35f;

        [Tooltip("Seconds the percentage takes to count up to its new value.")]
        [SerializeField, Min(0f)] private float countTime = 0.25f;
        [Tooltip("How much it pops (scale) each time the percentage changes.")]
        [SerializeField, Min(0f)] private float punch = 0.12f;

        private RectTransform _rect;
        private Transform _target;
        private float _headHeight;
        private int _shownPercent;
        private Coroutine _count;
        private Vector3 _baseScale = Vector3.one;

        public RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;
        public Transform Target => _target;
        public float HeadHeight => _headHeight;

        private void Awake()
        {
            _baseScale = transform.localScale;
            if (_target == null)
                gameObject.SetActive(false);
        }

        public void Attach(Transform target, float headHeight)
        {
            _target = target;
            _headHeight = headHeight;
            _shownPercent = 0;
            SetPercentText(0);
            if (itemGroup != null)
                itemGroup.SetActive(false);
            gameObject.SetActive(target != null);
        }

        public void Detach()
        {
            StopCount();
            _target = null;
            gameObject.SetActive(false);
        }

        public void ShowStep(OutfitItem worn, OutfitItem target, bool matched, int percent)
        {
            if (_target == null)
                return;

            if (itemGroup != null)
                itemGroup.SetActive(true);

            if (frame != null)
                frame.color = matched ? matchColor : missColor;

            if (icon != null)
            {
                var shown = worn != null ? worn : target;
                icon.sprite = shown != null ? shown.Icon : null;
                icon.enabled = icon.sprite != null;
                icon.color = worn != null ? Color.white : new Color(1f, 1f, 1f, missingItemAlpha);
            }

            StopCount();
            if (isActiveAndEnabled)
                _count = StartCoroutine(CountTo(percent));
            else
                SetPercentText(_shownPercent = percent);
        }

        private IEnumerator CountTo(int percent)
        {
            var from = _shownPercent;
            for (var t = 0f; t < 1f && countTime > 0f;)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / countTime);
                SetPercentText(Mathf.RoundToInt(Mathf.Lerp(from, percent, t)));
                transform.localScale = _baseScale * (1f + punch * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }

            _shownPercent = percent;
            SetPercentText(percent);
            transform.localScale = _baseScale;
            _count = null;
        }

        private void StopCount()
        {
            if (_count != null)
            {
                StopCoroutine(_count);
                _count = null;
            }

            transform.localScale = _baseScale;
        }

        private void SetPercentText(int percent)
        {
            if (percentText != null)
                percentText.text = $"{percent}%";
            if (progressBar != null)
                progressBar.normalizedValue = Mathf.Clamp01(percent / 100f);
        }
    }
}
