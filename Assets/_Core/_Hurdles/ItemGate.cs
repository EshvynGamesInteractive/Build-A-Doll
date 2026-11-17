using System.Collections.Generic;
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

        [Tooltip("Optional: a SpriteRenderer of your own that shows the item's icon. Empty = one is made at the preview anchor.")]
        [SerializeField] private SpriteRenderer iconRenderer;

        [Tooltip("Where the item's icon is shown on the red gate.")]
        [SerializeField] private Transform previewAnchor;

        [Tooltip("Where the item's icon is shown on the green gate (when the item is part of the target look).")]
        [SerializeField] private Transform greenPreviewAnchor;

        [Header("Icon")]
        [Tooltip("Largest side of the icon, in world units.")]
        [SerializeField, Min(0.1f)] private float iconSize = 1.6f;

        [Tooltip("Optional: material for the icon. Empty = the default sprite material.")]
        [SerializeField] private Material iconMaterial;

        [SerializeField] private int iconSortingOrder = 10;

        private SpriteRenderer _icon;
        private IReadOnlyList<OutfitItem> _targetItems;
        private bool _started;

        public OutfitItem Item => item;

        private void Start()
        {
            _started = true;
            Refresh();
        }

        public void Configure(OutfitItem itemOverride, IReadOnlyList<OutfitItem> targetItems)
        {
            _targetItems = targetItems;
            SetItem(itemOverride != null ? itemOverride : item);
        }

        public void SetItem(OutfitItem newItem)
        {
            item = newItem;
            SetChoiceMatch(MatchesTarget());
            if (_started)
                Refresh();
        }

        private bool MatchesTarget()
        {
            if (item == null || _targetItems == null)
                return false;

            foreach (var targetItem in _targetItems)
            {
                if (item == targetItem)
                    return true;
            }

            return false;
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

            // The item's value (its Style Score) is paid out in coins: plus for good picks, minus for bad.
            if (item != null && player.Score != null)
                player.Score.AddCoins(item.StyleScore);
        }

        // Shows the item's 2D icon (OutfitItem.Icon) on the gate. Icons are made with
        // Build A Doll > Outfits > Render Item Icons.
        private void Refresh()
        {
            var sprite = item != null ? item.Icon : null;

            if (iconRenderer != null)
            {
                iconRenderer.sprite = sprite;
                return;
            }

            var anchor = ShowsGreen && greenPreviewAnchor != null ? greenPreviewAnchor : previewAnchor;
            if (!Application.isPlaying || anchor == null)
                return;

            if (sprite == null)
            {
                if (_icon != null)
                    _icon.gameObject.SetActive(false);
                if (item != null)
                    Debug.LogWarning($"'{item.name}' has no icon, so gate '{name}' shows nothing. Run Build A Doll > Outfits > Render Item Icons.", item);
                return;
            }

            if (_icon == null)
            {
                var go = new GameObject("Icon");
                _icon = go.AddComponent<SpriteRenderer>();
                _icon.sortingOrder = iconSortingOrder;
                if (iconMaterial != null)
                    _icon.sharedMaterial = iconMaterial;
            }

            // Follows the anchor of whichever visual (red or green) is showing.
            _icon.transform.SetParent(anchor, false);
            _icon.transform.localPosition = Vector3.zero;
            _icon.transform.localRotation = Quaternion.identity;
            _icon.gameObject.SetActive(true);
            _icon.sprite = sprite;

            var size = sprite.bounds.size;
            var largest = Mathf.Max(size.x, size.y);
            var scale = largest > 0f ? iconSize / largest : 1f;
            _icon.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void OnValidate()
        {
            if (iconRenderer != null && item != null && item.Icon != null)
                iconRenderer.sprite = item.Icon;
        }
    }
}
