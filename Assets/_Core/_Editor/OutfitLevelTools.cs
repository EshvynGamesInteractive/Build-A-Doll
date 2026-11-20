#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Game.Hurdles;
using Game.Level;
using Game.Outfit;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Right-click a LevelData > "Fill Gate Items From Outfit": walks the level's gate/ring entries
    /// in order and gives each one the next part of the level's outfit, with decoys from other
    /// outfits for the same slot (correct side picked at random). Hair and accessories are put on
    /// RingPair, clothes (shirt, pants, dress, shoes, socks) on GatePair.
    /// </summary>
    public static class OutfitLevelTools
    {
        // Order the parts are offered along the track: pants (or a dress, which replaces pants and shirt),
        // then shirt, hair, shoes, then everything else.
        private static readonly OutfitSlot[] SlotOrder =
        {
            OutfitSlot.Dress, OutfitSlot.Bottom, OutfitSlot.Top, OutfitSlot.Hair, OutfitSlot.Shoes,
            OutfitSlot.Socks, OutfitSlot.Hat, OutfitSlot.Glasses, OutfitSlot.HandItem, OutfitSlot.Bag,
            OutfitSlot.Wings, OutfitSlot.Accessory, OutfitSlot.Makeup
        };

        // Small things are picked from floor rings; clothes from gates.
        private static readonly OutfitSlot[] RingSlots =
        {
            OutfitSlot.Hair, OutfitSlot.Hat, OutfitSlot.Glasses, OutfitSlot.HandItem,
            OutfitSlot.Bag, OutfitSlot.Accessory, OutfitSlot.Wings, OutfitSlot.Makeup
        };

        public static bool UsesRing(OutfitSlot slot) => System.Array.IndexOf(RingSlots, slot) >= 0;

        private static bool IsRing(GameObject prefab) => prefab != null && prefab.name.Contains("Ring");

        private static GameObject FindHurdlePrefab(string prefabName)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{prefabName} t:Prefab", new[] { EditorPaths.HurdlePrefabs }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == prefabName)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            return null;
        }

        [MenuItem("CONTEXT/LevelData/Fill Gate Items From Outfit")]
        private static void FillGates(MenuCommand command) => FillGateItems((LevelData)command.context, true);

        /// <summary>The variant's parts in the order they're offered along the track.</summary>
        internal static List<OutfitItem> OrderedParts(OutfitVariant variant) =>
            variant.AllParts.Where(p => p != null).OrderBy(p => System.Array.IndexOf(SlotOrder, p.Slot)).ToList();

        /// <summary>Fills the level's gate entries with its outfit's parts plus decoys. Returns the summary.</summary>
        internal static string FillGateItems(LevelData level, bool showDialog)
        {
            var variant = level.Outfit;
            if (variant == null)
            {
                const string noOutfit = "Set this level's Outfit (or a Target Look with a Variant) first.";
                if (showDialog)
                    EditorUtility.DisplayDialog("Fill Gates", noOutfit, "OK");
                return noOutfit;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<OutfitCatalog>(OutfitImporter.CatalogPath);
            if (catalog == null)
                catalog = EditorPaths.FindAsset<OutfitCatalog>();

            var parts = OrderedParts(variant);
            var random = new System.Random();
            var ringPair = FindHurdlePrefab("RingPair");
            var gatePair = FindHurdlePrefab("GatePair");
            var swapped = 0;
            var so = new SerializedObject(level);
            var entries = so.FindProperty("entries");
            var next = 0;
            var gateEntries = 0;

            for (var i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var prefab = entry.FindPropertyRelative("prefab").objectReferenceValue as GameObject;
                var gates = prefab != null ? prefab.GetComponentsInChildren<ItemGate>(true) : null;
                if (gates == null || gates.Length == 0)
                    continue;

                gateEntries++;
                if (next >= parts.Count)
                    continue; // more gates than parts: keep what's there

                var correct = parts[next++];

                // Hair and accessories go on a ring pair, clothes on a gate pair (moving gates stay moving).
                var prefabProp = entry.FindPropertyRelative("prefab");
                if (UsesRing(correct.Slot) && !IsRing(prefab) && ringPair != null)
                {
                    prefabProp.objectReferenceValue = ringPair;
                    gates = ringPair.GetComponentsInChildren<ItemGate>(true);
                    swapped++;
                }
                else if (!UsesRing(correct.Slot) && IsRing(prefab) && gatePair != null)
                {
                    prefabProp.objectReferenceValue = gatePair;
                    gates = gatePair.GetComponentsInChildren<ItemGate>(true);
                    swapped++;
                }
                var decoys = PickDecoys(catalog, variant, correct, gates.Length - 1, random);
                var side = (LevelData.GateSide)entry.FindPropertyRelative("greenSide").enumValueIndex;
                var correctIndex = SideIndex(gates, side, random);

                var items = entry.FindPropertyRelative("gateItems");
                items.arraySize = gates.Length;
                for (int g = 0, d = 0; g < gates.Length; g++)
                {
                    var item = g == correctIndex ? correct : d < decoys.Count ? decoys[d++] : null;
                    items.GetArrayElementAtIndex(g).objectReferenceValue = item;
                }
            }

            so.ApplyModifiedProperties(); // undoable
            EditorUtility.SetDirty(level);

            var message = $"{level.name}: filled {Mathf.Min(gateEntries, parts.Count)} gates for '{variant.DisplayName}' ({parts.Count} parts).";
            if (swapped > 0)
                message += $"\n{swapped} entries switched between ring pair (hair, accessories) and gate pair (clothes).";
            if (gateEntries < parts.Count)
                message += $"\n\nOnly {gateEntries} gate entries: add {parts.Count - gateEntries} more so every part can be picked " +
                           $"(missing: {string.Join(", ", parts.Skip(gateEntries).Select(p => p.Slot))}).";
            if (catalog == null)
                message += "\n\nNo Outfit Catalog found, so gates have no decoys. Run Build A Doll > Outfits > Import Outfits From Doll FBX.";

            Debug.Log(message, level);
            if (showDialog)
                EditorUtility.DisplayDialog("Fill Gates", message, "OK");
            return message;
        }

        /// <summary>Index (hierarchy order) of the leftmost / rightmost gate in a pair prefab; random for Random or a single gate.</summary>
        internal static int SideIndex(ItemGate[] gates, LevelData.GateSide side, System.Random random)
        {
            if (gates.Length < 2 || side == LevelData.GateSide.Random)
                return random.Next(gates.Length);

            var best = 0;
            for (var g = 1; g < gates.Length; g++)
            {
                var x = gates[g].transform.position.x;
                var bestX = gates[best].transform.position.x;
                if (side == LevelData.GateSide.Left ? x < bestX : x > bestX)
                    best = g;
            }
            return best;
        }

        /// <summary>Sideways positions of the leftmost and rightmost gate of a pair prefab (prefab root at 0).</summary>
        internal static bool TryGetGateLanes(GameObject prefab, out float left, out float right)
        {
            left = right = 0f;
            var gates = prefab != null ? prefab.GetComponentsInChildren<ItemGate>(true) : null;
            if (gates == null || gates.Length < 2)
                return false;

            left = right = gates[0].transform.position.x - prefab.transform.position.x;
            foreach (var gate in gates)
            {
                var x = gate.transform.position.x - prefab.transform.position.x;
                left = Mathf.Min(left, x);
                right = Mathf.Max(right, x);
            }
            return right - left > 0.5f;
        }

        // Wrong choices for the same slot from other outfits; Top and Dress stand in for each other.
        private static List<OutfitItem> PickDecoys(OutfitCatalog catalog, OutfitVariant variant, OutfitItem correct, int count, System.Random random)
        {
            var result = new List<OutfitItem>();
            if (catalog == null || count <= 0)
                return result;

            var pool = catalog.GetPartsForSlot(correct.Slot);
            if (correct.Slot == OutfitSlot.Dress)
                pool.AddRange(catalog.GetPartsForSlot(OutfitSlot.Top));
            else if (correct.Slot == OutfitSlot.Top)
                pool.AddRange(catalog.GetPartsForSlot(OutfitSlot.Dress));

            pool.RemoveAll(p => p == null || variant.Contains(p));

            while (result.Count < count && pool.Count > 0)
            {
                var index = random.Next(pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return result;
        }

        [MenuItem("CONTEXT/OutfitVariant/Wear On Player (Play Mode)")]
        private static void PreviewOnDoll(MenuCommand command)
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Wear Outfit", "Enter Play mode first.", "OK");
                return;
            }

            // The player's doll is the one that raises events (the AI and demo dolls don't).
            var dolls = Object.FindObjectsByType<DollOutfit>(FindObjectsSortMode.None);
            var doll = dolls.FirstOrDefault(d => d.RaiseEvents) ?? dolls.FirstOrDefault();
            if (doll == null)
            {
                EditorUtility.DisplayDialog("Wear Outfit", "No doll with a DollOutfit in the scene.", "OK");
                return;
            }

            doll.WearVariant((OutfitVariant)command.context);
            EditorGUIUtility.PingObject(doll);
        }
    }
}
#endif
