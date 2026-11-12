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

        [Tooltip("The items to copy, one per slot. Compared slot by slot at the finish.")]
        [SerializeField] private OutfitItem[] items;

        [Tooltip("Optional: the doll that wears this look. Empty = the database's Default Demo Prefab.")]
        [SerializeField] private GameObject demoPrefab;

        [Tooltip("Optional: picture of the look for UI (e.g. a 'Make this look' card).")]
        [SerializeField] private Sprite preview;

        [Header("Judging")]
        [Tooltip("Match % needed to win.")]
        [SerializeField, Range(0, 100)] private int passPercent = 60;

        [Tooltip("Match % for 1, 2 and 3 stars.")]
        [SerializeField] private int[] starPercents = { 50, 75, 100 };

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public IReadOnlyList<OutfitItem> Items => items;
        public bool HasItems => items != null && items.Length > 0;
        public GameObject DemoPrefab => demoPrefab;
        public Sprite Preview => preview;
        public int PassPercent => passPercent;

        public bool IsPass(int matchPercent) => matchPercent >= passPercent;

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
