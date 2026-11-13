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

            /// <summary>Notification: a runner (player or AI) crossed its finish line.</summary>
            public static readonly GameEvent<_Core._Player.RunnerController> FinishReached = new();

            /// <summary>Notification: a level was built and the player is at the start. Args: level, 1-based level number.</summary>
            public static readonly GameEvent<Game.Level.LevelData, int> LevelLoaded = new();

            /// <summary>Notification: finish reached with enough score. Rewards are already saved.</summary>
            public static readonly GameEvent<Game.Level.LevelResult> LevelCompleted = new();

            /// <summary>Notification: finish reached without enough score.</summary>
            public static readonly GameEvent<Game.Level.LevelResult> LevelFailed = new();

            /// <summary>Request: load the next level (e.g. "Next" button on Level Complete).</summary>
            public static readonly GameEvent RequestNextLevel = new();

            public static readonly GameEvent RequestPlaySavedLevel = new();

            /// <summary>Request: play the current level again (e.g. "Retry" button).</summary>
            public static readonly GameEvent RequestRestartLevel = new();

            /// <summary>Notification: saved coin total changed. Arg: new total.</summary>
            public static readonly GameEvent<int> WalletChanged = new();

            /// <summary>Notification: the Gameplay scene has loaded and hands over its scene references.</summary>
            public static readonly GameEvent<Game.Level.GameplayScene> GameplaySceneReady = new();

            /// <summary>Notification: a player was spawned (first load, or a level with a different doll).</summary>
            public static readonly GameEvent<_Core._Player.RunnerController> PlayerSpawned = new();

            /// <summary>Notification: the target look is on show at level start ("Make this look!"). Arg: the level.</summary>
            public static readonly GameEvent<Game.Level.LevelData> TargetLookShown = new();

            /// <summary>Notification: the target look was hidden; the player can start running.</summary>
            public static readonly GameEvent TargetLookHidden = new();

            /// <summary>Notification: at the finish, the demo stands next to the player. Args: player match, AI match.</summary>
            public static readonly GameEvent<Game.Outfit.OutfitMatch, Game.Outfit.OutfitMatch> ComparisonStarted = new();
        }
    }
}
