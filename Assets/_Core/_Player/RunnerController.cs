using Core._GameEvent;
using Game.Outfit;
using Game.Player;
using Game.Player.Config;
using Game.Player.Input;
using Game.Player.Movement;
using Game.Score;
using UnityEngine;

namespace _Core._Player
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
            Stopped,
            Walking, // scripted walk (e.g. onto the finish stage); input is ignored
            Placed   // after a scripted walk: stays exactly where she is until ResetRun
        }

        [SerializeField] private RunnerMovementConfig config;

        [Tooltip("Any component that implements IDragInput, e.g. PointerDragInput.")]
        [SerializeField] private MonoBehaviour inputSource;
        [SerializeField] private RunnerAnimator animator;
        [SerializeField] private DollOutfit dollOutfit;
        [SerializeField] private RunScore score;

        [Tooltip("Model that turns towards the drag direction. Leave empty to turn this object.")]
        [SerializeField] private Transform visual;

        [Tooltip("Shrunk while sliding so she fits under bars. Leave empty to use the CapsuleCollider on this object.")]
        [SerializeField] private CapsuleCollider hitbox;

        [Tooltip("Start on the first touch. Turn off once a 'Tap to play' screen raises GameplayEvents.StartRun.")]
        [SerializeField] private bool startOnFirstTouch = true;

        /// <summary>While true, touching doesn't start the run (e.g. while the target look is shown).</summary>
        public bool StartLocked { get; set; }

        private const float MovingThreshold = 0.05f;

        private readonly ForwardMotion _forward = new();
        private readonly LateralMotion _lateral = new();
        private readonly VerticalMotion _vertical = new();
        private readonly RunnerModifiers _modifiers = new();

        private IDragInput _input;
        private IActionInput _actions;
        private Phase _phase = Phase.Idle;
        private float _distance;
        private bool _isStumbling;
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

        public bool IsRunning => _phase == Phase.Walking ? _walkSpeedNow > MovingThreshold : _forward.CurrentSpeed > MovingThreshold;

        public float NormalizedSpeed =>
            config.ForwardSpeed <= 0f ? 0f
            : (_phase == Phase.Walking ? _walkSpeedNow : _forward.CurrentSpeed) / config.ForwardSpeed;

        /// <summary>True while a scripted walk (WalkTo) is still going.</summary>
        public bool IsWalking => _phase == Phase.Walking;

        // ---------- Scripted walk ----------
        private readonly System.Collections.Generic.List<Vector3> _walkPath = new();
        private float _walkSpeed;
        private float _walkSpeedNow;
        private Vector3 _walkFace;
        private System.Action _walkArrived;

        public bool IsGrounded => _vertical.IsGrounded;
        public bool IsSliding => _vertical.IsSliding;
        
        public RunScore Score => score;
        public RunnerAnimator Animator => animator;
        public DollOutfit DollOutfit => dollOutfit;
        public RunnerMovementConfig Config => config;

        /// <summary>True for the AI opponent. Its score, outfit and run start don't raise player events.</summary>
        public bool IsAI { get; private set; }

        /// <summary>Distance run along the track since the start.</summary>
        public float Distance => _distance;

        /// <summary>Sideways position she's heading for, from the track centre.</summary>
        public float LateralTarget => _lateral.TargetOffset;

        /// <summary>Turns this runner into the AI: its own events go quiet so the HUD only shows the player.</summary>
        public void SetAI(bool isAI)
        {
            IsAI = isAI;
            if (dollOutfit != null)
                dollOutfit.RaiseEvents = !isAI;
            if (score != null)
                score.RaiseEvents = !isAI;
        }

        /// <summary>Swaps what drives this runner, e.g. touch input for AI input.</summary>
        public void SetInput(IDragInput drag, IActionInput actions)
        {
            _input = drag;
            _actions = actions;
            if (_input != null && config != null)
                enabled = true;
        }

        /// <summary>Swaps the movement tuning, e.g. a slower config for an easy AI.</summary>
        public void SetConfig(RunnerMovementConfig newConfig)
        {
            if (newConfig != null)
                config = newConfig;
        }

        /// <summary>Stops this runner only (the global StopRun stops everyone).</summary>
        public void Stop() => StopRun();

        /// <summary>
        /// Walks her through the points (world space) at 'speed', playing the run animation, then turns her to face
        /// 'faceDirection' and calls 'onArrived'. Input is ignored meanwhile. ResetRun ends it.
        /// </summary>
        public void WalkTo(System.Collections.Generic.IEnumerable<Vector3> path, float speed, Vector3 faceDirection, System.Action onArrived = null)
        {
            _walkPath.Clear();
            if (path != null)
                _walkPath.AddRange(path);

            _phase = Phase.Walking;
            _walkSpeed = Mathf.Max(0.1f, speed);
            _walkSpeedNow = _forward.CurrentSpeed; // carry on from her run speed, then ease to walking pace
            _walkFace = Vector3.ProjectOnPlane(faceDirection, Vector3.up);
            _walkArrived = onArrived;

            CancelInvoke(nameof(EndStumble));
            _isStumbling = false;
            _vertical.Reset();
            _forward.Reset();
            _modifiers.Reset();
            _squash = 1f;
            if (visual != transform)
                ApplySquash();
            UpdateSlidePose(0f);
        }

        /// <summary>Puts her straight at a point, facing 'faceDirection' (e.g. when a scripted walk can't wait any longer).</summary>
        public void PlaceAt(Vector3 position, Vector3 faceDirection)
        {
            var arrived = _walkArrived;
            _walkPath.Clear();
            _walkArrived = null;
            _walkSpeedNow = 0f;
            _phase = Phase.Placed;
            transform.position = position;
            var face = Vector3.ProjectOnPlane(faceDirection, Vector3.up);
            if (face.sqrMagnitude > 1e-4f)
                visual.rotation = Quaternion.LookRotation(face.normalized, Vector3.up);
            arrived?.Invoke();
        }

        private void UpdateWalk(float deltaTime)
        {
            var turnT = 1f - Mathf.Exp(-config.TurnLerpSpeed * 0.6f * deltaTime);

            if (_walkPath.Count == 0)
            {
                // Arrived: slow down and turn to face the given way.
                _walkSpeedNow = Mathf.MoveTowards(_walkSpeedNow, 0f, config.Deceleration * deltaTime);
                if (_walkFace.sqrMagnitude > 1e-4f)
                    visual.rotation = Quaternion.Slerp(visual.rotation, Quaternion.LookRotation(_walkFace.normalized, Vector3.up), turnT);

                var facing = _walkFace.sqrMagnitude < 1e-4f || Vector3.Angle(visual.forward, _walkFace) < 3f;
                if (_walkSpeedNow <= MovingThreshold && facing)
                {
                    _walkSpeedNow = 0f;
                    _phase = Phase.Placed;
                    var arrived = _walkArrived;
                    _walkArrived = null;
                    arrived?.Invoke();
                }
                return;
            }

            _walkSpeedNow = Mathf.MoveTowards(_walkSpeedNow, _walkSpeed, Mathf.Max(config.Acceleration, config.Deceleration) * deltaTime);

            var target = _walkPath[0];
            var step = _walkSpeedNow * deltaTime;
            var toTarget = target - transform.position;
            if (toTarget.magnitude <= step)
            {
                transform.position = target;
                _walkPath.RemoveAt(0);
                return;
            }

            transform.position += toTarget.normalized * step;
            var flat = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            if (flat.sqrMagnitude > 1e-4f)
                visual.rotation = Quaternion.Slerp(visual.rotation, Quaternion.LookRotation(flat.normalized, Vector3.up), turnT);
        }
        

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
            if (Time.timeScale == 0f)
                return;

            if (_phase == Phase.Walking)
            {
                UpdateWalk(Time.deltaTime);
                return;
            }

            if (_phase == Phase.Placed)
                return;

            if (_phase == Phase.Idle)
            {
                if (startOnFirstTouch && !StartLocked && _input.IsHolding)
                    StartRun();
                else
                    return;
            }

            var deltaTime = Time.deltaTime;
            var canControl = _phase == Phase.Running && !_modifiers.IsHeld;

            if (canControl)
            {
                if (!_modifiers.IsSteeringLocked)
                    _lateral.AddDrag(_input.HorizontalDelta, config.DragSensitivity, config.TrackHalfWidth);
                ReadActions();
            }

            _modifiers.Tick(config.HeightOffsetSpeed, deltaTime);
            _distance += _forward.Tick(GetTargetSpeed(canControl), config.Acceleration, config.Deceleration, deltaTime);
            var offset = _lateral.Tick(config.LateralSmoothTime, deltaTime);
            var height = _vertical.Tick(config.SlideDuration, deltaTime) + _modifiers.Height;

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

            // Boosts (cannon, speed pads) carry her even when the finger is off the screen.
            if (_modifiers.IsBoosting)
                return _modifiers.BoostSpeed;

            var wantsToRun = !config.RunOnlyWhileHolding || _input.IsHolding;
            if (!wantsToRun)
                return 0f;

            var speed = config.ForwardSpeed * _modifiers.SpeedFactor;
            if (_isStumbling)
                return speed * _stumbleSpeedFactor;

            return speed;
        }

        /// <summary>Slows the doll down for a moment, e.g. after hitting a minus item.</summary>
        public void Stumble(float duration, float speedFactor)
        {
            _isStumbling = true;
            _stumbleSpeedFactor = Mathf.Clamp01(speedFactor);

            // A new hit restarts the timer.
            CancelInvoke(nameof(EndStumble));
            Invoke(nameof(EndStumble), duration);

            // Drop speed right away so the hit feels like a hit, not a gentle slowdown.
            _forward.ClampSpeed(config.ForwardSpeed * _stumbleSpeedFactor);
        }

        public bool IsStumbling => _isStumbling;

        private void EndStumble() => _isStumbling = false;

        private void UpdateTurn(float deltaTime)
        {
            var yaw = Mathf.Clamp(_lateral.Velocity * config.TurnAnglePerSpeed, -config.MaxTurnAngle, config.MaxTurnAngle);
            var target = _visualBaseRotation * Quaternion.Euler(0f, yaw, 0f);
            var t = 1f - Mathf.Exp(-config.TurnLerpSpeed * deltaTime);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, target, t);
        }

        // ---------- Track elements (gates, boosters, set pieces) ----------

        /// <summary>True while something holds her in place (make-up step, cannon, rewarded offer).</summary>
        public bool IsHeld => _modifiers.IsHeld;

        /// <summary>Stops her on the spot and ignores input until every source has released her.</summary>
        public void Hold(object source)
        {
            _modifiers.Hold(source);
            _forward.SetSpeed(0f);
        }

        public void Release(object source) => _modifiers.Release(source);

        /// <summary>Ignores sideways drags until unlocked (e.g. while running along a wall).</summary>
        public void LockSteering(object source) => _modifiers.LockSteering(source);

        public void UnlockSteering(object source) => _modifiers.UnlockSteering(source);

        /// <summary>Multiplies her run speed while set (terrain, rides, statuses). Factors from different sources multiply.</summary>
        public void SetSpeedFactor(object source, float factor) => _modifiers.SetSpeedFactor(source, factor);

        public void ClearSpeedFactor(object source) => _modifiers.ClearSpeedFactor(source);

        /// <summary>Lifts her above the track while set (flying, wall run). The highest offset wins; she eases to it.</summary>
        public void SetHeightOffset(object source, float height) => _modifiers.SetHeightOffset(source, height);

        public void ClearHeightOffset(object source) => _modifiers.ClearHeightOffset(source);

        /// <summary>Runs at 'speed' for 'duration' seconds, even without holding the screen.</summary>
        public void Boost(float speed, float duration)
        {
            _modifiers.Boost(speed, duration);
            _forward.SetSpeed(speed);
        }

        /// <summary>Jumps without a swipe (jump patch). Only works on the ground.</summary>
        public void ForceJump(float height, float airTime) => _vertical.Jump(height, airTime);

        /// <summary>Slides without a swipe (slide patch, roll gate).</summary>
        public void ForceSlide() => _vertical.Slide(config.SlideDuration, config.FastFallSpeed);

        /// <summary>Sends her sideways to 'offset' from the track centre (kept inside the track).</summary>
        public void SetLateralTarget(float offset) => _lateral.SetTarget(offset, config.TrackHalfWidth);

        /// <summary>Shoots her 'distance' down the track in an arc 'height' high that lasts 'airTime' seconds (cannon).</summary>
        public void Launch(float distance, float height, float airTime)
        {
            airTime = Mathf.Max(0.1f, airTime);
            if (!_vertical.IsGrounded)
                _vertical.Reset();

            _vertical.Jump(height, airTime);
            Boost(distance / airTime, airTime);
        }

        /// <summary>Starts this runner now (e.g. when the target-look intro ends), without waiting for a touch.</summary>
        public void BeginRun() => StartRun();

        private void StartRun()
        {
            if (_phase is Phase.Running or Phase.Walking or Phase.Placed)
                return;

            _phase = Phase.Running;
            if (!IsAI)
                GameEvents.GameplayEvents.RunStarted.Raise();
        }

        private void StopRun()
        {
            if (_phase == Phase.Running)
                _phase = Phase.Stopped;
        }

        /// <summary>True once she's stopped (finish line or scripted walk done).</summary>
        public bool IsStopped => _phase is Phase.Stopped or Phase.Placed;

        private void ResetRun()
        {
            _phase = Phase.Idle;
            _walkPath.Clear();
            _walkArrived = null;
            _walkSpeedNow = 0f;
            _distance = 0f;
            CancelInvoke(nameof(EndStumble));
            _isStumbling = false;
            _forward.Reset();
            _lateral.Reset();
            _vertical.Reset();
            _modifiers.Reset();

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
