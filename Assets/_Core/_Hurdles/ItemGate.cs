using System.Collections.Generic;
using _Core._Player;
using Game.Outfit;
using Game.Player;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>
    /// A gate that puts its item on the doll when she runs through it.
    /// Pair two under a HurdleGroup for the classic left/right choice.
    /// The item can be set in the prefab or per level (LevelData gate items), so one gate prefab
    /// works for every item.
    /// </summary>
    public class ItemGate : TrackHurdle
    {
        public enum PreviewStyle { Auto = 0, Icon2D = 1, Model3D = 2 }

        [SerializeField] private OutfitItem item;

        [Tooltip("Auto = a 3D model of the item on floor rings (no preview anchor), the 2D icon on gates.")]
        [SerializeField] private PreviewStyle previewStyle = PreviewStyle.Auto;

        [Tooltip("Optional: a SpriteRenderer of your own that shows the item's icon. Empty = one is made at the preview anchor.")]
        [SerializeField] private SpriteRenderer iconRenderer;

        [Tooltip("Where the item's icon is shown on the red gate.")]
        [SerializeField] private Transform previewAnchor;

        [Tooltip("Where the item's icon is shown on the green gate (when the item is part of the target look).")]
        [SerializeField] private Transform greenPreviewAnchor;

        [Header("Icon")]
        [Tooltip("Largest side of the icon, in world units.")]
        [SerializeField, Min(0.1f)] private float iconSize = 1.6f;

        [Tooltip("Optional: material for the icon. Empty = the default sprite material.")]
        [SerializeField] private Material iconMaterial;

        [SerializeField] private int iconSortingOrder = 10;

        [Tooltip("Used when there's no preview anchor (e.g. floor rings): the icon stands upright at this offset from the gate.")]
        [SerializeField] private Vector3 iconOffset = Vector3.zero;

        [Tooltip("How far the icon bobs up and down, in world units. 0 = still.")]
        [SerializeField, Min(0f)] private float iconBob;

        [SerializeField, Min(0f)] private float iconBobSpeed = 2.5f;

        [Header("3D model preview (rings)")]
        [Tooltip("Largest side of the 3D model, in world units.")]
        [SerializeField, Min(0.1f)] private float modelSize = 1.3f;

        [Tooltip("Degrees per second the model turns. 0 = still, facing the runner.")]
        [SerializeField] private float modelSpinSpeed = 60f;

        private bool _iconOnSelf;

        private SpriteRenderer _icon;
        private Transform _model;       // spins + bobs, sits at the icon spot
        private OutfitItem _modelItem;  // item the model was built for
        private bool _modelHasMesh;
        private IReadOnlyList<OutfitItem> _targetItems;
        private bool _started;

        public OutfitItem Item => item;

        /// <summary>True when this gate's item is part of the target look (the green choice).</summary>
        public bool IsTargetChoice => MatchesTarget();

        private void Start()
        {
            _started = true;
            Refresh();
        }

        public void Configure(OutfitItem itemOverride, IReadOnlyList<OutfitItem> targetItems)
        {
            _targetItems = targetItems;
            SetItem(itemOverride != null ? itemOverride : item);
        }

        public void SetItem(OutfitItem newItem)
        {
            item = newItem;
            SetChoiceMatch(MatchesTarget());
            if (_started)
                Refresh();
        }

        private bool MatchesTarget()
        {
            if (item == null || _targetItems == null)
                return false;

            foreach (var targetItem in _targetItems)
            {
                if (item == targetItem)
                    return true;
            }

            return false;
        }

        protected override void OnPlayerHit(RunnerController player)
        {
            var outfit = player.DollOutfit;
            if (outfit == null)
            {
                Debug.LogWarning($"Player '{player.name}' has no {nameof(DollOutfit)}; '{name}' had nothing to dress.", this);
                return;
            }

            outfit.Equip(item);

            // The item's value (its Style Score) is paid out in coins: plus for good picks, minus for bad.
            if (item != null && player.Score != null)
                player.Score.AddCoins(item.StyleScore);
        }

        // Shows the item's 2D icon (OutfitItem.Icon) on the gate. Icons are made with
        // Build A Doll > Outfits > Render Item Icons.
        private void Refresh()
        {
            var sprite = item != null ? item.Icon : null;

            if (iconRenderer != null)
            {
                iconRenderer.sprite = sprite;
                return;
            }

            var anchor = ShowsGreen && greenPreviewAnchor != null ? greenPreviewAnchor : previewAnchor;
            if (!Application.isPlaying)
                return;

            // Floor rings have no anchor: the icon stands on this object, above the ring.
            _iconOnSelf = anchor == null;
            if (_iconOnSelf)
                anchor = transform;

            if (UsesModel && RefreshModel(anchor))
            {
                if (_icon != null)
                    _icon.gameObject.SetActive(false);
                return;
            }
            if (_model != null)
                _model.gameObject.SetActive(false);

            if (sprite == null)
            {
                if (_icon != null)
                    _icon.gameObject.SetActive(false);
                if (item != null)
                    Debug.LogWarning($"'{item.name}' has no icon, so gate '{name}' shows nothing. Run Build A Doll > Outfits > Render Item Icons.", item);
                return;
            }

            if (_icon == null)
            {
                var go = new GameObject("Icon");
                _icon = go.AddComponent<SpriteRenderer>();
                _icon.sortingOrder = iconSortingOrder;
                if (iconMaterial != null)
                    _icon.sharedMaterial = iconMaterial;
            }

            // Follows the anchor of whichever visual (red or green) is showing.
            _icon.transform.SetParent(anchor, false);
            _icon.transform.localPosition = _iconOnSelf ? iconOffset : Vector3.zero;
            _icon.transform.localRotation = Quaternion.identity;
            _icon.gameObject.SetActive(!(_iconOnSelf && IsUsed));
            _icon.sprite = sprite;

            var size = sprite.bounds.size;
            var largest = Mathf.Max(size.x, size.y);
            var scale = largest > 0f ? iconSize / largest : 1f;
            _icon.transform.localScale = new Vector3(scale, scale, 1f);
        }

        // Auto: 3D model for hair and accessories (the floor-ring items) or when there's no preview anchor; 2D icon otherwise.
        private bool UsesModel => previewStyle == PreviewStyle.Model3D ||
                                  (previewStyle == PreviewStyle.Auto &&
                                   ((previewAnchor == null && greenPreviewAnchor == null) || (item != null && IsRingSlot(item.Slot))));

        private static bool IsRingSlot(OutfitSlot slot) =>
            slot is OutfitSlot.Hair or OutfitSlot.Hat or OutfitSlot.Glasses or OutfitSlot.HandItem or OutfitSlot.Bag
                or OutfitSlot.Accessory or OutfitSlot.Wings or OutfitSlot.Makeup;

        // 3D preview: a static copy of the item's meshes (bind pose), centred and scaled to Model Size.
        // Returns false (show the icon instead) when the item has no prefab or no meshes.
        private bool RefreshModel(Transform anchor)
        {
            if (item == null || item.Prefab == null)
                return false;

            if (_model == null)
                _model = new GameObject("Preview3D").transform;

            if (_modelItem != item)
            {
                _modelItem = item;
                _modelHasMesh = BuildModel(item.Prefab, _model, modelSize);
            }

            if (!_modelHasMesh)
                return false;

            _model.SetParent(anchor, false);
            _model.localPosition = _iconOnSelf ? iconOffset : Vector3.zero;
            _model.localRotation = Quaternion.Euler(0f, 180f, 0f); // front towards the runner
            _model.localScale = Vector3.one;
            _model.gameObject.SetActive(!(_iconOnSelf && IsUsed));
            return true;
        }

        private static bool BuildModel(GameObject source, Transform pivot, float size)
        {
            for (var i = pivot.childCount - 1; i >= 0; i--)
                Destroy(pivot.GetChild(i).gameObject);

            var holder = new GameObject("Model").transform;
            holder.SetParent(pivot, false);
            var toRoot = source.transform.worldToLocalMatrix;
            var bounds = new Bounds();
            var any = false;

            void AddMesh(Mesh mesh, Material[] materials, Transform from)
            {
                if (mesh == null)
                    return;

                var matrix = toRoot * from.localToWorldMatrix;
                var go = new GameObject(mesh.name);
                go.transform.SetParent(holder, false);
                go.transform.localPosition = matrix.GetPosition();
                go.transform.localRotation = matrix.rotation;
                go.transform.localScale = matrix.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // Mesh bounds in the holder's space.
                var b = mesh.bounds;
                for (var c = 0; c < 8; c++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    var p = matrix.MultiplyPoint3x4(corner);
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }

            foreach (var skinned in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                AddMesh(skinned.sharedMesh, skinned.sharedMaterials, skinned.transform);
            foreach (var meshRenderer in source.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = meshRenderer.GetComponent<MeshFilter>();
                if (filter != null)
                    AddMesh(filter.sharedMesh, meshRenderer.sharedMaterials, meshRenderer.transform);
            }

            if (!any)
                return false;

            var largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            var scale = largest > 0.0001f ? size / largest : 1f;
            holder.localScale = Vector3.one * scale;
            holder.localPosition = -bounds.center * scale; // centred on the pivot
            return true;
        }

        // An icon / model under the red/green visual hides with it; one on this object is hidden here.
        protected override void OnVisualRefreshed()
        {
            if (!_iconOnSelf)
                return;

            var showsModel = _model != null && _modelHasMesh && UsesModel && _modelItem == item && item != null;
            if (showsModel)
                _model.gameObject.SetActive(!IsUsed);
            else if (_icon != null)
                _icon.gameObject.SetActive(!IsUsed && _icon.sprite != null);
        }

        private void Update()
        {
            var basePosition = _iconOnSelf ? iconOffset : Vector3.zero;
            var bob = iconBob > 0f ? Vector3.up * (Mathf.Sin(Time.time * iconBobSpeed) * iconBob) : Vector3.zero;

            if (_model != null && _model.gameObject.activeInHierarchy)
            {
                _model.localPosition = basePosition + bob;
                if (modelSpinSpeed != 0f)
                    _model.Rotate(0f, modelSpinSpeed * Time.deltaTime, 0f, Space.Self);
                return;
            }

            if (iconBob <= 0f || _icon == null || !_icon.gameObject.activeInHierarchy)
                return;

            _icon.transform.localPosition = basePosition + bob;
        }

        private void OnValidate()
        {
            if (iconRenderer != null && item != null && item.Icon != null)
                iconRenderer.sprite = item.Icon;
        }
    }
}
