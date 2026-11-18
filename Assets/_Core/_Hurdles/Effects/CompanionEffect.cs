using _Core._Player;
using Game.Player.Features;
using UnityEngine;

namespace Game.Hurdles.Effects
{
    /// <summary>Egg gate or fairy companion: a companion joins her and floats along. Needs a RunnerCompanions on the doll.</summary>
    public class CompanionEffect : MonoBehaviour, IRunnerEffect
    {
        [Tooltip("The pet / egg / fairy prefab (its own animation, e.g. hatching, plays when it spawns).")]
        [SerializeField] private GameObject companionPrefab;

        public void Apply(RunnerController runner)
        {
            if (runner.TryGetFeature(out RunnerCompanions companions))
                companions.Add(companionPrefab);
        }
    }
}
