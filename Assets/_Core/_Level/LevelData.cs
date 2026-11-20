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

        [Header("Target look")]
        [Tooltip("The look to copy: shown on the demo doll at the start and compared at the finish. Empty = use Outfit; both empty = no comparison (run score decides).")]
        [SerializeField] private TargetLook targetLook;

        [NonSerialized] private TargetLook _outfitLook;

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
        public TargetLook TargetLook => targetLook != null ? targetLook : GetOutfitLook();

        /// <summary>The variant this level completes: Outfit, or the Target Look's variant.</summary>
        public OutfitVariant Outfit => outfit != null ? outfit : targetLook != null ? targetLook.Variant : null;
        public bool HasAiOpponent => hasAiOpponent;
        public Game.AI.AiProfile AiProfile => aiProfile;
        public bool HasTarget => TargetLook != null && TargetLook.HasAnything;
        public IReadOnlyList<OutfitItem> TargetOutfit => HasTarget && TargetLook.HasItems ? TargetLook.Items : null;
        public GameObject DemoPrefab => TargetLook != null ? TargetLook.DemoPrefab : null;
        public IReadOnlyList<OutfitItem> StartingOutfit => startingOutfit;
        public IReadOnlyList<Entry> Entries => entries;
        public bool HasTargetSkin => hasTargetSkin;
        public Color TargetSkin => targetSkin;
        /// <summary>Custom order of the parts along the track; empty = default order.</summary>
        public IReadOnlyList<OutfitSlot> GateOrder => gateOrder ?? Array.Empty<OutfitSlot>();
        public int ScoreToPass => scoreToPass;
        public int CompletionCoins => completionCoins;

        /// <summary>With a target look 'score' is the match % and the look judges it; otherwise Star Scores.</summary>
        public int GetStars(int score)
        {
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

        public bool IsPass(int score) => HasTarget ? TargetLook.IsPass(score) : score >= scoreToPass;

        private TargetLook GetOutfitLook()
        {
            if (outfit == null)
                return null;

            // Rebuilt if the outfit was changed in the inspector since it was made.
            if (_outfitLook == null || _outfitLook.Variant != outfit)
                _outfitLook = TargetLook.CreateFor(outfit, outfitPassPercent, outfitStarPercents, outfitMakeup);

            return _outfitLook;
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
