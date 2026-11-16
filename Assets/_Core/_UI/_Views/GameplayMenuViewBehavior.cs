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
    public class GameplayMenuViewBehavior : UIScreen
    {
        [SerializeField] private Button pauseButton;
        [SerializeField] private TMP_Text coinsText;

        [Tooltip("Shows +N / -N when coins are gained or lost during the run.")]
        [FormerlySerializedAs("scorePopup")]
        [SerializeField] private TMP_Text coinsPopup;
        [SerializeField, Min(0f)] private float popupDuration = 1.2f;

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
        }

        private void OnDisable()
        {
            GameEvents.GameplayEvents.CoinsChanged.Unregister(OnRunCoinsChanged);
            GameEvents.GameplayEvents.WalletChanged.Unregister(OnWalletChanged);
            GameEvents.GameplayEvents.LevelLoaded.Unregister(OnLevelLoaded);
            GameEvents.GameplayEvents.ResetRun.Unregister(OnResetRun);
            GameEvents.GameplayEvents.RunStarted.Unregister(OnRunStarted);
            ClearPopup();
        }

        protected override void Initialize()
        {
            UIPanelButtonUtility.Bind(pauseButton, () => GameEvents.UIEvents.ShowByType.Raise(ScreenType.PausePanel));
            RefreshCoins();
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
        }

        private void OnResetRun()
        {
            _runStarted = false;
            _runCoins = 0;
            ClearPopup();
            RefreshCoins();
        }

        private void OnRunStarted() => _runStarted = true;

        private void OnWalletChanged(int _) => RefreshCoins();

        // 'coins' is the coins gained this run; the label always shows the saved wallet.
        private void OnRunCoinsChanged(int coins)
        {
            var delta = coins - _runCoins;
            _runCoins = coins;
            RefreshCoins();

            if (delta == 0 || !_runStarted || coinsPopup == null || !container.activeInHierarchy)
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
                coinsText.text = $"Coins {LevelProgress.TotalCoins}";
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
