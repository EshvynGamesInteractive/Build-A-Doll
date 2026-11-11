namespace Core._GameEvent
{
    public abstract partial class GameEvents
    {
        public static class GameplayEvents
        {
            /// <summary>Request: start the player's run (e.g. from a "Tap to play" screen).</summary>
            public static readonly GameEvent StartRun = new();

            /// <summary>Request: put the player back at the start of the track.</summary>
            public static readonly GameEvent ResetRun = new();
        }
    }
}
