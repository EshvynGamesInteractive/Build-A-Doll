using _Core._Player;
using Core._GameEvent;
using Game.Outfit;
using UnityEngine;

namespace Game.Hurdles.Effects
{
    /// <summary>Scissors: cuts the hair or takes items off the doll, optionally costing coins.</summary>
    public class UnequipEffect : MonoBehaviour, IRunnerEffect
    {
        [SerializeField] private OutfitSlot[] slots = { OutfitSlot.Hair };
        [SerializeField, Min(0)] private int coinCost;

        public void Apply(RunnerController runner)
        {
            var outfit = runner.DollOutfit;
            if (outfit != null && slots != null)
            {
                foreach (var slot in slots)
                    outfit.Unequip(slot);
            }

            if (coinCost <= 0)
                return;

            if (runner.Score != null)
                runner.Score.AddCoins(-coinCost);
            if (!runner.IsAI)
                GameEvents.GameplayEvents.PenaltyTaken.Raise(coinCost);
        }
    }
}
