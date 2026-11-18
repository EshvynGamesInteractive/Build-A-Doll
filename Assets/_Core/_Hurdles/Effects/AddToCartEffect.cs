using _Core._Player;
using Game.Hurdles.Data;
using Game.Level;
using Game.Player.Features;
using UnityEngine;

namespace Game.Hurdles.Effects
{
    /// <summary>
    /// Collectible item on item levels ("Collect These Kitchen Items"): goes in the shopping cart
    /// (up to its capacity). Good when the level wants it. Needs a ShoppingCart on the doll.
    /// </summary>
    public class AddToCartEffect : MonoBehaviour, IRunnerEffect, IGateInfo, ILevelConfigurable
    {
        [SerializeField] private CollectibleItemData item;

        private bool _wanted = true;

        public CollectibleItemData Item => item;
        public string Label => item != null ? item.DisplayName : string.Empty;
        public bool IsGood => _wanted;

        public void Configure(LevelData level)
        {
            _wanted = true;
            if (level == null || !level.IsCollectLevel)
                return;

            _wanted = false;
            foreach (var wanted in level.CollectItems)
            {
                if (wanted == item)
                {
                    _wanted = true;
                    return;
                }
            }
        }

        public void Apply(RunnerController runner)
        {
            if (runner.TryGetFeature(out ShoppingCart cart))
                cart.TryAdd(item);
        }
    }
}
