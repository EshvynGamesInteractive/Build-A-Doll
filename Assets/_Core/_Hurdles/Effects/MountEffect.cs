using _Core._Player;
using Game.Player.Features;
using UnityEngine;

namespace Game.Hurdles.Effects
{
    /// <summary>
    /// Rewarded pet (dolphin, bird, reptile), rewarded seahorse, or rewarded ramp flight (wings + clouds):
    /// she rides it for a few seconds. Needs a RunnerMount on the doll.
    /// </summary>
    public class MountEffect : MonoBehaviour, IRunnerEffect
    {
        [SerializeField] private MountSettings mount = new();

        public void Apply(RunnerController runner)
        {
            if (runner.TryGetFeature(out RunnerMount runnerMount))
                runnerMount.Mount(mount);
        }
    }
}
