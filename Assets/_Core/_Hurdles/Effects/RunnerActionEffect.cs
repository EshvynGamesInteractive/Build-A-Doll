using System.Collections;
using _Core._Player;
using UnityEngine;

namespace Game.Hurdles.Effects
{
    /// <summary>
    /// Jump / Slide / Side-run patches and the roll gate: makes her jump (higher than a swipe jump),
    /// slide, roll, or run along a wall for a while. Add a TerrainEffect on the same patch to drop
    /// her into water, or place it before a ramp.
    /// </summary>
    public class RunnerActionEffect : MonoBehaviour, IRunnerEffect
    {
        public enum RunnerAction
        {
            Jump,
            Slide,
            SideRun,
            Roll
        }

        [SerializeField] private RunnerAction action = RunnerAction.Jump;

        [Header("Jump")]
        [Tooltip("Jump height compared to a swipe jump (RunnerMovementConfig).")]
        [SerializeField, Min(0.1f)] private float jumpHeightMultiplier = 1.5f;
        [SerializeField, Min(0.1f)] private float jumpAirTimeMultiplier = 1.3f;

        [Header("Side run")]
        [Tooltip("-1 = left wall, 1 = right wall.")]
        [SerializeField, Range(-1f, 1f)] private float side = 1f;
        [SerializeField, Min(0.1f)] private float sideRunDuration = 1.5f;
        [Tooltip("How high up the wall she runs.")]
        [SerializeField] private float sideRunHeight = 0.6f;
        [Tooltip("Animator bool set while running on the wall.")]
        [SerializeField] private string sideRunBool = "WallRun";

        [Header("Animation")]
        [Tooltip("Animator trigger fired on hit, e.g. Roll. Jump and slide animate by themselves.")]
        [SerializeField] private string animatorTrigger;

        public void Apply(RunnerController runner)
        {
            switch (action)
            {
                case RunnerAction.Jump:
                    var config = runner.Config;
                    runner.ForceJump(config.JumpHeight * jumpHeightMultiplier, config.JumpAirTime * jumpAirTimeMultiplier);
                    break;
                case RunnerAction.Slide:
                case RunnerAction.Roll:
                    runner.ForceSlide();
                    break;
                case RunnerAction.SideRun:
                    StartCoroutine(SideRun(runner));
                    break;
            }

            if (runner.Animator != null)
                runner.Animator.PlayTrigger(animatorTrigger);
        }

        private IEnumerator SideRun(RunnerController runner)
        {
            runner.SetLateralTarget(side * runner.Config.TrackHalfWidth);
            runner.LockSteering(this);
            runner.SetHeightOffset(this, sideRunHeight);
            if (runner.Animator != null)
                runner.Animator.SetBool(sideRunBool, true);

            yield return new WaitForSeconds(sideRunDuration);

            if (runner == null)
                yield break;

            runner.UnlockSteering(this);
            runner.ClearHeightOffset(this);
            if (runner.Animator != null)
                runner.Animator.SetBool(sideRunBool, false);
        }
    }
}
