using Cevir.Core.Cloud;
using Cevir.Core.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Profile > settings: profile photo, name and signing hand, e-mail, password, reminders, sign out and account
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
        private TMP_InputField _newPassword;
        private TMP_InputField _repeatPassword;
        private TextMeshProUGUI _remindersStatus;
        private GameObject _dangerCard;
        private TextMeshProUGUI _deleteLabel;
        private TextMeshProUGUI _message;
        private bool _deleteArmed;
        private bool _busy;
        private Texture2D _photoTexture;

        public static AccountSettingsUI Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Build();
            _panel.SetActive(false);
            AvatarStore.Changed += ShowLocalPhoto;
        }

        private void OnDestroy()
        {
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
            CloudService cloud = CloudService.Instance;
            bool signedIn = cloud != null && cloud.State == CloudState.SignedIn;
            _emailCard.SetActive(signedIn);
            _passwordCard.SetActive(signedIn);
            _dangerCard.SetActive(signedIn);
            _emailCurrent.text = signedIn ? "Hazırkı: <b>" + cloud.Email + "</b>" : string.Empty;
            _deleteLabel.text = _deleteArmed ? "TƏSDİQLƏ: HESABI SİL" : "HESABI SİL";
            UIKit.Select(_rightHand, !_leftHanded);
            UIKit.Select(_leftHand, _leftHanded);
            ReminderRefresh();
        }

        private void ReminderRefresh()
        {
            GameManager game = GameManager.Instance;
            if (_remindersStatus == null || game == null || game.Progress == null) return;
            ReminderSettings r = game.Progress.reminders;
            _remindersStatus.text = r != null && r.enabled && r.configured ? "Açıq · " + r.WindowText : "Bağlı";
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
                    Message("Şəkil açıla bilmədi. Başqa şəkil seç.", true);
                    return;
                }

                byte[] jpeg = AvatarStore.PrepareJpeg(picked);
                Destroy(picked);
                UploadPhoto(jpeg);
            }, "Profil şəkli seç", "image/*");
#else
            Message("Şəkil seçmək telefonda işləyir.", true);
#endif
        }

        private void UploadPhoto(byte[] jpeg)
        {
            if (jpeg == null) return;
            AvatarStore.Save(jpeg, null);
            CloudService cloud = CloudService.Instance;
            if (cloud == null || cloud.State != CloudState.SignedIn)
            {
                Message("Şəkil yadda saxlandı.", false);
                return;
            }

            SetBusy(true);
            Message("Şəkil yüklənir...", false);
            cloud.UploadAvatar(jpeg, error =>
            {
                SetBusy(false);
                if (error == null)
                {
                    AvatarStore.Save(jpeg, GameManager.Instance != null ? GameManager.Instance.Progress.avatarUrl : null);
                    Message("Profil şəkli yeniləndi.", false);
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
                Message(error ?? "Profil şəkli silindi.", error != null);
            });
        }

        // ------------------------------------------------------------------ details

        private void SaveDetails()
        {
            GameManager game = GameManager.Instance;
            if (game == null) return;
            if (_firstName.text.Trim().Length == 0)
            {
                Message("Adını yaz.", true);
                return;
            }

            game.SetLearnerName(_firstName.text, _lastName.text);
            if (_nameSign != game.Progress.nameSign) game.SetNameSign(_nameSign);
            if (_leftHanded != game.Progress.leftHanded) game.SetLeftHanded(_leftHanded);
            Message("Məlumatlar yadda saxlandı.", false);
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
            if (_nameSignLabel != null) _nameSignLabel.text = _nameSign.Length > 0 ? _nameSign : "Seç";
        }

        private void ChangeEmail()
        {
            CloudService cloud = CloudService.Instance;
            string email = _newEmail.text.Trim();
            if (_busy || cloud == null) return;
            if (email.Length < 3 || !email.Contains("@") || !email.Contains("."))
            {
                Message("Yeni e-poçt ünvanını düzgün yaz.", true);
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
                    ? "Təsdiq linki " + email + " ünvanına göndərildi. Linki açandan sonra e-poçt dəyişəcək."
                    : "E-poçt dəyişdirildi.", false);
                Render();
            });
        }

        private void ChangePassword()
        {
            CloudService cloud = CloudService.Instance;
            if (_busy || cloud == null) return;
            if (_newPassword.text.Length < 6)
            {
                Message("Şifrə ən azı 6 simvol olmalıdır.", true);
                return;
            }

            if (_newPassword.text != _repeatPassword.text)
            {
                Message("Şifrələr eyni deyil.", true);
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

                Message(error ?? "Şifrə dəyişdirildi.", error != null);
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
                Message("Hesabın və bütün irəliləyişin həmişəlik silinəcək. Əminsənsə, bir daha bas.", true);
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

            RectTransform safe = UIKit.Rect("SafeArea", panel);
            UIKit.Stretch(safe);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            RectTransform content = kit.Scroll(safe, new RectOffset(60, 60, 40, 100), 30f);

            RectTransform top = UIKit.Rect("Top", content);
            UIKit.Row(top.gameObject, 20f);
            UIKit.Layout(top.gameObject, preferredHeight: 96f);
            kit.IconButton(top, "Back", iconBack, "‹", 96f, UIPalette.TextPrimary, Color.white, Close);
            TextMeshProUGUI heading = kit.Text(top, "Heading", "Hesab ayarları", 46f, UIPalette.TextPrimary);
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
            Button pick = kit.Button(photoButtons, "Pick", "ŞƏKLİ DƏYİŞ", Color.white, Color.white, 104f, true, PickPhoto);
            UIKit.Layout(pick.gameObject, flexibleWidth: 1f);
            Button remove = kit.Button(photoButtons, "Remove", "SİL", UIPalette.RedLight, UIPalette.RedDark, 104f, false, RemovePhoto);
            UIKit.Layout(remove.gameObject, flexibleWidth: 0.5f);
            _removePhoto = remove.gameObject;

            // Name and signing hand.
            RectTransform details = kit.Card(content, "Details", 40, 20f);
            kit.Text(details, "Title", "Şəxsi məlumatlar", 36f, UIPalette.TextPrimary);
            _firstName = kit.Input(details, "FirstName", "Ad", TMP_InputField.ContentType.Name, 30);
            _lastName = kit.Input(details, "LastName", "Soyad", TMP_InputField.ContentType.Name, 30);
            kit.Text(details, "NameSignTitle", "Ləqəbin / ən tanınan xüsusiyyətin", 30f, UIPalette.TextSecondary);
            Button nameSign = kit.Button(details, "NameSign", "", UIPalette.Surface, UIPalette.TextPrimary, 92f, false, out _nameSignLabel, PickNameSign);
            UIKit.Layout(nameSign.gameObject, flexibleWidth: 1f);
            kit.Text(details, "HandTitle", "İşarə əli", 30f, UIPalette.TextSecondary);
            RectTransform hands = UIKit.Rect("Hands", details);
            UIKit.Row(hands.gameObject, 16f).childForceExpandWidth = true;
            _rightHand = kit.Button(hands, "Right", "Sağ", UIPalette.Surface, UIPalette.TextSecondary, 92f, false, () => { _leftHanded = false; Render(); });
            UIKit.Layout(_rightHand.gameObject, flexibleWidth: 1f);
            _leftHand = kit.Button(hands, "Left", "Sol", UIPalette.Surface, UIPalette.TextSecondary, 92f, false, () => { _leftHanded = true; Render(); });
            UIKit.Layout(_leftHand.gameObject, flexibleWidth: 1f);
            kit.Button(details, "SaveDetails", "YADDA SAXLA", Color.white, Color.white, 112f, true, SaveDetails);

            // Reminders.
            RectTransform reminders = kit.Card(content, "Reminders", 36, 10f);
            RectTransform reminderRow = UIKit.Rect("Row", reminders);
            UIKit.Row(reminderRow.gameObject, 22f);
            Image bellTile = kit.Image(reminderRow, "BellTile", theme != null ? theme.panel : null, UIPalette.CyanLight, true);
            UIKit.Layout(bellTile.gameObject, preferredWidth: 96f, preferredHeight: 96f, minWidth: 96f);
            Image bell = kit.Image(bellTile.transform, "Bell", iconBell, UIPalette.Primary, false);
            bell.rectTransform.anchorMin = bell.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            bell.rectTransform.sizeDelta = new Vector2(54f, 54f);
            RectTransform reminderTexts = UIKit.Rect("Texts", reminderRow);
            UIKit.Column(reminderTexts.gameObject, 0, 4f, TextAnchor.MiddleLeft);
            UIKit.Layout(reminderTexts.gameObject, flexibleWidth: 1f);
            kit.Text(reminderTexts, "Title", "Bildirişlər", 36f, UIPalette.TextPrimary);
            _remindersStatus = kit.Text(reminderTexts, "Status", "", 28f, UIPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, false);
            Button reminderButton = kit.Button(reminderRow, "Open", "AYARLA", UIPalette.CyanLight, UIPalette.Primary, 84f, false, OpenReminders);
            UIKit.Layout(reminderButton.gameObject, preferredWidth: 210f, minWidth: 180f);

            // E-mail.
            RectTransform emailCard = kit.Card(content, "Email", 40, 18f);
            _emailCard = emailCard.gameObject;
            kit.Text(emailCard, "Title", "E-poçt", 36f, UIPalette.TextPrimary);
            _emailCurrent = kit.Text(emailCard, "Current", "", 30f, UIPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, false);
            _emailCurrent.enableWordWrapping = false;
            _emailCurrent.overflowMode = TextOverflowModes.Ellipsis;
            _newEmail = kit.Input(emailCard, "NewEmail", "Yeni e-poçt", TMP_InputField.ContentType.EmailAddress, 254);
            kit.Button(emailCard, "ChangeEmail", "E-POÇTU DƏYİŞ", UIPalette.CyanLight, UIPalette.Primary, 104f, false, ChangeEmail);

            // Password.
            RectTransform passwordCard = kit.Card(content, "Password", 40, 18f);
            _passwordCard = passwordCard.gameObject;
            kit.Text(passwordCard, "Title", "Şifrəni dəyiş", 36f, UIPalette.TextPrimary);
            _newPassword = kit.Input(passwordCard, "NewPassword", "Yeni şifrə (ən azı 6 simvol)", TMP_InputField.ContentType.Password, 72);
            _repeatPassword = kit.Input(passwordCard, "RepeatPassword", "Yeni şifrəni təkrar yaz", TMP_InputField.ContentType.Password, 72);
            kit.Button(passwordCard, "ChangePassword", "ŞİFRƏNİ DƏYİŞ", UIPalette.CyanLight, UIPalette.Primary, 104f, false, ChangePassword);

            // Sign out and delete.
            RectTransform danger = kit.Card(content, "Account", 40, 18f);
            _dangerCard = danger.gameObject;
            kit.Button(danger, "SignOut", "HESABDAN ÇIX", UIPalette.Surface, UIPalette.TextPrimary, 104f, false, SignOut);
            TextMeshProUGUI deleteLabel;
            kit.Button(danger, "Delete", "HESABI SİL", UIPalette.RedLight, UIPalette.RedDark, 104f, false, out deleteLabel, DeleteAccount);
            _deleteLabel = deleteLabel;
        }
    }
}
