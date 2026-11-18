using _Core._Player;
using Core._GameEvent;
using UnityEngine;

namespace Game.Hurdles.Effects
{
    /// <summary>
    /// Crate: swaps the intact model for broken pieces that fly off. Pieces are Rigidbodies under
    /// 'Broken' (off in the prefab); they go back together on ResetRun. Leave the hurdle's Active Visual
    /// empty so the pieces stay visible.
    /// </summary>
    public class BreakApartEffect : MonoBehaviour, IRunnerEffect
    {
        [SerializeField] private GameObject intact;
        [SerializeField] private GameObject broken;
        [SerializeField, Min(0f)] private float pieceForce = 4f;

        private Rigidbody[] _pieces;
        private Vector3[] _positions;
        private Quaternion[] _rotations;

        private void Awake()
        {
            if (broken == null)
                return;

            _pieces = broken.GetComponentsInChildren<Rigidbody>(true);
            _positions = new Vector3[_pieces.Length];
            _rotations = new Quaternion[_pieces.Length];
            for (var i = 0; i < _pieces.Length; i++)
            {
                _positions[i] = _pieces[i].transform.localPosition;
                _rotations[i] = _pieces[i].transform.localRotation;
            }

            Restore();
        }

        private void OnEnable() => GameEvents.GameplayEvents.ResetRun.Register(Restore);

        private void OnDisable() => GameEvents.GameplayEvents.ResetRun.Unregister(Restore);

        public void Apply(RunnerController runner)
        {
            if (intact != null)
                intact.SetActive(false);
            if (broken == null)
                return;

            broken.SetActive(true);
            var from = runner.transform.position;
            foreach (var piece in _pieces)
            {
                piece.isKinematic = false;
                var away = (piece.position - from).normalized + Vector3.up;
                piece.AddForce(away * pieceForce, ForceMode.Impulse);
            }
        }

        private void Restore()
        {
            if (intact != null)
                intact.SetActive(true);
            if (broken == null || _pieces == null)
                return;

            for (var i = 0; i < _pieces.Length; i++)
            {
                var piece = _pieces[i];
                if (!piece.isKinematic)
                {
                    piece.linearVelocity = Vector3.zero;
                    piece.angularVelocity = Vector3.zero;
                }

                piece.isKinematic = true;
                piece.transform.SetLocalPositionAndRotation(_positions[i], _rotations[i]);
            }

            broken.SetActive(false);
        }
    }
}
