using System;
using System.Globalization;
using Cevir.Core.Cloud;
using Cevir.Core.Game;
using Cevir.Core.Gamification;
using Cevir.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Profile tab: the profile card (photo, name, join date, ləqəb, current flame, total XP) and four pages under it:
    /// Statistics, Notifications, Friends, Account and Languages. Built at runtime in the interface language.
    /// </summary>
    public sealed class ProfileScreenUI : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private CevirTheme theme;
        [SerializeField] private Sprite iconUser;
        [SerializeField] private Sprite iconPencil;
        [SerializeField] private Sprite iconFlame;
        [SerializeField] private Sprite iconZap;
        [SerializeField] private Sprite iconChart;
        [SerializeField] private Sprite iconBell;
        [SerializeField] private Sprite iconUsers;
        [SerializeField] private Sprite iconSettings;
        [SerializeField] private Sprite iconLanguages;

        private GameManager _game;
        private RectTransform _root;
        private RawImage _photo;
        private Image _photoPlaceholder;
        private TextMeshProUGUI _name;
        private TextMeshProUGUI _joined;
        private TextMeshProUGUI _nameSign;
        private TextMeshProUGUI _streak;
        private TextMeshProUGUI _xp;
        private TextMeshProUGUI _notificationsNote;
        private TextMeshProUGUI _accountNote;
        private TextMeshProUGUI _languageNote;
        private string _avatarUrlShown;
        private Texture2D _avatarTexture;
        private float _nextRefresh;

        private void Awake()
        {
            Build();
            HomeSkyUI.Create((RectTransform)transform, theme); // the same sky as the Learn page, behind everything
        }

        private void OnEnable()
        {
            _game = GameManager.Instance;
            Loc.LanguageChanged += OnLanguageChanged;
            AvatarStore.Changed += OnAvatarChanged;
            if (_game != null)
            {
                _game.ProfileChanged += Refresh;
                _game.Xp.XpGained += OnXpGained;
                _game.Streak.StreakChanged += OnStreakChanged;
            }

            _avatarUrlShown = null;
            Refresh();
        }

        private void OnDisable()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
            AvatarStore.Changed -= OnAvatarChanged;
            if (_game == null) return;
            _game.ProfileChanged -= Refresh;
            _game.Xp.XpGained -= OnXpGained;
            _game.Streak.StreakChanged -= OnStreakChanged;
        }

        private void Update()
        {
            // Notification switches change on their own page; keep the lines here current.
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 1f;
            RefreshNotes();
        }

        private void OnLanguageChanged(string language)
        {
            if (_root != null) Destroy(_root.gameObject);
            Build();
            _avatarUrlShown = null;
            Refresh();
        }

        private void OnAvatarChanged()
        {
            _avatarUrlShown = null;
            Refresh();
        }

        private void OnXpGained(XpState state, int amount) => Refresh();
        private void OnStreakChanged(StreakState state) => Refresh();

        // ------------------------------------------------------------------ values

        private void Refresh()
        {
            if (_game == null || _name == null) return;
            PlayerProgress progress = _game.Progress;

            _name.text = _game.LearnerName;
            _joined.text = string.Format(UIStrings.Joined, JoinedText(progress.joinedUtcTicks));
            string sign = progress.nameSign ?? string.Empty;
            _nameSign.text = sign.Length > 0
                ? Loc.Pick("Name sign: ", "Ləqəb: ", "Жестовое имя: ") + "<b>" + Stories.NameSignOptions.Display(sign) + "</b>"
                : Loc.Pick("No name sign yet", "Ləqəb seçilməyib", "Жестовое имя не выбрано");
            _streak.text = _game.Streak.State.current.ToString();
            _xp.text = _game.Xp.Total.ToString();
            if (isActiveAndEnabled) RefreshAvatar();
            RefreshNotes();
        }

        private void RefreshNotes()
        {
            if (_game == null || _notificationsNote == null) return;
            ReminderSettings r = _game.Progress.reminders ?? new ReminderSettings();
            int on = (r.greetings ? 1 : 0) + (r.configured && r.enabled ? 1 : 0) + (r.leagueAlerts ? 1 : 0);
            _notificationsNote.text = on == 0
                ? Loc.Pick("All off", "Hamısı bağlıdır", "Все выключены")
                : r.greetings
                    ? Loc.Pick("Daily greetings on", "Gündəlik salamlar açıqdır", "Приветствия дня включены")
                    : Loc.Pick("Greetings off", "Salamlar bağlıdır", "Приветствия выключены");

            _languageNote.text = Loc.NativeName(Loc.Language);

            CloudService cloud = CloudService.Instance;
            bool signedIn = cloud != null && cloud.State == CloudState.SignedIn;
            _accountNote.text = signedIn && !string.IsNullOrEmpty(cloud.Email)
                ? cloud.Email
                : Loc.Pick("Name, ləqəb, password, e-mail", "Ad, ləqəb, şifrə, e-poçt", "Имя, жестовое имя, пароль, почта");
        }

        private void RefreshAvatar()
        {
            if (_photo == null || _game == null) return;
            string url = _game.Progress.avatarUrl ?? string.Empty;
            if (_avatarUrlShown == url) return;
            _avatarUrlShown = url;
            StartCoroutine(AvatarStore.Fetch(url, texture =>
            {
                if (_photo == null) return;
                if (_avatarTexture != null && _avatarTexture != texture) Destroy(_avatarTexture);
                _avatarTexture = texture;
                _photo.texture = texture;
                _photo.gameObject.SetActive(texture != null);
                _photoPlaceholder.gameObject.SetActive(texture == null);
            }));
        }

        private static string JoinedText(long ticks)
        {
            var joined = new DateTime(ticks, DateTimeKind.Utc).ToLocalTime();
            CultureInfo culture = Loc.IsAz ? AzerbaijaniCulture() : Loc.IsRu ? Loc.Culture : CultureInfo.InvariantCulture;
            string month = culture.DateTimeFormat.GetMonthName(joined.Month);
            if (!Loc.IsEn && month.Length > 0) month = char.ToUpper(month[0]) + month.Substring(1);
            return joined.Day + " " + month + " " + joined.Year;
        }

        private static CultureInfo AzerbaijaniCulture()
        {
            try
            {
                return new CultureInfo("az-Latn-AZ");
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture;
            }
        }

        // ------------------------------------------------------------------ pages

        private static void OpenAccount()
        {
            if (AccountSettingsUI.Instance != null) AccountSettingsUI.Instance.Open();
        }

        // ------------------------------------------------------------------ layout

        private void Build()
        {
            var kit = new UIKit(font, theme);
            _root = UIKit.Rect("Content", transform);
            UIKit.Stretch(_root);
            RectTransform content = kit.Scroll(_root, new RectOffset(60, 60, 60, 80), 30f);

            BuildCard(kit, content);

            Entry(kit, content, "Statistics", iconChart, UIPalette.Primary, UIPalette.CyanLight,
                Loc.Pick("Statistics", "Statistika", "Статистика"),
                Loc.Pick("Minutes and XP per day, flames, level", "Hər gün dəqiqə və XP, alov, səviyyə", "Минуты и XP по дням, огонёк, уровень"),
                () => { if (StatisticsUI.Instance != null) StatisticsUI.Instance.Open(); });
            _notificationsNote = Entry(kit, content, "Notifications", iconBell, UIPalette.AmberDark, UIPalette.AmberLight,
                Loc.Pick("Notifications", "Bildirişlər", "Уведомления"), "",
                () => { if (NotificationsUI.Instance != null) NotificationsUI.Instance.Open(); });
            Entry(kit, content, "Friends", iconUsers, UIPalette.GreenDark, UIPalette.GreenLight,
                Loc.Pick("Friends", "Dostlar", "Друзья"),
                Loc.Pick("Invite friends, Instagram, website", "Dostları dəvət et, Instagram, sayt", "Пригласить друзей, Instagram, сайт"),
                () => { if (FriendsUI.Instance != null) FriendsUI.Instance.Open(); });
            _accountNote = Entry(kit, content, "Account", iconSettings != null ? iconSettings : iconUser, UIPalette.TextSecondary, UIPalette.Surface,
                Loc.Pick("Account", "Hesab", "Аккаунт"), "", OpenAccount);
            _languageNote = Entry(kit, content, "Languages", iconLanguages, UIPalette.Primary, UIPalette.CyanLight,
                Loc.Pick("Languages", "Dillər", "Языки"), "",
                () => { if (LanguagesUI.Instance != null) LanguagesUI.Instance.Open(); });
        }

        /// <summary>The profile card: photo and name with join date and ləqəb, then the flame and XP.</summary>
        private void BuildCard(UIKit kit, Transform parent)
        {
            RectTransform card = kit.Card(parent, "ProfileCard", 40, 28f);

            RectTransform top = UIKit.Rect("Top", card);
            HorizontalLayoutGroup topRow = UIKit.Row(top.gameObject, 32f);
            topRow.childForceExpandHeight = false;
            UIKit.Layout(top.gameObject, preferredHeight: 220f);

            // Square photo: blue tile with the person icon until a photo is set.
            Image avatar = kit.Image(top, "Avatar", theme != null ? theme.panel : null, UIPalette.Primary, true);
            UIKit.Layout(avatar.gameObject, preferredWidth: 220f, preferredHeight: 220f, minWidth: 220f);
            avatar.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            _photoPlaceholder = kit.Image(avatar.transform, "Placeholder", iconUser, Color.white, false);
            _photoPlaceholder.rectTransform.anchorMin = _photoPlaceholder.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _photoPlaceholder.rectTransform.sizeDelta = new Vector2(120f, 120f);
            RectTransform photoRt = UIKit.Rect("Photo", avatar.transform);
            UIKit.Stretch(photoRt, 6f, 6f, 6f, 6f);
            _photo = photoRt.gameObject.AddComponent<RawImage>();
            _photo.raycastTarget = false;
            photoRt.gameObject.SetActive(false);

            RectTransform texts = UIKit.Rect("Texts", top);
            VerticalLayoutGroup textColumn = UIKit.Column(texts.gameObject, 0, 8f, TextAnchor.MiddleLeft);
            textColumn.childForceExpandHeight = false;
            UIKit.Layout(texts.gameObject, flexibleWidth: 1f, preferredHeight: 220f);
            _name = kit.Text(texts, "Name", "", 56f, UIPalette.TextPrimary);
            _name.enableWordWrapping = false;
            _name.overflowMode = TextOverflowModes.Ellipsis;
            UIKit.Layout(_name.gameObject, preferredHeight: 72f);
            _nameSign = kit.Text(texts, "NameSign", "", 30f, UIPalette.Primary, TextAlignmentOptions.MidlineLeft, false);
            _nameSign.enableWordWrapping = false;
            _nameSign.overflowMode = TextOverflowModes.Ellipsis;
            _nameSign.richText = true;
            UIKit.Layout(_nameSign.gameObject, preferredHeight: 44f);
            _joined = kit.Text(texts, "Joined", "", 28f, UIPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, false);
            _joined.enableWordWrapping = false;
            UIKit.Layout(_joined.gameObject, preferredHeight: 40f);

            Button edit = kit.IconButton(top, "Edit", iconPencil, "✎", 84f, UIPalette.Primary, UIPalette.CyanLight, OpenAccount);
            edit.transform.SetAsLastSibling();

            // Flame and total XP.
            RectTransform stats = UIKit.Rect("Stats", card);
            HorizontalLayoutGroup statsRow = UIKit.Row(stats.gameObject, 24f);
            statsRow.childForceExpandWidth = true;
            UIKit.Layout(stats.gameObject, preferredHeight: 150f);
            _streak = Chip(kit, stats, "Flame", iconFlame, UIPalette.Streak, UIPalette.AmberLight, Loc.Pick("day flame", "günlük alov", "дн. огонёк"));
            _xp = Chip(kit, stats, "Xp", iconZap, UIPalette.Xp, UIPalette.CyanLight, Loc.Pick("total XP", "ümumi XP", "всего XP"));
        }

        private TextMeshProUGUI Chip(UIKit kit, Transform parent, string name, Sprite icon, Color tint, Color fill, string caption)
        {
            Image chip = kit.Image(parent, name, theme != null ? theme.panel : null, fill, true);
            UIKit.Layout(chip.gameObject, flexibleWidth: 1f, preferredHeight: 150f);
            HorizontalLayoutGroup row = UIKit.Row(chip.gameObject, 18f);
            row.padding = new RectOffset(28, 20, 16, 16);
            kit.Icon(chip.transform, "Icon", icon, 64f, tint);
            RectTransform texts = UIKit.Rect("Texts", chip.transform);
            UIKit.Column(texts.gameObject, 0, 0f, TextAnchor.MiddleLeft);
            UIKit.Layout(texts.gameObject, flexibleWidth: 1f);
            TextMeshProUGUI value = kit.Text(texts, "Value", "0", 52f, UIPalette.TextPrimary);
            value.enableWordWrapping = false;
            kit.Text(texts, "Caption", caption, 26f, UIPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, false).enableWordWrapping = false;
            return value;
        }

        /// <summary>A whole-width row that opens a page: icon tile, title and a short line, arrow. Returns the short line.</summary>
        private TextMeshProUGUI Entry(UIKit kit, Transform parent, string name, Sprite icon, Color tint, Color fill, string title, string note,
            Action onClick)
        {
            RectTransform card = kit.Card(parent, name, 0, 0f);
            DestroyImmediate(card.GetComponent<VerticalLayoutGroup>());
            Image background = card.GetComponent<Image>();
            background.raycastTarget = true;
            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => onClick());
            HorizontalLayoutGroup row = UIKit.Row(card.gameObject, 28f);
            row.padding = new RectOffset(32, 36, 24, 24);
            UIKit.Layout(card.gameObject, preferredHeight: 164f, minHeight: 164f);

            Image tile = kit.Image(card, "IconTile", theme != null ? theme.panel : null, fill, true);
            UIKit.Layout(tile.gameObject, preferredWidth: 112f, preferredHeight: 112f, minWidth: 112f);
            Image glyph = kit.Image(tile.transform, "Icon", icon, tint, false);
            glyph.rectTransform.anchorMin = glyph.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            glyph.rectTransform.sizeDelta = new Vector2(62f, 62f);

            RectTransform texts = UIKit.Rect("Texts", card);
            UIKit.Column(texts.gameObject, 0, 4f, TextAnchor.MiddleLeft);
            UIKit.Layout(texts.gameObject, flexibleWidth: 1f);
            kit.Text(texts, "Title", title, 40f, UIPalette.TextPrimary);
            TextMeshProUGUI line = kit.Text(texts, "Note", note, 27f, UIPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, false);
            line.enableWordWrapping = false;
            line.overflowMode = TextOverflowModes.Ellipsis;

            TextMeshProUGUI arrow = kit.Text(card, "Arrow", "›", 64f, UIPalette.TextMuted, TextAlignmentOptions.Center);
            UIKit.Layout(arrow.gameObject, preferredWidth: 40f, minWidth: 40f);
            return line;
        }
    }
}
