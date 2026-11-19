using System.Collections.Generic;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// The list of levels in play order, plus shared defaults.
    /// After the last level the game loops back to 'loopFromLevel' so players never run out.
    /// </summary>
    /// <summary>How the ramp model is made as long as the level.</summary>
    public enum RampFit
    {
        Tile,
        Stretch
    }

    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "Build A Doll/Level/Level Database")]
    public class LevelDatabase : ScriptableObject
    {
        [SerializeField] private List<LevelData> levels = new();

        [Tooltip("Player spawned when a level doesn't set its own.")]
        [SerializeField] private GameObject defaultPlayerPrefab;

        [Header("Target look")]
        [Tooltip("Doll that shows the target look. Needs a DollOutfit, and no RunnerController.")]
        [SerializeField] private GameObject defaultDemoPrefab;

        [Tooltip("Seconds the target look is shown before the run can start. With a Demo Render Texture the Gameplay UI's intro decides " +
                 "when to start; this (plus 5 s) is only a safety limit in case no UI answers.")]
        [SerializeField, Min(0f)] private float demoShowTime = 3f;

        [Tooltip("Render texture the demo camera draws the target-look doll into; the Gameplay UI's demo image shows it. " +
                 "Empty = the old way: the demo doll stands on the track in front of the player.")]
        [SerializeField] private RenderTexture demoRenderTexture;

        [Tooltip("Where the demo doll and its camera stand at level start, relative to the track start (sideways, up, along). " +
                 "Far away so the main camera never sees it. A Demo Point on the GameplayScene overrides this.")]
        [SerializeField] private Vector3 demoStageOffset = new(-300f, 0f, -300f);

        [Tooltip("Demo camera's vertical field of view.")]
        [SerializeField, Range(10f, 60f)] private float demoCameraFov = 30f;

        [Tooltip("Brightness of the demo camera's own light on the demo doll (the scene's post-processing doesn't reach the render texture). 0 = no extra light.")]
        [SerializeField, Range(0f, 4f)] private float demoLightIntensity = 1.2f;

        [Tooltip("On: the demo doll turns to face the scene's sun, so the camera sees her lit side.")]
        [SerializeField] private bool demoFaceSun = true;

        [Tooltip("Seconds the item-by-item outfit comparison takes on the finish stage (split evenly between the target items).")]
        [SerializeField, Min(0f)] private float comparisonTime = 3f;

        [Header("Finish stage")]
        [Tooltip("On: the stage is spawned from Stage Prefabs. Off: no stage is spawned; the stages placed in the Gameplay scene (GameplayScene → Scene Stages) are used instead.")]
        [SerializeField] private bool spawnStage = true;

        [Tooltip("Spawn Stage off only. On: the scene stage is moved past the finish line (levels have different lengths). Off: it stays exactly where it's placed in the scene.")]
        [SerializeField] private bool moveSceneStageToFinish = true;

        [Tooltip("Stages spawned past the finish line; one is picked at random each level, and player and AI walk onto it for the comparison. Empty = they compare where they stopped.")]
        [SerializeField] private List<GameObject> stagePrefabs = new();

        [Tooltip("On: the stage spawns (or the scene stage shows) when the level loads, so it's already standing past the finish line during the run. Off: only when the player crosses the finish line.")]
        [SerializeField] private bool spawnStageAtLevelStart = true;

        [Tooltip("On: the same stage never comes up twice in a row (when there are 2 or more). Applies to Scene Stages too.")]
        [SerializeField] private bool stageAvoidRepeat = true;

        // Old single-stage field: moved into Stage Prefabs by OnValidate, kept so existing assets don't lose it.
        [SerializeField, HideInInspector] private GameObject stagePrefab;

        [Tooltip("Metres from the finish line to the front edge of the stage.")]
        [SerializeField, Min(0f)] private float stageGap = 4f;

        [Tooltip("On: the stage stands centred between the player's ramp and the AI's ramp. Off: centred on the player's ramp.")]
        [SerializeField] private bool stageBetweenRamps = true;

        [Tooltip("The stage model is resized to this width (sideways) x depth (along the track), in metres. 0 = keep the model's own size on that axis.")]
        [SerializeField] private Vector3 stageSize = new(12f, 12f, 12f);

        [Tooltip("Sideways distance between the player's and the AI's spots on the stage (metres). 0 = half the stage width.")]
        [SerializeField, Min(0f)] private float stageSpotSpacing = 5f;

        [Tooltip("Walking speed onto the stage (m/s).")]
        [SerializeField, Min(0.1f)] private float stageWalkSpeed = 4f;

        [Tooltip("Longest wait for the AI to cross its own finish line after the player arrives; then it's put on the stage directly.")]
        [SerializeField, Min(0f)] private float aiFinishWait = 3f;

        [Tooltip("Pause after the last item before the win / lose animation.")]
        [SerializeField, Min(0f)] private float resultDelay = 0.6f;

        [Tooltip("Longest wait for the win / lose animation before the result panel opens anyway.")]
        [SerializeField, Min(0.5f)] private float resultAnimMaxTime = 5f;

        [Header("Ramps")]
        [Tooltip("Optional: ground for each ramp, any length. It's repeated (Tile) or scaled (Stretch) along the track to cover the level.")]
        [SerializeField] private GameObject rampPrefab;

        [Tooltip("Tile: the ramp model is repeated end to end at its real size (textures keep their shape). " +
                 "Stretch: one copy is scaled to the level's length (textures get stretched).")]
        [SerializeField] private RampFit rampFit = RampFit.Tile;

        [Tooltip("Tile only: how much each copy overlaps the next, to hide seams.")]
        [SerializeField, Min(0f)] private float rampTileOverlap = 0.01f;

        [Tooltip("Spawn the ramp ground under the player too. Turn off if the Gameplay scene already has a track.")]
        [SerializeField] private bool spawnPlayerRamp = true;

        [Tooltip("One of these is put on the ramp at random each time a level loads (never the same twice in a row). Empty = keep the ramp's own texture.")]
        [SerializeField] private Texture2D[] rampTextures;

        [Header("AI opponent")]
        [Tooltip("AI difficulty for levels that don't set their own. Empty = no AI.")]
        [SerializeField] private Game.AI.AiProfile defaultAiProfile;

        [Tooltip("Optional: the AI's doll. Empty = a copy of the player's doll, driven by AI input.")]
        [SerializeField] private GameObject aiPrefab;

        [Tooltip("How far to the side of the player's ramp the AI's ramp is (negative = left).")]
        [SerializeField] private float aiRampOffset = 10f;

        [Tooltip("Used when a level doesn't set its own finish line.")]
        [SerializeField] private GameObject defaultFinishLine;

        [Tooltip("After the last level, levels are picked from this level number (1-based) onwards. Skips the tutorial levels.")]
        [SerializeField, Min(1)] private int loopFromLevel = 1;

        [Tooltip("On: after the last level, a random level is picked each time (shuffled like a deck: no repeats until all have been played, never the same twice in a row). Off: they replay in order.")]
        [SerializeField] private bool randomAfterLast = true;

        [Tooltip("Changes the random order after the last level.")]
        [SerializeField] private int randomSeed = 7919;

        public int Count => levels.Count;
        public GameObject DefaultFinishLine => defaultFinishLine;
        public GameObject DefaultPlayerPrefab => defaultPlayerPrefab;
        public GameObject DefaultDemoPrefab => defaultDemoPrefab;
        public GameObject RampPrefab => rampPrefab;
        public RampFit RampFitMode => rampFit;
        public float RampTileOverlap => rampTileOverlap;
        public bool SpawnPlayerRamp => spawnPlayerRamp;
        public Texture2D[] RampTextures => rampTextures;
        public Game.AI.AiProfile DefaultAiProfile => defaultAiProfile;
        public GameObject AiPrefab => aiPrefab;
        public float AiRampOffset => aiRampOffset;
        public float DemoShowTime => demoShowTime;
        public RenderTexture DemoRenderTexture => demoRenderTexture;
        public Vector3 DemoStageOffset => demoStageOffset;
        public float DemoCameraFov => demoCameraFov;
        public float DemoLightIntensity => demoLightIntensity;
        public bool DemoFaceSun => demoFaceSun;
        public float ComparisonTime => comparisonTime;
        public IReadOnlyList<GameObject> StagePrefabs => stagePrefabs;
        public bool SpawnStageAtLevelStart => spawnStageAtLevelStart;
        public bool SpawnStage => spawnStage;
        public bool MoveSceneStageToFinish => moveSceneStageToFinish;
        public bool StageAvoidRepeat => stageAvoidRepeat;

        /// <summary>
        /// A random stage from Stage Prefabs (empty entries skipped), or null when there are none.
        /// Pass the index returned last time as <paramref name="lastIndex"/> (-1 = none); it's updated to the pick.
        /// </summary>
        public GameObject PickStagePrefab(ref int lastIndex)
        {
            var valid = new List<int>();
            for (var i = 0; i < stagePrefabs.Count; i++)
                if (stagePrefabs[i] != null)
                    valid.Add(i);

            if (valid.Count == 0)
                return stagePrefab; // not migrated yet (asset not opened in the editor since the change)

            if (stageAvoidRepeat && valid.Count > 1)
                valid.Remove(lastIndex);

            lastIndex = valid[Random.Range(0, valid.Count)];
            return stagePrefabs[lastIndex];
        }
        public float StageGap => stageGap;
        public bool StageBetweenRamps => stageBetweenRamps;
        public float StageSpotSpacing => stageSpotSpacing;
        public Vector3 StageSize => stageSize;
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

        private void OnValidate()
        {
            // Move the old single Stage Prefab into the list.
            if (stagePrefab != null)
            {
                stagePrefabs ??= new List<GameObject>();
                if (!stagePrefabs.Contains(stagePrefab))
                    stagePrefabs.Insert(0, stagePrefab);
                stagePrefab = null;
            }
        }
    }
}
