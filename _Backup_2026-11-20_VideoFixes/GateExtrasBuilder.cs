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
    ///   Uses the Scissor.fbx model (Assets/_ART/_3D/_Hurdles), scaled to ScissorsHeight: slides left-right and the two blades snip round the screw.
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
        private const float ScissorsHeight = 2.4f;
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

            var ad = BuildRewardedAdGate(warnings);
            if (ad != null) made.Add(Save(ad, GateFolder));

            AssetDatabase.SaveAssets();
            var summary = made.Count > 0 ? "Built: " + string.Join(", ", made) : "Nothing built.";
            if (warnings.Count > 0) summary += "\n\n" + string.Join("\n", warnings);
            Debug.Log("[GateExtras] " + summary);
            EditorUtility.DisplayDialog(Title, summary, "OK");
        }

        // Builds _Gates/RewardedAdGate and puts one in Level_02 (76 m, right lane) if it isn't there yet.
        [MenuItem("Build A Doll/Hurdles/Build Rewarded Ad Gate (+ add to Level 2)")]
        public static void BuildRewardedAdGateOnly()
        {
            var warnings = new List<string>();
            var gate = BuildRewardedAdGate(warnings);
            var summary = "Nothing built.";
            if (gate != null)
            {
                var name = Save(gate, GateFolder);
                AssetDatabase.SaveAssets();
                summary = $"Built {name}.";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{GateFolder}/{name}.prefab");
                summary += "\n" + AddEntryIfMissing("Level_02", prefab, 76f, 2.25f);
            }

            if (warnings.Count > 0) summary += "\n\n" + string.Join("\n", warnings);
            Debug.Log("[GateExtras] " + summary);
            EditorUtility.DisplayDialog(Title, summary, "OK");
        }

        // Single gate (CoinGatePair's right frame, green) with an "AD" sign: RewardedAdGate gives back what scissors cut.
        private static GameObject BuildRewardedAdGate(List<string> warnings)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(GateFolder + "/CoinGatePair.prefab");
            if (source == null)
            {
                warnings.Add($"CoinGatePair.prefab not found in {GateFolder}; rewarded ad gate skipped.");
                return null;
            }

            var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            root.name = "RewardedAdGate";

            var coins = root.GetComponentsInChildren<CoinGate>(true);
            if (coins.Length == 0)
            {
                Object.DestroyImmediate(root);
                warnings.Add("CoinGatePair has no CoinGate; rewarded ad gate skipped.");
                return null;
            }

            var keep = coins.OrderByDescending(g => g.transform.localPosition.x).First();
            foreach (var c in coins)
                if (c != keep) Object.DestroyImmediate(c.gameObject);

            var go = keep.gameObject;
            go.name = "Gate";
            var p = go.transform.localPosition;
            go.transform.localPosition = new Vector3(0f, p.y, p.z);
            var hitSound = new SerializedObject(keep).FindProperty("hitSound").objectReferenceValue;
            Object.DestroyImmediate(keep);

            // "AD" sign in the gate (TextMeshPro, editable in the prefab).
            var sign = new GameObject("AdSign").transform;
            sign.SetParent(go.transform, false);
            sign.localPosition = new Vector3(0f, 0.3f, -0.05f);
            var text = sign.gameObject.AddComponent<TMPro.TextMeshPro>();
            text.text = "<size=140%>AD</size>\n+50"; // RewardedAdGate switches it to HAIR BACK after a cut
            text.fontSize = 4f;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.color = Color.white;
            text.rectTransform.sizeDelta = new Vector2(3.5f, 2f);

            var gate = go.AddComponent<RewardedAdGate>();
            var so = new SerializedObject(gate);
            so.FindProperty("redVisual").objectReferenceValue = ChildNamed(go.transform, "Red");
            so.FindProperty("greenVisual").objectReferenceValue = ChildNamed(go.transform, "Green");
            so.FindProperty("hitSound").objectReferenceValue = hitSound;
            so.FindProperty("sign").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // Adds an entry for 'prefab' to a level asset (sorted by distance) unless that level already uses the prefab.
        private static string AddEntryIfMissing(string levelName, GameObject prefab, float distance, float lateral)
        {
            var level = AssetDatabase.LoadAssetAtPath<Game.Level.LevelData>($"{EditorPaths.Levels}/{levelName}.asset");
            if (level == null || prefab == null)
                return $"{levelName} not found: add the gate to a level by hand.";

            var so = new SerializedObject(level);
            var entries = so.FindProperty("entries");
            for (var i = 0; i < entries.arraySize; i++)
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue == prefab)
                    return $"{levelName} already has it.";

            var index = 0;
            while (index < entries.arraySize && entries.GetArrayElementAtIndex(index).FindPropertyRelative("distance").floatValue <= distance)
                index++;
            entries.InsertArrayElementAtIndex(index);
            var e = entries.GetArrayElementAtIndex(index);
            e.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            e.FindPropertyRelative("distance").floatValue = distance;
            e.FindPropertyRelative("lateral").floatValue = lateral;
            e.FindPropertyRelative("gateItems").arraySize = 0;
            e.FindPropertyRelative("greenSide").enumValueIndex = 0;
            e.FindPropertyRelative("paintGates").arraySize = 0;
            e.FindPropertyRelative("coinGates").arraySize = 0;
            e.FindPropertyRelative("skinGates").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
            return $"Added to {levelName} at {distance} m, lateral {lateral}.";
        }

        // ---------- Makeup station (mirror) ----------
        // Builds _Gates/MakeupStation and, in Level_03, swaps the lips / eye shadow paint gate pairs for stations
        // with a 3-colour palette (target colour free, one for 500 coins, one for a rewarded ad).
        [MenuItem("Build A Doll/Hurdles/Build Makeup Station (+ use in Level 3)")]
        public static void BuildMakeupStationOnly()
        {
            var warnings = new List<string>();
            var station = BuildMakeupStation(warnings);
            var summary = "Nothing built.";
            if (station != null)
            {
                var name = Save(station, GateFolder);
                AssetDatabase.SaveAssets();
                summary = $"Built {name}.";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{GateFolder}/{name}.prefab");
                summary += "\n" + UseStationsInLevel("Level_03", prefab);
            }

            if (warnings.Count > 0) summary += "\n\n" + string.Join("\n", warnings);
            Debug.Log("[GateExtras] " + summary);
            EditorUtility.DisplayDialog(Title, summary, "OK");
        }

        // A mirror in a gate frame with a "MAKEUP" sign. Art can replace the Mirror quad / frame later.
        private static GameObject BuildMakeupStation(List<string> warnings)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(GateFolder + "/CoinGatePair.prefab");
            if (source == null)
            {
                warnings.Add($"CoinGatePair.prefab not found in {GateFolder}; makeup station skipped.");
                return null;
            }

            var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            root.name = "MakeupStation";

            var coins = root.GetComponentsInChildren<CoinGate>(true);
            if (coins.Length == 0)
            {
                Object.DestroyImmediate(root);
                warnings.Add("CoinGatePair has no CoinGate; makeup station skipped.");
                return null;
            }

            var keep = coins.OrderByDescending(g => g.transform.localPosition.x).First();
            foreach (var c in coins)
                if (c != keep) Object.DestroyImmediate(c.gameObject);

            var go = keep.gameObject;
            go.name = "Mirror";
            var p = go.transform.localPosition;
            go.transform.localPosition = new Vector3(0f, p.y, p.z);
            var hitSound = new SerializedObject(keep).FindProperty("hitSound").objectReferenceValue;
            Object.DestroyImmediate(keep);

            var glass = Primitive(PrimitiveType.Quad, "Glass", go.transform, ItemGateSetup.CreateMaterial("MakeupMirror", new Color(0.75f, 0.9f, 1f)));
            glass.localPosition = new Vector3(0f, 0f, 0.05f); // a Quad faces -Z: towards the runner
            glass.localScale = new Vector3(3f, 2.2f, 1f);

            // Can't be missed: the trigger spans the whole track (half width 4).
            if (go.TryGetComponent<BoxCollider>(out var box))
                box.size = new Vector3(8.5f, box.size.y, box.size.z);

            var sign = new GameObject("Sign").transform;
            sign.SetParent(go.transform, false);
            sign.localPosition = new Vector3(0f, 1.5f, -0.05f);
            var text = sign.gameObject.AddComponent<TMPro.TextMeshPro>();
            text.text = "MAKEUP";
            text.fontSize = 5f;
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.color = Color.white;
            text.rectTransform.sizeDelta = new Vector2(4f, 1.2f);

            var station = go.AddComponent<MakeupStation>();
            var so = new SerializedObject(station);
            so.FindProperty("redVisual").objectReferenceValue = ChildNamed(go.transform, "Red");
            so.FindProperty("greenVisual").objectReferenceValue = ChildNamed(go.transform, "Green");
            so.FindProperty("hitSound").objectReferenceValue = hitSound;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // Level_03: lips / eye shadow paint gate pairs → makeup stations (same place, centre), palette in the entry.
        private static string UseStationsInLevel(string levelName, GameObject station)
        {
            var level = AssetDatabase.LoadAssetAtPath<Game.Level.LevelData>($"{EditorPaths.Levels}/{levelName}.asset");
            if (level == null || station == null)
                return $"{levelName} not found: put the station in a level by hand.";

            var so = new SerializedObject(level);
            var entries = so.FindProperty("entries");
            var swapped = 0;
            for (var i = 0; i < entries.arraySize; i++)
            {
                var e = entries.GetArrayElementAtIndex(i);
                var prefab = e.FindPropertyRelative("prefab").objectReferenceValue as GameObject;
                if (prefab == null || !prefab.name.StartsWith("PaintGatePair")) continue;

                var lips = prefab.name.Contains("Lips");
                (string name, Color color)[] palette = lips
                    ? new[] { ("Pink", new Color(1f, 0.4f, 0.85f)), ("Red", new Color(0.85f, 0.1f, 0.15f)), ("Coral", new Color(1f, 0.5f, 0.4f)) }
                    : new[] { ("Brown", new Color(0.68f, 0.32f, 0.08f)), ("Pink", new Color(1f, 0.4f, 0.85f)), ("Purple", new Color(0.6f, 0.3f, 0.9f)) };
                var zone = lips ? Game.Outfit.MakeupZone.Lips : Game.Outfit.MakeupZone.EyeShadow;

                e.FindPropertyRelative("prefab").objectReferenceValue = station;
                e.FindPropertyRelative("lateral").floatValue = 0f;
                e.FindPropertyRelative("greenSide").enumValueIndex = 0;
                var paint = e.FindPropertyRelative("paintGates");
                paint.arraySize = palette.Length;
                for (var k = 0; k < palette.Length; k++)
                {
                    var item = paint.GetArrayElementAtIndex(k);
                    item.FindPropertyRelative("zone").enumValueIndex = (int)zone;
                    item.FindPropertyRelative("colorName").stringValue = palette[k].name;
                    item.FindPropertyRelative("color").colorValue = palette[k].color;
                }
                swapped++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
            AssetDatabase.SaveAssets();
            return swapped > 0 ? $"{levelName}: {swapped} paint gate pair(s) replaced by makeup stations." : $"{levelName}: no paint gate pairs to replace (already done?).";
        }

        [MenuItem("Build A Doll/Hurdles/Rebuild Kit_Scissors Only")]
        public static void BuildScissorsOnly()
        {
            var warnings = new List<string>();
            var scissors = BuildScissors(warnings);
            var summary = scissors != null ? "Rebuilt " + Save(scissors, KitFolder) + "." : "Nothing built.";
            AssetDatabase.SaveAssets();
            if (warnings.Count > 0) summary += "\n\n" + string.Join("\n", warnings);
            Debug.Log("[GateExtras] " + summary);
            EditorUtility.DisplayDialog(Title, summary, "OK");
        }

        // ---------- Scissors ----------
        // Scissors: slide left-right across the lane, and the two blades open and close round the screw like real
        // scissors cutting (each blade on its own pivot at the screw, turning the opposite way). All motion is DOTween.
        // Hierarchy: Kit_Scissors > Slide (moves; trigger + PenaltyItem) > Model > Blade_A / Blade_B (pivots at the screw).
        private const float ScissorsSlide = 1.5f;      // ± sideways, world units
        private const float ScissorsSlideSpeed = 0.3f; // cycles per second
        private const float SnipAngle = 14f;           // each blade turns this far open/closed
        private const float SnipTime = 0.3f;           // seconds per open (and per close)

        private static GameObject BuildScissors(List<string> warnings)
        {
            var root = new GameObject("Kit_Scissors");
            var slide = new GameObject("Slide").transform;
            slide.SetParent(root.transform, false);
            var model = new GameObject("Model").transform;
            model.SetParent(slide, false);

            var art = FindModel("Scissor") ?? FindModel("Scissors");
            var width = ScissorsWidth;
            if (art != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(art);
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = "Scissor";
                instance.transform.SetParent(model, false);
                foreach (var c in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                FitToHeight(instance.transform, ScissorsHeight); // the model is already upright, facing the runner
                var b = WorldBounds(instance.transform);
                width = Mathf.Clamp(b.size.x, 1f, 2.6f);
                RigBlades(instance.transform, warnings);
            }
            else
            {
                warnings.Add("Scissor.fbx not found under Assets/_ART: Kit_Scissors uses the placeholder model.");
                PlaceholderScissors(model);
            }

            // Left-right across the lane.
            Mover(slide.gameObject, slide: ScissorsSlide, slideSpeed: ScissorsSlideSpeed);

            // Trigger moves with the scissors. Full height: steer round, no jumping over.
            var box = slide.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, ScissorsHeight * 0.5f, 0f);
            box.size = new Vector3(width * 0.8f, ScissorsHeight, 0.6f);

            var penalty = slide.gameObject.AddComponent<PenaltyItem>();
            var so = new SerializedObject(penalty);
            so.FindProperty("aiAvoid").enumValueIndex = (int)PenaltyItem.AiAvoidance.SteerAround;
            so.FindProperty("coinCost").intValue = 2;
            so.FindProperty("stumbleDuration").floatValue = 0.5f;
            so.FindProperty("removesItem").boolValue = true;
            so.FindProperty("slotToRemove").intValue = (int)OutfitSlot.Hair;
            so.FindProperty("activeVisual").objectReferenceValue = model.gameObject; // one use: the scissors vanish after cutting
            so.ApplyModifiedPropertiesWithoutUndo();

            var body = slide.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            return root;
        }

        // Puts each blade mesh on a pivot at the screw and makes them snip in opposite directions.
        private static void RigBlades(Transform scissor, List<string> warnings)
        {
            var blades = scissor.GetComponentsInChildren<Renderer>(true).Where(r => MeshOf(r) != null).ToList();
            if (blades.Count < 2)
            {
                warnings.Add($"Scissor model has {blades.Count} mesh(es); the blades need to be two separate meshes to snip. Kit_Scissors only slides.");
                return;
            }

            // Two biggest meshes = the blades.
            blades = blades.OrderByDescending(r => r.bounds.size.sqrMagnitude).Take(2).ToList();
            var screw = FindScrew(blades);
            var all = WorldBounds(scissor);
            // The blades turn in the scissors' flat plane: axis = its thinnest side.
            var size = all.size;
            var axis = size.z <= size.x && size.z <= size.y ? Vector3.forward : size.x <= size.y ? Vector3.right : Vector3.up;

            for (var i = 0; i < 2; i++)
            {
                var blade = blades[i].transform;
                var pivot = new GameObject(i == 0 ? "Blade_A" : "Blade_B").transform;
                pivot.SetParent(blade.parent, false);
                pivot.position = screw;
                pivot.rotation = Quaternion.identity;
                blade.SetParent(pivot, true);

                var localAxis = pivot.InverseTransformDirection(axis).normalized;
                DOTweenMotion.AddTween(pivot.gameObject, DG.Tweening.DOTweenAnimation.AnimationType.LocalRotate, SnipTime,
                    localAxis * (i == 0 ? SnipAngle : -SnipAngle), DG.Tweening.Ease.InOutSine, DG.Tweening.LoopType.Yoyo,
                    DG.Tweening.RotateMode.LocalAxisAdd, relative: false);
            }
        }

        // The screw: the smallest separate piece inside the blade meshes (the model has a small screw part).
        // Fallback: the middle of where the two blades' boxes overlap.
        private static Vector3 FindScrew(List<Renderer> blades)
        {
            Bounds? best = null;
            var bestCount = int.MaxValue;
            foreach (var r in blades)
            {
                var mesh = MeshOf(r);
                foreach (var (bounds, count) in MeshParts(mesh, r.transform.localToWorldMatrix))
                {
                    if (count < bestCount && count < mesh.vertexCount * 0.4f)
                    {
                        best = bounds;
                        bestCount = count;
                    }
                }
            }

            if (best.HasValue)
                return best.Value.center;

            var a = blades[0].bounds;
            var b = blades[1].bounds;
            var min = Vector3.Max(a.min, b.min);
            var max = Vector3.Min(a.max, b.max);
            return (min + max) * 0.5f;
        }

        private static Mesh MeshOf(Renderer r) =>
            r is SkinnedMeshRenderer skinned ? skinned.sharedMesh : r.TryGetComponent<MeshFilter>(out var f) ? f.sharedMesh : null;

        // Connected pieces of a mesh (vertices joined by triangles or at the same position), as world bounds + vertex count.
        private static List<(Bounds bounds, int count)> MeshParts(Mesh mesh, Matrix4x4 toWorld)
        {
            var verts = mesh.vertices;
            var parent = new int[verts.Length];
            for (var i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            void Join(int x, int y) { x = Find(x); y = Find(y); if (x != y) parent[x] = y; }

            var tris = mesh.triangles;
            for (var t = 0; t + 2 < tris.Length; t += 3) { Join(tris[t], tris[t + 1]); Join(tris[t], tris[t + 2]); }
            var seen = new Dictionary<Vector3Int, int>();
            for (var i = 0; i < verts.Length; i++)
            {
                var key = Vector3Int.RoundToInt(verts[i] * 1000f);
                if (seen.TryGetValue(key, out var other)) Join(i, other); else seen[key] = i;
            }

            var groups = new Dictionary<int, (Bounds b, int n)>();
            for (var i = 0; i < verts.Length; i++)
            {
                var p = toWorld.MultiplyPoint3x4(verts[i]);
                var root = Find(i);
                if (groups.TryGetValue(root, out var g)) { g.b.Encapsulate(p); g.n++; groups[root] = g; }
                else groups[root] = (new Bounds(p, Vector3.zero), 1);
            }

            return groups.Values.Select(g => (g.b, g.n)).ToList();
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
