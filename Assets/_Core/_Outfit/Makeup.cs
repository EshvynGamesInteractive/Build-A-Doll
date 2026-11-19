using System;
using UnityEngine;

namespace Game.Outfit
{
    /// <summary>Part of the face a paint gate paints. Saved as numbers, so only ever add new values at the end.</summary>
    public enum MakeupZone
    {
        Lips = 0,
        [InspectorName("Eye Shadow")] EyeShadow = 1,
        Blush = 2
    }

    /// <summary>A makeup colour on one face zone (e.g. pink Eye Shadow).</summary>
    [Serializable]
    public struct MakeupColor
    {
        public MakeupZone zone;
        [Tooltip("Shown on gates, e.g. 'Pink'.")]
        public string name;
        public Color color;

        public MakeupColor(MakeupZone zone, string name, Color color)
        {
            this.zone = zone;
            this.name = name;
            this.color = color;
        }

        /// <summary>Two colours count as the same when every channel is within this.</summary>
        public const float Tolerance = 0.08f;

        public static bool Same(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) <= Tolerance && Mathf.Abs(a.g - b.g) <= Tolerance && Mathf.Abs(a.b - b.b) <= Tolerance;

        public static string ZoneName(MakeupZone zone) => zone switch
        {
            MakeupZone.Lips => "LIPS",
            MakeupZone.EyeShadow => "EYES",
            MakeupZone.Blush => "BLUSH",
            _ => zone.ToString().ToUpperInvariant()
        };
    }
}
