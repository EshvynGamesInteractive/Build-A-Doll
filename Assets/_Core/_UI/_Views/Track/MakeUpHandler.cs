using Core._GameEvent;
using Game.Hurdles;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _Core._UI._Views
{
    /// <summary>
    /// The make-up mini-step in the Gameplay HUD: shows the prompt (e.g. "Apply Lipstick"), lets the player
    /// drag the make-up stick, and counts dragging over the face area. When enough is done it tells the
    /// MakeUpStation, which puts the make-up on and lets her run on. All UI is set up in the prefab.
    /// </summary>
    public class MakeUpHandler : MonoBehaviour, IHudPresenter<MakeUpStation>
    {
        [SerializeField] private GameObject container;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private MakeUpStick stick;

        [Tooltip("Optional: the stick's Image, given the station's stick icon.")]
        [SerializeField] private Image stickImage;

        [Tooltip("Area over the doll's face. Only drags inside it count. Empty = anywhere.")]
        [SerializeField] private RectTransform faceArea;

        [Tooltip("Optional: an Image (Filled) showing how much is done.")]
        [SerializeField] private Image progressFill;

        [Tooltip("Screen pixels of dragging over the face needed to finish.")]
        [SerializeField, Min(1f)] private float strokeToFinish = 1200f;

        [SerializeField] private AudioClip doneSound;

        private MakeUpStation _station;
        private RectTransform _stickRect;
        private Vector3 _stickHome;
        private float _stroke;

        private void Awake()
        {
            if (stick != null)
            {
                _stickRect = stick.transform as RectTransform;
                if (_stickRect != null)
                    _stickHome = _stickRect.localPosition;
                stick.Dragged += OnStickDragged;
                stick.Released += ReturnStick;
            }

            HudPresenter<MakeUpStation>.Register(this);
            GameEvents.GameplayEvents.ResetRun.Register(Close);
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (stick != null)
            {
                stick.Dragged -= OnStickDragged;
                stick.Released -= ReturnStick;
            }

            HudPresenter<MakeUpStation>.Unregister(this);
            GameEvents.GameplayEvents.ResetRun.Unregister(Close);
        }

        public void Present(MakeUpStation station)
        {
            _station = station;
            _stroke = 0f;

            if (promptText != null)
                promptText.text = station.Prompt;
            if (stickImage != null && station.StickIcon != null)
                stickImage.sprite = station.StickIcon;

            SetProgress(0f);
            ReturnStick();
            SetVisible(true);
        }

        private void OnStickDragged(PointerEventData data)
        {
            if (_station == null)
                return;

            MoveStick(data);

            if (faceArea != null && !RectTransformUtility.RectangleContainsScreenPoint(faceArea, data.position, data.pressEventCamera))
                return;

            _stroke += data.delta.magnitude;
            SetProgress(_stroke / strokeToFinish);
            if (_stroke >= strokeToFinish)
                Finish();
        }

        private void MoveStick(PointerEventData data)
        {
            if (_stickRect == null || _stickRect.parent is not RectTransform parent)
                return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, data.position, data.pressEventCamera, out var local))
                _stickRect.localPosition = new Vector3(local.x, local.y, _stickHome.z);
        }

        private void Finish()
        {
            var station = _station;
            Close();
            if (doneSound != null)
                GameEvents.AudioEvents.PlaySfx.Raise(doneSound);
            if (station != null)
                station.Complete();
        }

        private void Close()
        {
            _station = null;
            ReturnStick();
            SetVisible(false);
        }

        private void ReturnStick()
        {
            if (_stickRect != null)
                _stickRect.localPosition = _stickHome;
        }

        private void SetProgress(float progress)
        {
            if (progressFill != null)
                progressFill.fillAmount = Mathf.Clamp01(progress);
        }

        private void SetVisible(bool visible)
        {
            if (container != null)
                container.SetActive(visible);
        }
    }
}
