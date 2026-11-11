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

            /// <summary>Notification: the doll put on an item (e.g. after running through a gate).</summary>
            public static readonly GameEvent<Game.Outfit.OutfitItem> OutfitItemEquipped = new();

            /// <summary>Notification: coins collected this run changed. Arg: new total.</summary>
            public static readonly GameEvent<int> CoinsChanged = new();

            /// <summary>Notification: run score changed (pickups, penalties, outfit style). Arg: new total.</summary>
            public static readonly GameEvent<int> ScoreChanged = new();

            /// <summary>Notification: the player hit a minus item. Arg: points lost. Use for screen shake, red flash, etc.</summary>
            public static readonly GameEvent<int> PenaltyTaken = new();
        }
    }
}
