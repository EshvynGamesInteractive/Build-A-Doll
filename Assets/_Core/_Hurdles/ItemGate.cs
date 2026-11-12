using _Core._Player;
using Game.Outfit;
using Game.Player;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// A gate that puts its item on the doll when she runs through it.
    /// Pair two under a HurdleGroup for the classic left/right choice.
    /// The item can be set in the prefab or per level (LevelData gate items), so one gate prefab
    /// works for every item.
    /// </summary>
    public class ItemGate : TrackHurdle
    {
        [SerializeField] private OutfitItem item;

        [Tooltip("Optional: shows the item's icon on the gate.")]
        [SerializeField] private SpriteRenderer iconRenderer;

        [Tooltip("Optional: a copy of the item's prefab is shown here so the player sees what they'll get.")]
        [SerializeField] private Transform previewAnchor;

        private GameObject _preview;

        public OutfitItem Item => item;

        protected override void Awake()
        {
            base.Awake();
            Refresh();
        }

        public void SetItem(OutfitItem newItem)
        {
            item = newItem;
            Refresh();
        }

        protected override void OnPlayerHit(RunnerController player)
        {
            var outfit = player.DollOutfit;
            if (outfit == null)
            {
                Debug.LogWarning($"Player '{player.name}' has no {nameof(DollOutfit)}; '{name}' had nothing to dress.", this);
                return;
            }

            outfit.Equip(item);
        }

        private void Refresh()
        {
            if (iconRenderer != null && item != null && item.Icon != null)
                iconRenderer.sprite = item.Icon;

            if (!Application.isPlaying || previewAnchor == null)
                return;

            if (_preview != null)
                Destroy(_preview);

            if (item != null && item.Prefab != null)
            {
                _preview = Instantiate(item.Prefab, previewAnchor, false);
                _preview.name = "Preview";

                // Preview is for looks only; don't let its colliders trigger anything.
                foreach (var col in _preview.GetComponentsInChildren<Collider>())
                    col.enabled = false;
            }
        }

        private void OnValidate()
        {
            if (iconRenderer != null && item != null && item.Icon != null)
                iconRenderer.sprite = item.Icon;
        }
    }
}
