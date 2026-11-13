namespace Game.Player
{
    /// <summary>Read-only view of the runner, for animation, VFX, UI and so on.</summary>
    public interface IRunnerState
    {
        bool IsRunning { get; }

        /// <summary>Current forward speed, 0..1 of the configured max.</summary>
        float NormalizedSpeed { get; }

        /// <summary>False while jumping.</summary>
        bool IsGrounded { get; }

        bool IsSliding { get; }

        bool IsStumbling { get; }
    }
}
