using System.Collections;
using _Core.UI;
using Core._GameEvent;
using Core.Utilities;
using Game.Level;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    /// <summary>
    /// Run HUD only: pause, coins, +N/-N popup and the small target-look picture in the corner.
    /// The other gameplay screens are their own panels (25 Nov 2026):
    /// TargetLookPanel (level start card + PLAY), MakeupPanel (paint tool), ComparisonPanel (finish labels + blocker).
    /// </summary>
    public class GameplayMenuViewBehavior : UIScreen
    {
        [SerializeField] private Button pauseButton;

        [Tooltip("Optional. Usually empty: the CoinBar prefab keeps its own number up to date (CoinBarView).")]
        [SerializeField] private TMP_Text coinsText;
        [Tooltip("{0} = wallet coins. Only used for Coins Text.")]
        [SerializeField] private string coinsFormat = "{0}";

        [Tooltip("Shows +N / -N when coins are gained or lost during the run.")]
        [FormerlySerializedAs("scorePopup")]
        [SerializeField] private TMP_Text coinsPopup;
        [SerializeField, Min(0f)] private float popupDuration = 1.2f;

        [Header("Target look picture")]
        [Tooltip("Small copy of the target look (RawImage with the demo render texture) in the corner. " +
                 "Shown once the TargetLookPanel's card has flown here (PLAY pressed), hidden for the finish comparison.")]
        [SerializeField] private GameObject targetThumb;

        private int _runCoins;
        private bool _runStarted;
        private Coroutine _popupTimer;

        private void OnEnable()
        {
            GameEvents.GameplayEvents.CoinsChanged.Register(OnRunCoinsChanged);
            GameEvents.GameplayEvents.WalletChanged.Register(OnWalletChanged);
            GameEvents.GameplayEvents.LevelLoaded.Register(OnLevelLoaded);
            GameEvents.GameplayEvents.ResetRun.Register(OnResetRun);
            GameEvents.GameplayEvents.RunStarted.Register(OnRunStarted);
            GameEvents.GameplayEvents.TargetLookIntroDone.Register(OnTargetLookIntroDone);
            GameEvents.GameplayEvents.ComparisonStarted.Register(OnComparisonStarted);
            GameEvents.GameplayEvents.InputBlocked.Register(OnInputBlocked);
        }

        private void OnDisable()
        {
            GameEvents.GameplayEvents.CoinsChanged.Unregister(OnRunCoinsChanged);
            GameEvents.GameplayEvents.WalletChanged.Unregister(OnWalletChanged);
            GameEvents.GameplayEvents.LevelLoaded.Unregister(OnLevelLoaded);
            GameEvents.GameplayEvents.ResetRun.Unregister(OnResetRun);
            GameEvents.GameplayEvents.RunStarted.Unregister(OnRunStarted);
            GameEvents.GameplayEvents.TargetLookIntroDone.Unregister(OnTargetLookIntroDone);
            GameEvents.GameplayEvents.ComparisonStarted.Unregister(OnComparisonStarted);
            GameEvents.GameplayEvents.InputBlocked.Unregister(OnInputBlocked);
            ClearPopup();
        }

        protected override void Initialize()
        {
            UIPanelButtonUtility.Bind(pauseButton, () => GameEvents.UIEvents.ShowByType.Raise(ScreenType.PausePanel));
            RefreshCoins();
            SetThumb(false);
        }

        public override void Hide()
        {
            ClearPopup();
            base.Hide();
        }

        private void OnLevelLoaded(LevelData level, int levelNumber)
        {
            var runScore = ReferenceManager.Instance?.player?.Score;
            _runCoins = runScore != null ? runScore.Coins : 0;
            RefreshCoins();
            SetThumb(false);
        }

        private void OnResetRun()
        {
            _runStarted = false;
            _runCoins = 0;
            ClearPopup();
            RefreshCoins();
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

        private void OnRunStarted() => _runStarted = true;

        // No pausing during the finish sequence (the ComparisonPanel's blocker also catches every touch).
        private void OnInputBlocked(bool blocked)
        {
            if (pauseButton != null)
                pauseButton.interactable = !blocked;
        }

        private void OnWalletChanged(int _) => RefreshCoins();

        // 'coins' is the coins gained this run; the coin bar always shows the saved wallet.
        private void OnRunCoinsChanged(int coins)
        {
            var delta = coins - _runCoins;
            _runCoins = coins;
            RefreshCoins();

            if (delta == 0 || !_runStarted || coinsPopup == null || container == null || !container.activeInHierarchy)
                return;

            ClearPopup();
            coinsPopup.text = delta.ToString("+0;-0");
            coinsPopup.color = delta > 0 ? new Color32(146, 255, 132, 255) : new Color32(255, 149, 149, 255);
            coinsPopup.gameObject.SetActive(true);
            _popupTimer = StartCoroutine(HidePopupAfterDelay());
        }

        private void RefreshCoins()
        {
            if (coinsText != null)
                coinsText.text = string.Format(coinsFormat, LevelProgress.TotalCoins);
        }

        private IEnumerator HidePopupAfterDelay()
        {
            yield return new WaitForSeconds(popupDuration);
            coinsPopup.gameObject.SetActive(false);
            _popupTimer = null;
        }

        private void ClearPopup()
        {
            if (_popupTimer != null)
            {
                StopCoroutine(_popupTimer);
                _popupTimer = null;
            }

            if (coinsPopup != null)
                coinsPopup.gameObject.SetActive(false);
        }
    }
}
