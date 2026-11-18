using _Core._Player;
using Core._GameEvent;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// Cannon: she stops in it while the barrel swings; the HUD's "Fire!" button (CannonFireButton)
    /// launches her down the track. Fired at the middle of the swing = the longest shot.
    /// The AI fires by itself after a short wait.
    /// </summary>
    public class CannonSpinner : TrackHurdle
    {
        [Header("Aim")]
        [Tooltip("The part that swings while waiting for Fire! (the barrel).")]
        [SerializeField] private Transform barrel;
        [SerializeField] private Vector3 swingAxis = Vector3.right;
        [SerializeField, Range(0f, 80f)] private float swingAngle = 30f;
        [Tooltip("Full swings per second.")]
        [SerializeField, Min(0.05f)] private float swingSpeed = 0.8f;

        [Header("Launch")]
        [Tooltip("Shot length at the edge of the swing.")]
        [SerializeField, Min(0f)] private float minDistance = 12f;
        [Tooltip("Shot length at the middle of the swing.")]
        [SerializeField, Min(0f)] private float maxDistance = 30f;
        [SerializeField, Min(0f)] private float launchHeight = 6f;
        [SerializeField, Min(0.1f)] private float flightTime = 1.6f;

        [Tooltip("Seconds before firing by itself (AI, or no Fire button in the HUD).")]
        [SerializeField, Min(0f)] private float autoFireDelay = 0.6f;

        [SerializeField] private ParticleSystem fireEffect;
        [SerializeField] private AudioClip fireSound;

        private const float TwoPi = Mathf.PI * 2f;

        private RunnerController _runner;
        private Quaternion _barrelRest;
        private float _time;

        /// <summary>True while a runner sits in the cannon.</summary>
        public bool IsWaiting => _runner != null;

        /// <summary>0..1: how good a shot it would be right now (1 at the middle of the swing).</summary>
        public float Power => 1f - Mathf.Abs(Mathf.Sin(_time * swingSpeed * TwoPi));

        protected override void Awake()
        {
            base.Awake();
            if (barrel != null)
                _barrelRest = barrel.localRotation;
        }

        private void Update()
        {
            if (_runner == null)
                return;

            _time += Time.deltaTime;
            if (barrel != null)
                barrel.localRotation = _barrelRest * Quaternion.AngleAxis(swingAngle * Mathf.Sin(_time * swingSpeed * TwoPi), swingAxis);
        }

        protected override void OnPlayerHit(RunnerController player)
        {
            _runner = player;
            _time = 0f;
            player.Hold(this);

            if (player.IsAI || !HudPresenter<CannonSpinner>.TryPresent(this))
                Invoke(nameof(Fire), autoFireDelay);
        }

        /// <summary>Launches the runner sitting in the cannon. Called by the HUD's Fire! button.</summary>
        public void Fire()
        {
            if (_runner == null)
                return;

            CancelInvoke(nameof(Fire));
            var runner = _runner;
            var distance = Mathf.Lerp(minDistance, maxDistance, Power);
            _runner = null;

            runner.Release(this);
            runner.Launch(distance, launchHeight, flightTime);

            if (fireEffect != null)
                fireEffect.Play();
            if (!runner.IsAI && fireSound != null)
                GameEvents.AudioEvents.PlaySfx.Raise(fireSound);
        }

        public override void ResetHurdle()
        {
            base.ResetHurdle();
            CancelInvoke(nameof(Fire));
            _runner = null;
            _time = 0f;
            if (barrel != null)
                barrel.localRotation = _barrelRest;
        }
    }
}
