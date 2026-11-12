using System.Collections.Generic;
using UnityEngine;

namespace Game.Outfit
{
    /// <summary>How close the doll's outfit is to the level's target look.</summary>
    public readonly struct OutfitMatch
    {
        public readonly int Matched;
        public readonly int Total;

        /// <summary>Target items that weren't worn (wrong item in that slot, or nothing).</summary>
        public readonly IReadOnlyList<OutfitItem> Missed;

        public OutfitMatch(int matched, int total, IReadOnlyList<OutfitItem> missed)
        {
            Matched = matched;
            Total = total;
            Missed = missed;
        }

        /// <summary>0..100. A level with no target counts as a full match.</summary>
        public int Percent => Total > 0 ? Mathf.RoundToInt(100f * Matched / Total) : 100;
    }

    /// <summary>Compares what a doll is wearing against a target look, slot by slot.</summary>
    public static class OutfitComparer
    {
        public static OutfitMatch Compare(IReadOnlyList<OutfitItem> target, DollOutfit outfit)
        {
            var missed = new List<OutfitItem>();
            if (target == null || target.Count == 0)
                return new OutfitMatch(0, 0, missed);

            var matched = 0;
            foreach (var wanted in target)
            {
                if (wanted == null)
                    continue;

                if (outfit != null && outfit.Worn.TryGetValue(wanted.Slot, out var worn) && worn == wanted)
                    matched++;
                else
                    missed.Add(wanted);
            }

            return new OutfitMatch(matched, matched + missed.Count, missed);
        }
    }
}
