using _Core._Player;
using Core._GameEvent;
using Game.Player;

namespace Game.Hurdles
{
    /// <summary>End of the level. The LevelManager takes it from here (stop, score, rewards).</summary>
    public class FinishLine : TrackHurdle
    {
        // Each ramp has its own finish line; the arg says who crossed it (player or AI).
        protected override void OnPlayerHit(RunnerController player) =>
            GameEvents.GameplayEvents.FinishReached.Raise(player);
    }
}
