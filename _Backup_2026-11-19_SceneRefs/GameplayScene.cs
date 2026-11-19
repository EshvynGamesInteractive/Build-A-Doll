using System.Collections.Generic;
using _Core;
using Core._GameEvent;
using Game.CameraRig;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Level
{
    /// <summary>
    /// Put one in the Gameplay scene. Holds the scene's own references (where the player spawns,
    /// the follow camera) and hands them to the persistent LevelManager when the scene is ready,
    /// so nothing has to search the scene.
    /// Pressing Play in this scene directly loads Bootstrap first, so the game always starts the same way.
    /// </summary>
    public class GameplayScene : MonoBehaviour
    {
        [Tooltip("Player spawns here, facing its forward. The track starts here too.")]
        [SerializeField] private Transform playerSpawnPoint;

        [SerializeField] private CameraFollow cameraFollow;

        [Tooltip("Optional: where the target-look demo doll stands at level start. Empty = a few metres down the track, facing the player.")]
        [SerializeField] private Transform demoPoint;

        [Tooltip("Finish stages already placed in THIS scene. Used when LevelDatabase → Spawn Stage is off: one is picked at random each level and shown, the rest are hidden. Each can have a FinishStage component for its player / AI spots and camera point.")]
        [SerializeField] private List<GameObject> sceneStages = new();

        [Tooltip("Optional: a player already placed in THIS scene (not a prefab). Used only when no player prefab is set on the level or database.")]
        [SerializeField] private _Core._Player.RunnerController scenePlayer;

        [SerializeField] private GameObject aiRamp;
        [SerializeField] private GameObject playerRamp;
        [SerializeField] private Transform playerStartPoint;
        [SerializeField] private Transform playerEndPoint;
        [SerializeField] private Transform aiStartPoint;
        [SerializeField] private Transform aiEndPoint;
        
        [SerializeField] private Transform demoSpawnPoint;
        [SerializeField] private Camera demoCamera;
        

        /// <summary>Spawn point if set, else where the scene player stands, else this object.</summary>
        public Transform PlayerSpawnPoint
        {
            get
            {
                if (playerSpawnPoint != null)
                    return playerSpawnPoint;
                return ScenePlayer != null ? ScenePlayer.transform : transform;
            }
        }

        public CameraFollow CameraFollow => cameraFollow;
        public Transform DemoPoint => demoPoint;
        public IReadOnlyList<GameObject> SceneStages => sceneStages;

        /// <summary>The scene player, or null. A prefab asset put in the field by mistake is ignored.</summary>
        public _Core._Player.RunnerController ScenePlayer =>
            scenePlayer != null && scenePlayer.gameObject.scene.IsValid() ? scenePlayer : null;

        private void Start()
        {
            // Managers live in Bootstrap; without them nothing can run.
            if (ReferenceManager.Instance == null)
            {
                SceneManager.LoadScene(GameConstants.BootstrapSceneName);
                return;
            }

            GameEvents.GameplayEvents.GameplaySceneReady.Raise(this);
        }

        private void OnValidate()
        {
            // A prefab from the Project window isn't a scene object; using it would change the prefab itself.
            if (scenePlayer != null && !scenePlayer.gameObject.scene.IsValid())
            {
                Debug.LogWarning($"'{scenePlayer.name}' is a prefab, not a player in this scene. Put player prefabs on the {nameof(LevelDatabase)} (Default Player Prefab) instead; cleared Scene Player.", this);
                scenePlayer = null;
            }

            // Same for stages: only objects in this scene. Stage prefabs go on the LevelDatabase.
            for (var i = sceneStages.Count - 1; i >= 0; i--)
            {
                if (sceneStages[i] != null && !sceneStages[i].scene.IsValid())
                {
                    Debug.LogWarning($"'{sceneStages[i].name}' is a prefab, not a stage in this scene. Put stage prefabs on the {nameof(LevelDatabase)} (Stage Prefabs) instead; removed it from Scene Stages.", this);
                    sceneStages.RemoveAt(i);
                }
            }
        }
    }
}
