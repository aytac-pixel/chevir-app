using Cevir.Core.Cloud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Sign in / create account, shown over the whole app while the learner is signed out (only when a server is
    /// configured). Creating an account asks for a name, a name sign (nickname or most recognisable feature: Deaf
    /// people call each other by name signs), e-mail and password; the name is used in the app and on the league table,
    /// the name sign in the stories.
    /// </summary>
    public sealed class AuthScreenUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private GameObject nameRow;
        [SerializeField] private TMP_InputField nameField;
        [SerializeField] private TMP_InputField nameSignField;
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

        private CloudService _cloud;
        private bool _signUp = true;
        private bool _reset; // entering the e-mailed code and a new password
        private bool _busy;

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

        private void Render()
        {
            if (titleLabel != null) titleLabel.text = _reset ? "Yeni şifrə" : _signUp ? UIStrings.SignUpTitle : UIStrings.SignInTitle;
            if (nameRow != null) nameRow.SetActive(_signUp && !_reset);
            if (nameSignField != null) nameSignField.gameObject.SetActive(_signUp && !_reset);
            if (nameSignHint != null) nameSignHint.gameObject.SetActive(_signUp && !_reset);
            if (codeField != null) codeField.gameObject.SetActive(_reset);
            var passwordHint = passwordField != null ? passwordField.placeholder as TextMeshProUGUI : null;
            if (passwordHint != null) passwordHint.text = _reset ? "Yeni şifrə" : "Şifrə";
            if (primaryLabel != null)
            {
                primaryLabel.text = _busy ? UIStrings.PleaseWait
                    : _reset ? "ŞİFRƏNİ YENİLƏ" : (_signUp ? UIStrings.SignUpButton : UIStrings.SignInButton);
            }

            if (switchLabel != null) switchLabel.text = _reset ? "Daxil olmağa qayıt" : _signUp ? UIStrings.SwitchToSignIn : UIStrings.SwitchToSignUp;
            if (forgotButton != null) forgotButton.gameObject.SetActive(!_signUp && !_reset);
            if (primaryButton != null) primaryButton.interactable = !_busy;
        }

        private void Submit()
        {
            if (_busy || _cloud == null) return;
            string name = nameField != null ? nameField.text.Trim() : string.Empty;
            string nameSign = nameSignField != null ? nameSignField.text.Trim() : string.Empty;
            string email = emailField != null ? emailField.text.Trim() : string.Empty;
            string password = passwordField != null ? passwordField.text : string.Empty;

            if (_signUp && name.Length == 0) { Message(UIStrings.NeedName, true); return; }
            if (_signUp && !_reset && nameSignField != null && nameSign.Length == 0) { Message(UIStrings.NeedNameSign, true); return; }
            if (email.Length < 3 || !email.Contains("@")) { Message(UIStrings.NeedEmail, true); return; }
            if (password.Length < 6) { Message(UIStrings.NeedPassword, true); return; }

            SetBusy(true);
            if (_reset)
            {
                string code = codeField != null ? codeField.text.Trim() : string.Empty;
                if (code.Length < 6)
                {
                    SetBusy(false);
                    Message("E-poçtuna gələn kodu yaz.", true);
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
                _cloud.SignUp(name, nameSign, email, password, (error, needsConfirmation) =>
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
