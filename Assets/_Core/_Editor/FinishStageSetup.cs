#if UNITY_EDITOR
using Game.Level;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Checks the finish stages listed on the open scene's GameplayScene → Scene Stages and reports any
    /// without a FinishStage component or without PlayerSpot / AiSpot. It doesn't create anything:
    /// add the FinishStage component and the spot objects by hand.
    /// </summary>
    internal static class FinishStageSetup
    {
        [MenuItem("Build A Doll/Level/Check Finish Stage Spots")]
        private static void CheckAll()
        {
            var scene = Object.FindFirstObjectByType<GameplayScene>();
            if (scene == null)
            {
                EditorUtility.DisplayDialog("Finish stages", "Open the Gameplay scene first (no GameplayScene found).", "OK");
                return;
            }

            int ok = 0, problems = 0;
            foreach (var stage in scene.SceneStages)
            {
                if (stage == null)
                    continue;

                if (!stage.TryGetComponent(out FinishStage layout))
                {
                    Debug.LogWarning($"Stage '{stage.name}' has no FinishStage component. Add one and assign its PlayerSpot / AiSpot.", stage);
                    problems++;
                }
                else if (layout.PlayerSpot == null || layout.AiSpot == null)
                {
                    Debug.LogWarning($"Stage '{stage.name}': FinishStage is missing its {(layout.PlayerSpot == null ? "PlayerSpot" : "AiSpot")}.", stage);
                    problems++;
                }
                else
                {
                    ok++;
                }
            }

            Debug.Log($"Finish stages: {ok} ready, {problems} need spots.", scene);
        }
    }
}
#endif
