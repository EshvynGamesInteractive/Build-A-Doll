using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Outfit
{
    /// <summary>
    /// Sits on the root of an outfit part prefab made by the Outfit Importer. A prefab can't point
    /// at the doll's bones, so bones are saved by name and looked up on the doll when the part spawns.
    /// Skinned meshes are re-bound to the doll's skeleton; rigid meshes (no skin weights, e.g. some
    /// hand items) are parented to the bone they follow so they move with her.
    /// One part can hold several meshes that are worn together (e.g. hair + its strands).
    /// </summary>
    [DisallowMultipleComponent]
    public class SkinnedPart : MonoBehaviour
    {
        [Serializable]
        public class SkinnedEntry
        {
            public SkinnedMeshRenderer renderer;
            public string rootBone;
            public string[] bones;
        }

        [Serializable]
        public class RigidEntry
        {
            public Transform target;
            public string bone;
            public Vector3 localPosition;
            public Quaternion localRotation = Quaternion.identity;
            public Vector3 localScale = Vector3.one;
        }

        [SerializeField] private SkinnedEntry[] skinned = Array.Empty<SkinnedEntry>();
        [SerializeField] private RigidEntry[] rigid = Array.Empty<RigidEntry>();

        /// <summary>Points this part at the doll's bones. Returns false if any bone was missing.</summary>
        public bool Bind(IReadOnlyDictionary<string, Transform> dollBones)
        {
            var allFound = true;

            if (skinned != null)
            {
                foreach (var entry in skinned)
                {
                    if (entry == null || entry.renderer == null)
                        continue;

                    var bones = new Transform[entry.bones?.Length ?? 0];
                    for (var i = 0; i < bones.Length; i++)
                    {
                        if (string.IsNullOrEmpty(entry.bones[i]))
                            continue;

                        if (!dollBones.TryGetValue(entry.bones[i], out bones[i]))
                        {
                            allFound = false;
                            Debug.LogWarning($"'{name}': the doll has no bone '{entry.bones[i]}'.", this);
                        }
                    }

                    entry.renderer.bones = bones;

                    if (!string.IsNullOrEmpty(entry.rootBone) && dollBones.TryGetValue(entry.rootBone, out var root))
                        entry.renderer.rootBone = root;
                }
            }

            if (rigid != null)
            {
                foreach (var entry in rigid)
                {
                    if (entry == null || entry.target == null || string.IsNullOrEmpty(entry.bone))
                        continue;

                    if (!dollBones.TryGetValue(entry.bone, out var bone))
                    {
                        allFound = false;
                        Debug.LogWarning($"'{name}': the doll has no bone '{entry.bone}' to follow.", this);
                        continue;
                    }

                    // Destroying this part's root also has to remove these, see OnDestroy.
                    entry.target.SetParent(bone, false);
                    entry.target.localPosition = entry.localPosition;
                    entry.target.localRotation = entry.localRotation;
                    entry.target.localScale = entry.localScale;
                }
            }

            return allFound;
        }

        /// <summary>
        /// For showing the part on its own (e.g. above a gate) with no doll to bind to: skinned
        /// meshes are drawn in their rest pose, and the whole part is centred on this object.
        /// </summary>
        public void PrepareAsPreview()
        {
            var hasBounds = false;
            var bounds = new Bounds();

            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                Bounds local;
                if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                {
                    smr.bones = Array.Empty<Transform>();
                    smr.rootBone = null;
                    local = smr.sharedMesh.bounds;
                    local.center = transform.InverseTransformPoint(smr.transform.TransformPoint(local.center));
                }
                else if (r.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null)
                {
                    local = filter.sharedMesh.bounds;
                    local.center = transform.InverseTransformPoint(r.transform.TransformPoint(local.center));
                }
                else
                {
                    continue;
                }

                if (hasBounds)
                    bounds.Encapsulate(local);
                else
                    bounds = local;
                hasBounds = true;
            }

            if (!hasBounds)
                return;

            foreach (Transform child in transform)
                child.localPosition -= bounds.center;
        }

        // Rigid meshes were moved under the doll's bones, so they aren't children any more.
        private void OnDestroy()
        {
            if (rigid == null)
                return;

            foreach (var entry in rigid)
            {
                if (entry != null && entry.target != null && !entry.target.IsChildOf(transform))
                    Destroy(entry.target.gameObject);
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor only: filled in by the Outfit Importer.</summary>
        public void EditorSetup(SkinnedEntry[] skinnedEntries, RigidEntry[] rigidEntries)
        {
            skinned = skinnedEntries ?? Array.Empty<SkinnedEntry>();
            rigid = rigidEntries ?? Array.Empty<RigidEntry>();
        }
#endif
    }
}
