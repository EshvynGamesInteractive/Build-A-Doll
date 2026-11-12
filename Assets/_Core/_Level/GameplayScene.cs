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

        [Tooltip("Optional: a player already placed in THIS scene (not a prefab). Used only when no player prefab is set on the level or database.")]
        [SerializeField] private _Core._Player.RunnerController scenePlayer;

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
        }
    }
}
