using Cevir.Core.Localization;
using Cevir.Core.Cloud;
using Cevir.Core.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Profile > Account: the registered e-mail (and changing it), profile photo, name, ləqəb and signing hand, password, sign out and account
    /// deletion. Account parts show only when signed in to the server. Builds its own content at runtime.
    /// </summary>
    public sealed class AccountSettingsUI : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private CevirTheme theme;
        [SerializeField] private Sprite background;
        [SerializeField] private Sprite iconBack;
        [SerializeField] private Sprite iconUser;
        [SerializeField] private Sprite iconBell;

        private GameObject _panel;
        private RawImage _photo;
        private Image _photoPlaceholder;
        private GameObject _removePhoto;
        private TMP_InputField _firstName;
        private TMP_InputField _lastName;
        private TextMeshProUGUI _nameSignLabel;
        private string _nameSign = string.Empty;
        private Button _rightHand;
        private Button _leftHand;
        private bool _leftHanded;
        private GameObject _emailCard;
        private TextMeshProUGUI _emailCurrent;
        private TMP_InputField _newEmail;
        private GameObject _passwordCard;
        private TextMeshProUGUI _phoneCurrent;
        private TMP_InputField _newPhone;
        private TMP_InputField _newPassword;
        private TMP_InputField _repeatPassword;
        private TextMeshProUGUI _remindersStatus;
        private GameObject _dataCard;
        private Button _dataOn;
        private Button _dataOff;
        private GameObject _dangerCard;
        private TextMeshProUGUI _deleteLabel;
        private TextMeshProUGUI _message;
        private bool _deleteArmed;
        private bool _busy;
        private Texture2D _photoTexture;
        private Button _dayTheme;
        private Button _nightTheme;

        public static AccountSettingsUI Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Build();
            _panel.SetActive(false);
            AvatarStore.Changed += ShowLocalPhoto;
            Loc.LanguageChanged += Rebuild;
            ThemeManager.Changed += Render;
        }

        private void OnDestroy()
        {
            Loc.LanguageChanged -= Rebuild;
            ThemeManager.Changed -= Render;
            AvatarStore.Changed -= ShowLocalPhoto;
            if (Instance == this) Instance = null;
        }

        public bool IsOpen => _panel != null && _panel.activeSelf;

        public void Open()
        {
            GameManager game = GameManager.Instance;
            if (game == null) return;
            _firstName.text = game.Progress.firstName;
            _lastName.text = game.Progress.lastName;
            _nameSign = game.Progress.nameSign ?? string.Empty;
            ShowNameSign();
            _leftHanded = game.Progress.leftHanded;
            _newEmail.text = string.Empty;
            _newPassword.text = string.Empty;
            _repeatPassword.text = string.Empty;
            _deleteArmed = false;
            Message(null, false);
            _panel.SetActive(true);
            ShowLocalPhoto();
            Render();
        }

        public void Close()
        {
            _panel.SetActive(false);
        }

        /// <summary>Content is built once in the interface language; a new language builds it again.</summary>
        private void Rebuild(string language)
        {
            bool open = IsOpen;
            if (_panel != null) Destroy(_panel);
            Build();
            _panel.SetActive(false);
            if (open) Open();
        }

        private void OnEnable()
        {
            ReminderRefresh();
        }

        private void Update()
        {
            // Keeps the reminder line current after the reminder panel closes.
            if (IsOpen && Time.frameCount % 30 == 0) ReminderRefresh();
        }

        private void Render()
        {
            if (_panel == null) return;
            CloudService cloud = CloudService.Instance;
            bool signedIn = cloud != null && cloud.State == CloudState.SignedIn;
            _emailCard.SetActive(signedIn);
            _passwordCard.SetActive(signedIn);
            _dangerCard.SetActive(signedIn);
            _dataCard.SetActive(signedIn);
            bool consent = GameManager.Instance != null && GameManager.Instance.Progress.dataConsent;
            UIKit.Select(_dataOn, consent);
            UIKit.Select(_dataOff, !consent);
            string phoneNow = GameManager.Instance != null ? GameManager.Instance.Progress.phone : string.Empty;
            _phoneCurrent.text = string.IsNullOrEmpty(phoneNow)
                ? Loc.Pick("Not added yet", "Hələ əlavə olunmayıb", "Ещё не добавлен")
                : Loc.T("Hazırkı: ") + "<b>" + phoneNow + "</b>";
            _emailCurrent.text = signedIn ? Loc.T("Hazırkı: ") + "<b>" + cloud.Email + "</b>" : string.Empty;
            _deleteLabel.text = _deleteArmed ? Loc.T("TƏSDİQLƏ: HESABI SİL") : Loc.T("HESABI SİL");
            UIKit.Select(_dayTheme, !ThemeManager.IsDark);
            UIKit.Select(_nightTheme, ThemeManager.IsDark);
            UIKit.Select(_rightHand, !_leftHanded);
            UIKit.Select(_leftHand, _leftHanded);
            ReminderRefresh();
        }

        private void ReminderRefresh()
        {
            GameManager game = GameManager.Instance;
            if (_remindersStatus == null || game == null || game.Progress == null) return;
            ReminderSettings r = game.Progress.reminders;
            _remindersStatus.text = r != null && r.enabled && r.configured ? Loc.T("Açıq") + " · " + r.WindowText : Loc.T("Bağlı");
        }

        // ------------------------------------------------------------------ photo

        private void ShowLocalPhoto()
        {
            if (_photo == null) return;
            if (_photoTexture != null) Destroy(_photoTexture);
            _photoTexture = AvatarStore.LoadLocal();
            _photo.texture = _photoTexture;
            _photo.gameObject.SetActive(_photoTexture != null);
            _photoPlaceholder.gameObject.SetActive(_photoTexture == null);
            _removePhoto.SetActive(_photoTexture != null);
        }

        private void PickPhoto()
        {
            if (_busy) return;
#if UNITY_ANDROID || UNITY_IOS
            NativeGallery.GetImageFromGallery(path =>
            {
                if (string.IsNullOrEmpty(path)) return;
                Texture2D picked = NativeGallery.LoadImageAtPath(path, 1024, false, false);
                if (picked == null)
                {
                    Message(Loc.T("Şəkil açıla bilmədi. Başqa şəkil seç."), true);
                    return;
                }

                byte[] jpeg = AvatarStore.PrepareJpeg(picked);
                Destroy(picked);
                UploadPhoto(jpeg);
            }, Loc.T("Profil şəkli seç"), "image/*");
#else
            Message(Loc.T("Şəkil seçmək telefonda işləyir."), true);
#endif
        }

        private void UploadPhoto(byte[] jpeg)
        {
            if (jpeg == null) return;
            AvatarStore.Save(jpeg, null);
            CloudService cloud = CloudService.Instance;
            if (cloud == null || cloud.State != CloudState.SignedIn)
            {
                Message(Loc.T("Şəkil yadda saxlandı."), false);
                return;
            }

            SetBusy(true);
            Message(Loc.T("Şəkil yüklənir..."), false);
            cloud.UploadAvatar(jpeg, error =>
            {
                SetBusy(false);
                if (error == null)
                {
                    AvatarStore.Save(jpeg, GameManager.Instance != null ? GameManager.Instance.Progress.avatarUrl : null);
                    Message(Loc.T("Profil şəkli yeniləndi."), false);
                }
                else
                {
                    Message(error, true);
                }
            });
        }

        private void RemovePhoto()
        {
            if (_busy) return;
            AvatarStore.Delete();
            CloudService cloud = CloudService.Instance;
            if (cloud == null || cloud.State != CloudState.SignedIn)
            {
                if (GameManager.Instance != null) GameManager.Instance.SetAvatarUrl(string.Empty);
                return;
            }

            SetBusy(true);
            cloud.RemoveAvatar(error =>
            {
                SetBusy(false);
                Message(error ?? Loc.T("Profil şəkli silindi."), error != null);
            });
        }

        // ------------------------------------------------------------------ details

        private void SaveDetails()
        {
            GameManager game = GameManager.Instance;
            if (game == null) return;
            if (_firstName.text.Trim().Length == 0)
            {
                Message(Loc.T("Adını yaz."), true);
                return;
            }

            game.SetLearnerName(_firstName.text, _lastName.text);
            if (_nameSign != game.Progress.nameSign) game.SetNameSign(_nameSign);
            if (_leftHanded != game.Progress.leftHanded) game.SetLeftHanded(_leftHanded);
            Message(Loc.T("Məlumatlar yadda saxlandı."), false);
        }

        private void PickNameSign()
        {
            if (NameSignPickerUI.Instance == null) return;
            NameSignPickerUI.Instance.Open(_nameSign, word =>
            {
                _nameSign = word ?? string.Empty;
                ShowNameSign();
            });
        }

        private void ShowNameSign()
        {
            if (_nameSignLabel != null) _nameSignLabel.text = _nameSign.Length > 0 ? Stories.NameSignOptions.Display(_nameSign) : Loc.T("Seç");
        }

        private void ChangeEmail()
        {
            CloudService cloud = CloudService.Instance;
            string email = _newEmail.text.Trim();
            if (_busy || cloud == null) return;
            if (email.Length < 3 || !email.Contains("@") || !email.Contains("."))
            {
                Message(Loc.T("Yeni e-poçt ünvanını düzgün yaz."), true);
                return;
            }

            SetBusy(true);
            cloud.ChangeEmail(email, (error, pending) =>
            {
                SetBusy(false);
                if (error != null)
                {
                    Message(error, true);
                    return;
                }

                _newEmail.text = string.Empty;
                Message(pending
                    ? string.Format(Loc.T("Təsdiq linki {0} ünvanına göndərildi. Linki açandan sonra e-poçt dəyişəcək."), email)
                    : Loc.T("E-poçt dəyişdirildi."), false);
                Render();
            });
        }

        private void SavePhone()
        {
            GameManager game = GameManager.Instance;
            if (game == null) return;
            string typed = _newPhone.text.Trim();
            string phone = GameManager.CleanPhone(typed);
            if (typed.Length > 0 && phone.Length == 0)
            {
                Message(Loc.Pick("Write the phone number with digits only, e.g. +994501234567.",
                    "Telefon nömrəsini rəqəmlərlə yaz, məs. +994501234567.", "Напиши номер цифрами, например +994501234567."), true);
                return;
            }

            game.SetPhone(phone);
            _newPhone.text = string.Empty;
            Message(phone.Length > 0 ? Loc.Pick("Phone number saved.", "Telefon nömrəsi yadda saxlandı.", "Номер сохранён.")
                : Loc.Pick("Phone number removed.", "Telefon nömrəsi silindi.", "Номер удалён."), false);
            Render();
        }

        private void ChangePassword()
        {
            CloudService cloud = CloudService.Instance;
            if (_busy || cloud == null) return;
            if (_newPassword.text.Length < 6)
            {
                Message(Loc.T("Şifrə ən azı 6 simvol olmalıdır."), true);
                return;
            }

            if (_newPassword.text != _repeatPassword.text)
            {
                Message(Loc.T("Şifrələr eyni deyil."), true);
                return;
            }

            SetBusy(true);
            cloud.ChangePassword(_newPassword.text, error =>
            {
                SetBusy(false);
                if (error == null)
                {
                    _newPassword.text = string.Empty;
                    _repeatPassword.text = string.Empty;
                }

                Message(error ?? Loc.T("Şifrə dəyişdirildi."), error != null);
            });
        }

        private void SetDataConsent(bool allowed)
        {
            CloudService cloud = CloudService.Instance;
            GameManager game = GameManager.Instance;
            if (_busy || cloud == null || game == null) return;
            if (game.Progress.dataConsent == allowed && game.Progress.dataConsentAsked) return;

            SetBusy(true);
            cloud.SetDataConsent(allowed, error =>
            {
                SetBusy(false);
                Render();
                if (error != null) Message(error, true);
                else Message(allowed ? Loc.T("Təşəkkürlər! Cəhdlərin Chevir-i inkişaf etdirməyə kömək edəcək.")
                    : Loc.T("Söndürüldü. Əvvəl saxlanan cəhdlərin silindi."), false);
            });
        }

        private void OpenReminders()
        {
            if (ReminderSettingsUI.Instance != null) ReminderSettingsUI.Instance.Open(false);
        }

        private void SignOut()
        {
            CloudService cloud = CloudService.Instance;
            if (cloud == null) return;
            Close();
            cloud.SignOut();
        }

        private void DeleteAccount()
        {
            CloudService cloud = CloudService.Instance;
            if (_busy || cloud == null) return;
            if (!_deleteArmed)
            {
                _deleteArmed = true;
                Message(Loc.T("Hesabın və bütün irəliləyişin həmişəlik silinəcək. Əminsənsə, bir daha bas."), true);
                Render();
                return;
            }

            SetBusy(true);
            cloud.DeleteAccount(error =>
            {
                SetBusy(false);
                if (error != null)
                {
                    _deleteArmed = false;
                    Message(error, true);
                    Render();
                    return;
                }

                Close();
            });
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
        }

        private void Message(string text, bool error)
        {
            if (_message == null) return;
            _message.text = text ?? string.Empty;
            _message.color = error ? UIPalette.Red : UIPalette.GreenDark;
            _message.gameObject.SetActive(!string.IsNullOrEmpty(text));
            if (!string.IsNullOrEmpty(text) && CevirUIRoot.Instance != null) CevirUIRoot.Instance.ShowToast(text);
        }

        // ------------------------------------------------------------------ layout

        private void Build()
        {
            var kit = new UIKit(font, theme);
            RectTransform panel = UIKit.Rect("Panel", transform);
            UIKit.Stretch(panel);
            Image bg = panel.gameObject.AddComponent<Image>();
            bg.sprite = background;
            bg.color = background != null ? Color.white : UIPalette.Background;
            bg.raycastTarget = true;
            _panel = panel.gameObject;
            HomeSkyUI.Create(panel, theme); // the same sky as the Learn page (time of day and weather)

            RectTransform safe = UIKit.Rect("SafeArea", panel);
            UIKit.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            RectTransform content = kit.Scroll(safe, new RectOffset(60, 60, 40, 100), 30f);

            // Title bar on a white card, so it reads on every sky.
            RectTransform top = kit.Card(content, "Top", 0, 0f);
            DestroyImmediate(top.GetComponent<VerticalLayoutGroup>());
            HorizontalLayoutGroup topRow = UIKit.Row(top.gameObject, 20f);
            topRow.padding = new RectOffset(20, 20, 16, 16);
            UIKit.Layout(top.gameObject, preferredHeight: 128f);
            kit.IconButton(top, "Back", iconBack, "‹", 96f, UIPalette.TextPrimary, Color.white, Close);
            TextMeshProUGUI heading = kit.Text(top, "Heading", Loc.Pick("Account", "Hesab", "Аккаунт"), 46f, UIPalette.TextPrimary);
            UIKit.Layout(heading.gameObject, flexibleWidth: 1f);

            _message = kit.Text(content, "Message", "", 30f, UIPalette.GreenDark, TextAlignmentOptions.Center, false);
            _message.gameObject.SetActive(false);

            // Photo.
            RectTransform photoCard = kit.Card(content, "PhotoCard", 40, 20f);
            VerticalLayoutGroup photoColumn = photoCard.GetComponent<VerticalLayoutGroup>();
            photoColumn.childAlignment = TextAnchor.UpperCenter;
            photoColumn.childForceExpandWidth = false;
            Image ring = kit.Image(photoCard, "Avatar", theme != null ? theme.circle : null, UIPalette.Primary, false);
            UIKit.Layout(ring.gameObject, preferredWidth: 240f, preferredHeight: 240f);
            Mask mask = ring.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            _photoPlaceholder = kit.Image(ring.transform, "Placeholder", iconUser, Color.white, false);
            _photoPlaceholder.rectTransform.anchorMin = _photoPlaceholder.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _photoPlaceholder.rectTransform.sizeDelta = new Vector2(130f, 130f);
            RectTransform photoRt = UIKit.Rect("Photo", ring.transform);
            UIKit.Stretch(photoRt);
            _photo = photoRt.gameObject.AddComponent<RawImage>();
            _photo.raycastTarget = false;

            RectTransform photoButtons = UIKit.Rect("PhotoButtons", photoCard);
            HorizontalLayoutGroup photoRow = UIKit.Row(photoButtons.gameObject, 20f, TextAnchor.MiddleCenter);
            photoRow.childForceExpandWidth = true;
            UIKit.Layout(photoButtons.gameObject, preferredWidth: 900f);
            Button pick = kit.Button(photoButtons, "Pick", Loc.T("ŞƏKLİ DƏYİŞ"), Color.white, Color.white, 104f, true, PickPhoto);
            UIKit.Layout(pick.gameObject, flexibleWidth: 1f);
            Button remove = kit.Button(photoButtons, "Remove", Loc.T("SİL"), UIPalette.RedLight, UIPalette.RedDark, 104f, false, RemovePhoto);
            UIKit.Layout(remove.gameObject, flexibleWidth: 0.5f);
            _removePhoto = remove.gameObject;

            // Name and signing hand.
            RectTransform details = kit.Card(content, "Details", 40, 20f);
            kit.Text(details, "Title", Loc.T("Şəxsi məlumatlar"), 36f, UIPalette.TextPrimary);
            _firstName = kit.Input(details, "FirstName", Loc.T("Ad"), TMP_InputField.ContentType.Name, 30);
            _lastName = kit.Input(details, "LastName", Loc.T("Soyad"), TMP_InputField.ContentType.Name, 30);
            kit.Text(details, "NameSignTitle", Loc.T("Ləqəbin / ən tanınan xüsusiyyətin"), 30f, UIPalette.TextSecondary);
            Button nameSign = kit.Button(details, "NameSign", "", UIPalette.Surface, UIPalette.TextPrimary, 92f, false, out _nameSignLabel, PickNameSign);
            UIKit.Layout(nameSign.gameObject, flexibleWidth: 1f);
            kit.Text(details, "HandTitle", Loc.T("İşarə əli"), 30f, UIPalette.TextSecondary);
            RectTransform hands = UIKit.Rect("Hands", details);
            UIKit.Row(hands.gameObject, 16f).childForceExpandWidth = true;
            _rightHand = kit.Button(hands, "Right", Loc.T("Sağ"), UIPalette.Surface, UIPalette.TextSecondary, 92f, false, () => { _leftHanded = false; Render(); });
            UIKit.Layout(_rightHand.gameObject, flexibleWidth: 1f);
            _leftHand = kit.Button(hands, "Left", Loc.T("Sol"), UIPalette.Surface, UIPalette.TextSecondary, 92f, false, () => { _leftHanded = true; Render(); });
            UIKit.Layout(_leftHand.gameObject, flexibleWidth: 1f);
            kit.Button(details, "SaveDetails", Loc.T("YADDA SAXLA"), Color.white, Color.white, 112f, true, SaveDetails);

            // Look: day (light) or night (dark) colours, saved on this phone.
            RectTransform look = kit.Card(content, "Look", 40, 18f);
            kit.Text(look, "Title", Loc.T("Görünüş"), 36f, UIPalette.TextPrimary);
            RectTransform lookRow = UIKit.Rect("Choice", look);
            UIKit.Row(lookRow.gameObject, 16f).childForceExpandWidth = true;
            _dayTheme = kit.Button(lookRow, "Day", Loc.T("GÜNDÜZ"), UIPalette.Surface, UIPalette.TextSecondary, 96f, false, () => ThemeManager.SetDark(false));
            UIKit.Layout(_dayTheme.gameObject, flexibleWidth: 1f);
            _nightTheme = kit.Button(lookRow, "Night", Loc.T("GECƏ"), UIPalette.Surface, UIPalette.TextSecondary, 96f, false, () => ThemeManager.SetDark(true));
            UIKit.Layout(_nightTheme.gameObject, flexibleWidth: 1f);

            // Data permission (sign samples for the dataset).
            RectTransform data = kit.Card(content, "DataConsent", 40, 16f);
            _dataCard = data.gameObject;
            kit.Text(data, "Title", DataConsentUI.Title, 34f, UIPalette.TextPrimary);
            kit.Text(data, "Body", Loc.T("Kamerada göstərdiyin işarələrin nöqtələri və 3 saniyəlik kiçik video saxlanılır ki, Chevir " +
                "işarə dilini daha yaxşı tanısın. Söndürəndə əvvəl saxlananlar da silinir."), 28f, UIPalette.TextSecondary,
                TextAlignmentOptions.TopLeft, false);
            RectTransform dataRow = UIKit.Rect("Choice", data);
            UIKit.Row(dataRow.gameObject, 16f).childForceExpandWidth = true;
            _dataOn = kit.Button(dataRow, "On", Loc.T("İcazə verirəm"), UIPalette.Surface, UIPalette.TextSecondary, 92f, false, () => SetDataConsent(true));
            UIKit.Layout(_dataOn.gameObject, flexibleWidth: 1f);
            _dataOff = kit.Button(dataRow, "Off", Loc.T("İcazə vermirəm"), UIPalette.Surface, UIPalette.TextSecondary, 92f, false, () => SetDataConsent(false));
            UIKit.Layout(_dataOff.gameObject, flexibleWidth: 1f);

            // E-mail.
            RectTransform emailCard = kit.Card(content, "Email", 40, 18f);
            _emailCard = emailCard.gameObject;
            kit.Text(emailCard, "Title", Loc.Pick("E-mail you registered with", "Qeydiyyatdan keçdiyin e-poçt", "Почта, с которой ты зарегистрирован"),
                36f, UIPalette.TextPrimary);
            _emailCurrent = kit.Text(emailCard, "Current", "", 30f, UIPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, false);
            _emailCurrent.enableWordWrapping = false;
            _emailCurrent.overflowMode = TextOverflowModes.Ellipsis;
            _newEmail = kit.Input(emailCard, "NewEmail", Loc.T("Yeni e-poçt"), TMP_InputField.ContentType.EmailAddress, 254);
            kit.Button(emailCard, "ChangeEmail", Loc.T("E-POÇTU DƏYİŞ"), UIPalette.CyanLight, UIPalette.Primary, 104f, false, ChangeEmail);

            // Phone number (private: only the learner sees it).
            RectTransform phoneCard = kit.Card(content, "Phone", 40, 18f);
            kit.Text(phoneCard, "Title", Loc.Pick("Phone number", "Telefon nömrəsi", "Номер телефона"), 36f, UIPalette.TextPrimary);
            _phoneCurrent = kit.Text(phoneCard, "Current", "", 30f, UIPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, false);
            _newPhone = kit.Input(phoneCard, "NewPhone", Loc.Pick("New number, e.g. +994501234567", "Yeni nömrə, məs. +994501234567",
                "Новый номер, например +994501234567"), TMP_InputField.ContentType.Standard, 20);
            _newPhone.keyboardType = TouchScreenKeyboardType.PhonePad;
            kit.Button(phoneCard, "SavePhone", Loc.Pick("SAVE NUMBER", "NÖMRƏNİ YADDA SAXLA", "СОХРАНИТЬ НОМЕР"), UIPalette.CyanLight,
                UIPalette.Primary, 104f, false, SavePhone);

            // Password.
            RectTransform passwordCard = kit.Card(content, "Password", 40, 18f);
            _passwordCard = passwordCard.gameObject;
            kit.Text(passwordCard, "Title", Loc.T("Şifrəni dəyiş"), 36f, UIPalette.TextPrimary);
            _newPassword = kit.Input(passwordCard, "NewPassword", Loc.T("Yeni şifrə (ən azı 6 simvol)"), TMP_InputField.ContentType.Password, 72);
            _repeatPassword = kit.Input(passwordCard, "RepeatPassword", Loc.T("Yeni şifrəni təkrar yaz"), TMP_InputField.ContentType.Password, 72);
            kit.Button(passwordCard, "ChangePassword", Loc.T("ŞİFRƏNİ DƏYİŞ"), UIPalette.CyanLight, UIPalette.Primary, 104f, false, ChangePassword);

            // Sign out and delete.
            RectTransform danger = kit.Card(content, "Account", 40, 18f);
            _dangerCard = danger.gameObject;
            kit.Button(danger, "SignOut", Loc.T("HESABDAN ÇIX"), UIPalette.Surface, UIPalette.TextPrimary, 104f, false, SignOut);
            TextMeshProUGUI deleteLabel;
            kit.Button(danger, "Delete", Loc.T("HESABI SİL"), UIPalette.RedLight, UIPalette.RedDark, 104f, false, out deleteLabel, DeleteAccount);
            _deleteLabel = deleteLabel;

            // The registered e-mail and phone number come first, right under the title.
            emailCard.SetSiblingIndex(_message.transform.GetSiblingIndex() + 1);
            phoneCard.SetSiblingIndex(emailCard.GetSiblingIndex() + 1);
        }
    }
}
