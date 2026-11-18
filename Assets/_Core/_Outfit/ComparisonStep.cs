namespace Game.Outfit
{
    /// <summary>
    /// One step of the finish comparison: a target item, whether each doll wears it,
    /// and each doll's match percentage counting the items compared so far.
    /// </summary>
    public readonly struct ComparisonStep
    {
        /// <summary>The target item being compared now.</summary>
        public readonly OutfitItem Target;

        /// <summary>What the player / AI wears in that slot (null = nothing).</summary>
        public readonly OutfitItem PlayerItem;
        public readonly OutfitItem AiItem;

        public readonly bool PlayerMatched;
        public readonly bool AiMatched;

        /// <summary>0..100, out of ALL target items, counting only the ones compared so far (so it climbs to the final score).</summary>
        public readonly int PlayerPercent;
        public readonly int AiPercent;

        /// <summary>0-based index of this step, and how many steps there are.</summary>
        public readonly int Index;
        public readonly int Count;

        public readonly bool HasAi;

        public ComparisonStep(OutfitItem target, OutfitItem playerItem, OutfitItem aiItem, bool playerMatched, bool aiMatched,
            int playerPercent, int aiPercent, int index, int count, bool hasAi)
        {
            Target = target;
            PlayerItem = playerItem;
            AiItem = aiItem;
            PlayerMatched = playerMatched;
            AiMatched = aiMatched;
            PlayerPercent = playerPercent;
            AiPercent = aiPercent;
            Index = index;
            Count = count;
            HasAi = hasAi;
        }

        public bool IsLast => Index == Count - 1;
    }
}
