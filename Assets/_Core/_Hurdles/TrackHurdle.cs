using _Core._Player;
using Core._GameEvent;
using Game.Player;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// Base for anything on the track the doll runs through: item gates, money, obstacles.
    /// Detects the player with a trigger collider, fires once, and comes back on ResetRun.
    /// Subclasses only decide what happens to the player in OnPlayerHit.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class TrackHurdle : MonoBehaviour
    {
        [Tooltip("Shown while the hurdle can still be hit. Leave empty to keep everything visible.")]
        [SerializeField] private GameObject activeVisual;

        [SerializeField] private GameObject redVisual;
        [SerializeField] private GameObject greenVisual;

        [Tooltip("Optional effect spawned where the player hit it.")]
        [SerializeField] private ParticleSystem hitEffect;

        [SerializeField] private AudioClip hitSound;

        private Collider _trigger;
        private HurdleGroup _group;

        public bool IsUsed { get; private set; }

        protected virtual void Awake()
        {
            _trigger = GetComponent<Collider>();
            _trigger.isTrigger = true;

            // Trigger events need a Rigidbody on one side; a kinematic one costs nothing.
            if (!TryGetComponent<Rigidbody>(out var body))
                body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            _group = GetComponentInParent<HurdleGroup>();
        }

        protected virtual void OnEnable() => GameEvents.GameplayEvents.ResetRun.Register(ResetHurdle);

        protected virtual void OnDisable() => GameEvents.GameplayEvents.ResetRun.Unregister(ResetHurdle);

        private void OnTriggerEnter(Collider other)
        {
            if (IsUsed)
                return;

            var player = other.GetComponentInParent<RunnerController>();
            if (player == null)
                return;

            if (_group != null && !_group.TryClaim(this))
                return;

            Use();
            OnPlayerHit(player);

            if (hitEffect != null)
                Instantiate(hitEffect, other.ClosestPoint(transform.position), Quaternion.identity).Play();

            if (hitSound != null)
                GameEvents.AudioEvents.PlaySfx.Raise(hitSound);
        }

        /// <summary>What this hurdle does to the player.</summary>
        protected abstract void OnPlayerHit(RunnerController player);

        /// <summary>Turns the hurdle off without effect, e.g. when the other gate of a pair was taken.</summary>
        public void Use()
        {
            IsUsed = true;
            _trigger.enabled = false;
            if (activeVisual != null)
                activeVisual.SetActive(false);
        }

        public virtual void ResetHurdle()
        {
            IsUsed = false;
            _trigger.enabled = true;
            if (activeVisual != null)
                activeVisual.SetActive(true);
        }
    }
}
