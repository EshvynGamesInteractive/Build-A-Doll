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
        public float TurnAnglePerSpeed => turnAnglePerSpeed;
        public float MaxTurnAngle => maxTurnAngle;
        public float TurnLerpSpeed => turnLerpSpeed;
    }
}
