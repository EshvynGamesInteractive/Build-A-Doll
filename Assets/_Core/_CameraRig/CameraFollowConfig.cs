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

        [SerializeField, Min(0f)] private float smoothTime = 0.12f;

        public Vector3 Offset => offset;
        public Vector3 LookOffset => lookOffset;
        public float LateralFollow => lateralFollow;
        public float SmoothTime => smoothTime;
    }
}
