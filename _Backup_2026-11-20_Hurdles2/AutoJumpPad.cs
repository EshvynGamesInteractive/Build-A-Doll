using _Core._Player;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// Trigger zone in front of a jump hurdle (Kit_LowBlock): the doll (player or AI) jumps over the hurdle by herself.
    /// Uses the normal jump (RunnerController.Jump), so the Jump animation plays as usual.
    /// Assign a Hit Effect (e.g. a dust puff) on this component for a visible "jump" effect.
    /// </summary>
    public class AutoJumpPad : TrackHurdle
    {
        protected override void OnPlayerHit(RunnerController player) => player.Jump();
    }
}
