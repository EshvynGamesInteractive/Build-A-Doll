using Game.Outfit;
using Game.Player;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// A gate that puts its item on the doll when she runs through it.
    /// Pair two under a HurdleGroup for the classic left/right choice.
    /// </summary>
    public class ItemGate : TrackHurdle
    {
        [SerializeField] private OutfitItem item;

        [Tooltip("Optional: shows the item's icon on the gate.")]
        [SerializeField] private SpriteRenderer iconRenderer;

        public OutfitItem Item => item;

        protected override void Awake()
        {
            base.Awake();
            RefreshIcon();
        }

        protected override void OnPlayerHit(RunnerController player)
        {
            var outfit = player.GetComponentInChildren<DollOutfit>();
            if (outfit == null)
            {
                Debug.LogWarning($"Player '{player.name}' has no {nameof(DollOutfit)}; '{name}' had nothing to dress.", this);
                return;
            }

            outfit.Equip(item);
        }

        private void RefreshIcon()
        {
            if (iconRenderer != null && item != null && item.Icon != null)
                iconRenderer.sprite = item.Icon;
        }

        private void OnValidate() => RefreshIcon();
    }
}
