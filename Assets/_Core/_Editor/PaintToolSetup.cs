#if UNITY_EDITOR
using System.Linq;
using Game.UI;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Wires the team's makeup palette UI (GameplayMenu > Container > MakeupKitPaletteContainer) to PaintToolView and
    /// lays it out like the reference: "Apply Eye shadow" title, green progress bar under it, the 3D face behind
    /// (transparent panel that still takes the drag), a long brush whose tip follows the finger, 3 big colour buttons
    /// at the bottom with a price label ("500" / "FREE" for the ad one).
    /// Re-runnable. Only touches objects inside MakeupKitPaletteContainer (adds a Title TMP if there isn't one).
    /// Menu: Build A Doll > Paint Tool > Set Up Gameplay Palette UI.
    /// </summary>
    public static class PaintToolSetup
    {
        private const string Title = "Paint Tool Setup";
        private const string PrefabPath = "Assets/_Core/_UI/_ViewsPrefab/GameplayMenu.prefab";
        private const string PanelName = "MakeupKitPaletteContainer";

        private static readonly Color TitleColor = new(1f, 0.72f, 0.16f);
        private static readonly Color BarFill = new(0.45f, 0.85f, 0.2f);
        private static readonly Color BarBack = new(0.85f, 0.85f, 0.85f);
        private static readonly Color BrushColor = new(0.13f, 0.2f, 0.3f);

        [MenuItem("Build A Doll/Paint Tool/Set Up Gameplay Palette UI")]
        public static void SetUpUi()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var panel = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == PanelName);
                if (panel == null)
                {
                    EditorUtility.DisplayDialog(Title, $"{PanelName} not found in GameplayMenu.prefab.", "OK");
                    return;
                }

                // PaintToolView on the always-active parent (the panel itself is hidden between sessions).
                var host = panel.parent != null ? panel.parent.gameObject : root;
                var view = host.GetComponent<PaintToolView>();
                if (view == null) view = host.AddComponent<PaintToolView>();

                // Drawn over the HUD (but under the comparison's InputBlocker, which stays last).
                panel.SetAsLastSibling();
                var blocker = host.transform.Find("InputBlocker");
                if (blocker != null) blocker.SetAsLastSibling();

                // Panel: transparent, full screen, still catches the drag (the 3D face shows through).
                var panelImage = panel.GetComponent<Image>();
                if (panelImage != null)
                {
                    panelImage.color = new Color(0f, 0f, 0f, 0f);
                    panelImage.raycastTarget = true;
                }
                Stretch(panel as RectTransform);

                // Title at the top.
                var title = panel.Find("Title")?.GetComponent<TextMeshProUGUI>();
                if (title == null)
                {
                    var go = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    go.transform.SetParent(panel, false);
                    title = go.GetComponent<TextMeshProUGUI>();
                }
                title.text = "Apply Eye shadow";
                title.fontSize = 80f;
                title.fontStyle = FontStyles.Bold;
                title.alignment = TextAlignmentOptions.Center;
                title.color = TitleColor;
                title.raycastTarget = false;
                // Left of the target-look picture (top-right corner, 320×460), so they don't overlap.
                title.fontSize = 70f;
                Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(-180f, -250f), new Vector2(660f, 110f));

                // Progress bar under the title (Slider, not touchable).
                var slider = panel.Find("ProgressBar")?.GetComponent<Slider>();
                if (slider != null)
                {
                    slider.interactable = false;
                    slider.transition = Selectable.Transition.None;
                    slider.minValue = 0f;
                    slider.maxValue = 1f;
                    slider.value = 0f;
                    Place(slider.transform as RectTransform, new Vector2(0.5f, 1f), new Vector2(-180f, -350f), new Vector2(600f, 55f));
                    var handleArea = slider.transform.Find("Handle Slide Area");
                    if (handleArea != null) handleArea.gameObject.SetActive(false);
                    var back = slider.transform.Find("Background")?.GetComponent<Image>();
                    if (back != null) { back.color = BarBack; Stretch(back.rectTransform); }
                    var fillArea = slider.transform.Find("Fill Area") as RectTransform;
                    if (fillArea != null) Stretch(fillArea, 8f);
                    var fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
                    if (fill != null) fill.color = BarFill;
                    foreach (var g in slider.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
                }

                // Face icon: decorative, never blocks the drag.
                // Face icon: decorative, never blocks the drag; hidden while it has no sprite (it showed as a white square).
                var face = panel.Find("FaceIcon")?.GetComponent<Image>();
                if (face != null)
                {
                    face.raycastTarget = false;
                    face.gameObject.SetActive(face.sprite != null);
                }

                // Brush: long handle, tip at the pivot (top), follows the finger (shown while dragging).
                var brush = panel.Find("Brush") as RectTransform;
                if (brush != null)
                {
                    var img = brush.GetComponent<Image>();
                    if (img != null)
                    {
                        img.raycastTarget = false;
                        if (img.sprite == null) img.color = BrushColor;
                    }
                    brush.anchorMin = brush.anchorMax = new Vector2(0.5f, 0.5f);
                    brush.pivot = new Vector2(0.5f, 1f);
                    brush.sizeDelta = new Vector2(70f, 520f);
                    brush.SetAsLastSibling();
                }

                // Colour buttons: 3 big squares along the bottom, label under the colour.
                var buttons = Enumerable.Range(1, 3)
                    .Select(i => panel.Find($"ColorButton ({i})")?.GetComponent<Button>())
                    .Where(b => b != null).ToArray();
                var palette = new PaintToolView.PaletteButton[buttons.Length];
                for (var i = 0; i < buttons.Length; i++)
                {
                    var b = buttons[i];
                    var rt = (RectTransform)b.transform;
                    var x = (i - (buttons.Length - 1) * 0.5f) * 270f;
                    Place(rt, new Vector2(0.5f, 0f), new Vector2(x, 380f), new Vector2(230f, 230f));
                    var swatch = b.GetComponent<Image>();
                    var label = b.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (label != null)
                    {
                        label.text = "";
                        label.fontSize = 52f;
                        label.fontStyle = FontStyles.Bold;
                        label.alignment = TextAlignmentOptions.Bottom;
                        label.color = Color.white;
                        label.raycastTarget = false;
                        label.enableAutoSizing = false;
                        var lr = label.rectTransform;
                        lr.anchorMin = new Vector2(0f, 0f);
                        lr.anchorMax = new Vector2(1f, 0f);
                        lr.pivot = new Vector2(0.5f, 0f);
                        lr.anchoredPosition = new Vector2(0f, 8f);
                        lr.sizeDelta = new Vector2(0f, 70f);
                    }

                    palette[i] = new PaintToolView.PaletteButton
                    {
                        button = b,
                        swatch = swatch,
                        priceText = label,
                        priceRoot = label != null ? label.gameObject : null
                    };
                }

                // Hand the references to PaintToolView.
                var so = new SerializedObject(view);
                so.FindProperty("panel").objectReferenceValue = panel.gameObject;
                so.FindProperty("instruction").objectReferenceValue = title;
                so.FindProperty("progressSlider").objectReferenceValue = slider;
                so.FindProperty("brush").objectReferenceValue = brush;
                var list = so.FindProperty("paletteButtons");
                list.arraySize = palette.Length;
                for (var i = 0; i < palette.Length; i++)
                {
                    var e = list.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("button").objectReferenceValue = palette[i].button;
                    e.FindPropertyRelative("swatch").objectReferenceValue = palette[i].swatch;
                    e.FindPropertyRelative("priceText").objectReferenceValue = palette[i].priceText;
                    e.FindPropertyRelative("priceRoot").objectReferenceValue = palette[i].priceRoot;
                    e.FindPropertyRelative("selected").objectReferenceValue = null;
                    e.FindPropertyRelative("adRoot").objectReferenceValue = null;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                panel.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[PaintToolSetup] PaintToolView on '{host.name}' wired to {PanelName}: title, progress slider, brush, {palette.Length} colour buttons.");
                EditorUtility.DisplayDialog(Title,
                    $"Done: PaintToolView added to '{host.name}' and wired to {PanelName} ({palette.Length} colour buttons).\n\n" +
                    "Next: open the Gameplay scene and run Build A Doll > Paint Tool > Add Makeup Camera To Open Scene.", "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Close-up camera for the face, found by PaintToolView by name ("CM Makeup").
        [MenuItem("Build A Doll/Paint Tool/Add Makeup Camera To Open Scene")]
        public static void AddMakeupCamera()
        {
            var existing = Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.name == "CM Makeup");
            if (existing != null)
            {
                EditorUtility.DisplayDialog(Title, "The open scene already has a 'CM Makeup' camera.", "OK");
                Selection.activeObject = existing;
                return;
            }

            var go = new GameObject("CM Makeup");
            Undo.RegisterCreatedObjectUndo(go, "Add Makeup Camera");
            var cam = go.AddComponent<CinemachineCamera>();
            var lens = cam.Lens;
            if (Camera.main != null) lens = LensSettings.FromCamera(Camera.main);
            lens.FieldOfView = 35f;
            cam.Lens = lens;
            var priority = cam.Priority;
            priority.Enabled = true;
            priority.Value = 100;
            cam.Priority = priority;
            cam.enabled = false; // PaintToolView turns it on during makeup and places it in front of the face

            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeObject = go;
            EditorUtility.DisplayDialog(Title, "Added 'CM Makeup' (priority 100, disabled) to the open scene. Save the scene.", "OK");
        }

        private static void Stretch(RectTransform rt, float inset = 0f)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(-inset * 2f, -inset * 2f);
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            if (rt == null) return;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }
    }
}
#endif
