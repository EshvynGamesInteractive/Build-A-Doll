using Core._GameEvent;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player.Input
{
    /// <summary>
    /// Reads drag input from the Input System's Pointer (touch on mobile, mouse in the Editor).
    /// Also raises the shared PointerDown / PointerUp events for anyone else who cares (UI, tutorial).
    /// </summary>
    [DefaultExecutionOrder(-100)] // read input before the controller moves
    public class PointerDragInput : MonoBehaviour, IDragInput
    {
        public bool IsHolding { get; private set; }
        public float HorizontalDelta { get; private set; }

        private Vector2 _lastPosition;

        private void Update()
        {
            HorizontalDelta = 0f;

            var pointer = Pointer.current;
            if (pointer == null)
                return;

            var isPressed = pointer.press.isPressed;
            var position = pointer.position.ReadValue();

            if (isPressed && !IsHolding)
                BeginHold(position);
            else if (!isPressed && IsHolding)
                EndHold();
            else if (isPressed)
                TrackDrag(position);
        }

        private void OnDisable()
        {
            if (IsHolding)
                EndHold();
        }

        private void BeginHold(Vector2 position)
        {
            IsHolding = true;
            _lastPosition = position;
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
    }
}
