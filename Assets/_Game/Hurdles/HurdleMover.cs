using Core._GameEvent;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// Makes a hurdle move: slide side to side across the track, bob up and down, and/or spin.
    /// Works on anything: a single coin, a minus item, or a whole gate pair (put it on the parent).
    /// Movement is in local space around where it was placed, and restarts on ResetRun.
    /// </summary>
    public class HurdleMover : MonoBehaviour
    {
        [Header("Slide (side to side)")]
        [Tooltip("How far it slides each way from where it was placed. 0 = no slide.")]
        [SerializeField, Min(0f)] private float slideDistance = 0f;
        [Tooltip("Full left-right-left cycles per second.")]
        [SerializeField, Min(0f)] private float slideSpeed = 0.5f;
        [SerializeField] private Vector3 slideAxis = Vector3.right;

        [Header("Bob (up and down)")]
        [SerializeField, Min(0f)] private float bobHeight = 0f;
        [SerializeField, Min(0f)] private float bobSpeed = 1f;

        [Header("Spin")]
        [Tooltip("Degrees per second around the local up axis. 0 = no spin.")]
        [SerializeField] private float spinSpeed = 0f;

        [Header("Timing")]
        [Tooltip("0..1 offset in the cycle, so a row of movers doesn't move in lockstep.")]
        [SerializeField, Range(0f, 1f)] private float phase = 0f;

        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private float _time;

        private void Awake()
        {
            _startPosition = transform.localPosition;
            _startRotation = transform.localRotation;
        }

        private void OnEnable() => GameEvents.GameplayEvents.ResetRun.Register(ResetMover);

        private void OnDisable() => GameEvents.GameplayEvents.ResetRun.Unregister(ResetMover);

        private void Update()
        {
            _time += Time.deltaTime;

            var offset = Vector3.zero;

            if (slideDistance > 0f)
                offset += slideAxis.normalized * (Mathf.Sin(Cycle(slideSpeed)) * slideDistance);

            if (bobHeight > 0f)
                offset += Vector3.up * ((Mathf.Sin(Cycle(bobSpeed)) * 0.5f + 0.5f) * bobHeight);

            transform.localPosition = _startPosition + _startRotation * offset;

            if (spinSpeed != 0f)
                transform.localRotation = _startRotation * Quaternion.Euler(0f, spinSpeed * _time + phase * 360f, 0f);
        }

        private float Cycle(float cyclesPerSecond) => (_time * cyclesPerSecond + phase) * Mathf.PI * 2f;

        private void ResetMover()
        {
            _time = 0f;
            transform.localPosition = _startPosition;
            transform.localRotation = _startRotation;
        }

        private void OnDrawGizmosSelected()
        {
            if (slideDistance <= 0f)
                return;

            var axis = transform.parent != null
                ? transform.parent.TransformDirection((Application.isPlaying ? _startRotation : transform.localRotation) * slideAxis.normalized)
                : slideAxis.normalized;
            var centre = Application.isPlaying && transform.parent != null ? transform.parent.TransformPoint(_startPosition) : transform.position;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(centre - axis * slideDistance, centre + axis * slideDistance);
        }
    }
}
