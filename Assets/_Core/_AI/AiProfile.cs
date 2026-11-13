using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// How good the AI opponent is. Make one asset per difficulty (Easy, Normal, Hard)
    /// and pick it per level, or use the database default.
    /// Create via Create > Build A Doll > AI > AI Profile.
    /// </summary>
    [CreateAssetMenu(fileName = "AiProfile_", menuName = "Build A Doll/AI/AI Profile")]
    public class AiProfile : ScriptableObject
    {
        [Tooltip("Chance (0..1) of picking the gate with the target item. Lower = more wrong items.")]
        [SerializeField, Range(0f, 1f)] private float gateAccuracy = 0.7f;

        [Tooltip("Chance (0..1) of reacting to an obstacle (jump, slide or steer). Lower = hits more.")]
        [SerializeField, Range(0f, 1f)] private float obstacleSkill = 0.8f;

        [Tooltip("How fast it moves sideways, in world units per second.")]
        [SerializeField, Min(0.1f)] private float steerSpeed = 5f;

        [Tooltip("How far ahead (world units) it starts lining up with a gate.")]
        [SerializeField, Min(1f)] private float gateLookAhead = 12f;

        [Tooltip("Seconds after the target look hides before it starts running.")]
        [SerializeField, Min(0f)] private float startDelay = 0.3f;

        [Tooltip("Optional: its own movement tuning (e.g. a bit slower than the player). Empty = same as the player.")]
        [SerializeField] private Game.Player.Config.RunnerMovementConfig movementConfig;

        public float GateAccuracy => gateAccuracy;
        public float ObstacleSkill => obstacleSkill;
        public float SteerSpeed => steerSpeed;
        public float GateLookAhead => gateLookAhead;
        public float StartDelay => startDelay;
        public Game.Player.Config.RunnerMovementConfig MovementConfig => movementConfig;
    }
}
