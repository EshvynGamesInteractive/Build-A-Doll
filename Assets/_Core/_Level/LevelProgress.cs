using Core._GameEvent;
using UnityEngine;

namespace Game.Level
{
    /// <summary>Saved progress: which level the player is on, and their coin wallet.</summary>
    public static class LevelProgress
    {
        /// <summary>0-based index of the level to play next. Keeps counting past the last level (the database loops).</summary>
        public static int CurrentLevelIndex
        {
            get => PlayerPrefs.GetInt(GameConstants.CurrentLevelKey, 0);
            set
            {
                PlayerPrefs.SetInt(GameConstants.CurrentLevelKey, Mathf.Max(0, value));
                PlayerPrefs.Save();
            }
        }

        public static int TotalCoins
        {
            get => PlayerPrefs.GetInt(GameConstants.TotalCoinsKey, 0);
            private set
            {
                PlayerPrefs.SetInt(GameConstants.TotalCoinsKey, Mathf.Max(0, value));
                PlayerPrefs.Save();
            }
        }

        public static void AddCoins(int amount)
        {
            if (amount == 0)
                return;

            TotalCoins += amount;
            GameEvents.GameplayEvents.WalletChanged.Raise(TotalCoins);
        }

        /// <summary>
        /// Completion coins of the level just passed, waiting for the Level Complete Get button (or the reward multiplier,
        /// which pays them multiplied instead). Not saved: leaving without tapping Get loses them.
        /// </summary>
        public static int PendingLevelCoins { get; set; }

        /// <summary>Pays out PendingLevelCoins (Get button) and clears it.</summary>
        public static void CollectPendingLevelCoins()
        {
            var coins = PendingLevelCoins;
            PendingLevelCoins = 0;
            AddCoins(coins);
        }

        /// <summary>Takes coins from the wallet if there are enough. Returns false (and takes nothing) otherwise.</summary>
        public static bool TrySpendCoins(int amount)
        {
            if (amount <= 0)
                return true;
            if (TotalCoins < amount)
                return false;
            AddCoins(-amount);
            return true;
        }

        /// <summary>Wipes all saved progress: level and coins.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(GameConstants.CurrentLevelKey);
            PlayerPrefs.DeleteKey(GameConstants.TotalCoinsKey);
            OutfitFlow.Reset();
            ThemeDesigns.ResetAll();
            PlayerPrefs.Save();
            GameEvents.GameplayEvents.WalletChanged.Raise(0);
        }
    }
}
