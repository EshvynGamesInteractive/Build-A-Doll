using Core._GameEvent;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Feeds the runner's state into an Animator. Kept apart from movement so the doll model
    /// (and its animator) can change without touching the controller.
    /// Parameters that the Animator doesn't have are skipped.
    /// </summary>
    public class RunnerAnimator : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        [Tooltip("Any component that implements IRunnerState, e.g. RunnerController.")]
        [SerializeField] private MonoBehaviour stateSource;

        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string isRunningParameter = "IsRunning";
        [Tooltip("Trigger fired when she hits a minus item.")]
        [SerializeField] private string stumbleParameter = "Stumble";
        [SerializeField] private string isGroundedParameter = "IsGrounded";
        [SerializeField] private string isSlidingParameter = "IsSliding";
        [Tooltip("Trigger fired on take-off.")]
        [SerializeField] private string jumpParameter = "Jump";
        [Tooltip("Trigger fired when a slide starts.")]
        [SerializeField] private string slideParameter = "Slide";

        [Header("Run speed sync")]
        [Tooltip("Float the Locomotion state uses as its speed multiplier, so her feet match how fast she moves.")]
        [SerializeField] private string runAnimSpeedParameter = "RunAnimSpeed";

        [Tooltip("How fast (units/sec) the run animation looks like it's going on a doll at scale 1. " +
                 "Feet slide forward / animation too fast: raise it. Feet look like they skate backwards: lower it. Tune in Play mode.")]
        [SerializeField, Min(0.1f)] private float runClipSpeed = 4f;

        [Header("Slide speed sync")]
        [Tooltip("Float the Slide state uses as its speed multiplier, so the slide clip lasts exactly as long as the slide (RunnerMovementConfig.SlideDuration).")]
        [SerializeField] private string slideAnimSpeedParameter = "SlideAnimSpeed";
        [Tooltip("Name of the slide clip in the animator, used to read its length.")]
        [SerializeField] private string slideClipName = "Sliding";

        [SerializeField, Range(0.1f, 1f)] private float minRunAnimSpeed = 0.5f;
        [SerializeField, Range(1f, 3f)] private float maxRunAnimSpeed = 1.6f;

        [Header("Result")]
        [Tooltip("Trigger fired when this doll wins the level (Victory, then a dance).")]
        [SerializeField] private string winParameter = "Win";
        [Tooltip("Trigger fired when the player loses the level.")]
        [SerializeField] private string loseParameter = "Lose";
        [Tooltip("Trigger fired when the AI loses, so the two dolls don't react the same.")]
        [SerializeField] private string aiLoseParameter = "LoseAlt";

        private _Core._Player.RunnerController _runner;

        private IRunnerState _state;
        private int _speedHash;
        private int _isRunningHash;
        private bool _hasSpeed;
        private bool _hasIsRunning;
        private int _stumbleHash;
        private bool _hasStumble;
        private int _isGroundedHash, _isSlidingHash, _jumpHash, _slideHash;
        private bool _hasIsGrounded, _hasIsSliding, _hasJump, _hasSlide;
        private bool _wasGrounded = true;
        private bool _wasSliding;
        private bool _wasStumbling;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            _state = stateSource as IRunnerState ?? GetComponentInParent<IRunnerState>();

            if (animator == null || _state == null)
            {
                enabled = false;
                return;
            }

            _speedHash = Animator.StringToHash(speedParameter);
            _isRunningHash = Animator.StringToHash(isRunningParameter);
            _hasSpeed = HasParameter(_speedHash, AnimatorControllerParameterType.Float);
            _hasIsRunning = HasParameter(_isRunningHash, AnimatorControllerParameterType.Bool);
            _stumbleHash = Animator.StringToHash(stumbleParameter);
            _hasStumble = HasParameter(_stumbleHash, AnimatorControllerParameterType.Trigger);

            _isGroundedHash = Animator.StringToHash(isGroundedParameter);
            _isSlidingHash = Animator.StringToHash(isSlidingParameter);
            _jumpHash = Animator.StringToHash(jumpParameter);
            _slideHash = Animator.StringToHash(slideParameter);
            _hasIsGrounded = HasParameter(_isGroundedHash, AnimatorControllerParameterType.Bool);
            _hasIsSliding = HasParameter(_isSlidingHash, AnimatorControllerParameterType.Bool);
            _hasJump = HasParameter(_jumpHash, AnimatorControllerParameterType.Trigger);
            _hasSlide = HasParameter(_slideHash, AnimatorControllerParameterType.Trigger);

            _runner = GetComponentInParent<_Core._Player.RunnerController>();
            _runAnimSpeedHash = Animator.StringToHash(runAnimSpeedParameter);
            _hasRunAnimSpeed = HasParameter(_runAnimSpeedHash, AnimatorControllerParameterType.Float);

            _slideAnimSpeedHash = Animator.StringToHash(slideAnimSpeedParameter);
            _hasSlideAnimSpeed = HasParameter(_slideAnimSpeedHash, AnimatorControllerParameterType.Float);
            _slideClipLength = FindClipLength(slideClipName);
        }

        private int _slideAnimSpeedHash;
        private bool _hasSlideAnimSpeed;
        private float _slideClipLength;

        private float FindClipLength(string clipName)
        {
            if (animator.runtimeAnimatorController == null || string.IsNullOrEmpty(clipName))
                return 0f;

            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name == clipName)
                    return clip.length;
            }

            return 0f;
        }

        // Plays the whole slide clip (down, slide, get up) in the time the slide lasts.
        private void UpdateSlideSpeed()
        {
            if (!_hasSlideAnimSpeed || _slideClipLength <= 0f || _runner == null || _runner.Config == null)
                return;

            var duration = _runner.Config.SlideDuration;
            animator.SetFloat(_slideAnimSpeedHash, duration > 0.01f ? _slideClipLength / duration : 1f);
        }

        private int _runAnimSpeedHash;
        private bool _hasRunAnimSpeed;

        // Playback speed of the run so its stride matches her real speed. A bigger doll takes longer
        // strides, so it plays slower; a faster run plays faster.
        private float RunAnimSpeed()
        {
            if (_runner == null || _runner.Config == null)
                return 1f;

            var groundSpeed = _runner.Config.ForwardSpeed * _state.NormalizedSpeed;
            var size = Mathf.Max(0.01f, animator.transform.lossyScale.y);
            return Mathf.Clamp(groundSpeed / (runClipSpeed * size), minRunAnimSpeed, maxRunAnimSpeed);
        }

        private void OnEnable()
        {
            GameEvents.GameplayEvents.LevelCompleted.Register(OnLevelCompleted);
            GameEvents.GameplayEvents.LevelFailed.Register(OnLevelFailed);
            GameEvents.GameplayEvents.ResetRun.Register(OnResetRun);
        }

        private void OnDisable()
        {
            GameEvents.GameplayEvents.LevelCompleted.Unregister(OnLevelCompleted);
            GameEvents.GameplayEvents.LevelFailed.Unregister(OnLevelFailed);
            GameEvents.GameplayEvents.ResetRun.Unregister(OnResetRun);
        }

        private bool IsAI => _runner != null && _runner.IsAI;

        /// <summary>The doll's Animator (may be null).</summary>
        public Animator UnityAnimator => animator;

        /// <summary>
        /// Coroutine: call right after the Win / Lose trigger was fired. Waits until the animator has moved into the
        /// result state and that state has played once (or moved on, e.g. Victory into Dance). Gives up after maxTime.
        /// </summary>
        public System.Collections.IEnumerator WaitForResultAnimation(float maxTime, float minTime = 0.5f)
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
            {
                yield return new WaitForSeconds(minTime);
                yield break;
            }

            var startState = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            var resultState = 0;
            var elapsed = 0f;

            while (elapsed < maxTime)
            {
                yield return null;
                elapsed += Time.deltaTime;

                if (animator.IsInTransition(0))
                    continue;

                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (resultState == 0)
                {
                    // Still in the old pose: the trigger hasn't been picked up yet.
                    if (info.fullPathHash != startState)
                        resultState = info.fullPathHash;
                    else if (elapsed > 1.5f)
                        break; // no result state on this animator
                    continue;
                }

                var done = info.fullPathHash != resultState || info.normalizedTime >= 1f;
                if (done && elapsed >= minTime)
                    yield break;
            }

            while (elapsed < minTime)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
        }

        // The player passed: she wins and the AI loses.
        private void OnLevelCompleted(Game.Level.LevelResult result) => PlayResult(!IsAI);

        // The player failed: the AI wins only if it actually matched the look better.
        private void OnLevelFailed(Game.Level.LevelResult result) =>
            PlayResult(IsAI && result.HasAi && result.AiScore > result.Score);

        private void PlayResult(bool won)
        {
            if (animator == null || !enabled)
                return;

            var trigger = won ? winParameter : IsAI ? aiLoseParameter : loseParameter;
            var hash = Animator.StringToHash(trigger);
            if (HasParameter(hash, AnimatorControllerParameterType.Trigger))
                animator.SetTrigger(hash);
        }

        // Back to the idle pose at the start line.
        private void OnResetRun()
        {
            if (animator == null || !enabled)
                return;

            animator.Rebind();
            animator.Update(0f);
            _wasGrounded = true;
            _wasSliding = false;
            _wasStumbling = false;
        }

        private void Update()
        {
            if (_hasSpeed)
                animator.SetFloat(_speedHash, _state.NormalizedSpeed);

            if (_hasIsRunning)
                animator.SetBool(_isRunningHash, _state.IsRunning);

            if (_hasRunAnimSpeed)
                animator.SetFloat(_runAnimSpeedHash, RunAnimSpeed());

            var grounded = _state.IsGrounded;
            var sliding = _state.IsSliding;

            if (_hasIsGrounded)
                animator.SetBool(_isGroundedHash, grounded);
            if (_hasIsSliding)
                animator.SetBool(_isSlidingHash, sliding);
            if (_hasJump && _wasGrounded && !grounded)
                animator.SetTrigger(_jumpHash);
            if (_hasSlide && !_wasSliding && sliding)
            {
                UpdateSlideSpeed();
                animator.SetTrigger(_slideHash);
            }

            // Stumble when THIS doll starts stumbling (not on any penalty anywhere, e.g. the AI's).
            var stumbling = _state.IsStumbling;
            if (_hasStumble && !_wasStumbling && stumbling)
                animator.SetTrigger(_stumbleHash);

            _wasGrounded = grounded;
            _wasSliding = sliding;
            _wasStumbling = stumbling;
        }

        /// <summary>Fires a trigger if the Animator has it, e.g. "Roll" or "Die" from a track element.</summary>
        public void PlayTrigger(string trigger)
        {
            if (animator == null || !enabled || string.IsNullOrEmpty(trigger))
                return;

            var hash = Animator.StringToHash(trigger);
            if (HasParameter(hash, AnimatorControllerParameterType.Trigger))
                animator.SetTrigger(hash);
        }

        /// <summary>Sets a bool if the Animator has it, e.g. "Ride", "Swim" or "OnFire".</summary>
        public void SetBool(string parameter, bool value)
        {
            if (animator == null || !enabled || string.IsNullOrEmpty(parameter))
                return;

            var hash = Animator.StringToHash(parameter);
            if (HasParameter(hash, AnimatorControllerParameterType.Bool))
                animator.SetBool(hash, value);
        }

        private bool HasParameter(int hash, AnimatorControllerParameterType type)
        {
            foreach (var parameter in animator.parameters)
            {
                if (parameter.nameHash == hash && parameter.type == type)
                    return true;
            }

            return false;
        }

        private void OnValidate()
        {
            if (stateSource != null && stateSource is not IRunnerState)
            {
                Debug.LogWarning($"'{stateSource.name}' does not implement {nameof(IRunnerState)}.", this);
                stateSource = null;
            }
        }
    }
}
