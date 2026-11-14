using Cevir.Core.Stories;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>One letter of the learner's name in the fingerspelling HUD.</summary>
    public sealed class LetterTileUI : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image outline;
        [SerializeField] private TextMeshProUGUI label;

        private bool _current;
        private float _pop;

        public void Bind(SpelledLetter letter, bool current)
        {
            if (label != null) label.text = letter.Letter;
            bool popped = letter.IsDone && _current;
            _current = current;
            if (popped) _pop = 1f;

            Color fill, line, text;
            switch (letter.Status)
            {
                case LetterStatus.Correct:
                    fill = UIPalette.Green; line = UIPalette.Green; text = Color.white;
                    break;
                case LetterStatus.SelfChecked:
                    fill = UIPalette.GreenLight; line = UIPalette.Green; text = UIPalette.GreenDark;
                    break;
                case LetterStatus.Skipped:
                    fill = UIPalette.LockedFill; line = UIPalette.LockedFill; text = UIPalette.TextMuted;
                    break;
                case LetterStatus.Unsupported:
                    fill = new Color(1f, 1f, 1f, 0.35f); line = new Color(1f, 1f, 1f, 0f); text = UIPalette.TextMuted;
                    break;
                default:
                    fill = Color.white; line = current ? UIPalette.Primary : UIPalette.GlassBorder; text = current ? UIPalette.Primary : UIPalette.TextPrimary;
                    break;
            }

            if (background != null) background.color = fill;
            if (outline != null) outline.color = line;
            if (label != null) label.color = text;
        }

        private void Update()
        {
            float target = _current ? 1.12f + Mathf.Sin(Time.unscaledTime * 6f) * 0.03f : 1f;
            if (_pop > 0f)
            {
                _pop = Mathf.Max(0f, _pop - Time.unscaledDeltaTime * 3f);
                target += Mathf.Sin(_pop * Mathf.PI) * 0.35f;
            }

            transform.localScale = Vector3.one * Mathf.Lerp(transform.localScale.x, target, 0.35f);
        }
    }
}
