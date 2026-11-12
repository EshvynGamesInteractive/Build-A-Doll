namespace Game.Level
{
    /// <summary>How the level went. Sent with LevelCompleted / LevelFailed for the result screens.</summary>
    public readonly struct LevelResult
    {
        public readonly LevelData Level;

        /// <summary>1-based, as shown to the player ("Level 12"), counting past the last level.</summary>
        public readonly int LevelNumber;

        public readonly bool Passed;
        public readonly int Score;
        public readonly int Stars;
        public readonly int CoinsCollected;
        public readonly int CompletionCoins;

        /// <summary>Outfit vs target look. Percent is 100 for levels without a target.</summary>
        public readonly Game.Outfit.OutfitMatch Match;

        public LevelResult(LevelData level, int levelNumber, bool passed, int score, int stars, int coinsCollected, int completionCoins, Game.Outfit.OutfitMatch match)
        {
            Match = match;
            Level = level;
            LevelNumber = levelNumber;
            Passed = passed;
            Score = score;
            Stars = stars;
            CoinsCollected = coinsCollected;
            CompletionCoins = completionCoins;
        }

        public int TotalCoins => CoinsCollected + CompletionCoins;
    }
}
