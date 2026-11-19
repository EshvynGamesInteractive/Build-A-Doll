using System.Collections.Generic;
using _Core;
using Core._GameEvent;
using Game.CameraRig;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Level
{
    /// <summary>
    /// Put one in the Gameplay scene. Holds everything that is placed in the scene by hand (ramps, their
    /// start / end points, finish stages, the demo doll's spot, camera and light, the follow camera) and hands
    /// them to the persistent LevelManager when the scene is ready. Nothing here is created by code.
    /// Pressing Play in this scene directly loads Bootstrap first, so the game always starts the same way.
    /// </summary>
    public class GameplayScene : MonoBehaviour
    {
        [Header("Player ramp")]
        [Tooltip("The player's ramp in the scene. Hurdles of the level are put under it.")]
        [SerializeField] private GameObject playerRamp;

        [Tooltip("Player spawns here, facing its blue (forward) arrow. Hurdle distances count from here. Keep its scale at 1.")]
        [SerializeField] private Transform playerStartPoint;

        [Tooltip("End of the player's run. Put the player's FinishLine here.")]
        [SerializeField] private Transform playerEndPoint;

        [Header("AI ramp")]
        [Tooltip("The AI's ramp in the scene. Hidden on levels without an AI opponent.")]
        [SerializeField] private GameObject aiRamp;

        [Tooltip("AI spawns here, facing its blue (forward) arrow. The AI plans its run in this point's space, so keep its scale at 1 and its forward along the ramp.")]
        [SerializeField] private Transform aiStartPoint;

        [Tooltip("End of the AI's run. Put the AI's FinishLine here.")]
        [SerializeField] private Transform aiEndPoint;

        [Header("Camera")]
        [SerializeField] private CameraFollow cameraFollow;

        [Header("Finish stages")]
        [Tooltip("Finish stages placed in THIS scene. One is picked at random each level and shown, the rest are hidden. " +
                 "Give each a FinishStage component with its PlayerSpot / AiSpot (and optional CameraPoint).")]
        [SerializeField] private List<GameObject> sceneStages = new();

        [Header("Demo doll (target look)")]
        [Tooltip("The demo doll is spawned here, facing its forward. Place it far from the track so the main camera never sees it.")]
        [SerializeField] private Transform demoSpawnPoint;

        [Tooltip("Camera that films the demo doll into the demo render texture. Placed and framed by hand.")]
        [SerializeField] private Camera demoCamera;

        [Tooltip("Optional: light for the demo doll. Switched on and off together with the demo camera.")]
        [SerializeField] private Light demoLight;

        public GameObject PlayerRamp => playerRamp;
        public Transform PlayerStartPoint => playerStartPoint;
        public Transform PlayerEndPoint => playerEndPoint;

        public GameObject AiRamp => aiRamp;
        public Transform AiStartPoint => aiStartPoint;
        public Transform AiEndPoint => aiEndPoint;

        public CameraFollow CameraFollow => cameraFollow;
        public IReadOnlyList<GameObject> SceneStages => sceneStages;

        public Transform DemoSpawnPoint => demoSpawnPoint;
        public Camera DemoCamera => demoCamera;
        public Light DemoLight => demoLight;

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
            // Only objects in this scene: a prefab from the Project window would change the prefab itself.
            for (var i = sceneStages.Count - 1; i >= 0; i--)
            {
                if (sceneStages[i] != null && !sceneStages[i].scene.IsValid())
                {
                    Debug.LogWarning($"'{sceneStages[i].name}' is a prefab, not a stage in this scene. Place the stage in the scene and drag the scene object in; removed it from Scene Stages.", this);
                    sceneStages.RemoveAt(i);
                }
            }
        }
    }
}
