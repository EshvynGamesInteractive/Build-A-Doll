#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Outfit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// Turns the doll FBX's wardrobe into game data. The FBX is laid out as
    ///   Wardrobe / "Outfit | Police" / "Police | Shared Pieces" and "Police | Variant 01..."
    ///   with parts under them, some inside "Hair", "head_accessaries" or "hand_objects" groups.
    /// For every part it makes a prefab (just that mesh, bound to the doll's bones by name at
    /// runtime) and an OutfitItem config; for every variant an OutfitVariant; for every outfit an
    /// OutfitSet; and one OutfitCatalog listing them all.
    /// Safe to run again: assets keep their GUIDs (levels and gates stay linked), prefabs are
    /// rebuilt, slots are re-detected, and names, icons and style scores you set are kept.
    /// </summary>
    public static class OutfitImporter
    {
        private const string Sep = " | ";
        private const string OutfitPrefix = "Outfit | ";
        private const string DollModelName = "Doll Rigged";

        private static string ItemPrefabRoot => EditorPaths.Prefabs + "/_Items";
        private static string ItemDataRoot => EditorPaths.Data + "/_Items";
        private static string OutfitDataRoot => EditorPaths.Data + "/_Outfits";
        public static string CatalogPath => OutfitDataRoot + "/OutfitCatalog.asset";

        private class PartData
        {
            public string Label;
            public OutfitSlot Slot;
            public bool Guessed;
            public readonly List<Renderer> Renderers = new();
            public OutfitItem Asset;
        }

        private class GroupData
        {
            public string Label;
            public readonly List<PartData> Parts = new();
        }

        private class OutfitData
        {
            public string Name;
            public string Key;
            public readonly GroupData Shared = new() { Label = "Shared Pieces" };
            public readonly List<GroupData> Variants = new();
        }

        [MenuItem("Build A Doll/Outfits/Import Outfits From Doll FBX")]
        private static void ImportMenu()
        {
            var model = FindDollModel();
            if (model == null)
                return;

            if (!EditorUtility.DisplayDialog("Import Outfits",
                    $"Make part prefabs, item configs, variants and outfits from '{model.name}'?\n\n" +
                    $"Prefabs: {ItemPrefabRoot}\nConfigs: {ItemDataRoot}\nOutfits: {OutfitDataRoot}\n\n" +
                    "Existing assets are updated in place.", "Import", "Cancel"))
                return;

            Import(model, dryRun: false);
        }

        [MenuItem("Build A Doll/Outfits/Preview Import (log only)")]
        private static void PreviewMenu()
        {
            var model = FindDollModel();
            if (model != null)
                Import(model, dryRun: true);
        }

        public static OutfitCatalog Import(GameObject model, bool dryRun)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var warnings = new List<string>();

            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                var wardrobe = FindDeep(instance.transform, t => t.name == "Wardrobe");
                var hips = FindDeep(instance.transform, t => t.name.EndsWith("Hips", StringComparison.Ordinal));

                if (wardrobe == null)
                {
                    EditorUtility.DisplayDialog("Import Outfits", $"'{model.name}' has no 'Wardrobe' object.", "OK");
                    return null;
                }

                var outfits = Parse(wardrobe, warnings);

                if (dryRun)
                {
                    LogPreview(outfits, warnings);
                    return null;
                }

                return Build(outfits, instance.transform, hips, scene, warnings);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        // ---------- Reading the FBX ----------

        private static List<OutfitData> Parse(Transform wardrobe, List<string> warnings)
        {
            var outfits = new List<OutfitData>();

            foreach (Transform outfitNode in wardrobe)
            {
                if (!outfitNode.name.StartsWith(OutfitPrefix, StringComparison.Ordinal))
                    continue;

                var data = new OutfitData { Name = outfitNode.name.Substring(OutfitPrefix.Length).Trim() };
                data.Key = MakeKey(data.Name);

                foreach (Transform groupNode in outfitNode)
                {
                    if (IsJunk(groupNode))
                        continue;

                    // A mesh straight under the outfit counts as a shared piece.
                    if (!groupNode.name.Contains(Sep))
                    {
                        CollectParts(groupNode, groupNode.parent, data.Shared, warnings);
                        continue;
                    }

                    var label = LastSegment(groupNode.name);
                    if (label.StartsWith("Shared", StringComparison.OrdinalIgnoreCase))
                    {
                        CollectParts(groupNode, groupNode, data.Shared, warnings);
                        continue;
                    }

                    var group = new GroupData { Label = label };
                    CollectParts(groupNode, groupNode, group, warnings);

                    if (group.Parts.Count == 0)
                        warnings.Add($"{data.Name} / {label}: no meshes, skipped.");
                    else
                        data.Variants.Add(group);
                }

                // An outfit with only shared pieces still gets one variant to wear them.
                if (data.Variants.Count == 0 && data.Shared.Parts.Count > 0)
                    data.Variants.Add(new GroupData { Label = "Variant 01" });

                if (data.Variants.Count == 0)
                {
                    warnings.Add($"{data.Name}: no meshes at all, skipped.");
                    continue;
                }

                DetectDresses(data);
                outfits.Add(data);
            }

            return outfits;
        }

        // Every mesh under 'node'. Meshes in the same sub-group that land in the same slot become
        // one part (e.g. two hair meshes in one Hair group are worn together).
        private static void CollectParts(Transform node, Transform groupRoot, GroupData group, List<string> warnings)
        {
            var bySubGroup = new Dictionary<(Transform, OutfitSlot), PartData>();

            foreach (var renderer in node.GetComponentsInChildren<Renderer>(true))
            {
                if (IsJunk(renderer.transform) || !(renderer is SkinnedMeshRenderer || renderer is MeshRenderer))
                    continue;

                var subGroup = FindSubGroup(renderer.transform, groupRoot);
                var hint = subGroup != null ? LastSegment(subGroup.name) : null;
                var slot = Classify(renderer.name, hint, out var guessed);

                if (guessed)
                    warnings.Add($"'{renderer.name}' ({group.Label}): couldn't tell its slot, set to {slot}. Fix it on the item config.");

                var key = (subGroup, slot);
                if (!bySubGroup.TryGetValue(key, out var part))
                {
                    part = new PartData { Label = renderer.name, Slot = slot, Guessed = guessed };
                    bySubGroup[key] = part;
                    group.Parts.Add(part);
                }

                part.Renderers.Add(renderer);
            }
        }

        // Group like "Police | Shared Pieces | Hair" between the mesh and its variant, if any.
        private static Transform FindSubGroup(Transform mesh, Transform groupRoot)
        {
            for (var t = mesh.parent; t != null && t != groupRoot; t = t.parent)
            {
                if (t.name.Contains(Sep))
                    return t;
            }

            return null;
        }

        /// <summary>Works out a part's slot from its group (Hair, head_accessaries...) or its mesh name.</summary>
        public static OutfitSlot Classify(string meshName, string groupHint, out bool guessed)
        {
            guessed = false;
            var n = meshName.ToLowerInvariant();
            var g = groupHint?.ToLowerInvariant() ?? string.Empty;

            switch (g)
            {
                case "hair":
                    return OutfitSlot.Hair;
                case "head_accessaries":
                case "head_accessories":
                    return Has(n, "glass") ? OutfitSlot.Glasses : OutfitSlot.Hat;
                case "hand_objects":
                    return Has(n, "bag") ? OutfitSlot.Bag : OutfitSlot.HandItem;
                case "angelreward":
                    return Has(n, "wing") ? OutfitSlot.Wings : OutfitSlot.Accessory;
            }

            // Mesh names mix English and pinyin: xiezi = shoes, tuoxie = slippers, wazi = socks,
            // kuzi = trousers, qunzi = skirt, yifu = clothes, neiyi = underwear, dunpai = shield,
            // xianglian = necklace.
            if (Has(n, "hair")) return OutfitSlot.Hair;
            if (Has(n, "glass")) return OutfitSlot.Glasses;
            if (Has(n, "wing")) return OutfitSlot.Wings;
            if (Has(n, "hat", "crown", "headband", "helmet", "cap")) return OutfitSlot.Hat;
            if (Has(n, "sock", "wazi")) return OutfitSlot.Socks;
            if (Has(n, "shoe", "xiezi", "tuoxie", "boot")) return OutfitSlot.Shoes;
            if (Has(n, "pant", "kuzi", "skirt", "qunzi", "short", "jean")) return OutfitSlot.Bottom;
            if (Has(n, "dress")) return OutfitSlot.Dress;
            if (Has(n, "clothes", "yifu", "blouse", "coat", "shirt", "neiyi", "jacket")) return OutfitSlot.Top;
            if (Has(n, "bag")) return OutfitSlot.Bag;
            if (Has(n, "bat", "racket", "umbrella", "bouquet", "skateboard", "ball", "dunpai", "shield")) return OutfitSlot.HandItem;
            if (Has(n, "ring", "necklace", "xianglian", "earring")) return OutfitSlot.Accessory;

            guessed = true;
            return OutfitSlot.Accessory;
        }

        // A top worn with no pants or skirt is a full-body piece (kimono, wedding dress, jumpsuit...).
        private static void DetectDresses(OutfitData data)
        {
            foreach (var variant in data.Variants)
            {
                var combo = Combine(variant, data.Shared);
                if (combo.Any(p => p.Slot == OutfitSlot.Bottom))
                    continue;

                foreach (var part in combo.Where(p => p.Slot == OutfitSlot.Top))
                    part.Slot = OutfitSlot.Dress;
            }
        }

        // Same rule as OutfitVariant.AllParts: own parts first, then shared for free slots.
        private static List<PartData> Combine(GroupData variant, GroupData shared)
        {
            var all = new List<PartData>();
            foreach (var part in variant.Parts.Concat(shared.Parts))
            {
                if (all.All(p => p.Slot != part.Slot))
                    all.Add(part);
            }

            return all;
        }

        // ---------- Making assets ----------

        private static OutfitCatalog Build(List<OutfitData> outfits, Transform modelRoot, Transform hips, Scene scene, List<string> warnings)
        {
            var bones = hips != null ? hips.GetComponentsInChildren<Transform>(true) : Array.Empty<Transform>();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var total = outfits.Sum(o => o.Shared.Parts.Count + o.Variants.Sum(v => v.Parts.Count));
            var done = 0;
            var outfitAssets = new List<OutfitSet>();
            var variantCount = 0;

            foreach (var data in outfits)
            {
                foreach (var part in data.Shared.Parts.Concat(data.Variants.SelectMany(v => v.Parts)))
                {
                    EditorUtility.DisplayProgressBar("Import Outfits", $"{data.Name}: {part.Label}", (float)done++ / Mathf.Max(1, total));
                    part.Asset = BuildPart(data, part, modelRoot, bones, scene, usedNames, warnings);
                }

                var folder = OutfitDataRoot + "/" + data.Key;
                EditorPaths.EnsureFolder(folder);

                var outfitAsset = LoadOrCreate<OutfitSet>($"{folder}/Outfit_{data.Key}.asset");
                var variants = new List<OutfitVariant>();

                for (var i = 0; i < data.Variants.Count; i++)
                {
                    var group = data.Variants[i];
                    var number = VariantNumber(group.Label, i);
                    var variant = LoadOrCreate<OutfitVariant>($"{folder}/{data.Key}_V{number}.asset");
                    variant.EditorSetup(KeepName(variant.DisplayName, variant.name, $"{data.Name} {number}"),
                        outfitAsset, group.Parts.Select(p => p.Asset).Where(a => a != null).ToArray());
                    EditorUtility.SetDirty(variant);
                    variants.Add(variant);
                }

                outfitAsset.EditorSetup(KeepName(outfitAsset.DisplayName, outfitAsset.name, data.Name),
                    data.Shared.Parts.Select(p => p.Asset).Where(a => a != null).ToArray(), variants.ToArray());
                EditorUtility.SetDirty(outfitAsset);
                outfitAssets.Add(outfitAsset);
                variantCount += variants.Count;
            }

            EditorPaths.EnsureFolder(OutfitDataRoot);
            var catalog = LoadOrCreate<OutfitCatalog>(CatalogPath);
            catalog.EditorSetOutfits(outfitAssets);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            var summary = $"Imported {outfitAssets.Count} outfits, {variantCount} variants, {total} parts.";
            var log = new StringBuilder(summary);
            foreach (var warning in warnings)
                log.Append("\n  • ").Append(warning);
            Debug.Log(log.ToString(), catalog);

            EditorUtility.DisplayDialog("Import Outfits",
                summary + (warnings.Count > 0 ? $"\n\n{warnings.Count} warnings, see the Console." : string.Empty), "OK");
            EditorGUIUtility.PingObject(catalog);
            return catalog;
        }

        private static OutfitItem BuildPart(OutfitData outfit, PartData part, Transform modelRoot, Transform[] bones,
            Scene scene, HashSet<string> usedNames, List<string> warnings)
        {
            var assetName = UniqueName($"{outfit.Key}_{Sanitize(part.Label)}", usedNames);
            var subFolder = SubFolderFor(part.Slot, outfit);
            var prefabFolder = $"{ItemPrefabRoot}/{subFolder}";
            var dataFolder = $"{ItemDataRoot}/{subFolder}";
            EditorPaths.EnsureFolder(prefabFolder);
            EditorPaths.EnsureFolder(dataFolder);

            var root = new GameObject(assetName);
            SceneManager.MoveGameObjectToScene(root, scene);

            var skinned = new List<SkinnedPart.SkinnedEntry>();
            var rigid = new List<SkinnedPart.RigidEntry>();

            foreach (var source in part.Renderers)
            {
                var child = new GameObject(source.name);
                child.layer = source.gameObject.layer;
                child.transform.SetParent(root.transform, false);

                if (source is SkinnedMeshRenderer srcSkin && srcSkin.bones.Length > 0)
                {
                    var dst = child.AddComponent<SkinnedMeshRenderer>();
                    dst.sharedMesh = srcSkin.sharedMesh;
                    dst.localBounds = srcSkin.localBounds;
                    dst.quality = srcSkin.quality;
                    dst.updateWhenOffscreen = srcSkin.updateWhenOffscreen;
                    dst.skinnedMotionVectors = srcSkin.skinnedMotionVectors;
                    CopyRendererSettings(srcSkin, dst);

                    if (srcSkin.sharedMesh != null)
                    {
                        for (var i = 0; i < srcSkin.sharedMesh.blendShapeCount; i++)
                            dst.SetBlendShapeWeight(i, srcSkin.GetBlendShapeWeight(i));
                    }

                    skinned.Add(new SkinnedPart.SkinnedEntry
                    {
                        renderer = dst,
                        rootBone = srcSkin.rootBone != null ? srcSkin.rootBone.name : null,
                        bones = srcSkin.bones.Select(b => b != null ? b.name : string.Empty).ToArray()
                    });
                }
                else
                {
                    var mesh = source is SkinnedMeshRenderer s ? s.sharedMesh
                        : source.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
                    if (mesh == null)
                    {
                        Object.DestroyImmediate(child);
                        continue;
                    }

                    child.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var dst = child.AddComponent<MeshRenderer>();
                    CopyRendererSettings(source, dst);

                    // In the prefab it sits where it was on the doll model (for previews)...
                    var src = source.transform;
                    child.transform.localPosition = modelRoot.InverseTransformPoint(src.position);
                    child.transform.localRotation = Quaternion.Inverse(modelRoot.rotation) * src.rotation;
                    child.transform.localScale = src.lossyScale;

                    // ...and on the doll it follows the nearest suitable bone.
                    var bone = NearestBone(source.bounds.center, bones, part.Slot);
                    if (bone == null)
                    {
                        warnings.Add($"'{source.name}': no bones found for this rigid mesh; it won't follow the animation.");
                        continue;
                    }

                    rigid.Add(new SkinnedPart.RigidEntry
                    {
                        target = child.transform,
                        bone = bone.name,
                        localPosition = bone.InverseTransformPoint(src.position),
                        localRotation = Quaternion.Inverse(bone.rotation) * src.rotation,
                        localScale = Divide(src.lossyScale, bone.lossyScale)
                    });
                }
            }

            root.AddComponent<SkinnedPart>().EditorSetup(skinned.ToArray(), rigid.ToArray());
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{prefabFolder}/{assetName}.prefab");
            Object.DestroyImmediate(root);

            var item = LoadOrCreate<OutfitItem>($"{dataFolder}/{assetName}.asset");
            var so = new SerializedObject(item);
            so.FindProperty("prefab").objectReferenceValue = prefab;
            so.FindProperty("slot").intValue = (int)part.Slot;

            var nameProp = so.FindProperty("displayName");
            if (string.IsNullOrEmpty(nameProp.stringValue))
                nameProp.stringValue = $"{outfit.Name} {SlotLabel(part.Slot)}";

            // A full-body piece takes off the shirt and pants (and they take it off).
            var clears = so.FindProperty("alsoClears");
            if (part.Slot == OutfitSlot.Dress)
            {
                clears.arraySize = 2;
                clears.GetArrayElementAtIndex(0).intValue = (int)OutfitSlot.Top;
                clears.GetArrayElementAtIndex(1).intValue = (int)OutfitSlot.Bottom;
            }
            else
            {
                clears.arraySize = 0;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        // ---------- Helpers ----------

        private static GameObject FindDollModel()
        {
            foreach (var guid in AssetDatabase.FindAssets($"\"{DollModelName}\" t:Model"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == DollModelName)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            // Fall back to a selected model.
            if (Selection.activeObject is GameObject selected && PrefabUtility.GetPrefabAssetType(selected) == PrefabAssetType.Model)
                return selected;

            EditorUtility.DisplayDialog("Import Outfits",
                $"Couldn't find the '{DollModelName}' model. Select the doll FBX in the Project window and try again.", "OK");
            return null;
        }

        // Where a part's prefab and config go, matching the existing _Items folders.
        private static string SubFolderFor(OutfitSlot slot, OutfitData outfit) => slot switch
        {
            OutfitSlot.Hair => "_Hairs",
            OutfitSlot.Hat => "_Head_accessaries",
            OutfitSlot.Glasses => "_Glasses",
            OutfitSlot.Wings => "_Angle_Wings",
            OutfitSlot.HandItem => "_Hand_Items",
            OutfitSlot.Bag => "_Hand_Items",
            _ => "__Dresss/" + DressFolderName(outfit)
        };

        // Reuses an existing folder like "_Forest_fairy" for "Forest Fairy" so nothing is duplicated.
        private static string DressFolderName(OutfitData outfit)
        {
            var parent = ItemPrefabRoot + "/__Dresss";
            if (AssetDatabase.IsValidFolder(parent))
            {
                var wanted = Normalise(outfit.Name);
                foreach (var folder in AssetDatabase.GetSubFolders(parent))
                {
                    var folderName = Path.GetFileName(folder);
                    if (Normalise(folderName) == wanted)
                        return folderName;
                }
            }

            return "_" + outfit.Key;
        }

        private static Transform NearestBone(Vector3 point, Transform[] bones, OutfitSlot slot)
        {
            Func<string, bool> preferred = slot switch
            {
                OutfitSlot.HandItem or OutfitSlot.Bag => n => n.EndsWith("RightHand") || n.EndsWith("LeftHand"),
                OutfitSlot.Hat or OutfitSlot.Hair or OutfitSlot.Glasses => n => n.EndsWith("Head"),
                OutfitSlot.Wings => n => n.Contains("Spine"),
                _ => null
            };

            var candidates = preferred != null ? bones.Where(b => preferred(b.name)).ToArray() : bones;
            if (candidates.Length == 0)
                candidates = bones;

            Transform best = null;
            var bestDistance = float.MaxValue;
            foreach (var bone in candidates)
            {
                var distance = (bone.position - point).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = bone;
                }
            }

            return best;
        }

        private static void CopyRendererSettings(Renderer from, Renderer to)
        {
            to.sharedMaterials = from.sharedMaterials;
            to.shadowCastingMode = from.shadowCastingMode;
            to.receiveShadows = from.receiveShadows;
            to.lightProbeUsage = from.lightProbeUsage;
            to.reflectionProbeUsage = from.reflectionProbeUsage;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // Keeps a display name someone typed; otherwise uses the generated one.
        private static string KeepName(string current, string assetName, string generated) =>
            string.IsNullOrEmpty(current) || current == assetName ? generated : current;

        private static string VariantNumber(string label, int index)
        {
            var digits = new string(label.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var number) ? number.ToString("00") : (index + 1).ToString("00");
        }

        private static string SlotLabel(OutfitSlot slot) => slot switch
        {
            OutfitSlot.Top => "Shirt",
            OutfitSlot.Bottom => "Pants",
            OutfitSlot.HandItem => "Hand Item",
            _ => slot.ToString()
        };

        private static bool IsJunk(Transform t) => t.name.StartsWith("Untitled", StringComparison.Ordinal);

        private static string LastSegment(string name)
        {
            var index = name.LastIndexOf(Sep, StringComparison.Ordinal);
            return (index >= 0 ? name.Substring(index + Sep.Length) : name).Trim();
        }

        private static bool Has(string text, params string[] words) => words.Any(w => text.Contains(w));

        private static string MakeKey(string name) => Sanitize(name.Replace(' ', '_'));

        private static string Normalise(string name) => name.Replace("_", "").Replace(" ", "").ToLowerInvariant();

        private static string Sanitize(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.Select(c => invalid.Contains(c) || c == ' ' || c == ':' ? '_' : c).ToArray();
            return new string(chars).Trim('_');
        }

        private static string UniqueName(string wanted, HashSet<string> used)
        {
            var candidate = wanted;
            for (var i = 2; !used.Add(candidate); i++)
                candidate = $"{wanted}_{i}";
            return candidate;
        }

        private static Vector3 Divide(Vector3 a, Vector3 b) =>
            new(b.x != 0 ? a.x / b.x : 1f, b.y != 0 ? a.y / b.y : 1f, b.z != 0 ? a.z / b.z : 1f);

        private static Transform FindDeep(Transform root, Func<Transform, bool> match)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (match(t))
                    return t;
            }

            return null;
        }

        private static void LogPreview(List<OutfitData> outfits, List<string> warnings)
        {
            var log = new StringBuilder($"Outfit import preview: {outfits.Count} outfits (nothing was created)\n");
            foreach (var outfit in outfits)
            {
                log.Append("\n■ ").Append(outfit.Name);
                if (outfit.Shared.Parts.Count > 0)
                    log.Append("\n    Shared: ").Append(Describe(outfit.Shared));
                foreach (var variant in outfit.Variants)
                    log.Append("\n    ").Append(variant.Label).Append(": ").Append(Describe(variant));
            }

            foreach (var warning in warnings)
                log.Append("\n  • ").Append(warning);

            Debug.Log(log.ToString());
        }

        private static string Describe(GroupData group) =>
            string.Join(", ", group.Parts.Select(p =>
                $"{p.Slot}={string.Join("+", p.Renderers.Select(r => r.name))}{(p.Guessed ? " (?)" : "")}"));
    }
}
#endif
