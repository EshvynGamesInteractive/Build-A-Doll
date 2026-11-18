using System.Collections;
using _Core._Player;
using Core._GameEvent;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// Collection animation for pickups: when the hurdle on this object is hit, 'Flyer' arcs into the doll
    /// and shrinks away. Comes back on ResetRun. The flyer must not be (or be inside) the hurdle's
    /// Active / Red / Green Visual, which hide the moment it's hit.
    /// </summary>
    public class CollectAnimation : MonoBehaviour
    {
        [Tooltip("What flies to the doll (e.g. the item's model).")]
        [SerializeField] private Transform flyer;
        [SerializeField, Min(0.05f)] private float duration = 0.45f;
        [SerializeField] private float arcHeight = 1f;

        [Tooltip("Point on the doll it flies to, from her feet.")]
        [SerializeField] private Vector3 targetOffset = new(0f, 1.2f, 0f);

        [SerializeField] private AnimationCurve scaleOverTime = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        private TrackHurdle _hurdle;
        private Vector3 _localPosition;
        private Quaternion _localRotation;
        private Vector3 _localScale;
        private Coroutine _routine;

        private void Awake()
        {
            _hurdle = GetComponent<TrackHurdle>();
            if (_hurdle == null)
                Debug.LogWarning($"{nameof(CollectAnimation)} on '{name}' needs a hurdle on the same object.", this);

            if (flyer == null)
                return;

            _localPosition = flyer.localPosition;
            _localRotation = flyer.localRotation;
            _localScale = flyer.localScale;
        }

        private void OnEnable()
        {
            if (_hurdle != null)
                _hurdle.PlayerHit += OnPlayerHit;
            GameEvents.GameplayEvents.ResetRun.Register(Restore);
        }

        private void OnDisable()
        {
            if (_hurdle != null)
                _hurdle.PlayerHit -= OnPlayerHit;
            GameEvents.GameplayEvents.ResetRun.Unregister(Restore);
        }

        private void OnPlayerHit(RunnerController runner)
        {
            if (flyer == null)
                return;

            if (_routine != null)
                StopCoroutine(_routine);
            _routine = StartCoroutine(Fly(runner.transform));
        }

        private IEnumerator Fly(Transform target)
        {
            var start = flyer.position;
            for (var elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                var t = elapsed / duration;
                var end = target != null ? target.position + targetOffset : start;
                flyer.position = Vector3.Lerp(start, end, t) + Vector3.up * (arcHeight * Mathf.Sin(t * Mathf.PI));
                flyer.localScale = _localScale * scaleOverTime.Evaluate(t);
                yield return null;
            }

            flyer.gameObject.SetActive(false);
            _routine = null;
        }

        private void Restore()
        {
            if (flyer == null)
                return;

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            flyer.SetLocalPositionAndRotation(_localPosition, _localRotation);
            flyer.localScale = _localScale;
            flyer.gameObject.SetActive(true);
        }
    }
}
