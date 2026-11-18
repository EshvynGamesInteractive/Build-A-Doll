using _Core._Player;
using Game.Player.Features;
using UnityEngine;

namespace Game.Hurdles.Effects
{
    /// <summary>
    /// Eyes gate or new body material pickup: swaps a material slot on the doll (eyes, body).
    /// Needs a DollAppearance on the doll.
    /// </summary>
    public class MaterialEffect : MonoBehaviour, IRunnerEffect, IGateInfo
    {
        [SerializeField] private AppearanceSlot slot = AppearanceSlot.Eyes;
        [SerializeField] private Material material;

        [Tooltip("Text for the gate label. Empty = none.")]
        [SerializeField] private string label;
        [SerializeField] private bool isGood = true;

        public string Label => label;
        public bool IsGood => isGood;

        public void Apply(RunnerController runner)
        {
            if (runner.TryGetFeature(out DollAppearance appearance))
                appearance.SetMaterial(slot, material);
        }
    }
}
