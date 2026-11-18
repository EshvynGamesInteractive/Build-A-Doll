using System;
using System.Collections.Generic;
using Core._GameEvent;
using UnityEngine;

namespace Game.Player.Features
{
    /// <summary>
    /// Scales the doll's body parts (enhancer / regulator gates). Each part scales its bones; bones
    /// listed under Keep Size are scaled back so, for example, a bigger neck doesn't also blow up the head.
    /// Parts left empty are filled in from a humanoid Animator. Everything goes back to normal on ResetRun.
    /// Scales ease towards the target and are applied in LateUpdate, after animation.
    /// </summary>
    [DisallowMultipleComponent]
    public class DollBody : MonoBehaviour
    {
        [Serializable]
        private class PartBones
        {
            public BodyPart part;

            [Tooltip("Bones this part scales.")]
            public Transform[] bones;

            [Tooltip("Child bones that keep their size (scaled back), e.g. the head for the neck.")]
            public Transform[] keepSize;

            [Tooltip("Local axes that scale, 0..1 each. (1,1,1) = uniform; (0,1,0) = longer only.")]
            public Vector3 axes = Vector3.one;
        }

        [Tooltip("Humanoid Animator used to fill in parts that have no bones set. Empty = found in children.")]
        [SerializeField] private Animator animator;

        [Tooltip("Optional overrides. Parts not listed (or without bones) come from the humanoid rig.")]
        [SerializeField] private PartBones[] parts;

        [Tooltip("Scale units per second while a part grows or shrinks.")]
        [SerializeField, Min(0.01f)] private float resizeSpeed = 4f;

        [Tooltip("Smallest and largest scale a part can reach.")]
        [SerializeField] private Vector2 scaleLimits = new(0.3f, 4f);

        [Tooltip("Optional: the model root (a child of the RunnerController's Visual), raised as the legs grow so her feet stay on the track.")]
        [SerializeField] private Transform heightRoot;

        private readonly Dictionary<BodyPart, float> _targets = new();
        private readonly Dictionary<BodyPart, float> _current = new();
        private readonly Dictionary<Transform, Vector3> _baseScales = new();
        private readonly Dictionary<Transform, Vector3> _own = new();
        private readonly Dictionary<Transform, Vector3> _counter = new();
        private readonly List<PartBones> _resolved = new();
        private readonly List<BodyPart> _partBuffer = new();

        private Vector3 _heightRootBase;
        private float _legLength;
        private bool _resizing;
        private bool _changed;

        public float GetScale(BodyPart part) => _targets.TryGetValue(part, out var scale) ? scale : 1f;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);

            Resolve();

            if (heightRoot != null)
                _heightRootBase = heightRoot.localPosition;
            _legLength = MeasureLegLength();
        }

        private void OnEnable() => GameEvents.GameplayEvents.ResetRun.Register(OnResetRun);

        private void OnDisable() => GameEvents.GameplayEvents.ResetRun.Unregister(OnResetRun);

        /// <summary>Sets a part's scale (1 = normal). Eases there unless 'instant'.</summary>
        public void SetScale(BodyPart part, float scale, bool instant = false)
        {
            _targets[part] = Mathf.Clamp(scale, scaleLimits.x, scaleLimits.y);
            if (instant)
            {
                _current[part] = _targets[part];
                Apply();
                return;
            }

            _resizing = true;
        }

        /// <summary>Every part back to normal (rewarded fix, reset).</summary>
        public void ResetAll(bool instant = false)
        {
            foreach (BodyPart part in Enum.GetValues(typeof(BodyPart)))
            {
                _targets[part] = 1f;
                if (instant)
                    _current[part] = 1f;
            }

            if (instant)
                Apply();
            else
                _resizing = true;
        }

        private void OnResetRun() => ResetAll(true);

        private void LateUpdate()
        {
            // Keep applying while any part is changed, in case the animation writes bone scales.
            if (!_resizing && !_changed)
                return;

            if (_resizing)
            {
                var done = true;
                _partBuffer.Clear();
                _partBuffer.AddRange(_targets.Keys);
                foreach (var part in _partBuffer)
                {
                    var target = _targets[part];
                    var next = Mathf.MoveTowards(GetCurrent(part), target, resizeSpeed * Time.deltaTime);
                    _current[part] = next;
                    if (!Mathf.Approximately(next, target))
                        done = false;
                }

                _resizing = !done;
            }

            Apply();
        }

        private float GetCurrent(BodyPart part) => _current.TryGetValue(part, out var scale) ? scale : 1f;

        private void Apply()
        {
            _own.Clear();
            _counter.Clear();
            _changed = false;

            foreach (var entry in _resolved)
            {
                var scale = GetCurrent(entry.part);
                if (!Mathf.Approximately(scale, 1f))
                    _changed = true;

                var factor = Vector3.one + (scale - 1f) * entry.axes;
                Multiply(_own, entry.bones, factor);
                Multiply(_counter, entry.keepSize, factor);
            }

            foreach (var pair in _baseScales)
            {
                if (pair.Key == null)
                    continue;

                var own = _own.TryGetValue(pair.Key, out var o) ? o : Vector3.one;
                var counter = _counter.TryGetValue(pair.Key, out var c) ? c : Vector3.one;
                pair.Key.localScale = Vector3.Scale(pair.Value, new Vector3(own.x / counter.x, own.y / counter.y, own.z / counter.z));
            }

            ApplyHeight();
        }

        private static void Multiply(Dictionary<Transform, Vector3> factors, Transform[] bones, Vector3 factor)
        {
            if (bones == null)
                return;

            foreach (var bone in bones)
            {
                if (bone != null)
                    factors[bone] = factors.TryGetValue(bone, out var value) ? Vector3.Scale(value, factor) : factor;
            }
        }

        private void ApplyHeight()
        {
            if (heightRoot == null || _legLength <= 0f)
                return;

            var parentScale = heightRoot.parent != null ? Mathf.Max(0.0001f, heightRoot.parent.lossyScale.y) : 1f;
            var lift = _legLength * (GetCurrent(BodyPart.Legs) - 1f) / parentScale;
            heightRoot.localPosition = _heightRootBase + Vector3.up * lift;
        }

        private float MeasureLegLength()
        {
            if (animator == null || !animator.isHuman)
                return 0f;

            var upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            return upper != null && foot != null ? Vector3.Distance(upper.position, foot.position) : 0f;
        }

        // Inspector entries first, then the humanoid rig for every part still missing.
        private void Resolve()
        {
            _resolved.Clear();
            _baseScales.Clear();
            var configured = new HashSet<BodyPart>();

            if (parts != null)
            {
                foreach (var entry in parts)
                {
                    if (entry == null || entry.bones == null || entry.bones.Length == 0)
                        continue;

                    _resolved.Add(entry);
                    configured.Add(entry.part);
                }
            }

            if (animator != null && animator.isHuman)
            {
                foreach (BodyPart part in Enum.GetValues(typeof(BodyPart)))
                {
                    if (configured.Contains(part))
                        continue;

                    var entry = FromHumanoid(part);
                    if (entry != null)
                        _resolved.Add(entry);
                }
            }

            foreach (var entry in _resolved)
            {
                entry.axes = new Vector3(Mathf.Clamp01(entry.axes.x), Mathf.Clamp01(entry.axes.y), Mathf.Clamp01(entry.axes.z));
                CacheBaseScales(entry.bones);
                CacheBaseScales(entry.keepSize);
            }

            foreach (BodyPart part in Enum.GetValues(typeof(BodyPart)))
            {
                _targets[part] = 1f;
                _current[part] = 1f;
            }
        }

        private void CacheBaseScales(Transform[] bones)
        {
            if (bones == null)
                return;

            foreach (var bone in bones)
            {
                if (bone != null)
                    _baseScales.TryAdd(bone, bone.localScale);
            }
        }

        private PartBones FromHumanoid(BodyPart part)
        {
            switch (part)
            {
                case BodyPart.Head:
                    return Entry(part, Bones(HumanBodyBones.Head), null);
                case BodyPart.Neck:
                    return Entry(part, Bones(HumanBodyBones.Neck), Bones(HumanBodyBones.Head));
                case BodyPart.Torso:
                    var chest = Bone(HumanBodyBones.Chest);
                    return Entry(part, new[] { chest != null ? chest : Bone(HumanBodyBones.Spine) },
                        new[] { Bone(HumanBodyBones.Neck), ShoulderOrArm(true), ShoulderOrArm(false) });
                case BodyPart.Shoulders:
                    return Entry(part, Bones(HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder),
                        Bones(HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm));
                case BodyPart.Arms:
                    return Entry(part, Bones(HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm), null);
                case BodyPart.Legs:
                    return Entry(part, Bones(HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg), null);
                default:
                    return null;
            }
        }

        private Transform ShoulderOrArm(bool left)
        {
            var shoulder = Bone(left ? HumanBodyBones.LeftShoulder : HumanBodyBones.RightShoulder);
            return shoulder != null ? shoulder : Bone(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
        }

        private Transform Bone(HumanBodyBones bone) => animator.GetBoneTransform(bone);

        private Transform[] Bones(params HumanBodyBones[] bones) => Array.ConvertAll(bones, Bone);

        private static PartBones Entry(BodyPart part, Transform[] bones, Transform[] keepSize)
        {
            bones = Array.FindAll(bones, b => b != null);
            if (bones.Length == 0)
                return null;

            return new PartBones
            {
                part = part,
                bones = bones,
                keepSize = keepSize != null ? Array.FindAll(keepSize, b => b != null) : Array.Empty<Transform>(),
                axes = Vector3.one
            };
        }
    }
}
