using System.Collections.Generic;
using UnityEngine;

namespace Game.Player.Movement
{
    /// <summary>
    /// Temporary changes that track elements put on the runner: hold her in place, lock steering,
    /// change her speed, lift her off the track or boost her forward. Every change is keyed by its
    /// source (the hurdle or feature that asked), so several can overlap and each removes only its own.
    /// Everything is cleared on ResetRun.
    /// </summary>
    public class RunnerModifiers
    {
        private readonly HashSet<object> _holds = new();
        private readonly HashSet<object> _steeringLocks = new();
        private readonly Dictionary<object, float> _speedFactors = new();
        private readonly Dictionary<object, float> _heightOffsets = new();

        private float _speedFactor = 1f;
        private float _boostSpeed;
        private float _boostTimer;

        public bool IsHeld => _holds.Count > 0;
        public bool IsSteeringLocked => _steeringLocks.Count > 0;
        public bool IsBoosting => _boostTimer > 0f;
        public float BoostSpeed => _boostSpeed;

        /// <summary>All speed factors multiplied together.</summary>
        public float SpeedFactor => _speedFactor;

        /// <summary>Current (smoothed) height above the track.</summary>
        public float Height { get; private set; }

        public void Hold(object source) => _holds.Add(source);
        public void Release(object source) => _holds.Remove(source);

        public void LockSteering(object source) => _steeringLocks.Add(source);
        public void UnlockSteering(object source) => _steeringLocks.Remove(source);

        public void SetSpeedFactor(object source, float factor)
        {
            _speedFactors[source] = Mathf.Max(0f, factor);
            RecalculateSpeedFactor();
        }

        public void ClearSpeedFactor(object source)
        {
            if (_speedFactors.Remove(source))
                RecalculateSpeedFactor();
        }

        public void SetHeightOffset(object source, float height) => _heightOffsets[source] = height;
        public void ClearHeightOffset(object source) => _heightOffsets.Remove(source);

        public void Boost(float speed, float duration)
        {
            _boostSpeed = Mathf.Max(0f, speed);
            _boostTimer = Mathf.Max(0f, duration);
        }

        public void Tick(float heightSpeed, float deltaTime)
        {
            if (_boostTimer > 0f)
                _boostTimer -= deltaTime;

            Height = Mathf.MoveTowards(Height, TargetHeight(), heightSpeed * deltaTime);
        }

        public void Reset()
        {
            _holds.Clear();
            _steeringLocks.Clear();
            _speedFactors.Clear();
            _heightOffsets.Clear();
            _speedFactor = 1f;
            _boostSpeed = 0f;
            _boostTimer = 0f;
            Height = 0f;
        }

        // The highest offset wins (e.g. flying beats a wall run).
        private float TargetHeight()
        {
            if (_heightOffsets.Count == 0)
                return 0f;

            var height = float.MinValue;
            foreach (var offset in _heightOffsets.Values)
                height = Mathf.Max(height, offset);
            return height;
        }

        private void RecalculateSpeedFactor()
        {
            _speedFactor = 1f;
            foreach (var factor in _speedFactors.Values)
                _speedFactor *= factor;
        }
    }
}
