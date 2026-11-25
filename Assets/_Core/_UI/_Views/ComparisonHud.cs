using _Core._Player;
using Core._GameEvent;
using Game.Outfit;
using UnityEngine;

namespace _Core._UI._Views
{
    /// <summary>
    /// Finish-stage comparison UI. Uses UI objects made in the prefab (nothing is created from code):
    /// player / target-look / AI frames aligned above the dolls' heads, and an input blocker
    /// (a full-screen transparent Image with Raycast Target on) shown while GameplayEvents.InputBlocked is on.
    /// Put it on the ComparisonPanel screen and assign its prefab objects there.
    /// </summary>
    public class ComparisonHud : MonoBehaviour
    {
        [SerializeField] private ComparisonLabel playerLabel;
        [SerializeField] private ComparisonLabel aiLabel;
        [Tooltip("Target-look frame between the player and AI, using the demo render texture.")]
        [SerializeField] private RectTransform targetFrame;
        [SerializeField, Min(0f)] private float horizontalGap = 16f;

        [Tooltip("Full-screen transparent Image (Raycast Target on), last child of the screen so it covers the buttons. Starts disabled.")]
        [SerializeField] private GameObject inputBlocker;

        [Tooltip("Metres above the top of the doll's head.")]
        [SerializeField] private float headPadding = 0.35f;

        private Canvas _canvas;

        private void Awake()
        {
            if (inputBlocker != null)
                inputBlocker.SetActive(false);
            if (targetFrame != null)
                targetFrame.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.GameplayEvents.StageComparisonStarted.Register(OnStageComparisonStarted);
            GameEvents.GameplayEvents.ComparisonItemShown.Register(OnItemShown);
            GameEvents.GameplayEvents.ComparisonEnded.Register(HideLabels);
            GameEvents.GameplayEvents.ResetRun.Register(HideLabels);
            GameEvents.GameplayEvents.InputBlocked.Register(SetBlocked);
        }

        private void OnDisable()
        {
            GameEvents.GameplayEvents.StageComparisonStarted.Unregister(OnStageComparisonStarted);
            GameEvents.GameplayEvents.ComparisonItemShown.Unregister(OnItemShown);
            GameEvents.GameplayEvents.ComparisonEnded.Unregister(HideLabels);
            GameEvents.GameplayEvents.ResetRun.Unregister(HideLabels);
            GameEvents.GameplayEvents.InputBlocked.Unregister(SetBlocked);
        }

        private void OnStageComparisonStarted(RunnerController player, RunnerController ai)
        {
            if (playerLabel != null)
                playerLabel.Attach(player != null ? player.transform : null, HeadHeight(player));
            if (aiLabel != null)
                aiLabel.Attach(ai != null ? ai.transform : null, HeadHeight(ai));
            if (targetFrame != null)
                targetFrame.gameObject.SetActive(player != null);
            LateUpdate();
        }

        private void OnItemShown(ComparisonStep step)
        {
            if (playerLabel != null)
                playerLabel.ShowStep(step.PlayerItem, step.Target, step.PlayerMatched, step.PlayerPercent);
            if (step.HasAi && aiLabel != null)
                aiLabel.ShowStep(step.AiItem, step.Target, step.AiMatched, step.AiPercent);
        }

        private void HideLabels()
        {
            if (targetFrame != null)
                targetFrame.gameObject.SetActive(false);
            if (playerLabel != null)
                playerLabel.Detach();
            if (aiLabel != null)
                aiLabel.Detach();
        }

        private void SetBlocked(bool blocked)
        {
            if (inputBlocker != null)
                inputBlocker.SetActive(blocked);
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null)
                return;

            var parent = playerLabel != null ? playerLabel.Rect.parent as RectTransform : null;
            if (parent == null)
                return;

            if (_canvas == null)
                _canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            var uiCamera = _canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : _canvas.worldCamera;
            var hasPlayer = HeadPoint(playerLabel, cam, parent, uiCamera, out var playerPoint);
            var hasAi = HeadPoint(aiLabel, cam, parent, uiCamera, out var aiPoint);
            if (targetFrame != null)
                targetFrame.gameObject.SetActive(hasPlayer);
            if (!hasPlayer)
            {
                if (hasAi)
                    Place(aiLabel.Rect, parent, aiPoint);
                return;
            }

            // A shared screen height keeps different doll heights / stage depths from staggering the UI.
            var height = hasAi ? Mathf.Max(playerPoint.y, aiPoint.y) : playerPoint.y;
            var frameWidth = targetFrame != null ? targetFrame.rect.width * targetFrame.localScale.x : 0f;
            var playerWidth = playerLabel.Rect.rect.width * playerLabel.Rect.localScale.x;
            var rowHeight = playerLabel.Rect.rect.height * playerLabel.Rect.localScale.y;
            if (targetFrame != null)
                rowHeight = Mathf.Max(rowHeight, targetFrame.rect.height * targetFrame.localScale.y);
            if (hasAi)
                rowHeight = Mathf.Max(rowHeight, aiLabel.Rect.rect.height * aiLabel.Rect.localScale.y);
            height = Mathf.Clamp(height, parent.rect.yMin + 24f,
                Mathf.Max(parent.rect.yMin + 24f, parent.rect.yMax - rowHeight - 24f));
            if (hasAi)
            {
                var aiWidth = aiLabel.Rect.rect.width * aiLabel.Rect.localScale.x;
                var middle = (playerPoint.x + aiPoint.x) * 0.5f;
                var leftGap = (playerWidth + frameWidth) * 0.5f + horizontalGap;
                var rightGap = (aiWidth + frameWidth) * 0.5f + horizontalGap;
                middle = Mathf.Clamp(middle, parent.rect.xMin + leftGap + playerWidth * 0.5f + 16f,
                    parent.rect.xMax - rightGap - aiWidth * 0.5f - 16f);
                // Keep the target between both labels, with enough space for the three frames.
                playerPoint.x = middle - leftGap;
                aiPoint.x = middle + rightGap;
                Place(aiLabel.Rect, parent, new Vector2(aiPoint.x, height));
                if (targetFrame != null)
                    Place(targetFrame, parent, new Vector2(middle, height));
            }
            else if (targetFrame != null)
            {
                playerPoint.x = Mathf.Clamp(playerPoint.x, parent.rect.xMin + playerWidth * 0.5f + 16f,
                    parent.rect.xMax - playerWidth * 0.5f - frameWidth - horizontalGap - 16f);
                // Solo comparisons still show the target next to the player.
                Place(targetFrame, parent, new Vector2(playerPoint.x +
                    (playerWidth + frameWidth) * 0.5f + horizontalGap, height));
            }
            Place(playerLabel.Rect, parent, new Vector2(playerPoint.x, height));
        }

        private bool HeadPoint(ComparisonLabel label, Camera cam, RectTransform parent,
            Camera uiCamera, out Vector2 point)
        {
            point = default;
            if (label == null || label.Target == null)
                return false;

            var world = label.Target.position + Vector3.up * (label.HeadHeight + headPadding);
            var screen = cam.WorldToScreenPoint(world);
            var visible = screen.z > 0f;
            if (label.gameObject.activeSelf != visible)
                label.gameObject.SetActive(visible);
            return visible && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out point);
        }

        private static void Place(RectTransform rect, RectTransform parent, Vector2 point)
        {
            rect.position = parent.TransformPoint(new Vector3(point.x, point.y, 0f));
        }

        // How far above her feet the top of her head is.
        private static float HeadHeight(RunnerController runner)
        {
            if (runner == null)
                return 0f;

            var top = float.NegativeInfinity;
            foreach (var r in runner.GetComponentsInChildren<Renderer>())
            {
                if (r.enabled && r.gameObject.activeInHierarchy && r is not ParticleSystemRenderer)
                    top = Mathf.Max(top, r.bounds.max.y);
            }

            var height = float.IsInfinity(top) ? 3.3f : top - runner.transform.position.y;
            return Mathf.Clamp(height, 0.5f, 8f);
        }
    }
}
