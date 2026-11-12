using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Folders for the setup tools, worked out from where this script lives, so moving or
    /// renaming folders doesn't break them. Root is the folder that holds the _Editor folder
    /// (currently Assets/_Core).
    /// </summary>
    internal static class EditorPaths
    {
        private static string _root;

        public static string Root => _root ??= FindRoot();
        public static string Data => Root + "/_Data";
        public static string Levels => Data + "/_Levels";
        public static string TargetLooks => Data + "/_TargetLooks";
        public static string Samples => Root + "/_Samples";
        public static string Prefabs => Root + "/_Prefabs";
        public static string HurdlePrefabs => Prefabs + "/_Hurdles";

        /// <summary>First asset of this type anywhere in the project, or null.</summary>
        public static T FindAsset<T>() where T : Object
        {
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                    return asset;
            }

            return null;
        }

        /// <summary>Creates the folder and any missing parents.</summary>
        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string FindRoot()
        {
            foreach (var guid in AssetDatabase.FindAssets("EditorPaths t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("/EditorPaths.cs"))
                    continue;

                var editorFolder = Path.GetDirectoryName(path);
                var root = Path.GetDirectoryName(editorFolder)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(root))
                    return root;
            }

            Debug.LogWarning("Couldn't find EditorPaths.cs; using Assets as the root for setup tools.");
            return "Assets";
        }
    }
}
