using Game.Outfit;

namespace Game.Level
{
    /// <summary>How the level went. Sent with LevelCompleted / LevelFailed for the result screens.</summary>
    public readonly struct LevelResult
    {
        public readonly LevelData Level;

        /// <summary>1-based, as shown to the player ("Level 12"), counting past the last level.</summary>
        public readonly int LevelNumber;

        public readonly bool Passed;

        /// <summary>Player's score: match % with a target look, otherwise the run score.</summary>
        public readonly int Score;

        public readonly int Stars;
        public readonly int CoinsCollected;
        public readonly int CompletionCoins;

        /// <summary>Player's outfit vs the target look. Percent is 100 for levels without a target.</summary>
        public readonly OutfitMatch Match;

        /// <summary>True when an AI raced this level; then Passed means the player beat it.</summary>
        public readonly bool HasAi;

        /// <summary>AI's score, same scale as Score.</summary>
        public readonly int AiScore;

        public readonly OutfitMatch AiMatch;

        public LevelResult(LevelData level, int levelNumber, bool passed, int score, int stars, int coinsCollected,
            int completionCoins, OutfitMatch match, bool hasAi, int aiScore, OutfitMatch aiMatch)
        {
            Level = level;
            LevelNumber = levelNumber;
            Passed = passed;
            Score = score;
            Stars = stars;
            CoinsCollected = coinsCollected;
            CompletionCoins = completionCoins;
            Match = match;
            HasAi = hasAi;
            AiScore = aiScore;
            AiMatch = aiMatch;
        }

        public int TotalCoins => CoinsCollected + CompletionCoins;
    }
}
