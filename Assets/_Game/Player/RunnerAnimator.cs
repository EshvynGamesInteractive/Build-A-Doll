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

        private IRunnerState _state;
        private int _speedHash;
        private int _isRunningHash;
        private bool _hasSpeed;
        private bool _hasIsRunning;

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
        }

        private void Update()
        {
            if (_hasSpeed)
                animator.SetFloat(_speedHash, _state.NormalizedSpeed);

            if (_hasIsRunning)
                animator.SetBool(_isRunningHash, _state.IsRunning);
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
