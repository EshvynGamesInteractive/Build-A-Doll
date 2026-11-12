using _Core._Player;
using Core._GameEvent;
using Game.Player;

namespace Game.Hurdles
{
    /// <summary>End of the level. The LevelManager takes it from here (stop, score, rewards).</summary>
    public class FinishLine : TrackHurdle
    {
        protected override void OnPlayerHit(RunnerController player) =>
            GameEvents.GameplayEvents.FinishReached.Raise();
    }
}
