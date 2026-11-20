using System;
using System.Collections.Generic;
using Core._GameEvent;
using UnityEngine;

namespace Game.Outfit
{
    /// <summary>
    /// What the doll is wearing. Equip() puts an item on (replacing whatever was in that slot),
    /// spawning its prefab at the slot's anchor. Skinned clothes are re-bound to the doll's
    /// skeleton by bone name so they animate with her.
    /// </summary>
    [DisallowMultipleComponent]
    public class DollOutfit : MonoBehaviour
    {
        [Serializable]
        private struct SlotAnchor
        {
            public OutfitSlot slot;
            public Transform anchor;
        }

        [Tooltip("Where each slot's item is spawned. Slots not listed use the default anchor.")]
        [SerializeField] private SlotAnchor[] anchors;

        [Tooltip("Used for slots without their own anchor. Leave empty to use this object.")]
        [SerializeField] private Transform defaultAnchor;

        [Tooltip("Root bone of the doll's rig (e.g. mixamorig:Hips), for binding skinned clothes. Empty = found from a humanoid Animator in children.")]
        [SerializeField] private Transform skeletonRoot;

        [Tooltip("Turn off for display dolls (e.g. the target-look demo) so their items don't count as the player's.")]
        [SerializeField] private bool raiseEvents = true;

        [Tooltip("Worn at the start (and after a reset).")]
        [SerializeField] private OutfitItem[] startingItems;

        /// <summary>Off for display dolls and the AI, so their items don't count as the player's.</summary>
        public bool RaiseEvents
        {
            get => raiseEvents;
            set => raiseEvents = value;
        }

        private readonly Dictionary<OutfitSlot, OutfitItem> _worn = new();
        private readonly Dictionary<OutfitSlot, GameObject> _spawned = new();
        private readonly Dictionary<OutfitSlot, OutfitItem> _cut = new(); // taken off by hurdles (scissors), can be given back
        private Dictionary<string, Transform> _bonesByName;

        public IReadOnlyDictionary<OutfitSlot, OutfitItem> Worn => _worn;

        /// <summary>Sum of the style score of everything currently worn.</summary>
        public int StyleScore
        {
            get
            {
                var total = 0;
                foreach (var item in _worn.Values)
                    total += item.StyleScore;
                return total;
            }
        }

        private void Awake()
        {
            if (defaultAnchor == null)
                defaultAnchor = transform;

            if (skeletonRoot == null)
                skeletonRoot = FindSkeletonRoot();
        }

        private void Start() => WearStartingItems();

        private void OnEnable() => GameEvents.GameplayEvents.ResetRun.Register(WearStartingItems);

        private void OnDisable() => GameEvents.GameplayEvents.ResetRun.Unregister(WearStartingItems);

        public void Equip(OutfitItem item)
        {
            if (item == null)
                return;

            if (_cut.Remove(item.Slot)) // she picked a new one: nothing to give back for this slot
                RaiseCutChanged();

            if (item.AlsoClears != null)
            {
                foreach (var slot in item.AlsoClears)
                    Unequip(slot);
            }

            // Wearing a Top or Bottom takes off any item that clears that slot (e.g. a Dress).
            foreach (var worn in new List<OutfitItem>(_worn.Values))
            {
                if (worn.AlsoClears != null && Array.IndexOf(worn.AlsoClears, item.Slot) >= 0)
                    Unequip(worn.Slot);
            }

            Unequip(item.Slot);
            _worn[item.Slot] = item;

            if (item.Prefab != null)
                _spawned[item.Slot] = Spawn(item);

            if (raiseEvents)
                GameEvents.GameplayEvents.OutfitItemEquipped.Raise(item);
        }

        public void Unequip(OutfitSlot slot)
        {
            _worn.Remove(slot);

            if (_spawned.TryGetValue(slot, out var go))
            {
                if (go != null)
                    Destroy(go);
                _spawned.Remove(slot);
            }
        }

        /// <summary>Sets what she wears at the start of a run (e.g. per level). Applied on the next ResetRun.</summary>
        public void SetStartingItems(System.Collections.Generic.IReadOnlyList<OutfitItem> items)
        {
            startingItems = new OutfitItem[items?.Count ?? 0];
            for (var i = 0; i < startingItems.Length; i++)
                startingItems[i] = items[i];
        }

        /// <summary>Takes everything off and puts on every part of the variant (own parts + shared pieces).</summary>
        public void WearVariant(OutfitVariant variant)
        {
            ClearAll();
            if (variant == null)
                return;

            foreach (var part in variant.AllParts)
                Equip(part);
        }

        /// <summary>Is every part of this variant on her right now?</summary>
        public bool IsWearing(OutfitVariant variant)
        {
            if (variant == null)
                return false;

            foreach (var part in variant.AllParts)
            {
                if (!_worn.TryGetValue(part.Slot, out var worn) || worn != part)
                    return false;
            }

            return true;
        }

        public void ClearAll()
        {
            foreach (var slot in new List<OutfitSlot>(_worn.Keys))
                Unequip(slot);
        }

        /// <summary>True while something cut off by a hurdle can still be given back (e.g. by a rewarded-ad gate).</summary>
        public bool HasCutItems => _cut.Count > 0;

        /// <summary>Takes the item in this slot off and remembers it, so RestoreCutItems can put it back.</summary>
        public void CutOff(OutfitSlot slot)
        {
            if (_worn.TryGetValue(slot, out var item) && item != null)
                _cut[slot] = item;
            Unequip(slot);
            RaiseCutChanged();
        }

        private void RaiseCutChanged()
        {
            if (raiseEvents)
                GameEvents.GameplayEvents.CutItemsChanged.Raise(_cut.Count > 0);
        }

        /// <summary>Puts back everything cut off by hurdles this run. Returns how many items came back.</summary>
        public int RestoreCutItems()
        {
            var items = new List<OutfitItem>(_cut.Values);
            _cut.Clear();
            foreach (var item in items)
                Equip(item);
            RaiseCutChanged();
            return items.Count;
        }

        private void WearStartingItems()
        {
            _cut.Clear();
            RaiseCutChanged();
            ClearAll();

            if (startingItems == null)
                return;

            foreach (var item in startingItems)
                Equip(item);
        }

        private GameObject Spawn(OutfitItem item)
        {
            var instance = Instantiate(item.Prefab, GetAnchor(item.Slot), false);
            instance.name = item.Prefab.name;
            BindToSkeleton(instance);
            return instance;
        }

        private Transform GetAnchor(OutfitSlot slot)
        {
            if (anchors != null)
            {
                foreach (var entry in anchors)
                {
                    if (entry.slot == slot && entry.anchor != null)
                        return entry.anchor;
                }
            }

            return defaultAnchor;
        }

        private void BindToSkeleton(GameObject instance)
        {
            if (skeletonRoot == null)
                skeletonRoot = FindSkeletonRoot();

            if (skeletonRoot == null)
                return;

            // Parts made by the Outfit Importer carry their bone names.
            var part = instance.GetComponent<SkinnedPart>();
            if (part != null)
            {
                _bonesByName ??= BuildBoneMap();
                part.Bind(_bonesByName);
                return;
            }

            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0)
                return;

            _bonesByName ??= BuildBoneMap();

            foreach (var skinned in renderers)
                RebindBones(skinned);
        }

        // Points one skinned mesh's bones at the doll's bones with the same names.
        private void RebindBones(SkinnedMeshRenderer skinned)
        {
            var bones = skinned.bones;
            for (var i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null && _bonesByName.TryGetValue(bones[i].name, out var match))
                    bones[i] = match;
            }

            skinned.bones = bones;

            if (skinned.rootBone != null && _bonesByName.TryGetValue(skinned.rootBone.name, out var root))
                skinned.rootBone = root;
        }

        private Transform FindSkeletonRoot()
        {
            var animator = GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman)
            {
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null)
                    return hips;
            }

            return null;
        }

        private Dictionary<string, Transform> BuildBoneMap()
        {
            var map = new Dictionary<string, Transform>();
            foreach (var bone in skeletonRoot.GetComponentsInChildren<Transform>(true))
                map.TryAdd(bone.name, bone);

            // Some rigs bind meshes to the armature object above the hips too.
            if (skeletonRoot.parent != null)
                map.TryAdd(skeletonRoot.parent.name, skeletonRoot.parent);

            return map;
        }
    }
}
