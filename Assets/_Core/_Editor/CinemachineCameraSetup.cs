#if UNITY_EDITOR
using Game.CameraRig;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Adds the Cinemachine camera rig to the open scene: a CinemachineBrain on the Main Camera and the
    /// CM Run / CM Stage Walk / CM Stage cameras, wired into the CameraFollow component.
    /// Safe to run again; it reuses what is already there. (CameraFollow also builds the rig at runtime
    /// if this was never run — this just lets you see and tweak the cameras in the scene.)
    /// </summary>
    public static class CinemachineCameraSetup
    {
        [MenuItem("Build A Doll/Camera/Setup Cinemachine Cameras")]
        public static void SetupInOpenScene()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("No Main Camera in the scene; skipped Cinemachine setup.");
                return;
            }

            var follow = camera.GetComponent<CameraFollow>();
            if (follow == null)
                follow = Undo.AddComponent<CameraFollow>(camera.gameObject);

            Setup(follow);
            EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
            Selection.activeGameObject = camera.gameObject;
            Debug.Log("Cinemachine camera rig ready: CinemachineBrain on the Main Camera, CM Run / CM Stage Walk / CM Stage under 'CM Camera Rig'.");
        }

        public static void Setup(CameraFollow follow)
        {
            var camera = follow.GetComponent<Camera>();

            if (follow.GetComponent<CinemachineBrain>() == null)
                Undo.AddComponent<CinemachineBrain>(follow.gameObject);

            var so = new SerializedObject(follow);
            var run = so.FindProperty("runCamera");
            var walk = so.FindProperty("stageWalkCamera");
            var stage = so.FindProperty("stageCamera");

            Transform root = null;
            foreach (var p in new[] { run, walk, stage })
                if (p.objectReferenceValue is CinemachineCamera existing && existing.transform.parent != null)
                    root = existing.transform.parent;

            if (root == null)
            {
                var found = GameObject.Find("CM Camera Rig");
                root = found != null ? found.transform : new GameObject("CM Camera Rig").transform;
                if (found == null)
                    Undo.RegisterCreatedObjectUndo(root.gameObject, "Create CM Camera Rig");
            }

            // One camera can't fill two slots: keep it as the Run camera and make the others.
            if (walk.objectReferenceValue == run.objectReferenceValue) walk.objectReferenceValue = null;
            if (stage.objectReferenceValue == run.objectReferenceValue || stage.objectReferenceValue == walk.objectReferenceValue)
                stage.objectReferenceValue = null;

            if (run.objectReferenceValue == null)
                run.objectReferenceValue = Register(CameraFollow.CreateRunCamera(root, camera), camera);
            if (walk.objectReferenceValue == null)
                walk.objectReferenceValue = Register(CameraFollow.CreateStageWalkCamera(root, camera), camera);
            if (stage.objectReferenceValue == null)
                stage.objectReferenceValue = Register(CameraFollow.CreateStageCamera(root, camera), camera);

            so.ApplyModifiedProperties();

            // A hand-made camera in the Run slot gets the Follow / look-at parts it needs.
            var runCam = run.objectReferenceValue as CinemachineCamera;
            if (runCam != null)
            {
                Undo.RegisterFullObjectHierarchyUndo(runCam.gameObject, "Setup CM Run");
                CameraFollow.EnsureShotComponents(runCam, walk.objectReferenceValue as CinemachineCamera);
                if (runCam.name == "CinemachineCamera")
                    runCam.name = "CM Run";
            }
        }

        private static CinemachineCamera Register(CinemachineCamera cam, Camera pose)
        {
            cam.transform.SetPositionAndRotation(pose.transform.position, pose.transform.rotation);
            Undo.RegisterCreatedObjectUndo(cam.gameObject, "Create " + cam.name);
            return cam;
        }
    }
}
#endif
