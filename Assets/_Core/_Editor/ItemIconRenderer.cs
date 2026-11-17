#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Outfit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// Renders every outfit part to a transparent PNG and sets it as the item's Icon, which the
    /// gates show as a 2D sprite. Each part is put on the doll's skeleton (body hidden) so it has
    /// the shape it has when worn, then photographed from the front.
    /// Icons you made yourself are kept unless you choose "Re-render All".
    /// </summary>
    public static class ItemIconRenderer
    {
        private const string DollModelName = "Doll Rigged";
        private const int Size = 512;

        // Camera angle: a little from the side and above, so parts don't look flat.
        private const float Yaw = 20f;
        private const float Pitch = 12f;
        private const float FieldOfView = 20f;
        private const float Padding = 1.08f;

        public static string IconRoot => "Assets/_ART/_2D/_ItemIcons";

        [MenuItem("Build A Doll/Outfits/Render Item Icons (missing only)")]
        private static void RenderMissing() => RenderAll(overwrite: false);

        [MenuItem("Build A Doll/Outfits/Render Item Icons (re-render all)")]
        private static void RenderEverything() => RenderAll(overwrite: true);

        [MenuItem("CONTEXT/OutfitItem/Render Icon")]
        private static void RenderOne(MenuCommand command) =>
            Render(new List<OutfitItem> { (OutfitItem)command.context }, overwrite: true);

        private static void RenderAll(bool overwrite)
        {
            var items = AssetDatabase.FindAssets("t:OutfitItem")
                .Select(g => AssetDatabase.LoadAssetAtPath<OutfitItem>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(i => i != null && i.Prefab != null)
                .ToList();

            Render(items, overwrite);
        }

        public static void Render(List<OutfitItem> items, bool overwrite)
        {
            var model = FindModel();
            var scene = EditorSceneManager.NewPreviewScene();
            var made = 0;
            var skipped = 0;
            var failed = new List<string>();
            var written = new List<(OutfitItem item, string path)>();

            RenderTexture rt = null;
            Texture2D black = null, white = null;

            try
            {
                var camera = MakeCamera(scene);
                MakeLights(scene);
                rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                black = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                white = new Texture2D(Size, Size, TextureFormat.RGBA32, false);

                for (var i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (EditorUtility.DisplayCancelableProgressBar("Render Item Icons", item.name, (float)i / items.Count))
                        break;

                    // Keep icons someone drew or picked by hand.
                    if (!overwrite && item.Icon != null)
                    {
                        skipped++;
                        continue;
                    }

                    var path = IconPathFor(item);
                    if (RenderItem(item, model, scene, camera, rt, black, white, path))
                    {
                        written.Add((item, path));
                        made++;
                    }
                    else
                    {
                        failed.Add(item.name);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                EditorSceneManager.ClosePreviewScene(scene);
                if (rt != null) Object.DestroyImmediate(rt);
                if (black != null) Object.DestroyImmediate(black);
                if (white != null) Object.DestroyImmediate(white);
            }

            // Import the PNGs as sprites and hook them up.
            AssetDatabase.Refresh();
            foreach (var (item, path) in written)
                AssignIcon(item, path);
            AssetDatabase.SaveAssets();

            var message = $"Rendered {made} icons into {IconRoot}." +
                          (skipped > 0 ? $"\nKept {skipped} items that already had an icon." : string.Empty) +
                          (failed.Count > 0 ? $"\nNothing visible for: {string.Join(", ", failed)}" : string.Empty);
            Debug.Log(message);
            EditorUtility.DisplayDialog("Render Item Icons", message, "OK");
        }

        private static bool RenderItem(OutfitItem item, GameObject model, UnityEngine.SceneManagement.Scene scene,
            Camera camera, RenderTexture rt, Texture2D black, Texture2D white, string path)
        {
            GameObject doll = null;
            GameObject part = null;

            try
            {
                part = (GameObject)Object.Instantiate(item.Prefab);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(part, scene);

                var skinnedPart = part.GetComponent<SkinnedPart>();
                if (skinnedPart != null && model != null)
                {
                    // Wear it on an invisible doll so skinned meshes take their worn shape.
                    doll = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                    foreach (var r in doll.GetComponentsInChildren<Renderer>(true))
                        r.enabled = false;

                    skinnedPart.Bind(BoneMap(doll.transform));
                }

                var renderers = part.GetComponentsInChildren<Renderer>(true)
                    .Concat(doll != null ? doll.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled) : Enumerable.Empty<Renderer>())
                    .Where(r => r.enabled)
                    .ToArray();
                if (renderers.Length == 0)
                    return false;

                var bounds = renderers[0].bounds;
                foreach (var r in renderers)
                    bounds.Encapsulate(r.bounds);
                if (bounds.size.sqrMagnitude < 1e-8f)
                    return false;

                Frame(camera, bounds);

                // Black and white backgrounds give the alpha, whatever the pipeline does with it.
                Grab(camera, rt, Color.black, black);
                Grab(camera, rt, Color.white, white);
                var icon = Composite(black, white);
                if (icon == null)
                    return false;

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, icon.EncodeToPNG());
                Object.DestroyImmediate(icon);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Icon for '{item.name}' failed: {e.Message}", item);
                return false;
            }
            finally
            {
                if (part != null) Object.DestroyImmediate(part);
                if (doll != null) Object.DestroyImmediate(doll);
            }
        }

        private static Camera MakeCamera(UnityEngine.SceneManagement.Scene scene)
        {
            var go = new GameObject("IconCamera");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            var camera = go.AddComponent<Camera>();
            camera.scene = scene;
            camera.cameraType = CameraType.Preview;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.fieldOfView = FieldOfView;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.enabled = false;
            return camera;
        }

        private static void MakeLights(UnityEngine.SceneManagement.Scene scene)
        {
            AddLight(scene, "Key", new Vector3(30f, 160f, 0f), 1.1f);
            AddLight(scene, "Fill", new Vector3(10f, -120f, 0f), 0.5f);
        }

        private static void AddLight(UnityEngine.SceneManagement.Scene scene, string name, Vector3 euler, float intensity)
        {
            var go = new GameObject(name);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.rotation = Quaternion.Euler(euler);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        // Front of the doll is +Z: the camera sits in front, slightly to the side and above.
        private static void Frame(Camera camera, Bounds bounds)
        {
            var radius = bounds.extents.magnitude * Padding;
            var distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var direction = Quaternion.Euler(-Pitch, Yaw, 0f) * Vector3.forward;
            camera.transform.position = bounds.center + direction * distance;
            camera.transform.LookAt(bounds.center);
        }

        private static void Grab(Camera camera, RenderTexture rt, Color background, Texture2D into)
        {
            camera.backgroundColor = background;

            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (GraphicsSettings.currentRenderPipeline != null && RenderPipeline.SupportsRenderRequest(camera, request))
            {
                camera.targetTexture = null; // the request renders into rt
                RenderPipeline.SubmitRenderRequest(camera, request);
            }
            else
            {
                camera.targetTexture = rt;
                camera.Render();
            }

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            into.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            into.Apply();
            RenderTexture.active = previous;
        }

        // alpha = 1 - (white - black); colour = black / alpha. Then crops to the visible pixels.
        private static Texture2D Composite(Texture2D onBlack, Texture2D onWhite)
        {
            var b = onBlack.GetPixels();
            var w = onWhite.GetPixels();
            var result = new Color[b.Length];
            int minX = Size, minY = Size, maxX = -1, maxY = -1;

            for (var i = 0; i < b.Length; i++)
            {
                var diff = ((w[i].r - b[i].r) + (w[i].g - b[i].g) + (w[i].b - b[i].b)) / 3f;
                var a = Mathf.Clamp01(1f - diff);
                if (a < 0.02f)
                {
                    result[i] = Color.clear;
                    continue;
                }

                result[i] = new Color(Mathf.Clamp01(b[i].r / a), Mathf.Clamp01(b[i].g / a), Mathf.Clamp01(b[i].b / a), a);
                var x = i % Size;
                var y = i / Size;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            if (maxX < 0)
                return null;

            // Square crop around the part with a small margin, so icons line up on the gates.
            var side = Mathf.Max(maxX - minX, maxY - minY) + 1;
            side = Mathf.Min(Size, Mathf.CeilToInt(side * 1.08f));
            var cx = (minX + maxX) / 2;
            var cy = (minY + maxY) / 2;

            var icon = new Texture2D(side, side, TextureFormat.RGBA32, false);
            var pixels = new Color[side * side];
            for (var y = 0; y < side; y++)
            {
                var sy = cy - side / 2 + y;
                for (var x = 0; x < side; x++)
                {
                    var sx = cx - side / 2 + x;
                    pixels[y * side + x] = sx >= 0 && sy >= 0 && sx < Size && sy < Size ? result[sy * Size + sx] : Color.clear;
                }
            }

            icon.SetPixels(pixels);
            icon.Apply();
            return icon;
        }

        private static void AssignIcon(OutfitItem item, string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 256;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                return;

            var so = new SerializedObject(item);
            so.FindProperty("icon").objectReferenceValue = sprite;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Mirrors the item's config folder: _Data/_Items/_Hairs/X.asset -> _ItemIcons/_Hairs/X.png
        private static string IconPathFor(OutfitItem item)
        {
            var assetPath = AssetDatabase.GetAssetPath(item);
            var itemsRoot = EditorPaths.Data + "/_Items/";
            var relative = assetPath.StartsWith(itemsRoot)
                ? Path.ChangeExtension(assetPath.Substring(itemsRoot.Length), ".png")
                : item.name + ".png";
            return $"{IconRoot}/{relative}".Replace('\\', '/');
        }

        private static Dictionary<string, Transform> BoneMap(Transform doll)
        {
            var map = new Dictionary<string, Transform>();
            foreach (var t in doll.GetComponentsInChildren<Transform>(true))
                map.TryAdd(t.name, t);
            return map;
        }

        private static GameObject FindModel()
        {
            foreach (var guid in AssetDatabase.FindAssets($"\"{DollModelName}\" t:Model"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == DollModelName)
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            Debug.LogWarning($"'{DollModelName}' not found; skinned parts will be drawn in their rest pose.");
            return null;
        }
    }
}
#endif
