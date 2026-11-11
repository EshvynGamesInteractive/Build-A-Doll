namespace Core._GameEvent
{
    public abstract partial class GameEvents
    {
        public static class GameplayEvents
        {
            /// <summary>Request: start the player's run (e.g. from a "Tap to play" screen).</summary>
            public static readonly GameEvent StartRun = new();

            /// <summary>Request: stop the player (e.g. finish line reached). She slows down and stops taking input.</summary>
            public static readonly GameEvent StopRun = new();

            /// <summary>Request: put the player back at the start of the track.</summary>
            public static readonly GameEvent ResetRun = new();

            /// <summary>Notification: the run has started. Hide "Tap to play", start timers, etc.</summary>
            public static readonly GameEvent RunStarted = new();
        }
    }
}
