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

        /// <summary>
        /// Positive adds coins, negative takes them away (run counter, never below 0).
        /// 'toWallet' = also save the change into the wallet (player only). Coin rule (user, 26 Nov 2026): the wallet only
        /// gains coins from the Level Complete Get button, the reward multiplier / spin wheel, coin gates and rewarded ads,
        /// and only loses coins on penalties (see Penalty). Item gates and track coins add nothing.
        /// </summary>
        public void AddCoins(int amount, bool toWallet = false)
        {
            if (amount == 0)
                return;

            var before = Coins;
            Coins = Mathf.Max(0, Coins + amount);

            if (RaiseEvents)
            {
                if (toWallet)
                    LevelProgress.AddCoins(Coins - before);
                GameEvents.GameplayEvents.CoinsChanged.Raise(Coins);
            }
        }

        /// <summary>Penalty hit: takes 'cost' from the run counter and (player only) from the wallet, never below 0.</summary>
        public void Penalty(int cost)
        {
            if (cost <= 0)
                return;
            Coins = Mathf.Max(0, Coins - cost);
            if (RaiseEvents)
            {
                LevelProgress.AddCoins(-cost);
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
