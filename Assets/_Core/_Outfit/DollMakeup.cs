using System;
using System.Collections.Generic;
using Core._GameEvent;
using UnityEngine;

namespace Game.Outfit
{
    /// <summary>Colors authored face overlays. Never creates scene objects, meshes or materials.</summary>
    [DisallowMultipleComponent]
    public class DollMakeup : MonoBehaviour
    {
        [Serializable]
        public class Zone
        {
            public MakeupZone zone;
            [Tooltip("Authored face overlays, using BuildADoll/MakeupOverlay. One renderer per volume.")]
            public Renderer[] overlays;
            [Tooltip("Face-aligned ellipses: local XY covers the region, local Z points out of the face.")]
            public Transform[] volumes;
        }

        [SerializeField] private List<Zone> zones = new();
        [SerializeField] private bool raiseEvents = true;
        private readonly Dictionary<MakeupZone, Color> _painted = new();
        private MakeupColor[] _startingMakeup = Array.Empty<MakeupColor>();
        private MaterialPropertyBlock _block;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int StrengthId = Shader.PropertyToID("_Strength");

        public bool RaiseEvents { get => raiseEvents; set => raiseEvents = value; }
        public IReadOnlyDictionary<MakeupZone, Color> Painted => _painted;
        public bool TryGetColor(MakeupZone zone, out Color color) => _painted.TryGetValue(zone, out color);
        public bool HasZone(MakeupZone zone) => GetZone(zone) != null;
        public Zone GetZone(MakeupZone zone)
        {
            foreach (var entry in zones)
                if (entry != null && entry.zone == zone)
                    return entry;
            return null;
        }

        public bool CanPaint(MakeupZone zone)
        {
            var entry = GetZone(zone);
            if (entry?.overlays == null || entry.volumes == null || entry.volumes.Length == 0 ||
                entry.overlays.Length != entry.volumes.Length)
                return false;
            for (var i = 0; i < entry.volumes.Length; i++)
                if (entry.volumes[i] == null || entry.overlays[i] == null)
                    return false;
            return true;
        }

        private void OnEnable() => GameEvents.GameplayEvents.ResetRun.Register(ResetMakeup);
        private void OnDisable() => GameEvents.GameplayEvents.ResetRun.Unregister(ResetMakeup);
        private void Start() => ResetMakeup();

        public void Paint(MakeupZone zone, Color color, bool instant = false)
        {
            if (!CanPaint(zone))
                return;
            var entry = GetZone(zone);
            for (var i = 0; i < entry.overlays.Length; i++)
                SetCoverage(zone, i, color, 1f);
            _painted[zone] = color;
            if (raiseEvents)
                GameEvents.GameplayEvents.MakeupPainted.Raise(zone, color);
        }

        public void SetCoverage(MakeupZone zone, int region, Color color, float coverage)
        {
            var entry = GetZone(zone);
            if (entry?.overlays == null || region < 0 || region >= entry.overlays.Length)
                return;
            var overlay = entry.overlays[region];
            if (overlay == null)
                return;
            _block ??= new MaterialPropertyBlock();
            overlay.GetPropertyBlock(_block);
            _block.SetFloat("_UsePaintMask", 0f);
            _block.SetColor(ColorId, color);
            _block.SetFloat(StrengthId, Mathf.Clamp01(coverage) * 0.85f);
            overlay.SetPropertyBlock(_block);
            overlay.enabled = coverage > 0f;
        }

        public void SetBrushMask(MakeupZone zone, int region, float[] mask)
        {
            var entry = GetZone(zone);
            var overlay = entry.overlays[region];
            overlay.GetPropertyBlock(_block);
            _block.SetFloat("_UsePaintMask", 1f);
            _block.SetFloatArray("_PaintMask", mask);
            _block.SetMatrix("_WorldToPaint", entry.volumes[region].worldToLocalMatrix);
            // Mask controls the painted area; strength stays constant inside each brush stroke.
            _block.SetFloat(StrengthId, 0.85f);
            overlay.SetPropertyBlock(_block);
        }

        public void SetStartingMakeup(IReadOnlyList<MakeupColor> makeup)
        {
            _startingMakeup = new MakeupColor[makeup?.Count ?? 0];
            for (var i = 0; i < _startingMakeup.Length; i++)
                _startingMakeup[i] = makeup[i];
        }

        private void ResetMakeup()
        {
            _painted.Clear();
            foreach (var entry in zones)
                if (entry?.overlays != null)
                    for (var i = 0; i < entry.overlays.Length; i++)
                        SetCoverage(entry.zone, i, Color.white, 0f);
            var events = raiseEvents;
            raiseEvents = false;
            foreach (var entry in _startingMakeup)
                Paint(entry.zone, entry.color, true);
            raiseEvents = events;
        }

        // Kept for existing editor menu callers; authoring must be done manually.
        public void CreateDefaultZones() => Debug.LogWarning(
            "Assign face overlays and face-aligned volume transforms manually in DollMakeup. No objects were created.", this);
    }
}
