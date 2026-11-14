using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>NPC speech bubble with the speaker's name, a typewriter reveal and a replay-voice button.</summary>
    public sealed class SpeechBubbleUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TextMeshProUGUI speakerLabel;
        [SerializeField] private TextMeshProUGUI textLabel;
        [SerializeField] private Button replayButton;
        [SerializeField] private float charactersPerSecond = 32f;

        private float _revealed;
        private int _total;

        public event Action ReplayRequested;

        public bool IsTyping => gameObject.activeSelf && _revealed < _total;

        private void Awake()
        {
            if (replayButton != null) replayButton.onClick.AddListener(() => { if (ReplayRequested != null) ReplayRequested(); });
        }

        public void Show(string speaker, string text, bool hasVoice)
        {
            gameObject.SetActive(true);
            if (group != null) group.alpha = 1f;
            if (speakerLabel != null) speakerLabel.text = speaker;
            if (replayButton != null) replayButton.gameObject.SetActive(hasVoice);
            if (textLabel == null) return;
            textLabel.text = text;
            textLabel.ForceMeshUpdate();
            _total = textLabel.textInfo.characterCount;
            _revealed = 0f;
            textLabel.maxVisibleCharacters = 0;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void Complete()
        {
            _revealed = _total;
            if (textLabel != null) textLabel.maxVisibleCharacters = _total;
        }

        private void Update()
        {
            if (textLabel == null || _revealed >= _total) return;
            _revealed = Mathf.Min(_total, _revealed + charactersPerSecond * Time.deltaTime);
            textLabel.maxVisibleCharacters = Mathf.FloorToInt(_revealed);
        }
    }
}
