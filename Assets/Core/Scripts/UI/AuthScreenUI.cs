using Cevir.Core.Cloud;
using Cevir.Core.Localization;
using Cevir.Core.Stories;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Sign in / create account, shown over the whole app while the learner is signed out (only when a server is
    /// configured). Creating an account asks for a name, a nickname / best-known feature picked from a list of
    /// dictionary signs (Deaf people call each other by such a sign), e-mail and password; the name is used in the app
    /// and on the league table, the nickname sign in the stories.
    /// </summary>
    public sealed class AuthScreenUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private GameObject nameRow;
        [SerializeField] private TMP_InputField nameField;
        [Tooltip("Opens the list of nicknames / best-known features (NameSignPickerUI).")]
        [SerializeField] private Button nameSignButton;
        [SerializeField] private TextMeshProUGUI nameSignLabel;
        [SerializeField] private TextMeshProUGUI nameSignHint;
        [SerializeField] private TMP_InputField emailField;
        [SerializeField] private TMP_InputField passwordField;
        [SerializeField] private TMP_InputField codeField;
        [SerializeField] private Button primaryButton;
        [SerializeField] private TextMeshProUGUI primaryLabel;
        [SerializeField] private Button switchButton;
        [SerializeField] private TextMeshProUGUI switchLabel;
        [SerializeField] private Button forgotButton;
        [SerializeField] private TextMeshProUGUI messageLabel;
        [Tooltip("Interface language buttons in the order of Loc.Supported (AZ, EN, RU).")]
        [SerializeField] private Button[] languageButtons;

        private CloudService _cloud;
        private bool _signUp = true;
        private bool _reset; // entering the e-mailed code and a new password
        private bool _busy;
        private string _nameSign = string.Empty;
        private bool? _dataConsent; // answer to the data permission, asked when "create account" is pressed

        private void Awake()
        {
            if (primaryButton != null) primaryButton.onClick.AddListener(Submit);
            if (switchButton != null) switchButton.onClick.AddListener(() =>
            {
                if (_reset) _reset = false;
                else _signUp = !_signUp;
                Message(null, false);
                Render();
            });
            if (codeField != null)
            {
                codeField.contentType = TMP_InputField.ContentType.IntegerNumber;
                codeField.characterLimit = 10;
            }
            if (forgotButton != null) forgotButton.onClick.AddListener(ForgotPassword);
            if (nameSignButton != null) nameSignButton.onClick.AddListener(PickNameSign);
            if (languageButtons != null)
            {
                for (int i = 0; i < languageButtons.Length && i < Loc.Supported.Length; i++)
                {
                    string code = Loc.Supported[i];
                    if (languageButtons[i] != null) languageButtons[i].onClick.AddListener(() => SetLanguage(code));
                }
            }
            if (passwordField != null)
            {
                passwordField.contentType = TMP_InputField.ContentType.Password;
                passwordField.characterLimit = 72;
            }

            if (emailField != null)
            {
                emailField.contentType = TMP_InputField.ContentType.EmailAddress;
                emailField.characterLimit = 254;
            }
        }

        private void OnEnable()
        {
            Loc.LanguageChanged += OnLanguageChanged;
        }

        private void OnDisable()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(string language)
        {
            if (messageLabel != null) messageLabel.gameObject.SetActive(false);
            Render();
        }

        private void Start()
        {
            _cloud = CloudService.Instance;
            if (_cloud == null)
            {
                Show(false);
                return;
            }

            _cloud.StateChanged += OnStateChanged;
            OnStateChanged(_cloud.State);
        }

        private void OnDestroy()
        {
            if (_cloud != null) _cloud.StateChanged -= OnStateChanged;
        }

        private void OnStateChanged(CloudState state)
        {
            bool show = state == CloudState.SignedOut;
            if (show)
            {
                _busy = false;
                if (passwordField != null) passwordField.text = string.Empty;
                Message(null, false);
                Render();
            }

            Show(show);
        }

        private void Show(bool visible)
        {
            GameObject target = root != null ? root : gameObject;
            if (target.activeSelf != visible) target.SetActive(visible);
        }

        private static void SetLanguage(string code)
        {
            if (Game.GameManager.Instance != null) Game.GameManager.Instance.SetUiLanguage(code);
            else Loc.SetLanguage(code);
        }

        private void Render()
        {
            if (languageButtons != null)
            {
                for (int i = 0; i < languageButtons.Length && i < Loc.Supported.Length; i++) UIKit.Select(languageButtons[i], Loc.Supported[i] == Loc.Language);
            }

            if (titleLabel != null) titleLabel.text = _reset ? Loc.T("Yeni şifrə") : _signUp ? UIStrings.SignUpTitle : UIStrings.SignInTitle;
            if (nameRow != null) nameRow.SetActive(_signUp && !_reset);
            if (nameSignButton != null) nameSignButton.gameObject.SetActive(_signUp && !_reset);
            if (nameSignLabel != null)
            {
                nameSignLabel.text = _nameSign.Length > 0 ? Loc.T("Ləqəbin: ") + NameSignOptions.Display(_nameSign) : Loc.T("Ləqəbin / ən tanınan xüsusiyyətin: seç");
                nameSignLabel.color = _nameSign.Length > 0 ? UIPalette.TextPrimary : UIPalette.TextSecondary;
            }
            if (nameSignHint != null) nameSignHint.gameObject.SetActive(_signUp && !_reset);
            if (codeField != null) codeField.gameObject.SetActive(_reset);
            var passwordHint = passwordField != null ? passwordField.placeholder as TextMeshProUGUI : null;
            if (passwordHint != null) passwordHint.text = _reset ? Loc.T("Yeni şifrə") : Loc.T("Şifrə");
            if (primaryLabel != null)
            {
                primaryLabel.text = _busy ? UIStrings.PleaseWait
                    : _reset ? Loc.T("ŞİFRƏNİ YENİLƏ") : (_signUp ? UIStrings.SignUpButton : UIStrings.SignInButton);
            }

            if (switchLabel != null) switchLabel.text = _reset ? Loc.T("Daxil olmağa qayıt") : _signUp ? UIStrings.SwitchToSignIn : UIStrings.SwitchToSignUp;
            if (forgotButton != null) forgotButton.gameObject.SetActive(!_signUp && !_reset);
            if (primaryButton != null) primaryButton.interactable = !_busy;
        }

        private void Submit()
        {
            if (_busy || _cloud == null) return;
            string name = nameField != null ? nameField.text.Trim() : string.Empty;
            string nameSign = _nameSign;
            string email = emailField != null ? emailField.text.Trim() : string.Empty;
            string password = passwordField != null ? passwordField.text : string.Empty;

            if (_signUp && name.Length == 0) { Message(UIStrings.NeedName, true); return; }
            if (_signUp && !_reset && nameSignButton != null && nameSign.Length == 0) { Message(UIStrings.NeedNameSign, true); return; }
            if (email.Length < 3 || !email.Contains("@")) { Message(UIStrings.NeedEmail, true); return; }
            if (password.Length < 6) { Message(UIStrings.NeedPassword, true); return; }

            // Like the camera permission: ask before the account is created (once; the answer is sent with the sign-up).
            if (_signUp && !_reset && !_dataConsent.HasValue && DataConsentUI.Instance != null)
            {
                DataConsentUI.Instance.Ask(allowed =>
                {
                    _dataConsent = allowed;
                    Submit();
                });
                return;
            }

            SetBusy(true);
            if (_reset)
            {
                string code = codeField != null ? codeField.text.Trim() : string.Empty;
                if (code.Length < 6)
                {
                    SetBusy(false);
                    Message(Loc.T("E-poçtuna gələn kodu yaz."), true);
                    return;
                }

                _cloud.ResetPasswordWithCode(email, code, password, error =>
                {
                    SetBusy(false);
                    if (error != null) Message(error, true);
                    else _reset = false;
                });
                return;
            }

            if (_signUp)
            {
                _cloud.SignUp(name, nameSign, _dataConsent, email, password, (error, needsConfirmation) =>
                {
                    SetBusy(false);
                    if (error != null)
                    {
                        Message(error, true);
                        return;
                    }

                    if (needsConfirmation)
                    {
                        _signUp = false;
                        Render();
                        Message(UIStrings.ConfirmEmailSent, false);
                    }
                });
            }
            else
            {
                _cloud.SignIn(email, password, error =>
                {
                    SetBusy(false);
                    if (error != null) Message(error, true);
                });
            }
        }

        private void PickNameSign()
        {
            if (_busy || NameSignPickerUI.Instance == null) return;
            NameSignPickerUI.Instance.Open(_nameSign, word =>
            {
                _nameSign = word ?? string.Empty;
                Message(null, false);
                Render();
            });
        }

        private void ForgotPassword()
        {
            if (_busy || _cloud == null) return;
            string email = emailField != null ? emailField.text.Trim() : string.Empty;
            if (email.Length < 3 || !email.Contains("@"))
            {
                Message(UIStrings.NeedEmail, true);
                return;
            }

            SetBusy(true);
            _cloud.SendPasswordReset(email, error =>
            {
                SetBusy(false);
                if (error == null)
                {
                    _reset = true;
                    if (codeField != null) codeField.text = string.Empty;
                    if (passwordField != null) passwordField.text = string.Empty;
                    Render();
                }

                Message(error ?? UIStrings.ResetSent, error != null);
            });
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            Render();
        }

        private void Message(string text, bool isError)
        {
            if (messageLabel == null) return;
            messageLabel.text = text ?? string.Empty;
            messageLabel.color = isError ? UIPalette.Red : UIPalette.GreenDark;
            messageLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
    }
}
