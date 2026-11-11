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
        private Vector3 _trackOrigin;
        private Quaternion _trackRotation;

        private void Start()
        {
            if (target == null || config == null)
            {
                Debug.LogError($"{nameof(CameraFollow)} on '{name}' needs a target and a config.", this);
                enabled = false;
                return;
            }

            _trackOrigin = target.position;
            _trackRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(target.forward, Vector3.up), Vector3.up);
            transform.position = GetDesiredPosition();
            LookAtTarget();
        }

        private void LateUpdate()
        {
            transform.position = Vector3.SmoothDamp(transform.position, GetDesiredPosition(), ref _velocity, config.SmoothTime);
            LookAtTarget();
        }

        private Vector3 GetDesiredPosition() => GetFollowPoint(config.Offset);

        private void LookAtTarget() => transform.LookAt(GetFollowPoint(config.LookOffset));

        private Vector3 GetFollowPoint(Vector3 trackSpaceOffset)
        {
            // Work in track space so the sideways part can be scaled down.
            var local = Quaternion.Inverse(_trackRotation) * (target.position - _trackOrigin);
            local.x *= config.LateralFollow;
            return _trackOrigin + _trackRotation * (local + trackSpaceOffset);
        }
    }
}
