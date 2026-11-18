using _Core._Player;
using Core._GameEvent;
using Game.Outfit;
using UnityEngine;

namespace _Core._UI._Views
{
    /// <summary>
    /// Finish-stage comparison UI. Uses UI objects made in the prefab (nothing is created from code):
    /// a ComparisonLabel for the player and one for the AI, kept over their heads, and an input blocker
    /// (a full-screen transparent Image with Raycast Target on) shown while GameplayEvents.InputBlocked is on.
    /// Put it on the GameplayMenu screen and assign it there.
    /// </summary>
    public class ComparisonHud : MonoBehaviour
    {
        [SerializeField] private ComparisonLabel playerLabel;
        [SerializeField] private ComparisonLabel aiLabel;

        [Tooltip("Full-screen transparent Image (Raycast Target on), last child of the screen so it covers the buttons. Starts disabled.")]
        [SerializeField] private GameObject inputBlocker;

        [Tooltip("Metres above the top of the doll's head.")]
        [SerializeField] private float headPadding = 0.35f;

        private Canvas _canvas;

        private void Awake()
        {
            if (inputBlocker != null)
                inputBlocker.SetActive(false);
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

            Place(playerLabel, cam);
            Place(aiLabel, cam);
        }

        // Keeps the label's pivot just above the doll's head; hidden while she's behind the camera.
        private void Place(ComparisonLabel label, Camera cam)
        {
            if (label == null || label.Target == null)
                return;

            if (_canvas == null)
            {
                _canvas = label.GetComponentInParent<Canvas>();
                if (_canvas != null)
                    _canvas = _canvas.rootCanvas;
            }

            var world = label.Target.position + Vector3.up * (label.HeadHeight + headPadding);
            var screen = cam.WorldToScreenPoint(world);
            var visible = screen.z > 0f;
            if (label.gameObject.activeSelf != visible)
                label.gameObject.SetActive(visible);
            if (!visible)
                return;

            var uiCamera = _canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            var parent = label.Rect.parent as RectTransform;
            if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out var local))
                label.Rect.localPosition = local;
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
