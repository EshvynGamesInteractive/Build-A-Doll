using System;
using UnityEngine;

namespace Game.Player.Features
{
    /// <summary>A ride for RunnerMount: dolphin, bird, reptile, seahorse, or wings for a ramp flight.</summary>
    [Serializable]
    public class MountSettings
    {
        [Tooltip("Spawned at the doll's mount anchor: the dolphin, bird, seahorse, wings...")]
        public GameObject prefab;

        [Tooltip("Optional extra effect spawned with it, e.g. clouds under the wings.")]
        public GameObject effectPrefab;

        [Min(0.1f)] public float duration = 4f;

        [Tooltip("How high above the track she rides (flying). 0 = on the track.")]
        public float heightOffset;

        [Range(0.1f, 3f)] public float speedFactor = 1.3f;

        [Tooltip("Animator bool set while riding, e.g. Ride, Fly, Swim. Empty = none.")]
        public string animatorBool = "Ride";

        [Tooltip("Harmful track elements can't touch her while riding.")]
        public bool invulnerable = true;

        [Tooltip("Steering is off while riding (e.g. a flight that follows the track centre).")]
        public bool lockSteering;
    }
}
