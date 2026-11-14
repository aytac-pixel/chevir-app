using System.Collections.Generic;
using System.Text;
using Cevir.Core.LanguagePacks;
using Cevir.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Picture-in-picture of the NPC's signs: plays each sign's reference clip in turn with its gloss
    /// ("SALAM · AD · SOYAD?"). Without clips yet it still shows the glosses.
    /// </summary>
    public sealed class SignClipPanelUI : MonoBehaviour
    {
        [SerializeField] private VideoPlayer player;
        [SerializeField] private RawImage videoImage;
        [SerializeField] private GameObject videoFrame;
        [SerializeField] private TextMeshProUGUI glossLabel;

        private readonly List<SignDefinition> _queue = new List<SignDefinition>();
        private int _index;
        private RenderTexture _texture;

        private void Awake()
        {
            if (player != null) player.loopPointReached += OnClipEnded;
        }

        private void OnDestroy()
        {
            if (player != null) player.loopPointReached -= OnClipEnded;
            if (_texture != null)
            {
                _texture.Release();
                Destroy(_texture);
            }
        }

        public void Play(IReadOnlyList<SignDefinition> signs)
        {
            _queue.Clear();
            var glosses = new StringBuilder();
            if (signs != null)
            {
                foreach (SignDefinition sign in signs)
                {
                    if (sign == null) continue;
                    if (glosses.Length > 0) glosses.Append(" · ");
                    glosses.Append(Loc.ToUpperAz(sign.Gloss));
                    if (sign.ReferenceClip != null) _queue.Add(sign);
                }
            }

            gameObject.SetActive(glosses.Length > 0);
            if (glossLabel != null) glossLabel.text = glosses.ToString();
            if (videoFrame != null) videoFrame.SetActive(_queue.Count > 0 && player != null);
            _index = 0;
            if (_queue.Count > 0) PlayCurrent();
        }

        public void Stop()
        {
            if (player != null && player.isPlaying) player.Stop();
            _queue.Clear();
        }

        private void PlayCurrent()
        {
            if (player == null || videoImage == null) return;
            VideoClip clip = _queue[_index].ReferenceClip;
            int w = Mathf.Max(16, (int)clip.width), h = Mathf.Max(16, (int)clip.height);
            if (_texture == null || _texture.width != w || _texture.height != h)
            {
                if (_texture != null)
                {
                    _texture.Release();
                    Destroy(_texture);
                }

                _texture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
                _texture.Create();
            }

            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = _texture;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.isLooping = false;
            player.clip = clip;
            videoImage.texture = _texture;
            player.Play();
        }

        private void OnClipEnded(VideoPlayer source)
        {
            if (_queue.Count == 0) return;
            _index = (_index + 1) % _queue.Count; // loop the phrase while the learner reads it
            PlayCurrent();
        }
    }
}
