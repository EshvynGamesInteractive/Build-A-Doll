using System;
using System.Collections.Generic;
using Game.Outfit;
using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// Theme plan (24 Nov 2026): the look she designed on each free style level (Nurse, Sport, Police...), saved by the
    /// free style level's asset name. Every pass overwrites it, so replaying a free style level from the map and
    /// redesigning updates its styled level ("My Nurse Look"). PlayerPrefs, one key per free style level.
    /// </summary>
    public static class ThemeDesigns
    {
        private const string Prefix = "theme_design_v1_";
        private const string KeysKey = "theme_design_v1_keys";

        [Serializable]
        private class Design
        {
            public List<string> items = new();
        }

        /// <summary>Raised after a design is saved (argument = the free style level).</summary>
        public static event Action<LevelData> Changed;

        private static string Key(LevelData freeStyle) => Prefix + freeStyle.name;

        /// <summary>The saved design as stored (empty when none). Changes whenever the design changes.</summary>
        public static string GetRaw(LevelData freeStyle) =>
            freeStyle != null ? PlayerPrefs.GetString(Key(freeStyle), string.Empty) : string.Empty;

        public static bool Has(LevelData freeStyle) => !string.IsNullOrEmpty(GetRaw(freeStyle));

        /// <summary>Saves (replaces) her design for this free style level. An empty pick list keeps the old design.</summary>
        public static void Save(LevelData freeStyle, IReadOnlyList<OutfitItem> picks)
        {
            if (freeStyle == null || picks == null)
                return;
            var design = new Design();
            foreach (var item in picks)
                if (item != null && !design.items.Contains(item.name))
                    design.items.Add(item.name);
            if (design.items.Count == 0)
                return;

            PlayerPrefs.SetString(Key(freeStyle), JsonUtility.ToJson(design));
            var keys = PlayerPrefs.GetString(KeysKey, string.Empty);
            if (!(";" + keys + ";").Contains(";" + freeStyle.name + ";"))
                PlayerPrefs.SetString(KeysKey, string.IsNullOrEmpty(keys) ? freeStyle.name : keys + ";" + freeStyle.name);
            PlayerPrefs.Save();
            Changed?.Invoke(freeStyle);
        }

        /// <summary>Her design as items, or null when she hasn't designed one yet.</summary>
        public static List<OutfitItem> Load(LevelData freeStyle, OutfitCatalog catalog)
        {
            var raw = GetRaw(freeStyle);
            if (string.IsNullOrEmpty(raw))
                return null;
            var design = JsonUtility.FromJson<Design>(raw);
            if (design == null || design.items == null || design.items.Count == 0)
                return null;

            var result = new List<OutfitItem>();
            foreach (var itemName in design.items)
            {
                var item = FindOnTrack(freeStyle, itemName) ?? OutfitFlow.FindItem(catalog, itemName);
                if (item != null)
                    result.Add(item);
            }
            return result.Count > 0 ? result : null;
        }

        // The free style level's own gates hold every item she could have picked.
        private static OutfitItem FindOnTrack(LevelData freeStyle, string itemName)
        {
            foreach (var entry in freeStyle.Entries)
            {
                if (entry?.gateItems == null)
                    continue;
                foreach (var item in entry.gateItems)
                    if (item != null && item.name == itemName)
                        return item;
            }
            return null;
        }

        public static void ResetAll()
        {
            var keys = PlayerPrefs.GetString(KeysKey, string.Empty);
            foreach (var name in keys.Split(';'))
                if (!string.IsNullOrEmpty(name))
                    PlayerPrefs.DeleteKey(Prefix + name);
            PlayerPrefs.DeleteKey(KeysKey);
            PlayerPrefs.Save();
        }
    }
}
