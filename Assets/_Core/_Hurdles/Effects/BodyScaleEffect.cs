using _Core._Player;
using Game.Player.Features;
using UnityEngine;

namespace Game.Hurdles.Effects
{
    /// <summary>
    /// Body gate. Enhancer (type 1) sets a body part's scale: Head x1-2, Legs x0.5-3, Arms x0.5-3,
    /// Torso x2, Neck x2, Shoulders x3 (below 1 shrinks it). Regulator (type 0) puts it back to normal.
    /// Needs a DollBody on the doll.
    /// </summary>
    public class BodyScaleEffect : MonoBehaviour, IRunnerEffect, IGateInfo
    {
        public enum GateType
        {
            Regulator = 0,
            Enhancer = 1
        }

        [SerializeField] private GateType type = GateType.Enhancer;
        [SerializeField] private BodyPart part = BodyPart.Head;

        [Tooltip("Enhancer only: the part's new scale. Below 1 shrinks it.")]
        [SerializeField, Range(0.3f, 4f)] private float value = 2f;

        public float TargetScale => type == GateType.Regulator ? 1f : value;

        public string Label => type == GateType.Regulator ? $"{part}\nNORMAL" : $"{part}\nx{value:0.#}";

        /// <summary>Only a normal-sized part is good for the look.</summary>
        public bool IsGood => Mathf.Approximately(TargetScale, 1f);

        public void Apply(RunnerController runner)
        {
            if (runner.TryGetFeature(out DollBody body))
                body.SetScale(part, TargetScale);
        }
    }
}
