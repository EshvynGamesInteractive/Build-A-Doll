using UnityEngine;
using UnityEngine.UI;

namespace Core.UI
{
    public class TabItem : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private GameObject content;
        [SerializeField] private TabSelectionVisual visual;

        public Button Button => button;

        public void SetSelected(bool selected)
        {
            if (content != null)
                content.SetActive(selected);

            if (visual != null)
                visual.SetSelected(selected);
        }
    }
}