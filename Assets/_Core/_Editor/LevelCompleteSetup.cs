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
    /// Builds the special-outfit progress bar (e.g. "Umbrella 2/3" → "Umbrella UNLOCKED!") into
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
                text.text = "Umbrella  1/3";
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
