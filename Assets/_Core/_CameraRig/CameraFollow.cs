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

        [Header("Walk to the stage")]
        [Tooltip("Seconds the camera takes to travel from the finish line to its stage view while the dolls walk. Higher = slower, smoother move.")]
        [SerializeField, Min(0.01f)] private float stageMoveTime = 1.4f;
        [Tooltip("While the dolls walk the camera keeps looking at them: this high above their feet (metres).")]
        [SerializeField, Min(0f)] private float groupLookHeight = 1.2f;
        [Tooltip("How quickly the camera turns to keep the walking dolls in view. Higher = snappier.")]
        [SerializeField, Min(0.1f)] private float groupTurnSpeed = 4f;

        private Vector3 _velocity;
        private bool _stageMode;
        private Vector3 _stagePosition;
        private Quaternion _stageRotation;
        private Vector3 _trackOrigin;
        private Quaternion _trackRotation;

        // Dolls the camera keeps looking at while it travels to the stage view (null = look straight at the stage view).
        private Transform[] _group;
        private bool _settled = true;

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
            _group = null;
            _settled = true;
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
            BeginStageMove();
        }

        /// <summary>Leaves the target and moves to an exact position / rotation (a stage's Camera Point). SetTarget goes back to following.</summary>
        public void ShowStagePose(Vector3 position, Quaternion rotation)
        {
            _stagePosition = position;
            _stageRotation = Quaternion.LookRotation(rotation * Vector3.forward, Vector3.up); // no roll
            BeginStageMove();
        }

        /// <summary>
        /// While travelling to the stage view, keep these dolls in frame (the camera looks at their middle).
        /// Call again to change who's watched, e.g. when the AI starts walking too.
        /// </summary>
        public void WatchGroup(params Transform[] dolls)
        {
            _group = dolls;
            _settled = false;
        }

        /// <summary>The dolls are on their spots: stop watching them and ease into the exact stage view.</summary>
        public void SettleOnStage()
        {
            _group = null;
            _settled = true;
        }

        private void BeginStageMove()
        {
            // Keep the current velocity so leaving the follow view doesn't jerk.
            _stageMode = true;
            _settled = _group == null;
            enabled = true;
        }

        private void LateUpdate()
        {
            if (_stageMode)
            {
                var dt = Time.deltaTime;
                var moveTime = _settled ? stageSmoothTime : stageMoveTime;
                transform.position = Vector3.SmoothDamp(transform.position, _stagePosition, ref _velocity, moveTime);

                // Walking: look at the dolls. Arrived: turn to the stage view itself.
                var wanted = _stageRotation;
                float turn;
                if (!_settled && TryGetGroupCentre(out var centre))
                {
                    var toGroup = centre - transform.position;
                    if (toGroup.sqrMagnitude > 0.01f)
                        wanted = Quaternion.LookRotation(toGroup, Vector3.up);
                    turn = 1f - Mathf.Exp(-dt * groupTurnSpeed);
                }
                else
                {
                    turn = 1f - Mathf.Exp(-dt * 3f / stageSmoothTime);
                }

                transform.rotation = Quaternion.Slerp(transform.rotation, wanted, turn);
                return;
            }

            if (target == null)
                return;

            transform.position = Vector3.SmoothDamp(transform.position, GetDesiredPosition(), ref _velocity, config.SmoothTime);
            LookAtTarget();
        }

        // Middle of the watched dolls, at look height.
        private bool TryGetGroupCentre(out Vector3 centre)
        {
            centre = Vector3.zero;
            if (_group == null)
                return false;

            var count = 0;
            foreach (var doll in _group)
            {
                if (doll == null || !doll.gameObject.activeInHierarchy)
                    continue;
                centre += doll.position;
                count++;
            }

            if (count == 0)
                return false;

            centre = centre / count + Vector3.up * groupLookHeight;
            return true;
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
