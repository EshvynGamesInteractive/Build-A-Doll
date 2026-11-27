using _Core._Player;
using Game.Player;
using Game.Score;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// A reward picked up by running into it: coins, gems, hearts...
    /// Add a HurdleMover to make it spin or bob.
    /// </summary>
    public class Collectable : TrackHurdle
    {
        [SerializeField, Min(0)] private int coins = 0;

        protected override void OnPlayerHit(RunnerController player)
        {
            var score = player.Score;
            if (score == null)
            {
                Debug.LogWarning($"Player '{player.name}' has no {nameof(RunScore)}; '{name}' was not counted.", this);
                return;
            }

            // Track coins collected in gameplay go into the wallet (user, 26 Nov 2026).
            score.AddCoins(coins, true);
        }
    }
}
