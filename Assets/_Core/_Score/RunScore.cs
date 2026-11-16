using Core._GameEvent;
using Game.Level;
using UnityEngine;

namespace Game.Score
{
    /// <summary>
    /// Coins for the current run. Lives on the player; coin pickups, gates and penalties add or take coins.
    /// The player's coins go straight into the saved wallet (LevelProgress), so they survive quitting.
    /// Coins resets to 0 on ResetRun (the wallet keeps the total).
    /// </summary>
    [DisallowMultipleComponent]
    public class RunScore : MonoBehaviour
    {
        /// <summary>Off for the AI, so its coins don't show on the player's HUD or go into the wallet.</summary>
        public bool RaiseEvents { get; set; } = true;

        /// <summary>Coins gained this run (never below 0).</summary>
        public int Coins { get; private set; }

        private void OnEnable() => GameEvents.GameplayEvents.ResetRun.Register(ResetCoins);

        private void OnDisable() => GameEvents.GameplayEvents.ResetRun.Unregister(ResetCoins);

        /// <summary>Positive adds coins, negative takes them away.</summary>
        public void AddCoins(int amount)
        {
            if (amount == 0)
                return;

            var before = Coins;
            Coins = Mathf.Max(0, Coins + amount);

            // Player only: save straight into the wallet. A minus can only take back coins
            // gained in this run, never ones saved earlier.
            if (RaiseEvents)
            {
                LevelProgress.AddCoins(Coins - before);
                GameEvents.GameplayEvents.CoinsChanged.Raise(Coins);
            }
        }

        private void ResetCoins()
        {
            Coins = 0;
            if (RaiseEvents)
                GameEvents.GameplayEvents.CoinsChanged.Raise(Coins);
        }
    }
}
