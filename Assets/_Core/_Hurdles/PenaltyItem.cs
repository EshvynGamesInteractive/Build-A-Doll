using _Core._Player;
using Core._GameEvent;
using Game.Outfit;
using Game.Player;
using Game.Score;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Hurdles
{
    /// <summary>
    /// A "minus" item: mud, scissors, paint splash... Hitting it costs coins,
    /// can make the doll stumble, and can take an item off her (e.g. scissors cut the dress).
    /// A Kill hurdle ends the run instead: the level is failed.
    /// </summary>
    public class PenaltyItem : TrackHurdle, IAiObstacle
    {
        public enum HurdleType
        {
            [Tooltip("Slows her down (stumble animation) and costs coins.")]
            Stumble,
            [Tooltip("Ends the run: the level is failed.")]
            Kill
        }

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

        [Tooltip("Stumble: slows her down and costs coins. Kill: ends the run and fails the level.")]
        [SerializeField] private HurdleType hurdleType = HurdleType.Stumble;

        [Tooltip("Animator trigger played on a Kill hit.")]
        [SerializeField] private string killTrigger = "Die";

        [Tooltip("Coins taken away when hit.")]
        [FormerlySerializedAs("pointsLost")]
        [SerializeField, Min(0)] private int coinCost = 2;

        [Header("Stumble")]
        [Tooltip("Seconds the doll is slowed down. 0 = no stumble.")]
        [SerializeField, Min(0f)] private float stumbleDuration = 0.6f;
        [SerializeField, Range(0f, 1f)] private float stumbleSpeedFactor = 0.3f;

        [Header("Outfit")]
        [Tooltip("Take an item off the doll when hit.")]
        [SerializeField] private bool removesItem;
        [SerializeField] private OutfitSlot slotToRemove = OutfitSlot.Dress;

        public AiAvoidance AiAvoid => aiAvoid;
        public HurdleType Type => hurdleType;
        public bool AvoidByAi => true;
        protected override bool IsHazard => true;

        protected override void OnPlayerHit(RunnerController player)
        {
            if (hurdleType == HurdleType.Kill)
            {
                Kill(player);
                return;
            }

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
                score.AddCoins(-coinCost);
            }

            if (stumbleDuration > 0f)
                player.Stumble(stumbleDuration, stumbleSpeedFactor);

            if (!player.IsAI)
                GameEvents.GameplayEvents.PenaltyTaken.Raise(coinCost);
        }

        private void Kill(RunnerController player)
        {
            player.Hold(this);
            player.Stop();
            if (player.Animator != null)
                player.Animator.PlayTrigger(killTrigger);
            GameEvents.TrackEvents.RunnerKilled.Raise(player);
        }
    }
}
