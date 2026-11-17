using System.Collections.Generic;
using UnityEngine;

namespace Game.Outfit
{
    /// <summary>
    /// One version of an outfit (e.g. "Police - Variant 02"): its own parts plus the outfit's
    /// shared pieces. A level asks the player to complete one of these.
    /// Create via Create > Build A Doll > Outfit > Outfit Variant, or let the Outfit Importer make them.
    /// </summary>
    [CreateAssetMenu(fileName = "Variant_", menuName = "Build A Doll/Outfit/Outfit Variant")]
    public class OutfitVariant : ScriptableObject
    {
        [SerializeField] private string displayName;

        [Tooltip("The outfit this variant belongs to. Its Shared Parts are added to this variant.")]
        [SerializeField] private OutfitSet outfit;

        [Tooltip("Shown in menus (wardrobe, level card...).")]
        [SerializeField] private Sprite icon;

        [Tooltip("Parts only this variant has. A part here replaces a shared part in the same slot.")]
        [SerializeField] private OutfitItem[] parts;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public OutfitSet Outfit => outfit;
        public Sprite Icon => icon != null ? icon : outfit != null ? outfit.Icon : null;
        public IReadOnlyList<OutfitItem> OwnParts => parts;

        /// <summary>Stable id for saving progress: asset name, which the importer keeps unique.</summary>
        public string Id => name;

        /// <summary>Everything she wears for this variant: own parts first, then shared parts for slots not yet filled. A new list each call.</summary>
        public IReadOnlyList<OutfitItem> AllParts
        {
            get
            {
                var all = new List<OutfitItem>();

                if (parts != null)
                {
                    foreach (var part in parts)
                        AddIfSlotFree(part, all);
                }

                if (outfit != null && outfit.SharedParts != null)
                {
                    foreach (var part in outfit.SharedParts)
                        AddIfSlotFree(part, all);
                }

                return all;
            }
        }

        /// <summary>The part this variant puts in a slot, or null.</summary>
        public OutfitItem GetPart(OutfitSlot slot)
        {
            foreach (var part in AllParts)
            {
                if (part.Slot == slot)
                    return part;
            }

            return null;
        }

        public bool Contains(OutfitItem item)
        {
            foreach (var part in AllParts)
            {
                if (part == item)
                    return true;
            }

            return false;
        }

        private static void AddIfSlotFree(OutfitItem part, List<OutfitItem> all)
        {
            if (part == null)
                return;

            foreach (var existing in all)
            {
                if (existing.Slot == part.Slot)
                    return;
            }

            all.Add(part);
        }

#if UNITY_EDITOR
        /// <summary>Editor only: filled in by the Outfit Importer.</summary>
        public void EditorSetup(string newDisplayName, OutfitSet owner, OutfitItem[] ownParts)
        {
            displayName = newDisplayName;
            outfit = owner;
            parts = ownParts;
        }
#endif
    }
}
