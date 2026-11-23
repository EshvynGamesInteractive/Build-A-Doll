using System.Collections.Generic;
using UnityEngine;

namespace Game.Outfit
{
    /// <summary>
    /// Every outfit in the game, for wardrobe screens, progress and picking decoy items for gates.
    /// The Outfit Importer fills this in.
    /// </summary>
    [CreateAssetMenu(fileName = "OutfitCatalog", menuName = "Build A Doll/Outfit/Outfit Catalog")]
    public class OutfitCatalog : ScriptableObject
    {
        [SerializeField] private List<OutfitSet> outfits = new();

        public IReadOnlyList<OutfitSet> Outfits => outfits;

        /// <summary>Every part in the catalog that goes in this slot.</summary>
        public List<OutfitItem> GetPartsForSlot(OutfitSlot slot)
        {
            var result = new List<OutfitItem>();
            foreach (var outfit in outfits)
            {
                if (outfit == null)
                    continue;

                AddParts(outfit.SharedParts, slot, result);
                if (outfit.Variants == null)
                    continue;

                foreach (var variant in outfit.Variants)
                {
                    if (variant != null)
                        AddParts(variant.OwnParts, slot, result);
                }
            }

            return result;
        }

        /// <summary>The outfit an item belongs to (shared or variant part), or null.</summary>
        public OutfitSet FindSetOf(OutfitItem item)
        {
            if (item == null)
                return null;

            foreach (var outfit in outfits)
            {
                if (outfit == null)
                    continue;
                if (Contains(outfit.SharedParts, item))
                    return outfit;
                if (outfit.Variants == null)
                    continue;
                foreach (var variant in outfit.Variants)
                    if (variant != null && Contains(variant.OwnParts, item))
                        return outfit;
            }

            return null;
        }

        private static bool Contains(IReadOnlyList<OutfitItem> parts, OutfitItem item)
        {
            if (parts == null)
                return false;
            foreach (var part in parts)
                if (part == item)
                    return true;
            return false;
        }

        public OutfitVariant FindVariant(string id)
        {
            foreach (var outfit in outfits)
            {
                if (outfit == null || outfit.Variants == null)
                    continue;

                foreach (var variant in outfit.Variants)
                {
                    if (variant != null && variant.Id == id)
                        return variant;
                }
            }

            return null;
        }

        private static void AddParts(IReadOnlyList<OutfitItem> parts, OutfitSlot slot, List<OutfitItem> result)
        {
            if (parts == null)
                return;

            foreach (var part in parts)
            {
                if (part != null && part.Slot == slot && !result.Contains(part))
                    result.Add(part);
            }
        }

#if UNITY_EDITOR
        public void EditorSetOutfits(List<OutfitSet> all) => outfits = all;
#endif
    }
}
