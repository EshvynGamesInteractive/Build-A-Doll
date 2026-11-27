using _Core.UI;
using Core._GameEvent;
using Core.Utilities;
using Game.Level;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace _Core._UI._Views
{
    public class GameplayMenuViewBehavior : UIScreen
    {
        [SerializeField] private Button pauseButton;
        [SerializeField] private GameObject targetThumb;
        [SerializeField] private TMP_Text penaltyToast;
        [SerializeField] private GameObject penaltyToastObject;
        [SerializeField, Min(0f)] private float penaltyToastDuration = 3f;
        
        private bool runStarted;
        private Coroutine popupTimer;
        private CanvasGroup interactionGroup;
        private Coroutine penaltyToastTimer;

        public GameplayMenuViewBehavior(bool runStarted)
        {
            this.runStarted = runStarted;
        }


        private void OnEnable()
        {
            GameEvents.GameplayEvents.PenaltyTaken.Register(OnPenaltyTaken);
            GameEvents.GameplayEvents.LevelLoaded.Register(OnLevelLoaded);
            GameEvents.GameplayEvents.ResetRun.Register(OnResetRun);
            GameEvents.GameplayEvents.RunStarted.Register(OnRunStarted);
            GameEvents.GameplayEvents.TargetLookIntroDone.Register(OnTargetLookIntroDone);
            GameEvents.GameplayEvents.ComparisonStarted.Register(OnComparisonStarted);
            GameEvents.GameplayEvents.InputBlocked.Register(OnInputBlocked);
        }

        private void OnDisable()
        {
            GameEvents.GameplayEvents.PenaltyTaken.Unregister(OnPenaltyTaken);
            HidePenaltyToast();
            GameEvents.GameplayEvents.LevelLoaded.Unregister(OnLevelLoaded);
            GameEvents.GameplayEvents.ResetRun.Unregister(OnResetRun);
            GameEvents.GameplayEvents.RunStarted.Unregister(OnRunStarted);
            GameEvents.GameplayEvents.TargetLookIntroDone.Unregister(OnTargetLookIntroDone);
            GameEvents.GameplayEvents.ComparisonStarted.Unregister(OnComparisonStarted);
            GameEvents.GameplayEvents.InputBlocked.Unregister(OnInputBlocked);
        }

        protected override void Initialize()
        {
            UIPanelButtonUtility.Bind(pauseButton, () => GameEvents.UIEvents.ShowByType.Raise(ScreenType.PausePanel));
            HidePenaltyToast();
            SetThumb(false);
        }

        private void OnLevelLoaded(LevelData level, int levelNumber)
        {
            HidePenaltyToast();
            SetThumb(false);
        }

        private void OnPenaltyTaken(int coins)
        {
            if (container == null || !container.activeInHierarchy)
                return;
            
            penaltyToastObject.SetActive(true);  
            penaltyToast.text = $"-{coins} coins";
            penaltyToastTimer = StartCoroutine(HidePenaltyToastAfterDelay());
        }

        private IEnumerator HidePenaltyToastAfterDelay()
        {
            yield return new WaitForSecondsRealtime(penaltyToastDuration);
            penaltyToastTimer = null;
            HidePenaltyToast();
        }

        private void HidePenaltyToast()
        {
            if (penaltyToastTimer != null)
                StopCoroutine(penaltyToastTimer);
            
            penaltyToastTimer = null;
            penaltyToastObject.SetActive(false);
        }

        private void OnResetRun()
        {
            HidePenaltyToast();
            OnInputBlocked(false);
            runStarted = false;
            SetThumb(false);
        }

        // The card on the TargetLookPanel has flown to the corner: the HUD's copy takes over from here.
        private void OnTargetLookIntroDone() => SetThumb(true);

        // Nothing left to film once the comparison starts.
        private void OnComparisonStarted(Game.Outfit.OutfitMatch player, Game.Outfit.OutfitMatch ai) => SetThumb(false);

        private void SetThumb(bool visible)
        {
            if (targetThumb != null && targetThumb.activeSelf != visible)
                targetThumb.SetActive(visible);
        }

        private void OnRunStarted() => runStarted = true;

        // No pausing during the finish sequence (the ComparisonPanel's blocker also catches every touch).
        private void OnInputBlocked(bool blocked)
        {
            if (container != null)
            {
                if (interactionGroup == null)
                    interactionGroup = container.GetComponent<CanvasGroup>();
                // Unity's missing/destroyed objects require its overloaded null comparison, not ??.
                if (interactionGroup == null)
                    interactionGroup = container.AddComponent<CanvasGroup>();
                interactionGroup.interactable = !blocked;
                interactionGroup.blocksRaycasts = !blocked;
            }
            if (pauseButton != null)
                pauseButton.interactable = !blocked;
        }
    }
}
