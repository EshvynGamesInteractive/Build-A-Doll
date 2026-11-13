using UnityEngine;

namespace Game.Player.Movement
{
    /// <summary>
    /// Turns drag input into a sideways offset from the track centre, kept inside the track
    /// and smoothed so the doll glides after the finger instead of snapping.
    /// </summary>
    public class LateralMotion
    {
        public float Offset { get; private set; }
        public float Velocity => _velocity;

        /// <summary>Where the doll is heading sideways (before smoothing).</summary>
        public float TargetOffset => _targetOffset;

        private float _targetOffset;
        private float _velocity;

        public void AddDrag(float screenFraction, float sensitivity, float halfWidth)
        {
            _targetOffset = Mathf.Clamp(_targetOffset + screenFraction * sensitivity, -halfWidth, halfWidth);
        }

        public float Tick(float smoothTime, float deltaTime)
        {
            Offset = Mathf.SmoothDamp(Offset, _targetOffset, ref _velocity, smoothTime, Mathf.Infinity, deltaTime);
            return Offset;
        }

        public void Reset(float offset = 0f)
        {
            Offset = offset;
            _targetOffset = offset;
            _velocity = 0f;
        }
    }
}
