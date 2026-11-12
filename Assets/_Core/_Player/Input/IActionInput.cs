namespace Game.Player.Input
{
    /// <summary>One-shot actions: true for the single frame the jump or slide was asked for.</summary>
    public interface IActionInput
    {
        bool JumpRequested { get; }
        bool SlideRequested { get; }
    }
}
