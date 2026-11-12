using Core._GameEvent;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// Makes a hurdle move: slide side to side across the track, bob up and down, and/or spin.
    /// Works on anything: a single coin, a minus item, or a whole gate pair (put it on the parent).
    /// Movement is in local space around where it was placed, and restarts on ResetRun.
    /// Uses Update because the motion never stops; a mover with nothing to animate turns itself off.
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

        private const float TwoPi = Mathf.PI * 2f;

        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private Vector3 _slideOffset;
        private bool _slides;
        private bool _bobs;
        private bool _spins;
        private float _time;

        private void Awake()
        {
            _startPosition = transform.localPosition;
            _startRotation = transform.localRotation;

            // Work out once what this mover does, so Update does no extra checks.
            _slides = slideDistance > 0f && slideSpeed > 0f;
            _bobs = bobHeight > 0f && bobSpeed > 0f;
            _spins = spinSpeed != 0f;
            _slideOffset = _startRotation * (slideAxis.normalized * slideDistance);

            // Nothing to animate: no Update calls at all.
            if (!_slides && !_bobs && !_spins)
                enabled = false;
        }

        private void OnEnable() => GameEvents.GameplayEvents.ResetRun.Register(ResetMover);

        private void OnDisable() => GameEvents.GameplayEvents.ResetRun.Unregister(ResetMover);

        private void Update()
        {
            _time += Time.deltaTime;

            var position = _startPosition;
            if (_slides)
                position += _slideOffset * Mathf.Sin((_time * slideSpeed + phase) * TwoPi);
            if (_bobs)
                position.y += (Mathf.Sin((_time * bobSpeed + phase) * TwoPi) * 0.5f + 0.5f) * bobHeight;

            var rotation = _spins
                ? _startRotation * Quaternion.Euler(0f, spinSpeed * _time + phase * 360f, 0f)
                : _startRotation;

            transform.SetLocalPositionAndRotation(position, rotation);
        }

        private void ResetMover()
        {
            _time = 0f;
            transform.SetLocalPositionAndRotation(_startPosition, _startRotation);
        }

        private void OnDrawGizmosSelected()
        {
            if (slideDistance <= 0f)
                return;

            var parent = transform.parent;
            var rotation = Application.isPlaying ? _startRotation : transform.localRotation;
            var centre = Application.isPlaying ? _startPosition : transform.localPosition;
            var axis = rotation * slideAxis.normalized;

            if (parent != null)
            {
                axis = parent.TransformDirection(axis);
                centre = parent.TransformPoint(centre);
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(centre - axis * slideDistance, centre + axis * slideDistance);
        }
    }
}
