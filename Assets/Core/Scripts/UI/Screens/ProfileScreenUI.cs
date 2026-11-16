using System;
using System.Collections.Generic;
using System.Globalization;
using Cevir.Core.Cloud;
using Cevir.Core.Game;
using Cevir.Core.Gamification;
using Cevir.Core.Localization;
using Cevir.Core.Social;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Profile: header (avatar, name, join date, Edit profile), this week's XP / league place / league badge,
    /// the statistics grid (streak, total XP, level, signs learned), the invite card and the account (e-mail, sign out).
    /// </summary>
    public sealed class ProfileScreenUI : MonoBehaviour
    {
        [Header("Header")]
        [SerializeField] private RawImage avatarPhoto;
        [SerializeField] private GameObject avatarIcon;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI joinedLabel;
        [SerializeField] private Button editButton;

        [Header("Metrics")]
        [SerializeField] private TextMeshProUGUI weeklyXpValue;
        [SerializeField] private TextMeshProUGUI leagueRankValue;
        [SerializeField] private Image leagueBadgeIcon;
        [SerializeField] private Image leagueBadgeBackdrop;
        [SerializeField] private TextMeshProUGUI leagueBadgeLabel;

        [Header("Statistics")]
        [SerializeField] private TextMeshProUGUI streakValue;
        [SerializeField] private TextMeshProUGUI xpValue;
        [SerializeField] private TextMeshProUGUI levelValue;
        [SerializeField] private TextMeshProUGUI signsValue;

        [Header("Invite")]
        [SerializeField] private Button inviteButton;
        [SerializeField] private Button instagramButton;
        [SerializeField] private Button facebookButton;

        [Header("Edit profile")]
        [SerializeField] private BottomSheetUI sheet;
        [SerializeField] private TMP_InputField firstNameField;
        [SerializeField] private TMP_InputField lastNameField;
        [SerializeField] private Button rightHandButton;
        [SerializeField] private Button leftHandButton;

        [Header("Notifications")]
        [SerializeField] private TextMeshProUGUI notificationsStatus;
        [SerializeField] private Button notificationsButton;

        [Header("Account")]
        [SerializeField] private GameObject accountCard;
        [SerializeField] private TextMeshProUGUI emailLabel;
        [SerializeField] private Button signOutButton;
        [SerializeField] private Button settingsButton;

        private GameManager _game;
        private bool _pendingLeftHanded;

        private void Awake()
        {
            if (editButton != null) editButton.onClick.AddListener(OpenEditor);
            if (inviteButton != null) inviteButton.onClick.AddListener(Invite);
            if (instagramButton != null) instagramButton.onClick.AddListener(() => Application.OpenURL("https://www.instagram.com/"));
            if (facebookButton != null) facebookButton.onClick.AddListener(() =>
                Application.OpenURL("https://www.facebook.com/sharer/sharer.php?u=" + Uri.EscapeDataString(InviteLink)));
            if (signOutButton != null) signOutButton.onClick.AddListener(ConfirmSignOut);
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenEditor);
            if (notificationsButton != null) notificationsButton.onClick.AddListener(() =>
            {
                if (ReminderSettingsUI.Instance != null) ReminderSettingsUI.Instance.Open(false);
            });
            if (rightHandButton != null) rightHandButton.onClick.AddListener(() => { _pendingLeftHanded = false; RefreshEditorToggles(); });
            if (leftHandButton != null) leftHandButton.onClick.AddListener(() => { _pendingLeftHanded = true; RefreshEditorToggles(); });
        }

        private void OnEnable()
        {
            _game = GameManager.Instance;
            Loc.LanguageChanged += OnLanguageChanged;
            if (_game != null)
            {
                _game.ProfileChanged += Refresh;
                _game.Xp.XpGained += OnXpGained;
                _game.Streak.StreakChanged += OnStreakChanged;
            }

            if (sheet != null) sheet.Hide();
            AvatarStore.Changed += OnAvatarChanged;
            _avatarUrlShown = null;
            Refresh();
        }

        private string _avatarUrlShown;
        private Texture2D _avatarTexture;
        private float _nextReminderRefresh;

        private void OnAvatarChanged()
        {
            _avatarUrlShown = null;
            Refresh();
        }

        private void Update()
        {
            // The reminder settings change in another panel; keep the line here current.
            if (Time.unscaledTime < _nextReminderRefresh) return;
            _nextReminderRefresh = Time.unscaledTime + 1f;
            RefreshReminders();
        }

        private void RefreshReminders()
        {
            if (notificationsStatus == null || _game == null || _game.Progress == null) return;
            ReminderSettings r = _game.Progress.reminders;
            notificationsStatus.text = r != null && r.configured && r.enabled
                ? "Hər gün " + r.WindowText + " aralığında xatırladırıq"
                : "Xatırlatmalar bağlıdır";
        }

        private void RefreshAvatar()
        {
            if (avatarPhoto == null || _game == null) return;
            string url = _game.Progress.avatarUrl ?? string.Empty;
            if (_avatarUrlShown == url) return;
            _avatarUrlShown = url;
            StartCoroutine(AvatarStore.Fetch(url, texture =>
            {
                if (_avatarTexture != null && _avatarTexture != texture) Destroy(_avatarTexture);
                _avatarTexture = texture;
                avatarPhoto.texture = texture;
                avatarPhoto.gameObject.SetActive(texture != null);
                if (avatarIcon != null) avatarIcon.SetActive(texture == null);
            }));
        }

        private void OnDisable()
        {
            AvatarStore.Changed -= OnAvatarChanged;
            Loc.LanguageChanged -= OnLanguageChanged;
            if (_game == null) return;
            _game.ProfileChanged -= Refresh;
            _game.Xp.XpGained -= OnXpGained;
            _game.Streak.StreakChanged -= OnStreakChanged;
        }

        private void OnLanguageChanged(string language) => Refresh();
        private void OnXpGained(XpState state, int amount) => Refresh();
        private void OnStreakChanged(StreakState state) => Refresh();

        private string InviteLink => _game != null && _game.Social != null ? _game.Social.InviteLink : "https://chevirapp.com";

        private void Refresh()
        {
            if (_game == null) return;
            PlayerProgress progress = _game.Progress;

            if (nameLabel != null) nameLabel.text = _game.LearnerName;
            if (isActiveAndEnabled) RefreshAvatar();
            RefreshReminders();
            if (joinedLabel != null)
            {
                var joined = new DateTime(progress.joinedUtcTicks, DateTimeKind.Utc).ToLocalTime();
                CultureInfo culture = Loc.IsAz ? AzerbaijaniCulture() : CultureInfo.InvariantCulture;
                string month = culture.DateTimeFormat.GetMonthName(joined.Month);
                if (Loc.IsAz && month.Length > 0) month = char.ToUpper(month[0]) + month.Substring(1);
                joinedLabel.text = string.Format(UIStrings.Joined, month + " " + joined.Year);
            }

            LeagueSnapshot league = _game.GetLeagueSnapshot();
            LeagueEntry you = league.You;
            if (weeklyXpValue != null) weeklyXpValue.text = _game.Xp.WeeklyFor(_game.LocalNow()).ToString();
            if (leagueRankValue != null) leagueRankValue.text = you != null && !league.Offline ? "#" + you.Rank : "—";

            Color tier = LeagueTiers.Color(progress.leagueTier);
            if (leagueBadgeIcon != null) leagueBadgeIcon.color = tier;
            if (leagueBadgeBackdrop != null) leagueBadgeBackdrop.color = new Color(1f, 1f, 1f, 0f); // pixel design: the cup alone
            if (leagueBadgeLabel != null) leagueBadgeLabel.text = LeagueTiers.Name(progress.leagueTier);

            if (streakValue != null) streakValue.text = _game.Streak.State.current.ToString();
            if (xpValue != null) xpValue.text = _game.Xp.Total.ToString();
            LevelPathSummary level = _game.CurrentLevel;
            if (levelValue != null) levelValue.text = level != null ? level.Level.Title : "—";
            if (signsValue != null) signsValue.text = _game.ActivePackProgress != null ? _game.ActivePackProgress.SignsLearned.ToString() : "0";

            CloudService cloud = CloudService.Instance;
            bool signedIn = cloud != null && cloud.State == CloudState.SignedIn;
            if (accountCard != null) accountCard.SetActive(signedIn);
            if (emailLabel != null) emailLabel.text = signedIn ? cloud.Email : string.Empty;
        }

        private void ConfirmSignOut()
        {
            CloudService cloud = CloudService.Instance;
            if (cloud == null || sheet == null) return;
            sheet.Show(null, UIStrings.SignOutTitle, UIStrings.SignOutBody, UIPalette.Red, UIStrings.SignOut, cloud.SignOut, UIStrings.Close, null, false);
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

        private void Invite()
        {
            string text = string.Format(UIStrings.InviteMessage, _game != null ? _game.Streak.State.current : 0, InviteLink);
            if (!ShareUtility.ShareText(text, UIStrings.InviteFriends) && CevirUIRoot.Instance != null)
            {
                CevirUIRoot.Instance.ShowToast(UIStrings.LinkCopied);
            }
        }

        // ------------------------------------------------------------------ edit profile

        private void OpenEditor()
        {
            if (AccountSettingsUI.Instance != null)
            {
                AccountSettingsUI.Instance.Open();
                return;
            }

            if (sheet == null || _game == null) return;
            if (firstNameField != null) firstNameField.text = _game.Progress.firstName;
            if (lastNameField != null) lastNameField.text = _game.Progress.lastName;
            _pendingLeftHanded = _game.Progress.leftHanded;
            RefreshEditorToggles();
            sheet.Show(null, UIStrings.EditProfile, null, UIPalette.Primary, UIStrings.Save, SaveEditor, UIStrings.Close, null, true);
        }

        private void SaveEditor()
        {
            if (_game == null) return;
            _game.SetLearnerName(firstNameField != null ? firstNameField.text : _game.Progress.firstName,
                lastNameField != null ? lastNameField.text : _game.Progress.lastName);
            if (_pendingLeftHanded != _game.Progress.leftHanded) _game.SetLeftHanded(_pendingLeftHanded);
        }

        private void RefreshEditorToggles()
        {
            Toggle(rightHandButton, !_pendingLeftHanded);
            Toggle(leftHandButton, _pendingLeftHanded);
        }

        private static void Toggle(Button button, bool on)
        {
            if (button == null) return;
            var image = button.targetGraphic as Image;
            if (image != null) image.color = on ? UIPalette.Primary : UIPalette.Surface;
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.color = on ? Color.white : UIPalette.TextSecondary;
        }
    }
}
