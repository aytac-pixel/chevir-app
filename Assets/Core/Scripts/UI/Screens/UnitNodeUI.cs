using System;
using Cevir.Core.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// One unit on the home path: "UNIT n" + title above a rounded tile. Completed = blue tile with a book,
    /// Active = glowing tile with a hand and the START pill, Locked = grey tile with a padlock,
    /// checkpoint = round trophy tile with "ENTRANCE" under it.
    /// </summary>
    public sealed class UnitNodeUI : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI kicker;
        [SerializeField] private TextMeshProUGUI title;
        [SerializeField] private Image glow;
        [SerializeField] private Image rim;
        [SerializeField] private Image edge;
        [SerializeField] private Image face;
        [SerializeField] private Image icon;
        [SerializeField] private GameObject startPill;
        [SerializeField] private TextMeshProUGUI startLabel;
        [SerializeField] private TextMeshProUGUI entranceLabel;

        private Action<UnitPathEntry> _onTap;
        private UnitPathEntry _entry;

        public UnitPathEntry Entry => _entry;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => { if (_onTap != null && _entry != null) _onTap(_entry); });
        }

        public void Bind(UnitPathEntry entry, CevirTheme theme, Action<UnitPathEntry> onTap)
        {
            _entry = entry;
            _onTap = onTap;

            bool locked = entry.State == NodeState.Locked;
            bool active = entry.State == NodeState.Active;
            bool done = entry.State == NodeState.Completed;

            if (kicker != null)
            {
                kicker.text = string.Format(UIStrings.UnitKicker, entry.Number);
                kicker.color = active ? UIPalette.Primary : UIPalette.TextMuted;
            }

            if (title != null)
            {
                title.text = entry.Unit != null ? entry.Unit.Title : (entry.Level != null ? entry.Level.Title : string.Empty);
                title.color = active ? UIPalette.Primary : (locked ? UIPalette.TextSecondary : UIPalette.TextPrimary);
            }

            Sprite tileShape = entry.IsCheckpoint ? theme.circle : theme.panel;
            if (rim != null) rim.sprite = tileShape;
            if (edge != null)
            {
                edge.sprite = tileShape;
                edge.color = locked ? UIPalette.LockedEdge : UIPalette.PrimaryDark;
            }

            if (face != null)
            {
                if (locked)
                {
                    face.sprite = tileShape;
                    face.color = UIPalette.LockedFill;
                }
                else
                {
                    face.sprite = entry.IsCheckpoint ? theme.circle : (active ? theme.tileActiveGradient : theme.tileGradient);
                    face.color = entry.IsCheckpoint ? UIPalette.Primary : Color.white;
                }
            }

            if (icon != null)
            {
                if (entry.IsCheckpoint) icon.sprite = theme.iconTrophy;
                else if (locked) icon.sprite = theme.iconLock;
                else if (active) icon.sprite = theme.iconHand;
                else icon.sprite = entry.Unit != null && entry.Unit.Icon != null ? entry.Unit.Icon : theme.iconBook;
                icon.color = locked ? UIPalette.LockedIcon : Color.white;
            }

            if (glow != null) glow.gameObject.SetActive(active);
            if (startPill != null) startPill.SetActive(active);
            if (startLabel != null) startLabel.text = done ? UIStrings.PracticeAgain : UIStrings.Start;
            if (entranceLabel != null)
            {
                entranceLabel.gameObject.SetActive(entry.IsCheckpoint && !active);
                entranceLabel.text = UIStrings.Entrance;
            }
        }
    }
}
