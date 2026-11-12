using _Core._Player;
using Game.Hurdles;
using Game.Outfit;
using Game.Player;
using Game.Score;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Adds hurdles to the runner in the open scene: DollOutfit, RunScore and a hitbox on the player,
    /// placeholder outfit items, three left/right gate pairs (the last one moving),
    /// rows of spinning coins and minus items (one of them sliding scissors that cut the dress).
    /// Replace the sample items with real ones later; the gates just point at OutfitItem assets.
    /// </summary>
    public static class ItemGateSetup
    {
        private static string SampleFolder => EditorPaths.Samples;

        internal struct SampleItem
        {
            public string Name;
            public OutfitSlot Slot;
            public PrimitiveType Shape;
            public Vector3 Scale;
            public Color Color;
            public int Score;
        }

        // Pairs: [0] vs [1], [2] vs [3], [4] vs [5]
        internal static readonly SampleItem[] Samples =
        {
            new() { Name = "Hair_Blonde", Slot = OutfitSlot.Hair, Shape = PrimitiveType.Sphere, Scale = new Vector3(0.75f, 0.6f, 0.75f), Color = new Color(1f, 0.85f, 0.3f), Score = 2 },
            new() { Name = "Hair_Messy", Slot = OutfitSlot.Hair, Shape = PrimitiveType.Sphere, Scale = new Vector3(0.9f, 0.7f, 0.9f), Color = new Color(0.35f, 0.25f, 0.2f), Score = -1 },
            new() { Name = "Dress_Ballgown", Slot = OutfitSlot.Dress, Shape = PrimitiveType.Cylinder, Scale = new Vector3(1.3f, 0.45f, 1.3f), Color = new Color(1f, 0.35f, 0.6f), Score = 3 },
            new() { Name = "Dress_Sack", Slot = OutfitSlot.Dress, Shape = PrimitiveType.Cylinder, Scale = new Vector3(1.1f, 0.45f, 1.1f), Color = new Color(0.55f, 0.45f, 0.3f), Score = -2 },
            new() { Name = "Shoes_Heels", Slot = OutfitSlot.Shoes, Shape = PrimitiveType.Cube, Scale = new Vector3(0.7f, 0.15f, 0.5f), Color = new Color(0.9f, 0.1f, 0.2f), Score = 2 },
            new() { Name = "Shoes_Boots", Slot = OutfitSlot.Shoes, Shape = PrimitiveType.Cube, Scale = new Vector3(0.75f, 0.35f, 0.55f), Color = new Color(0.15f, 0.15f, 0.15f), Score = 0 },
        };

        [MenuItem("Build A Doll/Add Sample Hurdles")]
        public static void Setup()
        {
            var player = Object.FindFirstObjectByType<RunnerController>();
            if (player == null)
            {
                RunnerSceneSetup.Setup();
                player = Object.FindFirstObjectByType<RunnerController>();
            }

            SetupPlayer(player);

            var items = new OutfitItem[Samples.Length];
            for (var i = 0; i < Samples.Length; i++)
                items[i] = CreateItem(Samples[i]);

            foreach (var oldName in new[] { "ItemGates", "TrackHurdles" })
            {
                var old = GameObject.Find(oldName);
                if (old != null)
                    Undo.DestroyObjectImmediate(old);
            }

            var hurdles = new GameObject("TrackHurdles");
            Undo.RegisterCreatedObjectUndo(hurdles, "Create Track Hurdles");
            hurdles.transform.SetPositionAndRotation(player.transform.position, player.transform.rotation);
            var root = CreateChild(hurdles.transform, "ItemGates");

            for (var pair = 0; pair < Samples.Length / 2; pair++)
            {
                var gatePair = CreateGatePair(root, new Vector3(0f, 0f, 20f + pair * 25f), items[pair * 2], items[pair * 2 + 1], Samples[pair * 2].Color, Samples[pair * 2 + 1].Color);

                // Last pair slides, so the player has to time the choice.
                if (pair == Samples.Length / 2 - 1)
                    AddMover(gatePair, slide: 0.9f, slideSpeed: 0.35f);
            }

            CreateCollectables(CreateChild(hurdles.transform, "Collectables"));
            CreatePenalties(CreateChild(hurdles.transform, "Penalties"));

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            Debug.Log("Hurdles added. Press Play, hold to run: drag into gates to dress the doll, grab coins, dodge the red minus items.");
        }

        internal static void SetupPlayer(RunnerController player)
        {
            var go = player.gameObject;

            // Hitbox so gates can detect her. Kinematic: the controller moves her, not physics.
            if (go.GetComponent<Collider>() == null)
            {
                var capsule = Undo.AddComponent<CapsuleCollider>(go);
                capsule.center = Vector3.up;
                capsule.height = 2f;
                capsule.radius = 0.4f;
            }

            if (!go.TryGetComponent<Rigidbody>(out var body))
                body = Undo.AddComponent<Rigidbody>(go);
            body.isKinematic = true;
            body.useGravity = false;

            // Placeholder capsule's own collider would double-trigger; drop it.
            var visualCollider = go.transform.Find("Visual")?.GetComponent<Collider>();
            if (visualCollider != null)
                Undo.DestroyObjectImmediate(visualCollider);

            if (go.GetComponent<RunScore>() == null)
                Undo.AddComponent<RunScore>(go);

            if (go.GetComponent<DollOutfit>() != null)
                return;

            var outfit = Undo.AddComponent<DollOutfit>(go);
            var parent = go.transform.Find("Visual") ?? go.transform;

            // Anchors fit the placeholder capsule (local y -1..1). Move them onto the real doll later.
            var so = new SerializedObject(outfit);
            var anchors = so.FindProperty("anchors");
            AddAnchor(anchors, parent, OutfitSlot.Hair, new Vector3(0f, 0.8f, 0f));
            AddAnchor(anchors, parent, OutfitSlot.Dress, new Vector3(0f, -0.35f, 0f));
            AddAnchor(anchors, parent, OutfitSlot.Shoes, new Vector3(0f, -0.95f, 0.1f));
            so.FindProperty("defaultAnchor").objectReferenceValue = parent;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void AddAnchor(SerializedProperty list, Transform parent, OutfitSlot slot, Vector3 localPosition)
        {
            var anchor = new GameObject($"Anchor_{slot}").transform;
            anchor.SetParent(parent, false);
            anchor.localPosition = localPosition;
            Undo.RegisterCreatedObjectUndo(anchor.gameObject, "Create Anchor");

            var index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            var entry = list.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("slot").enumValueIndex = (int)slot;
            entry.FindPropertyRelative("anchor").objectReferenceValue = anchor;
        }

        internal static OutfitItem CreateItem(SampleItem sample)
        {
            EnsureFolder();

            var itemPath = $"{SampleFolder}/{sample.Name}.asset";
            var item = AssetDatabase.LoadAssetAtPath<OutfitItem>(itemPath);
            if (item != null)
                return item;

            var material = CreateMaterial(sample.Name, sample.Color);

            var temp = GameObject.CreatePrimitive(sample.Shape);
            temp.name = sample.Name;
            temp.transform.localScale = sample.Scale;
            Object.DestroyImmediate(temp.GetComponent<Collider>());
            temp.GetComponent<MeshRenderer>().sharedMaterial = material;
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, $"{SampleFolder}/{sample.Name}.prefab");
            Object.DestroyImmediate(temp);

            item = ScriptableObject.CreateInstance<OutfitItem>();
            AssetDatabase.CreateAsset(item, itemPath);

            var so = new SerializedObject(item);
            so.FindProperty("displayName").stringValue = sample.Name.Replace('_', ' ');
            so.FindProperty("slot").enumValueIndex = (int)sample.Slot;
            so.FindProperty("prefab").objectReferenceValue = prefab;
            so.FindProperty("styleScore").intValue = sample.Score;

            if (sample.Slot == OutfitSlot.Dress)
            {
                var clears = so.FindProperty("alsoClears");
                clears.arraySize = 2;
                clears.GetArrayElementAtIndex(0).enumValueIndex = (int)OutfitSlot.Top;
                clears.GetArrayElementAtIndex(1).enumValueIndex = (int)OutfitSlot.Bottom;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        internal static Transform CreateGatePair(Transform root, Vector3 localPosition, OutfitItem left, OutfitItem right, Color leftColor, Color rightColor)
        {
            var pair = CreateChild(root, $"GatePair_{left.Slot}");
            pair.localPosition = localPosition;
            pair.gameObject.AddComponent<HurdleGroup>();

            CreateGate(pair, left, leftColor, -1.15f);
            CreateGate(pair, right, rightColor, 1.15f);
            return pair;
        }

        internal static void CreateCollectables(Transform root)
        {
            var material = CreateMaterial("Coin", new Color(1f, 0.8f, 0.1f));

            // Short weaving lines of coins in the gaps between gates.
            float[] rowStarts = { 6f, 29f, 54f };
            for (var row = 0; row < rowStarts.Length; row++)
                CreateCoinRow(root, material, rowStarts[row], row);
        }

        private static void CreateCoinRow(Transform root, Material material, float startZ, int row)
        {
            for (var i = 0; i < 6; i++)
            {
                var x = Mathf.Sin((i + row * 2) * 0.6f) * 1.4f;
                var coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                coin.name = "Coin";
                coin.transform.SetParent(root, false);
                coin.transform.localPosition = new Vector3(x, 0.8f, startZ + i * 1.5f);
                coin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                coin.transform.localScale = new Vector3(0.6f, 0.05f, 0.6f);
                coin.GetComponent<MeshRenderer>().sharedMaterial = material;

                // Capsule collider from the primitive is fine as the trigger; Collectable makes it one.
                var collectable = coin.AddComponent<Collectable>();
                var so = new SerializedObject(collectable);
                so.FindProperty("coins").intValue = 1;
                so.FindProperty("points").intValue = 1;
                so.ApplyModifiedPropertiesWithoutUndo();

                // Spin around the world up axis: wrap in a parent so the coin's tilt stays.
                var holder = CreateChild(root, "CoinHolder");
                holder.localPosition = coin.transform.localPosition;
                coin.transform.SetParent(holder, true);
                AddMover(holder, spin: 180f, phase: i / 6f);
            }
        }

        internal static void CreatePenalties(Transform root)
        {
            var mud = CreateMaterial("Penalty_Mud", new Color(0.35f, 0.22f, 0.1f));
            var scissors = CreateMaterial("Penalty_Scissors", new Color(0.85f, 0.1f, 0.1f));

            // Mud puddle: static, slows her and costs points.
            CreatePenalty(root, "Mud", PrimitiveType.Cylinder, mud, new Vector3(1.2f, 0.05f, 30f), new Vector3(1.6f, 0.05f, 1.6f), 2, 0.6f, false);
            CreatePenalty(root, "Mud", PrimitiveType.Cylinder, mud, new Vector3(-1.2f, 0.05f, 37f), new Vector3(1.6f, 0.05f, 1.6f), 2, 0.6f, false);

            // Scissors: slide across the track after the dress gate and cut the dress off.
            var cutter = CreatePenalty(root, "Scissors", PrimitiveType.Cube, scissors, new Vector3(0f, 0.9f, 58f), new Vector3(0.9f, 0.9f, 0.9f), 3, 0.4f, true);
            AddMover(cutter, slide: 1.8f, slideSpeed: 0.4f, spin: 120f);

            // Full-width obstacles you can't steer around.
            var barrier = CreateMaterial("Penalty_Barrier", new Color(0.95f, 0.5f, 0.1f));
            var width = TrackWidth();

            // Low wall: swipe up to jump over it (top at 0.6, jump clears 1.6).
            CreatePenalty(root, "JumpWall", PrimitiveType.Cube, barrier, new Vector3(0f, 0.3f, 16.5f), new Vector3(width, 0.6f, 0.3f), 3, 0.8f, false);

            // High bar: swipe down to slide under it (bottom at 1.2, standing doll is 2 tall, sliding 0.9).
            CreatePenalty(root, "SlideBar", PrimitiveType.Cube, barrier, new Vector3(0f, 1.6f, 85f), new Vector3(width, 0.8f, 0.3f), 3, 0.8f, false);
            CreatePost(root, barrier, new Vector3(-width * 0.5f, 1f, 85f));
            CreatePost(root, barrier, new Vector3(width * 0.5f, 1f, 85f));

            // Jump over a mud puddle that covers the whole track.
            CreatePenalty(root, "WideMud", PrimitiveType.Cylinder, mud, new Vector3(0f, 0.05f, 95f), new Vector3(width, 0.05f, 2f), 2, 0.6f, false);
        }

        internal static float TrackWidth()
        {
            var config = EditorPaths.FindAsset<Game.Player.Config.RunnerMovementConfig>();
            return config != null ? config.TrackHalfWidth * 2f + 1f : 5.4f;
        }

        // Decoration only, no collider.
        internal static void CreatePost(Transform root, Material material, Vector3 localPosition)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "Post";
            Object.DestroyImmediate(post.GetComponent<Collider>());
            post.transform.SetParent(root, false);
            post.transform.localPosition = localPosition;
            post.transform.localScale = new Vector3(0.2f, 2f, 0.2f);
            post.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        internal static Transform CreatePenalty(Transform root, string name, PrimitiveType shape, Material material, Vector3 localPosition, Vector3 scale, int pointsLost, float stumble, bool cutsDress)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(root, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;

            // Primitive collider is fine as the trigger; for the flat mud make sure it's tall enough to hit.
            if (go.TryGetComponent<CapsuleCollider>(out var capsule))
            {
                Object.DestroyImmediate(capsule);
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(1f, 20f, 1f);
            }

            var penalty = go.AddComponent<PenaltyItem>();
            var so = new SerializedObject(penalty);
            so.FindProperty("pointsLost").intValue = pointsLost;
            so.FindProperty("stumbleDuration").floatValue = stumble;
            so.FindProperty("removesItem").boolValue = cutsDress;
            so.FindProperty("slotToRemove").enumValueIndex = (int)OutfitSlot.Dress;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go.transform;
        }

        internal static void AddMover(Transform target, float slide = 0f, float slideSpeed = 0.5f, float spin = 0f, float phase = 0f)
        {
            var mover = target.gameObject.AddComponent<HurdleMover>();
            var so = new SerializedObject(mover);
            so.FindProperty("slideDistance").floatValue = slide;
            so.FindProperty("slideSpeed").floatValue = slideSpeed;
            so.FindProperty("spinSpeed").floatValue = spin;
            so.FindProperty("phase").floatValue = phase;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        internal static void CreateGate(Transform pair, OutfitItem item, Color color, float x)
        {
            var gate = new GameObject($"Gate_{item.name}");
            gate.transform.SetParent(pair, false);
            gate.transform.localPosition = new Vector3(x, 1.25f, 0f);

            var trigger = gate.AddComponent<BoxCollider>();
            trigger.size = new Vector3(2.1f, 2.5f, 0.4f);

            // Panel + preview of what she'll get. Hidden once the gate is used.
            var visual = new GameObject("Visual");
            visual.transform.SetParent(gate.transform, false);

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Panel";
            Object.DestroyImmediate(panel.GetComponent<Collider>());
            panel.transform.SetParent(visual.transform, false);
            panel.transform.localScale = new Vector3(2.1f, 2.5f, 0.05f);
            var transparent = color;
            transparent.a = 0.55f;
            panel.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial($"Gate_{item.name}", transparent, true);

            // Item preview floating in the panel.
            if (item.Prefab != null)
            {
                var preview = (GameObject)PrefabUtility.InstantiatePrefab(item.Prefab, visual.transform);
                preview.name = "Preview";
                preview.transform.localPosition = new Vector3(0f, 0.2f, -0.1f);
            }

            var itemGate = gate.AddComponent<ItemGate>();
            var so = new SerializedObject(itemGate);
            so.FindProperty("item").objectReferenceValue = item;
            so.FindProperty("activeVisual").objectReferenceValue = visual;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static Material CreateMaterial(string name, Color color, bool transparent = false)
        {
            EnsureFolder();

            var path = $"{SampleFolder}/M_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (transparent && material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        internal static void EnsureFolder()
        {
            EditorPaths.EnsureFolder(SampleFolder);
        }
    }
}
