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
        [Tooltip("The part of the card that moves to the corner for the run (e.g. IconFrame with the doll image). Empty = the whole card.")]
        [SerializeField] private RectTransform demoFrame;
        [Tooltip("The card's own background (e.g. DemoImageContainer's Image). Hidden when the frame leaves the card, back for the next level.")]
        [SerializeField] private Graphic cardBackground;
        [Tooltip("Where the demo frame goes for the run (e.g. top right). Its width sets the final size.")]
        [SerializeField] private RectTransform demoTarget;
        [Tooltip("Seconds to pop in.")]
        [SerializeField, Min(0f)] private float demoPopTime = 0.25f;
        [Tooltip("Seconds it stays big in the middle. Only used when there's no Start Button.")]
        [SerializeField, Min(0f)] private float demoHoldTime = 2f;

        [Tooltip("PLAY! button on the target-look card. The card stays up until it's pressed; then it moves to the corner and the run starts. Empty = wait Demo Hold Time instead.")]
        [SerializeField] private Button startButton;
        [Tooltip("Optional settings button on the target-look card. Opens Settings; closing it comes back to the card.")]
        [SerializeField] private Button introSettingsButton;
        [Tooltip("Shown only while the card waits for PLAY! (e.g. the \"Dress Her Like This!\" title, the PLAY! button).")]
        [SerializeField] private GameObject[] introOnlyObjects;
        [Tooltip("Hidden while the card waits for PLAY!, shown for the run (e.g. Pause, coins).")]
        [SerializeField] private GameObject[] runOnlyObjects;
        [Tooltip("Title on the target-look card (e.g. \"DRESS HER LIKE THIS!\"). Shows the level's Brief (\"Design a NURSE outfit!\") when it has one, its own text otherwise.")]
        [SerializeField] private TMP_Text briefText;
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
        private bool _startPressed;
        private Vector2 _frameStartPosition;
        private Vector3 _frameStartScale;

        // What moves to the corner: the frame if set, else the whole card.
        private RectTransform Mover => demoFrame != null ? demoFrame : demoImage;
        private bool _waitingForStart;
        private string _defaultBrief;

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
            UIPanelButtonUtility.Bind(startButton, () => _startPressed = true);
            UIPanelButtonUtility.Bind(introSettingsButton, () => GameEvents.UIEvents.ShowByType.Raise(ScreenType.Settings));
            RefreshCoins();
            if (briefText != null && _defaultBrief == null)
                _defaultBrief = briefText.text;
            SaveDemoStart();
            if (demoImage != null)
                demoImage.gameObject.SetActive(false);
            SetIntroMode(false);
        }

        public override void Hide()
        {
            ClearPopup();

            // Settings opened from the card: keep the card waiting for PLAY!, it's there again when Settings closes.
            if (!_waitingForStart)
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

        // Theme line for this level ("Design a NURSE outfit!"), or the card's own title.
        private void ShowBrief(LevelData level)
        {
            if (briefText == null)
                return;
            if (_defaultBrief == null)
                _defaultBrief = briefText.text;
            briefText.text = level != null && !string.IsNullOrEmpty(level.Brief) ? level.Brief : _defaultBrief;
        }

        // Level start: pop the demo image in where it sits in the prefab, hold, slide it to the target, then start the run.
        private void OnTargetLookShown(LevelData level)
        {
            ShowBrief(level);
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
            ResetCard();
            demoImage.localScale = Vector3.zero;
            demoImage.gameObject.SetActive(true);

            yield return Animate(demoPopTime, t => demoImage.localScale = _demoStartScale * EaseOutBack(t));

            if (startButton != null)
            {
                // Wait for PLAY! on the card.
                _startPressed = false;
                _waitingForStart = true;
                SetIntroMode(true);
                while (!_startPressed)
                    yield return null;
                _waitingForStart = false;
                SetIntroMode(false);
            }
            else
            {
                yield return new WaitForSeconds(demoHoldTime);
            }

            // Only the frame leaves: the card's background goes, the frame slides to the corner.
            if (demoFrame != null && cardBackground != null)
                cardBackground.enabled = false;

            if (demoTarget != null)
            {
                var mover = Mover;
                var fromPosition = mover.position;
                var fromScale = mover.localScale;
                GetDemoTarget(mover, out var toPosition, out var toScale);
                yield return Animate(demoMoveTime, t =>
                {
                    var e = Mathf.SmoothStep(0f, 1f, t);
                    mover.position = Vector3.Lerp(fromPosition, toPosition, e);
                    mover.localScale = Vector3.Lerp(fromScale, toScale, e);
                });
            }

            _demoIntro = null;
            GameEvents.GameplayEvents.TargetLookIntroDone.Raise();
        }

        // Where the mover's pivot lands when its rect lines up with the target's, and the scale that matches the target's width.
        private void GetDemoTarget(RectTransform mover, out Vector3 position, out Vector3 scale)
        {
            var target = demoTarget.rect;
            var pivot = mover.pivot;
            position = demoTarget.TransformPoint(new Vector3(target.x + target.width * pivot.x, target.y + target.height * pivot.y, 0f));

            var imageWidth = mover.rect.width * mover.lossyScale.x;
            var targetWidth = target.width * demoTarget.lossyScale.x;
            scale = imageWidth > 0.0001f ? mover.localScale * (targetWidth / imageWidth) : mover.localScale;
        }

        // Card and frame back where they sit in the prefab, background on.
        private void ResetCard()
        {
            demoImage.anchoredPosition = _demoStartPosition;
            demoImage.localScale = _demoStartScale;
            if (demoFrame != null)
            {
                demoFrame.anchoredPosition = _frameStartPosition;
                demoFrame.localScale = _frameStartScale;
            }
            if (cardBackground != null)
                cardBackground.enabled = true;
        }

        private void SaveDemoStart()
        {
            if (_demoStartSaved || demoImage == null)
                return;

            _demoStartSaved = true;
            _demoStartPosition = demoImage.anchoredPosition;
            _demoStartScale = demoImage.localScale;
            if (demoFrame != null)
            {
                _frameStartPosition = demoFrame.anchoredPosition;
                _frameStartScale = demoFrame.localScale;
            }
        }

        // Card waiting for PLAY!: intro-only objects on, run-only objects off (and the other way round).
        private void SetIntroMode(bool intro)
        {
            if (introOnlyObjects != null)
                foreach (var go in introOnlyObjects)
                    if (go != null) go.SetActive(intro);

            if (runOnlyObjects != null)
                foreach (var go in runOnlyObjects)
                    if (go != null) go.SetActive(!intro);
        }

        // Stops the intro and hides the image (back at its start place, ready for the next level).
        private void StopDemoIntro(bool keepVisible)
        {
            if (_demoIntro != null)
                SetIntroMode(false);
            _waitingForStart = false;

            if (_demoIntro != null)
            {
                StopCoroutine(_demoIntro);
                _demoIntro = null;
            }

            if (demoImage == null || keepVisible || !_demoStartSaved)
                return;

            ResetCard();
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
