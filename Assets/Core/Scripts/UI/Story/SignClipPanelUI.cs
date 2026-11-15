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
    /// Picture-in-picture of the NPC's signs: plays each sign's reference clip in turn, with a subtitle under the
    /// video giving the meaning of the sign being shown (highlighted inside the whole phrase "SALAM · NECƏSƏN").
    /// Without clips yet it still shows the glosses.
    /// </summary>
    public sealed class SignClipPanelUI : MonoBehaviour
    {
        [SerializeField] private VideoPlayer player;
        [SerializeField] private RawImage videoImage;
        [SerializeField] private GameObject videoFrame;
        [SerializeField] private TextMeshProUGUI glossLabel;

        private readonly List<SignDefinition> _queue = new List<SignDefinition>();
        private readonly List<SignDefinition> _phrase = new List<SignDefinition>();
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
            _phrase.Clear();
            if (signs != null)
            {
                foreach (SignDefinition sign in signs)
                {
                    if (sign == null) continue;
                    _phrase.Add(sign);
                    if (sign.ReferenceClip != null) _queue.Add(sign);
                }
            }

            gameObject.SetActive(_phrase.Count > 0);
            ShowSubtitle(null);
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
            AspectRatioFitter fitter = videoImage.GetComponent<AspectRatioFitter>();
            if (fitter != null) fitter.aspectRatio = w / (float)h;
            player.Play();
            ShowSubtitle(_queue[_index]);
        }

        /// <summary>Subtitle: the meaning of the sign on screen, large, with the rest of the phrase dimmed.</summary>
        private void ShowSubtitle(SignDefinition current)
        {
            if (glossLabel == null) return;
            var text = new StringBuilder();
            foreach (SignDefinition sign in _phrase)
            {
                if (text.Length > 0) text.Append("<color=#9AA6B8> · </color>");
                string gloss = Loc.ToUpperAz(sign.Gloss);
                if (current == null || sign == current) text.Append("<b>").Append(gloss).Append("</b>");
                else text.Append("<color=#9AA6B8>").Append(gloss).Append("</color>");
            }

            glossLabel.richText = true;
            glossLabel.text = text.ToString();
        }

        private void OnClipEnded(VideoPlayer source)
        {
            if (_queue.Count == 0) return;
            _index = (_index + 1) % _queue.Count; // loop the phrase while the learner reads it
            PlayCurrent();
        }
    }
}
