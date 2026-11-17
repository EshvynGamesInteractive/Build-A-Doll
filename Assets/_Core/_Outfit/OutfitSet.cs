using System.Collections.Generic;
using UnityEngine;

namespace Game.Outfit
{
    /// <summary>
    /// A whole outfit theme (e.g. "Police") with its variants. Shared Parts are worn by every
    /// variant unless the variant has its own part for that slot.
    /// Create via Create > Build A Doll > Outfit > Outfit, or let the Outfit Importer make them.
    /// </summary>
    [CreateAssetMenu(fileName = "Outfit_", menuName = "Build A Doll/Outfit/Outfit")]
    public class OutfitSet : ScriptableObject
    {
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;

        [Tooltip("Parts every variant wears (e.g. the same hair and shoes), unless a variant replaces them.")]
        [SerializeField] private OutfitItem[] sharedParts;

        [SerializeField] private OutfitVariant[] variants;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public Sprite Icon => icon;
        public IReadOnlyList<OutfitItem> SharedParts => sharedParts;
        public IReadOnlyList<OutfitVariant> Variants => variants;
        public int VariantCount => variants?.Length ?? 0;

        public OutfitVariant GetVariant(int index) =>
            variants != null && index >= 0 && index < variants.Length ? variants[index] : null;

#if UNITY_EDITOR
        /// <summary>Editor only: filled in by the Outfit Importer.</summary>
        public void EditorSetup(string newDisplayName, OutfitItem[] shared, OutfitVariant[] allVariants)
        {
            displayName = newDisplayName;
            sharedParts = shared;
            variants = allVariants;
        }
#endif
    }
}
