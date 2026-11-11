using Core._GameEvent;
using Game.Player.Config;
using Game.Player.Input;
using Game.Player.Movement;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Runs the doll along the track: forward on her own (or while holding), sideways by dragging.
    /// Only wires input, config and the two motion classes together; the maths lives in
    /// ForwardMotion and LateralMotion, and the tuning lives in RunnerMovementConfig.
    /// The track runs along this object's forward axis as placed in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class RunnerController : MonoBehaviour, IRunnerState
    {
        private enum Phase
        {
            Idle,
            Running,
            Stopped
        }

        [SerializeField] private RunnerMovementConfig config;

        [Tooltip("Any component that implements IDragInput, e.g. PointerDragInput.")]
        [SerializeField] private MonoBehaviour inputSource;

        [Tooltip("Model that turns towards the drag direction. Leave empty to turn this object.")]
        [SerializeField] private Transform visual;

        [Tooltip("Start on the first touch. Turn off once a 'Tap to play' screen raises GameplayEvents.StartRun.")]
        [SerializeField] private bool startOnFirstTouch = true;

        private const float MovingThreshold = 0.05f;

        private readonly ForwardMotion _forward = new();
        private readonly LateralMotion _lateral = new();

        private IDragInput _input;
        private Phase _phase = Phase.Idle;
        private float _distance;

        private Vector3 _startPosition;
        private Vector3 _trackForward;
        private Vector3 _trackRight;
        private Quaternion _visualBaseRotation;

        public bool IsRunning => _forward.CurrentSpeed > MovingThreshold;

        public float NormalizedSpeed =>
            config.ForwardSpeed > 0f ? _forward.CurrentSpeed / config.ForwardSpeed : 0f;

        private void Awake()
        {
            if (visual == null)
                visual = transform;

            _input = inputSource as IDragInput ?? GetComponentInChildren<IDragInput>();

            if (config == null || _input == null)
            {
                Debug.LogError($"{nameof(RunnerController)} on '{name}' needs a config and an IDragInput.", this);
                enabled = false;
                return;
            }

            _startPosition = transform.position;
            _trackForward = transform.forward;
            _trackRight = transform.right;
            _visualBaseRotation = visual.localRotation;
        }

        private void OnEnable()
        {
            GameEvents.GameplayEvents.StartRun.Register(StartRun);
            GameEvents.GameplayEvents.StopRun.Register(StopRun);
            GameEvents.GameplayEvents.ResetRun.Register(ResetRun);
        }

        private void OnDisable()
        {
            GameEvents.GameplayEvents.StartRun.Unregister(StartRun);
            GameEvents.GameplayEvents.StopRun.Unregister(StopRun);
            GameEvents.GameplayEvents.ResetRun.Unregister(ResetRun);
        }

        private void Update()
        {
            if (_phase == Phase.Idle)
            {
                if (startOnFirstTouch && _input.IsHolding)
                    StartRun();
                else
                    return;
            }

            var deltaTime = Time.deltaTime;
            var canControl = _phase == Phase.Running;

            if (canControl)
                _lateral.AddDrag(_input.HorizontalDelta, config.DragSensitivity, config.TrackHalfWidth);

            _distance += _forward.Tick(GetTargetSpeed(canControl), config.Acceleration, config.Deceleration, deltaTime);
            var offset = _lateral.Tick(config.LateralSmoothTime, deltaTime);

            transform.position = _startPosition + _trackForward * _distance + _trackRight * offset;
            UpdateTurn(deltaTime);
        }

        private float GetTargetSpeed(bool canControl)
        {
            if (!canControl)
                return 0f;

            var wantsToRun = !config.RunOnlyWhileHolding || _input.IsHolding;
            return wantsToRun ? config.ForwardSpeed : 0f;
        }

        private void UpdateTurn(float deltaTime)
        {
            var yaw = Mathf.Clamp(_lateral.Velocity * config.TurnAnglePerSpeed, -config.MaxTurnAngle, config.MaxTurnAngle);
            var target = _visualBaseRotation * Quaternion.Euler(0f, yaw, 0f);
            var t = 1f - Mathf.Exp(-config.TurnLerpSpeed * deltaTime);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, target, t);
        }

        private void StartRun()
        {
            if (_phase == Phase.Running)
                return;

            _phase = Phase.Running;
            GameEvents.GameplayEvents.RunStarted.Raise();
        }

        private void StopRun()
        {
            if (_phase == Phase.Running)
                _phase = Phase.Stopped;
        }

        private void ResetRun()
        {
            _phase = Phase.Idle;
            _distance = 0f;
            _forward.Reset();
            _lateral.Reset();

            transform.position = _startPosition;
            visual.localRotation = _visualBaseRotation;
        }

        private void OnValidate()
        {
            if (inputSource != null && inputSource is not IDragInput)
            {
                Debug.LogWarning($"'{inputSource.name}' does not implement {nameof(IDragInput)}.", this);
                inputSource = null;
            }
        }
    }
}
