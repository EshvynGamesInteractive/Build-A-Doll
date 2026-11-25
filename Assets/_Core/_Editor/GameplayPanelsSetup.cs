#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using _Core._UI;
using _Core._UI._LevelMap;
using _Core._UI._Views;
using _Core.UI;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Build A Doll > UI > Split Gameplay UI Into Panels (25 Nov 2026). Re-runnable; only moves / wires objects that are
    /// already built in the prefabs (no new art).
    ///
    /// 1. GameplayMenu is split into its own screens, like Pause / Settings:
    ///    • TargetLookPanel.prefab  ← DemoImageContainer (card, Frame, PLAY, Settings, title, coins) + TargetLookViewBehavior
    ///    • MakeupPanel.prefab      ← MakeupKitPaletteContainer + PaintToolView (moved) + MakeupPanelViewBehavior
    ///    • ComparisonPanel.prefab  ← PlayerLabel, AiLabel, InputBlocker + ComparisonHud (moved) + ComparisonViewBehavior
    ///    GameplayMenu keeps the run HUD (Pause, CoinBar, ScorePopup) and gets TargetPos > TargetThumb (copy of Frame).
    /// 2. The three panels are added to Canvas.prefab and to UIManager → Screens.
    /// 3. Missing wiring: CoinBar prefab → CoinBarView; HomeMenu level map → 30 nodes; Level Complete → reward arc
    ///    (SpinWheel / Needle / Multiplier), Get button = Next, completion coins label; makeup palette → all 3 buttons.
    /// </summary>
    public static class GameplayPanelsSetup
    {
        private const string Title = "Gameplay UI Panels";
        private const string Folder = "Assets/_Core/_UI/_ViewsPrefab";
        private const string GameplayPath = Folder + "/GameplayMenu.prefab";
        private const string TargetLookPath = Folder + "/TargetLookPanel.prefab";
        private const string MakeupPath = Folder + "/MakeupPanel.prefab";
        private const string ComparisonPath = Folder + "/ComparisonPanel.prefab";
        private const string CanvasPath = Folder + "/Canvas.prefab";
        private const string CoinBarPath = Folder + "/CoinBar.prefab";
        private const string HomePath = Folder + "/HomeMenu.prefab";
        private const string LevelCompletePath = Folder + "/LevelCompletePanel.prefab";

        [MenuItem("Build A Doll/UI/Split Gameplay UI Into Panels")]
        public static void Run()
        {
            var log = new StringBuilder();
            SplitGameplayMenu(log);
            AssetDatabase.Refresh();
            AddPanelsToCanvas(log);
            WireCoinBar(log);
            WireHomeMenu(log);
            WireLevelComplete(log);
            AssetDatabase.SaveAssets();

            Debug.Log("[GameplayPanelsSetup]\n" + log);
            EditorUtility.DisplayDialog(Title, log.ToString(), "OK");
        }

        // ---------------------------------------------------------------- 1. Split

        private static void SplitGameplayMenu(StringBuilder log)
        {
            var root = PrefabUtility.LoadPrefabContents(GameplayPath);
            try
            {
                var hud = root.GetComponent<GameplayMenuViewBehavior>();
                var container = hud != null && hud.container != null ? hud.container.transform : root.transform.Find("Container");
                if (container == null)
                {
                    log.AppendLine("GameplayMenu: no Container found, nothing split.");
                    return;
                }

                var thumb = SplitTargetLook(root, container, log);
                SplitMakeup(root, container, log);
                SplitComparison(root, container, log);

                if (hud != null)
                {
                    var so = new SerializedObject(hud);
                    if (thumb != null)
                        so.FindProperty("targetThumb").objectReferenceValue = thumb;
                    // The CoinBar shows the wallet by itself now (CoinBarView); "Coins 123" from here would fight it.
                    so.FindProperty("coinsText").objectReferenceValue = null;
                    if (so.FindProperty("pauseButton").objectReferenceValue == null)
                        so.FindProperty("pauseButton").objectReferenceValue = Find<Button>(container, "Pause");
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, GameplayPath);
                log.AppendLine("GameplayMenu: now HUD only (Pause, CoinBar, ScorePopup, TargetPos > TargetThumb).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject SplitTargetLook(GameObject root, Transform container, StringBuilder log)
        {
            var targetPos = container.Find("TargetPos") as RectTransform;
            var existingThumb = targetPos != null ? targetPos.Find("TargetThumb") : null;
            var card = container.Find("DemoImageContainer") as RectTransform;
            if (card == null)
            {
                log.AppendLine("TargetLookPanel: already split (no DemoImageContainer in GameplayMenu).");
                return existingThumb != null ? existingThumb.gameObject : null;
            }

            var frame = card.Find("Frame") as RectTransform;

            // HUD copy of the frame, sitting exactly where the card's frame lands (centre of TargetPos, TargetPos width).
            GameObject thumb = existingThumb != null ? existingThumb.gameObject : null;
            if (thumb == null && targetPos != null && frame != null)
            {
                thumb = Object.Instantiate(frame.gameObject, targetPos, false);
                thumb.name = "TargetThumb";
                var rt = (RectTransform)thumb.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = frame.sizeDelta;
                rt.localRotation = Quaternion.identity;
                var scale = frame.sizeDelta.x > 0.01f ? targetPos.sizeDelta.x / frame.sizeDelta.x : 1f;
                rt.localScale = new Vector3(scale, scale, 1f);
                foreach (var g in thumb.GetComponentsInChildren<Graphic>(true))
                    g.raycastTarget = false;
                thumb.SetActive(false);
            }

            var panel = NewPanelRoot("TargetLookPanel", root);
            card.SetParent(panel, false);

            // The panel's own copy of TargetPos (same anchors), where the frame flies to.
            RectTransform frameTarget = null;
            if (targetPos != null)
            {
                var copy = new GameObject("FrameTarget", typeof(RectTransform));
                frameTarget = (RectTransform)copy.transform;
                frameTarget.SetParent(panel, false);
                frameTarget.anchorMin = targetPos.anchorMin;
                frameTarget.anchorMax = targetPos.anchorMax;
                frameTarget.pivot = targetPos.pivot;
                frameTarget.anchoredPosition = targetPos.anchoredPosition;
                frameTarget.sizeDelta = targetPos.sizeDelta;
                copy.layer = card.gameObject.layer;
            }

            var view = panel.gameObject.AddComponent<TargetLookViewBehavior>();
            SetScreen(view, ScreenType.TargetLook, card.gameObject);
            var so = new SerializedObject(view);
            so.FindProperty("card").objectReferenceValue = card;
            so.FindProperty("frame").objectReferenceValue = frame;
            so.FindProperty("cardBackground").objectReferenceValue = card.GetComponent<Image>();
            so.FindProperty("frameTarget").objectReferenceValue = frameTarget;
            so.FindProperty("playButton").objectReferenceValue = Find<Button>(card, "PlayBtn");
            so.FindProperty("settingsButton").objectReferenceValue = Find<Button>(card, "SettingButton");
            so.FindProperty("briefText").objectReferenceValue = Find<TMP_Text>(card, "Image/Text (TMP)");
            var cardOnly = new List<GameObject>();
            foreach (var n in new[] { "PlayBtn", "SettingButton", "Image", "Light" })
                if (card.Find(n) != null) cardOnly.Add(card.Find(n).gameObject);
            foreach (Transform child in card)
                if (child.name.StartsWith("CoinBar")) cardOnly.Add(child.gameObject);
            SetArray(so.FindProperty("cardOnlyObjects"), cardOnly);
            so.ApplyModifiedPropertiesWithoutUndo();

            card.gameObject.SetActive(false);
            SavePanel(panel, TargetLookPath);
            log.AppendLine($"TargetLookPanel: created (card, Frame → FrameTarget, PLAY, Settings, {cardOnly.Count} card-only objects). " +
                           "Fixed: the frame that flies is now Frame (was the Light glow).");
            return thumb;
        }

        private static void SplitMakeup(GameObject root, Transform container, StringBuilder log)
        {
            var palette = container.Find("MakeupKitPaletteContainer");
            if (palette == null)
            {
                log.AppendLine("MakeupPanel: already split (no MakeupKitPaletteContainer in GameplayMenu).");
                return;
            }

            var oldTool = container.GetComponent<PaintToolView>() ?? root.GetComponent<PaintToolView>();
            var panel = NewPanelRoot("MakeupPanel", root);
            palette.SetParent(panel, false);

            var tool = panel.gameObject.AddComponent<PaintToolView>();
            if (oldTool != null)
            {
                EditorUtility.CopySerialized(oldTool, tool);
                Object.DestroyImmediate(oldTool);
            }

            var so = new SerializedObject(tool);
            so.FindProperty("panel").objectReferenceValue = palette.gameObject;
            so.FindProperty("openThroughUIManager").boolValue = true;
            if (so.FindProperty("instruction").objectReferenceValue == null)
                so.FindProperty("instruction").objectReferenceValue = Find<TMP_Text>(palette, "Image/Title");
            if (so.FindProperty("progressSlider").objectReferenceValue == null)
                so.FindProperty("progressSlider").objectReferenceValue = Find<Slider>(palette, "ProgressBar");
            if (so.FindProperty("brush").objectReferenceValue == null)
                so.FindProperty("brush").objectReferenceValue = palette.Find("Brush");
            var wired = WirePalette(so, palette);
            so.ApplyModifiedPropertiesWithoutUndo();

            var view = panel.gameObject.AddComponent<MakeupPanelViewBehavior>();
            SetScreen(view, ScreenType.MakeupPanel, palette.gameObject);
            var vso = new SerializedObject(view);
            vso.FindProperty("paintTool").objectReferenceValue = tool;
            vso.FindProperty("restartButton").objectReferenceValue = Find<Button>(palette, "Button");
            vso.ApplyModifiedPropertiesWithoutUndo();

            palette.gameObject.SetActive(false);
            SavePanel(panel, MakeupPath);
            log.AppendLine($"MakeupPanel: created (PaintToolView moved, {wired} colour buttons wired, retry button wired).");
        }

        private static void SplitComparison(GameObject root, Transform container, StringBuilder log)
        {
            var player = container.Find("PlayerLabel");
            var ai = container.Find("AiLabel");
            var blocker = container.Find("InputBlocker");
            if (player == null && ai == null && blocker == null)
            {
                log.AppendLine("ComparisonPanel: already split (no labels / InputBlocker in GameplayMenu).");
                return;
            }

            var oldHud = root.GetComponent<ComparisonHud>();
            var panel = NewPanelRoot("ComparisonPanel", root);
            var inner = new GameObject("Container", typeof(RectTransform));
            inner.layer = panel.gameObject.layer;
            var innerRect = (RectTransform)inner.transform;
            innerRect.SetParent(panel, false);
            Stretch(innerRect);
            foreach (var t in new[] { player, ai, blocker })
                if (t != null) t.SetParent(innerRect, false);
            if (blocker != null) blocker.SetAsLastSibling();

            var hud = panel.gameObject.AddComponent<ComparisonHud>();
            if (oldHud != null)
            {
                EditorUtility.CopySerialized(oldHud, hud);
                Object.DestroyImmediate(oldHud);
            }
            var hso = new SerializedObject(hud);
            if (player != null) hso.FindProperty("playerLabel").objectReferenceValue = player.GetComponent<ComparisonLabel>();
            if (ai != null) hso.FindProperty("aiLabel").objectReferenceValue = ai.GetComponent<ComparisonLabel>();
            if (blocker != null) hso.FindProperty("inputBlocker").objectReferenceValue = blocker.gameObject;
            hso.ApplyModifiedPropertiesWithoutUndo();

            var view = panel.gameObject.AddComponent<ComparisonViewBehavior>();
            SetScreen(view, ScreenType.ComparisonPanel, inner);

            inner.SetActive(false);
            SavePanel(panel, ComparisonPath);
            log.AppendLine("ComparisonPanel: created (YOU / AI labels + InputBlocker, ComparisonHud moved).");
        }

        // ---------------------------------------------------------------- 2. Canvas

        private static void AddPanelsToCanvas(StringBuilder log)
        {
            var root = PrefabUtility.LoadPrefabContents(CanvasPath);
            try
            {
                var manager = root.GetComponent<UIManager>();
                if (manager == null)
                {
                    log.AppendLine("Canvas: no UIManager, panels not added.");
                    return;
                }

                var gameplay = root.GetComponentsInChildren<GameplayMenuViewBehavior>(true).FirstOrDefault();
                var sibling = gameplay != null ? gameplay.transform.GetSiblingIndex() + 1 : root.transform.childCount;
                var so = new SerializedObject(manager);
                var list = so.FindProperty("screens");
                var added = new List<string>();

                foreach (var path in new[] { TargetLookPath, MakeupPath, ComparisonPath })
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset == null)
                        continue;

                    var instance = root.transform.Cast<Transform>()
                        .FirstOrDefault(t => PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) == asset);
                    if (instance == null)
                    {
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
                        instance = go.transform;
                        Stretch((RectTransform)instance);
                        added.Add(asset.name);
                    }
                    instance.SetSiblingIndex(Mathf.Min(sibling++, root.transform.childCount - 1));

                    var screen = instance.GetComponent<UIScreen>();
                    var inList = false;
                    for (var i = 0; i < list.arraySize; i++)
                        if (list.GetArrayElementAtIndex(i).objectReferenceValue == screen) inList = true;
                    if (!inList && screen != null)
                    {
                        list.arraySize++;
                        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = screen;
                    }
                }

                // Drop empty slots.
                for (var i = list.arraySize - 1; i >= 0; i--)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == null)
                        list.DeleteArrayElementAtIndex(i);

                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, CanvasPath);
                log.AppendLine(added.Count > 0
                    ? $"Canvas: added {string.Join(", ", added)} and put them in UIManager → Screens ({list.arraySize} screens)."
                    : $"Canvas: panels already there ({list.arraySize} screens).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---------------------------------------------------------------- 3. Missing wiring

        private static void WireCoinBar(StringBuilder log)
        {
            var root = PrefabUtility.LoadPrefabContents(CoinBarPath);
            try
            {
                var view = root.GetComponent<CoinBarView>() ?? root.AddComponent<CoinBarView>();
                var so = new SerializedObject(view);
                so.FindProperty("coinsText").objectReferenceValue = Find<TMP_Text>(root.transform, "CoinsText");
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, CoinBarPath);
                log.AppendLine("CoinBar: CoinBarView added (every coin bar shows the wallet).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void WireHomeMenu(StringBuilder log)
        {
            var root = PrefabUtility.LoadPrefabContents(HomePath);
            try
            {
                var map = root.GetComponent<LevelMapView>() ?? root.GetComponentInChildren<LevelMapView>(true);
                if (map == null)
                {
                    log.AppendLine("HomeMenu: no LevelMapView.");
                    return;
                }

                // Node (1) … Node (30): first = earliest level (the layout's Reverse Arrangement puts it at the bottom).
                var nodes = root.GetComponentsInChildren<LevelNode>(true)
                    .OrderBy(n => NodeNumber(n.name, n.transform.GetSiblingIndex()))
                    .ToList();
                var so = new SerializedObject(map);
                SetArray(so.FindProperty("nodes"), nodes);
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, HomePath);
                log.AppendLine($"HomeMenu: level map wired to {nodes.Count} nodes (the list was empty, so level buttons did nothing).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void WireLevelComplete(StringBuilder log)
        {
            var root = PrefabUtility.LoadPrefabContents(LevelCompletePath);
            try
            {
                var view = root.GetComponent<LevelCompleteViewBehavior>();
                var container = view != null && view.container != null ? view.container.transform : root.transform.Find("Container");
                if (view == null || container == null)
                {
                    log.AppendLine("LevelCompletePanel: view or Container missing.");
                    return;
                }

                var so = new SerializedObject(view);
                var notes = new List<string>();

                var wheel = container.Find("SpinWheel");
                var multiplier = Find<Button>(container, "Multiplier");
                if (wheel != null)
                {
                    var arc = wheel.GetComponent<RewardMultiplierView>() ?? wheel.gameObject.AddComponent<RewardMultiplierView>();
                    var aso = new SerializedObject(arc);
                    var needle = wheel.Find("Needle") as RectTransform;
                    aso.FindProperty("needle").objectReferenceValue = needle;
                    aso.FindProperty("claimButton").objectReferenceValue = multiplier;
                    aso.FindProperty("rewardText").objectReferenceValue = multiplier != null ? Find<TMP_Text>(multiplier.transform, "CoinsText") : null;
                    aso.ApplyModifiedPropertiesWithoutUndo();
                    if (needle != null && needle.GetComponent<Graphic>() != null)
                        needle.GetComponent<Graphic>().raycastTarget = false;
                    so.FindProperty("rewardMultiplier").objectReferenceValue = arc;
                    notes.Add("reward arc (pin swing + ad button)");
                }

                var get = Find<Button>(container, "GetButton");
                if (get != null)
                {
                    if (so.FindProperty("nextLevelButton").objectReferenceValue == null)
                        so.FindProperty("nextLevelButton").objectReferenceValue = get;
                    so.FindProperty("completionCoinsText").objectReferenceValue = Find<TMP_Text>(get.transform, "BasicCoins");
                    notes.Add("Get button = Next + completion coins label");
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, LevelCompletePath);
                log.AppendLine(notes.Count > 0 ? "LevelCompletePanel: wired " + string.Join(", ", notes) + "." : "LevelCompletePanel: nothing to wire.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---------------------------------------------------------------- Palette

        /// <summary>Team palette button (Layout > Free / Coins / AdWatch): PaintColor = swatch, Text (TMP) = price,
        /// icon Image (coin / video) shown while locked. Free keeps its own "Free" label and tick.</summary>
        internal static PaintToolView.PaletteButton TeamPaletteButton(Button button)
        {
            var t = button.transform;
            var free = button.name.StartsWith("Free");
            var swatch = t.Find("PaintColor") != null ? t.Find("PaintColor").GetComponent<Image>() : button.GetComponent<Image>();
            return new PaintToolView.PaletteButton
            {
                button = button,
                swatch = swatch,
                priceText = free ? null : Find<TMP_Text>(t, "Text (TMP)"),
                priceRoot = free || t.Find("Image") == null ? null : t.Find("Image").gameObject
            };
        }

        private static int WirePalette(SerializedObject tool, Transform palette)
        {
            var layout = palette.Find("Layout");
            if (layout == null)
                return 0;

            var buttons = layout.Cast<Transform>().Select(c => c.GetComponent<Button>()).Where(b => b != null).ToArray();
            var list = tool.FindProperty("paletteButtons");
            list.arraySize = buttons.Length;
            for (var i = 0; i < buttons.Length; i++)
            {
                var p = TeamPaletteButton(buttons[i]);
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("button").objectReferenceValue = p.button;
                e.FindPropertyRelative("swatch").objectReferenceValue = p.swatch;
                e.FindPropertyRelative("priceText").objectReferenceValue = p.priceText;
                e.FindPropertyRelative("priceRoot").objectReferenceValue = p.priceRoot;
                e.FindPropertyRelative("selected").objectReferenceValue = null;
                e.FindPropertyRelative("adRoot").objectReferenceValue = null;
                // Clicks are added by PaintToolView.Awake: drop Inspector calls to the old (moved) PaintToolView so nothing fires twice.
                for (var c = p.button.onClick.GetPersistentEventCount() - 1; c >= 0; c--)
                {
                    var target = p.button.onClick.GetPersistentTarget(c);
                    if (target == null || target is PaintToolView)
                        UnityEditor.Events.UnityEventTools.RemovePersistentListener(p.button.onClick, c);
                }
            }
            return buttons.Length;
        }

        // ---------------------------------------------------------------- Helpers

        private static RectTransform NewPanelRoot(string name, GameObject prefabRoot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = prefabRoot.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(prefabRoot.transform, false); // moves it into the prefab's editing scene
            Stretch(rt);
            return rt;
        }

        private static void SavePanel(RectTransform panel, string path)
        {
            panel.SetParent(null, false);
            Stretch(panel);
            PrefabUtility.SaveAsPrefabAsset(panel.gameObject, path);
            Object.DestroyImmediate(panel.gameObject);
        }

        private static void SetScreen(UIScreen screen, ScreenType type, GameObject container)
        {
            var so = new SerializedObject(screen);
            so.FindProperty("screenType").intValue = (int)type;
            so.FindProperty("container").objectReferenceValue = container;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        private static void SetArray<T>(SerializedProperty array, IList<T> items) where T : Object
        {
            array.arraySize = items.Count;
            for (var i = 0; i < items.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }

        private static T Find<T>(Transform parent, string path) where T : Component
        {
            var t = parent != null ? parent.Find(path) : null;
            return t != null ? t.GetComponent<T>() : null;
        }

        private static int NodeNumber(string name, int fallback)
        {
            var m = Regex.Match(name, @"\((\d+)\)");
            return m.Success ? int.Parse(m.Groups[1].Value) : 1000 + fallback;
        }
    }
}
#endif
