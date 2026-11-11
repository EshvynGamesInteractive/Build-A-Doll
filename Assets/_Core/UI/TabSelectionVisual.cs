using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core.UI
{
    public class TabSelectionVisual : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text text;

        [Header("Selected State")]
        [SerializeField] private Sprite selectedBackgroundSprite;
        [SerializeField] private Color selectedTextColor = Color.white;
        [SerializeField] private Vector3 selectedScale = Vector3.one;

        [Header("Unselected State")]
        [SerializeField] private Sprite unselectedBackgroundSprite;
        [SerializeField] private Color unselectedTextColor = Color.gray;
        [SerializeField] private Vector3 unselectedScale = Vector3.one;
    

        public void SetSelected(bool selected)
        {
            if (background != null)
                background.sprite = selected
                    ? selectedBackgroundSprite
                    : unselectedBackgroundSprite;

            if (text != null)
                text.color = selected
                    ? selectedTextColor
                    : unselectedTextColor;

            transform.localScale = selected
                ? selectedScale
                : unselectedScale;
        }
    }
}