using System.Collections.Generic;
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
            if (levels.Count == 0)
                return null;

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
