using _Core._Player;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// Jump ramp trigger (Kit_LowBlock): the doll (player or AI) launches off it by herself: up AND forward.
    /// She keeps running forward through the jump (Forward Boost × run speed, even if the finger is lifted),
    /// and the normal Jump animation plays. Assign a Hit Effect for a visible puff.
    /// </summary>
    public class AutoJumpPad : TrackHurdle
    {
        [Tooltip("How high she goes (world units). The swipe jump is 1.6; the doll is about 3.3 tall.")]
        [SerializeField, Min(0.1f)] private float jumpHeight = 2.4f;
        [Tooltip("Seconds in the air. Longer = she travels further forward.")]
        [SerializeField, Min(0.1f)] private float airTime = 0.95f;
        [Tooltip("Forward speed in the air, × the normal run speed (1.3 = 30% faster).")]
        [SerializeField, Min(0.1f)] private float forwardBoost = 1.3f;

        protected override void OnPlayerHit(RunnerController player) => player.Jump(jumpHeight, airTime, forwardBoost);
    }
}
