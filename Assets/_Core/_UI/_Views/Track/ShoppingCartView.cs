using Core._GameEvent;
using Game.Level;
using Game.Player.Features;
using UnityEngine;

namespace _Core._UI._Views
{
    /// <summary>
    /// The HUD for item levels ("Collect These Kitchen Items"): the wanted list, ticked off as items are
    /// collected, and the cart's slots filled in pick-up order. Shown only on item levels.
    /// All slots are set up in the prefab.
    /// </summary>
    public class ShoppingCartView : MonoBehaviour
    {
        [Tooltip("Shown only on item levels.")]
        [SerializeField] private GameObject container;

        [Tooltip("The wanted list. Extra slots are hidden.")]
        [SerializeField] private CartSlotView[] wantedSlots;

        [Tooltip("The cart, e.g. 3 slots. Extra slots are hidden.")]
        [SerializeField] private CartSlotView[] cartSlots;

        private void Awake()
        {
            GameEvents.TrackEvents.CartChanged.Register(OnCartChanged);
            GameEvents.GameplayEvents.LevelLoaded.Register(OnLevelLoaded);
            if (container != null)
                container.SetActive(false);
        }

        // The HUD may wake up after the level loaded: catch up with the current level and cart.
        private void OnEnable()
        {
            var references = ReferenceManager.Instance;
            if (references == null)
                return;

            if (references.levelManager != null)
                OnLevelLoaded(references.levelManager.CurrentLevel, references.levelManager.CurrentLevelNumber);

            if (references.player != null && references.player.TryGetFeature(out ShoppingCart cart))
                OnCartChanged(cart);
        }

        private void OnDestroy()
        {
            GameEvents.TrackEvents.CartChanged.Unregister(OnCartChanged);
            GameEvents.GameplayEvents.LevelLoaded.Unregister(OnLevelLoaded);
        }

        private void OnLevelLoaded(LevelData level, int levelNumber)
        {
            if (container != null)
                container.SetActive(level != null && level.IsCollectLevel);
        }

        private void OnCartChanged(ShoppingCart cart)
        {
            if (wantedSlots != null)
            {
                for (var i = 0; i < wantedSlots.Length; i++)
                {
                    if (wantedSlots[i] == null)
                        continue;

                    if (i >= cart.Wanted.Count)
                    {
                        wantedSlots[i].Hide();
                        continue;
                    }

                    var wanted = cart.Wanted[i];
                    var collected = ContainsItem(cart, wanted);
                    wantedSlots[i].Show(wanted.Icon, collected ? CartSlotView.Mark.Correct : CartSlotView.Mark.None);
                }
            }

            if (cartSlots == null)
                return;

            for (var i = 0; i < cartSlots.Length; i++)
            {
                if (cartSlots[i] == null)
                    continue;

                if (i >= cart.Capacity)
                    cartSlots[i].Hide();
                else if (i >= cart.Items.Count)
                    cartSlots[i].SetEmpty();
                else
                {
                    var item = cart.Items[i];
                    cartSlots[i].Show(item.Icon, cart.IsWanted(item) ? CartSlotView.Mark.Correct : CartSlotView.Mark.Wrong);
                }
            }
        }

        private static bool ContainsItem(ShoppingCart cart, Game.Hurdles.Data.CollectibleItemData item)
        {
            foreach (var inCart in cart.Items)
            {
                if (inCart == item)
                    return true;
            }

            return false;
        }
    }
}
