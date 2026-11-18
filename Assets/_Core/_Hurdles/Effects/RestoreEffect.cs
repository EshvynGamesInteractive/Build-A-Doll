using _Core._Player;
using Game.Player.Features;
using UnityEngine;

namespace Game.Hurdles.Effects
{
    /// <summary>Rewarded fix: repairs the doll mid-run (ends fire / dirt, body back to normal, colours and materials back).</summary>
    public class RestoreEffect : MonoBehaviour, IRunnerEffect
    {
        [SerializeField] private bool clearHarmfulStatuses = true;
        [SerializeField] private bool resetBody = true;
        [SerializeField] private bool resetPaint = true;
        [SerializeField] private bool resetAppearance;

        public void Apply(RunnerController runner)
        {
            if (clearHarmfulStatuses && runner.TryGetFeature(out DollStatus status))
                status.ClearHarmful();
            if (resetBody && runner.TryGetFeature(out DollBody body))
                body.ResetAll();
            if (resetPaint && runner.TryGetFeature(out DollPaint paint))
                paint.ResetColors();
            if (resetAppearance && runner.TryGetFeature(out DollAppearance appearance))
                appearance.ResetAll();
        }
    }
}
