using _Core.UI;
using Core._GameEvent;
using Core.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    /// <summary>
    /// Privacy policy overlay.
    /// First launch: shown by the BootLoader after the splash and must be accepted (Close hidden) before Home.
    /// Later: opened from Settings (privacy button); Accept / Close return to Settings.
    /// Visit opens the policy page (GameConstants.PrivacyPolicyUrl).
    /// </summary>
    public class PrivacyPolicyViewBehavior : UIScreen
    {
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button visitButton;
        [Tooltip("Optional. Hidden until the policy has been accepted once.")]
        [SerializeField] private Button closeButton;

        public static bool IsAccepted => PlayerPrefs.GetInt(GameConstants.PrivacyAcceptedKey, 0) == 1;

        protected override void Initialize()
        {
            base.Initialize();
            if (closeButton == null && container != null)
            {
                var close = container.transform.Find("CloseButton");
                if (close == null)
                    foreach (var b in container.GetComponentsInChildren<Button>(true))
                        if (b.name == "CloseButton") { close = b.transform; break; }
                if (close != null)
                    closeButton = close.GetComponent<Button>();
            }

            UIPanelButtonUtility.Bind(acceptButton, OnAccept);
            UIPanelButtonUtility.Bind(visitButton, OnVisit);
            UIPanelButtonUtility.Bind(closeButton, OnClose);
            RefreshClose();
        }

        public override void Show()
        {
            transform.SetAsLastSibling(); // overlay: draw over the screen it opens on
            base.Show();
            RefreshClose();
        }

        private void RefreshClose()
        {
            if (closeButton != null)
                closeButton.gameObject.SetActive(IsAccepted);
        }

        private void OnAccept()
        {
            var first = !IsAccepted;
            PlayerPrefs.SetInt(GameConstants.PrivacyAcceptedKey, 1);
            PlayerPrefs.Save();
            if (first)
                GameEvents.UIEvents.PrivacyAccepted.Raise();
            GameEvents.UIEvents.CloseOverlay.Raise();
        }

        private void OnClose()
        {
            if (IsAccepted)
                GameEvents.UIEvents.CloseOverlay.Raise();
        }

        private static void OnVisit() => Application.OpenURL(GameConstants.PrivacyPolicyUrl);
    }
}
