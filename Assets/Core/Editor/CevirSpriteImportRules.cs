using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Cevir.Core.Editor
{
    /// <summary>
    /// Import settings for everything in Assets/Core/Sprites: UI sprites without mipmaps, and 9-slice borders read
    /// from the file name ("panel_r48__b48.png" gets a 48 px border on every side). Art can be replaced by dropping
    /// in a file with the same name.
    /// </summary>
    public sealed class CevirSpriteImportRules : AssetPostprocessor
    {
        public const string SpriteRoot = "Assets/Core/Sprites/";
        private static readonly Regex Border = new Regex(@"__b(\d+)", RegexOptions.Compiled);

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SpriteRoot)) return;
            Apply((TextureImporter)assetImporter, assetPath);
        }

        private static void Apply(TextureImporter importer, string path)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.spritePixelsPerUnit = 100f;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.filterMode = FilterMode.Bilinear;

            // Pixel art is stored at its real pixel size (a story scene is 216 x 384) and scaled up on screen:
            // point filtering keeps every pixel a sharp square, and the small files stay uncompressed so the
            // palette colours stay exact.
            if (IsPixelArt(path))
            {
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
            }

            Match match = Border.Match(path);
            float border = match.Success ? float.Parse(match.Groups[1].Value) : 0f;
            importer.spriteBorder = new Vector4(border, border, border, border);
        }

        private static bool IsPixelArt(string path)
        {
            return path.StartsWith(SpriteRoot + "Story/") || path.StartsWith(SpriteRoot + "Decor/") || path.StartsWith(SpriteRoot + "Units/") ||
                   path.StartsWith(SpriteRoot + "Home/");
        }

        /// <summary>Re-imports any sprite that was imported before these rules existed (first project open).</summary>
        public static void EnsureAll()
        {
            bool changed = false;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteRoot.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || importer.textureType == TextureImporterType.Sprite) continue;
                Apply(importer, path);
                importer.SaveAndReimport();
                changed = true;
            }

            if (changed) AssetDatabase.Refresh();
        }
    }
}
