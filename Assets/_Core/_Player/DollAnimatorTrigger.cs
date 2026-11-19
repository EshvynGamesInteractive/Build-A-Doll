using System.Collections;
using UnityEngine;

namespace Game.Player
{
    public class DollAnimatorTrigger : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string trigger = "Dance";

        [Header("Rotation")]
        [SerializeField] private float rotationDuration = 1.5f;

        private Coroutine rotationCoroutine;

        private void OnEnable()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            rotationCoroutine = StartCoroutine(RotateAndDance());
        }

        private IEnumerator RotateAndDance()
        {
            Quaternion startRotation = transform.rotation;
            float elapsed = 0f;

            while (elapsed < rotationDuration)
            {
                elapsed += Time.deltaTime;

                float progress = Mathf.Clamp01(elapsed / rotationDuration);

                transform.rotation = startRotation *
                                     Quaternion.Euler(0f, 360f * progress, 0f);

                yield return null;
            }

            transform.rotation = startRotation;

            TriggerDanceAnimation();
        }

        private void TriggerDanceAnimation()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return;

            foreach (var parameter in animator.parameters)
            {
                if (parameter.name == trigger &&
                    parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger(trigger);
                    return;
                }
            }
        }

        private void OnDisable()
        {
            if (rotationCoroutine != null)
            {
                StopCoroutine(rotationCoroutine);
                rotationCoroutine = null;
            }
        }
    }
}