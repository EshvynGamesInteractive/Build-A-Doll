using Core._GameEvent;
using Game.Outfit;
using UnityEngine;

namespace Game.Score
{
    /// <summary>
    /// Coins and score for the current run. Lives on the player; pickups and penalties add to it.
    /// Score = points from pickups/penalties + style score of what the doll is wearing.
    /// Resets on ResetRun. Saving coins to the player's wallet is left to the end-of-level flow.
    /// </summary>
    [DisallowMultipleComponent]
    public class RunScore : MonoBehaviour
    {
        [Tooltip("Optional. When set, the outfit's style score counts towards the total.")]
        [SerializeField] private DollOutfit outfit;

        [Tooltip("Score can't go below this.")]
        [SerializeField] private int minimumScore = 0;

        private int _points;

        public int Coins { get; private set; }
        public int Score => Mathf.Max(minimumScore, _points + (outfit != null ? outfit.StyleScore : 0));

        private void Awake()
        {
            if (outfit == null)
                outfit = GetComponent<DollOutfit>();
        }

        private void OnEnable()
        {
            GameEvents.GameplayEvents.ResetRun.Register(ResetScore);
            GameEvents.GameplayEvents.OutfitItemEquipped.Register(OnOutfitChanged);
        }

        private void OnDisable()
        {
            GameEvents.GameplayEvents.ResetRun.Unregister(ResetScore);
            GameEvents.GameplayEvents.OutfitItemEquipped.Unregister(OnOutfitChanged);
        }

        public void AddCoins(int amount)
        {
            if (amount == 0)
                return;

            Coins = Mathf.Max(0, Coins + amount);
            GameEvents.GameplayEvents.CoinsChanged.Raise(Coins);
        }

        public void AddPoints(int amount)
        {
            if (amount == 0)
                return;

            _points += amount;
            GameEvents.GameplayEvents.ScoreChanged.Raise(Score);
        }

        private void OnOutfitChanged(OutfitItem _) => GameEvents.GameplayEvents.ScoreChanged.Raise(Score);

        private void ResetScore()
        {
            _points = 0;
            Coins = 0;
            GameEvents.GameplayEvents.CoinsChanged.Raise(Coins);
            GameEvents.GameplayEvents.ScoreChanged.Raise(Score);
        }
    }
}
