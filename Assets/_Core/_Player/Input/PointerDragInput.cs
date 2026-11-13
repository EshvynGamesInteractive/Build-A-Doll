using System.Collections.Generic;
using Core._GameEvent;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace Game.Player.Input
{
    /// <summary>
    /// Reads input from the Input System's Pointer (touch on mobile, mouse in the Editor):
    /// hold to run, drag sideways to steer, quick flick up to jump, flick down to slide.
    /// Space / Up and Down / S also jump and slide, for testing in the Editor.
    /// Also raises the shared PointerDown / PointerUp events for anyone else who cares (UI, tutorial).
    /// </summary>
    [DefaultExecutionOrder(-100)] // read input before the controller moves
    public class PointerDragInput : MonoBehaviour, IDragInput, IActionInput
    {
        [Header("Swipe up / down")]
        [Tooltip("How far the finger must move vertically, as a fraction of screen height.")]
        [SerializeField, Range(0.01f, 0.5f)] private float swipeDistance = 0.06f;

        [Tooltip("The move must happen within this many seconds to count as a flick, not a slow drag.")]
        [SerializeField, Min(0.05f)] private float swipeMaxTime = 0.25f;

        public bool IsHolding { get; private set; }
        public float HorizontalDelta { get; private set; }
        public bool JumpRequested { get; private set; }
        public bool SlideRequested { get; private set; }

        private readonly List<RaycastResult> _uiHits = new();
        private bool _blockedPress;
        private Vector2 _lastPosition;
        private Vector2 _swipeStart;
        private float _swipeStartTime;

        private void Update()
        {
            HorizontalDelta = 0f;
            JumpRequested = false;
            SlideRequested = false;

            ReadKeyboard();

            var pointer = Pointer.current;
            if (pointer == null)
                return;

            var isPressed = pointer.press.isPressed;
            var position = pointer.position.ReadValue();

            if (!isPressed)
                _blockedPress = false;
            else if (pointer.press.wasPressedThisFrame && IsOverUI(position))
                _blockedPress = true;

            if (Time.timeScale == 0f || _blockedPress)
            {
                if (IsHolding)
                    EndHold();
                return;
            }

            if (isPressed && !IsHolding)
                BeginHold(position);
            else if (!isPressed && IsHolding)
                EndHold();
            else if (isPressed)
            {
                TrackDrag(position);
                TrackSwipe(position);
            }
        }

        private void OnDisable()
        {
            if (IsHolding)
                EndHold();
        }

        private bool IsOverUI(Vector2 position)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = position }, _uiHits);
            var isOverUI = _uiHits.Count > 0;
            _uiHits.Clear();
            return isOverUI;
        }

        private void ReadKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.spaceKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                JumpRequested = true;

            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                SlideRequested = true;
        }

        private void BeginHold(Vector2 position)
        {
            IsHolding = true;
            _lastPosition = position;
            RestartSwipe(position);
            GameEvents.InputEvents.PointerDown.Raise();
        }

        private void EndHold()
        {
            IsHolding = false;
            GameEvents.InputEvents.PointerUp.Raise();
        }

        private void TrackDrag(Vector2 position)
        {
            var screenWidth = Mathf.Max(1, Screen.width);
            HorizontalDelta = (position.x - _lastPosition.x) / screenWidth;
            _lastPosition = position;
        }

        // The finger stays down the whole run, so a swipe is a quick vertical flick measured
        // from a start point that keeps moving forward while the finger is only steering.
        private void TrackSwipe(Vector2 position)
        {
            if (Time.unscaledTime - _swipeStartTime > swipeMaxTime)
            {
                RestartSwipe(position);
                return;
            }

            var delta = position - _swipeStart;
            var screenHeight = Mathf.Max(1, Screen.height);

            if (Mathf.Abs(delta.y) < swipeDistance * screenHeight || Mathf.Abs(delta.y) < Mathf.Abs(delta.x))
                return;

            if (delta.y > 0f)
                JumpRequested = true;
            else
                SlideRequested = true;

            RestartSwipe(position);
        }

        private void RestartSwipe(Vector2 position)
        {
            _swipeStart = position;
            _swipeStartTime = Time.unscaledTime;
        }
    }
}
