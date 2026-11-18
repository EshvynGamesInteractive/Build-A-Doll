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

        [Header("Target look intro")]
        [Tooltip("The demo image (holds the RawImage that shows the demo doll's render texture). Where it's placed in the prefab is where it pops up at level start.")]
        [SerializeField] private RectTransform demoImage;
        [Tooltip("Where the demo image goes for the run (e.g. top right). Its width sets the final size.")]
        [SerializeField] private RectTransform demoTarget;
        [Tooltip("Seconds to pop in.")]
        [SerializeField, Min(0f)] private float demoPopTime = 0.25f;
        [Tooltip("Seconds it stays big in the middle.")]
        [SerializeField, Min(0f)] private float demoHoldTime = 2f;
        [Tooltip("Seconds to move to the target. The run starts when it arrives.")]
        [SerializeField, Min(0f)] private float demoMoveTime = 0.6f;

        [Header("Finish comparison")]
        [Tooltip("Labels over the dolls on the finish stage + touch blocker (set up in this prefab).")]
        [SerializeField] private ComparisonHud comparisonHud;

        private int _runCoins;
        private bool _runStarted;
        private Coroutine _popupTimer;
        private Coroutine _demoIntro;
        private bool _demoStartSaved;
        private Vector2 _demoStartPosition;
        private Vector3 _demoStartScale;

        private void OnEnable()
        {
            GameEvents.GameplayEvents.CoinsChanged.Register(OnRunCoinsChanged);
            GameEvents.GameplayEvents.WalletChanged.Register(OnWalletChanged);
            GameEvents.GameplayEvents.LevelLoaded.Register(OnLevelLoaded);
            GameEvents.GameplayEvents.ResetRun.Register(OnResetRun);
            GameEvents.GameplayEvents.RunStarted.Register(OnRunStarted);
            GameEvents.GameplayEvents.TargetLookShown.Register(OnTargetLookShown);
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
            GameEvents.GameplayEvents.TargetLookShown.Unregister(OnTargetLookShown);
            GameEvents.GameplayEvents.ComparisonStarted.Unregister(OnComparisonStarted);
            GameEvents.GameplayEvents.InputBlocked.Unregister(OnInputBlocked);
            ClearPopup();
            StopDemoIntro(false);
        }

        protected override void Initialize()
        {
            UIPanelButtonUtility.Bind(pauseButton, () => GameEvents.UIEvents.ShowByType.Raise(ScreenType.PausePanel));
            if (comparisonHud == null)
                Debug.LogWarning($"{name}: no {nameof(ComparisonHud)} assigned, so the finish comparison won't show labels or block input.", this);
            RefreshCoins();
            SaveDemoStart();
            if (demoImage != null)
                demoImage.gameObject.SetActive(false);
        }

        public override void Hide()
        {
            ClearPopup();
            StopDemoIntro(false);
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
            StopDemoIntro(false);
        }

        // ---------- Target look intro ----------

        // Level start: pop the demo image in where it sits in the prefab, hold, slide it to the target, then start the run.
        private void OnTargetLookShown(LevelData level)
        {
            StopDemoIntro(false);
            if (demoImage == null || !isActiveAndEnabled)
            {
                GameEvents.GameplayEvents.TargetLookIntroDone.Raise();
                return;
            }

            _demoIntro = StartCoroutine(PlayDemoIntro());
        }

        // The comparison starts: the demo image container goes away (back to its start place, disabled, ready for the next level).
        private void OnComparisonStarted(Game.Outfit.OutfitMatch player, Game.Outfit.OutfitMatch ai) => StopDemoIntro(false);

        private IEnumerator PlayDemoIntro()
        {
            SaveDemoStart();
            demoImage.anchoredPosition = _demoStartPosition;
            demoImage.localScale = Vector3.zero;
            demoImage.gameObject.SetActive(true);

            yield return Animate(demoPopTime, t => demoImage.localScale = _demoStartScale * EaseOutBack(t));
            yield return new WaitForSeconds(demoHoldTime);

            if (demoTarget != null)
            {
                var fromPosition = demoImage.position;
                var fromScale = demoImage.localScale;
                GetDemoTarget(out var toPosition, out var toScale);
                yield return Animate(demoMoveTime, t =>
                {
                    var e = Mathf.SmoothStep(0f, 1f, t);
                    demoImage.position = Vector3.Lerp(fromPosition, toPosition, e);
                    demoImage.localScale = Vector3.Lerp(fromScale, toScale, e);
                });
            }

            _demoIntro = null;
            GameEvents.GameplayEvents.TargetLookIntroDone.Raise();
        }

        // Where the image's pivot lands when its rect lines up with the target's, and the scale that matches the target's width.
        private void GetDemoTarget(out Vector3 position, out Vector3 scale)
        {
            var target = demoTarget.rect;
            var pivot = demoImage.pivot;
            position = demoTarget.TransformPoint(new Vector3(target.x + target.width * pivot.x, target.y + target.height * pivot.y, 0f));

            var imageWidth = demoImage.rect.width * demoImage.lossyScale.x;
            var targetWidth = target.width * demoTarget.lossyScale.x;
            scale = imageWidth > 0.0001f ? demoImage.localScale * (targetWidth / imageWidth) : demoImage.localScale;
        }

        private void SaveDemoStart()
        {
            if (_demoStartSaved || demoImage == null)
                return;

            _demoStartSaved = true;
            _demoStartPosition = demoImage.anchoredPosition;
            _demoStartScale = demoImage.localScale;
        }

        // Stops the intro and hides the image (back at its start place, ready for the next level).
        private void StopDemoIntro(bool keepVisible)
        {
            if (_demoIntro != null)
            {
                StopCoroutine(_demoIntro);
                _demoIntro = null;
            }

            if (demoImage == null || keepVisible || !_demoStartSaved)
                return;

            demoImage.anchoredPosition = _demoStartPosition;
            demoImage.localScale = _demoStartScale;
            demoImage.gameObject.SetActive(false);
        }

        private static IEnumerator Animate(float duration, System.Action<float> step)
        {
            if (duration <= 0f)
            {
                step(1f);
                yield break;
            }

            for (var t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / duration);
                step(t);
                yield return null;
            }
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            var p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        private void OnRunStarted() => _runStarted = true;

        // No pausing during the finish sequence (the HUD's blocker also catches every touch).
        private void OnInputBlocked(bool blocked)
        {
            if (pauseButton != null)
                pauseButton.interactable = !blocked;
        }

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
