using UnityEngine;

namespace Game.CameraRig
{
    /// <summary>
    /// Third-person runner camera. Follows the target down the track and only partly follows
    /// her sideways, so dragging left/right doesn't make the whole view swing.
    /// Track direction is taken from the target's facing when the scene starts.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private CameraFollowConfig config;

        [Header("Finish stage view")]
        [Tooltip("Seconds the camera takes to settle on the stage.")]
        [SerializeField, Min(0.01f)] private float stageSmoothTime = 0.6f;
        [Tooltip("Degrees the camera looks down at the stage.")]
        [SerializeField, Range(0f, 45f)] private float stagePitch = 12f;
        [Tooltip("Extra room around the dolls, as a fraction of the stage view width.")]
        [SerializeField, Range(0f, 2f)] private float stagePadding = 0.5f;
        [Tooltip("Multiplies the stage view distance. Higher = more zoomed out.")]
        [SerializeField, Range(0.5f, 3f)] private float stageZoomOut = 1.3f;

        private Vector3 _velocity;
        private bool _stageMode;
        private Vector3 _stagePosition;
        private Quaternion _stageRotation;
        private Vector3 _trackOrigin;
        private Quaternion _trackRotation;

        private void Start()
        {
            if (target == null)
            {
                // Spawned players are handed over later through SetTarget.
                enabled = false;
                return;
            }

            SetTarget(target);
        }

        /// <summary>Follow a new target (e.g. a freshly spawned player). Snaps straight behind it.</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;

            if (target == null || config == null)
            {
                Debug.LogError($"{nameof(CameraFollow)} on '{name}' needs a target and a config.", this);
                enabled = false;
                return;
            }

            enabled = true;
            _stageMode = false;
            _velocity = Vector3.zero;
            _trackOrigin = target.position;
            _trackRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(target.forward, Vector3.up), Vector3.up);
            transform.position = GetDesiredPosition();
            LookAtTarget();
        }

        /// <summary>
        /// Leaves the target and frames the finish stage: looks at 'lookPoint' from in front ('facing' = the way the
        /// dolls face, i.e. towards the camera), far enough back that 'halfWidth' metres either side fit on screen.
        /// SetTarget goes back to following.
        /// </summary>
        public void ShowStage(Vector3 lookPoint, Vector3 facing, float halfWidth, float halfHeight)
        {
            var cam = GetComponent<Camera>();
            var vHalf = (cam != null ? cam.fieldOfView : 60f) * 0.5f * Mathf.Deg2Rad;
            var aspect = cam != null ? cam.aspect : 9f / 16f;
            var hHalf = Mathf.Atan(Mathf.Tan(vHalf) * aspect);

            var width = halfWidth * (1f + stagePadding);
            var height = halfHeight * (1f + stagePadding);
            var distance = Mathf.Max(width / Mathf.Tan(hHalf), height / Mathf.Tan(vHalf)) * stageZoomOut;

            var forward = Vector3.ProjectOnPlane(facing, Vector3.up).normalized;
            if (forward == Vector3.zero)
                forward = -(_trackRotation * Vector3.forward);

            var lookDirection = Quaternion.AngleAxis(stagePitch, Vector3.Cross(Vector3.up, -forward)) * -forward;
            _stagePosition = lookPoint - lookDirection.normalized * distance;
            _stageRotation = Quaternion.LookRotation(lookPoint - _stagePosition, Vector3.up);
            _stageMode = true;
            _velocity = Vector3.zero;
            enabled = true;
        }

        private void LateUpdate()
        {
            if (_stageMode)
            {
                transform.position = Vector3.SmoothDamp(transform.position, _stagePosition, ref _velocity, stageSmoothTime);
                var t = 1f - Mathf.Exp(-Time.deltaTime * 3f / stageSmoothTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, _stageRotation, t);
                return;
            }

            if (target == null)
                return;

            transform.position = Vector3.SmoothDamp(transform.position, GetDesiredPosition(), ref _velocity, config.SmoothTime);
            LookAtTarget();
        }

        private Vector3 GetDesiredPosition() => GetFollowPoint(config.Offset, config.LateralFollow);

        // The look point follows her sideways more than the camera does, so she stays in frame in the side lanes.
        private void LookAtTarget() => transform.LookAt(GetFollowPoint(config.LookOffset, config.LookLateralFollow), Vector3.up);

        private Vector3 GetFollowPoint(Vector3 trackSpaceOffset, float lateralFollow)
        {
            // Work in track space so the sideways part can be scaled down.
            var local = Quaternion.Inverse(_trackRotation) * (target.position - _trackOrigin);
            local.x *= lateralFollow;
            return _trackOrigin + _trackRotation * (local + trackSpaceOffset);
        }
    }
}
