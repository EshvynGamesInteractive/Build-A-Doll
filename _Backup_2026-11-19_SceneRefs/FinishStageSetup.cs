#if UNITY_EDITOR
using Game.Level;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Adds a FinishStage component with PlayerSpot / AiSpot children to every prefab in
    /// LevelDatabase → Stage Prefabs that doesn't have them yet. Existing spots are left alone,
    /// so it's safe to run again after adding a new stage.
    /// </summary>
    internal static class FinishStageSetup
    {
        [MenuItem("Build A Doll/Level/Set Up Finish Stage Spots")]
        private static void SetUpAll()
        {
            var database = EditorPaths.FindAsset<LevelDatabase>();
            if (database == null)
            {
                EditorUtility.DisplayDialog("Finish stages", "No LevelDatabase found in the project.", "OK");
                return;
            }

            // Same side rule as the game: the player stands on the side away from the AI's ramp.
            var playerSide = database.AiRampOffset >= 0f ? -1f : 1f;
            var done = 0;

            foreach (var prefab in database.StagePrefabs)
            {
                if (prefab == null)
                    continue;

                var path = AssetDatabase.GetAssetPath(prefab);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (!root.TryGetComponent(out FinishStage stage))
                        stage = root.AddComponent<FinishStage>();

                    stage.CreateMissingSpots(playerSide);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    done++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            Debug.Log($"Finish stage spots set up on {done} stage prefab(s). Open each one to move PlayerSpot / AiSpot (and add a Camera Point from the FinishStage menu if you want a fixed camera).", database);
        }
    }
}
#endif
