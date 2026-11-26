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
        [Tooltip("Small padlock on the corner of a locked tile (the tile itself shows the unit's own icon, greyed).")]
        [SerializeField] private GameObject lockBadge;

        private const float TileSize = 164f;
        private const float LockedTileSize = 104f;

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
                kicker.color = UIPalette.Primary; // every unit name is blue (design)
            }

            if (title != null)
            {
                title.text = entry.Unit != null ? entry.Unit.Title : (entry.Level != null ? entry.Level.Title : string.Empty);
                title.color = UIPalette.Primary;
            }

            Sprite tileShape = entry.IsCheckpoint ? theme.circle : theme.panel;
            if (rim != null) rim.sprite = tileShape;
            // Pixel style: locked units are smaller tiles; the edge is the tile's hard dark shadow.
            float size = locked ? LockedTileSize : TileSize;
            if (edge != null)
            {
                edge.sprite = tileShape;
                edge.color = UIPalette.Ink;
                edge.rectTransform.sizeDelta = new Vector2(size, size);
                edge.rectTransform.anchoredPosition = locked ? new Vector2(6f, -6f) : new Vector2(10f, -10f);
            }

            if (face != null)
            {
                face.rectTransform.sizeDelta = new Vector2(size, size);
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
                // Every unit has its own pixel icon (letters, a waving hand, a clock...); locked units show it greyed
                // with a small padlock, so the path already tells what is coming.
                Sprite unitIcon = entry.Unit != null ? entry.Unit.Icon : null;
                if (unitIcon != null) icon.sprite = unitIcon;
                else if (entry.IsCheckpoint) icon.sprite = theme.iconTrophy;
                else if (locked) icon.sprite = theme.iconLock;
                else icon.sprite = theme.iconHand;
                icon.color = locked ? UIPalette.LockedIcon : Color.white;
                float iconSize = locked ? 64f : 96f;
                icon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
            }

            if (lockBadge != null)
            {
                bool showLock = locked && entry.Unit != null && entry.Unit.Icon != null;
                lockBadge.SetActive(showLock);
                if (showLock)
                {
                    var badge = (RectTransform)lockBadge.transform;
                    badge.anchoredPosition = new Vector2(size * 0.5f, size * 0.5f);
                }
            }

            if (glow != null) glow.gameObject.SetActive(false); // no glow in the pixel design
            if (startPill != null) startPill.SetActive(!locked && !entry.IsCheckpoint);
            if (startLabel != null) startLabel.text = done ? UIStrings.PracticeAgain : UIStrings.Start;
            if (entranceLabel != null)
            {
                entranceLabel.gameObject.SetActive(entry.IsCheckpoint && !active);
                entranceLabel.text = UIStrings.Entrance;
            }
        }
    }
}
