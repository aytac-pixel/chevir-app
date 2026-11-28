using Cevir.Core.Social;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>One leaderboard row: rank (cup for the top 3), profile photo (or initials), name, zone arrow and XP.</summary>
    public sealed class LeagueRowUI : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image youGlow;
        [SerializeField] private TextMeshProUGUI rankLabel;
        [SerializeField] private Image rankCup;
        [SerializeField] private Image avatar;
        [SerializeField] private Image avatarRing;
        [SerializeField] private TextMeshProUGUI initialsLabel;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private Image zoneArrow;
        [SerializeField] private TextMeshProUGUI xpLabel;
        [SerializeField] private TextMeshProUGUI xpUnitLabel;

        private RawImage _photo;
        private string _photoKey;

        /// <summary>
        /// The learner's profile photo inside the circle; the coloured initials stay underneath and show when there
        /// is no photo (or until it has downloaded).
        /// </summary>
        private void ShowPhoto(LeagueEntry entry)
        {
            if (avatar == null) return;
            if (_photo == null)
            {
                if (avatar.GetComponent<Mask>() == null) avatar.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                var go = new GameObject("Photo", typeof(RectTransform), typeof(RawImage));
                go.transform.SetParent(avatar.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                _photo = go.GetComponent<RawImage>();
                _photo.raycastTarget = false;
            }

            string key = entry.IsYou ? "you:" + entry.AvatarUrl : entry.AvatarUrl;
            _photoKey = key;
            _photo.enabled = false;
            if (initialsLabel != null) initialsLabel.enabled = true;
            if (entry.IsYou)
            {
                Texture2D own = LeagueAvatars.Own();
                if (own != null)
                {
                    SetPhoto(own);
                    return;
                }
            }

            LeagueAvatars.Get(entry.AvatarUrl, texture =>
            {
                if (this == null || _photo == null || _photoKey != key) return; // the row was reused meanwhile
                SetPhoto(texture);
            });
        }

        private void SetPhoto(Texture2D texture)
        {
            _photo.texture = texture;
            _photo.enabled = texture != null;
            if (initialsLabel != null) initialsLabel.enabled = texture == null;
        }

        private static readonly Color[] CupColors = { UIPalette.Gold, UIPalette.Silver, UIPalette.Bronze };

        public void Bind(LeagueEntry entry, CevirTheme theme)
        {
            bool you = entry.IsYou;
            bool top = entry.Rank <= 3 && !you;

            if (background != null)
            {
                background.sprite = you ? theme.buttonGradient : theme.panel;
                background.color = you ? Color.white : entry.Zone == LeagueZone.Demote ? UIPalette.DemoteRow : Color.white;
            }

            if (youGlow != null) youGlow.gameObject.SetActive(you);

            if (rankCup != null)
            {
                rankCup.gameObject.SetActive(top);
                if (top) rankCup.color = CupColors[entry.Rank - 1];
            }

            if (rankLabel != null)
            {
                rankLabel.gameObject.SetActive(!top);
                rankLabel.text = entry.Rank.ToString();
                rankLabel.color = you ? Color.white : UIPalette.TextSecondary;
            }

            if (avatar != null) avatar.color = entry.AvatarColor;
            if (avatarRing != null) avatarRing.gameObject.SetActive(you);
            if (initialsLabel != null) initialsLabel.text = entry.Initials;
            ShowPhoto(entry);

            if (nameLabel != null)
            {
                nameLabel.text = entry.DisplayName;
                nameLabel.color = you ? Color.white : UIPalette.TextPrimary;
            }

            if (zoneArrow != null)
            {
                bool showArrow = !you && entry.Zone != LeagueZone.Stay;
                zoneArrow.gameObject.SetActive(showArrow);
                if (showArrow)
                {
                    zoneArrow.sprite = entry.Zone == LeagueZone.Promote ? theme.iconArrowUp : theme.iconArrowDown;
                    zoneArrow.color = entry.Zone == LeagueZone.Promote ? UIPalette.Primary : UIPalette.Red;
                }
            }

            if (xpLabel != null)
            {
                xpLabel.text = entry.Xp.ToString();
                xpLabel.color = you ? Color.white : UIPalette.TextPrimary;
            }

            if (xpUnitLabel != null) xpUnitLabel.color = you ? new Color(1f, 1f, 1f, 0.85f) : UIPalette.TextMuted;
        }
    }
}
