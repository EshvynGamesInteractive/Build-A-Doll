using Core._GameEvent;
using Game.Level;
using UnityEngine;

namespace Game.Outfit
{
    /// <summary>
    /// Remembers which outfit variants the player has completed. A variant is completed when the
    /// player passes a level whose target is that variant (each level completes one outfit).
    /// </summary>
    public static class OutfitProgress
    {
        private const string KeyPrefix = "OutfitCompleted_";

        public static bool IsCompleted(OutfitVariant variant) =>
            variant != null && PlayerPrefs.GetInt(KeyPrefix + variant.Id, 0) == 1;

        public static void MarkCompleted(OutfitVariant variant)
        {
            if (variant == null || IsCompleted(variant))
                return;

            PlayerPrefs.SetInt(KeyPrefix + variant.Id, 1);
            PlayerPrefs.Save();
            GameEvents.GameplayEvents.OutfitCompleted.Raise(variant);
        }

        /// <summary>How many variants of this outfit are completed.</summary>
        public static int CompletedCount(OutfitSet outfit)
        {
            if (outfit == null || outfit.Variants == null)
                return 0;

            var count = 0;
            foreach (var variant in outfit.Variants)
            {
                if (IsCompleted(variant))
                    count++;
            }

            return count;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            // Static events survive play sessions when domain reload is off, so don't register twice.
            GameEvents.GameplayEvents.LevelCompleted.Unregister(OnLevelCompleted);
            GameEvents.GameplayEvents.LevelCompleted.Register(OnLevelCompleted);
        }

        private static void OnLevelCompleted(LevelResult result)
        {
            if (result.Passed && result.Level != null)
                MarkCompleted(result.Level.Outfit);
        }
    }
}
