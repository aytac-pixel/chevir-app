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

            if (background != null) background.color = Color.white;
            if (numberBox != null)
            {
                numberBox.sprite = locked ? theme.panel : theme.tileGradient;
                numberBox.color = locked ? UIPalette.Background : Color.white;
            }

            if (numberLabel != null)
            {
                numberLabel.text = entry.Story.Number.ToString();
                numberLabel.color = locked ? UIPalette.TextSecondary : Color.white;
            }

            if (titleLabel != null)
            {
                titleLabel.text = entry.Story.Title;
                titleLabel.color = UIPalette.TextPrimary;
            }

            if (statusLabel != null)
            {
                statusLabel.text = locked ? UIStrings.StoryLocked : done ? UIStrings.StoryCompleted : playable ? UIStrings.StoryNew : UIStrings.StorySoon;
                statusLabel.color = locked ? UIPalette.TextMuted : done ? UIPalette.Green : UIPalette.Cyan;
            }

            // Locked: a bare padlock; open stories get a bordered square with play / check.
            if (badge != null) badge.color = locked ? new Color(1f, 1f, 1f, 0f) : done ? UIPalette.GreenLight : UIPalette.CyanLight;
            if (badgeIcon != null)
            {
                badgeIcon.sprite = locked ? theme.iconLock : done ? theme.iconCheck : theme.iconPlay;
                badgeIcon.color = locked ? UIPalette.LockedIcon : done ? UIPalette.Green : UIPalette.Primary;
            }
        }
    }
}
