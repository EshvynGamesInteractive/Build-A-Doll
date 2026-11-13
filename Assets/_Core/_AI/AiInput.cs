using System.Collections.Generic;
using _Core._Player;
using Core._GameEvent;
using Game.Player.Input;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Drives a RunnerController like a player would: holds to run, steers, jumps and slides,
    /// following the steps AiPlanner worked out at level start. The controller can't tell it
    /// apart from touch input, so the AI moves with exactly the same rules as the player.
    /// </summary>
    [DefaultExecutionOrder(-100)] // decide before the controller moves, like PointerDragInput
    public class AiInput : MonoBehaviour, IDragInput, IActionInput
    {
        public bool IsHolding { get; private set; }
        public float HorizontalDelta { get; private set; }
        public bool JumpRequested { get; private set; }
        public bool SlideRequested { get; private set; }

        private RunnerController _runner;
        private Transform _ramp;
        private AiProfile _profile;
        private List<AiStep> _steps = new();
        private int _next;
        private Transform _steerTarget;
        private float _steerLateral;

        /// <summary>Hands the AI its runner, its ramp and its plan. Call again for each level.</summary>
        public void Init(RunnerController runner, Transform ramp, AiProfile profile, List<AiStep> steps)
        {
            _runner = runner;
            _ramp = ramp;
            _profile = profile;
            _steps = steps ?? new List<AiStep>();
            ResetPlan();
        }

        /// <summary>Starts running after the profile's start delay.</summary>
        public void Go()
        {
            CancelInvoke(nameof(BeginHolding));
            Invoke(nameof(BeginHolding), _profile != null ? _profile.StartDelay : 0f);
        }

        private void OnEnable() => GameEvents.GameplayEvents.ResetRun.Register(ResetPlan);

        private void OnDisable() => GameEvents.GameplayEvents.ResetRun.Unregister(ResetPlan);

        private void Update()
        {
            JumpRequested = false;
            SlideRequested = false;
            HorizontalDelta = 0f;

            if (Time.timeScale == 0f || !IsHolding || _runner == null || _profile == null)
                return;

            // One step per frame at most; steps are sorted by distance.
            if (_next < _steps.Count && _runner.Distance >= _steps[_next].Distance)
            {
                Apply(_steps[_next]);
                _next++;
            }

            Steer();
        }

        private void Apply(AiStep step)
        {
            switch (step.Action)
            {
                case AiStep.Kind.Jump:
                    JumpRequested = true;
                    break;
                case AiStep.Kind.Slide:
                    SlideRequested = true;
                    break;
                default:
                    _steerTarget = step.Target;
                    _steerLateral = step.Lateral;
                    break;
            }
        }

        // Moves the runner's sideways target towards where the AI wants to be, at its steer speed.
        private void Steer()
        {
            var config = _runner.Config;
            if (config == null || config.DragSensitivity <= 0f)
                return;

            var wanted = _steerTarget != null && _ramp != null
                ? _ramp.InverseTransformPoint(_steerTarget.position).x
                : _steerLateral;

            var maxStep = _profile.SteerSpeed * Time.deltaTime;
            var step = Mathf.Clamp(wanted - _runner.LateralTarget, -maxStep, maxStep);

            // The controller reads drag as a fraction of the screen; convert back from world units.
            HorizontalDelta = step / config.DragSensitivity;
        }

        private void BeginHolding() => IsHolding = true;

        private void ResetPlan()
        {
            CancelInvoke(nameof(BeginHolding));
            IsHolding = false;
            _next = 0;
            _steerTarget = null;
            _steerLateral = 0f;
        }
    }
}
