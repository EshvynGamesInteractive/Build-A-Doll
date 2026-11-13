using System.Collections;
using _Core.UI;
using Core._GameEvent;
using Core.Utilities;
using Game.Level;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    public class GameplayMenuViewBehavior : UIScreen
    {
        [SerializeField] private Button pauseButton;
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text scorePopup;
        [SerializeField, Min(0f)] private float popupDuration = 1.2f;

        private int _score;
        private bool _runStarted;
        private Coroutine _popupTimer;

        private void OnEnable()
        {
            GameEvents.GameplayEvents.CoinsChanged.Register(SetCoins);
            GameEvents.GameplayEvents.ScoreChanged.Register(SetScore);
            GameEvents.GameplayEvents.LevelLoaded.Register(OnLevelLoaded);
            GameEvents.GameplayEvents.ResetRun.Register(OnResetRun);
            GameEvents.GameplayEvents.RunStarted.Register(OnRunStarted);
        }

        private void OnDisable()
        {
            GameEvents.GameplayEvents.CoinsChanged.Unregister(SetCoins);
            GameEvents.GameplayEvents.ScoreChanged.Unregister(SetScore);
            GameEvents.GameplayEvents.LevelLoaded.Unregister(OnLevelLoaded);
            GameEvents.GameplayEvents.ResetRun.Unregister(OnResetRun);
            GameEvents.GameplayEvents.RunStarted.Unregister(OnRunStarted);
            ClearPopup();
        }

        protected override void Initialize()
        {
            UIPanelButtonUtility.Bind(pauseButton, () => GameEvents.UIEvents.ShowByType.Raise(ScreenType.PausePanel));
        }

        public override void Hide()
        {
            ClearPopup();
            base.Hide();
        }

        private void OnLevelLoaded(LevelData level, int levelNumber)
        {
            var runScore = ReferenceManager.Instance?.player?.Score;
            SetCoins(runScore != null ? runScore.Coins : 0);
            SetScore(runScore != null ? runScore.Score : 0);
        }

        private void OnResetRun()
        {
            _runStarted = false;
            ClearPopup();
        }

        private void OnRunStarted() => _runStarted = true;

        private void SetCoins(int coins)
        {
            if (coinsText != null)
                coinsText.text = $"Coins {coins}";
        }

        private void SetScore(int score)
        {
            var delta = score - _score;
            _score = score;
            if (scoreText != null)
                scoreText.text = $"Score {score}";

            if (delta == 0 || !_runStarted || scorePopup == null || !container.activeInHierarchy)
                return;

            ClearPopup();
            scorePopup.text = delta.ToString("+0;-0");
            scorePopup.color = delta > 0 ? new Color32(146, 255, 132, 255) : new Color32(255, 149, 149, 255);
            scorePopup.gameObject.SetActive(true);
            _popupTimer = StartCoroutine(HidePopupAfterDelay());
        }

        private IEnumerator HidePopupAfterDelay()
        {
            yield return new WaitForSeconds(popupDuration);
            scorePopup.gameObject.SetActive(false);
            _popupTimer = null;
        }

        private void ClearPopup()
        {
            if (_popupTimer != null)
            {
                StopCoroutine(_popupTimer);
                _popupTimer = null;
            }

            if (scorePopup != null)
                scorePopup.gameObject.SetActive(false);
        }
    }
}
