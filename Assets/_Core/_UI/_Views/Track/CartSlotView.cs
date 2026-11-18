using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    /// <summary>One slot of the shopping cart or the wanted list. Its icon and marks are set up in the prefab.</summary>
    public class CartSlotView : MonoBehaviour
    {
        public enum Mark
        {
            None,
            Correct,
            Wrong
        }

        [SerializeField] private Image icon;
        [SerializeField] private GameObject correctMark;
        [SerializeField] private GameObject wrongMark;

        [Tooltip("Shown while the slot is empty.")]
        [SerializeField] private GameObject emptyState;

        public void Show(Sprite sprite, Mark mark)
        {
            gameObject.SetActive(true);
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            SetMark(mark);
            if (emptyState != null)
                emptyState.SetActive(false);
        }

        public void SetEmpty()
        {
            gameObject.SetActive(true);
            if (icon != null)
                icon.enabled = false;

            SetMark(Mark.None);
            if (emptyState != null)
                emptyState.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private void SetMark(Mark mark)
        {
            if (correctMark != null)
                correctMark.SetActive(mark == Mark.Correct);
            if (wrongMark != null)
                wrongMark.SetActive(mark == Mark.Wrong);
        }
    }
}
