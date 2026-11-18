using _Core._Player;

namespace Game.Hurdles.Effects
{
    /// <summary>
    /// One thing a track element does to the runner who touches it. Put one or more on a hurdle
    /// (EffectHurdle, RewardedSetPiece) and they all run on hit, so new elements are made by combining
    /// effects in a prefab instead of writing a new hurdle class.
    /// </summary>
    public interface IRunnerEffect
    {
        void Apply(RunnerController runner);
    }

    /// <summary>Optional: lets an effect describe itself on its gate (value label, good or bad choice).</summary>
    public interface IGateInfo
    {
        string Label { get; }
        bool IsGood { get; }
    }
}
