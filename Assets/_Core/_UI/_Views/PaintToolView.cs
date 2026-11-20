using System;
using System.Collections.Generic;
using _Core._Player;
using Core._GameEvent;
using Game.Ads;
using Game.Level;
using Game.Outfit;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Scene-owned makeup interaction (like the reference "Apply Eye shadow" screen). All UI, camera and overlays are
    /// authored in the Inspector. Opened by a MakeupStation (makeup mirror) or a PaintGate: the run freezes, the camera
    /// closes in on the face, a brush follows the finger and paints until the progress bar is full.
    /// Palette: up to 3 colours, each Free, bought with coins (e.g. 500) or unlocked by a rewarded ad.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaintToolView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public enum PriceType { Free = 0, Coins = 1, RewardedAd = 2 }

        [Serializable]
        public class PaletteOption
        {
            public string name = "Pink";
            public Color color = new(1f, 0.4f, 0.85f);
            public PriceType price = PriceType.Free;
            [Min(0)] public int cost = 500;
        }

        /// <summary>One palette button, authored in the prefab (nothing is created from code).</summary>
        [Serializable]
        public class PaletteButton
        {
            public Button button;
            [Tooltip("Image tinted with the option's colour.")] public Image swatch;
            [Tooltip("Shown on the selected colour (e.g. a green frame).")] public GameObject selected;
            [Tooltip("Shown while the colour costs coins; its text shows the price.")] public GameObject priceRoot;
            public TMP_Text priceText;
            [Tooltip("Shown while the colour needs an ad (e.g. 'Free ▶').")] public GameObject adRoot;
        }

        public const string MakeupColorPlacement = "makeup_color";
        private const int GridSize = 12;
        public static PaintToolView Instance { get; private set; }
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text instruction;
        [SerializeField] private Image progressFill;
        [Tooltip("Optional: a Slider used as the progress bar (instead of, or as well as, a filled Image).")]
        [SerializeField] private Slider progressSlider;
        [SerializeField] private RectTransform brush;
        [SerializeField] private Camera renderCamera;
        [Tooltip("Pre-authored camera; priority must exceed the run camera. Disabled outside makeup. " +
                 "Empty = the scene's CinemachineCamera named Makeup Camera Name (this view can live in a prefab).")]
        [SerializeField] private CinemachineCamera makeupCamera;
        [SerializeField] private string makeupCameraName = "CM Makeup";
        [Tooltip("Camera position relative to runner; position it in front of the face.")]
        [SerializeField] private Vector3 cameraOffset = new(0f, 1.65f, 0.65f);
        [SerializeField] private Vector3 focusOffset = new(0f, 1.65f, 0f);
        [SerializeField, Range(0.03f, 0.5f)] private float brushRadius = 0.15f;
        [SerializeField, Range(0.5f, 1f)] private float completionCoverage = 0.95f;
        [SerializeField, Min(0f)] private float completionDelay = 0.4f;
        [Tooltip("Old colour list, used only when Default Options is empty (all free).")]
        [SerializeField] private Color[] palette = { new(1f, 0.4f, 0.85f), new(0.85f, 0.1f, 0.15f) };

        [Header("Palette (reference: free / 500 coins / free with ad)")]
        [SerializeField] private PaletteButton[] paletteButtons;
        [Tooltip("Used when the station/gate doesn't bring its own options.")]
        [SerializeField] private PaletteOption[] defaultOptions =
        {
            new() { name = "Pink", color = new Color(1f, 0.4f, 0.85f), price = PriceType.Free },
            new() { name = "Brown", color = new Color(0.55f, 0.27f, 0.07f), price = PriceType.Coins, cost = 500 },
            new() { name = "Purple", color = new Color(0.6f, 0.3f, 0.9f), price = PriceType.RewardedAd }
        };

        [Header("Palette labels (one TMP per button: Price Text)")]
        [Tooltip("{0} = price.")]
        [SerializeField] private string coinsLabel = "{0}";
        [SerializeField] private string adLabel = "FREE";
        [SerializeField] private string unlockedLabel = "";

        [Header("Titles")]
        [SerializeField] private string eyeShadowTitle = "Apply Eye shadow";
        [SerializeField] private string lipsTitle = "Apply Lipstick";
        [SerializeField] private string blushTitle = "Apply Blush";

        [Header("UI preview (no face overlays yet)")]
        [Tooltip("When the doll has no paintable DollMakeup zone, still open the screen: the bar fills as you drag, the palette works, " +
                 "nothing is painted. Lets the UI be tested before the face overlays are authored.")]
        [SerializeField] private bool previewWithoutOverlays = true;
        [Tooltip("Drag distance (× screen height) that fills the bar in UI preview.")]
        [SerializeField, Min(0.1f)] private float previewDragToFill = 3f;

        [Header("Brush")]
        [Tooltip("Paint lands this far above the finger (× screen height), so the finger doesn't hide the brush tip. 0 = under the finger.")]
        [SerializeField] private Vector2 paintOffset = new(0f, 0.06f);

        [Header("Close-up on the face")]
        [Tooltip("Frame the head bone of a humanoid doll (works with any doll scale). Off = Camera Offset / Focus Offset.")]
        [SerializeField] private bool focusHeadBone = true;
        [Tooltip("How much of the doll fills the screen height in the close-up, × the doll's scale (0.38 ≈ the face, like the reference). " +
                 "The camera distance is worked out from the camera's field of view.")]
        [SerializeField, Min(0.05f)] private float faceViewHeight = 0.38f;
        [Tooltip("Moves the look point up from the head bone, × the doll's scale (the head bone is at the neck end).")]
        [SerializeField] private float faceHeight = 0.08f;

        private readonly List<PaletteOption> _options = new();
        private bool[] _unlocked = Array.Empty<bool>();
        private int _selected = -1;

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
        private Game.CameraRig.CameraFollow _follow; // fallback close-up when there's no makeup camera
        private bool _preview;          // UI preview: no overlays, progress from drag distance
        private float _previewDragged;
        private Vector2 _lastPoint;
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

            if (paletteButtons != null)
            {
                for (var i = 0; i < paletteButtons.Length; i++)
                {
                    var index = i;
                    if (paletteButtons[i]?.button != null)
                        paletteButtons[i].button.onClick.AddListener(() => SelectColor(index));
                }
            }
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

        public bool TryBegin(RunnerController runner, DollMakeup makeup, MakeupZone zone, Color color) =>
            TryBegin(runner, makeup, zone, color, null);

        /// <summary>Opens the makeup screen. 'options' = the palette for this session (null = Default Options);
        /// the option matching 'color' (or the first free one) starts selected.</summary>
        public bool TryBegin(RunnerController runner, DollMakeup makeup, MakeupZone zone, Color color, IReadOnlyList<PaletteOption> options)
        {
            if (renderCamera == null) renderCamera = Camera.main;
            if (makeupCamera == null) makeupCamera = FindMakeupCamera();
            var paintable = makeup != null && makeup.CanPaint(zone);
            if (!isActiveAndEnabled || IsPainting || runner == null || (!paintable && !previewWithoutOverlays) ||
                panel == null || panel == gameObject || renderCamera == null)
                return false;
            if (!paintable)
                Debug.Log("[PaintTool] UI preview: the doll has no paintable DollMakeup zone yet, so nothing is painted (see _Documentation/PaintToolSetup.txt).", runner);

            _preview = !paintable;
            _previewDragged = 0f;
            _runner = runner;
            _makeup = paintable ? makeup : null;
            _zone = zone;
            _color = color;
            SetUpPalette(options, color);
            _hadColor = paintable && makeup.TryGetColor(zone, out _previousColor);
            _complete = false;
            _dragging = false;
            _waitForRelease = false;
            var regions = paintable ? makeup.GetZone(zone).volumes.Length : 0;
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
            GetFacePose(runner, out var eye, out var focus);
            var look = Quaternion.LookRotation(focus - eye, Vector3.up);
            if (makeupCamera != null)
            {
                makeupCamera.transform.SetPositionAndRotation(eye, look);
                makeupCamera.PreviousStateIsValid = false;
                makeupCamera.enabled = true;
            }
            else
            {
                // No makeup camera in the scene: borrow the follow camera's stage shot for the close-up.
                _follow = FindFirstObjectByType<Game.CameraRig.CameraFollow>();
                if (_follow != null) _follow.ShowStagePose(eye, look);
                else Debug.LogWarning($"[PaintTool] No makeup camera ('{makeupCameraName}') and no CameraFollow: the view stays behind the doll.", this);
            }
            if (instruction != null)
                instruction.text = zone switch { MakeupZone.EyeShadow => eyeShadowTitle, MakeupZone.Lips => lipsTitle, _ => blushTitle };
            if (progressSlider != null) { progressSlider.interactable = false; progressSlider.minValue = 0f; progressSlider.maxValue = 1f; }
            if (brush != null) brush.gameObject.SetActive(false); // shown while dragging
            panel.SetActive(true);
            return true;
        }

        private CinemachineCamera FindMakeupCamera()
        {
            if (string.IsNullOrEmpty(makeupCameraName)) return null;
            foreach (var cam in FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (cam.name == makeupCameraName) return cam;
            return null;
        }

        private void SetProgress(float value)
        {
            value = Mathf.Clamp01(value);
            if (progressFill != null) progressFill.fillAmount = value;
            if (progressSlider != null) progressSlider.SetValueWithoutNotify(value);
        }

        // Close-up: in front of the head bone (scaled with the doll), or the old fixed offsets.
        private void GetFacePose(RunnerController runner, out Vector3 eye, out Vector3 focus)
        {
            var animator = runner.GetComponentInChildren<Animator>();
            var head = focusHeadBone && animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (head != null)
            {
                var scale = Mathf.Max(0.01f, animator.transform.lossyScale.y);
                var forward = Vector3.ProjectOnPlane(runner.transform.forward, Vector3.up).normalized;
                var fov = makeupCamera != null ? makeupCamera.Lens.FieldOfView : renderCamera != null ? renderCamera.fieldOfView : 60f;
                var distance = faceViewHeight * scale * 0.5f / Mathf.Tan(Mathf.Clamp(fov, 10f, 120f) * 0.5f * Mathf.Deg2Rad);
                focus = head.position + Vector3.up * (faceHeight * scale);
                eye = focus + forward * distance;
            }
            else
            {
                eye = runner.transform.TransformPoint(cameraOffset);
                focus = runner.transform.TransformPoint(focusOffset);
            }
        }

        // ---------- Palette ----------
        private void SetUpPalette(IReadOnlyList<PaletteOption> options, Color color)
        {
            _options.Clear();
            if (options != null && options.Count > 0)
                _options.AddRange(options);
            else if (defaultOptions != null && defaultOptions.Length > 0)
                _options.AddRange(defaultOptions);
            else if (palette != null)
                foreach (var c in palette) _options.Add(new PaletteOption { name = "", color = c, price = PriceType.Free });

            _unlocked = new bool[_options.Count];
            _selected = -1;
            for (var i = 0; i < _options.Count; i++)
            {
                _unlocked[i] = _options[i].price == PriceType.Free;
                if (_selected < 0 && _unlocked[i] && MakeupColor.Same(_options[i].color, color)) _selected = i;
            }

            if (_selected < 0)
                for (var i = 0; i < _options.Count && _selected < 0; i++)
                    if (_unlocked[i]) _selected = i;
            if (_selected >= 0) _color = _options[_selected].color;
            RefreshPalette();
        }

        private void RefreshPalette()
        {
            if (paletteButtons == null) return;
            for (var i = 0; i < paletteButtons.Length; i++)
            {
                var b = paletteButtons[i];
                if (b == null) continue;
                var has = i < _options.Count;
                if (b.button != null) b.button.gameObject.SetActive(has);
                if (!has) continue;

                var option = _options[i];
                if (b.swatch != null) b.swatch.color = option.color;
                if (b.selected != null) b.selected.SetActive(i == _selected);
                else if (b.button != null) b.button.transform.localScale = Vector3.one * (i == _selected ? 1.12f : 1f); // no frame: the chosen one is bigger
                var locked = !_unlocked[i];
                var coins = locked && option.price == PriceType.Coins;
                var ad = locked && option.price == PriceType.RewardedAd;
                if (b.priceRoot != null) b.priceRoot.SetActive(coins || (ad && b.adRoot == null));
                if (b.adRoot != null) b.adRoot.SetActive(ad);
                // One label can do both: "500" / "FREE" (ad) / nothing once unlocked.
                if (b.priceText != null)
                    b.priceText.text = coins ? string.Format(coinsLabel, option.cost) : ad && b.adRoot == null ? adLabel : unlockedLabel;
            }
        }

        /// <summary>Palette button pressed (buttons in Palette Buttons are wired automatically).
        /// Free → select. Coins → buy if the wallet has enough. Ad → watch a rewarded ad, then select.</summary>
        public void SelectColor(int index)
        {
            if (!IsPainting || _complete || index < 0 || index >= _options.Count || index == _selected) return;

            if (_unlocked[index])
            {
                Choose(index);
                return;
            }

            var option = _options[index];
            switch (option.price)
            {
                case PriceType.Coins:
                    if (LevelProgress.TrySpendCoins(option.cost))
                        Unlock(index);
                    else
                        Debug.Log($"[PaintTool] Not enough coins for {option.name} ({option.cost}).");
                    break;
                case PriceType.RewardedAd:
                    RewardedAds.Show(MakeupColorPlacement, () => { if (IsPainting && !_complete) Unlock(index); });
                    break;
                default:
                    Unlock(index);
                    break;
            }
        }

        private void Unlock(int index)
        {
            _unlocked[index] = true;
            Choose(index);
        }

        private void Choose(int index)
        {
            _selected = index;
            _color = _options[index].color;
            ClearCoverage(); // a new colour starts the painting again
            RefreshPalette();
        }

        private void ClearCoverage()
        {
            _paintedSamples = 0;
            _previewDragged = 0f;
            if (_covered != null && _makeup != null)
            {
                for (var i = 0; i < _covered.Length; i++)
                {
                    System.Array.Clear(_covered[i], 0, _covered[i].Length);
                    _counts[i] = 0;
                    _makeup.SetCoverage(_zone, i, _color, 0f);
                }
            }
            SetProgress(0f);
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (!IsPainting || _complete || _dragging || Time.timeScale == 0f) return;
            _pointerId = data.pointerId;
            _dragging = true;
            _lastPoint = data.position;
            if (brush != null) brush.gameObject.SetActive(true);
            ApplyBrush(data);
        }
        public void OnDrag(PointerEventData data)
        {
            if (_dragging && data.pointerId == _pointerId) ApplyBrush(data);
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (data.pointerId != _pointerId) return;
            _dragging = false;
            if (brush != null) brush.gameObject.SetActive(false);
        }

        private static Vector2 Sample(int cell) => new(
            ((cell % GridSize) + 0.5f) / GridSize - 0.5f,
            ((cell / GridSize) + 0.5f) / GridSize - 0.5f);

        private void ApplyBrush(PointerEventData data)
        {
            if (!IsPainting || _complete || Time.timeScale == 0f) return;
            // The brush tip (and the paint) sits a little above the finger.
            var point = data.position + paintOffset * Screen.height;
            if (brush != null && brush.parent is RectTransform parent &&
                RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, point, data.pressEventCamera, out var world))
                brush.position = world;
            if (_preview)
            {
                // UI preview: progress from how far the finger has moved.
                _previewDragged += Vector2.Distance(_lastPoint, data.position) / Mathf.Max(1f, Screen.height);
                _lastPoint = data.position;
                var p = _previewDragged / previewDragToFill;
                SetProgress(p);
                if (p < 1f) return;
                _complete = true;
                _closeAt = Time.time + completionDelay;
                return;
            }

            var ray = renderCamera.ScreenPointToRay(point);
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
            SetProgress(progress / completionCoverage);
            if (progress < completionCoverage) return;
            _complete = true;
            _makeup.Paint(_zone, _color, true);
            _closeAt = Time.time + completionDelay;
        }

        private void Update()
        {
            if ((_covered != null || _preview) && _runner == null) { Cancel(); return; }
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
            if (_follow != null && _runner != null) _follow.SetTarget(_runner.transform); // back to following her
            _follow = null;
            if (_animator != null) _animator.speed = _animationSpeed;
            _animator = null;
            if (_runner != null) _runner.MakeupActive = false;
            _runner = null;
            _makeup = null;
            _covered = null;
            _preview = false;
            _dragging = false;
            _complete = false;
            if (panel != null) panel.SetActive(false);
            if (makeupCamera != null) makeupCamera.enabled = false;
        }
    }
}
