using System;
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
    /// <summary>
    /// Level start screen (own panel since 25 Nov 2026, was part of GameplayMenu): the "DRESS HER LIKE THIS!" card with
    /// the demo doll's render texture, PLAY and Settings.
    /// TargetLookShown → this panel opens and the card pops in → PLAY → the frame flies to Frame Target (same place as
    /// the HUD's Target Thumb) → Gameplay HUD shown → TargetLookIntroDone (LevelManager starts the run).
    /// Settings from the card comes back to the waiting card. Everything is built in the TargetLookPanel prefab.
    /// </summary>
    public class TargetLookViewBehavior : UIScreen
    {
        [Tooltip("The card that pops in (usually the Container). Empty = Container.")]
        [SerializeField] private RectTransform card;
        [Tooltip("The part of the card that flies to the corner (Frame with the doll picture).")]
        [SerializeField] private RectTransform frame;
        [Tooltip("The card's full-screen background. Hidden while the frame flies, so the 3D scene shows.")]
        [SerializeField] private Graphic cardBackground;
        [Tooltip("Where the frame lands (top right). Must match where the HUD's Target Thumb sits. Its width sets the final size.")]
        [SerializeField] private RectTransform frameTarget;

        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [Tooltip("Shown on the waiting card, hidden while the frame flies (title, PLAY, settings, glow, coins).")]
        [SerializeField] private GameObject[] cardOnlyObjects;
        [Tooltip("Title. Shows the level's Brief (\"Design a NURSE outfit!\") when it has one, its own text otherwise.")]
        [SerializeField] private TMP_Text briefText;

        [Header("Timing (seconds, unscaled)")]
        [SerializeField, Min(0f)] private float popTime = 0.25f;
        [Tooltip("Only used when there's no Play Button.")]
        [SerializeField, Min(0f)] private float holdTime = 2f;
        [SerializeField, Min(0f)] private float moveTime = 0.6f;

        private Coroutine _intro;
        private bool _waitingForPlay;
        private bool _playPressed;
        private string _defaultBrief;

        private bool _saved;
        private Vector2 _cardPosition, _framePosition;
        private Vector3 _cardScale, _frameScale;

        private RectTransform Card => card != null ? card : container != null ? container.transform as RectTransform : null;

        private void OnEnable()
        {
            GameEvents.GameplayEvents.TargetLookShown.Register(OnTargetLookShown);
            GameEvents.GameplayEvents.ResetRun.Register(OnStop);
            GameEvents.GameplayEvents.RunStarted.Register(OnRunStarted);
            GameEvents.GameplayEvents.ComparisonStarted.Register(OnComparisonStarted);
        }

        private void OnDisable()
        {
            GameEvents.GameplayEvents.TargetLookShown.Unregister(OnTargetLookShown);
            GameEvents.GameplayEvents.ResetRun.Unregister(OnStop);
            GameEvents.GameplayEvents.RunStarted.Unregister(OnRunStarted);
            GameEvents.GameplayEvents.ComparisonStarted.Unregister(OnComparisonStarted);
            Stop();
        }

        protected override void Initialize()
        {
            UIPanelButtonUtility.Bind(playButton, () => _playPressed = true);
            UIPanelButtonUtility.Bind(settingsButton, () => GameEvents.UIEvents.ShowByType.Raise(ScreenType.Settings));
            if (briefText != null && _defaultBrief == null)
                _defaultBrief = briefText.text;
            SaveStart();
        }

        public override void Hide()
        {
            base.Hide();

            // Settings opened from the card: keep waiting for PLAY, the card is there again when Settings closes.
            if (_waitingForPlay && UIManager.IsShowing(ScreenType.Settings))
                return;
            Stop();
        }

        private void OnTargetLookShown(LevelData level)
        {
            ShowBrief(level);
            Stop();
            if (container == null || Card == null || !isActiveAndEnabled)
            {
                GameEvents.GameplayEvents.TargetLookIntroDone.Raise();
                return;
            }

            GameEvents.UIEvents.ShowByType.Raise(ScreenType.TargetLook);
            _intro = StartCoroutine(PlayIntro());
        }

        // LevelManager started the run on its own (no demo camera → timer): don't leave the card over the run.
        private void OnRunStarted()
        {
            if (_intro == null)
                return;
            Stop();
            Leave();
        }

        private void OnComparisonStarted(Game.Outfit.OutfitMatch player, Game.Outfit.OutfitMatch ai) => OnStop();

        private void OnStop() => Stop();

        private IEnumerator PlayIntro()
        {
            SaveStart();
            ResetCard();
            SetCardObjects(true);

            var cardRect = Card;
            cardRect.localScale = Vector3.zero;
            yield return Animate(popTime, t => cardRect.localScale = _cardScale * EaseOutBack(t));

            if (playButton != null)
            {
                _playPressed = false;
                _waitingForPlay = true;
                while (!_playPressed)
                    yield return null;
                _waitingForPlay = false;
            }
            else
            {
                yield return new WaitForSecondsRealtime(holdTime);
            }

            // Only the frame leaves: background and card extras go, the frame flies to the corner.
            SetCardObjects(false);
            if (cardBackground != null)
                cardBackground.enabled = false;

            if (frame != null && frameTarget != null)
            {
                var fromPosition = frame.position;
                var fromScale = frame.localScale;
                GetTarget(frame, out var toPosition, out var toScale);
                yield return Animate(moveTime, t =>
                {
                    var e = Mathf.SmoothStep(0f, 1f, t);
                    frame.position = Vector3.Lerp(fromPosition, toPosition, e);
                    frame.localScale = Vector3.Lerp(fromScale, toScale, e);
                });
            }

            _intro = null;
            Leave();
        }

        // Hand over to the HUD (its Target Thumb sits where the frame landed) and let LevelManager start the run.
        private void Leave()
        {
            if (UIManager.IsShowing(ScreenType.TargetLook))
                GameEvents.UIEvents.ShowByType.Raise(ScreenType.Gameplay);
            GameEvents.GameplayEvents.TargetLookIntroDone.Raise();
        }

        private void Stop()
        {
            _waitingForPlay = false;
            if (_intro != null)
            {
                StopCoroutine(_intro);
                _intro = null;
            }
            ResetCard();
            SetCardObjects(true);
        }

        private void ShowBrief(LevelData level)
        {
            if (briefText == null)
                return;
            if (_defaultBrief == null)
                _defaultBrief = briefText.text;
            briefText.text = level != null && !string.IsNullOrEmpty(level.Brief) ? level.Brief : _defaultBrief;
        }

        // Where the frame's pivot lands when its rect lines up with the target's, and the scale that matches the target's width.
        private void GetTarget(RectTransform mover, out Vector3 position, out Vector3 scale)
        {
            var target = frameTarget.rect;
            var pivot = mover.pivot;
            position = frameTarget.TransformPoint(new Vector3(target.x + target.width * pivot.x, target.y + target.height * pivot.y, 0f));

            var moverWidth = mover.rect.width * mover.lossyScale.x;
            var targetWidth = target.width * frameTarget.lossyScale.x;
            scale = moverWidth > 0.0001f ? mover.localScale * (targetWidth / moverWidth) : mover.localScale;
        }

        private void SaveStart()
        {
            if (_saved || Card == null)
                return;
            _saved = true;
            _cardPosition = Card.anchoredPosition;
            _cardScale = Card.localScale;
            if (frame != null)
            {
                _framePosition = frame.anchoredPosition;
                _frameScale = frame.localScale;
            }
        }

        private void ResetCard()
        {
            if (!_saved)
                return;
            Card.anchoredPosition = _cardPosition;
            Card.localScale = _cardScale;
            if (frame != null)
            {
                frame.anchoredPosition = _framePosition;
                frame.localScale = _frameScale;
            }
            if (cardBackground != null)
                cardBackground.enabled = true;
        }

        private void SetCardObjects(bool visible)
        {
            if (cardOnlyObjects == null)
                return;
            foreach (var go in cardOnlyObjects)
                if (go != null && go.activeSelf != visible)
                    go.SetActive(visible);
        }

        private static IEnumerator Animate(float duration, Action<float> step)
        {
            if (duration <= 0f)
            {
                step(1f);
                yield break;
            }

            for (var t = 0f; t < 1f;)
            {
                t = Mathf.Min(1f, t + Time.unscaledDeltaTime / duration);
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
    }
}
