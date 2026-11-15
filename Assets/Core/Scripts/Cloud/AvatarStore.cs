using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace Cevir.Core.Cloud
{
    /// <summary>
    /// The profile photo on this phone: a square 512×512 JPEG in the app's private folder, plus the server address it
    /// came from, so the photo shows offline and is downloaded again only when it changed (new phone, new photo).
    /// </summary>
    public static class AvatarStore
    {
        public const int Size = 512;
        private const string UrlKey = "cevir.avatar.url";

        private static string FilePath => Path.Combine(Application.persistentDataPath, "avatar.jpg");

        public static event Action Changed;

        public static bool HasLocal => File.Exists(FilePath);

        /// <summary>Centre-crops to a square, scales to 512×512 and encodes as JPEG.</summary>
        public static byte[] PrepareJpeg(Texture2D source)
        {
            if (source == null) return null;
            int side = Mathf.Min(source.width, source.height);
            var scale = new Vector2(side / (float)source.width, side / (float)source.height);
            var offset = new Vector2((1f - scale.x) * 0.5f, (1f - scale.y) * 0.5f);

            RenderTexture target = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(source, target, scale, offset);
            RenderTexture.active = target;
            var square = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            square.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            square.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);

            byte[] jpeg = square.EncodeToJPG(85);
            UnityEngine.Object.Destroy(square);
            return jpeg;
        }

        public static void Save(byte[] jpeg, string url)
        {
            if (jpeg == null) return;
            try
            {
                File.WriteAllBytes(FilePath, jpeg);
                PlayerPrefs.SetString(UrlKey, url ?? string.Empty);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Chevir] Could not save the profile photo: " + e.Message);
            }

            if (Changed != null) Changed();
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Chevir] Could not delete the profile photo: " + e.Message);
            }

            PlayerPrefs.DeleteKey(UrlKey);
            PlayerPrefs.Save();
            if (Changed != null) Changed();
        }

        public static Texture2D LoadLocal()
        {
            if (!HasLocal) return null;
            try
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (texture.LoadImage(File.ReadAllBytes(FilePath))) return texture;
                UnityEngine.Object.Destroy(texture);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Chevir] Could not read the profile photo: " + e.Message);
            }

            return null;
        }

        /// <summary>
        /// Gives the photo for <paramref name="url"/>: the local copy when it is the same photo, otherwise downloads it.
        /// An empty url means no photo on the server; a photo kept only on this phone is still shown.
        /// </summary>
        public static IEnumerator Fetch(string url, Action<Texture2D> done)
        {
            string cached = PlayerPrefs.GetString(UrlKey, string.Empty);
            if (string.IsNullOrEmpty(url) || (url == cached && HasLocal))
            {
                done(LoadLocal());
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = 20;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    done(LoadLocal());
                    yield break;
                }

                byte[] bytes = request.downloadHandler.data;
                var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (bytes == null || !texture.LoadImage(bytes))
                {
                    UnityEngine.Object.Destroy(texture);
                    done(LoadLocal());
                    yield break;
                }

                try
                {
                    File.WriteAllBytes(FilePath, bytes);
                    PlayerPrefs.SetString(UrlKey, url);
                    PlayerPrefs.Save();
                }
                catch (Exception)
                {
                    // Showing the photo matters more than caching it.
                }

                done(texture);
            }
        }
    }
}
