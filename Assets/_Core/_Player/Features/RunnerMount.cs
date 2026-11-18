using System.Collections.Generic;
using _Core._Player;
using Core._GameEvent;
using UnityEngine;

namespace Game.Player.Features
{
    /// <summary>
    /// Rides for a few seconds: a pet (dolphin, bird, reptile), a seahorse, or wings for a ramp flight.
    /// Spawns the ride at the mount anchor, changes her speed and height, and ends by itself.
    /// While an invulnerable ride is on, harmful track elements are ignored. Ends on ResetRun.
    /// </summary>
    [DisallowMultipleComponent]
    public class RunnerMount : MonoBehaviour, IHazardShield
    {
        [Tooltip("Where rides and their effects spawn (e.g. under the hips). Empty = this object.")]
        [SerializeField] private Transform mountAnchor;

        private readonly List<GameObject> _spawned = new();
        private RunnerController _runner;
        private MountSettings _current;
        private float _timeLeft;

        public bool IsMounted => _current != null;

        private void Awake()
        {
            if (mountAnchor == null)
                mountAnchor = transform;
            _runner = GetComponentInParent<RunnerController>();
        }

        private void OnEnable() => GameEvents.GameplayEvents.ResetRun.Register(Dismount);

        private void OnDisable() => GameEvents.GameplayEvents.ResetRun.Unregister(Dismount);

        public void Mount(MountSettings settings)
        {
            if (settings == null)
                return;

            Dismount();
            _current = settings;
            _timeLeft = settings.duration;
            Spawn(settings.prefab);
            Spawn(settings.effectPrefab);

            if (_runner == null)
                return;

            _runner.SetSpeedFactor(this, settings.speedFactor);
            _runner.SetHeightOffset(this, settings.heightOffset);
            if (settings.lockSteering)
                _runner.LockSteering(this);
            if (_runner.Animator != null)
                _runner.Animator.SetBool(settings.animatorBool, true);
        }

        public void Dismount()
        {
            if (_current == null)
                return;

            foreach (var spawned in _spawned)
            {
                if (spawned != null)
                    Destroy(spawned);
            }

            _spawned.Clear();

            if (_runner != null)
            {
                _runner.ClearSpeedFactor(this);
                _runner.ClearHeightOffset(this);
                _runner.UnlockSteering(this);
                if (_runner.Animator != null)
                    _runner.Animator.SetBool(_current.animatorBool, false);
            }

            _current = null;
        }

        public bool TryBlock() => _current != null && _current.invulnerable;

        public void GetRenderers(List<Renderer> results)
        {
            foreach (var spawned in _spawned)
            {
                if (spawned != null)
                    results.AddRange(spawned.GetComponentsInChildren<Renderer>(true));
            }
        }

        private void Update()
        {
            if (_current == null)
                return;

            _timeLeft -= Time.deltaTime;
            if (_timeLeft <= 0f)
                Dismount();
        }

        private void Spawn(GameObject prefab)
        {
            if (prefab == null)
                return;

            var instance = Instantiate(prefab, mountAnchor, false);
            instance.name = prefab.name;
            _spawned.Add(instance);
        }
    }
}
