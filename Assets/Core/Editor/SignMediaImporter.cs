using System;
using System.Collections.Generic;
using System.IO;
using Cevir.Core.LanguagePacks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace Cevir.Core.Editor
{
    /// <summary>
    /// Links media files dropped into a pack's Media folder to its signs by file name, so the content team only
    /// copies files in (same layout as their checklist, e.g. Media/A1/U01_Elifba/herf_a.png) and re-runs setup.
    /// Per sign: "&lt;slug&gt;.mp4/.webm" = video, "&lt;slug&gt;_yavas.mp4/.webm" = slow video, "&lt;slug&gt;.png/.jpg" = picture.
    /// Sub-folders do not matter; only the file name is matched. Files that match no sign are listed in the log.
    /// </summary>
    public sealed class SignMediaImporter : AssetPostprocessor
    {
        public const string MediaFolderName = "/Media/";
        private const string SlowSuffix = "_yavas";

        // Pictures in any language pack's Media folder are imported as UI sprites (no mipmaps, max 1024 px).
        private void OnPreprocessTexture()
        {
            if (!IsPackMedia(assetPath)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        // Videos are silent by spec; transcode so every Android device can play them.
        // WebM (VP8) needs no transcoding; the Linux editor used by CI can neither read MP4 nor transcode,
        // so CI converts the clips to WebM before Unity starts (Tools/setup/convert_videos_webm.sh).
        private void OnPreprocessAsset()
        {
            if (!IsPackMedia(assetPath)) return;
            var video = assetImporter as VideoClipImporter;
            if (video == null) return;
            video.importAudio = false;
            VideoImporterTargetSettings settings = video.defaultTargetSettings;
            settings.enableTranscoding = !assetPath.EndsWith(".webm", System.StringComparison.OrdinalIgnoreCase);
            settings.codec = VideoCodec.H264;
            video.defaultTargetSettings = settings;
        }

        private static bool IsPackMedia(string path)
        {
            return path.StartsWith("Assets/LanguagePacks/") && path.Contains(MediaFolderName);
        }

        public sealed class Report
        {
            public int Pictures;
            public int Videos;
            public int SlowVideos;
            public readonly List<string> Unmatched = new List<string>();

            public override string ToString()
            {
                return Pictures + " pictures, " + Videos + " videos, " + SlowVideos + " slow videos linked" +
                       (Unmatched.Count > 0 ? "; files with no matching sign: " + string.Join(", ", Unmatched) : string.Empty);
            }
        }

        /// <summary>
        /// Links every media file under <paramref name="mediaRoot"/> to the dictionary sign whose slug matches its
        /// file name. <paramref name="slugOf"/> gives the pack's naming rule. Existing links are kept when no file is found.
        /// </summary>
        public static Report Import(SignDictionary dictionary, string mediaRoot, Func<SignDefinition, string> slugOf)
        {
            var report = new Report();
            if (dictionary == null || !AssetDatabase.IsValidFolder(mediaRoot)) return report;

            var bySlug = new Dictionary<string, SignDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (SignDefinition sign in dictionary.Signs)
            {
                string slug = sign != null ? slugOf(sign) : null;
                if (!string.IsNullOrEmpty(slug) && !bySlug.ContainsKey(slug)) bySlug[slug] = sign;
            }

            var clips = new Dictionary<SignDefinition, VideoClip>();
            var slow = new Dictionary<SignDefinition, VideoClip>();
            var pictures = new Dictionary<SignDefinition, Sprite>();

            var guids = new List<string>(AssetDatabase.FindAssets("t:VideoClip", new[] { mediaRoot }));
            guids.AddRange(AssetDatabase.FindAssets("t:Texture2D", new[] { mediaRoot }));
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                bool isSlow = name.EndsWith(SlowSuffix, StringComparison.OrdinalIgnoreCase);
                string slug = isSlow ? name.Substring(0, name.Length - SlowSuffix.Length) : name;

                SignDefinition sign;
                if (!bySlug.TryGetValue(slug, out sign))
                {
                    report.Unmatched.Add(path.Substring(mediaRoot.Length + 1));
                    continue;
                }

                var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(path);
                if (clip != null)
                {
                    (isSlow ? slow : clips)[sign] = clip;
                    continue;
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) pictures[sign] = sprite;
                else Debug.LogWarning("[Chevir] Picture is not imported as a Sprite (re-import it): " + path);
            }

            foreach (SignDefinition sign in dictionary.Signs)
            {
                if (sign == null) continue;
                VideoClip c, s;
                Sprite p;
                clips.TryGetValue(sign, out c);
                slow.TryGetValue(sign, out s);
                pictures.TryGetValue(sign, out p);
                if (c == null && s == null && p == null) continue;

                sign.EditorSetMedia(c, s, p);
                EditorUtility.SetDirty(sign);
                if (c != null) report.Videos++;
                if (s != null) report.SlowVideos++;
                if (p != null) report.Pictures++;
            }

            return report;
        }
    }
}
