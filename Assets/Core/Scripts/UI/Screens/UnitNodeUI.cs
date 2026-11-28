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
        private const float LockedPictureTile = 156f;

        private Image _plate;

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

            Sprite picture = entry.Unit != null ? (locked ? entry.Unit.LockedPicture : entry.Unit.Picture) : null;
            if (picture == null && locked && entry.Unit != null) picture = entry.Unit.Picture;
            if (picture != null) BindPicture(theme, picture, locked, active);
            else BindIcon(entry, theme, locked, active);

            FitCaptionPlate(theme);
            if (glow != null) glow.gameObject.SetActive(false); // no glow in the pixel design
            if (startPill != null) startPill.SetActive(!locked && !entry.IsCheckpoint);
            if (startLabel != null) startLabel.text = done ? UIStrings.PracticeAgain : UIStrings.Start;
            if (entranceLabel != null)
            {
                entranceLabel.gameObject.SetActive(entry.IsCheckpoint && !active);
                entranceLabel.text = UIStrings.Entrance;
            }
        }

        /// <summary>
        /// The unit's picture fills the tile inside a dark pixel frame (blue frame on the unit to do now). A locked
        /// unit shows a blurred, paler copy of the picture with a padlock in the middle; once it opens the picture
        /// shows normally.
        /// </summary>
        private void BindPicture(CevirTheme theme, Sprite picture, bool locked, bool active)
        {
            float size = locked ? LockedPictureTile : TileSize;
            if (edge != null)
            {
                edge.sprite = theme.panel;
                edge.color = UIPalette.Ink;
                edge.rectTransform.sizeDelta = new Vector2(size, size);
                edge.rectTransform.anchoredPosition = locked ? new Vector2(6f, -6f) : new Vector2(10f, -10f);
            }

            if (face != null)
            {
                face.sprite = active ? theme.tileActiveGradient : theme.panel;
                face.type = Image.Type.Simple;
                face.color = active ? Color.white : UIPalette.Ink;
                face.rectTransform.sizeDelta = new Vector2(size, size);
            }

            if (icon != null)
            {
                icon.sprite = picture;
                icon.color = Color.white;
                icon.preserveAspect = true;
                float inner = size - (active ? 20f : 12f);
                icon.rectTransform.sizeDelta = new Vector2(inner, inner);
            }

            if (lockBadge != null)
            {
                lockBadge.SetActive(locked);
                if (locked)
                {
                    var badge = (RectTransform)lockBadge.transform;
                    badge.anchoredPosition = Vector2.zero;
                    badge.sizeDelta = new Vector2(50f, 50f);
                    if (badge.childCount > 0) ((RectTransform)badge.GetChild(0)).sizeDelta = new Vector2(30f, 30f);
                }
            }
        }

        /// <summary>Older look for a unit without a picture: its white pixel icon on a tile.</summary>
        private void BindIcon(UnitPathEntry entry, CevirTheme theme, bool locked, bool active)
        {
            Sprite tileShape = entry.IsCheckpoint ? theme.circle : theme.panel;
            if (rim != null) rim.sprite = tileShape;
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
                    badge.sizeDelta = new Vector2(56f, 56f);
                    if (badge.childCount > 0) ((RectTransform)badge.GetChild(0)).sizeDelta = new Vector2(30f, 30f);
                }
            }
        }

        /// <summary>
        /// A light plate behind "UNIT n" and the title, so they stay readable over every sky (night, sunset, rain).
        /// Sized to the text, at most the caption's width.
        /// </summary>
        private void FitCaptionPlate(CevirTheme theme)
        {
            if (title == null) return;
            var caption = title.transform.parent as RectTransform;
            if (caption == null) return;

            if (_plate == null)
            {
                var go = new GameObject("Plate", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                go.GetComponent<LayoutElement>().ignoreLayout = true;
                go.transform.SetParent(caption, false);
                go.transform.SetAsFirstSibling();
                _plate = go.GetComponent<Image>();
                _plate.raycastTarget = false;
                _plate.type = Image.Type.Sliced;
                RectTransform rt = _plate.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
            }

            _plate.sprite = theme != null ? theme.panel : null;
            _plate.color = UIPalette.Surface;
            float maxWidth = caption.rect.width > 1f ? caption.rect.width : 500f;
            float width = title.GetPreferredValues(title.text).x;
            if (kicker != null) width = Mathf.Max(width, kicker.GetPreferredValues(kicker.text).x);
            width = Mathf.Min(maxWidth, width) + 44f;
            RectTransform plate = _plate.rectTransform;
            plate.sizeDelta = new Vector2(width, 24f);
            plate.anchoredPosition = Vector2.zero;
        }
    }
}
