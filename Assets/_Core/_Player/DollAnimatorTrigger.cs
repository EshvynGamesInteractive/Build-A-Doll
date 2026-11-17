using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Fires an Animator trigger when this object is enabled, e.g. makes the target-look demo doll
    /// dance while the player studies the look.
    /// </summary>
    public class DollAnimatorTrigger : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string trigger = "Dance";

        private void OnEnable()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (animator == null || animator.runtimeAnimatorController == null)
                return;

            foreach (var parameter in animator.parameters)
            {
                if (parameter.name == trigger && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger(trigger);
                    return;
                }
            }
        }
    }
}
