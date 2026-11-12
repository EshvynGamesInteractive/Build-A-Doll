using Core._GameEvent;
using UnityEngine;

namespace Game.Level
{
    /// <summary>Saved progress: which level the player is on and their coin wallet.</summary>
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
    }
}
