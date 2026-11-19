using System.Collections.Generic;
using UnityEngine;

namespace Game.Outfit
{
    /// <summary>
    /// A look the player has to copy: the items, the demo doll that shows them at level start,
    /// and how the match is judged. Levels point at one of these, so a look can be reused.
    /// Create via Create > Build A Doll > Outfit > Target Look.
    /// </summary>
    [CreateAssetMenu(fileName = "Look_", menuName = "Build A Doll/Outfit/Target Look")]
    public class TargetLook : ScriptableObject
    {
        [SerializeField] private string displayName;

        [Tooltip("Optional: copy this outfit variant. When set, its parts are used instead of Items.")]
        [SerializeField] private OutfitVariant variant;

        [Tooltip("The items to copy, one per slot. Compared slot by slot at the finish. Ignored when Variant is set.")]
        [SerializeField] private OutfitItem[] items;

        [Tooltip("Optional: the doll that wears this look. Empty = the database's Default Demo Prefab.")]
        [SerializeField] private GameObject demoPrefab;

        [Tooltip("Optional: picture of the look for UI (e.g. a 'Make this look' card).")]
        [SerializeField] private Sprite preview;

        [Header("Makeup")]
        [Tooltip("Makeup to copy (paint gates): e.g. pink Eye Shadow, red Lips. Each counts like one item in the match %.")]
        [SerializeField] private MakeupColor[] makeup;

        [Header("Judging")]
        [Tooltip("Match % needed to win.")]
        [SerializeField, Range(0, 100)] private int passPercent = 60;

        [Tooltip("Match % for 1, 2 and 3 stars.")]
        [SerializeField] private int[] starPercents = { 50, 75, 100 };

        public string DisplayName => !string.IsNullOrEmpty(displayName) ? displayName
            : variant != null ? variant.DisplayName : name;
        public OutfitVariant Variant => variant;
        public IReadOnlyList<OutfitItem> Items => variant != null ? variant.AllParts : items;
        public bool HasItems => Items != null && Items.Count > 0;

        public IReadOnlyList<MakeupColor> Makeup => makeup ?? System.Array.Empty<MakeupColor>();

        /// <summary>Anything to judge at all: items or makeup.</summary>
        public bool HasAnything => HasItems || Makeup.Count > 0;

        public bool TryGetMakeup(MakeupZone zone, out MakeupColor color)
        {
            foreach (var m in Makeup)
            {
                if (m.zone == zone)
                {
                    color = m;
                    return true;
                }
            }
            color = default;
            return false;
        }
        public GameObject DemoPrefab => demoPrefab;
        public Sprite Preview => preview;
        public int PassPercent => passPercent;

        public bool IsPass(int matchPercent) => matchPercent >= passPercent;

        /// <summary>A look made at runtime for a level that only names an outfit variant.</summary>
        public static TargetLook CreateFor(OutfitVariant outfitVariant, int pass, int[] stars, MakeupColor[] makeupTargets = null)
        {
            var look = CreateInstance<TargetLook>();
            look.name = outfitVariant != null ? $"Look_{outfitVariant.name}" : "Look_Runtime";
            look.hideFlags = HideFlags.DontSave;
            look.variant = outfitVariant;
            look.passPercent = pass;
            look.starPercents = stars;
            look.makeup = makeupTargets;
            return look;
        }

        public int GetStars(int matchPercent)
        {
            var stars = 0;
            if (starPercents == null)
                return stars;

            foreach (var threshold in starPercents)
            {
                if (matchPercent >= threshold)
                    stars++;
            }

            return stars;
        }
    }
}
