using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

namespace Game.CameraRig
{
    /// <summary>
    /// Camera director built on Cinemachine. Sits on the Main Camera (next to its CinemachineBrain) and switches
    /// between three Cinemachine cameras; the Brain blends between them:
    ///   • Run         – follows the player down the track, only partly copying her sideways movement.
    ///   • Stage Walk  – parked at the stage view, turning to keep the walking dolls in frame.
    ///   • Stage       – the exact, settled stage view.
    /// The public API is unchanged (SetTarget / ShowStage / ShowStagePose / FocusStageSpots / WatchGroup / SettleOnStage),
    /// so LevelManager calls it exactly as before. This script never moves the Main Camera itself.
    /// Missing Cinemachine cameras are created at runtime (or once in the editor via Build A Doll/Camera/Setup Cinemachine Cameras).
    /// </summary>
    [DefaultExecutionOrder(-100)] // update the proxy targets before the CinemachineBrain reads them
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        private const int LivePriority = 20;
        private const int IdlePriority = 0;

        [SerializeField] private Transform target;
        [SerializeField] private CameraFollowConfig config;

        [Header("Cinemachine cameras (created automatically when empty)")]
        [SerializeField] private CinemachineCamera runCamera;
        [SerializeField] private CinemachineCamera stageWalkCamera;
        [SerializeField] private CinemachineCamera stageCamera;

        [Header("Ending camera")]
        [Tooltip("Fixed camera placed in the scene over the finish (e.g. 'EndingCam'): live from the finish line until both dolls " +
                 "stand on their stage spots, then the stage view cuts in instantly. Empty = the scene's CinemachineCamera named below.")]
        [SerializeField] private CinemachineCamera endingCamera;
        [SerializeField] private string endingCameraName = "EndingCam";

        private const int EndingPriority = 100; // above the run / stage cameras (20)
        private bool _endingLive;

        private enum Shot { None, Run, StageWalk, Stage }

        private CinemachineBrain _brain;
        private Camera _camera;
        private Shot _live = Shot.None;
        private bool _cutNext;

        // Invisible targets the Cinemachine cameras track; moved by this script every frame.
        private Transform _runFollowTarget;
        private Transform _runLookTarget;
        private Transform _groupLookTarget;

        private readonly Dictionary<Transform, Renderer[]> _rendererCache = new();
        private bool _stageMode;
        private Vector3 _stagePosition;
        private Quaternion _stageRotation;
        private Vector3 _trackOrigin;
        private Quaternion _trackRotation = Quaternion.identity;

        // Dolls the camera keeps looking at while it travels to the stage view (null = look straight at the stage view).
        private Transform[] _group;

        // Final framed view, worked out once from where the dolls will stand (FocusStageSpots).
        private bool _hasFinalPosition;
        private bool _exactPose; // the stage has its own Camera Point: go exactly there
        private Vector3 _finalPosition;
        private bool _settled = true;

        public CinemachineCamera RunCamera => runCamera;
        public CinemachineCamera StageWalkCamera => stageWalkCamera;
        public CinemachineCamera StageCamera => stageCamera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _brain = GetComponent<CinemachineBrain>();
            if (_brain == null)
                _brain = gameObject.AddComponent<CinemachineBrain>();

            EnsureRig();
        }

        private void Start()
        {
            if (target != null)
                SetTarget(target);
            // Otherwise spawned players are handed over later through SetTarget; until then the run camera
            // holds the Main Camera's scene position.
            if (_live == Shot.None)
            {
                _cutNext = true;
                GoLive(Shot.Run);
            }
        }

        /// <summary>Follow a new target (e.g. a freshly spawned player). Snaps straight behind it.</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;

            if (target == null || config == null)
            {
                Debug.LogError($"{nameof(CameraFollow)} on '{name}' needs a target and a config.", this);
                return;
            }

            EnsureRig();
            StopEnding(); // new level / retry: the ending camera is off
            _stageMode = false;
            _group = null;
            _settled = true;
            _hasFinalPosition = false;
            _exactPose = false;
            _trackOrigin = target.position;
            _trackRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(target.forward, Vector3.up), Vector3.up);

            ApplyRunSettings();
            UpdateRunTargets();
            runCamera.PreviousStateIsValid = false; // snap, no damping from the old position
            _cutNext = true;
            GoLive(Shot.Run);
        }

        /// <summary>
        /// Leaves the target and frames the finish stage: looks at 'lookPoint' from in front ('facing' = the way the
        /// dolls face, i.e. towards the camera), far enough back that 'halfWidth' metres either side fit on screen.
        /// SetTarget goes back to following.
        /// </summary>
        public void ShowStage(Vector3 lookPoint, Vector3 facing, float halfWidth, float halfHeight)
        {
            var vHalf = FieldOfView * 0.5f * Mathf.Deg2Rad;
            var hHalf = Mathf.Atan(Mathf.Tan(vHalf) * Aspect);

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

        /// <summary>Finish line crossed: the ending camera goes live at once (cut). Returns false if the scene has none.</summary>
        public bool BeginEnding()
        {
            var cam = FindEndingCamera();
            if (cam == null)
                return false;

            EnsureRig();
            _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            SetPriority(cam, EndingPriority);
            cam.PreviousStateIsValid = false;
            cam.enabled = true;
            _endingLive = true;
            return true;
        }

        /// <summary>Both dolls are on their stage spots: the ending camera goes off and the main camera shows the
        /// stage view (the final framing of both dolls) instantly, no blend.</summary>
        public void EndEnding()
        {
            if (!_endingLive)
                return;

            if (_stageMode && stageCamera != null)
            {
                UpdateStageCameras(); // stage camera already at its final pose
                stageCamera.PreviousStateIsValid = false;
            }

            _cutNext = true;
            if (_stageMode)
                GoLive(_settled ? Shot.Stage : Shot.StageWalk); // stage camera live now (cut), before the ending camera goes off
            _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            StopEnding();
        }

        private void StopEnding()
        {
            _endingLive = false;
            var cam = endingCamera != null ? endingCamera : FindEndingCamera();
            if (cam != null)
                cam.enabled = false;
        }

        private CinemachineCamera FindEndingCamera()
        {
            if (endingCamera != null)
                return endingCamera;
            if (string.IsNullOrEmpty(endingCameraName))
                return null;
            foreach (var cam in FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cam.name == endingCameraName)
                    return endingCamera = cam;
            }
            return null;
        }

        private void BeginStageMove()
        {
            EnsureRig();
            _stageMode = true;
            _settled = _group == null;
        }

        private void LateUpdate()
        {
            if (config == null || runCamera == null)
                return;

            if (_stageMode)
            {
                UpdateStageCameras();
                GoLive(_settled ? Shot.Stage : Shot.StageWalk);
                return;
            }

            if (target != null)
                UpdateRunTargets();
        }

        // ───────────────────────── Cinemachine plumbing ─────────────────────────

        // Makes this camera live; the Brain blends from the previous one using the blend picked here.
        private void GoLive(Shot shot)
        {
            if (shot == _live && !_cutNext)
                return;

            _brain.DefaultBlend = BlendInto(shot);
            _cutNext = false;

            SetPriority(runCamera, shot == Shot.Run ? LivePriority : IdlePriority);
            SetPriority(stageWalkCamera, shot == Shot.StageWalk ? LivePriority : IdlePriority);
            SetPriority(stageCamera, shot == Shot.Stage ? LivePriority : IdlePriority);
            _live = shot;
        }

        // Priority must be explicitly enabled, or Cinemachine treats the camera as priority 0.
        private static void SetPriority(CinemachineCamera cam, int value)
        {
            var priority = cam.Priority;
            priority.Enabled = true;
            priority.Value = value;
            cam.Priority = priority;
        }

        private CinemachineBlendDefinition BlendInto(Shot shot)
        {
            if (_cutNext || shot == Shot.Run || _live == Shot.None)
                return new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);

            // Leaving the run = the travel to the stage (stageMoveTime). Walk -> settled view = stageSmoothTime.
            var time = _live == Shot.Run ? config.StageMoveTime : config.StageSmoothTime;
            return new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, time);
        }

        private void UpdateRunTargets()
        {
            // Position target: her track position with the sideways part scaled down; rotation = track direction,
            // so the Follow offset stays in track space.
            _runFollowTarget.SetPositionAndRotation(GetFollowPoint(Vector3.zero, config.LateralFollow), _trackRotation);
            // Look target follows her sideways more than the camera does, so she stays in frame in the side lanes.
            _runLookTarget.position = GetFollowPoint(config.LookOffset, config.LookLateralFollow);
        }

        private void UpdateStageCameras()
        {
            // Travel straight to the final view (worked out from the dolls' spots), or to the stage view itself.
            var goal = _hasFinalPosition ? _finalPosition : _stagePosition;

            stageCamera.transform.SetPositionAndRotation(goal, _stageRotation);
            stageWalkCamera.transform.position = goal;

            // Walking: look at the dolls' middle. If none are visible, look along the stage view.
            _groupLookTarget.position = TryGetGroupCentre(out var centre)
                ? centre
                : goal + _stageRotation * Vector3.forward * 10f;

            if (stageWalkCamera.TryGetComponent<CinemachineRotationComposer>(out var composer))
            {
                // Same feel as the old exponential turn: ~99% of the way in ln(100) / turnSpeed seconds.
                var damping = 4.6f / Mathf.Max(0.1f, config.GroupTurnSpeed);
                composer.Damping = new Vector2(damping, damping);
            }
        }

        // Pushes the config values into the Run camera's Cinemachine components (the config stays the source of truth).
        private void ApplyRunSettings()
        {
            if (runCamera.TryGetComponent<CinemachineFollow>(out var follow))
            {
                follow.FollowOffset = config.Offset;
                var tracker = follow.TrackerSettings;
                tracker.BindingMode = BindingMode.LockToTarget;
                // SmoothDamp's smoothTime reaches ~99% in about 3x its value; Cinemachine damping is that full time.
                var damping = config.SmoothTime * 3f;
                tracker.PositionDamping = new Vector3(damping, damping, damping);
                tracker.RotationDamping = Vector3.zero;
                follow.TrackerSettings = tracker;
            }
        }

        // Finds or creates the Cinemachine cameras and the proxy targets they track.
        private void EnsureRig()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_brain == null) _brain = GetComponent<CinemachineBrain>();
            if (_brain == null) _brain = gameObject.AddComponent<CinemachineBrain>();

            // Each shot needs its own camera: the same CinemachineCamera in two slots can't do both jobs.
            if (stageWalkCamera == runCamera) stageWalkCamera = null;
            if (stageCamera == runCamera || stageCamera == stageWalkCamera) stageCamera = null;

            if (runCamera != null && stageWalkCamera != null && stageCamera != null && _runFollowTarget != null)
                return;

            var root = runCamera != null ? runCamera.transform.parent
                     : stageCamera != null ? stageCamera.transform.parent : null;
            if (root == null)
                root = new GameObject("CM Camera Rig").transform; // scene root — never under the Main Camera

            var targets = new GameObject("CM Targets (runtime)").transform;
            targets.SetParent(root, false);
            _runFollowTarget = new GameObject("Run Follow Target").transform;
            _runLookTarget = new GameObject("Run Look Target").transform;
            _groupLookTarget = new GameObject("Stage Group Look Target").transform;
            _runFollowTarget.SetParent(targets, false);
            _runLookTarget.SetParent(targets, false);
            _groupLookTarget.SetParent(targets, false);

            if (runCamera == null) runCamera = CreateRunCamera(root, _camera);
            if (stageWalkCamera == null) stageWalkCamera = CreateStageWalkCamera(root, _camera);
            if (stageCamera == null) stageCamera = CreateStageCamera(root, _camera);

            EnsureShotComponents(runCamera, stageWalkCamera);

            runCamera.Follow = _runFollowTarget;
            runCamera.LookAt = _runLookTarget;
            stageWalkCamera.Follow = null;
            stageWalkCamera.LookAt = _groupLookTarget;
            stageCamera.Follow = null;
            stageCamera.LookAt = null;

            // Until a target arrives, the run camera holds where the Main Camera was placed in the scene.
            runCamera.transform.SetPositionAndRotation(transform.position, transform.rotation);
            stageWalkCamera.transform.SetPositionAndRotation(transform.position, transform.rotation);
            stageCamera.transform.SetPositionAndRotation(transform.position, transform.rotation);
            _runFollowTarget.SetPositionAndRotation(transform.position, transform.rotation);
            _runLookTarget.position = transform.position + transform.forward * 10f;
            if (config != null)
                ApplyRunSettings();
        }

        /// <summary>Adds the Cinemachine parts a hand-made camera is missing (Follow + Hard Look At on Run, Rotation Composer on Stage Walk).</summary>
        public static void EnsureShotComponents(CinemachineCamera run, CinemachineCamera walk)
        {
            if (run != null)
            {
                if (!run.TryGetComponent<CinemachineFollow>(out var follow))
                {
                    follow = run.gameObject.AddComponent<CinemachineFollow>();
                    var tracker = follow.TrackerSettings;
                    tracker.BindingMode = BindingMode.LockToTarget;
                    follow.TrackerSettings = tracker;
                }
                if (run.GetComponent<CinemachineHardLookAt>() == null && run.GetComponent<CinemachineRotationComposer>() == null)
                    run.gameObject.AddComponent<CinemachineHardLookAt>();
            }

            if (walk != null && walk.GetComponent<CinemachineRotationComposer>() == null)
                walk.gameObject.AddComponent<CinemachineRotationComposer>().CenterOnActivate = true;
        }

        // Shared by the editor setup tool so scene-authored and runtime-created cameras are identical.
        public static CinemachineCamera CreateRunCamera(Transform parent, Camera lensSource)
        {
            var cam = CreateCinemachineCamera("CM Run", parent, lensSource);
            var follow = cam.gameObject.AddComponent<CinemachineFollow>();
            var tracker = follow.TrackerSettings;
            tracker.BindingMode = BindingMode.LockToTarget;
            follow.TrackerSettings = tracker;
            cam.gameObject.AddComponent<CinemachineHardLookAt>();
            return cam;
        }

        public static CinemachineCamera CreateStageWalkCamera(Transform parent, Camera lensSource)
        {
            var cam = CreateCinemachineCamera("CM Stage Walk", parent, lensSource);
            var composer = cam.gameObject.AddComponent<CinemachineRotationComposer>();
            composer.CenterOnActivate = true; // start already facing the dolls; the blend from the run camera smooths the turn
            return cam;
        }

        public static CinemachineCamera CreateStageCamera(Transform parent, Camera lensSource) =>
            CreateCinemachineCamera("CM Stage", parent, lensSource); // no procedural parts: uses its own transform

        private static CinemachineCamera CreateCinemachineCamera(string name, Transform parent, Camera lensSource)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var cam = go.AddComponent<CinemachineCamera>();
            if (lensSource != null)
                cam.Lens = LensSettings.FromCamera(lensSource); // keep the Main Camera's FOV / clip planes
            SetPriority(cam, IdlePriority);
            return cam;
        }

        // ───────────────────────── Framing maths (unchanged) ─────────────────────────

        private float FieldOfView => stageCamera != null ? stageCamera.Lens.FieldOfView : _camera != null ? _camera.fieldOfView : 60f;
        private float Aspect => _camera != null ? _camera.aspect : 9f / 16f;

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
            var vHalf = FieldOfView * 0.5f * Mathf.Deg2Rad;
            var tanV = Mathf.Max(0.01f, Mathf.Tan(vHalf) * (1f - config.FrameMargin * 2f));
            var tanH = Mathf.Max(0.01f, Mathf.Tan(vHalf) * Aspect * (1f - config.FrameMargin * 2f));

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
            if (dolls == null)
                return false;

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

        // Share of a jump's height the run camera follows (0 = stays at track height, 1 = goes all the way up).
        // Low, so jumps read as jumps and the camera doesn't bob into the hurdles she's jumping over.
        private const float JumpFollow = 0.2f;
        private _Core._Player.RunnerController _targetRunner;
        private Transform _targetRunnerOf;

        private Vector3 GetFollowPoint(Vector3 trackSpaceOffset, float lateralFollow)
        {
            // Work in track space so the sideways part can be scaled down.
            var local = Quaternion.Inverse(_trackRotation) * (target.position - _trackOrigin);
            local.x *= lateralFollow;

            if (_targetRunnerOf != target)
            {
                _targetRunnerOf = target;
                _targetRunner = target != null ? target.GetComponentInParent<_Core._Player.RunnerController>() : null;
            }
            if (_targetRunner != null)
                local.y -= _targetRunner.AirHeight * (1f - JumpFollow);

            return _trackOrigin + _trackRotation * (local + trackSpaceOffset);
        }
    }
}
