using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Profile photos on the league table: downloaded once per address and kept in memory while the app runs.
    /// The learner's own photo comes from the copy on the phone, so it shows offline too.
    /// </summary>
    public sealed class LeagueAvatars : MonoBehaviour
    {
        private const int MaxCached = 120;

        private static LeagueAvatars _instance;
        private readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
        private readonly Dictionary<string, List<Action<Texture2D>>> _waiting = new Dictionary<string, List<Action<Texture2D>>>();
        private readonly HashSet<string> _failed = new HashSet<string>();
        private Texture2D _own;
        private bool _ownLoaded;

        private static LeagueAvatars Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("LeagueAvatars");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<LeagueAvatars>();
                Cloud.AvatarStore.Changed += () => { if (_instance != null) _instance._ownLoaded = false; };
                return _instance;
            }
        }

        /// <summary>The learner's own photo saved on this phone, or null.</summary>
        public static Texture2D Own()
        {
            LeagueAvatars self = Instance;
            if (!self._ownLoaded)
            {
                self._ownLoaded = true;
                self._own = Cloud.AvatarStore.LoadLocal();
            }

            return self._own;
        }

        /// <summary>Calls <paramref name="done"/> with the photo at <paramref name="url"/> (at once when cached), or null.</summary>
        public static void Get(string url, Action<Texture2D> done)
        {
            if (string.IsNullOrEmpty(url))
            {
                done(null);
                return;
            }

            LeagueAvatars self = Instance;
            Texture2D texture;
            if (self._cache.TryGetValue(url, out texture))
            {
                done(texture);
                return;
            }

            if (self._failed.Contains(url))
            {
                done(null);
                return;
            }

            List<Action<Texture2D>> list;
            if (self._waiting.TryGetValue(url, out list))
            {
                list.Add(done);
                return;
            }

            self._waiting[url] = new List<Action<Texture2D>> { done };
            self.StartCoroutine(self.Download(url));
        }

        private IEnumerator Download(string url)
        {
            Texture2D texture = null;
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
            {
                request.timeout = 15;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success) texture = DownloadHandlerTexture.GetContent(request);
            }

            if (texture != null)
            {
                if (_cache.Count >= MaxCached) _cache.Clear();
                _cache[url] = texture;
            }
            else
            {
                _failed.Add(url);
            }

            List<Action<Texture2D>> list;
            if (_waiting.TryGetValue(url, out list))
            {
                _waiting.Remove(url);
                foreach (Action<Texture2D> callback in list) callback(texture);
            }
        }
    }
}
