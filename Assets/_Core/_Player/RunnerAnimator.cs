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
        }

        private void Update()
        {
            if (_hasSpeed)
                animator.SetFloat(_speedHash, _state.NormalizedSpeed);

            if (_hasIsRunning)
                animator.SetBool(_isRunningHash, _state.IsRunning);

            var grounded = _state.IsGrounded;
            var sliding = _state.IsSliding;

            if (_hasIsGrounded)
                animator.SetBool(_isGroundedHash, grounded);
            if (_hasIsSliding)
                animator.SetBool(_isSlidingHash, sliding);
            if (_hasJump && _wasGrounded && !grounded)
                animator.SetTrigger(_jumpHash);
            if (_hasSlide && !_wasSliding && sliding)
                animator.SetTrigger(_slideHash);

            // Stumble when THIS doll starts stumbling (not on any penalty anywhere, e.g. the AI's).
            var stumbling = _state.IsStumbling;
            if (_hasStumble && !_wasStumbling && stumbling)
                animator.SetTrigger(_stumbleHash);

            _wasGrounded = grounded;
            _wasSliding = sliding;
            _wasStumbling = stumbling;
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
