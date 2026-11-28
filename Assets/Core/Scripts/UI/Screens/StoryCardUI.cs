using System;
using Cevir.Core.Stories;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>One card on the Stories screen: the story's picture with its number, title, status and a lock / play / check badge.</summary>
    public sealed class StoryCardUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [Tooltip("The story's wide picture filling the card.")]
        [SerializeField] private Image sceneArt;
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

            if (background != null) background.color = UIPalette.Ink;
            if (sceneArt != null)
            {
                // The story's wide card picture; blurred while the story is locked. Older themes fall back to the scene.
                int index = entry.Story.Number - 1;
                Sprite[] set = locked ? theme.storyCardsLocked : theme.storyCards;
                Sprite card = set != null && index >= 0 && index < set.Length ? set[index] : null;
                if (card == null) card = entry.Story.Background;
                sceneArt.sprite = card;
                sceneArt.enabled = card != null;
                sceneArt.color = Color.white;
            }

            // Number tile on the left; the only padlock is the badge on the right.
            if (numberBox != null)
            {
                numberBox.sprite = theme.panel;
                numberBox.color = done ? UIPalette.GreenLight : Color.white;
            }

            if (numberLabel != null)
            {
                numberLabel.text = entry.Story.Number.ToString();
                numberLabel.color = locked ? UIPalette.TextSecondary : done ? UIPalette.Green : UIPalette.Primary;
            }

            if (titleLabel != null)
            {
                titleLabel.text = entry.Story.Title;
                titleLabel.color = Color.white;
            }

            if (statusLabel != null)
            {
                statusLabel.text = locked ? UIStrings.StoryLocked : done ? UIStrings.StoryCompleted : playable ? UIStrings.StoryNew : UIStrings.StorySoon;
                statusLabel.color = locked ? new Color(0.84f, 0.88f, 0.95f, 1f) : done ? new Color(0.55f, 0.95f, 0.72f, 1f) : new Color(0.6f, 0.9f, 1f, 1f);
            }

            if (badge != null) badge.color = locked ? Color.white : done ? UIPalette.GreenLight : UIPalette.CyanLight;
            if (badgeIcon != null)
            {
                badgeIcon.sprite = locked ? theme.iconLock : done ? theme.iconCheck : theme.iconPlay;
                badgeIcon.color = locked ? UIPalette.LockedIcon : done ? UIPalette.Green : UIPalette.Primary;
            }
        }
    }
}
