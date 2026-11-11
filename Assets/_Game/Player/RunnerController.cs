using Core._GameEvent;
using Game.Player.Config;
using Game.Player.Input;
using Game.Player.Movement;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Runs the doll along the track: forward on her own (or while holding), sideways by dragging,
    /// swipe up to jump over obstacles, swipe down to slide under them.
    /// Only wires input, config and the motion classes together; the maths lives in
    /// ForwardMotion, LateralMotion and VerticalMotion, and the tuning lives in RunnerMovementConfig.
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

        [Tooltip("Shrunk while sliding so she fits under bars. Leave empty to use the CapsuleCollider on this object.")]
        [SerializeField] private CapsuleCollider hitbox;

        [Tooltip("Start on the first touch. Turn off once a 'Tap to play' screen raises GameplayEvents.StartRun.")]
        [SerializeField] private bool startOnFirstTouch = true;

        private const float MovingThreshold = 0.05f;

        private readonly ForwardMotion _forward = new();
        private readonly LateralMotion _lateral = new();
        private readonly VerticalMotion _vertical = new();

        private IDragInput _input;
        private IActionInput _actions;
        private Phase _phase = Phase.Idle;
        private float _distance;
        private float _stumbleUntil;
        private float _stumbleSpeedFactor = 1f;

        private Vector3 _startPosition;
        private Vector3 _trackForward;
        private Vector3 _trackRight;
        private Quaternion _visualBaseRotation;
        private Vector3 _visualBasePosition;
        private Vector3 _visualBaseScale;
        private float _hitboxHeight;
        private Vector3 _hitboxCenter;
        private float _squash = 1f;

        public bool IsRunning => _forward.CurrentSpeed > MovingThreshold;

        public float NormalizedSpeed =>
            config.ForwardSpeed > 0f ? _forward.CurrentSpeed / config.ForwardSpeed : 0f;

        public bool IsGrounded => _vertical.IsGrounded;
        public bool IsSliding => _vertical.IsSliding;

        private void Awake()
        {
            if (visual == null)
                visual = transform;

            _input = inputSource as IDragInput ?? GetComponentInChildren<IDragInput>();
            _actions = inputSource as IActionInput ?? GetComponentInChildren<IActionInput>();

            if (hitbox == null)
                hitbox = GetComponent<CapsuleCollider>();

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
            _visualBasePosition = visual.localPosition;
            _visualBaseScale = visual.localScale;

            if (hitbox != null)
            {
                _hitboxHeight = hitbox.height;
                _hitboxCenter = hitbox.center;
            }
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
            {
                _lateral.AddDrag(_input.HorizontalDelta, config.DragSensitivity, config.TrackHalfWidth);
                ReadActions();
            }

            _distance += _forward.Tick(GetTargetSpeed(canControl), config.Acceleration, config.Deceleration, deltaTime);
            var offset = _lateral.Tick(config.LateralSmoothTime, deltaTime);
            var height = _vertical.Tick(config.SlideDuration, deltaTime);

            transform.position = _startPosition + _trackForward * _distance + _trackRight * offset + Vector3.up * height;
            UpdateTurn(deltaTime);
            UpdateSlidePose(deltaTime);
        }

        private void ReadActions()
        {
            if (_actions == null)
                return;

            if (_actions.JumpRequested)
                _vertical.Jump(config.JumpHeight, config.JumpAirTime);
            else if (_actions.SlideRequested)
                _vertical.Slide(config.SlideDuration, config.FastFallSpeed);
        }

        // Shrinks the hitbox (so bars pass over her) and, for the placeholder, squashes the model.
        private void UpdateSlidePose(float deltaTime)
        {
            var sliding = _vertical.IsSliding;

            if (hitbox != null)
            {
                var height = sliding ? Mathf.Min(config.SlideHitboxHeight, _hitboxHeight) : _hitboxHeight;
                hitbox.height = height;
                // Keep the bottom of the capsule where it was.
                var bottom = _hitboxCenter.y - _hitboxHeight * 0.5f;
                hitbox.center = new Vector3(_hitboxCenter.x, bottom + height * 0.5f, _hitboxCenter.z);
            }

            if (!config.SquashVisualOnSlide || visual == transform)
                return;

            var target = sliding ? config.SlideVisualScale : 1f;
            _squash = Mathf.MoveTowards(_squash, target, deltaTime * 8f);
            ApplySquash();
        }

        // Squash towards the feet: assumes the model's parent sits at ground level.
        private void ApplySquash()
        {
            visual.localScale = new Vector3(_visualBaseScale.x, _visualBaseScale.y * _squash, _visualBaseScale.z);
            visual.localPosition = new Vector3(_visualBasePosition.x, _visualBasePosition.y * _squash, _visualBasePosition.z);
        }

        private float GetTargetSpeed(bool canControl)
        {
            if (!canControl)
                return 0f;

            var wantsToRun = !config.RunOnlyWhileHolding || _input.IsHolding;
            if (!wantsToRun)
                return 0f;

            if (Time.time < _stumbleUntil)
                return config.ForwardSpeed * _stumbleSpeedFactor;

            return config.ForwardSpeed;
        }

        /// <summary>Slows the doll down for a moment, e.g. after hitting a minus item.</summary>
        public void Stumble(float duration, float speedFactor)
        {
            _stumbleUntil = Time.time + duration;
            _stumbleSpeedFactor = Mathf.Clamp01(speedFactor);

            // Drop speed right away so the hit feels like a hit, not a gentle slowdown.
            _forward.ClampSpeed(config.ForwardSpeed * _stumbleSpeedFactor);
        }

        public bool IsStumbling => Time.time < _stumbleUntil;

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
            _stumbleUntil = 0f;
            _forward.Reset();
            _lateral.Reset();
            _vertical.Reset();

            transform.position = _startPosition;
            visual.localRotation = _visualBaseRotation;
            _squash = 1f;
            if (visual != transform)
                ApplySquash();
            UpdateSlidePose(0f);
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
