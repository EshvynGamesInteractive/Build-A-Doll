using _Core._Player;
using Game.Player.Features;
using UnityEngine;

namespace Game.Hurdles.Effects
{
    /// <summary>
    /// Starts or ends a status on the doll: fire (Apply OnFire), fire extinguisher (Clear OnFire),
    /// poop / dirt (Apply Dirty), rewarded shield (Apply Shielded). Needs a DollStatus on the doll,
    /// with settings for the status.
    /// </summary>
    public class StatusEffect : MonoBehaviour, IRunnerEffect
    {
        public enum Mode
        {
            Apply,
            Clear,
            ClearAllHarmful
        }

        [SerializeField] private Mode mode = Mode.Apply;
        [SerializeField] private StatusType status = StatusType.OnFire;

        public void Apply(RunnerController runner)
        {
            if (!runner.TryGetFeature(out DollStatus doll))
                return;

            switch (mode)
            {
                case Mode.Apply:
                    doll.Apply(status);
                    break;
                case Mode.Clear:
                    doll.Clear(status);
                    break;
                case Mode.ClearAllHarmful:
                    doll.ClearHarmful();
                    break;
            }
        }
    }
}
