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
        }

        [SerializeField] private string displayName;

        [Tooltip("Optional: ground, walls, decoration for this level. Spawned at the track start.")]
        [SerializeField] private GameObject environmentPrefab;

        [Tooltip("Where the finish line is, from the start of the track.")]
        [SerializeField, Min(1f)] private float finishDistance = 120f;

        [Tooltip("Optional: overrides the database's default finish line prefab.")]
        [SerializeField] private GameObject finishLinePrefab;

        [Tooltip("Optional: a different doll for this level. Empty = the database's default player.")]
        [SerializeField] private GameObject playerPrefab;

        [Header("Target look")]
        [Tooltip("The look to copy: shown on the demo doll at the start and compared at the finish. Empty = no comparison (run score decides).")]
        [SerializeField] private TargetLook targetLook;

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
        public GameObject EnvironmentPrefab => environmentPrefab;
        public float FinishDistance => finishDistance;
        public GameObject FinishLinePrefab => finishLinePrefab;
        public GameObject PlayerPrefab => playerPrefab;
        public TargetLook TargetLook => targetLook;
        public bool HasAiOpponent => hasAiOpponent;
        public Game.AI.AiProfile AiProfile => aiProfile;
        public bool HasTarget => targetLook != null && targetLook.HasItems;
        public IReadOnlyList<OutfitItem> TargetOutfit => HasTarget ? targetLook.Items : null;
        public GameObject DemoPrefab => targetLook != null ? targetLook.DemoPrefab : null;
        public IReadOnlyList<OutfitItem> StartingOutfit => startingOutfit;
        public IReadOnlyList<Entry> Entries => entries;
        public int ScoreToPass => scoreToPass;
        public int CompletionCoins => completionCoins;

        /// <summary>With a target look 'score' is the match % and the look judges it; otherwise Star Scores.</summary>
        public int GetStars(int score)
        {
            if (HasTarget)
                return targetLook.GetStars(score);

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

        public bool IsPass(int score) => HasTarget ? targetLook.IsPass(score) : score >= scoreToPass;

        [ContextMenu("Sort Entries By Distance")]
        private void SortEntries() => entries.Sort((a, b) => a.distance.CompareTo(b.distance));

        private void OnValidate()
        {
            foreach (var entry in entries)
            {
                if (entry.distance > finishDistance)
                    Debug.LogWarning($"{name}: '{(entry.prefab != null ? entry.prefab.name : "empty")}' at {entry.distance} is past the finish ({finishDistance}).", this);
            }
        }
    }
}
