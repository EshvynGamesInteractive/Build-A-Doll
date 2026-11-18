using System;
using _Core._Player;
using Core._GameEvent;
using UnityEngine;

namespace Game.Player.Features
{
    /// <summary>
    /// What she runs on: land, water, underwater. Terrain patches switch it mid-run. Each terrain can show
    /// its own objects (swim ring, splash trail), set an animator bool (Swim) and change speed and height.
    /// Back to Land on ResetRun.
    /// </summary>
    [DisallowMultipleComponent]
    public class RunnerTerrain : MonoBehaviour
    {
        [Serializable]
        private class TerrainSettings
        {
            public TerrainType type;

            [Tooltip("Shown on this terrain only (swim ring, splash trail...). Children of the doll.")]
            public GameObject[] activeObjects;

            [Tooltip("Animator bool set on this terrain, e.g. Swim. Empty = none.")]
            public string animatorBool;

            [Range(0.1f, 3f)] public float speedFactor = 1f;

            [Tooltip("Height above the track on this terrain (e.g. floating). 0 = none.")]
            public float heightOffset;
        }

        [SerializeField] private TerrainSettings[] terrains;

        private RunnerController _runner;

        public TerrainType Current { get; private set; } = TerrainType.Land;

        private void Awake() => _runner = GetComponentInParent<RunnerController>();

        private void Start() => ResetTerrain();

        private void OnEnable() => GameEvents.GameplayEvents.ResetRun.Register(ResetTerrain);

        private void OnDisable() => GameEvents.GameplayEvents.ResetRun.Unregister(ResetTerrain);

        public void Set(TerrainType type)
        {
            if (type == Current)
                return;

            Apply(Find(Current), false);
            Current = type;
            Apply(Find(type), true);
            RaiseChanged();
        }

        private void ResetTerrain()
        {
            if (terrains != null)
            {
                foreach (var settings in terrains)
                    Apply(settings, false);
            }

            Current = TerrainType.Land;
            Apply(Find(Current), true);
            RaiseChanged();
        }

        private void Apply(TerrainSettings settings, bool on)
        {
            if (settings == null)
                return;

            if (settings.activeObjects != null)
            {
                foreach (var go in settings.activeObjects)
                {
                    if (go != null)
                        go.SetActive(on);
                }
            }

            if (_runner == null)
                return;

            if (on)
            {
                _runner.SetSpeedFactor(this, settings.speedFactor);
                _runner.SetHeightOffset(this, settings.heightOffset);
            }
            else
            {
                _runner.ClearSpeedFactor(this);
                _runner.ClearHeightOffset(this);
            }

            if (_runner.Animator != null)
                _runner.Animator.SetBool(settings.animatorBool, on);
        }

        private void RaiseChanged()
        {
            if (_runner != null && !_runner.IsAI)
                GameEvents.TrackEvents.TerrainChanged.Raise(Current);
        }

        private TerrainSettings Find(TerrainType type)
        {
            if (terrains == null)
                return null;

            foreach (var settings in terrains)
            {
                if (settings != null && settings.type == type)
                    return settings;
            }

            return null;
        }
    }
}
