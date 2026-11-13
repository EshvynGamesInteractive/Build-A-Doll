using System.Collections.Generic;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// The list of levels in play order, plus shared defaults.
    /// After the last level the game loops back to 'loopFromLevel' so players never run out.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelDatabase", menuName = "Build A Doll/Level/Level Database")]
    public class LevelDatabase : ScriptableObject
    {
        [SerializeField] private List<LevelData> levels = new();

        [Tooltip("Player spawned when a level doesn't set its own.")]
        [SerializeField] private GameObject defaultPlayerPrefab;

        [Header("Target look")]
        [Tooltip("Doll that shows the target look. Needs a DollOutfit, and no RunnerController.")]
        [SerializeField] private GameObject defaultDemoPrefab;

        [Tooltip("Seconds the target look is shown before the run can start.")]
        [SerializeField, Min(0f)] private float demoShowTime = 3f;

        [Tooltip("Seconds the demo stands next to the player at the finish before the result.")]
        [SerializeField, Min(0f)] private float comparisonTime = 2f;

        [Header("Ramps")]
        [Tooltip("Optional: ground for each ramp. Build it 1 unit long from z = 0; it's stretched to the level's length.")]
        [SerializeField] private GameObject rampPrefab;

        [Tooltip("Spawn the ramp ground under the player too. Turn off if the Gameplay scene already has a track.")]
        [SerializeField] private bool spawnPlayerRamp = true;

        [Header("AI opponent")]
        [Tooltip("AI difficulty for levels that don't set their own. Empty = no AI.")]
        [SerializeField] private Game.AI.AiProfile defaultAiProfile;

        [Tooltip("Optional: the AI's doll. Empty = a copy of the player's doll, driven by AI input.")]
        [SerializeField] private GameObject aiPrefab;

        [Tooltip("How far to the side of the player's ramp the AI's ramp is (negative = left).")]
        [SerializeField] private float aiRampOffset = 10f;

        [Tooltip("Used when a level doesn't set its own finish line.")]
        [SerializeField] private GameObject defaultFinishLine;

        [Tooltip("Level number (1-based) to loop back to after the last one. Skips the tutorial levels.")]
        [SerializeField, Min(1)] private int loopFromLevel = 1;

        public int Count => levels.Count;
        public GameObject DefaultFinishLine => defaultFinishLine;
        public GameObject DefaultPlayerPrefab => defaultPlayerPrefab;
        public GameObject DefaultDemoPrefab => defaultDemoPrefab;
        public GameObject RampPrefab => rampPrefab;
        public bool SpawnPlayerRamp => spawnPlayerRamp;
        public Game.AI.AiProfile DefaultAiProfile => defaultAiProfile;
        public GameObject AiPrefab => aiPrefab;
        public float AiRampOffset => aiRampOffset;
        public float DemoShowTime => demoShowTime;
        public float ComparisonTime => comparisonTime;

        /// <summary>Level for a play-through index (0, 1, 2... forever), looping after the last one.</summary>
        public LevelData GetLevel(int index)
        {
            if (levels.Count == 0)
                return null;

            if (index < levels.Count)
                return levels[Mathf.Max(0, index)];

            var loopStart = Mathf.Clamp(loopFromLevel - 1, 0, levels.Count - 1);
            var loopLength = levels.Count - loopStart;
            return levels[loopStart + (index - levels.Count) % loopLength];
        }
    }
}
