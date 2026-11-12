using UnityEngine;

namespace Game.Player.Movement
{
    /// <summary>
    /// Jumping and sliding. Jump is a fixed-height arc with a fixed air time; slide lasts a set time.
    /// Sliding while in the air drops her quickly and starts the slide on landing.
    /// Jumping while sliding stops the slide.
    /// </summary>
    public class VerticalMotion
    {
        public float Height { get; private set; }
        public bool IsGrounded => Height <= 0f && _velocity <= 0f;
        public bool IsSliding => _slideTimer > 0f;

        private float _velocity;
        private float _gravity;
        private float _slideTimer;
        private bool _slideQueued;

        public void Jump(float height, float airTime)
        {
            if (!IsGrounded || airTime <= 0f)
                return;

            // From h = v²/2g and T = 2v/g.
            _gravity = 8f * height / (airTime * airTime);
            _velocity = 4f * height / airTime;
            _slideTimer = 0f;
            _slideQueued = false;
        }

        public void Slide(float duration, float fastFallSpeed)
        {
            if (IsGrounded)
            {
                _slideTimer = duration;
                return;
            }

            _slideQueued = true;
            _velocity = Mathf.Min(_velocity, -fastFallSpeed);
        }

        public float Tick(float slideDuration, float deltaTime)
        {
            if (_slideTimer > 0f)
                _slideTimer -= deltaTime;

            if (Height > 0f || _velocity > 0f)
            {
                _velocity -= _gravity * deltaTime;
                Height += _velocity * deltaTime;

                if (Height <= 0f)
                    Land(slideDuration);
            }

            return Height;
        }

        public void Reset()
        {
            Height = 0f;
            _velocity = 0f;
            _slideTimer = 0f;
            _slideQueued = false;
        }

        private void Land(float slideDuration)
        {
            Height = 0f;
            _velocity = 0f;

            if (_slideQueued)
            {
                _slideQueued = false;
                _slideTimer = slideDuration;
            }
        }
    }
}
