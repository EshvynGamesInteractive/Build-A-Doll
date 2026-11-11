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

        [Tooltip("Root bone of the doll's rig, for binding skinned clothes. Leave empty if the doll isn't skinned.")]
        [SerializeField] private Transform skeletonRoot;

        [Tooltip("Worn at the start (and after a reset).")]
        [SerializeField] private OutfitItem[] startingItems;

        private readonly Dictionary<OutfitSlot, OutfitItem> _worn = new();
        private readonly Dictionary<OutfitSlot, GameObject> _spawned = new();
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
        }

        private void Start() => WearStartingItems();

        private void OnEnable() => GameEvents.GameplayEvents.ResetRun.Register(WearStartingItems);

        private void OnDisable() => GameEvents.GameplayEvents.ResetRun.Unregister(WearStartingItems);

        public void Equip(OutfitItem item)
        {
            if (item == null)
                return;

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

        public void ClearAll()
        {
            foreach (var slot in new List<OutfitSlot>(_worn.Keys))
                Unequip(slot);
        }

        private void WearStartingItems()
        {
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
                return;

            var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0)
                return;

            _bonesByName ??= BuildBoneMap();

            foreach (var skinned in renderers)
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
        }

        private Dictionary<string, Transform> BuildBoneMap()
        {
            var map = new Dictionary<string, Transform>();
            foreach (var bone in skeletonRoot.GetComponentsInChildren<Transform>(true))
                map.TryAdd(bone.name, bone);
            return map;
        }
    }
}
