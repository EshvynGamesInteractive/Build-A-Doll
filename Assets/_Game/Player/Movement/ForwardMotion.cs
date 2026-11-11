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
