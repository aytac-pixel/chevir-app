using Cevir.Core.Game;
using Cevir.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Profile > Friends: "Invite friends" shares the download link of the newest Chevir APK; below it Chevir's
    /// Instagram page and website.
    /// </summary>
    public sealed class FriendsUI : ProfilePanelBase
    {
        /// <summary>Always the newest APK on the project's release page.</summary>
        public static readonly string ApkLink = new Social.InviteOnlySocialService().InviteLink;
        public const string InstagramLink = "https://www.instagram.com/chevir_app/";
        public const string WebsiteLink = "https://chevirapp.com";

        [SerializeField] private Sprite iconUsers;
        [SerializeField] private Sprite iconInstagram;
        [SerializeField] private Sprite iconGlobe;
        [SerializeField] private Sprite logo;

        public static FriendsUI Instance { get; private set; }

        protected override string Title => Loc.Pick("Friends", "Dostlar", "Друзья");

        protected override void Awake()
        {
            Instance = this;
            base.Awake();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this) Instance = null;
        }

        protected override void BuildContent(UIKit kit, RectTransform content)
        {
            RectTransform invite = kit.Card(content, "Invite", 44, 20f);
            VerticalLayoutGroup column = invite.GetComponent<VerticalLayoutGroup>();
            column.childAlignment = TextAnchor.UpperCenter;
            IconTileCentered(kit, invite, iconUsers, UIPalette.Primary, UIPalette.CyanLight, 150f);
            kit.Text(invite, "Title", Loc.Pick("Learning together is more fun!", "Birlikdə öyrənmək daha maraqlıdır!", "Учиться вместе веселее!"),
                40f, UIPalette.TextPrimary, TextAlignmentOptions.Center);
            kit.Text(invite, "Body", Loc.Pick("Send your friends the link to the newest Chevir app.",
                    "Dostlarına Chevir tətbiqinin ən yeni versiyasının linkini göndər.",
                    "Отправь друзьям ссылку на новую версию приложения Chevir."),
                29f, UIPalette.TextSecondary, TextAlignmentOptions.Center, false);
            kit.Button(invite, "InviteButton", Loc.Pick("INVITE FRIENDS", "DOSTLARI DƏVƏT ET", "ПРИГЛАСИТЬ ДРУЗЕЙ"), Color.white, Color.white, 124f, true, Invite);

            RectTransform follow = kit.Card(content, "Follow", 40, 18f);
            kit.Text(follow, "Title", Loc.Pick("Follow Chevir", "Chevir-i izlə", "Следи за Chevir"), 36f, UIPalette.TextPrimary);
            LinkRow(kit, follow, "Instagram", iconInstagram, new Color32(0xC1, 0x35, 0x84, 0xFF), "Instagram", "@chevir_app",
                () => Application.OpenURL(InstagramLink));
            LinkRow(kit, follow, "Website", logo != null ? logo : iconGlobe, logo != null ? Color.white : UIPalette.Primary,
                Loc.Pick("Website", "Sayt", "Сайт"), "chevirapp.com", () => Application.OpenURL(WebsiteLink), logo != null);
        }

        private void IconTileCentered(UIKit kit, Transform parent, Sprite icon, Color tint, Color fill, float size)
        {
            RectTransform holder = UIKit.Rect("IconHolder", parent);
            UIKit.Row(holder.gameObject, 0f, TextAnchor.MiddleCenter);
            UIKit.Layout(holder.gameObject, preferredHeight: size);
            IconTile(kit, holder, icon, tint, fill, size);
        }

        /// <summary>A whole-width button: coloured icon tile, name and handle, arrow.</summary>
        private void LinkRow(UIKit kit, Transform parent, string name, Sprite icon, Color tileColor, string title, string handle,
            System.Action onClick, bool fullPicture = false)
        {
            RectTransform row = UIKit.Rect(name, parent);
            Image bg = row.gameObject.AddComponent<Image>();
            bg.sprite = theme != null ? theme.panel : null;
            bg.type = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 1.4f;
            bg.color = UIPalette.Surface;
            Button button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(() => onClick());
            HorizontalLayoutGroup layout = UIKit.Row(row.gameObject, 24f);
            layout.padding = new RectOffset(24, 30, 18, 18);
            UIKit.Layout(row.gameObject, preferredHeight: 136f, minHeight: 136f);

            if (fullPicture)
            {
                // The Chevir logo is a full-colour picture: show it as it is, on white.
                Image tile = kit.Image(row, "IconTile", theme != null ? theme.panel : null, Color.white, true);
                UIKit.Layout(tile.gameObject, preferredWidth: 100f, preferredHeight: 100f, minWidth: 100f);
                Image picture = kit.Image(tile.transform, "Logo", icon, Color.white, false);
                picture.rectTransform.anchorMin = picture.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                picture.rectTransform.sizeDelta = new Vector2(82f, 82f);
            }
            else
            {
                IconTile(kit, row, icon, Color.white, tileColor, 100f);
            }

            RectTransform texts = UIKit.Rect("Texts", row);
            UIKit.Column(texts.gameObject, 0, 2f, TextAnchor.MiddleLeft);
            UIKit.Layout(texts.gameObject, flexibleWidth: 1f);
            kit.Text(texts, "Title", title, 34f, UIPalette.TextPrimary);
            kit.Text(texts, "Handle", handle, 28f, UIPalette.Primary, TextAlignmentOptions.MidlineLeft, false);

            TextMeshProUGUI arrow = kit.Text(row, "Arrow", "›", 60f, UIPalette.TextMuted, TextAlignmentOptions.Center);
            UIKit.Layout(arrow.gameObject, preferredWidth: 40f, minWidth: 40f);
        }

        private void Invite()
        {
            GameManager game = GameManager.Instance;
            string text = string.Format(UIStrings.InviteMessage, game != null ? game.Streak.State.current : 0, ApkLink);
            if (!ShareUtility.ShareText(text, UIStrings.InviteFriends) && CevirUIRoot.Instance != null)
            {
                CevirUIRoot.Instance.ShowToast(UIStrings.LinkCopied);
            }
        }

        protected override void Render()
        {
        }
    }
}
