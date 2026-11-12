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

        [Tooltip("Used when a level doesn't set its own finish line.")]
        [SerializeField] private GameObject defaultFinishLine;

        [Tooltip("Level number (1-based) to loop back to after the last one. Skips the tutorial levels.")]
        [SerializeField, Min(1)] private int loopFromLevel = 1;

        public int Count => levels.Count;
        public GameObject DefaultFinishLine => defaultFinishLine;
        public GameObject DefaultPlayerPrefab => defaultPlayerPrefab;
        public GameObject DefaultDemoPrefab => defaultDemoPrefab;
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
