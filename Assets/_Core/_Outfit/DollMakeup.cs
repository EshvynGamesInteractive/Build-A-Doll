using System;
using System.Collections.Generic;
using Core._GameEvent;
using UnityEngine;

namespace Game.Outfit
{
    /// <summary>
    /// Paints makeup (lips, eye shadow, blush) on the doll's face. Each zone is marked by one or more "volumes":
    /// child objects of the head bone whose position, rotation and scale make an ellipsoid (see the gizmos).
    /// When a zone is first painted, the face triangles inside its volumes are copied into a thin overlay that
    /// follows the head (soft edges, drawn with BuildADoll/MakeupOverlay), so the shared texture atlas is never touched.
    /// Right-click → Create Default Zones places starting volumes from the head mesh; drag them onto the lips and eyelids.
    /// Paint clears on ResetRun (back to the starting makeup).
    /// </summary>
    [DisallowMultipleComponent]
    public class DollMakeup : MonoBehaviour
    {
        [Serializable]
        public class Zone
        {
            public MakeupZone zone;
            [Tooltip("Ellipsoids (child objects of the head bone) covering this zone. Scale = size of the ellipsoid.")]
            public Transform[] volumes;
        }

        [SerializeField] private List<Zone> zones = new();

        [Tooltip("Face meshes to paint on. Empty = the doll's own skinned meshes found at start (not clothes), minus names listed below.")]
        [SerializeField] private SkinnedMeshRenderer[] faceRenderers;
        [Tooltip("Skinned meshes whose name contains any of these are never painted (eyebrows, lashes, hair...).")]
        [SerializeField] private string[] skipNamesContaining = { "eyebrow", "lash", "hair", "brow" };

        [Header("Look")]
        [Tooltip("Optional material for the paint. Empty = Resources/BuildADoll/MakeupOverlay.")]
        [SerializeField] private Material overlayMaterial;
        [Tooltip("How far the paint sits off the face, as a share of the face mesh size (stops flicker).")]
        [SerializeField, Range(0f, 0.02f)] private float surfaceOffset = 0.002f;
        [Tooltip("Where the paint starts fading towards the volume's edge (0 = fades from the centre, 1 = hard edge).")]
        [SerializeField, Range(0f, 1f)] private float edgeSoftStart = 0.55f;
        [Tooltip("Seconds the paint takes to fade in when a gate paints her.")]
        [SerializeField, Min(0f)] private float paintTime = 0.35f;

        [Tooltip("Off for display dolls and the AI, so their changes don't count as the player's.")]
        [SerializeField] private bool raiseEvents = true;

        private class Layer
        {
            public readonly List<SkinnedMeshRenderer> Renderers = new();
            public Material Material;
            public bool Built;
            public float Fade;      // 0..1 shown amount
            public bool On;
        }

        private readonly Dictionary<MakeupZone, Color> _painted = new();
        private readonly Dictionary<MakeupZone, Layer> _layers = new();
        private MakeupColor[] _startingMakeup = Array.Empty<MakeupColor>();
        private readonly List<SkinnedMeshRenderer> _faceAtStart = new();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public bool RaiseEvents
        {
            get => raiseEvents;
            set => raiseEvents = value;
        }

        public IReadOnlyDictionary<MakeupZone, Color> Painted => _painted;

        public bool TryGetColor(MakeupZone zone, out Color color) => _painted.TryGetValue(zone, out color);

        public bool HasZone(MakeupZone zone) => FindZone(zone) != null;

        private void Awake()
        {
            // Before DollOutfit's Start spawns clothes: whatever skinned meshes exist now are the doll's own body.
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                _faceAtStart.Add(r);

            if (zones.Count == 0)
                CreateDefaultZones();
        }

        private void OnEnable() => GameEvents.GameplayEvents.ResetRun.Register(ResetMakeup);
        private void OnDisable() => GameEvents.GameplayEvents.ResetRun.Unregister(ResetMakeup);
        private void Start() => ResetMakeup();

        /// <summary>Paints a zone (replaces any colour already there).</summary>
        public void Paint(MakeupZone zone, Color color, bool instant = false)
        {
            var layer = GetLayer(zone);
            if (layer == null)
                return;

            _painted[zone] = color;
            SetLayerColor(layer, color);
            layer.On = true;
            if (instant || paintTime <= 0f)
                layer.Fade = 1f;
            ApplyFade(layer);

            if (raiseEvents)
                GameEvents.GameplayEvents.MakeupPainted.Raise(zone, color);
        }

        /// <summary>Makeup worn at the start (e.g. the demo doll showing the target makeup). Applied on the next ResetRun.</summary>
        public void SetStartingMakeup(IReadOnlyList<MakeupColor> makeup)
        {
            _startingMakeup = new MakeupColor[makeup?.Count ?? 0];
            for (var i = 0; i < _startingMakeup.Length; i++)
                _startingMakeup[i] = makeup[i];
        }

        private void ResetMakeup()
        {
            _painted.Clear();
            foreach (var layer in _layers.Values)
            {
                layer.On = false;
                layer.Fade = 0f;
                ApplyFade(layer);
            }

            var events = raiseEvents;
            raiseEvents = false;
            foreach (var m in _startingMakeup)
                Paint(m.zone, m.color, true);
            raiseEvents = events;
        }

        private void Update()
        {
            if (paintTime <= 0f)
                return;
            foreach (var layer in _layers.Values)
            {
                var target = layer.On ? 1f : 0f;
                if (Mathf.Approximately(layer.Fade, target))
                    continue;
                layer.Fade = Mathf.MoveTowards(layer.Fade, target, Time.deltaTime / paintTime);
                ApplyFade(layer);
            }
        }

        // ---------- Layers ----------

        private Layer GetLayer(MakeupZone zone)
        {
            if (_layers.TryGetValue(zone, out var layer))
                return layer;

            var z = FindZone(zone);
            if (z == null || z.volumes == null || z.volumes.Length == 0)
            {
                Debug.LogWarning($"'{name}' has no {zone} zone on its {nameof(DollMakeup)}, so it can't be painted. Right-click the component → Create Default Zones.", this);
                return null;
            }

            layer = new Layer { Material = new Material(LoadMaterial()) { name = $"Makeup_{zone}" } };
            _layers[zone] = layer;
            BuildLayer(zone, z, layer);
            return layer;
        }

        private Material LoadMaterial()
        {
            if (overlayMaterial != null)
                return overlayMaterial;
            var mat = Resources.Load<Material>("BuildADoll/MakeupOverlay");
            if (mat != null)
                return mat;
            var shader = Shader.Find("BuildADoll/MakeupOverlay");
            return new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
        }

        private Zone FindZone(MakeupZone zone)
        {
            foreach (var z in zones)
                if (z != null && z.zone == zone)
                    return z;
            return null;
        }

        private IEnumerable<SkinnedMeshRenderer> FaceRenderers()
        {
            if (faceRenderers != null && faceRenderers.Length > 0)
            {
                foreach (var r in faceRenderers)
                    if (r != null)
                        yield return r;
                yield break;
            }

            foreach (var r in _faceAtStart)
            {
                if (r == null || !r.gameObject.activeInHierarchy || !r.enabled || r.sharedMesh == null)
                    continue;
                if (IsSkipped(r.name))
                    continue;
                yield return r;
            }
        }

        private bool IsSkipped(string objectName)
        {
            if (skipNamesContaining == null)
                return false;
            var lower = objectName.ToLowerInvariant();
            foreach (var s in skipNamesContaining)
                if (!string.IsNullOrEmpty(s) && lower.Contains(s.ToLowerInvariant()))
                    return true;
            return false;
        }

        // Copies the face triangles inside the zone's volumes into a skinned overlay mesh on the same bones.
        private void BuildLayer(MakeupZone zone, Zone z, Layer layer)
        {
            layer.Built = true;
            var baked = new Mesh();

            foreach (var source in FaceRenderers())
            {
                var mesh = source.sharedMesh;
                if (!mesh.isReadable)
                {
                    Debug.LogWarning($"Can't paint on '{source.name}': its mesh isn't readable. Turn on Read/Write in the model's import settings.", source);
                    continue;
                }

                // Where each vertex is right now (world space), to test against the volumes.
                source.BakeMesh(baked, true);
                var posed = baked.vertices;
                var toWorld = BakedToWorld(source, posed);

                var vertices = mesh.vertices;
                var normals = mesh.normals;
                var alphas = new float[vertices.Length];
                var anyInside = false;
                for (var i = 0; i < posed.Length && i < vertices.Length; i++)
                {
                    alphas[i] = Coverage(toWorld.MultiplyPoint3x4(posed[i]), z.volumes);
                    anyInside |= alphas[i] > 0f;
                }
                if (!anyInside)
                    continue;

                // Triangles with at least one vertex inside, all sub-meshes together.
                var remap = new Dictionary<int, int>();
                var newVerts = new List<Vector3>();
                var newNormals = new List<Vector3>();
                var newColors = new List<Color>();
                var newWeights = new List<BoneWeight>();
                var newTris = new List<int>();
                var weights = mesh.boneWeights;
                var offset = surfaceOffset * mesh.bounds.size.magnitude;

                for (var sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    if (mesh.GetTopology(sub) != MeshTopology.Triangles)
                        continue;
                    var tris = mesh.GetTriangles(sub);
                    for (var t = 0; t + 2 < tris.Length; t += 3)
                    {
                        int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                        if (alphas[a] <= 0f && alphas[b] <= 0f && alphas[c] <= 0f)
                            continue;
                        newTris.Add(Remap(a));
                        newTris.Add(Remap(b));
                        newTris.Add(Remap(c));
                    }
                }

                int Remap(int index)
                {
                    if (remap.TryGetValue(index, out var mapped))
                        return mapped;
                    mapped = newVerts.Count;
                    remap[index] = mapped;
                    var n = normals.Length > index ? normals[index] : Vector3.zero;
                    newVerts.Add(vertices[index] + n * offset);
                    newNormals.Add(n);
                    newColors.Add(new Color(1f, 1f, 1f, alphas[index]));
                    if (weights.Length > index)
                        newWeights.Add(weights[index]);
                    return mapped;
                }

                var overlay = new Mesh { name = $"{source.sharedMesh.name}_{zone}" };
                if (newVerts.Count > 65000)
                    overlay.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                overlay.SetVertices(newVerts);
                overlay.SetNormals(newNormals);
                overlay.SetColors(newColors);
                overlay.SetTriangles(newTris, 0);
                if (newWeights.Count == newVerts.Count)
                    overlay.boneWeights = newWeights.ToArray();
                overlay.bindposes = mesh.bindposes;
                overlay.RecalculateBounds();

                var go = new GameObject($"Makeup_{zone}_{source.name}");
                go.transform.SetParent(source.transform.parent, false);
                go.transform.localPosition = source.transform.localPosition;
                go.transform.localRotation = source.transform.localRotation;
                go.transform.localScale = source.transform.localScale;

                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = overlay;
                smr.bones = source.bones;
                smr.rootBone = source.rootBone;
                smr.localBounds = source.localBounds;
                smr.updateWhenOffscreen = source.updateWhenOffscreen;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                smr.receiveShadows = false;
                smr.sharedMaterial = layer.Material;
                smr.enabled = false;
                layer.Renderers.Add(smr);
            }

            Destroy(baked);

            if (layer.Renderers.Count == 0)
                Debug.LogWarning($"'{name}': no face triangles inside the {zone} volumes. Move them onto the face (they're ellipsoids under the head bone).", this);
        }

        // BakeMesh(useScale: true) should already hold the renderer's scale (so only move + rotate), but check it
        // against where the renderer really is and fall back to the full matrix if that lines up better.
        private static Matrix4x4 BakedToWorld(SkinnedMeshRenderer source, Vector3[] posed)
        {
            var noScale = Matrix4x4.TRS(source.transform.position, source.transform.rotation, Vector3.one);
            var full = source.transform.localToWorldMatrix;
            if (posed.Length == 0)
                return noScale;

            var real = source.bounds;
            float Error(Matrix4x4 m)
            {
                var b = new Bounds(m.MultiplyPoint3x4(posed[0]), Vector3.zero);
                var stride = Mathf.Max(1, posed.Length / 500);
                for (var i = 0; i < posed.Length; i += stride)
                    b.Encapsulate(m.MultiplyPoint3x4(posed[i]));
                return (b.center - real.center).magnitude + (b.size - real.size).magnitude;
            }

            return Error(noScale) <= Error(full) ? noScale : full;
        }

        // 1 at a volume's centre, fading to 0 at its edge (the best of all volumes).
        private float Coverage(Vector3 world, Transform[] volumes)
        {
            var best = 0f;
            foreach (var v in volumes)
            {
                if (v == null)
                    continue;
                // Unit sphere in the volume's own space = the ellipsoid.
                var local = v.InverseTransformPoint(world) * 2f; // scale 1 = 1 unit across
                var d = local.magnitude;
                if (d >= 1f)
                    continue;
                var a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edgeSoftStart, 1f, d));
                best = Mathf.Max(best, a);
            }
            return best;
        }

        private static void SetLayerColor(Layer layer, Color color)
        {
            if (layer.Material == null)
                return;
            if (layer.Material.HasProperty(BaseColorId))
                layer.Material.SetColor(BaseColorId, color);
            if (layer.Material.HasProperty(ColorId))
                layer.Material.SetColor(ColorId, color);
        }

        private static void ApplyFade(Layer layer)
        {
            var visible = layer.Fade > 0.001f;
            foreach (var r in layer.Renderers)
                if (r != null)
                    r.enabled = visible;
            if (layer.Material != null && layer.Material.HasProperty("_Strength"))
                layer.Material.SetFloat("_Strength", 0.85f * layer.Fade);
        }

        // ---------- Default zones ----------

        /// <summary>
        /// Makes starting volumes for lips, both eyelids and both cheeks from the head mesh's size, under the head bone.
        /// They're only a first guess: select them and drag / scale them onto the face (gizmos show each one).
        /// </summary>
        [ContextMenu("Create Default Zones")]
        public void CreateDefaultZones()
        {
            var animator = GetComponentInChildren<Animator>(true);
            var headBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (headBone == null)
            {
                Debug.LogWarning($"'{name}': no humanoid head bone, so default makeup zones can't be placed. Add the volumes by hand.", this);
                return;
            }

            // The head: the doll's own skinned mesh whose bounds centre is closest to (and above) the head bone.
            var head = FindHeadBounds(headBone, out var bounds);
            if (!head)
                bounds = new Bounds(headBone.position + Vector3.up * 0.15f, Vector3.one * 0.3f);

            var root = transform;
            var fwd = Vector3.ProjectOnPlane(root.forward, Vector3.up).normalized;
            if (fwd == Vector3.zero)
                fwd = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, fwd);
            var ext = bounds.extents;
            var front = bounds.center + fwd * ext.z * 0.85f;

            zones.Clear();
            AddZone(MakeupZone.Lips, headBone, fwd,
                (front - Vector3.up * ext.y * 0.42f, new Vector3(ext.x * 0.5f, ext.y * 0.16f, ext.z * 0.35f)));
            AddZone(MakeupZone.EyeShadow, headBone, fwd,
                (front + Vector3.up * ext.y * 0.02f - right * ext.x * 0.38f, new Vector3(ext.x * 0.38f, ext.y * 0.2f, ext.z * 0.35f)),
                (front + Vector3.up * ext.y * 0.02f + right * ext.x * 0.38f, new Vector3(ext.x * 0.38f, ext.y * 0.2f, ext.z * 0.35f)));
            AddZone(MakeupZone.Blush, headBone, fwd,
                (front - Vector3.up * ext.y * 0.22f - right * ext.x * 0.5f, new Vector3(ext.x * 0.32f, ext.y * 0.22f, ext.z * 0.4f)),
                (front - Vector3.up * ext.y * 0.22f + right * ext.x * 0.5f, new Vector3(ext.x * 0.32f, ext.y * 0.22f, ext.z * 0.4f)));
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private void AddZone(MakeupZone zone, Transform headBone, Vector3 fwd, params (Vector3 position, Vector3 size)[] parts)
        {
            var z = new Zone { zone = zone, volumes = new Transform[parts.Length] };
            for (var i = 0; i < parts.Length; i++)
            {
                var go = new GameObject(parts.Length > 1 ? $"Makeup_{zone}_{(i == 0 ? "L" : "R")}" : $"Makeup_{zone}");
                var t = go.transform;
                t.SetParent(headBone, true);
                t.position = parts[i].position;
                t.rotation = Quaternion.LookRotation(fwd, Vector3.up);
                // Scale is the ellipsoid's full size in world units, so undo the head bone's scale.
                var parentScale = headBone.lossyScale;
                var size = parts[i].size * 2f;
                t.localScale = new Vector3(size.x / Mathf.Max(1e-4f, parentScale.x), size.y / Mathf.Max(1e-4f, parentScale.y), size.z / Mathf.Max(1e-4f, parentScale.z));
#if UNITY_EDITOR
                UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create makeup zone");
#endif
                z.volumes[i] = t;
            }
            zones.Add(z);
        }

        private bool FindHeadBounds(Transform headBone, out Bounds bounds)
        {
            bounds = default;
            var found = false;
            var best = float.MaxValue;
            var candidates = _faceAtStart.Count > 0 ? _faceAtStart : new List<SkinnedMeshRenderer>(GetComponentsInChildren<SkinnedMeshRenderer>(true));
            foreach (var r in candidates)
            {
                if (r == null || !r.gameObject.activeInHierarchy || IsSkipped(r.name))
                    continue;
                var b = r.bounds;
                if (b.center.y < headBone.position.y - 0.05f)
                    continue; // body / legs
                var d = Vector3.Distance(b.center, headBone.position);
                if (d < best)
                {
                    best = d;
                    bounds = b;
                    found = true;
                }
            }
            return found;
        }

        private void OnDrawGizmosSelected()
        {
            foreach (var z in zones)
            {
                if (z?.volumes == null)
                    continue;
                Gizmos.color = z.zone switch
                {
                    MakeupZone.Lips => new Color(1f, 0.2f, 0.4f, 0.8f),
                    MakeupZone.EyeShadow => new Color(0.8f, 0.3f, 1f, 0.8f),
                    _ => new Color(1f, 0.6f, 0.6f, 0.8f)
                };
                foreach (var v in z.volumes)
                {
                    if (v == null)
                        continue;
                    Gizmos.matrix = v.localToWorldMatrix;
                    Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
                }
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
