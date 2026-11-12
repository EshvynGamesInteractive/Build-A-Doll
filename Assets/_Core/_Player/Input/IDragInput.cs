namespace Game.Player.Input
{
    /// <summary>
    /// What the runner needs from input: is the finger down, and how far did it move sideways.
    /// Lets the controller work with touch, mouse, AI or tests without knowing which.
    /// </summary>
    public interface IDragInput
    {
        bool IsHolding { get; }

        /// <summary>Sideways drag since last frame, as a fraction of screen width (-1..1).</summary>
        float HorizontalDelta { get; }
    }
}
