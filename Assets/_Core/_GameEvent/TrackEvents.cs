using _Core._Player;
using Game.Player.Features;

namespace Core._GameEvent
{
    public abstract partial class GameEvents
    {
        /// <summary>Events from track elements: hazards, statuses, terrain and item collection.</summary>
        public static class TrackEvents
        {
            /// <summary>Notification: a runner hit a Kill hurdle. The LevelManager fails the level when it's the player.</summary>
            public static readonly GameEvent<RunnerController> RunnerKilled = new();

            /// <summary>Notification (player only): a status started or ended. Args: status, now active.</summary>
            public static readonly GameEvent<StatusType, bool> StatusChanged = new();

            /// <summary>Notification (player only): the ground under her changed (land, water, underwater).</summary>
            public static readonly GameEvent<TerrainType> TerrainChanged = new();

            /// <summary>Notification (player only): her shopping cart changed (wanted list set, item added, emptied).</summary>
            public static readonly GameEvent<ShoppingCart> CartChanged = new();
        }
    }
}
