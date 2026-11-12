using System.Collections.Generic;
using _Core._Player;
using Game.Hurdles;
using Game.Level;
using Game.Outfit;
using Game.Player;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// One-click level assets: hurdle prefabs, sample outfit items, three sample LevelData assets
    /// and a LevelDatabase. Assets only: it doesn't touch any scene (the player, LevelManager and
    /// GameplayScene are set up by hand in Bootstrap / Gameplay).
    /// Safe to run again: existing prefabs and levels are kept, and levels or the database whose
    /// references are broken (e.g. prefabs deleted) are repaired.
    /// </summary>
    public static class LevelSetup
    {
        private static string PrefabFolder => EditorPaths.HurdlePrefabs;
        private static string LevelFolder => EditorPaths.Levels;

        [MenuItem("Build A Doll/Create Sample Levels")]
        public static void Setup()
        {
            EditorPaths.EnsureFolder(PrefabFolder);
            EditorPaths.EnsureFolder(LevelFolder);

            var items = new OutfitItem[ItemGateSetup.Samples.Length];
            for (var i = 0; i < items.Length; i++)
                items[i] = ItemGateSetup.CreateItem(ItemGateSetup.Samples[i]);

            var hairGood = items[0];
            var hairBad = items[1];
            var dressGood = items[2];
            var dressBad = items[3];
            var shoesGood = items[4];
            var shoesOk = items[5];

            var gatePair = Prefab("GatePair", root => BuildGatePair(root, hairGood, hairBad, moving: false));
            var movingGatePair = Prefab("GatePair_Moving", root => BuildGatePair(root, hairGood, hairBad, moving: true));
            var coinLine = Prefab("Coins_Line", root => BuildCoins(root, wave: false));
            var coinWave = Prefab("Coins_Wave", root => BuildCoins(root, wave: true));
            var mud = Prefab("Penalty_Mud", BuildMud);
            var wideMud = Prefab("Penalty_WideMud", BuildWideMud);
            var scissors = Prefab("Penalty_Scissors", BuildScissors);
            var jumpWall = Prefab("Obstacle_JumpWall", BuildJumpWall);
            var slideBar = Prefab("Obstacle_SlideBar", BuildSlideBar);
            var finish = Prefab("FinishLine", BuildFinishLine);
            var demoDoll = Prefab("DemoDoll", BuildDemoDoll, EditorPaths.Prefabs);

            var levels = new List<LevelData>
            {
                Level("Level_01", "First Steps", 70f, new[] { 4, 10, 16 }, Look("Look_01", "Pink Princess", hairGood, dressGood, shoesGood), new[]
                {
                    E(coinLine, 8f),
                    E(gatePair, 20f, 0f, hairGood, hairBad),
                    E(coinWave, 29f),
                    E(gatePair, 45f, 0f, dressBad, dressGood),
                    E(coinLine, 50f, -1.2f),
                    E(gatePair, 60f, 0f, shoesGood, shoesOk),
                }),
                Level("Level_02", "Jump & Slide", 110f, new[] { 5, 12, 20 }, Look("Look_02", "Party Rebel", hairBad, dressGood, shoesOk), new[]
                {
                    E(coinLine, 6f),
                    E(jumpWall, 18f),
                    E(gatePair, 28f, 0f, hairBad, hairGood),
                    E(mud, 38f, 1.2f),
                    E(coinWave, 42f),
                    E(gatePair, 55f, 0f, dressGood, dressBad),
                    E(slideBar, 68f),
                    E(coinLine, 76f, 1.2f),
                    E(movingGatePair, 88f, 0f, shoesOk, shoesGood),
                    E(wideMud, 100f),
                }),
                Level("Level_03", "Scissors Street", 130f, new[] { 5, 12, 20 }, Look("Look_03", "Country Walk", hairGood, dressBad, shoesOk), new[]
                {
                    E(gatePair, 15f, 0f, hairGood, hairBad),
                    E(mud, 24f, -1.2f),
                    E(mud, 31f, 1.2f),
                    E(coinWave, 34f),
                    E(movingGatePair, 45f, 0f, dressBad, dressGood),
                    E(scissors, 58f),
                    E(coinLine, 62f, -1.2f),
                    E(jumpWall, 75f),
                    E(gatePair, 85f, 0f, shoesGood, shoesOk),
                    E(slideBar, 98f),
                    E(coinWave, 104f),
                    E(wideMud, 120f),
                }),
            };

            var database = CreateDatabase(levels, finish);
            SetIfEmpty(database, "defaultDemoPrefab", demoDoll);

            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(database);
            Debug.Log($"Levels ready: {levels.Count} levels in {AssetDatabase.GetAssetPath(database)}, hurdle prefabs in {PrefabFolder}. Press Play.", database);
        }

        // ---------- Prefab builders ----------

        private static GameObject Prefab(string name, System.Action<Transform> build, string folder = null)
        {
            folder ??= PrefabFolder;
            EditorPaths.EnsureFolder(folder);
            var path = $"{folder}/{name}.prefab";
            var existing = FindByName<GameObject>(name);
            if (existing != null)
                return existing;

            var root = new GameObject(name);
            build(root.transform);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void BuildGatePair(Transform root, OutfitItem left, OutfitItem right, bool moving)
        {
            root.gameObject.AddComponent<HurdleGroup>();
            BuildGate(root, "Gate_Left", left, -1.15f, new Color(1f, 0.55f, 0.75f, 0.5f));
            BuildGate(root, "Gate_Right", right, 1.15f, new Color(0.6f, 0.75f, 1f, 0.5f));

            if (moving)
                ItemGateSetup.AddMover(root, slide: 0.9f, slideSpeed: 0.35f);
        }

        private static void BuildGate(Transform pair, string name, OutfitItem item, float x, Color color)
        {
            var gate = ItemGateSetup.CreateChild(pair, name);
            gate.localPosition = new Vector3(x, 1.25f, 0f);

            var trigger = gate.gameObject.AddComponent<BoxCollider>();
            trigger.size = new Vector3(2.1f, 2.5f, 0.4f);

            var visual = ItemGateSetup.CreateChild(gate, "Visual");

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel";
            Object.DestroyImmediate(panel.GetComponent<Collider>());
            panel.transform.SetParent(visual, false);
            panel.transform.localScale = new Vector3(2.1f, 2.5f, 0.05f);
            panel.GetComponent<MeshRenderer>().sharedMaterial = ItemGateSetup.CreateMaterial(name, color, true);

            var previewAnchor = ItemGateSetup.CreateChild(visual, "PreviewAnchor");
            previewAnchor.localPosition = new Vector3(0f, 0.2f, -0.1f);

            var itemGate = gate.gameObject.AddComponent<ItemGate>();
            var so = new SerializedObject(itemGate);
            so.FindProperty("item").objectReferenceValue = item;
            so.FindProperty("activeVisual").objectReferenceValue = visual.gameObject;
            so.FindProperty("previewAnchor").objectReferenceValue = previewAnchor;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildCoins(Transform root, bool wave)
        {
            var material = ItemGateSetup.CreateMaterial("Coin", new Color(1f, 0.8f, 0.1f));

            for (var i = 0; i < 6; i++)
            {
                var x = wave ? Mathf.Sin(i * 0.9f) * 1.4f : 0f;

                var holder = ItemGateSetup.CreateChild(root, "Coin");
                holder.localPosition = new Vector3(x, 0.8f, i * 1.5f);

                // Pickup (trigger + script) stays active; only the mesh hides when collected.
                var sphere = holder.gameObject.AddComponent<SphereCollider>();
                sphere.radius = 0.45f;

                var mesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mesh.name = "Mesh";
                Object.DestroyImmediate(mesh.GetComponent<Collider>());
                mesh.transform.SetParent(holder, false);
                mesh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                mesh.transform.localScale = new Vector3(0.6f, 0.05f, 0.6f);
                mesh.GetComponent<MeshRenderer>().sharedMaterial = material;

                var collectable = holder.gameObject.AddComponent<Collectable>();
                var so = new SerializedObject(collectable);
                so.FindProperty("coins").intValue = 1;
                so.FindProperty("points").intValue = 1;
                so.FindProperty("activeVisual").objectReferenceValue = mesh;
                so.ApplyModifiedPropertiesWithoutUndo();

                ItemGateSetup.AddMover(holder, spin: 180f, phase: i / 6f);
            }
        }

        private static Material MudMaterial() => ItemGateSetup.CreateMaterial("Penalty_Mud", new Color(0.35f, 0.22f, 0.1f));
        private static Material BarrierMaterial() => ItemGateSetup.CreateMaterial("Penalty_Barrier", new Color(0.95f, 0.5f, 0.1f));

        private static void BuildMud(Transform root) =>
            ItemGateSetup.CreatePenalty(root, "Mud", PrimitiveType.Cylinder, MudMaterial(), new Vector3(0f, 0.05f, 0f), new Vector3(1.6f, 0.05f, 1.6f), 2, 0.6f, false);

        private static void BuildWideMud(Transform root) =>
            ItemGateSetup.CreatePenalty(root, "Mud", PrimitiveType.Cylinder, MudMaterial(), new Vector3(0f, 0.05f, 0f), new Vector3(ItemGateSetup.TrackWidth(), 0.05f, 2f), 2, 0.6f, false);

        private static void BuildScissors(Transform root)
        {
            var material = ItemGateSetup.CreateMaterial("Penalty_Scissors", new Color(0.85f, 0.1f, 0.1f));
            var cutter = ItemGateSetup.CreatePenalty(root, "Scissors", PrimitiveType.Cube, material, new Vector3(0f, 0.9f, 0f), new Vector3(0.9f, 0.9f, 0.9f), 3, 0.4f, true);
            ItemGateSetup.AddMover(cutter, slide: 1.8f, slideSpeed: 0.4f, spin: 120f);
        }

        private static void BuildJumpWall(Transform root) =>
            ItemGateSetup.CreatePenalty(root, "Wall", PrimitiveType.Cube, BarrierMaterial(), new Vector3(0f, 0.3f, 0f), new Vector3(ItemGateSetup.TrackWidth(), 0.6f, 0.3f), 3, 0.8f, false);

        private static void BuildSlideBar(Transform root)
        {
            var width = ItemGateSetup.TrackWidth();
            ItemGateSetup.CreatePenalty(root, "Bar", PrimitiveType.Cube, BarrierMaterial(), new Vector3(0f, 1.6f, 0f), new Vector3(width, 0.8f, 0.3f), 3, 0.8f, false);
            ItemGateSetup.CreatePost(root, BarrierMaterial(), new Vector3(-width * 0.5f, 1f, 0f));
            ItemGateSetup.CreatePost(root, BarrierMaterial(), new Vector3(width * 0.5f, 1f, 0f));
        }

        private static void BuildFinishLine(Transform root)
        {
            var width = ItemGateSetup.TrackWidth();
            var white = ItemGateSetup.CreateMaterial("Finish", Color.white);

            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "Stripe";
            Object.DestroyImmediate(stripe.GetComponent<Collider>());
            stripe.transform.SetParent(root, false);
            stripe.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            stripe.transform.localScale = new Vector3(width, 0.02f, 1f);
            stripe.GetComponent<MeshRenderer>().sharedMaterial = white;

            ItemGateSetup.CreatePost(root, white, new Vector3(-width * 0.5f, 1f, 0f));
            ItemGateSetup.CreatePost(root, white, new Vector3(width * 0.5f, 1f, 0f));

            var trigger = ItemGateSetup.CreateChild(root, "Trigger");
            trigger.localPosition = new Vector3(0f, 1.5f, 0f);
            var box = trigger.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(width + 2f, 3f, 0.5f);
            trigger.gameObject.AddComponent<FinishLine>();
        }

        // ---------- Level assets ----------

        private struct EntrySpec
        {
            public GameObject Prefab;
            public float Distance;
            public float Lateral;
            public OutfitItem[] Items;
        }

        private static EntrySpec E(GameObject prefab, float distance, float lateral = 0f, params OutfitItem[] items) =>
            new() { Prefab = prefab, Distance = distance, Lateral = lateral, Items = items };

        private static LevelData Level(string fileName, string displayName, float finishDistance, int[] stars, TargetLook look, EntrySpec[] entries)
        {
            // Keep a level that's already set up (it may have your edits). Refill it only if its
            // prefab/item references are broken, e.g. after the files they pointed to were deleted.
            var level = FindByName<LevelData>(fileName);
            if (level != null && !HasBrokenReferences(level))
            {
                // Older sample levels had no target look; give them one, leave everything else alone.
                SetIfEmpty(level, "targetLook", look);

                return level;
            }

            if (level == null)
            {
                level = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(level, $"{LevelFolder}/{fileName}.asset");
            }
            else
            {
                Debug.LogWarning($"'{level.name}' pointed at missing prefabs or items; refilled it with the sample layout.", level);
            }

            var so = new SerializedObject(level);
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("finishDistance").floatValue = finishDistance;
            so.FindProperty("targetLook").objectReferenceValue = look;

            var starProp = so.FindProperty("starScores");
            starProp.arraySize = stars.Length;
            for (var i = 0; i < stars.Length; i++)
                starProp.GetArrayElementAtIndex(i).intValue = stars[i];

            var list = so.FindProperty("entries");
            list.arraySize = entries.Length;
            for (var i = 0; i < entries.Length; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("prefab").objectReferenceValue = entries[i].Prefab;
                element.FindPropertyRelative("distance").floatValue = entries[i].Distance;
                element.FindPropertyRelative("lateral").floatValue = entries[i].Lateral;

                SetObjectArray(element.FindPropertyRelative("gateItems"), entries[i].Items);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
            return level;
        }

        private static T FindByName<T>(string assetName) where T : Object
        {
            foreach (var guid in AssetDatabase.FindAssets($"{assetName} t:{typeof(T).Name}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == assetName)
                    return AssetDatabase.LoadAssetAtPath<T>(path);
            }

            return null;
        }

        private static void SetObjectArray(SerializedProperty array, Object[] values)
        {
            values ??= new Object[0];
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        // A reference to a deleted asset reads as null but still has an instance ID: that's "broken".
        private static bool HasBrokenReferences(Object asset)
        {
            var it = new SerializedObject(asset).GetIterator();
            for (var more = it.Next(true); more; more = it.Next(true))
            {
                if (it.propertyType == SerializedPropertyType.ObjectReference &&
                    it.objectReferenceValue == null && it.objectReferenceInstanceIDValue != 0)
                    return true;
            }

            return false;
        }

        // A TargetLook asset in _Data/_TargetLooks. Kept if it already exists and isn't broken.
        private static TargetLook Look(string fileName, string displayName, params OutfitItem[] items)
        {
            var look = FindByName<TargetLook>(fileName);
            if (look != null && !HasBrokenReferences(look) && look.HasItems)
                return look;

            if (look == null)
            {
                EditorPaths.EnsureFolder(EditorPaths.TargetLooks);
                look = ScriptableObject.CreateInstance<TargetLook>();
                AssetDatabase.CreateAsset(look, $"{EditorPaths.TargetLooks}/{fileName}.asset");
            }

            var so = new SerializedObject(look);
            so.FindProperty("displayName").stringValue = displayName;
            SetObjectArray(so.FindProperty("items"), items);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(look);
            return look;
        }

        private static void SetIfEmpty(Object asset, string property, Object value)
        {
            var so = new SerializedObject(asset);
            var prop = so.FindProperty(property);
            if (prop == null || prop.objectReferenceValue != null)
                return;

            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        // Display-only doll for the target look: same attach points as the placeholder player,
        // no controller or collider, and its outfit doesn't raise player events.
        private static void BuildDemoDoll(Transform root)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Visual";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root, false);
            body.transform.localPosition = Vector3.up;
            body.GetComponent<MeshRenderer>().sharedMaterial = ItemGateSetup.CreateMaterial("DemoDoll", new Color(1f, 0.85f, 0.75f));

            var outfit = root.gameObject.AddComponent<DollOutfit>();
            var so = new SerializedObject(outfit);
            var anchors = so.FindProperty("anchors");
            ItemGateSetup.AddAnchor(anchors, body.transform, OutfitSlot.Hair, new Vector3(0f, 0.8f, 0f));
            ItemGateSetup.AddAnchor(anchors, body.transform, OutfitSlot.Dress, new Vector3(0f, -0.35f, 0f));
            ItemGateSetup.AddAnchor(anchors, body.transform, OutfitSlot.Shoes, new Vector3(0f, -0.95f, 0.1f));
            so.FindProperty("defaultAnchor").objectReferenceValue = body.transform;
            so.FindProperty("raiseEvents").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static LevelDatabase CreateDatabase(List<LevelData> levels, GameObject finish)
        {
            var database = EditorPaths.FindAsset<LevelDatabase>();
            if (database != null && !HasBrokenReferences(database) && database.Count > 0 && database.DefaultFinishLine != null)
                return database;

            if (database == null)
            {
                database = ScriptableObject.CreateInstance<LevelDatabase>();
                AssetDatabase.CreateAsset(database, $"{LevelFolder}/LevelDatabase.asset");
            }

            var so = new SerializedObject(database);
            var list = so.FindProperty("levels");
            list.arraySize = levels.Count;
            for (var i = 0; i < levels.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];
            so.FindProperty("defaultFinishLine").objectReferenceValue = finish;
            so.FindProperty("loopFromLevel").intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
            return database;
        }
    }
}
