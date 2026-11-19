using System.Collections.Generic;
using UnityEngine;

namespace Game.Outfit
{
    /// <summary>How close the doll is to the level's target look: outfit items plus makeup, each counting as one check.</summary>
    public readonly struct OutfitMatch
    {
        /// <summary>Outfit items worn / asked for.</summary>
        public readonly int Matched;
        public readonly int Total;

        /// <summary>Target items that weren't worn (wrong item in that slot, or nothing).</summary>
        public readonly IReadOnlyList<OutfitItem> Missed;

        /// <summary>Makeup zones painted the right colour / asked for.</summary>
        public readonly int MakeupMatched;
        public readonly int MakeupTotal;

        public OutfitMatch(int matched, int total, IReadOnlyList<OutfitItem> missed)
            : this(matched, total, missed, 0, 0) { }

        public OutfitMatch(int matched, int total, IReadOnlyList<OutfitItem> missed, int makeupMatched, int makeupTotal)
        {
            Matched = matched;
            Total = total;
            Missed = missed;
            MakeupMatched = makeupMatched;
            MakeupTotal = makeupTotal;
        }

        /// <summary>Everything judged: items + makeup.</summary>
        public int AllChecks => Total + MakeupTotal;
        public int AllMatched => Matched + MakeupMatched;

        /// <summary>0..100 over items and makeup. A level with nothing to judge counts as a full match.</summary>
        public int Percent => AllChecks > 0 ? Mathf.RoundToInt(100f * AllMatched / AllChecks) : 100;
    }

    /// <summary>Compares a doll against a target look: items slot by slot, makeup zone by zone.</summary>
    public static class OutfitComparer
    {
        /// <summary>Items only.</summary>
        public static OutfitMatch Compare(IReadOnlyList<OutfitItem> target, DollOutfit outfit)
        {
            var missed = new List<OutfitItem>();
            var matched = CompareItems(target, outfit, missed);
            return new OutfitMatch(matched, matched + missed.Count, missed);
        }

        /// <summary>Items and makeup.</summary>
        public static OutfitMatch Compare(TargetLook look, DollOutfit outfit, DollMakeup makeup)
        {
            var missed = new List<OutfitItem>();
            if (look == null)
                return new OutfitMatch(0, 0, missed);

            var matched = CompareItems(look.HasItems ? look.Items : null, outfit, missed);

            int makeupMatched = 0, makeupTotal = 0;
            foreach (var wanted in look.Makeup)
            {
                makeupTotal++;
                if (makeup != null && makeup.TryGetColor(wanted.zone, out var painted) && MakeupColor.Same(painted, wanted.color))
                    makeupMatched++;
            }

            return new OutfitMatch(matched, matched + missed.Count, missed, makeupMatched, makeupTotal);
        }

        private static int CompareItems(IReadOnlyList<OutfitItem> target, DollOutfit outfit, List<OutfitItem> missed)
        {
            if (target == null || target.Count == 0)
                return 0;

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
            return matched;
        }
    }
}
