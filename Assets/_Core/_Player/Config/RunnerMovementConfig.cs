using UnityEngine;

namespace Game.Player.Config
{
    /// <summary>
    /// Tuning values for the runner. Make one asset per feel (e.g. Default, Fast, Tutorial)
    /// and swap it on the RunnerController without touching code.
    /// </summary>
    [CreateAssetMenu(fileName = "RunnerMovementConfig", menuName = "Build A Doll/Player/Runner Movement Config")]
    public class RunnerMovementConfig : ScriptableObject
    {
        [Header("Forward")]
        [Tooltip("When on, the doll only runs while the finger is on the screen. When off, she runs on her own after the run starts.")]
        [SerializeField] private bool runOnlyWhileHolding = true;
        [SerializeField, Min(0f)] private float forwardSpeed = 6f;
        [Tooltip("Units per second gained per second.")]
        [SerializeField, Min(0.01f)] private float acceleration = 20f;
        [Tooltip("Units per second lost per second.")]
        [SerializeField, Min(0.01f)] private float deceleration = 25f;

        [Header("Sideways")]
        [Tooltip("World units moved when dragging across the full screen width.")]
        [SerializeField, Min(0f)] private float dragSensitivity = 8f;
        [Tooltip("How far the doll can go from the track centre, in world units.")]
        [SerializeField, Min(0f)] private float trackHalfWidth = 2.2f;
        [Tooltip("Time to catch up with the finger. Lower is snappier.")]
        [SerializeField, Min(0f)] private float lateralSmoothTime = 0.06f;

        [Header("Jump (swipe up)")]
        [Tooltip("How high her feet go, in world units. Must clear the jump-over obstacles.")]
        [SerializeField, Min(0f)] private float jumpHeight = 1.6f;
        [Tooltip("Seconds from take-off to landing.")]
        [SerializeField, Min(0.05f)] private float jumpAirTime = 0.7f;

        [Header("Slide (swipe down)")]
        [SerializeField, Min(0.05f)] private float slideDuration = 0.8f;
        [Tooltip("Hitbox height while sliding. Must be lower than the bottom of slide-under bars.")]
        [SerializeField, Min(0.1f)] private float slideHitboxHeight = 0.9f;
        [Tooltip("Downward speed when sliding in mid-air.")]
        [SerializeField, Min(0f)] private float fastFallSpeed = 12f;
        [Tooltip("Squash the model while sliding. For the placeholder; turn off once there's a slide animation.")]
        [SerializeField] private bool squashVisualOnSlide = true;
        [SerializeField, Range(0.1f, 1f)] private float slideVisualScale = 0.45f;

        [Header("Turning visual")]
        [Tooltip("Degrees of yaw per unit of sideways speed.")]
        [SerializeField, Min(0f)] private float turnAnglePerSpeed = 4f;
        [SerializeField, Range(0f, 89f)] private float maxTurnAngle = 35f;
        [SerializeField, Min(0f)] private float turnLerpSpeed = 12f;

        public bool RunOnlyWhileHolding => runOnlyWhileHolding;
        public float ForwardSpeed => forwardSpeed;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float DragSensitivity => dragSensitivity;
        public float TrackHalfWidth => trackHalfWidth;
        public float LateralSmoothTime => lateralSmoothTime;
        public float JumpHeight => jumpHeight;
        public float JumpAirTime => jumpAirTime;
        public float SlideDuration => slideDuration;
        public float SlideHitboxHeight => slideHitboxHeight;
        public float FastFallSpeed => fastFallSpeed;
        public bool SquashVisualOnSlide => squashVisualOnSlide;
        public float SlideVisualScale => slideVisualScale;
        public float TurnAnglePerSpeed => turnAnglePerSpeed;
        public float MaxTurnAngle => maxTurnAngle;
        public float TurnLerpSpeed => turnLerpSpeed;
    }
}
