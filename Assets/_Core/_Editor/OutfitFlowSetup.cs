#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using _Core._UI._Views;
using Game.AI;
using Game.Level;
using Game.Outfit;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Outfit flow setup (23 Nov 2026). Menus:
    /// - Build A Doll > Levels > Apply Outfit Flow Plan: main track order (tutorial 1–3, five 3-level unlock loops, extra
    ///   levels after), the five outfit rewards with their free style levels, tutorial clean-up, specials turned off.
    ///   Re-runnable; free style gates are only re-pointed, never duplicated.
    /// - Build A Doll > UI > Add Outfit Claim Popup To Level Complete
    /// - Build A Doll > UI > Add Gift Box To Home
    /// UI is built here in the editor and saved into the prefabs; nothing is created from code at runtime.
    /// </summary>
    public static class OutfitFlowSetup
    {
        private const string LevelsFolder = "Assets/_Core/_Data/_Levels/";
        private const string DatabasePath = LevelsFolder + "LevelDatabase.asset";
        private const string OutfitsFolder = "Assets/_Core/_Data/_Outfits/";
        private const string AiFolder = "Assets/_Core/_Data/_AI/";
        private const string ColorGatePath = "Assets/_Core/_Prefabs/_Hurdles/_Gates/DressColorGatePair.prefab";
        private const string LevelCompletePath = "Assets/_Core/_UI/_ViewsPrefab/LevelCompletePanel.prefab";
        private const string HomeMenuPath = "Assets/_Core/_UI/_ViewsPrefab/HomeMenu.prefab";
        private const string Title = "Outfit Flow Setup";

        // Main track: tutorials, then 3 levels per reward, then the levels left over (old specials last, as bonus levels).
        private static readonly string[] MainTrack =
        {
            "Level_01", "Level_02", "Level_03",              // tutorial: steer, jump, slide
            "Level_04", "Level_05", "Level_06",              // loop 1 → Rain
            "Level_07", "Level_08", "Level_10",              // loop 2 → Nurse
            "Level_11", "Level_12", "Level_14",              // loop 3 → Police
            "Level_15", "Level_16", "Level_18",              // loop 4 → Maid
            "Level_19", "Level_20", "Level_22",              // loop 5 → Basketball
            "Level_23", "Level_24",                          // after the last reward
            "Level_09", "Level_13", "Level_17", "Level_21", "Level_25", // former special levels, now regular
        };

        private struct Reward
        {
            public string variant, title, freeStyle, freeStyleName, brief, copyFrom;
            public Color colorA, colorB;
            public string colorAName, colorBName;
        }

        private static readonly Reward[] Rewards =
        {
            new() { variant = "Rain/Rain_V01", title = "Rain Outfit", freeStyle = "Level_FreeStyle_2", freeStyleName = "Free Style: Rainy Day", brief = "FREE STYLE! Style your RAIN look!",
                    colorAName = "Yellow", colorA = new Color(1f, 0.85f, 0.25f), colorBName = "Blue", colorB = new Color(0.4f, 0.65f, 1f) },
            new() { variant = "Nurse/Nurse_V01", title = "Nurse Outfit", freeStyle = "Level_FreeStyle_1", freeStyleName = "Free Style: Night Nurse", brief = "FREE STYLE! Style your NURSE look!",
                    colorAName = "White", colorA = Color.white, colorBName = "Pink", colorB = new Color(1f, 0.6f, 0.8f) },
            new() { variant = "Police/Police_V01", title = "Police Outfit", freeStyle = "Level_FreeStyle_3", freeStyleName = "Free Style: On Patrol", brief = "FREE STYLE! Style your POLICE look!",
                    colorAName = "Blue", colorA = new Color(0.45f, 0.6f, 1f), colorBName = "Lilac", colorB = new Color(0.8f, 0.65f, 1f) },
            new() { variant = "Maid/Maid_V01", title = "Maid Outfit", freeStyle = "Level_FreeStyle_5", freeStyleName = "Free Style: Maid Cafe", brief = "FREE STYLE! Style your MAID CAFE look!",
                    colorAName = "Pink", colorA = new Color(1f, 0.6f, 0.8f), colorBName = "Mint", colorB = new Color(0.6f, 1f, 0.8f) },
            new() { variant = "Basketball/Basketball_V01", title = "Basketball Outfit", freeStyle = "Level_FreeStyle_6", copyFrom = "Level_FreeStyle_2", freeStyleName = "Free Style: Court Star", brief = "FREE STYLE! Style your BASKETBALL look!",
                    colorAName = "Orange", colorA = new Color(1f, 0.6f, 0.25f), colorBName = "Blue", colorB = new Color(0.4f, 0.65f, 1f) },
        };

        // Tutorial levels keep only the gates and the move they teach.
        private static readonly string[] TutorialStrip = { "Scissor", "RewardedAdGate", "SkinGate", "Makeup", "PaintGate", "DressColorGate" };

        [MenuItem("Build A Doll/Levels/Apply Outfit Flow Plan")]
        public static void ApplyPlan()
        {
            var database = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
            if (database == null)
            {
                EditorUtility.DisplayDialog(Title, "LevelDatabase not found at " + DatabasePath, "OK");
                return;
            }

            var log = new List<string>();
            var main = new List<LevelData>();
            foreach (var name in MainTrack)
            {
                var level = LoadLevel(name);
                if (level == null) { log.Add("Missing " + name); continue; }
                main.Add(level);
                SetField(level, "isSpecial", false);
                EditorUtility.SetDirty(level);
            }

            // Tutorial clean-up: swipe guide on, AI kept, no scissors / ad gate / skin / makeup / colour gates.
            for (var i = 0; i < 3 && i < main.Count; i++)
            {
                var level = main[i];
                SetField(level, "showSwipeGuide", true);
                SetField(level, "hasAiOpponent", true); // every level races the AI, except free style
                SetField(level, "outfitMakeup", new MakeupColor[0]);
                var entries = GetField<List<LevelData.Entry>>(level, "entries");
                var removed = entries.RemoveAll(e => e.prefab != null && TutorialStrip.Any(k => e.prefab.name.Contains(k)));
                var gates = entries.Count(e => e.gateItems != null && e.gateItems.Length > 0);
                if (gates > 1)
                {
                    var pass = Mathf.RoundToInt(100f * (gates - 1) / gates);
                    SetField(level, "outfitPassPercent", pass);
                    SetField(level, "outfitStarPercents", new[] { pass, pass, 100 });
                }
                EditorUtility.SetDirty(level);
                log.Add($"{level.name}: tutorial (swipe guide on, AI on), removed {removed} extras");
            }

            // Rewards + their free style levels.
            var colorGate = AssetDatabase.LoadAssetAtPath<GameObject>(ColorGatePath);
            var rewards = new List<LevelDatabase.OutfitReward>();
            foreach (var r in Rewards)
            {
                var variant = AssetDatabase.LoadAssetAtPath<OutfitVariant>(OutfitsFolder + r.variant + ".asset");
                // A reward can get its own copy of another free style track (e.g. Basketball needs pants / top / hat / hand item gates).
                if (!string.IsNullOrEmpty(r.copyFrom) && LoadLevel(r.freeStyle) == null)
                    AssetDatabase.CopyAsset(LevelsFolder + r.copyFrom + ".asset", LevelsFolder + r.freeStyle + ".asset");
                var freeStyle = LoadLevel(r.freeStyle);
                if (variant == null || freeStyle == null) { log.Add($"Missing {r.variant} or {r.freeStyle}"); continue; }
                rewards.Add(new LevelDatabase.OutfitReward { outfit = variant, title = r.title, freeStyleLevel = freeStyle });
                log.Add(SetUpFreeStyle(freeStyle, variant, r, colorGate));
            }

            SetField(database, "levels", main);
            SetField(database, "useOutfitFlow", true);
            SetField(database, "tutorialLevels", 3);
            SetField(database, "levelsPerUnlock", 3);
            SetField(database, "outfitRewards", rewards);
            SetField(database, "loopFromLevel", 4);
            SetField(database, "yourLookAi", AssetDatabase.LoadAssetAtPath<AiProfile>(AiFolder + "AiProfile_Normal.asset"));
            SetField(database, "yourLookAiLate", AssetDatabase.LoadAssetAtPath<AiProfile>(AiFolder + "AiProfile_Hard.asset"));
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            Debug.Log("[Outfit Flow] " + string.Join("\n", log));
            EditorUtility.DisplayDialog(Title, $"Main track: {main.Count} levels, {rewards.Count} outfit rewards.\nDetails in the Console.\n\n" +
                                               "Reset saved progress (LevelManager context menu) before testing.", "OK");
        }

        // Free style tracks are authored in the level assets (24 Nov 2026): 11-12 gate pairs, two rounds for hair / top or
        // dress / pants / shoes, plus hat, hand item, glasses, socks. The second top / dress pair holds two colours of the
        // new outfit's main piece (both sides), so she always ends in it. No dress colour gate (palette) any more.
        // Here only the name / brief / flags are set and any old dress colour gate is removed.
        private static string SetUpFreeStyle(LevelData level, OutfitVariant variant, Reward r, GameObject colorGate)
        {
            SetField(level, "displayName", r.freeStyleName);
            SetField(level, "brief", r.brief);
            SetField(level, "isFreeStyle", true);
            SetField(level, "hasAiOpponent", false);

            var entries = GetField<List<LevelData.Entry>>(level, "entries");
            var removed = entries.RemoveAll(e => e.prefab != null && (e.prefab == colorGate || e.prefab.name.Contains("DressColorGate")));
            var gates = entries.Count(e => e.gateItems != null && e.gateItems.Length == 2);

            EditorUtility.SetDirty(level);
            return $"{level.name} → {r.freeStyleName}: {gates} gate pairs" + (removed > 0 ? $", removed {removed} dress colour gate(s)" : "");
        }

        // ---------- UI ----------

        [MenuItem("Build A Doll/UI/Add Outfit Claim Popup To Level Complete")]
        public static void AddClaimPopup()
        {
            var root = PrefabUtility.LoadPrefabContents(LevelCompletePath);
            try
            {
                var view = root.GetComponentInChildren<LevelCompleteViewBehavior>(true);
                if (view == null) { EditorUtility.DisplayDialog(Title, "LevelCompleteViewBehavior not found.", "OK"); return; }
                var parent = view.transform;
                var old = parent.Find("ClaimPopup");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                var box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

                var popup = NewUi("ClaimPopup", parent);
                popup.anchorMin = Vector2.zero; popup.anchorMax = Vector2.one; popup.offsetMin = popup.offsetMax = Vector2.zero;
                var dim = popup.gameObject.AddComponent<Image>();
                dim.color = new Color(0f, 0f, 0f, 0.6f); // also blocks the panel behind it

                var card = NewUi("Card", popup).gameObject.AddComponent<Image>();
                card.sprite = box; card.type = Image.Type.Sliced; card.color = new Color(1f, 0.93f, 0.97f);
                Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 1000f));

                var title = MakeText("Title", card.transform, "Rain Outfit UNLOCKED!", 64f, new Color(0.9f, 0.25f, 0.6f));
                Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(760f, 120f));

                var icon = NewUi("Icon", card.transform).gameObject.AddComponent<Image>();
                icon.preserveAspect = true; icon.raycastTarget = false;
                Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(420f, 420f));

                var claim = MakeButton("ClaimButton", card.transform, box, "CLAIM", new Color(0.35f, 0.8f, 0.3f), new Vector2(0f, -250f), new Vector2(460f, 130f), 60f);
                var skip = MakeButton("SkipButton", card.transform, box, "SKIP", new Color(0.7f, 0.7f, 0.75f), new Vector2(0f, -400f), new Vector2(300f, 90f), 44f);

                var so = new SerializedObject(view);
                so.FindProperty("claimRoot").objectReferenceValue = popup.gameObject;
                so.FindProperty("claimButton").objectReferenceValue = claim;
                so.FindProperty("skipButton").objectReferenceValue = skip;
                so.FindProperty("claimIcon").objectReferenceValue = icon;
                so.FindProperty("claimTitle").objectReferenceValue = title;
                so.ApplyModifiedPropertiesWithoutUndo();

                popup.SetAsLastSibling();
                popup.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, LevelCompletePath);
                EditorUtility.DisplayDialog(Title, "Added ClaimPopup (title, outfit icon, CLAIM, SKIP) to Level Complete and wired it.", "OK");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [MenuItem("Build A Doll/UI/Add Gift Box To Home")]
        public static void AddGiftBox()
        {
            var root = PrefabUtility.LoadPrefabContents(HomeMenuPath);
            try
            {
                var parent = root.transform;
                var old = parent.Find("GiftBoxAnchor");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var box = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

                // The anchor stays active (it holds the view); the button under it shows only while the box has outfits.
                var anchor = NewUi("GiftBoxAnchor", parent);
                Place(anchor, new Vector2(0f, 1f), new Vector2(130f, -330f), new Vector2(200f, 240f));
                var view = anchor.gameObject.AddComponent<GiftBoxView>();

                var button = MakeButton("GiftBox", anchor, box, "", new Color(1f, 0.75f, 0.3f), Vector2.zero, new Vector2(200f, 200f), 40f);
                var icon = NewUi("Icon", button.transform).gameObject.AddComponent<Image>();
                icon.preserveAspect = true; icon.raycastTarget = false;
                Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(150f, 150f));
                var count = MakeText("Count", button.transform, "1", 44f, Color.white);
                Place(count.rectTransform, new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(70f, 70f));
                var label = MakeText("Label", anchor, "Claim!", 36f, new Color(0.9f, 0.25f, 0.6f));
                Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(260f, 50f));
                label.transform.SetParent(button.transform, true);

                var so = new SerializedObject(view);
                so.FindProperty("root").objectReferenceValue = button.gameObject;
                so.FindProperty("button").objectReferenceValue = button;
                so.FindProperty("countText").objectReferenceValue = count;
                so.FindProperty("icon").objectReferenceValue = icon;
                so.FindProperty("titleText").objectReferenceValue = label;
                so.ApplyModifiedPropertiesWithoutUndo();

                button.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, HomeMenuPath);
                EditorUtility.DisplayDialog(Title, "Added the Gift Box (top left of Home) and wired GiftBoxView. Move it in the prefab if it overlaps.", "OK");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ---------- Helpers ----------

        private static LevelData LoadLevel(string name) => AssetDatabase.LoadAssetAtPath<LevelData>(LevelsFolder + name + ".asset");

        private static FieldInfo Field(object target, string name)
        {
            var f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null) Debug.LogError($"[Outfit Flow] No field '{name}' on {target.GetType().Name}");
            return f;
        }

        private static void SetField(object target, string name, object value) => Field(target, name)?.SetValue(target, value);
        private static T GetField<T>(object target, string name) => (T)Field(target, name)?.GetValue(target);

        private static RectTransform NewUi(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        private static TextMeshProUGUI MakeText(string name, Transform parent, string value, float size, Color color)
        {
            var t = NewUi(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            t.text = value; t.fontSize = size; t.fontStyle = FontStyles.Bold; t.color = color;
            t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            return t;
        }

        private static Button MakeButton(string name, Transform parent, Sprite sprite, string label, Color color, Vector2 position, Vector2 size, float fontSize)
        {
            var rt = NewUi(name, parent);
            Place(rt, new Vector2(0.5f, 0.5f), position, size);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.type = Image.Type.Sliced; image.color = color;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (!string.IsNullOrEmpty(label))
            {
                var t = MakeText("Text", rt, label, fontSize, Color.white);
                var tr = t.rectTransform;
                tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
            }
            return button;
        }
    }
}
#endif
