using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Core._UI._LevelMap
{
    /// <summary>
    /// One level button on the main-menu level map (a UI object you build in the HomeMenu prefab).
    /// LevelMapView tells it which level it shows and whether that level is done, current or locked;
    /// it shows the matching state objects and number, and raises a click for done / current levels.
    /// </summary>
    public class LevelNode : MonoBehaviour
    {
        public enum State
        {
            Completed,
            Current,
            Locked
        }

        [SerializeField] private Button button;
        [SerializeField] private Image stateIcon;

        [Header("State visuals (each optional)")]
        [Tooltip("Shown only on the current level (e.g. the green arrow, glow).")]
        [SerializeField] private GameObject currentMarker;
        [Tooltip("Shown on special levels (every Nth level, e.g. the skull / boss badge).")]
        [SerializeField] private GameObject specialBadge;

        private Action<int> _onClick;
        private int _levelIndex;
        private State _state;

        public int LevelIndex => _levelIndex;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();
            if (button != null)
                button.onClick.AddListener(OnClicked);
        }

        /// <summary>Shows level 'levelIndex' (0-based) in the given state. 'onClick' gets the level index.</summary>
        public void Setup(int levelIndex, State state, bool special, Action<int> onClick)
        {
            _levelIndex = levelIndex;
            _state = state;
            _onClick = onClick;
            
            SetActive(currentMarker, state == State.Current);
            SetActive(specialBadge, special);

            if (button != null)
                button.interactable = state != State.Locked;
        }

        /// <summary>Swaps the state image's sprite. A null sprite (not set on the map yet) keeps the current one.</summary>
        public void SetStateIcon(Sprite icon)
        {
            if (stateIcon != null && icon != null)
                stateIcon.sprite = icon;
        }

        public RectTransform Rect => (RectTransform)transform;

        private void OnClicked()
        {
            if (_state == State.Locked)
                return;

            Core._GameEvent.GameEvents.AudioEvents.ButtonClicked.Raise();
            _onClick?.Invoke(_levelIndex);
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }
    }
}
