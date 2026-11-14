using System;
using Cevir.Core.Stories;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>One row on the Stories screen: number box, title, status and a lock / play / check badge.</summary>
    public sealed class StoryCardUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image numberBox;
        [SerializeField] private TextMeshProUGUI numberLabel;
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private TextMeshProUGUI statusLabel;
        [SerializeField] private Image badge;
        [SerializeField] private Image badgeIcon;

        private StoryEntry _entry;
        private Action<StoryEntry> _onTap;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => { if (_entry != null && _onTap != null) _onTap(_entry); });
        }

        public void Bind(StoryEntry entry, CevirTheme theme, Action<StoryEntry> onTap)
        {
            _entry = entry;
            _onTap = onTap;
            bool locked = entry.State == StoryState.Locked;
            bool done = entry.State == StoryState.Completed;
            bool playable = entry.Story.IsPlayable;

            if (background != null) background.color = locked ? (Color)new Color32(0xEE, 0xF3, 0xFA, 0xFF) : Color.white;
            if (numberBox != null)
            {
                numberBox.sprite = locked ? theme.panel : theme.tileGradient;
                numberBox.color = locked ? (Color)new Color32(0xE2, 0xE7, 0xEF, 0xFF) : Color.white;
            }

            if (numberLabel != null)
            {
                numberLabel.text = entry.Story.Number.ToString();
                numberLabel.color = locked ? UIPalette.TextMuted : Color.white;
            }

            if (titleLabel != null)
            {
                titleLabel.text = entry.Story.Title;
                titleLabel.color = locked ? (Color)new Color32(0x8A, 0x97, 0xAB, 0xFF) : UIPalette.TextPrimary;
            }

            if (statusLabel != null)
            {
                statusLabel.text = locked ? UIStrings.StoryLocked : done ? UIStrings.StoryCompleted : playable ? UIStrings.StoryNew : UIStrings.StorySoon;
                statusLabel.color = locked ? UIPalette.TextMuted : done ? UIPalette.Green : UIPalette.Cyan;
            }

            if (badge != null) badge.color = locked ? (Color)new Color32(0xE0, 0xE5, 0xEC, 0xFF) : done ? UIPalette.GreenLight : UIPalette.CyanLight;
            if (badgeIcon != null)
            {
                badgeIcon.sprite = locked ? theme.iconLock : done ? theme.iconCheck : theme.iconPlay;
                badgeIcon.color = locked ? UIPalette.TextMuted : done ? UIPalette.Green : UIPalette.Primary;
            }
        }
    }
}
