using _Core.UI;
using Core.Utilities;
using Game.Level;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    /// <summary>
    /// Outfit flow: Home-screen Gift Box. Since 24 Nov 2026 Skip on Level Complete is final, so the box stays empty
    /// and this view stays hidden. Kept so the HomeMenu prefab reference doesn't break.
    /// UI is built in the HomeMenu prefab and assigned here (nothing is created from code).
    /// </summary>
    public class GiftBoxView : MonoBehaviour
    {
        [Tooltip("Shown only while the Gift Box holds an outfit.")]
        [SerializeField] private GameObject root;
        [SerializeField] private Button button;
        [Tooltip("Optional: number of outfits waiting.")]
        [SerializeField] private TMP_Text countText;
        [Tooltip("Optional: picture of the outfit that will be claimed.")]
        [SerializeField] private Image icon;
        [Tooltip("Optional: name of the outfit that will be claimed. {0} = outfit name.")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private string titleFormat = "Claim {0}!";

        private void Awake()
        {
            if (button != null)
                UIPanelButtonUtility.Bind(button, OnClick);
        }

        private void OnEnable()
        {
            OutfitFlow.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => OutfitFlow.Changed -= Refresh;

        private void Refresh()
        {
            var database = FindDatabase();
            var box = OutfitFlow.GiftBox;
            var show = database != null && database.UseOutfitFlow && box.Count > 0;
            if (root != null)
                root.SetActive(show);
            if (!show)
                return;

            var reward = database.GetReward(box[0]);
            if (countText != null)
                countText.text = box.Count.ToString();
            if (icon != null)
            {
                icon.sprite = reward != null ? reward.Icon : null;
                icon.enabled = icon.sprite != null;
            }
            if (titleText != null)
                titleText.text = string.Format(titleFormat, reward != null ? reward.Title : "outfit");
        }

        private void OnClick()
        {
            var box = OutfitFlow.GiftBox;
            if (box.Count > 0)
                OutfitFlow.ClaimFromGiftBox(box[0]);
        }

        private static LevelDatabase FindDatabase()
        {
            var manager = ReferenceManager.Instance != null ? ReferenceManager.Instance.levelManager : null;
            if (manager == null)
                manager = FindFirstObjectByType<_Core._Level.LevelManager>();
            return manager != null ? manager.Database : null;
        }
    }
}
