using System.Collections.Generic;
using Core._GameEvent;
using Game.Hurdles;
using Game.Level;
using TMPro;
using UnityEngine;

namespace _Core._UI._Views
{
    /// <summary>
    /// Tutorial hand for starting levels (LevelData Show Swipe Guide). Build the hand in the GameplayMenu prefab
    /// (an Image over the drag area) and assign it here; nothing is created from code.
    /// • Run start: the hand swipes left and right until the player first touches the screen.
    /// • Before each gate: the hand swipes toward the GREEN gate while she isn't in its lane yet.
    /// </summary>
    public class SwipeGuide : MonoBehaviour
    {
        [Tooltip("The hand image. Its position in the prefab is the middle of the swipe.")]
        [SerializeField] private RectTransform hand;
        [Tooltip("Optional hint text, e.g. \"Swipe to the GREEN gate!\". Shown together with the hand.")]
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private string introHint = "Hold & drag to move";
        [SerializeField] private string gateHint = "Swipe to the GREEN gate!";

        [Header("Swipe")]
        [Tooltip("How far the hand moves to each side, in UI pixels.")]
        [SerializeField, Min(10f)] private float swipeDistance = 220f;
        [Tooltip("Seconds for one swipe.")]
        [SerializeField, Min(0.1f)] private float swipeTime = 0.9f;

        [Header("Gate hints")]
        [Tooltip("Start pointing to the green gate this many metres before it.")]
        [SerializeField, Min(1f)] private float hintFrom = 24f;
        [Tooltip("Stop pointing this many metres before it.")]
        [SerializeField, Min(0f)] private float hintTo = 3f;
        [Tooltip("She counts as in the green lane when this close to it sideways (metres).")]
        [SerializeField, Min(0.1f)] private float laneTolerance = 0.9f;
        [Tooltip("Gates further than this sideways belong to the AI's ramp and are ignored (metres).")]
        [SerializeField, Min(1f)] private float maxSideways = 6f;

        private readonly List<ItemGate> _gates = new();
        private Vector2 _handHome;
        private bool _homeSaved;
        private bool _active;       // this level has the guide
        private bool _running;      // run in progress
        private bool _waitingFirstTouch;
        private Transform _trackFrame; // forward = run direction
        private float _phaseStart;
        private int _lastDirection = 99;

        private void Awake()
        {
            SaveHome();
            Show(false, 0);
        }

        private void OnEnable()
        {
            GameEvents.GameplayEvents.LevelLoaded.Register(OnLevelLoaded);
            GameEvents.GameplayEvents.RunStarted.Register(OnRunStarted);
            GameEvents.GameplayEvents.ResetRun.Register(OnStop);
            GameEvents.GameplayEvents.ComparisonStarted.Register(OnComparisonStarted);
            GameEvents.GameplayEvents.InputBlocked.Register(OnInputBlocked);
            GameEvents.InputEvents.PointerDown.Register(OnPointerDown);
        }

        private void OnDisable()
        {
            GameEvents.GameplayEvents.LevelLoaded.Unregister(OnLevelLoaded);
            GameEvents.GameplayEvents.RunStarted.Unregister(OnRunStarted);
            GameEvents.GameplayEvents.ResetRun.Unregister(OnStop);
            GameEvents.GameplayEvents.ComparisonStarted.Unregister(OnComparisonStarted);
            GameEvents.GameplayEvents.InputBlocked.Unregister(OnInputBlocked);
            GameEvents.InputEvents.PointerDown.Unregister(OnPointerDown);
        }

        private void OnLevelLoaded(LevelData level, int levelNumber)
        {
            _active = level != null && level.ShowSwipeGuide;
            OnStop();
        }

        private void OnRunStarted()
        {
            if (!_active)
                return;

            _running = true;
            _waitingFirstTouch = true;
            _gates.Clear();
            _gates.AddRange(FindObjectsByType<ItemGate>(FindObjectsSortMode.None));
            var scene = FindFirstObjectByType<GameplayScene>();
            _trackFrame = scene != null ? scene.PlayerStartPoint : null;
        }

        private void OnPointerDown() => _waitingFirstTouch = false;

        private void OnComparisonStarted(Game.Outfit.OutfitMatch player, Game.Outfit.OutfitMatch ai) => OnStop();

        private void OnInputBlocked(bool blocked)
        {
            if (blocked)
                OnStop();
        }

        private void OnStop()
        {
            _running = false;
            _waitingFirstTouch = false;
            _gates.Clear();
            Show(false, 0);
        }

        private void Update()
        {
            if (!_running || hand == null)
                return;

            if (_waitingFirstTouch)
            {
                Show(true, 0);
                return;
            }

            Show(TryGetGateDirection(out var direction), direction);
        }

        // -1 / +1 = swipe left / right toward the next green gate she isn't lined up with yet.
        private bool TryGetGateDirection(out int direction)
        {
            direction = 0;
            var player = ReferenceManager.Instance != null ? ReferenceManager.Instance.player : null;
            if (player == null)
                return false;

            var forward = _trackFrame != null ? _trackFrame.forward : Vector3.forward;
            var right = _trackFrame != null ? _trackFrame.right : Vector3.right;
            var origin = player.transform.position;

            ItemGate next = null;
            var nextAhead = float.MaxValue;
            var nextSide = 0f;
            foreach (var gate in _gates)
            {
                if (gate == null || gate.IsUsed || !gate.IsTargetChoice || !gate.gameObject.activeInHierarchy)
                    continue;

                var offset = gate.transform.position - origin;
                var ahead = Vector3.Dot(offset, forward);
                var side = Vector3.Dot(offset, right);
                if (ahead < hintTo || ahead > hintFrom || Mathf.Abs(side) > maxSideways || ahead >= nextAhead)
                    continue;

                next = gate;
                nextAhead = ahead;
                nextSide = side;
            }

            if (next == null || Mathf.Abs(nextSide) <= laneTolerance)
                return false;

            direction = nextSide > 0f ? 1 : -1;
            return true;
        }

        // direction 0 = swipe both ways (first touch), -1/+1 = swipe toward that side.
        private void Show(bool visible, int direction)
        {
            if (hand == null)
                return;

            if (hand.gameObject.activeSelf != visible)
                hand.gameObject.SetActive(visible);
            if (hintText != null)
            {
                if (hintText.gameObject.activeSelf != visible)
                    hintText.gameObject.SetActive(visible);
                if (visible)
                    hintText.text = direction == 0 ? introHint : gateHint;
            }

            if (!visible)
            {
                _lastDirection = 99;
                if (_homeSaved)
                    hand.anchoredPosition = _handHome;
                return;
            }

            if (direction != _lastDirection)
            {
                _lastDirection = direction;
                _phaseStart = Time.unscaledTime;
            }

            var phase = ((Time.unscaledTime - _phaseStart) % swipeTime) / swipeTime;
            var x = direction == 0
                ? Mathf.Sin(phase * Mathf.PI * 2f) * swipeDistance
                : Mathf.SmoothStep(0f, direction * swipeDistance, Mathf.Clamp01(phase * 1.25f));
            hand.anchoredPosition = _handHome + new Vector2(x, 0f);
        }

        private void SaveHome()
        {
            if (_homeSaved || hand == null)
                return;
            _handHome = hand.anchoredPosition;
            _homeSaved = true;
        }
    }
}
