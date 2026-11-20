using _Core._Player;
using Core._GameEvent;
using Game.Outfit;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>Scene-owned makeup interaction. All UI, camera and overlays are authored in the Inspector.</summary>
    [DisallowMultipleComponent]
    public sealed class PaintToolView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const int GridSize = 12;
        public static PaintToolView Instance { get; private set; }
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text instruction;
        [SerializeField] private Image progressFill;
        [SerializeField] private RectTransform brush;
        [SerializeField] private Camera renderCamera;
        [Tooltip("Pre-authored camera; priority must exceed the run camera. Disabled outside makeup.")]
        [SerializeField] private CinemachineCamera makeupCamera;
        [Tooltip("Camera position relative to runner; position it in front of the face.")]
        [SerializeField] private Vector3 cameraOffset = new(0f, 1.65f, 0.65f);
        [SerializeField] private Vector3 focusOffset = new(0f, 1.65f, 0f);
        [SerializeField, Range(0.03f, 0.5f)] private float brushRadius = 0.15f;
        [SerializeField, Range(0.5f, 1f)] private float completionCoverage = 0.95f;
        [SerializeField, Min(0f)] private float completionDelay = 0.4f;
        [SerializeField] private Color[] palette = { new(1f, 0.4f, 0.85f), new(0.85f, 0.1f, 0.15f) };

        private RunnerController _runner;
        private DollMakeup _makeup;
        private MakeupZone _zone;
        private Color _color;
        private float[][] _covered;
        private int[] _counts;
        private int _totalSamples;
        private int _paintedSamples;
        private int _pointerId;
        private Animator _animator;
        private float _animationSpeed;
        private bool _dragging;
        private bool _complete;
        private bool _waitForRelease;
        private bool _hadColor;
        private Color _previousColor;
        private float _closeAt;
        public bool IsPainting => _runner != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one PaintToolView may be active.", this);
                enabled = false;
                return;
            }
            Instance = this;
            if (panel != null) panel.SetActive(false);
            if (makeupCamera != null) makeupCamera.enabled = false;
        }

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
            GameEvents.GameplayEvents.ResetRun.Register(OnReset);
            GameEvents.GameplayEvents.StopRun.Register(Cancel);
        }

        private void OnDisable()
        {
            GameEvents.GameplayEvents.ResetRun.Unregister(OnReset);
            GameEvents.GameplayEvents.StopRun.Unregister(Cancel);
            Cancel();
            if (Instance == this) Instance = null;
        }

        public bool TryBegin(RunnerController runner, DollMakeup makeup, MakeupZone zone, Color color)
        {
            if (!isActiveAndEnabled || IsPainting || runner == null || makeup == null || !makeup.CanPaint(zone) ||
                panel == null || panel == gameObject || renderCamera == null || makeupCamera == null)
                return false;
            _runner = runner;
            _makeup = makeup;
            _zone = zone;
            _color = color;
            _hadColor = makeup.TryGetColor(zone, out _previousColor);
            _complete = false;
            _dragging = false;
            _waitForRelease = false;
            var regions = makeup.GetZone(zone).volumes.Length;
            _covered = new float[regions][];
            _counts = new int[regions];
            _totalSamples = 0;
            for (var i = 0; i < regions; i++)
            {
                _covered[i] = new float[GridSize * GridSize];
                for (var cell = 0; cell < GridSize * GridSize; cell++)
                    if (Sample(cell).sqrMagnitude <= 0.25f) _totalSamples++;
            }
            ClearCoverage();
            runner.MakeupActive = true;
            _animator = runner.GetComponentInChildren<Animator>();
            if (_animator != null)
            {
                _animationSpeed = _animator.speed;
                _animator.speed = 0f;
            }
            makeupCamera.transform.SetPositionAndRotation(runner.transform.TransformPoint(cameraOffset),
                Quaternion.LookRotation(runner.transform.TransformPoint(focusOffset) - runner.transform.TransformPoint(cameraOffset), runner.transform.up));
            makeupCamera.PreviousStateIsValid = false;
            makeupCamera.enabled = true;
            if (instruction != null)
                instruction.text = zone == MakeupZone.EyeShadow ? "Apply Eye Shadow" : "Apply Lipstick";
            panel.SetActive(true);
            return true;
        }

        // Wire each authored palette button to SelectColor with its palette index.
        public void SelectColor(int index)
        {
            if (!IsPainting || _complete || palette == null || index < 0 || index >= palette.Length) return;
            _color = palette[index];
            ClearCoverage();
        }

        private void ClearCoverage()
        {
            _paintedSamples = 0;
            for (var i = 0; i < _covered.Length; i++)
            {
                System.Array.Clear(_covered[i], 0, _covered[i].Length);
                _counts[i] = 0;
                _makeup.SetCoverage(_zone, i, _color, 0f);
            }
            if (progressFill != null) progressFill.fillAmount = 0f;
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (!IsPainting || _complete || _dragging || Time.timeScale == 0f) return;
            _pointerId = data.pointerId;
            _dragging = true;
            ApplyBrush(data);
        }
        public void OnDrag(PointerEventData data)
        {
            if (_dragging && data.pointerId == _pointerId) ApplyBrush(data);
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (data.pointerId == _pointerId) _dragging = false;
        }

        private static Vector2 Sample(int cell) => new(
            ((cell % GridSize) + 0.5f) / GridSize - 0.5f,
            ((cell / GridSize) + 0.5f) / GridSize - 0.5f);

        private void ApplyBrush(PointerEventData data)
        {
            if (!IsPainting || _complete || Time.timeScale == 0f) return;
            if (brush != null && brush.parent is RectTransform parent &&
                RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, data.position, data.pressEventCamera, out var world))
                brush.position = world;
            var ray = renderCamera.ScreenPointToRay(data.position);
            var volumes = _makeup.GetZone(_zone).volumes;
            var samplesPerRegion = _totalSamples / volumes.Length;
            for (var i = 0; i < volumes.Length; i++)
            {
                var volume = volumes[i];
                var plane = new Plane(volume.forward, volume.position);
                if (!plane.Raycast(ray, out var distance)) continue;
                var local = (Vector2)volume.InverseTransformPoint(ray.GetPoint(distance));
                if (local.sqrMagnitude > (0.5f + brushRadius) * (0.5f + brushRadius)) continue;
                for (var cell = 0; cell < _covered[i].Length; cell++)
                {
                    var sample = Sample(cell);
                    if (_covered[i][cell] > 0f || sample.sqrMagnitude > 0.25f ||
                        (sample - local).sqrMagnitude > brushRadius * brushRadius) continue;
                    _covered[i][cell] = 1f;
                    _counts[i]++;
                    _paintedSamples++;
                }
                _makeup.SetCoverage(_zone, i, _color, (float)_counts[i] / samplesPerRegion);
                _makeup.SetBrushMask(_zone, i, _covered[i]);
            }
            var progress = (float)_paintedSamples / _totalSamples;
            if (progressFill != null) progressFill.fillAmount = Mathf.Clamp01(progress / completionCoverage);
            if (progress < completionCoverage) return;
            _complete = true;
            _makeup.Paint(_zone, _color, true);
            _closeAt = Time.time + completionDelay;
        }

        private void Update()
        {
            if (_covered != null && _runner == null) { Cancel(); return; }
            if (!_complete || Time.timeScale == 0f || Time.time < _closeAt) return;
            // Hide the view, then wait for release so the last paint drag cannot steer the runner.
            if (!_waitForRelease)
            {
                panel.SetActive(false);
                _waitForRelease = true;
            }
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer == null || !pointer.press.isPressed) Cancel();
        }

        private void OnReset()
        {
            // DollMakeup owns reset visuals; never restore the previous run's preview over them.
            _complete = true;
            Cancel();
        }

        public void Cancel()
        {
            if (_makeup != null && !_complete)
            {
                var entry = _makeup.GetZone(_zone);
                for (var i = 0; i < entry.overlays.Length; i++)
                    _makeup.SetCoverage(_zone, i, _previousColor, _hadColor ? 1f : 0f);
            }
            if (_animator != null) _animator.speed = _animationSpeed;
            _animator = null;
            if (_runner != null) _runner.MakeupActive = false;
            _runner = null;
            _makeup = null;
            _covered = null;
            _dragging = false;
            _complete = false;
            if (panel != null) panel.SetActive(false);
            if (makeupCamera != null) makeupCamera.enabled = false;
        }
    }
}
