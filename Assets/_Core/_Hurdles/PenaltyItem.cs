using _Core._Player;
using Core._GameEvent;
using Game.Outfit;
using Game.Player;
using Game.Score;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// A "minus" item: mud, scissors, paint splash... Hitting it costs points (and optionally coins),
    /// can make the doll stumble, and can take an item off her (e.g. scissors cut the dress).
    /// </summary>
    public class PenaltyItem : TrackHurdle
    {
        public enum AiAvoidance
        {
            [Tooltip("Decide from the shape: high = slide, track-wide = jump, small = steer around.")]
            Auto,
            SteerAround,
            Jump,
            Slide
        }

        [Tooltip("How the AI gets past this.")]
        [SerializeField] private AiAvoidance aiAvoid = AiAvoidance.Auto;

        [SerializeField, Min(0)] private int pointsLost = 2;
        [SerializeField, Min(0)] private int coinsLost = 0;

        [Header("Stumble")]
        [Tooltip("Seconds the doll is slowed down. 0 = no stumble.")]
        [SerializeField, Min(0f)] private float stumbleDuration = 0.6f;
        [SerializeField, Range(0f, 1f)] private float stumbleSpeedFactor = 0.3f;

        [Header("Outfit")]
        [Tooltip("Take an item off the doll when hit.")]
        [SerializeField] private bool removesItem;
        [SerializeField] private OutfitSlot slotToRemove = OutfitSlot.Dress;

        public AiAvoidance AiAvoid => aiAvoid;

        protected override void OnPlayerHit(RunnerController player)
        {
            // Take the item off first so the score update below includes the lost style.
            if (removesItem)
            {
                var outfit = player.DollOutfit;
                if (outfit != null)
                    outfit.Unequip(slotToRemove);
            }

            var score = player.Score;
            if (score != null)
            {
                score.AddPoints(-pointsLost);
                score.AddCoins(-coinsLost);
            }

            if (stumbleDuration > 0f)
                player.Stumble(stumbleDuration, stumbleSpeedFactor);

            if (!player.IsAI)
                GameEvents.GameplayEvents.PenaltyTaken.Raise(pointsLost);
        }
    }
}
