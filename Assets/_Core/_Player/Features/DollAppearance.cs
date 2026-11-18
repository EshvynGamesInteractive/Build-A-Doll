using System;
using System.Collections.Generic;
using Core._GameEvent;
using UnityEngine;

namespace Game.Player.Features
{
    /// <summary>
    /// Swaps materials on the doll (eyes gate, new body material pickup). Each slot points at a renderer
    /// and material index; the original material comes back on ResetRun.
    /// </summary>
    [DisallowMultipleComponent]
    public class DollAppearance : MonoBehaviour
    {
        [Serializable]
        private class SlotRenderer
        {
            public AppearanceSlot slot;
            public Renderer renderer;
            [Min(0)] public int materialIndex;
        }

        [SerializeField] private SlotRenderer[] slots;

        private readonly Dictionary<SlotRenderer, Material> _originals = new();

        private void OnEnable() => GameEvents.GameplayEvents.ResetRun.Register(ResetAll);

        private void OnDisable() => GameEvents.GameplayEvents.ResetRun.Unregister(ResetAll);

        public void SetMaterial(AppearanceSlot slot, Material material)
        {
            if (material == null || slots == null)
                return;

            foreach (var entry in slots)
            {
                if (entry == null || entry.slot != slot || entry.renderer == null)
                    continue;

                var materials = entry.renderer.sharedMaterials;
                if (entry.materialIndex >= materials.Length)
                    continue;

                _originals.TryAdd(entry, materials[entry.materialIndex]);
                materials[entry.materialIndex] = material;
                entry.renderer.sharedMaterials = materials;
            }
        }

        public void ResetAll()
        {
            foreach (var pair in _originals)
            {
                var renderer = pair.Key.renderer;
                if (renderer == null)
                    continue;

                var materials = renderer.sharedMaterials;
                if (pair.Key.materialIndex < materials.Length)
                {
                    materials[pair.Key.materialIndex] = pair.Value;
                    renderer.sharedMaterials = materials;
                }
            }

            _originals.Clear();
        }
    }
}
