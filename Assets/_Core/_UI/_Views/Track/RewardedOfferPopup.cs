using System.Collections.Generic;
using Core._GameEvent;
using Core.Utilities;
using Game.Ads;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    /// <summary>
    /// The rewarded pop-up ("REMOVE FIRE?", "WATCH AD?") in the Gameplay HUD. Shows offers one at a time.
    /// Every UI element is set up in the prefab; this only fills and toggles them.
    /// Put it on an object inside the Gameplay screen; Container is the panel it shows and hides.
    /// </summary>
    public class RewardedOfferPopup : MonoBehaviour, IHudPresenter<RewardedOffer>
    {
        [SerializeField] private GameObject container;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Button watchButton;
        [SerializeField] private Button skipButton;

        [Tooltip("Seconds before it closes by itself as a \"no\". 0 = waits for a button.")]
        [SerializeField, Min(0f)] private float autoSkipSeconds = 5f;

        [Tooltip("Optional: an Image (Filled) that empties as the auto-skip time runs out.")]
        [SerializeField] private Image timerFill;

        private readonly Queue<RewardedOffer> _queue = new();
        private RewardedOffer _current;
        private float _timeLeft;
        private bool _waitingForAd;

        // Awake / OnDestroy (not OnEnable / OnDisable): the Gameplay screen hides while paused, and a
        // restart from the pause menu must still close the pop-up.
        private void Awake()
        {
            UIPanelButtonUtility.Bind(watchButton, OnWatch);
            UIPanelButtonUtility.Bind(skipButton, OnSkip);
            HudPresenter<RewardedOffer>.Register(this);
            GameEvents.GameplayEvents.ResetRun.Register(DeclineAll);
            SetVisible(false);
        }

        private void OnDestroy()
        {
            HudPresenter<RewardedOffer>.Unregister(this);
            GameEvents.GameplayEvents.ResetRun.Unregister(DeclineAll);
        }

        public void Present(RewardedOffer offer)
        {
            if (offer == null)
                return;

            _queue.Enqueue(offer);
            if (_current == null)
                ShowNext();
        }

        private void ShowNext()
        {
            _current = null;
            while (_queue.Count > 0)
            {
                var next = _queue.Dequeue();
                if (!next.IsResolved)
                {
                    _current = next;
                    break;
                }
            }

            if (_current == null)
            {
                SetVisible(false);
                return;
            }

            if (titleText != null)
                titleText.text = _current.Title;

            if (iconImage != null)
            {
                iconImage.sprite = _current.Icon;
                iconImage.enabled = _current.Icon != null;
            }

            _timeLeft = autoSkipSeconds;
            _waitingForAd = false;
            SetButtonsInteractable(true);
            SetVisible(true);
        }

        private void Update()
        {
            if (_current == null || _waitingForAd || autoSkipSeconds <= 0f)
                return;

            _timeLeft -= Time.unscaledDeltaTime;
            if (timerFill != null)
                timerFill.fillAmount = Mathf.Clamp01(_timeLeft / autoSkipSeconds);

            if (_timeLeft <= 0f)
                OnSkip();
        }

        private void OnWatch()
        {
            if (_current == null || _waitingForAd)
                return;

            _waitingForAd = true;
            SetButtonsInteractable(false);
            _current.Accept(ShowNext);
        }

        private void OnSkip()
        {
            if (_current == null || _waitingForAd)
                return;

            _current.Decline();
            ShowNext();
        }

        private void DeclineAll()
        {
            _current?.Decline();
            while (_queue.Count > 0)
                _queue.Dequeue().Decline();

            _current = null;
            _waitingForAd = false;
            SetVisible(false);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (watchButton != null)
                watchButton.interactable = interactable;
            if (skipButton != null)
                skipButton.interactable = interactable;
        }

        private void SetVisible(bool visible)
        {
            if (container != null)
                container.SetActive(visible);
        }
    }
}
