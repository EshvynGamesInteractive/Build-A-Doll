#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Game.Hurdles;
using UnityEditor;
using UnityEngine;
using Avoid = Game.Hurdles.PenaltyItem.AiAvoidance;

namespace Game.EditorTools
{
    /// <summary>
    /// Turns the hurdle models in _ART/_3D/_Hurdles (Hurdle1..26.fbx) into ready-to-place prefabs in
    /// _Prefabs/_Hurdles/_Kit: scaled to the track, with trigger boxes, PenaltyItem (stumble + coin cost, AI hint)
    /// and DOTweenAnimation motion (Inspector-editable).
    /// Re-runnable: prefabs are overwritten in place, so levels keep their references.
    /// Menu: Build A Doll > Hurdles > Build Hurdle Kit Prefabs.
    /// </summary>
    public static class HurdleKitBuilder
    {
        private const string Title = "Build Hurdle Kit";
        private static string KitFolder => EditorPaths.HurdlePrefabs + "/_Kit";

        private const float FullWidth = 8.9f;    // Hurdle16's rail is scaled to this (old walls are 9 wide)
        private const float JumpTop = 1.1f;      // jump-over boxes stop here so an ordinary jump (1.6 high) clears them
        private const float SlideBottom = 0.95f; // slide-under boxes start here (slide hitbox is 0.9 high)

        [MenuItem("Build A Doll/Hurdles/Build Hurdle Kit Prefabs")]
        public static void Build()
        {
            var models = FindModels();
            if (!models.TryGetValue("Hurdle16", out var reference))
            {
                EditorUtility.DisplayDialog(Title, "Hurdle16.fbx not found under Assets/_ART (it sets the kit's scale).", "OK");
                return;
            }

            var scale = FullWidth / Mathf.Max(0.0001f, MeasureWidth(reference));
            EditorPaths.EnsureFolder(KitFolder);
            var made = new List<string>();
            var warnings = new List<string>();

            try
            {
                for (var i = 1; i <= 26; i++)
                {
                    EditorUtility.DisplayProgressBar(Title, $"Hurdle{i}", i / 26f);
                    if (!models.TryGetValue($"Hurdle{i}", out var model))
                    {
                        warnings.Add($"Hurdle{i}.fbx not found.");
                        continue;
                    }

                    var root = BuildHurdle(i, model, scale);
                    if (root != null)
                        made.Add(Save(root));
                }

            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }

            var summary = $"{made.Count} prefabs in {KitFolder} (hurdle scale {scale:0.###}).";
            if (warnings.Count > 0)
                summary += "\n\n" + string.Join("\n", warnings);
            Debug.Log($"[HurdleKit] {summary}\n{string.Join(", ", made)}");
            EditorUtility.DisplayDialog(Title, summary, "OK");
        }

        // ---------- Hurdles ----------
        private static GameObject BuildHurdle(int id, GameObject asset, float s)
        {
            switch (id)
            {
                case 1: return Spinner("Kit_Spinner_Cross", asset, s, 90f);
                case 2: return Spinner("Kit_Spinner_Tri", asset, s, 100f);
                case 3: return Spinner("Kit_Spinner_Bar", asset, s, 120f);
                case 4: return Spinner("Kit_SweepArm_Long", asset, s, 140f);
                case 5: return Spinner("Kit_SweepArm_Short", asset, s, 160f);
                case 18: return Spinner("Kit_Paddles", asset, s, 110f);
                case 23: return Spinner("Kit_Puncher", asset, s, 130f);

                case 6: return Roller("Kit_SpikeRoller", asset, s);
                case 7: return Static("Kit_SlimeTray", asset, s, Avoid.SteerAround, 2, 0.6f, 0.5f);
                case 8: return Static("Kit_LowBlock", asset, s, Avoid.Jump, 2, 0.5f, JumpTop);
                case 11: return Static("Kit_Hurdle", asset, s, Avoid.Jump, 2, 0.5f, JumpTop);
                case 12: return Static("Kit_Bollard", asset, s * 1.6f, Avoid.SteerAround, 1, 0.4f);
                case 13: return Static("Kit_Cone", asset, s * 2f, Avoid.SteerAround, 1, 0.4f);
                case 14: return Static("Kit_Puddle", asset, s * 2f, Avoid.SteerAround, 2, 0.6f, 0.3f);
                case 19: return Static("Kit_BrickWall", asset, s, Avoid.SteerAround, 3, 0.8f);
                case 20: return Static("Kit_ToyWall", asset, s, Avoid.SteerAround, 3, 0.8f);

                case 9: return Slider("Kit_SlideBlock", asset, s, Avoid.SteerAround, 0.3f, false, false);
                case 15: return Slider("Kit_SpikeRail_Low", asset, s, Avoid.Jump, 0.5f, true, false);
                case 16: return Slider("Kit_SpikeRail_Wide", asset, s, Avoid.Jump, 0.3f, true, true);
                case 10: return Doors("Kit_ClosingDoors", asset, s, 0.35f, 0f);
                case 17: return Doors("Kit_ClosingPillars", asset, s, 0.3f, 180f);

                case 21: return Pendulum("Kit_Pendulum", asset, s);
                case 22: return Crusher("Kit_Crusher", asset, s * 1.5f);
                case 24: return Fan("Kit_Fan", asset, s);
                case 25: return RollerGate("Kit_RollerGate", asset, s);
                case 26: return SlideGate("Kit_SlideGate", asset, s);
            }

            return null;
        }

        // Stands still. Box = the whole model (top capped for jump-over hurdles).
        private static GameObject Static(string name, GameObject asset, float s, Avoid avoid, int coins, float stumble, float yMax = float.MaxValue)
        {
            var (root, _, parts) = Spawn(name, asset, s);
            Box(root, BoundsOf(parts), 0.85f, 0f, yMax);
            Penalty(root, avoid, coins, stumble);
            return root;
        }

        // Arms turning around a post. Collision is a bar across the sweep circle at the post, low enough to jump:
        // the run line through a spinner always needs a jump, wherever the arms are.
        private static GameObject Spinner(string name, GameObject asset, float s, float degreesPerSecond)
        {
            var (root, model, parts) = Spawn(name, asset, s);
            var post = parts.OrderBy(r => r.bounds.size.x * r.bounds.size.z).First();
            var arms = parts.Where(r => r != post).ToList();
            if (arms.Count == 0)
                arms = parts;

            var pivot = post.bounds.center;
            model.position -= new Vector3(pivot.x, 0f, pivot.z);

            var armBounds = BoundsOf(arms);
            var reach = Mathf.Max(Mathf.Abs(armBounds.min.x), Mathf.Abs(armBounds.max.x),
                                  Mathf.Abs(armBounds.min.z), Mathf.Abs(armBounds.max.z));

            var spin = Child(root.transform, "Spin", Vector3.zero);
            model.SetParent(spin, true);
            Mover(spin.gameObject, spin: degreesPerSecond);

            BoxLocal(root, new Vector3(0f, JumpTop * 0.5f, 0f), new Vector3(reach * 2f * 0.9f, JumpTop, 0.6f));
            Penalty(root, Avoid.Jump, 3, 0.6f);
            return root;
        }

        // Rolls side to side across the track; jump it.
        private static GameObject Roller(string name, GameObject asset, float s)
        {
            var (root, _, parts) = Spawn(name, asset, s);
            Box(root, BoundsOf(parts), 0.85f, 0f, JumpTop);
            Penalty(root, Avoid.Jump, 3, 0.6f);
            Mover(root, slide: 2.5f, slideSpeed: 0.35f);
            return root;
        }

        // A rail with something sliding along it. The widest part is the rail.
        private static GameObject Slider(string name, GameObject asset, float s, Avoid avoid, float speed, bool jumpOver, bool railIsHazard)
        {
            var (root, _, parts) = Spawn(name, asset, s);
            var rail = parts.OrderByDescending(r => r.bounds.size.x).First();
            var moving = parts.Where(r => r != rail).ToList();
            var railBounds = rail.bounds;

            if (railIsHazard)
            {
                Box(root, railBounds, 0.95f, 0f, railBounds.max.y);
                Penalty(root, Avoid.Jump, 3, 0.6f);
            }

            if (moving.Count == 0)
                return root;

            var movingBounds = BoundsOf(moving);
            var mover = Child(root.transform, "Mover", new Vector3(movingBounds.center.x, 0f, movingBounds.center.z));
            foreach (var r in moving)
                r.transform.SetParent(mover, true);

            Box(mover.gameObject, movingBounds, 0.85f, 0f, jumpOver ? JumpTop : float.MaxValue);
            Penalty(mover.gameObject, avoid, 3, 0.6f);
            var travel = railBounds.extents.x - movingBounds.extents.x - Mathf.Abs(movingBounds.center.x - railBounds.center.x);
            Mover(mover.gameObject, slide: Mathf.Max(0f, travel), slideSpeed: speed);
            return root;
        }

        // Two blocks (or pillars) at the ends of a rail that slide in towards the middle and back out.
        private static GameObject Doors(string name, GameObject asset, float s, float speed, float spin)
        {
            var (root, _, parts) = Spawn(name, asset, s);
            var rail = parts.OrderByDescending(r => r.bounds.size.x).First();
            var doors = parts.Where(r => r != rail).OrderBy(r => r.bounds.center.x).ToList();

            for (var i = 0; i < doors.Count; i++)
            {
                var b = doors[i].bounds;
                var mover = Child(root.transform, $"Door {i + 1}", new Vector3(b.center.x, 0f, b.center.z));
                doors[i].transform.SetParent(mover, true);
                Box(mover.gameObject, b, 0.85f);
                Penalty(mover.gameObject, Avoid.SteerAround, 3, 0.7f);

                // Swings between where it was modelled and 0.25 short of the middle; starts halfway.
                var towardsMiddle = b.center.x > 0f ? -1f : 1f;
                var amplitude = Mathf.Max(0f, Mathf.Abs(b.center.x) - b.extents.x - 0.25f) * 0.5f;
                mover.position += new Vector3(towardsMiddle * amplitude, 0f, 0f);
                // Both move towards the middle together: the right one runs half a cycle behind.
                Mover(mover.gameObject, slide: amplitude, slideSpeed: speed, spin: spin, phase: towardsMiddle > 0f ? 0f : 0.5f);
            }

            return root;
        }

        // Hangs from its top and swings side to side across the track. The bottom 40% (the ball) hurts.
        private static GameObject Pendulum(string name, GameObject asset, float s)
        {
            var (root, model, parts) = Spawn(name, asset, s);
            var b = BoundsOf(parts);
            var pivot = Child(root.transform, "Pivot", new Vector3(0f, b.max.y, 0f));
            model.SetParent(pivot, true);

            var head = new Bounds(new Vector3(0f, b.min.y + b.size.y * 0.2f, 0f), new Vector3(b.size.x, b.size.y * 0.4f, b.size.z));
            Box(pivot.gameObject, head, 0.8f);
            Penalty(pivot.gameObject, Avoid.SteerAround, 3, 0.7f);
            Mover(pivot.gameObject, swing: 50f, swingSpeed: 0.35f);
            return root;
        }

        // Goes up and down; run under it while it's up.
        private static GameObject Crusher(string name, GameObject asset, float s)
        {
            var (root, _, parts) = Spawn(name, asset, s);
            Box(root, BoundsOf(parts), 0.85f);
            Penalty(root, Avoid.SteerAround, 3, 0.8f);
            Mover(root, bob: 2.4f, bobSpeed: 0.4f);
            return root;
        }

        // Big blades turning like a fan facing the runner. Covers half the track: go around it.
        private static GameObject Fan(string name, GameObject asset, float s)
        {
            var (root, model, parts) = Spawn(name, asset, s);
            var b = BoundsOf(parts);
            Box(root, b, 0.8f);
            Penalty(root, Avoid.SteerAround, 3, 0.7f);

            var hub = Child(root.transform, "Spin", b.center);
            model.SetParent(hub, true);
            Mover(hub.gameObject, spin: 90f, spinAxis: Vector3.forward);
            return root;
        }

        // A frame with two spinning spiked rollers: go through the middle or round the outside.
        private static GameObject RollerGate(string name, GameObject asset, float s)
        {
            var (root, _, parts) = Spawn(name, asset, s);
            var frame = parts.OrderByDescending(r => r.bounds.size.x).First();
            foreach (var roller in parts.Where(r => r != frame))
            {
                var b = roller.bounds;
                var spin = Child(root.transform, "Roller", new Vector3(b.center.x, 0f, b.center.z));
                roller.transform.SetParent(spin, true);
                Mover(spin.gameObject, spin: 200f);
                Box(root, b, 0.8f);
            }

            Posts(root, frame.bounds);
            Penalty(root, Avoid.SteerAround, 3, 0.7f);
            return root;
        }

        // A frame with a spiked roller across it at head height: slide under, or go round the outside.
        private static GameObject SlideGate(string name, GameObject asset, float s)
        {
            var (root, _, parts) = Spawn(name, asset, s);
            var frame = parts.OrderByDescending(r => r.bounds.size.y).First();
            var bars = parts.Where(r => r != frame).ToList();
            var fb = frame.bounds;
            var top = bars.Count > 0 ? BoundsOf(bars).max.y : fb.max.y;

            var zone = new Bounds();
            zone.SetMinMax(new Vector3(fb.min.x + 0.3f, SlideBottom, fb.min.z), new Vector3(fb.max.x - 0.3f, top, fb.max.z));
            Box(root, zone, 1f);
            Posts(root, fb);
            Penalty(root, Avoid.Slide, 3, 0.7f);

            var roller = bars.OrderByDescending(r => r.bounds.size.y * r.bounds.size.z).FirstOrDefault();
            if (roller != null)
            {
                var spin = Child(root.transform, "Roller", roller.bounds.center);
                roller.transform.SetParent(spin, true);
                Mover(spin.gameObject, spin: 200f, spinAxis: Vector3.right);
            }

            return root;
        }

        private static void Posts(GameObject root, Bounds frame)
        {
            const float post = 0.3f;
            Box(root, new Bounds(new Vector3(frame.min.x + post * 0.5f, frame.center.y, frame.center.z), new Vector3(post, frame.size.y, frame.size.z)), 1f);
            Box(root, new Bounds(new Vector3(frame.max.x - post * 0.5f, frame.center.y, frame.center.z), new Vector3(post, frame.size.y, frame.size.z)), 1f);
        }

        // ---------- Helpers ----------

        // New root with the model under "Model", scaled, footprint centred on x/z 0 and standing on y 0.
        private static (GameObject root, Transform model, List<Renderer> parts) Spawn(string name, GameObject asset, float scale)
        {
            var root = new GameObject(name);
            var holder = Child(root.transform, "Model", Vector3.zero);
            var clone = Object.Instantiate(asset, holder, false);
            clone.name = asset.name;
            holder.localScale = Vector3.one * scale;

            var parts = clone.GetComponentsInChildren<Renderer>().ToList();
            var b = BoundsOf(parts);
            holder.position -= new Vector3(b.center.x, b.min.y, b.center.z);
            return (root, holder, parts);
        }

        private static Transform Child(Transform parent, string name, Vector3 worldPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = worldPosition;
            return t;
        }

        private static Bounds BoundsOf(List<Renderer> parts)
        {
            var b = parts[0].bounds;
            for (var i = 1; i < parts.Count; i++)
                b.Encapsulate(parts[i].bounds);
            return b;
        }

        // Trigger box from world bounds (the kit is built at the origin, unrotated), shrunk on x/z to be forgiving.
        private static void Box(GameObject go, Bounds world, float shrink, float yMin = float.MinValue, float yMax = float.MaxValue)
        {
            var min = world.min;
            var max = world.max;
            if (yMin > float.MinValue) min.y = yMin;
            max.y = Mathf.Min(max.y, yMax);
            if (max.y <= min.y) max.y = min.y + 0.1f;

            var size = max - min;
            size.x *= shrink;
            size.z *= shrink;
            BoxLocal(go, go.transform.InverseTransformPoint((min + max) * 0.5f), size);
        }

        private static void BoxLocal(GameObject go, Vector3 centre, Vector3 size)
        {
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = centre;
            box.size = size;
        }

        private static void Penalty(GameObject go, Avoid avoid, int coins, float stumble)
        {
            var penalty = go.AddComponent<PenaltyItem>();
            var so = new SerializedObject(penalty);
            so.FindProperty("aiAvoid").enumValueIndex = (int)avoid;
            so.FindProperty("coinCost").intValue = coins;
            so.FindProperty("stumbleDuration").floatValue = stumble;
            so.FindProperty("removesItem").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (!go.TryGetComponent<Rigidbody>(out var body))
                body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        // Motion = DOTweenAnimation components (set up in DOTweenMotion), applied in Save once the hurdle is fully built.
        private static void Mover(GameObject go, float slide = 0f, float slideSpeed = 0.5f, float spin = 0f, Vector3? spinAxis = null,
            float bob = 0f, float bobSpeed = 1f, float swing = 0f, float swingSpeed = 0.5f, float phase = 0f)
        {
            var m = DOTweenMotion.Motion.Default;
            m.slide = slide; m.slideSpeed = slideSpeed;
            m.spin = spin; m.spinAxis = spinAxis ?? Vector3.up;
            m.bob = bob; m.bobSpeed = bobSpeed;
            m.swing = swing; m.swingSpeed = swingSpeed;
            m.phase = phase;
            DOTweenMotion.Enqueue(go, m);
        }

        private static string Save(GameObject root)
        {
            DOTweenMotion.ApplyQueued(root);
            var name = root.name;
            PrefabUtility.SaveAsPrefabAsset(root, $"{KitFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
            return name;
        }

        private static Dictionary<string, GameObject> FindModels()
        {
            var result = new Dictionary<string, GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/_ART" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (file.StartsWith("Hurdle") && !result.ContainsKey(file))
                    result[file] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return result;
        }

        private static float MeasureWidth(GameObject asset)
        {
            var probe = Object.Instantiate(asset);
            var renderers = probe.GetComponentsInChildren<Renderer>().ToList();
            var width = renderers.Count > 0 ? BoundsOf(renderers).size.x : 0f;
            Object.DestroyImmediate(probe);
            return width;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (var t in parent.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                    return t;
            }

            return null;
        }
    }
}
#endif
