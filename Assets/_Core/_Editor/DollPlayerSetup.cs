#if UNITY_EDITOR
using System.IO;
using System.Linq;
using _Core._Player;
using Game.Outfit;
using Game.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// Puts the real doll model into the Player and DemoDoll prefabs.
    /// 1. Makes Doll_Base.prefab: the doll FBX with its body, skeleton and animator, but without
    ///    the Wardrobe (every outfit is spawned at runtime from its part prefab instead).
    /// 2. In Player.prefab and DemoDoll.prefab, hides the placeholder shape, adds Doll_Base under
    ///    Visual with her feet on the ground, and wires DollOutfit (skeleton, anchor) and RunnerAnimator.
    /// Safe to run again: the old doll is replaced.
    /// </summary>
    public static class DollPlayerSetup
    {
        private const string DollModelName = "Doll Rigged";
        private const string DollBaseName = "Doll_Base";
        private const string AnimatorName = "DollAnimator";

        // The doll model is 1.64 units tall; gates are ~2.4 tall and 4 wide, so she's scaled up to
        // match them. Only the model is scaled; the collision capsule stays the same.
        private const float DefaultDollScale = 2;

        private static string PlayerFolder => EditorPaths.Prefabs + "/_Player";
        private static string DollBasePath => PlayerFolder + "/" + DollBaseName + ".prefab";

        [MenuItem("Build A Doll/Outfits/Set Up Doll As Player")]
        private static void SetupMenu()
        {
            var dollBase = BuildDollBase();
            if (dollBase == null)
                return;

            var report = "Doll_Base.prefab made.";
            foreach (var name in new[] { "Player", "DemoDoll" })
            {
                var path = FindPrefabPath(name);
                report += path == null
                    ? $"\n{name}.prefab not found, skipped."
                    : $"\n{name}.prefab: {PutDollInto(path, dollBase)}";
            }

            Debug.Log(report, dollBase);
            EditorUtility.DisplayDialog("Set Up Doll As Player", report, "OK");
        }

        /// <summary>Doll FBX without the Wardrobe, saved as Doll_Base.prefab.</summary>
        public static GameObject BuildDollBase()
        {
            var model = FindModel();
            if (model == null)
            {
                EditorUtility.DisplayDialog("Set Up Doll As Player", $"Couldn't find the '{DollModelName}' model.", "OK");
                return null;
            }

            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var doll = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                PrefabUtility.UnpackPrefabInstance(doll, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                doll.name = DollBaseName;

                // Clothes come from part prefabs at runtime; keeping them here would load every mesh.
                var wardrobe = doll.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Wardrobe");
                if (wardrobe != null)
                    Object.DestroyImmediate(wardrobe.gameObject);

                // Not '??': Unity's fake-null objects slip past it.
                var animator = doll.GetComponentInChildren<Animator>(true);
                if (animator == null)
                    animator = doll.AddComponent<Animator>();

                animator.applyRootMotion = false; // RunnerController moves her
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                var controller = FindAsset<AnimatorController>(AnimatorName);
                if (controller != null)
                    animator.runtimeAnimatorController = controller;
                else
                    Debug.LogWarning($"No '{AnimatorName}' controller found; set the Animator's controller on {DollBaseName} by hand.");

                if (animator.avatar == null || !animator.avatar.isHuman)
                    Debug.LogWarning($"{DollBaseName}: the Animator has no humanoid Avatar. Set the FBX's Rig to Humanoid.");

                EditorPaths.EnsureFolder(PlayerFolder);
                return PrefabUtility.SaveAsPrefabAsset(doll, DollBasePath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static string PutDollInto(string prefabPath, GameObject dollBase)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var runner = root.GetComponentInChildren<RunnerController>(true);
                var outfit = root.GetComponentInChildren<DollOutfit>(true);

                // Model goes under Visual (the part RunnerController turns and squashes).
                var visual = GetVisual(root, runner);

                // Old doll from a previous run.
                var oldDolls = visual.GetComponentsInChildren<Transform>(true).Where(x => x.name == DollBaseName).ToArray();
                var scale = oldDolls.Length > 0 ? oldDolls[0].localScale : Vector3.one * DefaultDollScale; // keep a size set by hand
                foreach (var oldDoll in oldDolls)
                {
                    if (oldDoll != null) // may already be gone with its parent
                        Object.DestroyImmediate(oldDoll.gameObject);
                }

                // Hide the placeholder capsule/sphere.
                if (visual.TryGetComponent<MeshRenderer>(out var placeholder))
                    placeholder.enabled = false;

                var doll = (GameObject)PrefabUtility.InstantiatePrefab(dollBase, visual);
                doll.name = DollBaseName;
                doll.transform.localRotation = Quaternion.identity;
                doll.transform.localScale = scale;
                doll.transform.position = root.transform.TransformPoint(new Vector3(0f, GroundY(root), 0f));

                var animator = doll.GetComponent<Animator>();
                var hips = animator != null && animator.avatar != null && animator.avatar.isHuman
                    ? FindBone(doll.transform, "Hips")
                    : null;

                if (outfit != null)
                {
                    var so = new SerializedObject(outfit);
                    so.FindProperty("skeletonRoot").objectReferenceValue = hips;
                    so.FindProperty("defaultAnchor").objectReferenceValue = doll.transform;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                var runnerAnimator = root.GetComponentInChildren<RunnerAnimator>(true);
                if (runnerAnimator != null)
                {
                    var so = new SerializedObject(runnerAnimator);
                    so.FindProperty("animator").objectReferenceValue = animator;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return outfit != null ? "doll added, DollOutfit wired." : "doll added (no DollOutfit on it).";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform GetVisual(GameObject root, RunnerController runner)
        {
            if (runner != null)
            {
                var visual = new SerializedObject(runner).FindProperty("visual").objectReferenceValue as Transform;
                if (visual != null)
                    return visual;
            }

            var named = root.transform.Find("Visual");
            return named != null ? named : root.transform;
        }

        // Feet go at the bottom of the capsule; without one, at the prefab's pivot.
        private static float GroundY(GameObject root) =>
            root.TryGetComponent<CapsuleCollider>(out var capsule) ? capsule.center.y - capsule.height * 0.5f : 0f;

        private static Transform FindBone(Transform root, string suffix) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith(suffix));

        private static GameObject FindModel()
        {
            foreach (var guid in AssetDatabase.FindAssets($"\"{DollModelName}\" t:Model"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == DollModelName)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return null;
        }

        private static string FindPrefabPath(string name)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{name} t:Prefab", new[] { EditorPaths.Prefabs }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name)
                    return path;
            }

            return null;
        }

        private static T FindAsset<T>(string name) where T : Object
        {
            foreach (var guid in AssetDatabase.FindAssets($"{name} t:{typeof(T).Name}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == name)
                    return AssetDatabase.LoadAssetAtPath<T>(path);
            }

            return null;
        }
    }
}
#endif
