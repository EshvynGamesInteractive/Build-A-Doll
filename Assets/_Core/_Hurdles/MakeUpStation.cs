using _Core._Player;
using Core._GameEvent;
using Game.Outfit;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// Make-up mini-step: she stops, the HUD's MakeUpHandler shows the prompt ("Apply Lipstick") and the
    /// player drags the make-up stick over the face. When it's done, the make-up item goes on and she runs on.
    /// The AI gets the make-up straight away.
    /// </summary>
    public class MakeUpStation : TrackHurdle
    {
        [Tooltip("Put on the doll when the step is done (an Outfit Item in the Makeup slot: lipstick, eyeshadow...).")]
        [SerializeField] private OutfitItem makeUpItem;

        [Tooltip("Instruction shown on the HUD.")]
        [SerializeField] private string prompt = "Apply Lipstick";

        [Tooltip("Icon of the stick the player drags (lipstick, eyeshadow brush...).")]
        [SerializeField] private Sprite stickIcon;

        [Tooltip("Coins for finishing the step.")]
        [SerializeField, Min(0)] private int rewardCoins;

        private RunnerController _runner;

        public string Prompt => prompt;
        public Sprite StickIcon => stickIcon;

        /// <summary>True while a player is waiting at this station.</summary>
        public bool IsActive => _runner != null;

        protected override void OnPlayerHit(RunnerController player)
        {
            if (player.IsAI)
            {
                Finish(player);
                return;
            }

            _runner = player;
            player.Hold(this);

            if (!HudPresenter<MakeUpStation>.TryPresent(this))
                Complete();
        }

        /// <summary>Called by the HUD when the make-up is applied.</summary>
        public void Complete()
        {
            if (_runner == null)
                return;

            var runner = _runner;
            _runner = null;
            runner.Release(this);
            Finish(runner);
        }

        public override void ResetHurdle()
        {
            base.ResetHurdle();
            _runner = null;
        }

        private void Finish(RunnerController runner)
        {
            if (makeUpItem != null && runner.DollOutfit != null)
                runner.DollOutfit.Equip(makeUpItem);

            if (rewardCoins > 0 && runner.Score != null)
                runner.Score.AddCoins(rewardCoins);
        }
    }
}
