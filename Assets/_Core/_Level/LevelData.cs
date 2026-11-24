using System;
using System.Collections.Generic;
using Game.Outfit;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// One level as data: which hurdle prefabs go where on the track, the finish distance,
    /// the outfit she starts in and the rewards. The visuals live in the prefabs;
    /// this asset only says where to put them.
    /// Create via Create > Build A Doll > Level > Level Data.
    /// </summary>
    [CreateAssetMenu(fileName = "Level_", menuName = "Build A Doll/Level/Level Data")]
    public class LevelData : ScriptableObject
    {
        /// <summary>Which gate of a left/right pair is the right pick (green). Random = picked when the gates are filled.</summary>
        public enum GateSide { Random = 0, Left = 1, Right = 2 }

        [Serializable]
        public class Entry
        {
            [Tooltip("Gate pair, coin line, penalty, wall... any hurdle prefab.")]
            public GameObject prefab;

            [Tooltip("Distance from the start of the track, in world units.")]
            [Min(0f)] public float distance;

            [Tooltip("Sideways offset from the track centre. Negative = left.")]
            public float lateral;

            [Tooltip("For gate prefabs: items for its ItemGates, in hierarchy order (left, right). Empty = keep the prefab's items.")]
            public OutfitItem[] gateItems;

            [Tooltip("For gate pairs: which side gets the target item (green). The level plan puts cash in front of the other (red) side " +
                     "and a hurdle right after this one, so keep it when re-filling gates. Random = any side.")]
            public GateSide greenSide;

            [Tooltip("For paint gate prefabs: face zone and colour for its PaintGates in hierarchy order. Empty = keep the prefab's.")]
            public Game.Hurdles.PaintGate.Setting[] paintGates;
            [Tooltip("For coin gate prefabs: maths for its CoinGates in hierarchy order (CoinGatePair: right gate first, then left), e.g. x2 and -10. Empty = keep the prefab's.")]
            public Game.Hurdles.CoinGate.Setting[] coinGates;
            [Tooltip("For skin gate prefabs: name and colour for its SkinGates in hierarchy order (left, right). Empty = keep the prefab's.")]
            public Game.Hurdles.SkinGate.Setting[] skinGates;
            [Tooltip("For dress colour gate prefabs: name and colour for its DressColorGates in hierarchy order (left, right). Empty = keep the prefab's.")]
            public Game.Hurdles.DressColorGate.Setting[] dressColorGates;
        }

        [SerializeField] private string displayName;

        [Tooltip("Theme line on the target-look card at the start, e.g. \"Design a NURSE outfit!\". Empty = the card's own title.")]
        [SerializeField] private string brief;

        [Header("Tutorial / special")]
        [Tooltip("Starting levels: a hand on screen shows how to drag left/right, and points to the green gate before each choice.")]
        [SerializeField] private bool showSwipeGuide;

        [Tooltip("Special outfit level (Basketball, Police, Nurse…). The regular levels before it fill a progress bar on Level Complete; when it's full this level is unlocked.")]
        [SerializeField] private bool isSpecial;

        [Tooltip("Name shown for the special outfit, e.g. \"Basketball Star\". Empty = the outfit's name.")]
        [SerializeField] private string specialTitle;

        [Tooltip("Optional picture of the special outfit for the progress bar. Empty = the icon of its top or dress.")]
        [SerializeField] private Sprite specialIcon;
        [Tooltip("Optional: use this item's icon as the special picture (e.g. the Umbrella being unlocked). Used when Special Icon is empty.")]
        [SerializeField] private OutfitItem specialIconItem;

        [Header("Free style")]
        [Tooltip("Free style level: no target look. Both gates of every pair are good picks (shown green), so she designs her own look. " +
                 "Always passed; stars come from the Style score (every gate picked + items from the same outfit). No AI rival, no demo comparison.")]
        [SerializeField] private bool isFreeStyle;
        [Tooltip("Free style: Style % for 1, 2 and 3 stars. Style % = (gates picked + same-outfit bonus) / best possible.")]
        [SerializeField] private int[] freeStyleStarPercents = { 40, 70, 100 };

        [Tooltip("Length the level was designed for. The real finish is the ramp's End Point in the Gameplay scene; hurdles at or past it are skipped. Used only when the scene has no End Point.")]
        [SerializeField, Min(1f)] private float finishDistance = 120f;

        [Tooltip("Optional: a different doll for this level. Empty = the database's default player.")]
        [SerializeField] private GameObject playerPrefab;

        [Header("Outfit to complete")]
        [Tooltip("The outfit variant this level completes. Used as the target look when Target Look is empty, and marked completed when the level is passed.")]
        [SerializeField] private OutfitVariant outfit;

        [Tooltip("Match % needed to pass when the target comes from Outfit (no Target Look).")]
        [SerializeField, Range(0, 100)] private int outfitPassPercent = 60;

        [Tooltip("Match % for 1, 2 and 3 stars when the target comes from Outfit.")]
        [SerializeField] private int[] outfitStarPercents = { 50, 75, 100 };
        [Tooltip("Makeup to copy when the target comes from Outfit (paint gates), e.g. pink Eye Shadow. Each counts like one item.")]
        [SerializeField] private MakeupColor[] outfitMakeup;

        [Tooltip("Order the outfit's parts are offered along the track (and compared at the finish), e.g. Hair, Dress, Shoes. " +
                 "Slots not listed follow in the default order. Empty = default order (dress/pants, shirt, hair, shoes...).")]
        [SerializeField] private OutfitSlot[] gateOrder;

        [Header("Skin")]
        [Tooltip("The level asks for a skin colour: skin gates of this colour show green, and the demo doll has this skin.")]
        [SerializeField] private bool hasTargetSkin;
        [SerializeField] private Color targetSkin = Color.white;

        [Header("Dress colour")]
        [Tooltip("The level asks for a dress colour: dress colour gates of this colour show green, and the demo doll's dress has it.")]
        [SerializeField] private bool hasTargetDressColor;
        [SerializeField] private Color targetDressColor = Color.white;

        [Header("Target look")]
        [Tooltip("The look to copy: shown on the demo doll at the start and compared at the finish. Empty = use Outfit; both empty = no comparison (run score decides).")]
        [SerializeField] private TargetLook targetLook;

        [NonSerialized] private TargetLook _outfitLook;
        [NonSerialized] private bool _isYourLook;

        [Header("AI opponent")]
        [Tooltip("Race an AI on a second ramp. The player wins only by matching the target look better than the AI.")]
        [SerializeField] private bool hasAiOpponent = true;

        [Tooltip("Optional: AI difficulty for this level. Empty = the database's default.")]
        [SerializeField] private Game.AI.AiProfile aiProfile;

        [Header("Start")]
        [Tooltip("What she wears when the level starts.")]
        [SerializeField] private OutfitItem[] startingOutfit;

        [SerializeField] private List<Entry> entries = new();

        [Header("Result")]
        [Tooltip("Without a target look: score needed at the finish to pass. Below this the level is failed.")]
        [SerializeField] private int scoreToPass = 1;

        [Tooltip("Without a target look: score for 1, 2 and 3 stars.")]
        [SerializeField] private int[] starScores = { 1, 8, 15 };

        [Tooltip("Coins added on top of the ones she picked up, when the level is passed.")]
        [SerializeField, Min(0)] private int completionCoins = 50;

        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public string Brief => brief;
        public bool ShowSwipeGuide => showSwipeGuide;
        public bool IsSpecial => isSpecial;
        /// <summary>Free style: the player designs her own look; no target, no AI, always passed.</summary>
        public bool IsFreeStyle => isFreeStyle;
        /// <summary>Runtime level built from the player's own free style design (outfit flow).</summary>
        public bool IsYourLook => _isYourLook;
        public string SpecialTitle => !string.IsNullOrEmpty(specialTitle) ? specialTitle : Outfit != null ? Outfit.DisplayName : DisplayName;

        /// <summary>Special icon, else the icon of the outfit's dress, top, or first part that has one.</summary>
        public Sprite SpecialIcon
        {
            get
            {
                if (specialIcon != null)
                    return specialIcon;
                if (specialIconItem != null && specialIconItem.Icon != null)
                    return specialIconItem.Icon;
                if (Outfit == null)
                    return null;

                Sprite fallback = null;
                foreach (var part in Outfit.AllParts)
                {
                    if (part == null || part.Icon == null)
                        continue;
                    if (part.Slot == OutfitSlot.Dress || part.Slot == OutfitSlot.Top)
                        return part.Icon;
                    if (fallback == null)
                        fallback = part.Icon;
                }
                return fallback;
            }
        }
        public float FinishDistance => finishDistance;
        public GameObject PlayerPrefab => playerPrefab;
        public TargetLook TargetLook => isFreeStyle ? null : targetLook != null ? targetLook : GetOutfitLook();

        /// <summary>The variant this level completes: Outfit, or the Target Look's variant.</summary>
        public OutfitVariant Outfit => outfit != null ? outfit : targetLook != null ? targetLook.Variant : null;
        public bool HasAiOpponent => hasAiOpponent && !isFreeStyle;
        public Game.AI.AiProfile AiProfile => aiProfile;
        public bool HasTarget => TargetLook != null && TargetLook.HasAnything;
        public IReadOnlyList<OutfitItem> TargetOutfit => HasTarget && TargetLook.HasItems ? TargetLook.Items : null;
        public GameObject DemoPrefab => TargetLook != null ? TargetLook.DemoPrefab : null;
        public IReadOnlyList<OutfitItem> StartingOutfit => startingOutfit;
        public IReadOnlyList<Entry> Entries => entries;
        public bool HasTargetSkin => hasTargetSkin;
        public Color TargetSkin => targetSkin;
        public bool HasTargetDressColor => hasTargetDressColor;
        public Color TargetDressColor => targetDressColor;
        /// <summary>Custom order of the parts along the track; empty = default order.</summary>
        public IReadOnlyList<OutfitSlot> GateOrder => gateOrder ?? Array.Empty<OutfitSlot>();
        public int ScoreToPass => scoreToPass;
        public int CompletionCoins => completionCoins;

        /// <summary>With a target look 'score' is the match % and the look judges it; otherwise Star Scores.</summary>
        public int GetStars(int score)
        {
            if (isFreeStyle)
                return CountStars(score, freeStyleStarPercents);
            if (HasTarget)
                return TargetLook.GetStars(score);

            var stars = 0;
            if (starScores == null)
                return stars;

            foreach (var threshold in starScores)
            {
                if (score >= threshold)
                    stars++;
            }

            return stars;
        }

        public bool IsPass(int score) => isFreeStyle || (HasTarget ? TargetLook.IsPass(score) : score >= scoreToPass);

        private static int CountStars(int score, int[] thresholds)
        {
            var stars = 0;
            if (thresholds == null)
                return stars;
            foreach (var threshold in thresholds)
                if (score >= threshold)
                    stars++;
            return stars;
        }

        private TargetLook GetOutfitLook()
        {
            if (outfit == null)
                return null;

            // Rebuilt if the outfit was changed in the inspector since it was made.
            if (_outfitLook == null || _outfitLook.Variant != outfit)
                _outfitLook = TargetLook.CreateFor(outfit, outfitPassPercent, outfitStarPercents, outfitMakeup);

            return _outfitLook;
        }

        /// <summary>
        /// "Your Look" level (outfit flow): the free style level's track, with her saved design as the target.
        /// Every gate pair already holds the item she picked and the one she didn't, so the other one is the decoy.
        /// Gate sides are shuffled. Pass % = all but one item.
        /// </summary>
        public static LevelData CreateYourLook(LevelData freeStyleTemplate, IReadOnlyList<OutfitItem> design, bool hasSkin, Color skin,
            bool hasDressColor, Color dressColor, Game.AI.AiProfile ai, int coins, int seed)
        {
            var level = Instantiate(freeStyleTemplate);
            level.name = freeStyleTemplate.name + "_YourLook";
            level.hideFlags = HideFlags.DontSave;
            level._isYourLook = true;
            level.isFreeStyle = false;
            level.isSpecial = false;
            level.showSwipeGuide = false;
            level.outfit = null;
            level.displayName = "Your Look";
            level.brief = "YOUR STYLE IS BACK! Can you recreate it?";

            var count = design.Count;
            var pass = count <= 1 ? 100 : Mathf.RoundToInt(100f * (count - 1) / count);
            level.targetLook = TargetLook.CreateForItems(design, pass, new[] { pass, pass, 100 });
            level.hasAiOpponent = true; // empty profile = the database's default AI
            level.aiProfile = ai;
            level.completionCoins = coins;
            level.hasTargetSkin = hasSkin;
            level.targetSkin = skin;
            level.hasTargetDressColor = hasDressColor;
            level.targetDressColor = dressColor;

            var random = new System.Random(seed);
            foreach (var entry in level.entries)
            {
                if (entry.gateItems == null || entry.gateItems.Length != 2 || entry.gateItems[0] == null || entry.gateItems[1] == null)
                    continue;

                // Free style tracks offer some slots twice (the later pick replaces the earlier one). Every pair of a slot in
                // her design gets her item on one side, so no pair is all red and she can keep her look the whole run.
                var pairItems = entry.gateItems;
                if (System.Array.IndexOf(pairItems, null) < 0)
                {
                    OutfitItem wanted = null;
                    foreach (var item in design)
                        if (item != null && item.Slot == pairItems[0].Slot) { wanted = item; break; }
                    if (wanted != null && pairItems[0] != wanted && pairItems[1] != wanted)
                    {
                        // Keep the side that differs from the other (both sides equal = an old "must wear" pair).
                        pairItems = random.Next(2) == 0 ? new[] { wanted, pairItems[1] } : new[] { pairItems[0], wanted };
                    }
                    else if (pairItems[0] == pairItems[1] && wanted != null)
                    {
                        pairItems = new[] { wanted, pairItems[0] };
                    }
                }

                entry.gateItems = random.Next(2) == 0 ? new[] { pairItems[1], pairItems[0] } : new[] { pairItems[0], pairItems[1] };
            }
            return level;
        }

        [ContextMenu("Sort Entries By Distance")]
        private void SortEntries() => entries.Sort((a, b) => a.distance.CompareTo(b.distance));

        private void OnValidate()
        {
            _outfitLook = null; // pass % or stars may have changed

            foreach (var entry in entries)
            {
                if (entry.distance > finishDistance)
                    Debug.LogWarning($"{name}: '{(entry.prefab != null ? entry.prefab.name : "empty")}' at {entry.distance} is past the finish ({finishDistance}).", this);
            }
        }
    }
}
