using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Goes on the root of a finish stage prefab. Marks where the player and the AI stand on this stage
    /// (and, optionally, where the camera sits), so every stage can have its own layout.
    /// Each spot's blue (forward) arrow is the way the doll faces. Any spot left empty is worked out
    /// automatically, as before. Spots are children of the stage, so they follow its Stage Size resize.
    /// </summary>
    [DisallowMultipleComponent]
    public class FinishStage : MonoBehaviour
    {
        [Tooltip("Where the player stands. Blue arrow = the way she faces.")]
        [SerializeField] private Transform playerSpot;

        [Tooltip("Where the AI stands. Blue arrow = the way she faces.")]
        [SerializeField] private Transform aiSpot;

        [Tooltip("Optional. The camera moves to this exact position and rotation for the comparison. Empty = it frames the two spots automatically.")]
        [SerializeField] private Transform cameraPoint;

        public Transform PlayerSpot => playerSpot;
        public Transform AiSpot => aiSpot;
        public Transform CameraPoint => cameraPoint;

        /// <summary>
        /// Adds PlayerSpot / AiSpot children on top of the stage for any that are empty: player on the left,
        /// AI on the right (seen from the track), both facing back down the track.
        /// </summary>
        [ContextMenu("Create Missing Spots")]
        public void CreateMissingSpots() => CreateMissingSpots(-1f);

        /// <param name="playerSide">-1 = player on the left, +1 = player on the right (seen from the track).</param>
        public void CreateMissingSpots(float playerSide)
        {
            GetLocalBounds(out var min, out var max);
            var centre = (min + max) * 0.5f;
            var offset = (max.x - min.x) * 0.2f;
            var facing = Quaternion.LookRotation(Vector3.back, Vector3.up);

            if (playerSpot == null)
                playerSpot = CreateChild("PlayerSpot", new Vector3(centre.x + playerSide * offset, max.y, centre.z), facing);
            if (aiSpot == null)
                aiSpot = CreateChild("AiSpot", new Vector3(centre.x - playerSide * offset, max.y, centre.z), facing);
        }

        /// <summary>Adds a CameraPoint child in front of the stage, looking at its middle, if there isn't one.</summary>
        [ContextMenu("Create Camera Point")]
        public void CreateCameraPoint()
        {
            if (cameraPoint != null)
                return;

            GetLocalBounds(out var min, out var max);
            var look = new Vector3((min.x + max.x) * 0.5f, max.y + 1.2f, (min.z + max.z) * 0.5f);
            var width = Mathf.Max(max.x - min.x, 2f);
            var position = new Vector3(look.x, max.y + width * 0.35f, min.z - width * 1.1f);
            cameraPoint = CreateChild("CameraPoint", position, Quaternion.LookRotation(look - position, Vector3.up));
        }

        private Transform CreateChild(string childName, Vector3 localPosition, Quaternion localRotation)
        {
            var child = new GameObject(childName).transform;
            child.SetParent(transform, false);
            child.localPosition = localPosition;
            child.localRotation = localRotation;
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(child.gameObject, $"Create {childName}");
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            return child;
        }

        // Bounds of the stage's renderers in its own local space (a 4 x 4 m square if it has none).
        private void GetLocalBounds(out Vector3 min, out Vector3 max)
        {
            min = Vector3.one * float.MaxValue;
            max = Vector3.one * float.MinValue;
            var found = false;

            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                    continue;

                var b = r.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    var local = transform.InverseTransformPoint(corner);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
                found = true;
            }

            if (!found)
            {
                min = new Vector3(-2f, 0f, -2f);
                max = new Vector3(2f, 0f, 2f);
            }
        }

        private void OnDrawGizmos()
        {
            DrawSpot(playerSpot, new Color(0.2f, 0.6f, 1f));
            DrawSpot(aiSpot, new Color(1f, 0.35f, 0.35f));

            if (cameraPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(cameraPoint.position, Vector3.one * 0.4f);
                Gizmos.DrawRay(cameraPoint.position, cameraPoint.forward * 2f);
            }
        }

        private static void DrawSpot(Transform spot, Color color)
        {
            if (spot == null)
                return;

            Gizmos.color = color;
            Gizmos.DrawWireSphere(spot.position + Vector3.up * 0.05f, 0.4f);
            Gizmos.DrawLine(spot.position, spot.position + Vector3.up * 1.6f);
            Gizmos.DrawRay(spot.position + Vector3.up * 0.05f, Vector3.ProjectOnPlane(spot.forward, Vector3.up).normalized * 0.8f);
        }
    }
}
