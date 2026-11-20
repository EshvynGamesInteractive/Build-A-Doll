#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Game.Hurdles;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Menu: Build A Doll > Hurdles > Convert Hurdle Animations To DOTween.
    /// 1. Every prefab under Assets/_Core: each HurdleMover is replaced by DOTweenAnimation components with the same motion
    ///    (see DOTweenMotion), then the HurdleMover is removed. Edit speed/distance afterwards on the DOTweenAnimation in the Inspector.
    /// 2. Ring gates (ItemGates on floor rings): preview set to 3D model, and the preview's spin + bob moved from ItemGate code
    ///    to DOTweenAnimations on the Preview anchor (modelSpinSpeed and iconBob set to 0).
    /// Re-runnable: objects that already have a DOTweenAnimation for the ring preview are left alone.
    /// </summary>
    public static class DOTweenConverter
    {
        private const string Title = "Convert Hurdle Animations To DOTween";

        [MenuItem("Build A Doll/Hurdles/Convert Hurdle Animations To DOTween")]
        public static void Convert()
        {
            if (!EditorUtility.DisplayDialog(Title,
                    "Replace every HurdleMover in the prefabs under Assets/_Core with DOTweenAnimation components, and move the ring " +
                    "previews' spin/bob to DOTween. Prefabs are saved in place (a backup is in _Backup_2026-11-20_DOTween). Continue?",
                    "Convert", "Cancel"))
                return;

            var log = new List<string>();
            var warnings = new List<string>();
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { EditorPaths.Root })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();

            try
            {
                for (var i = 0; i < paths.Count; i++)
                {
                    EditorUtility.DisplayProgressBar(Title, paths[i], (float)i / paths.Count);
                    ConvertPrefab(paths[i], log, warnings);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }

            var summary = log.Count > 0 ? $"{log.Count} prefab(s) changed." : "Nothing to convert (already done?).";
            if (warnings.Count > 0) summary += $"\n\n{warnings.Count} warning(s), see the Console.";
            Debug.Log($"[DOTween] {summary}\n{string.Join("\n", log)}");
            foreach (var w in warnings) Debug.LogWarning("[DOTween] " + w);
            EditorUtility.DisplayDialog(Title, summary, "OK");
        }

        private static void ConvertPrefab(string path, List<string> log, List<string> warnings)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            var changes = new List<string>();
            try
            {
                foreach (var mover in root.GetComponentsInChildren<HurdleMover>(true))
                {
                    // A mover inside a nested prefab belongs to that prefab; it is converted when that prefab is.
                    if (PrefabUtility.IsPartOfPrefabInstance(mover) && !PrefabUtility.IsAddedComponentOverride(mover))
                        continue;

                    var so = new SerializedObject(mover);
                    var m = DOTweenMotion.Motion.Default;
                    m.slide = so.FindProperty("slideDistance").floatValue;
                    m.slideSpeed = so.FindProperty("slideSpeed").floatValue;
                    m.slideAxis = so.FindProperty("slideAxis").vector3Value;
                    m.bob = so.FindProperty("bobHeight").floatValue;
                    m.bobSpeed = so.FindProperty("bobSpeed").floatValue;
                    m.spin = so.FindProperty("spinSpeed").floatValue;
                    m.spinAxis = so.FindProperty("spinAxis").vector3Value;
                    m.swing = so.FindProperty("swingAngle").floatValue;
                    m.swingSpeed = so.FindProperty("swingSpeed").floatValue;
                    m.swingAxis = so.FindProperty("swingAxis").vector3Value;
                    m.phase = so.FindProperty("phase").floatValue;

                    var go = mover.gameObject;
                    Object.DestroyImmediate(mover, true);
                    if (m.Any)
                    {
                        DOTweenMotion.Add(go, m);
                        changes.Add($"{go.name}: {Describe(m)}");
                    }
                    else
                        changes.Add($"{go.name}: idle mover removed");
                }

                foreach (var gate in root.GetComponentsInChildren<ItemGate>(true))
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(gate))
                        continue;
                    if (ConvertRingPreview(gate, warnings))
                        changes.Add($"{gate.name}: ring preview = 3D model, spin/bob on DOTween");
                }

                if (changes.Count > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    log.Add($"{System.IO.Path.GetFileNameWithoutExtension(path)}: {string.Join("; ", changes)}");
                }
            }
            catch (System.Exception e)
            {
                warnings.Add($"{path}: {e.Message}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Floor rings (name has "Ring"): 3D preview, animated by DOTween on the preview anchor.
        private static bool ConvertRingPreview(ItemGate gate, List<string> warnings)
        {
            if (!gate.name.Contains("Ring") && !gate.transform.root.name.Contains("Ring"))
                return false;

            var so = new SerializedObject(gate);
            var anchor = so.FindProperty("previewAnchor").objectReferenceValue as Transform;
            if (anchor == null)
            {
                warnings.Add($"{gate.name}: ring has no Preview Anchor; set one so the 3D model has a DOTween-animated spot.");
                so.FindProperty("previewStyle").enumValueIndex = (int)ItemGate.PreviewStyle.Model3D;
                so.ApplyModifiedPropertiesWithoutUndo();
                return true;
            }

            var spinSpeed = so.FindProperty("modelSpinSpeed").floatValue;
            var bob = so.FindProperty("iconBob").floatValue;
            var bobSpeed = so.FindProperty("iconBobSpeed").floatValue; // radians per second in ItemGate's Sin(Time * speed)

            so.FindProperty("previewStyle").enumValueIndex = (int)ItemGate.PreviewStyle.Model3D;
            so.FindProperty("modelSpinSpeed").floatValue = 0f;
            so.FindProperty("iconBob").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (anchor.GetComponent<DOTweenAnimation>() != null)
                return true; // already set up

            var m = DOTweenMotion.Motion.Default;
            m.spin = Mathf.Approximately(spinSpeed, 0f) ? 60f : spinSpeed;
            m.bob = bob > 0f ? bob * 2f : 0.2f;                         // ItemGate bobbed ±iconBob; DOTween goes 0..h
            m.bobSpeed = bobSpeed > 0f ? bobSpeed / (2f * Mathf.PI) : 0.4f; // to cycles per second
            if (bob > 0f) anchor.localPosition -= Vector3.up * bob;    // keep the same centre
            DOTweenMotion.Add(anchor.gameObject, m);
            return true;
        }

        private static string Describe(DOTweenMotion.Motion m)
        {
            var parts = new List<string>();
            if (m.Slides) parts.Add($"slide ±{m.slide:0.##}");
            if (m.Bobs) parts.Add($"bob {m.bob:0.##}");
            if (m.Spins) parts.Add($"spin {m.spin:0}°/s");
            if (m.Swings) parts.Add($"swing ±{m.swing:0}°");
            return string.Join(", ", parts);
        }
    }
}
#endif
