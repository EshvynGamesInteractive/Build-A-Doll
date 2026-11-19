using UnityEngine;

namespace Game.CameraRig
{
    [CreateAssetMenu(fileName = "CameraFollowConfig", menuName = "Build A Doll/Camera/Camera Follow Config")]
    public class CameraFollowConfig : ScriptableObject
    {
        [Tooltip("Camera position relative to the target, in the target's track space (x = right, z = forward).")]
        [SerializeField] private Vector3 offset = new(0f, 6f, -7f);

        [Tooltip("Point the camera looks at, relative to the target.")]
        [SerializeField] private Vector3 lookOffset = new(0f, 1f, 3f);

        [Tooltip("How much of the target's sideways movement the camera copies. 0 = stays centred, 1 = fully follows.")]
        [SerializeField, Range(0f, 1f)] private float lateralFollow = 0.4f;

        [Tooltip("How much of the target's sideways movement the LOOK point copies. Higher than Lateral Follow, so the camera turns to keep her in view on narrow (portrait) screens while its position stays steady. 1 = always looks straight at her.")]
        [SerializeField, Range(0f, 1f)] private float lookLateralFollow = 0.8f;

        [Tooltip("How loosely the run camera trails her (seconds). Fed into the CM Run camera's Follow damping (x3).")]
        [SerializeField, Min(0f)] private float smoothTime = 0.12f;

        [Header("Finish stage view")]
        [Tooltip("Seconds of the Cinemachine blend from the walking view into the settled stage view.")]
        [SerializeField, Min(0.01f)] private float stageSmoothTime = 0.6f;
        [Tooltip("Degrees the camera looks down at the stage.")]
        [SerializeField, Range(0f, 45f)] private float stagePitch = 12f;
        [Tooltip("Extra room around the dolls, as a fraction of the stage view width (stages without a Camera Point).")]
        [SerializeField, Range(0f, 2f)] private float stagePadding = 0.5f;
        [Tooltip("Multiplies the stage view distance (stages without a Camera Point). Higher = more zoomed out.")]
        [SerializeField, Range(0.5f, 3f)] private float stageZoomOut = 1.3f;

        [Header("Walk to the stage")]
        [Tooltip("Seconds of the Cinemachine blend from the run camera to the stage view while the dolls walk. Higher = slower, smoother move.")]
        [SerializeField, Min(0.01f)] private float stageMoveTime = 1.4f;
        [Tooltip("While the dolls walk the camera keeps looking at them: this high above their feet (metres). Used only when Fit Dolls In View is off.")]
        [SerializeField, Min(0f)] private float groupLookHeight = 1.2f;
        [Tooltip("How quickly the camera turns to keep the walking dolls in view. Higher = snappier.")]
        [SerializeField, Min(0.1f)] private float groupTurnSpeed = 4f;

        [Header("Keep both dolls in frame")]
        [Tooltip("Stages WITHOUT a Camera Point: the camera moves to where both dolls, standing on their spots, fully fit on screen, centred. Stages with a Camera Point always use it exactly.")]
        [SerializeField] private bool fitDollsInView = true;
        [Tooltip("Empty border kept around the dolls, as a fraction of the screen (0.15 = 15% each side).")]
        [SerializeField, Range(0f, 0.45f)] private float frameMargin = 0.15f;
        [Tooltip("Closest the camera may get to the dolls on the stage view (metres).")]
        [SerializeField, Min(0.5f)] private float minStageDistance = 4f;

        public Vector3 Offset => offset;
        public Vector3 LookOffset => lookOffset;
        public float LateralFollow => lateralFollow;
        public float LookLateralFollow => lookLateralFollow;
        public float SmoothTime => smoothTime;

        public float StageSmoothTime => stageSmoothTime;
        public float StagePitch => stagePitch;
        public float StagePadding => stagePadding;
        public float StageZoomOut => stageZoomOut;
        public float StageMoveTime => stageMoveTime;
        public float GroupLookHeight => groupLookHeight;
        public float GroupTurnSpeed => groupTurnSpeed;
        public bool FitDollsInView => fitDollsInView;
        public float FrameMargin => frameMargin;
        public float MinStageDistance => minStageDistance;
    }
}
