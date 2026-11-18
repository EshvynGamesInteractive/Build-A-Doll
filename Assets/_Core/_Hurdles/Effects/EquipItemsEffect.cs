using System.Collections.Generic;
using _Core._Player;
using Game.Level;
using Game.Outfit;
using UnityEngine;

namespace Game.Hurdles.Effects
{
    /// <summary>
    /// Wearable pickup (clothes, hair, shoes, accessories, themed items) or Fashion House: puts the items
    /// on the doll. Good or bad: when the level has a target look, good = every item is in it;
    /// otherwise good = the items' Style Score isn't negative.
    /// </summary>
    public class EquipItemsEffect : MonoBehaviour, IRunnerEffect, IGateInfo, ILevelConfigurable
    {
        [SerializeField] private OutfitItem[] items;

        [Tooltip("Pay each item's Style Score in coins (plus for good, minus for bad), like item gates.")]
        [SerializeField] private bool payStyleScore = true;

        private IReadOnlyList<OutfitItem> _targetLook;

        public string Label
        {
            get
            {
                if (items == null)
                    return string.Empty;

                foreach (var item in items)
                {
                    if (item != null)
                        return item.DisplayName;
                }

                return string.Empty;
            }
        }

        public bool IsGood => _targetLook != null ? AllInTargetLook() : TotalStyleScore() >= 0;

        public void Configure(LevelData level) => _targetLook = level != null ? level.TargetOutfit : null;

        public void Apply(RunnerController runner)
        {
            var outfit = runner.DollOutfit;
            if (outfit == null || items == null)
                return;

            foreach (var item in items)
            {
                if (item == null)
                    continue;

                outfit.Equip(item);
                if (payStyleScore && runner.Score != null)
                    runner.Score.AddCoins(item.StyleScore);
            }
        }

        private bool AllInTargetLook()
        {
            if (items == null)
                return false;

            foreach (var item in items)
            {
                if (item == null)
                    continue;

                var found = false;
                foreach (var wanted in _targetLook)
                {
                    if (wanted == item)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    return false;
            }

            return true;
        }

        private int TotalStyleScore()
        {
            var total = 0;
            if (items == null)
                return total;

            foreach (var item in items)
            {
                if (item != null)
                    total += item.StyleScore;
            }

            return total;
        }
    }
}
