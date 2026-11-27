using System;
using System.Collections.Generic;
using Game.Outfit;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// The list of levels in play order, plus shared defaults.
    /// After the last level the game loops back to 'loopFromLevel' so players never run out.
    /// Ramps, finish lines, finish stages and the demo camera / light are placed in the Gameplay scene
    /// (see GameplayScene), not set here.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "Build A Doll/Level/Level Database")]
    public class LevelDatabase : ScriptableObject
    {
        [SerializeField] private List<LevelData> levels = new();

        [Tooltip("Player spawned at the scene's Player Start Point when a level doesn't set its own.")]
        [SerializeField] private GameObject defaultPlayerPrefab;

        [Header("Target look")]
        [Tooltip("Doll that shows the target look, spawned at the scene's Demo Spawn Point. Needs a DollOutfit, and no RunnerController.")]
        [SerializeField] private GameObject defaultDemoPrefab;

        [Tooltip("Only when the Gameplay scene has no Demo Camera: seconds the demo doll is shown before the run can start. " +
                 "With a Demo Camera the Gameplay UI's intro decides when to start.")]
        [SerializeField, Min(0f)] private float demoShowTime = 3f;

        [Tooltip("Render texture the scene's Demo Camera draws the target-look doll into; the Gameplay UI's demo image shows it. " +
                 "Put on the camera at level start. Empty = keep the camera's own Target Texture.")]
        [SerializeField] private RenderTexture demoRenderTexture;

        [Tooltip("Seconds the item-by-item outfit comparison takes on the finish stage (split evenly between the target items).")]
        [SerializeField, Min(0f)] private float comparisonTime = 3f;

        [Header("Finish stage (placed in the scene)")]
        [Tooltip("On: the same stage never comes up twice in a row (when there are 2 or more).")]
        [SerializeField] private bool stageAvoidRepeat = true;

        [Tooltip("Only for a stage without FinishStage spots: sideways distance between the player's and the AI's spots (metres). 0 = half the stage width.")]
        [SerializeField, Min(0f)] private float stageSpotSpacing = 5f;

        [Tooltip("Walking speed onto the stage (m/s).")]
        [SerializeField, Min(0.1f)] private float stageWalkSpeed = 4f;

        [Tooltip("Longest wait for the AI to cross its own finish line after the player arrives; then it's put on the stage directly.")]
        [SerializeField, Min(0f)] private float aiFinishWait = 3f;

        [Tooltip("Pause after the last item before the win / lose animation.")]
        [SerializeField, Min(0f)] private float resultDelay = 0.6f;

        [Tooltip("Longest wait for the win / lose animation before the result panel opens anyway.")]
        [SerializeField, Min(0.5f)] private float resultAnimMaxTime = 5f;

        [Header("Ramps (placed in the scene)")]
        [Tooltip("One of these is put on the scene ramps at random each time a level loads (never the same twice in a row; the AI's ramp gets a different one). Empty = keep the ramps' own texture.")]
        [SerializeField] private Texture2D[] rampTextures;

        [Header("AI opponent")]
        [Tooltip("AI difficulty for levels that don't set their own. Empty = no AI.")]
        [SerializeField] private Game.AI.AiProfile defaultAiProfile;

        [Tooltip("Optional: the AI's doll, spawned at the scene's AI Start Point. Empty = a copy of the player's doll, driven by AI input.")]
        [SerializeField] private GameObject aiPrefab;

        [Header("Order")]
        [Tooltip("After the last level, levels are picked from this level number (1-based) onwards. Skips the tutorial levels.")]
        [SerializeField, Min(1)] private int loopFromLevel = 1;

        [Tooltip("On: after the last level, a random level is picked each time (shuffled like a deck: no repeats until all have been played, never the same twice in a row). Off: they replay in order.")]
        [SerializeField] private bool randomAfterLast = true;

        [Tooltip("Changes the random order after the last level.")]
        [SerializeField] private int randomSeed = 7919;

        [Header("Special outfit levels")]
        [Tooltip("Level number (1-based) from which regular levels fill the special-outfit progress bar. Levels before it (the first outfits) don't count.")]
        [SerializeField, Min(1)] private int specialProgressFrom = 9;

        [Header("Free style")]
        [Tooltip("Free style levels use it to find which outfit each picked item comes from (same-outfit bonus). Empty = worked out from the item's asset name.")]
        [SerializeField] private Game.Outfit.OutfitCatalog outfitCatalog;
        public Game.Outfit.OutfitCatalog OutfitCatalog => outfitCatalog;

        [Serializable]
        public class OutfitReward
        {
            [Tooltip("Outfit unlocked at the end of a loop (Claim / Skip on Level Complete).")]
            public OutfitVariant outfit;
            [Tooltip("Name on the bar and the claim popup, e.g. \"Rain Outfit\". Empty = the outfit's name.")]
            public string title;
            [Tooltip("Optional picture. Empty = the icon of the outfit's dress or top.")]
            public Sprite icon;
            [Tooltip("Free style level played right after the outfit is claimed. Its track is also used for the Your Look level.")]
            public LevelData freeStyleLevel;

            public string Title => !string.IsNullOrEmpty(title) ? title : outfit != null ? outfit.DisplayName : "New Outfit";

            public Sprite Icon
            {
                get
                {
                    if (icon != null || outfit == null)
                        return icon;
                    Sprite fallback = null;
                    foreach (var part in outfit.AllParts)
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
        }

        [Header("Outfit flow (tutorial → unlock loops → claim → free style → Your Look)")]
        [Tooltip("On: Levels is the main track (tutorial levels first, then the unlock levels). Free style levels come only after a claim, " +
                 "Your Look levels at random. Off: the old fixed order with special levels.")]
        [SerializeField] private bool useOutfitFlow;
        [Tooltip("First levels of the list that teach the controls. They don't count toward the unlock bar.")]
        [SerializeField, Min(0)] private int tutorialLevels = 3;
        [Tooltip("Main levels (and Your Look levels) per unlocked outfit.")]
        [SerializeField, Min(1)] private int levelsPerUnlock = 3;
        [Tooltip("Outfits unlocked in this order, one per loop.")]
        [SerializeField] private List<OutfitReward> outfitRewards = new();
        [SerializeField] private int flowSeed = 1301;

        [Header("Your Look (her free style design as a level)")]
        [SerializeField] private bool yourLookEnabled = true;
        [Tooltip("Levels after its free style before a design can come back.")]
        [SerializeField, Min(1)] private int yourLookMinGap = 2;
        [Tooltip("Levels after its free style when it's forced (on the next progress slot 1 or 2).")]
        [SerializeField, Min(1)] private int yourLookMaxGap = 6;
        [SerializeField, Range(0f, 1f)] private float yourLookChance = 0.4f;
        [Tooltip("Chance per eligible level that an already played design comes back.")]
        [SerializeField, Range(0f, 1f)] private float yourLookThrowbackChance = 0.15f;
        [SerializeField] private Game.AI.AiProfile yourLookAi;
        [SerializeField] private Game.AI.AiProfile yourLookAiLate;
        [Tooltip("Loop number (1-based) from which Your Look Ai Late is used.")]
        [SerializeField, Min(1)] private int yourLookLateFromLoop = 4;
        [Tooltip("Your Look completion coins = the next main level's coins × this.")]
        [SerializeField, Min(0f)] private float yourLookCoinFactor = 1.5f;

        [Header("Theme plan (24 Nov 2026): fixed list with free style + styled levels")]
        [Tooltip("On: Levels is played in order. A free style level (Is Free Style, Theme Name) is unlocked after the levels " +
                 "before it (Claim / Skip on Level Complete). A level with Styled Look Of set replays her design from that " +
                 "free style level; replaying the free style level updates it. Takes priority over Use Outfit Flow.")]
        [SerializeField] private bool useThemePlan;

        [NonSerialized] private Dictionary<LevelData, KeyValuePair<string, LevelData>> _styledCache;

        public bool UseOutfitFlow => useOutfitFlow && !useThemePlan;
        public bool UseThemePlan => useThemePlan;

        /// <summary>Theme plan: the styled level built from her latest design (or the theme's default look before she has one).</summary>
        public LevelData ResolveStyled(LevelData level)
        {
            if (level == null || !level.IsStyledLook)
                return level;
            var source = level.StyledLookOf;
            var key = ThemeDesigns.GetRaw(source);
            _styledCache ??= new Dictionary<LevelData, KeyValuePair<string, LevelData>>();
            if (_styledCache.TryGetValue(level, out var cached) && cached.Key == key && cached.Value != null)
                return cached.Value;

            IReadOnlyList<OutfitItem> design = ThemeDesigns.Load(source, outfitCatalog);
            if (design == null || design.Count == 0)
                design = source.ThemeLook != null ? source.ThemeLook.AllParts : level.Outfit != null ? level.Outfit.AllParts : null;
            if (design == null || design.Count == 0)
                return level;

            var styled = LevelData.CreateStyledLook(level, design, unchecked(randomSeed + levels.IndexOf(level) * 31));
            _styledCache[level] = new KeyValuePair<string, LevelData>(key, styled);
            return styled;
        }

        /// <summary>Theme plan: true when the level after 'index' is a free style level (its unlock popup shows after 'index').</summary>
        public bool TryGetNextFreeStyle(int index, out LevelData freeStyle)
        {
            freeStyle = null;
            if (!useThemePlan || index < 0 || levels.Count == 0)
                return false;
            freeStyle = levels[ThemeListIndex(index + 1)];
            return freeStyle != null && freeStyle.IsFreeStyle;
        }

        /// <summary>
        /// Theme plan bar after finishing 'index': levels done toward the next free style level, counted from the previous
        /// free style level (or the end of the tutorial). Free style levels themselves and the tutorial don't count.
        /// </summary>
        public bool TryGetThemeProgress(int index, out LevelData nextFreeStyle, out int done, out int needed)
        {
            nextFreeStyle = null;
            done = needed = 0;
            if (useThemePlan && index >= levels.Count)
                index = ThemeListIndex(index);
            if (!useThemePlan || index < 0 || index >= levels.Count || levels[index] == null || levels[index].IsFreeStyle || index < tutorialLevels)
                return false;
            var next = -1;
            for (var i = index + 1; i < levels.Count; i++)
                if (levels[i] != null && levels[i].IsFreeStyle) { next = i; break; }
            if (next < 0)
                return false;
            var start = tutorialLevels;
            for (var i = index - 1; i >= tutorialLevels; i--)
                if (levels[i] != null && levels[i].IsFreeStyle) { start = i + 1; break; }
            for (var i = start; i < next; i++)
            {
                if (levels[i] == null || levels[i].IsFreeStyle)
                    continue;
                needed++;
                if (i <= index)
                    done++;
            }
            nextFreeStyle = levels[next];
            return needed > 0;
        }
        public int TutorialLevels => tutorialLevels;
        public int LevelsPerUnlock => levelsPerUnlock;
        public IReadOnlyList<OutfitReward> OutfitRewards => outfitRewards;
        public OutfitReward GetReward(int index) => index >= 0 && index < outfitRewards.Count ? outfitRewards[index] : null;
        public int FlowSeed => flowSeed;
        public bool YourLookEnabled => yourLookEnabled;
        public int YourLookMinGap => yourLookMinGap;
        public int YourLookMaxGap => Mathf.Max(yourLookMinGap, yourLookMaxGap);
        public float YourLookChance => yourLookChance;
        public float YourLookThrowbackChance => yourLookThrowbackChance;
        public Game.AI.AiProfile YourLookAi => yourLookAi;
        public Game.AI.AiProfile YourLookAiLate => yourLookAiLate;
        public int YourLookLateFromLoop => yourLookLateFromLoop;
        public float YourLookCoinFactor => yourLookCoinFactor;

        public int Count => levels.Count;

        /// <summary>Progress toward the next special outfit level, as seen after finishing a level.</summary>
        public readonly struct SpecialProgress
        {
            public readonly LevelData Special;   // the special level being unlocked
            public readonly int SpecialNumber;   // its 1-based level number
            public readonly int Done;            // regular levels finished toward it (after this one)
            public readonly int Needed;          // regular levels between the previous special and it
            public bool Unlocked => Done >= Needed;

            public SpecialProgress(LevelData special, int specialNumber, int done, int needed)
            {
                Special = special;
                SpecialNumber = specialNumber;
                Done = done;
                Needed = needed;
            }
        }

        /// <summary>
        /// After finishing the level at 'index' (0-based): which special level is next and how many of the regular
        /// levels before it are done. False for levels before Special Progress From, special levels themselves,
        /// levels past the last one (random order), or when no special level is ahead.
        /// </summary>
        public bool TryGetSpecialProgress(int index, out SpecialProgress progress)
        {
            progress = default;
            if (useOutfitFlow || useThemePlan)
                return false; // the outfit flow / theme plan have their own bar
            // Special and free style levels don't count toward the bar.
            if (index < 0 || index >= levels.Count || levels[index] == null || levels[index].IsSpecial || levels[index].IsFreeStyle)
                return false;

            var from = Mathf.Max(0, specialProgressFrom - 1);
            if (index < from)
                return false;

            var next = -1;
            for (var i = index + 1; i < levels.Count; i++)
            {
                if (levels[i] != null && levels[i].IsSpecial) { next = i; break; }
            }
            if (next < 0)
                return false;

            var start = from;
            for (var i = next - 1; i >= from; i--)
            {
                if (levels[i] != null && levels[i].IsSpecial) { start = i + 1; break; }
            }

            int needed = 0, done = 0;
            for (var i = start; i < next; i++)
            {
                if (levels[i] == null || levels[i].IsFreeStyle)
                    continue;
                needed++;
                if (i <= index)
                    done++;
            }
            if (needed <= 0)
                return false;

            progress = new SpecialProgress(levels[next], next + 1, Mathf.Clamp(done, 0, needed), needed);
            return true;
        }

        /// <summary>True when the level at this play-through index is a special outfit level.</summary>
        public bool IsSpecialAt(int index)
        {
            var level = GetLevel(index);
            // Theme plan / outfit flow: only free style levels get the badge (not the styled / Your Look levels that replay her design).
            if (useThemePlan || useOutfitFlow)
                return level != null && level.IsFreeStyle;
            return level != null && level.IsSpecial;
        }
        public GameObject DefaultPlayerPrefab => defaultPlayerPrefab;
        public GameObject DefaultDemoPrefab => defaultDemoPrefab;
        public Texture2D[] RampTextures => rampTextures;
        public Game.AI.AiProfile DefaultAiProfile => defaultAiProfile;
        public GameObject AiPrefab => aiPrefab;
        public float DemoShowTime => demoShowTime;
        public RenderTexture DemoRenderTexture => demoRenderTexture;
        public float ComparisonTime => comparisonTime;
        public bool StageAvoidRepeat => stageAvoidRepeat;
        public float StageSpotSpacing => stageSpotSpacing;
        public float StageWalkSpeed => stageWalkSpeed;
        public float AiFinishWait => aiFinishWait;
        public float ResultDelay => resultDelay;
        public float ResultAnimMaxTime => resultAnimMaxTime;

        /// <summary>
        /// Level for a play-through index (0, 1, 2... forever). Up to the last level: that level.
        /// After it: a random level from Loop From Level on (or in order when Random After Last is off).
        /// The same index always gives the same level, so Retry replays it and the level map stays consistent.
        /// </summary>
        public LevelData GetLevel(int index)
        {
            if (useThemePlan)
                return ResolveStyled(GetMainLevel(index));
            if (useOutfitFlow && levels.Count > 0)
                return OutfitFlow.Resolve(this, index, false);
            return GetMainLevel(index);
        }

        /// <summary>Level about to be played at this index. With the outfit flow this decides it (free style / Your Look / main) and saves it.</summary>
        public LevelData GetLevelForPlay(int index)
        {
            if (useThemePlan)
                return ResolveStyled(GetMainLevel(index));
            if (useOutfitFlow && levels.Count > 0)
                return OutfitFlow.Resolve(this, index, true);
            return GetMainLevel(index);
        }

        /// <summary>
        /// Theme plan: list position for a play-through index. Past the last level the plan restarts from Loop From Level
        /// and runs in order again (30 → 4, 5, 6 … 30 → 4 …), so the free style / styled pairs keep their order.
        /// </summary>
        public int ThemeListIndex(int index)
        {
            if (levels.Count == 0)
                return -1;
            if (index < levels.Count)
                return Mathf.Max(0, index);
            var loopStart = Mathf.Clamp(loopFromLevel - 1, 0, levels.Count - 1);
            return loopStart + (index - levels.Count) % (levels.Count - loopStart);
        }

        /// <summary>Main-track level: the list in order, then random from Loop From Level on (theme plan: in order).</summary>
        public LevelData GetMainLevel(int index)
        {
            if (levels.Count == 0)
                return null;

            if (useThemePlan)
                return levels[ThemeListIndex(index)];

            if (index < levels.Count)
                return levels[Mathf.Max(0, index)];

            var loopStart = Mathf.Clamp(loopFromLevel - 1, 0, levels.Count - 1);
            var loopLength = levels.Count - loopStart;
            var past = index - levels.Count;

            // Two or fewer levels to pick from: alternating in order already never repeats.
            if (!randomAfterLast || loopLength < 3)
                return levels[loopStart + past % loopLength];

            // Shuffled deck per round of loopLength picks: every level once per round, no back-to-back repeat between rounds.
            var round = past / loopLength;
            var deck = ShuffledRound(round, loopLength);
            return levels[loopStart + deck[past % loopLength]];
        }

        private int[] ShuffledRound(int round, int length)
        {
            var deck = Shuffle(round, length);
            if (round > 0)
            {
                var previous = Shuffle(round - 1, length);
                // Swap the first two (never the last), so the next round's check against the plain shuffle stays right.
                if (deck[0] == previous[length - 1])
                    (deck[0], deck[1]) = (deck[1], deck[0]);
            }
            return deck;
        }

        private int[] Shuffle(int round, int length)
        {
            var deck = new int[length];
            for (var i = 0; i < length; i++)
                deck[i] = i;

            var random = new System.Random(unchecked(randomSeed * 31 + round * 7919 + length));
            for (var i = length - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
            return deck;
        }
    }
}
