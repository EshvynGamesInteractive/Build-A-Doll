#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Game.Hurdles;
using Game.Outfit;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the extra track pieces Level 1 uses (20 Nov 2026):
    /// - _Kit/Kit_Scissors: lane-sized scissors standing on the track. Hitting them cuts her HAIR off
    ///   (PenaltyItem removesItem, slot Hair) + stumble + coin cost. Too tall to jump, so you steer round them.
    ///   Uses the Scissor.fbx model (Assets/_ART/_3D/_Hurdles); stood up blade-tip up, scaled to ScissorsHeight, spinning.
    ///   Falls back to a primitive placeholder if the model is missing.
    /// - _Gates/CoinGate_SingleMoving: one cash gate (from CoinGatePair's right gate) sliding left/right across
    ///   both lanes. Amount comes from the level entry (plan section "coinmoving:+50").
    /// Re-runnable: prefabs are overwritten in place, so levels keep their references.
    /// - _Gates/SkinGatePair: two skin colour gates (left, right) built from CoinGatePair's frames, each with a colour swatch.
    ///   Running through one turns the doll's body that colour (SkinGate + DollSkin). Colours come from the level entry.
    /// Menu: Build A Doll > Hurdles > Build Scissors + Moving Cash Gate (also builds the skin gate pair).
    /// </summary>
    public static class GateExtrasBuilder
    {
        private const string Title = "Build Scissors + Moving Cash Gate";
        private static string KitFolder => EditorPaths.HurdlePrefabs + "/_Kit";
        private static string GateFolder => EditorPaths.HurdlePrefabs + "/_Gates";

        // Scissors size: about one lane (gate lanes are ±2.25, track half width 4), taller than a jump clears.
        private const float ScissorsHeight = 2.2f;
        private const float ScissorsWidth = 1.6f;

        // Moving cash gate: slides from one gate lane to the other.
        private const float CashSlide = 2.25f;
        private const float CashSlideSpeed = 0.3f; // cycles per second (~3.3 s left-right-left)

        [MenuItem("Build A Doll/Hurdles/Build Scissors + Moving Cash Gate")]
        public static void Build()
        {
            var made = new List<string>();
            var warnings = new List<string>();

            var scissors = BuildScissors(warnings);
            if (scissors != null) made.Add(Save(scissors, KitFolder));

            var cash = BuildMovingCashGate(warnings);
            if (cash != null) made.Add(Save(cash, GateFolder));

            var skin = BuildSkinGatePair(warnings);
            if (skin != null) made.Add(Save(skin, GateFolder));

            AssetDatabase.SaveAssets();
            var summary = made.Count > 0 ? "Built: " + string.Join(", ", made) : "Nothing built.";
            if (warnings.Count > 0) summary += "\n\n" + string.Join("\n", warnings);
            Debug.Log("[GateExtras] " + summary);
            EditorUtility.DisplayDialog(Title, summary, "OK");
        }

        // ---------- Scissors ----------
        private static GameObject BuildScissors(List<string> warnings)
        {
            var root = new GameObject("Kit_Scissors");
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);

            var art = FindModel("Scissor") ?? FindModel("Scissors");
            var width = ScissorsWidth;
            if (art != null)
            {
                // Spin pivot at the track centre, model under it.
                var spin = new GameObject("Spin").transform;
                spin.SetParent(model, false);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(art);
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = "Scissor";
                instance.transform.SetParent(spin, false);
                foreach (var c in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                StandUp(instance.transform);
                FitToHeight(instance.transform, ScissorsHeight);
                var b = WorldBounds(instance.transform);
                width = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z), 1f, 2.2f);
                Mover(spin.gameObject, spin: 90f); // turns on the spot so it reads from any angle
            }
            else
            {
                warnings.Add("Scissor.fbx not found under Assets/_ART: Kit_Scissors uses the placeholder model.");
                PlaceholderScissors(model);
            }

            // Gentle bob so it reads as a hazard, not scenery.
            Mover(model.gameObject, bob: 0.15f, bobSpeed: 0.8f);

            // Trigger: lane-sized, full height (no jumping over it: steer round).
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, ScissorsHeight * 0.5f, 0f);
            box.size = new Vector3(width * 0.85f, ScissorsHeight, Mathf.Min(width, 1f) * 0.6f);

            var penalty = root.AddComponent<PenaltyItem>();
            var so = new SerializedObject(penalty);
            so.FindProperty("aiAvoid").enumValueIndex = (int)PenaltyItem.AiAvoidance.SteerAround;
            so.FindProperty("coinCost").intValue = 2;
            so.FindProperty("stumbleDuration").floatValue = 0.5f;
            so.FindProperty("removesItem").boolValue = true;
            so.FindProperty("slotToRemove").intValue = (int)OutfitSlot.Hair;
            so.ApplyModifiedPropertiesWithoutUndo();

            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            return root;
        }

        // Two crossed blades that snip open/closed + two ring handles, standing up and facing the runner.
        private static void PlaceholderScissors(Transform model)
        {
            var steel = ItemGateSetup.CreateMaterial("Scissors_Blade", new Color(0.78f, 0.8f, 0.85f));
            var handle = ItemGateSetup.CreateMaterial("Penalty_Scissors", new Color(0.85f, 0.1f, 0.1f));
            var pivotY = ScissorsHeight * 0.45f;

            for (var side = -1; side <= 1; side += 2)
            {
                // Each half turns round the pivot screw; opposite phases make them snip.
                var half = new GameObject(side < 0 ? "Half_L" : "Half_R").transform;
                half.SetParent(model, false);
                half.localPosition = new Vector3(0f, pivotY, 0f);
                half.localRotation = Quaternion.Euler(0f, 0f, side * 14f);

                // Blade: from the pivot up and across.
                var blade = Primitive(PrimitiveType.Cube, "Blade", half, steel);
                blade.localPosition = new Vector3(-side * 0.18f, 0.55f, side * 0.03f);
                blade.localRotation = Quaternion.Euler(0f, 0f, side * 12f);
                blade.localScale = new Vector3(0.22f, 1.2f, 0.05f);

                // Arm down to the handle.
                var arm = Primitive(PrimitiveType.Cube, "Arm", half, handle);
                arm.localPosition = new Vector3(side * 0.16f, -0.35f, side * 0.03f);
                arm.localRotation = Quaternion.Euler(0f, 0f, side * 18f);
                arm.localScale = new Vector3(0.12f, 0.55f, 0.08f);

                // Ring handle (flat cylinder facing the runner).
                var ring = Primitive(PrimitiveType.Cylinder, "Handle", half, handle);
                ring.localPosition = new Vector3(side * 0.36f, -0.78f, side * 0.03f);
                ring.localRotation = Quaternion.Euler(90f, 0f, 0f);
                ring.localScale = new Vector3(0.42f, 0.04f, 0.42f);

                Mover(half.gameObject, swing: 10f, swingSpeed: 1.2f, phase: side < 0 ? 0f : 0.5f);
            }

            var screw = Primitive(PrimitiveType.Cylinder, "Pivot", model, steel);
            screw.localPosition = new Vector3(0f, pivotY, 0f);
            screw.localRotation = Quaternion.Euler(90f, 0f, 0f);
            screw.localScale = new Vector3(0.14f, 0.08f, 0.14f);

            // Small stand so it sits on the ramp.
            var stand = Primitive(PrimitiveType.Cylinder, "Stand", model, handle);
            stand.localPosition = new Vector3(0f, 0.04f, 0f);
            stand.localScale = new Vector3(ScissorsWidth * 0.6f, 0.04f, 0.6f);
        }

        // ---------- Moving cash gate ----------
        private static GameObject BuildMovingCashGate(List<string> warnings)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(GateFolder + "/CoinGatePair.prefab");
            if (source == null)
            {
                warnings.Add($"CoinGatePair.prefab not found in {GateFolder}; moving cash gate skipped.");
                return null;
            }

            var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            // Keep the nested Red/GreenVisual links, drop the link to CoinGatePair itself.
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            root.name = "CoinGate_SingleMoving";

            var gates = root.GetComponentsInChildren<CoinGate>(true);
            if (gates.Length == 0)
            {
                warnings.Add("CoinGatePair has no CoinGate; moving cash gate skipped.");
                Object.DestroyImmediate(root);
                return null;
            }

            // Keep the right-hand gate (first in hierarchy = the one the level's first coin setting configures).
            var keep = gates.OrderByDescending(g => g.transform.localPosition.x).First();
            foreach (var g in gates)
            {
                if (g != keep)
                    Object.DestroyImmediate(g.gameObject);
            }

            keep.gameObject.name = "Gate";
            var p = keep.transform.localPosition;
            keep.transform.localPosition = new Vector3(0f, p.y, p.z);

            var so = new SerializedObject(keep);
            so.FindProperty("operation").enumValueIndex = (int)CoinGate.Operation.Add;
            so.FindProperty("amount").intValue = 50;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Slide on the gate itself (as Gate_SingleMovingLeftToRight does), so the root keeps the level's lateral.
            Mover(keep.gameObject, slide: CashSlide, slideSpeed: CashSlideSpeed);
            return root;
        }

        // ---------- Helpers ----------
        private static Transform Primitive(PrimitiveType type, string name, Transform parent, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        // Motion = DOTweenAnimation components (DOTweenMotion), applied in Save once the prefab is fully built.
        private static void Mover(GameObject go, float slide = 0f, float slideSpeed = 0.5f, float bob = 0f, float bobSpeed = 1f,
            float swing = 0f, float swingSpeed = 0.5f, float phase = 0f, float spin = 0f)
        {
            var m = DOTweenMotion.Motion.Default;
            m.slide = slide; m.slideSpeed = slideSpeed;
            m.bob = bob; m.bobSpeed = bobSpeed;
            m.swing = swing; m.swingSpeed = swingSpeed;
            m.spin = spin;
            m.phase = phase;
            DOTweenMotion.Enqueue(go, m);
        }

        // ---------- Skin gate pair ----------
        private static readonly (string name, Color color)[] DefaultSkins =
        {
            ("Light", new Color(0.98f, 0.86f, 0.78f)), // left
            ("Tan", new Color(0.78f, 0.56f, 0.40f))    // right
        };

        private static GameObject BuildSkinGatePair(List<string> warnings)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(GateFolder + "/CoinGatePair.prefab");
            if (source == null)
            {
                warnings.Add($"CoinGatePair.prefab not found in {GateFolder}; skin gate pair skipped.");
                return null;
            }

            var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            root.name = "SkinGatePair";

            // Left gate first in the hierarchy, so level settings read left, right.
            var coins = root.GetComponentsInChildren<CoinGate>(true).OrderBy(g => g.transform.localPosition.x).ToList();
            var swatchMaterial = ItemGateSetup.CreateMaterial("SkinSwatch", Color.white);

            for (var i = 0; i < coins.Count; i++)
            {
                var go = coins[i].gameObject;
                go.transform.SetSiblingIndex(i);
                go.name = i == 0 ? "Gate_Left" : "Gate_Right";
                var hitSound = new SerializedObject(coins[i]).FindProperty("hitSound").objectReferenceValue;
                Object.DestroyImmediate(coins[i]);

                // Colour swatch floating in the gate.
                var swatch = Primitive(PrimitiveType.Sphere, "Swatch", go.transform, swatchMaterial);
                swatch.localPosition = new Vector3(0f, 0.2f, 0f);
                swatch.localScale = Vector3.one * 1.1f;
                Mover(swatch.gameObject, bob: 0.2f, bobSpeed: 0.7f, phase: i * 0.5f);

                var gate = go.AddComponent<SkinGate>();
                var so = new SerializedObject(gate);
                var skin = DefaultSkins[Mathf.Min(i, DefaultSkins.Length - 1)];
                so.FindProperty("colorName").stringValue = skin.name;
                so.FindProperty("color").colorValue = skin.color;
                so.FindProperty("redVisual").objectReferenceValue = ChildNamed(go.transform, "Red");
                so.FindProperty("greenVisual").objectReferenceValue = ChildNamed(go.transform, "Green");
                so.FindProperty("hitSound").objectReferenceValue = hitSound;
                var swatches = so.FindProperty("swatches");
                swatches.arraySize = 1;
                swatches.GetArrayElementAtIndex(0).objectReferenceValue = swatch.GetComponent<Renderer>();
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (coins.Count != 2)
                warnings.Add($"SkinGatePair: CoinGatePair had {coins.Count} gates (expected 2).");
            return root;
        }

        private static GameObject ChildNamed(Transform parent, string contains)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Contains(contains))
                    return child.gameObject;
            }

            return null;
        }

        // Turns the model so its longest side points up (scissors stand blade-tip up).
        private static void StandUp(Transform t)
        {
            var size = WorldBounds(t).size;
            if (size.x >= size.y && size.x >= size.z)
                t.localRotation = Quaternion.Euler(0f, 0f, 90f) * t.localRotation;
            else if (size.z >= size.y && size.z >= size.x)
                t.localRotation = Quaternion.Euler(-90f, 0f, 0f) * t.localRotation;
        }

        private static Bounds WorldBounds(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        private static GameObject FindModel(string fileName)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{fileName} t:Model", new[] { "Assets/_ART" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == fileName)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return null;
        }

        private static void FitToHeight(Transform t, float height)
        {
            var renderers = t.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            if (b.size.y > 0.0001f) t.localScale *= height / b.size.y;
            b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            t.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);
        }

        private static string Save(GameObject root, string folder)
        {
            EditorPaths.EnsureFolder(folder);
            DOTweenMotion.ApplyQueued(root);
            var name = root.name;
            PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/{name}.prefab");
            Object.DestroyImmediate(root);
            return name;
        }
    }
}
#endif
