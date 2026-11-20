#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.AI;
using Game.Hurdles;
using Game.Level;
using Game.Outfit;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds LevelData assets, AI profiles and the LevelDatabase order from _Data/_Levels/LevelPlan.json.
    /// The plan describes each level as a list of sections (reusable hurdle chunks, outfit gates, coin gates);
    /// this lays them out along the track, then fills the gates from the level's outfit
    /// (same as LevelData > Fill Gate Items From Outfit).
    /// Menu: Build A Doll > Levels > Import Level Plan.
    /// </summary>
    public static class LevelPlanImporter
    {
        private const string PlanFile = "LevelPlan.json";
        private const string Title = "Import Level Plan";

        // ---- JSON model (field names match LevelPlan.json) ----
        [Serializable] private class Plan
        {
            public float startDistance = 12f, finishRunway = 20f, partLength = 8f, coinGateLength = 8f;
            public int defaultAllowMisses = 1, loopFromLevel = 4;
            public string startingVariant = "Casual_V01";

            // Level design rules applied to every left/right part gate:
            // cash leads toward the RED gate (lure), a hurdle stands right after the GREEN gate.
            public string lurePrefab = "CashBundle";
            public int lureCount = 4;
            public float lureSpan = 8f;   // metres before the gate the cash trail starts
            public float guardOffset = 12f;   // metres after the green gate the hurdle stands: time to react (12 m = 2 s at speed 6)
            public float guardClearance = 10f; // free metres after that hurdle before the next section starts
            public float maxTrackLength = 250f; // warn past this (scene ramp length minus a margin)
            public AiDef[] aiProfiles = Array.Empty<AiDef>();
            public Chunk[] chunks = Array.Empty<Chunk>();
            public LevelDef[] levels = Array.Empty<LevelDef>();
        }

        [Serializable] private class AiDef
        {
            public string name;
            public float gateAccuracy = 0.7f, obstacleSkill = 0.8f, steerSpeed = 5f, gateLookAhead = 12f, startDelay = 0.3f;
        }

        [Serializable] private class Chunk
        {
            public string id;
            public float length = 12f;
            public ChunkItem[] items = Array.Empty<ChunkItem>();
        }

        [Serializable] private class ChunkItem
        {
            public string prefab;
            public float offset, lateral;
        }

        [Serializable] private class LevelDef
        {
            public string id, name, outfit, ai;
            public float gap = 10f;
            public int completionCoins = 50;
            public int allowMisses = -1; // -1 = plan default
            public bool hasAiOpponent = true;
            public string[] sections = Array.Empty<string>();

            public string brief;              // theme line on the target card
            public bool tutorial;             // swipe guide hand
            public bool special;              // special outfit level
            public string specialTitle;
            public bool lure = true;          // cash trail toward the red gate
            public string[] guards = Array.Empty<string>(); // hurdles after the green gate, used in turn
            public float guardOffset = -1f;   // -1 = plan default (tutorials use more)
        }

        private struct Placed
        {
            public GameObject prefab;
            public float distance, lateral;
            public CoinGate.Setting[] coins;
            public LevelData.GateSide greenSide;
        }

        [MenuItem("Build A Doll/Levels/Import Level Plan (new levels only)")]
        private static void ImportNew() => Import(false);

        [MenuItem("Build A Doll/Levels/Import Level Plan (overwrite existing)")]
        private static void ImportOverwrite()
        {
            if (EditorUtility.DisplayDialog(Title,
                    "Rebuild every level in LevelPlan.json, replacing hurdles, outfit, AI and rewards of existing Level assets " +
                    "(including Level_01-03 and their Target Look links). Undo is not available. Continue?",
                    "Overwrite", "Cancel"))
                Import(true);
        }

        private static void Import(bool overwrite)
        {
            var planPath = EditorPaths.Levels + "/" + PlanFile;
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(planPath);
            if (json == null)
            {
                EditorUtility.DisplayDialog(Title, $"No plan at {planPath}.", "OK");
                return;
            }

            Plan plan;
            try { plan = JsonUtility.FromJson<Plan>(json.text); }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog(Title, $"LevelPlan.json can't be read:\n{e.Message}", "OK");
                return;
            }

            var log = new StringBuilder();
            var warnings = new List<string>();
            var prefabs = new Dictionary<string, GameObject>();
            var chunks = plan.chunks.Where(c => !string.IsNullOrEmpty(c.id)).ToDictionary(c => c.id);
            var forwardSpeed = ReadForwardSpeed();

            var ai = ImportAiProfiles(plan, overwrite, log);
            var starting = FindVariant(plan.startingVariant);
            if (starting == null)
                warnings.Add($"Starting variant '{plan.startingVariant}' not found: levels start with nothing on.");

            EditorPaths.EnsureFolder(EditorPaths.Levels);
            var ordered = new List<LevelData>();
            int created = 0, rebuilt = 0, kept = 0;

            try
            {
                for (var li = 0; li < plan.levels.Length; li++)
                {
                    var def = plan.levels[li];
                    EditorUtility.DisplayProgressBar(Title, def.id, (float)li / plan.levels.Length);
                    var path = $"{EditorPaths.Levels}/{def.id}.asset";
                    var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);

                    if (level != null && !overwrite)
                    {
                        ordered.Add(level);
                        kept++;
                        continue;
                    }

                    var variant = FindVariant(def.outfit);
                    if (variant == null)
                    {
                        warnings.Add($"{def.id}: outfit '{def.outfit}' not found, skipped.");
                        if (level != null) ordered.Add(level);
                        continue;
                    }

                    var isNew = level == null;
                    if (isNew)
                    {
                        level = ScriptableObject.CreateInstance<LevelData>();
                        AssetDatabase.CreateAsset(level, path);
                    }

                    var placed = Layout(plan, def, variant, chunks, prefabs, warnings, out var finish);
                    if (finish > plan.maxTrackLength)
                        warnings.Add($"{def.id}: finish at {finish:0} m is past {plan.maxTrackLength:0} m; hurdles beyond the scene's End Point are skipped. Remove a section or lower 'gap'.");
                    Validate(def, variant, warnings);
                    Write(level, def, variant, starting, placed, finish, plan, ai, warnings);
                    OutfitLevelTools.FillGateItems(level, false);

                    if (isNew) created++; else rebuilt++;
                    ordered.Add(level);
                    log.AppendLine($"{def.id} '{def.name}': {variant.name} ({OutfitLevelTools.OrderedParts(variant).Count} parts), " +
                                   $"{placed.Count} entries, finish {finish:0} (~{finish / forwardSpeed:0}s), AI {(def.hasAiOpponent ? def.ai : "-")}" +
                                   $"{(def.tutorial ? ", tutorial" : "")}{(def.special ? ", SPECIAL" : "")}");
                }

                UpdateDatabase(ordered, plan.loopFromLevel, warnings);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }

            var summary = $"Levels: {created} created, {rebuilt} rebuilt, {kept} kept as they were. Database: {ordered.Count} levels, loops from {plan.loopFromLevel}.";
            if (warnings.Count > 0)
                summary += $"\n\n{warnings.Count} warning(s), see the Console:\n- " + string.Join("\n- ", warnings.Take(8));
            Debug.Log($"[LevelPlan] {summary}\n{log}");
            foreach (var w in warnings) Debug.LogWarning("[LevelPlan] " + w);
            EditorUtility.DisplayDialog(Title, summary, "OK");
        }

        // ---- Layout: sections one after another, 'gap' units apart ----
        private static List<Placed> Layout(Plan plan, LevelDef def, OutfitVariant variant, Dictionary<string, Chunk> chunks,
            Dictionary<string, GameObject> prefabs, List<string> warnings, out float finish)
        {
            var result = new List<Placed>();
            var cursor = plan.startDistance;
            var parts = OutfitLevelTools.OrderedParts(variant);
            var random = new System.Random(StableHash(def.id)); // same plan = same sides every import
            var partIndex = 0;
            var guardIndex = 0;
            var lastSide = LevelData.GateSide.Random;
            var sameSideRun = 0;

            foreach (var raw in def.sections)
            {
                var section = raw.Trim();
                if (section.StartsWith("part"))
                {
                    var kind = section.Contains(":") ? section.Substring(section.IndexOf(':') + 1) : "";
                    var ring = partIndex < parts.Count && OutfitLevelTools.UsesRing(parts[partIndex].Slot);
                    partIndex++;
                    var name = kind == "single" ? "GateSingle" : ring ? "RingPair" : kind == "moving" ? "GatePair_Moving" : "GatePair";
                    var prefab = Prefab(name, prefabs, warnings, def.id);
                    var side = LevelData.GateSide.Random;
                    var guarded = false;
                    var guardAt = def.guardOffset >= 0f ? def.guardOffset : plan.guardOffset;

                    if (kind != "single" && OutfitLevelTools.TryGetGateLanes(prefab, out var left, out var right))
                    {
                        // Green side: random, but never three times in a row on the same side.
                        side = random.Next(2) == 0 ? LevelData.GateSide.Left : LevelData.GateSide.Right;
                        if (side == lastSide && sameSideRun >= 2)
                            side = side == LevelData.GateSide.Left ? LevelData.GateSide.Right : LevelData.GateSide.Left;
                        sameSideRun = side == lastSide ? sameSideRun + 1 : 1;
                        lastSide = side;

                        var green = side == LevelData.GateSide.Left ? left : right;
                        var red = side == LevelData.GateSide.Left ? right : left;

                        // Rules 2 + 3: a cash trail from the middle toward the RED gate, so grabbing cash means a swipe to the wrong side.
                        if (def.lure && plan.lureCount > 0)
                        {
                            var cash = Prefab(plan.lurePrefab, prefabs, warnings, def.id);
                            var span = Mathf.Clamp(Mathf.Min(plan.lureSpan, def.gap - 1.5f), 3f, plan.lureSpan);
                            for (var k = 0; k < plan.lureCount; k++)
                            {
                                var t = plan.lureCount == 1 ? 1f : (float)k / (plan.lureCount - 1);
                                Add(result, cash, Mathf.Max(0f, cursor - span + t * (span - 2f)), Mathf.Lerp(red * 0.15f, red, t), null);
                            }
                        }

                        // Rule 1: a hurdle right after the GREEN gate, on its lane.
                        if (def.guards != null && def.guards.Length > 0)
                        {
                            var guard = def.guards[guardIndex++ % def.guards.Length];
                            Add(result, Prefab(guard, prefabs, warnings, def.id), cursor + guardAt, green, null);
                            guarded = true;
                        }
                    }

                    Add(result, prefab, cursor, 0f, null, side);
                    // Margin: room to steer/jump/slide before the hurdle, and free track after it before the next section.
                    cursor += guarded ? Mathf.Max(plan.partLength, guardAt + plan.guardClearance - def.gap) : plan.partLength;
                }
                else if (section.StartsWith("coin:"))
                {
                    var settings = section.Substring(5).Split(',').Select(s => ParseCoin(s, def.id, warnings)).ToArray();
                    Add(result, Prefab("CoinGatePair", prefabs, warnings, def.id), cursor, 0f, settings);
                    cursor += plan.coinGateLength;
                }
                else if (chunks.TryGetValue(section, out var chunk))
                {
                    foreach (var item in chunk.items)
                        Add(result, Prefab(item.prefab, prefabs, warnings, def.id), Mathf.Max(0f, cursor + item.offset), item.lateral, null);
                    cursor += chunk.length;
                }
                else
                {
                    warnings.Add($"{def.id}: unknown section '{section}', skipped.");
                    continue;
                }

                cursor += def.gap;
            }

            finish = cursor - def.gap + plan.finishRunway;
            result.Sort((a, b) => a.distance.CompareTo(b.distance));
            return result;
        }

        private static void Add(List<Placed> list, GameObject prefab, float distance, float lateral, CoinGate.Setting[] coins,
            LevelData.GateSide greenSide = LevelData.GateSide.Random)
        {
            if (prefab != null)
                list.Add(new Placed { prefab = prefab, distance = distance, lateral = lateral, coins = coins, greenSide = greenSide });
        }

        // String hash that is the same on every machine and run (string.GetHashCode isn't).
        private static int StableHash(string text)
        {
            unchecked
            {
                var hash = 23;
                foreach (var c in text ?? "")
                    hash = hash * 31 + c;
                return hash;
            }
        }

        private static CoinGate.Setting ParseCoin(string text, string level, List<string> warnings)
        {
            text = text.Trim();
            var op = CoinGate.Operation.Add;
            var digits = text;
            if (text.Length > 0)
            {
                switch (char.ToLowerInvariant(text[0]))
                {
                    case 'x': case '*': op = CoinGate.Operation.Multiply; digits = text.Substring(1); break;
                    case '/': op = CoinGate.Operation.Divide; digits = text.Substring(1); break;
                    case '-': op = CoinGate.Operation.Subtract; digits = text.Substring(1); break;
                    case '+': digits = text.Substring(1); break;
                }
            }

            if (!int.TryParse(digits, out var amount))
                warnings.Add($"{level}: coin gate '{text}' isn't like x2, +20, -10 or /2.");
            return new CoinGate.Setting { operation = op, amount = Mathf.Max(0, amount) };
        }

        private static GameObject Prefab(string name, Dictionary<string, GameObject> cache, List<string> warnings, string level)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            if (cache.TryGetValue(name, out var found))
                return found;

            foreach (var guid in AssetDatabase.FindAssets($"{name} t:Prefab", new[] { EditorPaths.HurdlePrefabs }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == name)
                {
                    found = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    break;
                }
            }

            if (found == null)
                warnings.Add($"{level}: hurdle prefab '{name}' not found under {EditorPaths.HurdlePrefabs}.");
            cache[name] = found;
            return found;
        }

        // ---- Checks that the plan can reach 100% ----
        private static void Validate(LevelDef def, OutfitVariant variant, List<string> warnings)
        {
            var parts = OutfitLevelTools.OrderedParts(variant);
            var gates = def.sections.Where(s => s.Trim().StartsWith("part")).Select(s => s.Trim()).ToList();

            if (gates.Count != parts.Count)
                warnings.Add($"{def.id}: {gates.Count} part gates for {parts.Count} parts of {variant.name} " +
                             $"({string.Join(", ", parts.Select(p => p.Slot))}).");

            var duplicate = parts.GroupBy(p => p.Slot).FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null)
                warnings.Add($"{def.id}: {variant.name} has two {duplicate.Key} parts; the second replaces the first, so 100% is impossible.");

            for (var i = 0; i < Mathf.Min(gates.Count, parts.Count); i++)
            {
                if (gates[i] == "part:moving" && OutfitLevelTools.UsesRing(parts[i].Slot))
                    warnings.Add($"{def.id}: gate {i + 1} is '{gates[i]}' but gets {parts[i].Slot}, which uses a ring pair (it becomes a plain RingPair).");
            }
        }

        // ---- Writes the LevelData fields ----
        private static void Write(LevelData level, LevelDef def, OutfitVariant variant, OutfitVariant starting,
            List<Placed> placed, float finish, Plan plan, Dictionary<string, AiProfile> ai, List<string> warnings)
        {
            var so = new SerializedObject(level);
            so.FindProperty("displayName").stringValue = def.name;
            so.FindProperty("brief").stringValue = def.brief ?? "";
            so.FindProperty("showSwipeGuide").boolValue = def.tutorial;
            so.FindProperty("isSpecial").boolValue = def.special;
            so.FindProperty("specialTitle").stringValue = def.specialTitle ?? "";
            so.FindProperty("finishDistance").floatValue = Mathf.Ceil(finish);
            so.FindProperty("outfit").objectReferenceValue = variant;
            so.FindProperty("targetLook").objectReferenceValue = null; // the outfit is the target
            so.FindProperty("hasAiOpponent").boolValue = def.hasAiOpponent;
            so.FindProperty("completionCoins").intValue = def.completionCoins;

            AiProfile profile = null;
            if (!string.IsNullOrEmpty(def.ai) && !ai.TryGetValue(def.ai, out profile))
                warnings.Add($"{def.id}: AI profile '{def.ai}' not in the plan; using the database default.");
            so.FindProperty("aiProfile").objectReferenceValue = profile;

            // Pass = all parts but 'allowMisses'; stars = pass, one miss, perfect.
            var n = Mathf.Max(1, OutfitLevelTools.OrderedParts(variant).Count);
            var misses = Mathf.Clamp(def.allowMisses >= 0 ? def.allowMisses : plan.defaultAllowMisses, 0, n - 1);
            var pass = Mathf.FloorToInt(100f * (n - misses) / n);
            var oneMiss = Mathf.Max(pass, Mathf.FloorToInt(100f * (n - 1) / n));
            so.FindProperty("outfitPassPercent").intValue = pass;
            var stars = so.FindProperty("outfitStarPercents");
            stars.arraySize = 3;
            stars.GetArrayElementAtIndex(0).intValue = pass;
            stars.GetArrayElementAtIndex(1).intValue = oneMiss;
            stars.GetArrayElementAtIndex(2).intValue = 100;

            var start = so.FindProperty("startingOutfit");
            var startParts = starting != null ? starting.AllParts.Where(p => p != null).ToList() : new List<OutfitItem>();
            start.arraySize = startParts.Count;
            for (var i = 0; i < startParts.Count; i++)
                start.GetArrayElementAtIndex(i).objectReferenceValue = startParts[i];

            var entries = so.FindProperty("entries");
            entries.arraySize = placed.Count;
            for (var i = 0; i < placed.Count; i++)
            {
                var e = entries.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("prefab").objectReferenceValue = placed[i].prefab;
                e.FindPropertyRelative("distance").floatValue = placed[i].distance;
                e.FindPropertyRelative("lateral").floatValue = placed[i].lateral;
                e.FindPropertyRelative("gateItems").arraySize = 0;
                e.FindPropertyRelative("greenSide").enumValueIndex = (int)placed[i].greenSide;

                var coins = e.FindPropertyRelative("coinGates");
                var settings = placed[i].coins ?? Array.Empty<CoinGate.Setting>();
                coins.arraySize = settings.Length;
                for (var c = 0; c < settings.Length; c++)
                {
                    var s = coins.GetArrayElementAtIndex(c);
                    s.FindPropertyRelative("operation").enumValueIndex = (int)settings[c].operation;
                    s.FindPropertyRelative("amount").intValue = settings[c].amount;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(level);
        }

        // ---- AI profiles: created when missing, values updated when overwriting ----
        private static Dictionary<string, AiProfile> ImportAiProfiles(Plan plan, bool overwrite, StringBuilder log)
        {
            var result = new Dictionary<string, AiProfile>();
            EditorPaths.EnsureFolder(EditorPaths.AiProfiles);
            var template = EditorPaths.FindAsset<AiProfile>();
            var movement = template != null ? new SerializedObject(template).FindProperty("movementConfig").objectReferenceValue : null;

            foreach (var def in plan.aiProfiles)
            {
                if (string.IsNullOrEmpty(def.name))
                    continue;

                var path = $"{EditorPaths.AiProfiles}/{def.name}.asset";
                var profile = AssetDatabase.LoadAssetAtPath<AiProfile>(path);
                var isNew = profile == null;
                if (isNew)
                {
                    profile = ScriptableObject.CreateInstance<AiProfile>();
                    AssetDatabase.CreateAsset(profile, path);
                }

                if (isNew || overwrite)
                {
                    var so = new SerializedObject(profile);
                    so.FindProperty("gateAccuracy").floatValue = def.gateAccuracy;
                    so.FindProperty("obstacleSkill").floatValue = def.obstacleSkill;
                    so.FindProperty("steerSpeed").floatValue = def.steerSpeed;
                    so.FindProperty("gateLookAhead").floatValue = def.gateLookAhead;
                    so.FindProperty("startDelay").floatValue = def.startDelay;
                    var move = so.FindProperty("movementConfig");
                    if (move.objectReferenceValue == null)
                        move.objectReferenceValue = movement;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(profile);
                    log.AppendLine($"AI {(isNew ? "created" : "updated")}: {def.name}");
                }

                result[def.name] = profile;
            }

            return result;
        }

        private static void UpdateDatabase(List<LevelData> ordered, int loopFrom, List<string> warnings)
        {
            var db = AssetDatabase.LoadAssetAtPath<LevelDatabase>(EditorPaths.Levels + "/LevelDatabase.asset")
                     ?? EditorPaths.FindAsset<LevelDatabase>();
            if (db == null)
            {
                warnings.Add("No LevelDatabase found: levels were made but not added to a database.");
                return;
            }

            var so = new SerializedObject(db);
            var levels = so.FindProperty("levels");

            // Levels already in the database but not in the plan stay, after the planned ones.
            var extra = new List<LevelData>();
            for (var i = 0; i < levels.arraySize; i++)
            {
                if (levels.GetArrayElementAtIndex(i).objectReferenceValue is LevelData old && !ordered.Contains(old))
                    extra.Add(old);
            }
            if (extra.Count > 0)
                warnings.Add($"Kept {extra.Count} database level(s) not in the plan at the end: {string.Join(", ", extra.Select(l => l.name))}.");

            var all = ordered.Concat(extra).ToList();
            levels.arraySize = all.Count;
            for (var i = 0; i < all.Count; i++)
                levels.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
            so.FindProperty("loopFromLevel").intValue = Mathf.Clamp(loopFrom, 1, Mathf.Max(1, all.Count));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(db);
        }

        private static OutfitVariant FindVariant(string variantName)
        {
            if (string.IsNullOrEmpty(variantName))
                return null;
            foreach (var guid in AssetDatabase.FindAssets($"{variantName} t:OutfitVariant"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == variantName)
                    return AssetDatabase.LoadAssetAtPath<OutfitVariant>(path);
            }
            return null;
        }

        private static float ReadForwardSpeed()
        {
            var config = EditorPaths.FindAsset<Game.Player.Config.RunnerMovementConfig>();
            var prop = config != null ? new SerializedObject(config).FindProperty("forwardSpeed") : null;
            return prop != null && prop.floatValue > 0f ? prop.floatValue : 6f;
        }
    }
}
#endif
