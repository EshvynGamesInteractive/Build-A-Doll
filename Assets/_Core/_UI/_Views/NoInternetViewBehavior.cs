using _Core.UI;
using Core._GameEvent;
using Core.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    /// <summary>
    /// "No internet" overlay, opened by InternetMonitor over the current screen (game paused).
    /// OK checks the connection again: back online → the panel closes; still offline → it stays (button shakes).
    /// It also closes by itself when the monitor sees the connection come back.
    /// </summary>
    public class NoInternetViewBehavior : UIScreen
    {
        [SerializeField] private Button okayButton;

        private Coroutine _shake;

        protected override void Initialize()
        {
            base.Initialize();
            UIPanelButtonUtility.Bind(okayButton, OnOkay);
        }

        private void OnOkay()
        {
            if (okayButton != null)
                okayButton.interactable = false;
            GameEvents.UIEvents.RetryInternet.Raise();
        }

        /// <summary>Called by InternetMonitor when the retry finished and she's still offline.</summary>
        public void RetryFailed()
        {
            if (okayButton == null)
                return;
            okayButton.interactable = true;
            if (_shake != null)
                StopCoroutine(_shake);
            _shake = StartCoroutine(Shake(okayButton.transform));
        }

        public override void Show()
        {
            transform.SetAsLastSibling(); // overlay: draw over the screen it opens on
            base.Show();
            if (okayButton != null)
                okayButton.interactable = true;
        }

        private static System.Collections.IEnumerator Shake(Transform t)
        {
            var start = t.localPosition;
            for (var time = 0f; time < 0.35f; time += Time.unscaledDeltaTime)
            {
                t.localPosition = start + Vector3.right * (Mathf.Sin(time * 60f) * 12f * (1f - time / 0.35f));
                yield return null;
            }
            t.localPosition = start;
        }
    }
}
