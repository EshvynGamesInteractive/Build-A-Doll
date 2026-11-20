#if UNITY_EDITOR
using System.Collections.Generic;
using DG.Tweening;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Editor helper: sets up hurdle motion as DOTween Pro <see cref="DOTweenAnimation"/> components (visible and editable in the
    /// Inspector) instead of the old code-driven HurdleMover. Nothing animates from our code at runtime: DOTweenAnimation
    /// creates its own looping tween in Awake.
    ///   slide = LocalMove, relative, Yoyo, InOutSine, between -d and +d (start position baked to one end)
    ///   bob   = LocalMove up by h, relative, Yoyo, InOutSine
    ///   spin  = LocalRotate 360° around the local axis (LocalAxisAdd), Linear, endless Restart
    ///   swing = LocalRotate between -a and +a around the local axis, Yoyo, InOutSine
    /// One position tween and one rotation tween per object; a second one of the same kind goes on a "Motion" child.
    /// Phase is rounded to "start at one end" (0) or "the other end" (0.5); spin phase is baked into the start angle.
    /// </summary>
    internal static class DOTweenMotion
    {
        public struct Motion
        {
            public float slide, slideSpeed, bob, bobSpeed, spin, swing, swingSpeed, phase;
            public Vector3 slideAxis, spinAxis, swingAxis;

            public bool Slides => slide > 0f && slideSpeed > 0f;
            public bool Bobs => bob > 0f && bobSpeed > 0f;
            public bool Spins => !Mathf.Approximately(spin, 0f);
            public bool Swings => swing > 0f && swingSpeed > 0f;
            public bool Any => Slides || Bobs || Spins || Swings;

            public static Motion Default => new Motion
            {
                slideSpeed = 0.5f, bobSpeed = 1f, swingSpeed = 0.5f,
                slideAxis = Vector3.right, spinAxis = Vector3.up, swingAxis = Vector3.forward
            };
        }

        public const string TweenId = "hurdle";

        // Builders queue motions while they build, and apply them right before saving, so later position
        // changes in the builder don't undo the baked start position.
        private static readonly List<(GameObject go, Motion motion)> Queue = new();

        public static void Enqueue(GameObject go, Motion motion)
        {
            if (go != null && motion.Any)
                Queue.Add((go, motion));
        }

        /// <summary>Applies every queued motion whose object is under 'root'.</summary>
        public static void ApplyQueued(GameObject root)
        {
            for (var i = Queue.Count - 1; i >= 0; i--)
            {
                var (go, motion) = Queue[i];
                if (go == null) { Queue.RemoveAt(i); continue; }
                if (go.transform != root.transform && !go.transform.IsChildOf(root.transform)) continue;
                Add(go, motion);
                Queue.RemoveAt(i);
            }
        }

        public static void Add(GameObject go, Motion m)
        {
            var t = go.transform;
            var startHigh = Mathf.Repeat(m.phase, 1f) is >= 0.25f and < 0.75f;

            // Position tweens.
            var positionTarget = go;
            if (m.Slides)
            {
                var offset = t.localRotation * (Safe(m.slideAxis, Vector3.right) * m.slide);
                t.localPosition += startHigh ? offset : -offset;
                AddTween(positionTarget, DOTweenAnimation.AnimationType.LocalMove, 0.5f / m.slideSpeed,
                    startHigh ? -2f * offset : 2f * offset, Ease.InOutSine, LoopType.Yoyo);
                positionTarget = null;
            }

            if (m.Bobs)
            {
                var target = positionTarget != null ? positionTarget : MotionChild(go, "Motion_Bob");
                if (startHigh) target.transform.localPosition += Vector3.up * m.bob;
                AddTween(target, DOTweenAnimation.AnimationType.LocalMove, 0.5f / m.bobSpeed,
                    Vector3.up * (startHigh ? -m.bob : m.bob), Ease.InOutSine, LoopType.Yoyo);
            }

            // Rotation tweens.
            var rotationTarget = go;
            if (m.Spins)
            {
                var axis = Safe(m.spinAxis, Vector3.up);
                if (!Mathf.Approximately(Mathf.Repeat(m.phase, 1f), 0f))
                    t.localRotation *= Quaternion.AngleAxis(Mathf.Repeat(m.phase, 1f) * 360f, axis);
                AddTween(rotationTarget, DOTweenAnimation.AnimationType.LocalRotate, 360f / Mathf.Abs(m.spin),
                    axis * (360f * Mathf.Sign(m.spin)), Ease.Linear, LoopType.Restart, RotateMode.LocalAxisAdd, relative: false);
                rotationTarget = null;
            }

            if (m.Swings)
            {
                var target = rotationTarget != null ? rotationTarget : MotionChild(go, "Motion_Swing");
                var axis = Safe(m.swingAxis, Vector3.forward);
                target.transform.localRotation *= Quaternion.AngleAxis(startHigh ? m.swing : -m.swing, axis);
                AddTween(target, DOTweenAnimation.AnimationType.LocalRotate, 0.5f / m.swingSpeed,
                    axis * (startHigh ? -2f * m.swing : 2f * m.swing), Ease.InOutSine, LoopType.Yoyo, RotateMode.LocalAxisAdd, relative: false);
            }
        }

        public static DOTweenAnimation AddTween(GameObject go, DOTweenAnimation.AnimationType type, float duration, Vector3 endValue,
            Ease ease, LoopType loopType, RotateMode rotateMode = RotateMode.Fast, bool relative = true)
        {
            var tween = go.AddComponent<DOTweenAnimation>();
            tween.targetIsSelf = true;
            tween.target = go.transform;
            tween.targetType = DOTweenAnimation.TargetType.Transform;
            tween.animationType = type;
            tween.duration = Mathf.Max(0.01f, duration);
            tween.delay = 0f;
            tween.easeType = ease;
            tween.loops = -1;
            tween.loopType = loopType;
            tween.isRelative = relative;
            tween.isFrom = false;
            tween.endValueV3 = endValue;
            tween.optionalRotationMode = rotateMode;
            tween.autoPlay = true;
            tween.autoKill = false;
            tween.autoGenerate = true;
            tween.isActive = true;
            tween.isValid = true;
            tween.id = TweenId;
            EditorUtility.SetDirty(tween);
            return tween;
        }

        // A child that takes over this object's children, for a second tween of the same kind.
        private static GameObject MotionChild(GameObject go, string name)
        {
            var child = new GameObject(name).transform;
            var kids = new List<Transform>();
            foreach (Transform c in go.transform) kids.Add(c);
            child.SetParent(go.transform, false);
            foreach (var k in kids) k.SetParent(child, true);
            if (kids.Count == 0)
                Debug.LogWarning($"[DOTweenMotion] '{go.name}' moves two ways at once but has no children to put on '{name}'; only its first motion of that kind is kept.", go);
            return child.gameObject;
        }

        private static Vector3 Safe(Vector3 v, Vector3 fallback) => v.sqrMagnitude > 1e-6f ? v.normalized : fallback;
    }
}
#endif
