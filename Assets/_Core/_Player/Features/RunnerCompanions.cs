using System.Collections.Generic;
using Core._GameEvent;
using UnityEngine;

namespace Game.Player.Features
{
    /// <summary>
    /// Companions that float along with the doll: a pet hatched from an egg gate, a fairy...
    /// They follow smoothly instead of being parented, so they trail a little behind her moves.
    /// All of them go away on ResetRun.
    /// </summary>
    [DisallowMultipleComponent]
    public class RunnerCompanions : MonoBehaviour
    {
        [Tooltip("What companions follow. Empty = this object.")]
        [SerializeField] private Transform followTarget;

        [Tooltip("Where each companion floats, in the follow target's space. Also the most companions at once (the oldest leaves).")]
        [SerializeField] private Vector3[] slots = { new(0.9f, 1.6f, -0.4f), new(-0.9f, 1.6f, -0.4f) };

        [SerializeField, Min(0.1f)] private float followSharpness = 8f;

        private readonly List<Transform> _companions = new();

        public int Count => _companions.Count;

        private void Awake()
        {
            if (followTarget == null)
                followTarget = transform;
        }

        private void OnEnable() => GameEvents.GameplayEvents.ResetRun.Register(Clear);

        private void OnDisable() => GameEvents.GameplayEvents.ResetRun.Unregister(Clear);

        private void OnDestroy() => Clear();

        public GameObject Add(GameObject prefab)
        {
            if (prefab == null || slots == null || slots.Length == 0)
                return null;

            if (_companions.Count >= slots.Length)
                RemoveAt(0);

            var instance = Instantiate(prefab, SlotPosition(_companions.Count), followTarget.rotation);
            instance.name = prefab.name;
            _companions.Add(instance.transform);
            return instance;
        }

        public void GetRenderers(List<Renderer> results)
        {
            foreach (var companion in _companions)
            {
                if (companion != null)
                    results.AddRange(companion.GetComponentsInChildren<Renderer>(true));
            }
        }

        public void Clear()
        {
            for (var i = _companions.Count - 1; i >= 0; i--)
                RemoveAt(i);
        }

        private void LateUpdate()
        {
            if (_companions.Count == 0)
                return;

            var t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            for (var i = 0; i < _companions.Count; i++)
            {
                var companion = _companions[i];
                if (companion == null)
                    continue;

                companion.position = Vector3.Lerp(companion.position, SlotPosition(i), t);
                companion.rotation = Quaternion.Slerp(companion.rotation, followTarget.rotation, t);
            }
        }

        private Vector3 SlotPosition(int index) => followTarget.TransformPoint(slots[Mathf.Clamp(index, 0, slots.Length - 1)]);

        private void RemoveAt(int index)
        {
            var companion = _companions[index];
            _companions.RemoveAt(index);
            if (companion != null)
                Destroy(companion.gameObject);
        }
    }
}
