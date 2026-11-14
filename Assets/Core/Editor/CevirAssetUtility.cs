using System;
using UnityEditor;
using UnityEngine;

namespace Cevir.Core.Editor
{
    public static class CevirAssetUtility
    {
        /// <summary>Creates every missing folder in an "Assets/..." path.</summary>
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        /// <summary>Loads the asset at <paramref name="path"/> or creates it. <paramref name="init"/> runs only on creation.</summary>
        public static T LoadOrCreate<T>(string path, Action<T> init = null) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            var asset = ScriptableObject.CreateInstance<T>();
            if (init != null) init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static string SafeFileName(string value)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value.Replace('.', '_');
        }
    }
}
