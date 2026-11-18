using _Core._Player;
using Core._GameEvent;
using UnityEngine;

namespace Game.Hurdles.Effects
{
    /// <summary>Bomb blast, crate, dirt...: costs coins and slows her down for a moment.</summary>
    public class StumbleEffect : MonoBehaviour, IRunnerEffect
    {
        [SerializeField, Min(0)] private int coinCost = 2;

        [Tooltip("Seconds she's slowed down. 0 = no stumble.")]
        [SerializeField, Min(0f)] private float duration = 0.6f;
        [SerializeField, Range(0f, 1f)] private float speedFactor = 0.3f;

        public void Apply(RunnerController runner)
        {
            if (coinCost > 0 && runner.Score != null)
                runner.Score.AddCoins(-coinCost);

            if (duration > 0f)
                runner.Stumble(duration, speedFactor);

            if (!runner.IsAI)
                GameEvents.GameplayEvents.PenaltyTaken.Raise(coinCost);
        }
    }
}
