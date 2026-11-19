#if UNITY_EDITOR
using Game.Hurdles;
using Game.Outfit;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Paint gate tools:
    /// • Create Paint Gate Prefabs: makes PaintGatePair_Lips and PaintGatePair_Eyes from CoinGatePair
    ///   (same red / green gate visuals, CoinGate swapped for PaintGate).
    /// • Add Makeup Zones To Selected Doll: adds DollMakeup to the selected doll (in a prefab or the scene) and
    ///   places starting lip / eyelid / cheek volumes under its head bone, ready to be moved onto the face.
    /// </summary>
    internal static class PaintSetup
    {
        private const string SourcePrefabName = "CoinGatePair";

        [MenuItem("Build A Doll/Paint/Create Paint Gate Prefabs")]
        private static void CreatePaintGatePrefabs()
        {
            var source = FindPrefab(SourcePrefabName);
            if (source == null)
            {
                EditorUtility.DisplayDialog("Paint gates", $"Couldn't find the '{SourcePrefabName}' prefab to copy the gate visuals from.", "OK");
                return;
            }

            var folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(source))?.Replace('\\', '/');
            var lips = Make(source, folder, "PaintGatePair_Lips",
                new PaintGate.Setting { zone = MakeupZone.Lips, colorName = "Pink", color = new Color(1f, 0.4f, 0.85f) },
                new PaintGate.Setting { zone = MakeupZone.Lips, colorName = "Red", color = new Color(0.85f, 0.1f, 0.15f) });
            var eyes = Make(source, folder, "PaintGatePair_Eyes",
                new PaintGate.Setting { zone = MakeupZone.EyeShadow, colorName = "Pink", color = new Color(1f, 0.4f, 0.85f) },
                new PaintGate.Setting { zone = MakeupZone.EyeShadow, colorName = "Brown", color = new Color(0.68f, 0.32f, 0.08f) });

            if (lips != null)
                EditorGUIUtility.PingObject(lips);
            Debug.Log($"Paint gates made in {folder}: {(lips != null ? lips.name : "-")}, {(eyes != null ? eyes.name : "-")}. " +
                      "Add them to a level's entries; each entry can set its own zone and colours (Paint Gates).");
        }

        private static GameObject Make(GameObject source, string folder, string prefabName, PaintGate.Setting first, PaintGate.Setting second)
        {
            var path = $"{folder}/{prefabName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null &&
                !EditorUtility.DisplayDialog("Paint gates", $"{prefabName} already exists. Replace it?", "Replace", "Keep"))
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);

            var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            root.name = prefabName;

            var coinGates = root.GetComponentsInChildren<CoinGate>(true);
            var settings = new[] { first, second };
            for (var i = 0; i < coinGates.Length; i++)
            {
                var coin = coinGates[i];
                var go = coin.gameObject;
                var from = new SerializedObject(coin);

                // The coin label goes; the paint gate makes its own.
                var label = from.FindProperty("label")?.objectReferenceValue as Component;
                var active = from.FindProperty("activeVisual")?.objectReferenceValue;
                var red = from.FindProperty("redVisual")?.objectReferenceValue;
                var green = from.FindProperty("greenVisual")?.objectReferenceValue;
                var effect = from.FindProperty("hitEffect")?.objectReferenceValue;
                var sound = from.FindProperty("hitSound")?.objectReferenceValue;

                Object.DestroyImmediate(coin);
                if (label != null && label.gameObject != go)
                    Object.DestroyImmediate(label.gameObject);
                else
                {
                    var labelChild = go.transform.Find("Label");
                    if (labelChild != null)
                        Object.DestroyImmediate(labelChild.gameObject);
                }

                var paint = go.AddComponent<PaintGate>();
                var to = new SerializedObject(paint);
                to.FindProperty("activeVisual").objectReferenceValue = active;
                to.FindProperty("redVisual").objectReferenceValue = red;
                to.FindProperty("greenVisual").objectReferenceValue = green;
                to.FindProperty("hitEffect").objectReferenceValue = effect;
                to.FindProperty("hitSound").objectReferenceValue = sound;
                var s = settings[Mathf.Min(i, settings.Length - 1)];
                to.FindProperty("zone").enumValueIndex = (int)s.zone;
                to.FindProperty("colorName").stringValue = s.colorName;
                to.FindProperty("color").colorValue = s.color;
                to.ApplyModifiedPropertiesWithoutUndo();
                go.name = $"PaintGate_{s.colorName}";
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        [MenuItem("Build A Doll/Paint/Add Makeup Zones To Selected Doll")]
        private static void AddZones()
        {
            var selected = Selection.activeGameObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Makeup zones", "Select a doll first (open the Player / Demo prefab and select its root).", "OK");
                return;
            }

            var makeup = selected.GetComponentInChildren<DollMakeup>(true);
            if (makeup == null)
                makeup = Undo.AddComponent<DollMakeup>(selected);

            makeup.CreateDefaultZones();
            EditorUtility.SetDirty(makeup);
            Selection.activeGameObject = makeup.gameObject;
            Debug.Log($"Makeup zones added to '{makeup.name}'. Select the Makeup_* objects under the head bone and move / scale the ellipsoids onto the lips, eyelids and cheeks.", makeup);
        }

        [MenuItem("Build A Doll/Paint/Add Makeup Zones To Selected Doll", true)]
        private static bool CanAddZones() => Selection.activeGameObject != null;

        private static GameObject FindPrefab(string prefabName)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{prefabName} t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == prefabName)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }
    }
}
#endif
