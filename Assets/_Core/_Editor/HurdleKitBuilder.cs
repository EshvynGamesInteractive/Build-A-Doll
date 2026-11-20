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

        // Rebuilds one kit prefab only (the others, and any hand edits on them, stay as they are).
        [MenuItem("Build A Doll/Hurdles/Rebuild Kit_SlideGate Only")]
        public static void BuildSlideGateOnly() => BuildOne(26);

        [MenuItem("Build A Doll/Hurdles/Rebuild Kit_LowBlock Only")]
        public static void BuildLowBlockOnly() => BuildOne(8);

        private static void BuildOne(int id)
        {
            var models = FindModels();
            if (!models.TryGetValue("Hurdle16", out var reference) || !models.TryGetValue($"Hurdle{id}", out var model))
            {
                EditorUtility.DisplayDialog(Title, $"Hurdle16.fbx or Hurdle{id}.fbx not found under Assets/_ART.", "OK");
                return;
            }

            var scale = FullWidth / Mathf.Max(0.0001f, MeasureWidth(reference));
            EditorPaths.EnsureFolder(KitFolder);
            var root = BuildHurdle(id, model, scale);
            if (root == null) return;
            var name = Save(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[HurdleKit] Rebuilt {name}.");
            EditorUtility.DisplayDialog(Title, $"Rebuilt {name}.", "OK");
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
                case 23: return Hammer("Kit_Puncher", asset, s);

                case 6: return Roller("Kit_SpikeRoller", asset, s);
                case 7: return Static("Kit_SlimeTray", asset, s, Avoid.SteerAround, 2, 0.6f, 0.5f);
                case 8: return LowBlock("Kit_LowBlock", asset, s);
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
                // 22 (Kit_Crusher) removed 20 Nov 2026.
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

        // Low wedge = jump ramp. Turned 180° so the slope faces the runner. Running onto its front edge
        // (AutoJumpPad) launches her up and forward over it; it doesn't hurt, so there's no PenaltyItem.
        private static GameObject LowBlock(string name, GameObject asset, float s)
        {
            var (root, model, parts) = Spawn(name, asset, s);
            var b0 = BoundsOf(parts);
            model.RotateAround(new Vector3(b0.center.x, 0f, b0.center.z), Vector3.up, 180f);
            var b = BoundsOf(parts);

            var pad = Child(root.transform, "AutoJump", new Vector3(b.center.x, 0f, b.min.z));
            BoxLocal(pad.gameObject, new Vector3(0f, 1f, 0.3f), new Vector3(Mathf.Max(b.size.x, 1.6f), 2f, 0.6f));
            pad.gameObject.AddComponent<AutoJumpPad>();
            var body = pad.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            return root;
        }

        // Hammer: the drum on its arm is lifted high next to the post, then slams down onto the track and lifts again.
        // Pivot = top of the post, swing across the track (the arm's side). Dodge to the other side, or pass while it's up.
        private static GameObject Hammer(string name, GameObject asset, float s)
        {
            var (root, model, parts) = Spawn(name, asset, s);
            var post = parts.OrderBy(r => r.bounds.size.x * r.bounds.size.z).First();
            var head = parts.Where(r => r != post).ToList();
            if (head.Count == 0)
                head = parts;

            var pb = post.bounds;
            var hb = BoundsOf(head);
            var pivotPoint = new Vector3(pb.center.x, pb.max.y, pb.center.z);

            // Arm direction (flat) and the axis that lifts it: rotating +angle about Cross(dir, up) raises the arm.
            var flat = new Vector3(hb.center.x - pivotPoint.x, 0f, hb.center.z - pivotPoint.z);
            var dir = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.left;
            var axis = Vector3.Cross(dir, Vector3.up).normalized;
            var armLength = Mathf.Max(0.1f, flat.magnitude);

            var pivot = Child(root.transform, "Hammer", pivotPoint);
            foreach (var r in head)
                r.transform.SetParent(pivot, true);

            // Hit box on the head (it moves with the pivot).
            Box(pivot.gameObject, hb, 0.85f);
            Penalty(pivot.gameObject, Avoid.SteerAround, 3, 0.7f);

            // Slam: from raised (75° up) to just touching the track.
            var drop = Mathf.Max(0f, hb.min.y - 0.05f);
            var slam = Mathf.Clamp(Mathf.Asin(Mathf.Clamp01(drop / armLength)) * Mathf.Rad2Deg, 0f, 35f);
            const float raised = 75f;
            pivot.localRotation = Quaternion.AngleAxis(raised, axis);
            var local = pivot.InverseTransformDirection(axis);
            // InCubic: slow start, fast hit (Yoyo plays it back for the lift).
            DOTweenMotion.AddTween(pivot.gameObject, DG.Tweening.DOTweenAnimation.AnimationType.LocalRotate, 0.75f,
                local * -(raised + slam), DG.Tweening.Ease.InCubic, DG.Tweening.LoopType.Yoyo, DG.Tweening.RotateMode.LocalAxisAdd, relative: false);
            return root;
        }

        // Big blades turning like a fan facing the runner. Covers half the track: go around it.
        // The blades and hub are one mesh, so the hub is found as the centre of all vertices (the blades are
        // symmetric round it), and the fan is lifted so the blade tips clear the track.
        private static GameObject Fan(string name, GameObject asset, float s)
        {
            var (root, model, parts) = Spawn(name, asset, s);
            var (hubPoint, radius) = SpinCentre(parts, Vector3.forward);
            var lift = Mathf.Max(0f, radius + 0.1f - hubPoint.y);
            model.position += Vector3.up * lift;
            hubPoint += Vector3.up * lift;

            var hub = Child(root.transform, "Spin", hubPoint);
            model.SetParent(hub, true);
            Mover(hub.gameObject, spin: 90f, spinAxis: Vector3.forward);

            var depth = Mathf.Max(BoundsOf(parts).size.z, 0.6f);
            BoxLocal(root, root.transform.InverseTransformPoint(hubPoint), new Vector3(radius * 2f * 0.8f, radius * 2f * 0.8f, depth));
            Penalty(root, Avoid.SteerAround, 3, 0.7f);
            return root;
        }

        // Centre of all mesh vertices (world space) and the largest distance from it, measured across 'axis'.
        private static (Vector3 centre, float radius) SpinCentre(List<Renderer> parts, Vector3 axis)
        {
            var points = new List<Vector3>();
            foreach (var r in parts)
            {
                Mesh mesh = null;
                if (r is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
                else if (r.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
                if (mesh == null) continue;
                var m = r.transform.localToWorldMatrix;
                foreach (var v in mesh.vertices) points.Add(m.MultiplyPoint3x4(v));
            }

            if (points.Count == 0)
            {
                var b = BoundsOf(parts);
                return (b.center, Mathf.Max(b.extents.x, b.extents.y));
            }

            var centre = Vector3.zero;
            foreach (var p in points) centre += p;
            centre /= points.Count;

            var radius = 0f;
            foreach (var p in points)
                radius = Mathf.Max(radius, Vector3.ProjectOnPlane(p - centre, axis).magnitude);
            return (centre, radius);
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

        // A frame with a spiked roller across it that moves up and down (DOTween): when it's up, slide under it;
        // when it's down near the ground, jump over it. Or go round the outside. The posts stay put and hurt too.
        // Hierarchy: Kit_SlideGate (posts' boxes + PenaltyItem) > Lift (moves up/down; roller hit box + PenaltyItem) > Roller (spins).
        private const float SlideGateLowBottom = 0.15f;  // roller's lowest point when down (top then ≈ 0.9: an easy jump)
        private const float SlideGateSpeed = 0.35f;      // up-down cycles per second (~1.4 s each way)

        private static GameObject SlideGate(string name, GameObject asset, float s)
        {
            var (root, _, parts) = Spawn(name, asset, s);
            var frame = parts.OrderByDescending(r => r.bounds.size.y).First();
            var bars = parts.Where(r => r != frame).ToList();
            var fb = frame.bounds;
            var roller = bars.OrderByDescending(r => r.bounds.size.y * r.bounds.size.z).FirstOrDefault();

            Posts(root, fb);
            Penalty(root, Avoid.SteerAround, 3, 0.7f);
            if (roller == null)
                return root;

            var rb = roller.bounds;
            var barBounds = BoundsOf(bars);

            // Lift at the roller, carrying the roller and its end caps.
            var lift = Child(root.transform, "Lift", rb.center);
            foreach (var bar in bars)
                bar.transform.SetParent(lift, true);

            // Hit box (moves with the lift): from slide height up to the roller's top while it's up,
            // so sliding gets under it; when it's down the box top is the roller's top, so jumping clears it.
            // Roller + 0.5 under it: up (bottom ≈ 3.2) it hits her standing (hitbox 3.2 tall) but not sliding (0.9);
            // halfway it still needs a slide; down (top ≈ 0.9) she jumps over it.
            var zone = new Bounds();
            zone.SetMinMax(new Vector3(fb.min.x + 0.3f, rb.min.y - 0.5f, barBounds.min.z),
                           new Vector3(fb.max.x - 0.3f, barBounds.max.y, barBounds.max.z));
            Box(lift.gameObject, zone, 1f);
            Penalty(lift.gameObject, Avoid.Jump, 3, 0.7f);

            var spin = Child(lift, "Roller", rb.center);
            roller.transform.SetParent(spin, true);
            Mover(spin.gameObject, spin: 200f, spinAxis: Vector3.right);

            // Up-down: centred halfway between up (as modelled) and down, starting up.
            var travel = Mathf.Max(0f, rb.min.y - SlideGateLowBottom);
            lift.position -= Vector3.up * (travel * 0.5f);
            var m = DOTweenMotion.Motion.Default;
            m.slide = travel * 0.5f;
            m.slideSpeed = SlideGateSpeed;
            m.slideAxis = Vector3.up;
            m.phase = 0.5f; // start at the top, going down
            DOTweenMotion.Enqueue(lift.gameObject, m);
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
