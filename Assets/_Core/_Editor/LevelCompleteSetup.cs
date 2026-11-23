#if UNITY_EDITOR
using System.Linq;
using _Core._UI._Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the special-outfit progress bar (e.g. "Rain Outfit 2/3" → "Rain Outfit UNLOCKED!") into
    /// LevelCompletePanel > Container > Centre, under the outfit match text, and wires it to LevelCompleteViewBehavior
    /// (specialProgressRoot / Fill / Text / Icon / UnlockedObject). Re-runnable: rebuilds its own "SpecialProgress" object.
    /// Menu: Build A Doll > UI > Add Special Progress Bar To Level Complete.
    /// </summary>
    public static class LevelCompleteSetup
    {
        private const string PrefabPath = "Assets/_Core/_UI/_ViewsPrefab/LevelCompletePanel.prefab";
        private const string Title = "Level Complete Setup";

        [MenuItem("Build A Doll/UI/Add Special Progress Bar To Level Complete")]
        public static void AddProgressBar()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var view = root.GetComponentInChildren<LevelCompleteViewBehavior>(true);
                var centre = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Centre");
                if (view == null || centre == null)
                {
                    EditorUtility.DisplayDialog(Title, "LevelCompleteViewBehavior or 'Centre' not found in LevelCompletePanel.prefab.", "OK");
                    return;
                }

                var old = centre.Find("SpecialProgress");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

                var bar = NewUi("SpecialProgress", centre);
                Place(bar, new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(620f, 230f));

                var text = NewUi("Text", bar).gameObject.AddComponent<TextMeshProUGUI>();
                text.text = "Rain Outfit  1/3";
                text.fontSize = 44f;
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(60f, -35f), new Vector2(460f, 60f));

                var icon = NewUi("Icon", bar).gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-225f, -5f), new Vector2(140f, 140f));

                var back = NewUi("BarBack", bar).gameObject.AddComponent<Image>();
                back.sprite = sprite;
                back.type = Image.Type.Sliced;
                back.color = new Color(0.25f, 0.25f, 0.3f, 0.9f);
                back.raycastTarget = false;
                Place(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(60f, -10f), new Vector2(420f, 50f));

                var fill = NewUi("Fill", back.transform).gameObject.AddComponent<Image>();
                fill.sprite = sprite;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                fill.fillAmount = 0.33f;
                fill.color = new Color(0.45f, 0.85f, 0.2f);
                fill.raycastTarget = false;
                var fr = fill.rectTransform;
                fr.anchorMin = Vector2.zero;
                fr.anchorMax = Vector2.one;
                fr.offsetMin = new Vector2(5f, 5f);
                fr.offsetMax = new Vector2(-5f, -5f);

                var unlocked = NewUi("Unlocked", bar).gameObject.AddComponent<TextMeshProUGUI>();
                unlocked.text = "UNLOCKED!";
                unlocked.fontSize = 52f;
                unlocked.fontStyle = FontStyles.Bold;
                unlocked.alignment = TextAlignmentOptions.Center;
                unlocked.color = new Color(1f, 0.8f, 0.15f);
                unlocked.raycastTarget = false;
                Place(unlocked.rectTransform, new Vector2(0.5f, 0f), new Vector2(60f, 30f), new Vector2(460f, 60f));
                unlocked.gameObject.SetActive(false);

                var so = new SerializedObject(view);
                so.FindProperty("specialProgressRoot").objectReferenceValue = bar.gameObject;
                so.FindProperty("specialProgressFill").objectReferenceValue = fill;
                so.FindProperty("specialProgressText").objectReferenceValue = text;
                so.FindProperty("specialIcon").objectReferenceValue = icon;
                so.FindProperty("specialUnlockedObject").objectReferenceValue = unlocked.gameObject;
                so.ApplyModifiedPropertiesWithoutUndo();

                bar.gameObject.SetActive(false); // shown by the view only after levels that count
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                EditorUtility.DisplayDialog(Title, "Added the special progress bar (text, icon, bar, UNLOCKED! badge) to Level Complete and wired it.", "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---------- Reward wheel ----------
        private static readonly Color[] SegmentColors =
        {
            new(1f, 0.45f, 0.75f), new(0.6f, 0.35f, 0.95f), new(1f, 0.8f, 0.2f), new(0.3f, 0.75f, 1f)
        };

        /// <summary>
        /// Builds Centre > SpinWheel (wheel with 8 segments + labels, pin at the top, SPIN (AD) button, result text) under the
        /// special progress bar, adds SpinWheelView and wires it to LevelCompleteViewBehavior.spinWheel. Re-runnable.
        /// Segment labels come from SpinWheelView's Segments (defaults x2 x3 x2 x5 x2 x3 x2 x4).
        /// </summary>
        [MenuItem("Build A Doll/UI/Add Spin Wheel To Level Complete")]
        public static void AddSpinWheel()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var view = root.GetComponentInChildren<LevelCompleteViewBehavior>(true);
                var centre = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Centre");
                if (view == null || centre == null)
                {
                    EditorUtility.DisplayDialog(Title, "LevelCompleteViewBehavior or 'Centre' not found in LevelCompletePanel.prefab.", "OK");
                    return;
                }

                var old = centre.Find("SpinWheel");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                var box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

                var holder = NewUi("SpinWheel", centre);
                Place(holder, new Vector2(0.5f, 1f), new Vector2(0f, -545f), new Vector2(620f, 470f));
                var spin = holder.gameObject.AddComponent<SpinWheelView>();

                // Wheel (rotates) with the segments; labels from the component's segment list.
                var wheel = NewUi("Wheel", holder);
                Place(wheel, new Vector2(0.5f, 0.5f), new Vector2(0f, 45f), new Vector2(330f, 330f));
                var segments = spin.Segments;
                var count = Mathf.Max(1, segments.Count);
                var step = 360f / count;
                for (var i = 0; i < count; i++)
                {
                    var seg = NewUi($"Segment {i + 1}", wheel).gameObject.AddComponent<Image>();
                    seg.sprite = knob;
                    seg.type = Image.Type.Filled;
                    seg.fillMethod = Image.FillMethod.Radial360;
                    seg.fillOrigin = (int)Image.Origin360.Top;
                    seg.fillClockwise = true;
                    seg.fillAmount = 1f / count;
                    seg.color = SegmentColors[i % SegmentColors.Length];
                    seg.raycastTarget = false;
                    var sr = seg.rectTransform;
                    sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = sr.offsetMax = Vector2.zero;
                    sr.localEulerAngles = new Vector3(0f, 0f, step * 0.5f - i * step); // centred i × step clockwise from the top

                    var angle = i * step * Mathf.Deg2Rad;
                    var label = NewUi($"Label {i + 1}", wheel).gameObject.AddComponent<TextMeshProUGUI>();
                    label.text = segments.Count > i ? segments[i].label : "";
                    label.fontSize = 36f;
                    label.fontStyle = FontStyles.Bold;
                    label.alignment = TextAlignmentOptions.Center;
                    label.color = Color.white;
                    label.raycastTarget = false;
                    Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * 110f, new Vector2(90f, 50f));
                    label.rectTransform.localEulerAngles = new Vector3(0f, 0f, -i * step);
                }

                var cap = NewUi("Centre Cap", wheel).gameObject.AddComponent<Image>();
                cap.sprite = knob;
                cap.color = Color.white;
                cap.raycastTarget = false;
                Place(cap.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));

                // Pin at the top (doesn't rotate).
                var pin = NewUi("Pin", holder).gameObject.AddComponent<Image>();
                pin.sprite = box;
                pin.color = new Color(0.9f, 0.15f, 0.2f);
                pin.raycastTarget = false;
                Place(pin.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 45f + 165f + 4f), new Vector2(36f, 36f));
                pin.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);

                // SPIN (AD) button.
                var buttonRt = NewUi("SpinButton", holder);
                Place(buttonRt, new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(280f, 80f));
                var buttonImage = buttonRt.gameObject.AddComponent<Image>();
                buttonImage.sprite = box;
                buttonImage.type = Image.Type.Sliced;
                buttonImage.color = new Color(0.35f, 0.8f, 0.25f);
                var button = buttonRt.gameObject.AddComponent<Button>();
                button.targetGraphic = buttonImage;
                var buttonText = NewUi("Text", buttonRt).gameObject.AddComponent<TextMeshProUGUI>();
                buttonText.text = "SPIN (AD)";
                buttonText.fontSize = 40f;
                buttonText.fontStyle = FontStyles.Bold;
                buttonText.alignment = TextAlignmentOptions.Center;
                buttonText.color = Color.white;
                buttonText.raycastTarget = false;
                var bt = buttonText.rectTransform;
                bt.anchorMin = Vector2.zero; bt.anchorMax = Vector2.one; bt.offsetMin = bt.offsetMax = Vector2.zero;

                var result = NewUi("Result", holder).gameObject.AddComponent<TextMeshProUGUI>();
                result.text = "x2!  +100";
                result.fontSize = 40f;
                result.fontStyle = FontStyles.Bold;
                result.alignment = TextAlignmentOptions.Center;
                result.color = new Color(1f, 0.85f, 0.2f);
                result.raycastTarget = false;
                Place(result.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -232f), new Vector2(500f, 44f));
                result.gameObject.SetActive(false);

                var so = new SerializedObject(spin);
                so.FindProperty("wheel").objectReferenceValue = wheel;
                so.FindProperty("spinButton").objectReferenceValue = button;
                so.FindProperty("spinButtonText").objectReferenceValue = buttonText;
                so.FindProperty("resultText").objectReferenceValue = result;
                so.FindProperty("spinLabel").stringValue = "SPIN (AD)";
                so.ApplyModifiedPropertiesWithoutUndo();

                var vo = new SerializedObject(view);
                vo.FindProperty("spinWheel").objectReferenceValue = spin;
                vo.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                EditorUtility.DisplayDialog(Title, $"Added the reward wheel ({count} segments, pin, SPIN (AD) button) to Level Complete and wired it.", "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static RectTransform NewUi(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }
    }
}
#endif
