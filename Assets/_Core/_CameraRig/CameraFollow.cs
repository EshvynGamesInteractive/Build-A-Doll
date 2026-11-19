using System.Collections.Generic;
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

        private Vector3 _velocity;
        private readonly Dictionary<Transform, Renderer[]> _rendererCache = new();
        private bool _stageMode;
        private Vector3 _stagePosition;
        private Quaternion _stageRotation;
        private Vector3 _trackOrigin;
        private Quaternion _trackRotation;

        // Dolls the camera keeps looking at while it travels to the stage view (null = look straight at the stage view).
        private Transform[] _group;

        // Final framed view, worked out once from where the dolls will stand (FocusStageSpots).
        private bool _hasFinalPosition;
        private bool _exactPose; // the stage has its own Camera Point: go exactly there
        private Vector3 _finalPosition;
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
            _hasFinalPosition = false;
            _exactPose = false;
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

            var width = halfWidth * (1f + config.StagePadding);
            var height = halfHeight * (1f + config.StagePadding);
            var distance = Mathf.Max(width / Mathf.Tan(hHalf), height / Mathf.Tan(vHalf)) * config.StageZoomOut;

            var forward = Vector3.ProjectOnPlane(facing, Vector3.up).normalized;
            if (forward == Vector3.zero)
                forward = -(_trackRotation * Vector3.forward);

            var lookDirection = Quaternion.AngleAxis(config.StagePitch, Vector3.Cross(Vector3.up, -forward)) * -forward;
            _stagePosition = lookPoint - lookDirection.normalized * distance;
            _stageRotation = Quaternion.LookRotation(lookPoint - _stagePosition, Vector3.up);
            _exactPose = false;
            BeginStageMove();
        }

        /// <summary>Leaves the target and moves to an exact position / rotation (a stage's Camera Point). SetTarget goes back to following.</summary>
        public void ShowStagePose(Vector3 position, Quaternion rotation)
        {
            _stagePosition = position;
            _stageRotation = Quaternion.LookRotation(rotation * Vector3.forward, Vector3.up); // no roll
            _exactPose = true;
            _hasFinalPosition = false;
            BeginStageMove();
        }

        /// <summary>
        /// Call after ShowStage / ShowStagePose with the spots the dolls will stand on. Works out the final view
        /// (both dolls centred) right away, so the camera travels straight to it while they walk.
        /// 'dolls' are only used to measure their size (any null = a 0.6 x 1.8 m doll).
        /// </summary>
        public void FocusStageSpots(Vector3[] spots, params Transform[] dolls)
        {
            _hasFinalPosition = false;
            if (_exactPose || config == null || !config.FitDollsInView || spots == null || spots.Length == 0)
                return;

            _rendererCache.Clear();
            var size = new Vector3(0.6f, 1.8f, 0.6f);
            if (dolls != null && TryGetGroupBounds(dolls, out var measured, singleDollSize: true))
                size = measured.size;

            var bounds = new Bounds(spots[0] + Vector3.up * size.y * 0.5f, size);
            for (var i = 1; i < spots.Length; i++)
                bounds.Encapsulate(new Bounds(spots[i] + Vector3.up * size.y * 0.5f, size));

            _finalPosition = FitBounds(bounds);
            _hasFinalPosition = true;
        }

        /// <summary>
        /// While travelling to the stage view, keep these dolls in frame (the camera looks at their middle).
        /// Call again to change who's watched, e.g. when the AI starts walking too.
        /// </summary>
        public void WatchGroup(params Transform[] dolls)
        {
            _group = dolls;
            _settled = false;
            _rendererCache.Clear(); // outfits change between levels, so look their renderers up again
        }

        /// <summary>The dolls are on their spots: stop watching them and ease into the exact stage view.</summary>
        public void SettleOnStage()
        {
            if (_hasFinalPosition)
                _stagePosition = _finalPosition;
            else if (!_exactPose && config.FitDollsInView && TryGetGroupBounds(_group, out var bounds))
                _stagePosition = FitBounds(bounds);

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
                var moveTime = _settled ? config.StageSmoothTime : config.StageMoveTime;

                // Travel straight to the final view (worked out from the dolls' spots), or to the stage view itself.
                var goal = _hasFinalPosition ? _finalPosition : _stagePosition;

                transform.position = Vector3.SmoothDamp(transform.position, goal, ref _velocity, moveTime);

                // Walking: look at the dolls. Arrived: turn to the stage view itself.
                var wanted = _stageRotation;
                float turn;
                if (!_settled && TryGetGroupCentre(out var centre))
                {
                    var toGroup = centre - transform.position;
                    if (toGroup.sqrMagnitude > 0.01f)
                        wanted = Quaternion.LookRotation(toGroup, Vector3.up);
                    turn = 1f - Mathf.Exp(-dt * config.GroupTurnSpeed);
                }
                else
                {
                    turn = 1f - Mathf.Exp(-dt * 3f / config.StageSmoothTime);
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

            centre = centre / count + Vector3.up * config.GroupLookHeight;
            if (config.FitDollsInView && TryGetGroupBounds(_group, out var bounds))
                centre = bounds.center;
            return true;
        }

        // Camera position along the stage view's direction (_stageRotation) that fits 'bounds' on screen with both
        // dolls centred (no extra zoom-out). Never closer than the stage view itself (e.g. a Camera Point) or MinStageDistance.
        private Vector3 FitBounds(Bounds bounds)
        {
            var cam = GetComponent<Camera>();
            var vHalf = (cam != null ? cam.fieldOfView : 60f) * 0.5f * Mathf.Deg2Rad;
            var aspect = cam != null ? cam.aspect : 9f / 16f;
            var tanV = Mathf.Max(0.01f, Mathf.Tan(vHalf) * (1f - config.FrameMargin * 2f));
            var tanH = Mathf.Max(0.01f, Mathf.Tan(vHalf) * aspect * (1f - config.FrameMargin * 2f));

            var forward = _stageRotation * Vector3.forward;
            var inverse = Quaternion.Inverse(_stageRotation);
            var centre = bounds.center;

            // Distance from the centre so that each corner of the bounds is inside the view.
            var fit = 0f;
            var min = bounds.min;
            var max = bounds.max;
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                var local = inverse * (corner - centre);
                fit = Mathf.Max(fit, Mathf.Max(Mathf.Abs(local.x) / tanH, Mathf.Abs(local.y) / tanV) - local.z);
            }

            var viewDistance = Vector3.Dot(centre - _stagePosition, forward);
            var distance = Mathf.Max(fit, viewDistance, config.MinStageDistance);
            return centre - forward * distance;
        }

        // Combined renderer bounds of the active dolls.
        private bool TryGetGroupBounds(Transform[] dolls, out Bounds bounds, bool singleDollSize = false)
        {
            bounds = default;
            var found = false;
            foreach (var doll in dolls)
            {
                if (doll == null || !doll.gameObject.activeInHierarchy)
                    continue;

                if (!_rendererCache.TryGetValue(doll, out var renderers) || renderers == null)
                {
                    renderers = doll.GetComponentsInChildren<Renderer>(true);
                    _rendererCache[doll] = renderers;
                }

                var dollFound = false;
                foreach (var r in renderers)
                {
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy ||
                        r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                        continue;

                    if (!found) { bounds = r.bounds; found = true; }
                    else bounds.Encapsulate(r.bounds);
                    dollFound = true;
                }

                if (!dollFound)
                {
                    // No renderers: a 0.6 x 1.8 m box standing on her position.
                    var box = new Bounds(doll.position + Vector3.up * 0.9f, new Vector3(0.6f, 1.8f, 0.6f));
                    if (!found) { bounds = box; found = true; }
                    else bounds.Encapsulate(box);
                }

                if (singleDollSize && found)
                    return true; // just the first doll: her size is all that's wanted
            }
            return found;
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
