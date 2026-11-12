using _Core._Player;
using Game.CameraRig;
using Game.Player;
using Game.Player.Config;
using Game.Player.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// One-click setup for the runner in the open scene: config assets, a placeholder doll,
    /// a test track and a follow camera. Safe to run again; it reuses what is already there.
    /// </summary>
    public static class RunnerSceneSetup
    {

        [MenuItem("Build A Doll/Setup Runner In Scene")]
        public static void Setup()
        {
            var runnerConfig = LoadOrCreate<RunnerMovementConfig>();
            var cameraConfig = LoadOrCreate<CameraFollowConfig>();

            var player = Object.FindFirstObjectByType<RunnerController>();
            if (player == null)
                player = CreatePlayer(runnerConfig);

            if (GameObject.Find("Track") == null)
                CreateTrack(runnerConfig);

            SetupCamera(player.transform, cameraConfig);

            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            Selection.activeGameObject = player.gameObject;
            Debug.Log("Runner set up. Press Play, then hold and drag to run.");
        }

        private static RunnerController CreatePlayer(RunnerMovementConfig config)
        {
            var root = new GameObject("Player");
            Undo.RegisterCreatedObjectUndo(root, "Create Player");

            // Placeholder body; swap for the doll model later and point "visual" at it.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Visual";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up;

            var input = root.AddComponent<PointerDragInput>();
            var controller = root.AddComponent<RunnerController>();
            var animator = root.AddComponent<RunnerAnimator>();

            var so = new SerializedObject(controller);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("inputSource").objectReferenceValue = input;
            so.FindProperty("visual").objectReferenceValue = body.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            var animatorSo = new SerializedObject(animator);
            animatorSo.FindProperty("stateSource").objectReferenceValue = controller;
            animatorSo.ApplyModifiedPropertiesWithoutUndo();

            return controller;
        }

        private static void CreateTrack(RunnerMovementConfig config)
        {
            const float length = 200f;

            var track = GameObject.CreatePrimitive(PrimitiveType.Cube);
            track.name = "Track";
            track.transform.localScale = new Vector3(config.TrackHalfWidth * 2f + 1f, 0.2f, length);
            track.transform.position = new Vector3(0f, -0.1f, length * 0.5f - 5f);
            Undo.RegisterCreatedObjectUndo(track, "Create Track");
        }

        private static void SetupCamera(Transform target, CameraFollowConfig config)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("No Main Camera in the scene; skipped camera setup.");
                return;
            }

            var follow = camera.GetComponent<CameraFollow>();
            if (follow == null)
                follow = Undo.AddComponent<CameraFollow>(camera.gameObject);

            var so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = target;
            so.FindProperty("config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Uses an existing asset of this type wherever it is; otherwise makes one in the _Data folder.
        internal static T LoadOrCreate<T>() where T : ScriptableObject
        {
            var asset = EditorPaths.FindAsset<T>();
            if (asset != null)
                return asset;

            EditorPaths.EnsureFolder(EditorPaths.Data);
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, $"{EditorPaths.Data}/{typeof(T).Name}.asset");
            AssetDatabase.SaveAssets();
            return asset;
        }
    }
}
