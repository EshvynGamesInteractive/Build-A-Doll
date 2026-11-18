using UnityEngine;

namespace Game.Player.Movement
{
    /// <summary>Speeds the runner up towards a target speed and slows her down smoothly.</summary>
    public class ForwardMotion
    {
        public float CurrentSpeed { get; private set; }

        public float Tick(float targetSpeed, float acceleration, float deceleration, float deltaTime)
        {
            var rate = targetSpeed > CurrentSpeed ? acceleration : deceleration;
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, rate * deltaTime);
            return CurrentSpeed * deltaTime;
        }

        /// <summary>Jumps straight to a speed (e.g. stopped by a hold, or shot out of a cannon).</summary>
        public void SetSpeed(float speed)
        {
            CurrentSpeed = Mathf.Max(0f, speed);
        }

        public void ClampSpeed(float maxSpeed)
        {
            CurrentSpeed = Mathf.Min(CurrentSpeed, maxSpeed);
        }

        public void Reset()
        {
            CurrentSpeed = 0f;
        }
    }
}
