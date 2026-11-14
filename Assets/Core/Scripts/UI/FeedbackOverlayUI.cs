using System;
using System.Collections;
using Cevir.Core.Game;
using Cevir.Core.Lesson;
using Cevir.Core.Recognition;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Lesson overlays driven by the decision layer:
    /// a bottom sheet after every attempt (green = correct, amber = tracking/unsure, red = mistake) with
    /// Continue or Retry, a heart-loss animation, and full-screen modals for lesson results and out-of-hearts.
    /// Colour is never the only signal: every state also has its own title text.
    /// </summary>
    public sealed class FeedbackOverlayUI : MonoBehaviour
    {
        [Header("Bottom sheet")]
        [SerializeField] private RectTransform sheet;
        [SerializeField] private Image sheetBackground;
        [SerializeField] private TextMeshProUGUI sheetTitle;
        [SerializeField] private TextMeshProUGUI sheetMessage;
        [SerializeField] private Button sheetPrimaryButton;
        [SerializeField] private TextMeshProUGUI sheetPrimaryLabel;
        [SerializeField] private Button sheetSecondaryButton;
        [SerializeField] private TextMeshProUGUI sheetSecondaryLabel;

        [Header("Modal")]
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private RectTransform modalCard;
        [SerializeField] private TextMeshProUGUI modalTitle;
        [SerializeField] private TextMeshProUGUI modalBody;
        [SerializeField] private TextMeshProUGUI modalStats;
        [SerializeField] private Button modalPrimaryButton;
        [SerializeField] private TextMeshProUGUI modalPrimaryLabel;
        [SerializeField] private Button modalSecondaryButton;
        [SerializeField] private TextMeshProUGUI modalSecondaryLabel;

        [Header("Heart loss")]
        [SerializeField] private RectTransform heartIcon;
        [SerializeField] private TextMeshProUGUI heartLossLabel;

        [Header("Timing")]
        [SerializeField] private float sheetSlideSeconds = 0.18f;
        [SerializeField] private float modalPopSeconds = 0.22f;

        private Action _primary;
        private Action _secondary;
        private Action _modalPrimary;
        private Action _modalSecondary;
        private Coroutine _sheetRoutine;
        private Coroutine _heartRoutine;

        public bool IsShowing => (sheet != null && sheet.gameObject.activeSelf) || (modalRoot != null && modalRoot.activeSelf);

        private void Awake()
        {
            if (sheetPrimaryButton != null) sheetPrimaryButton.onClick.AddListener(() => Invoke(ref _primary, true));
            if (sheetSecondaryButton != null) sheetSecondaryButton.onClick.AddListener(() => Invoke(ref _secondary, true));
            if (modalPrimaryButton != null) modalPrimaryButton.onClick.AddListener(() => InvokeModal(ref _modalPrimary));
            if (modalSecondaryButton != null) modalSecondaryButton.onClick.AddListener(() => InvokeModal(ref _modalSecondary));
            HideAll();
        }

        public void HideAll()
        {
            if (sheet != null) sheet.gameObject.SetActive(false);
            if (modalRoot != null) modalRoot.SetActive(false);
            if (heartLossLabel != null) heartLossLabel.gameObject.SetActive(false);
            _primary = _secondary = _modalPrimary = _modalSecondary = null;
        }

        // ------------------------------------------------------------------ attempts

        /// <summary>Feedback after a camera attempt. Retry is offered while the exercise is still current.</summary>
        public void ShowCameraAttempt(AttemptOutcome outcome, MatchResult result, string uiLanguage, Action onContinue, Action onRetry)
        {
            string message = FeedbackMessages.Get(result.Feedback, uiLanguage);

            if (outcome.Passed)
            {
                string extra = result.IsPerfect ? "\n" + UIStrings.Perfect : string.Empty;
                ShowSheet(FeedbackSeverity.None, UIStrings.CorrectTitle, message + extra, UIStrings.Continue, onContinue, null, null);
            }
            else if (outcome.Advance)
            {
                // Third soft failure: a heart was charged and the exercise is retried later in the lesson.
                ShowSheet(FeedbackSeverity.Soft, UIStrings.WrongTitle, message + "\n" + UIStrings.ThirdFailRequeue,
                    UIStrings.Continue, onContinue, null, null);
            }
            else
            {
                string title = result.Severity == FeedbackSeverity.Neutral ? UIStrings.TryAgainTitle : UIStrings.WrongTitle;
                ShowSheet(result.Severity, title, message, UIStrings.Retry, onRetry, null, null);
            }

            if (outcome.HeartLost) PlayHeartLost();
        }

        public void ShowChoiceResult(bool correct, string correctAnswer, bool heartLost, Action onContinue)
        {
            if (correct)
            {
                ShowSheet(FeedbackSeverity.None, UIStrings.CorrectTitle, FeedbackMessages.Get(SignFeedback.Correct, Localization.Loc.Language),
                    UIStrings.Continue, onContinue, null, null);
            }
            else
            {
                ShowSheet(FeedbackSeverity.Soft, UIStrings.WrongTitle, UIStrings.CorrectAnswerIs + correctAnswer,
                    UIStrings.Continue, onContinue, null, null);
            }

            if (heartLost) PlayHeartLost();
        }

        public void ShowSheet(FeedbackSeverity severity, string title, string message,
            string primaryLabel, Action onPrimary, string secondaryLabel, Action onSecondary)
        {
            if (sheet == null) return;

            Color background, accent, dark;
            Palette(severity, out background, out accent, out dark);

            if (sheetBackground != null) sheetBackground.color = background;
            if (sheetTitle != null)
            {
                sheetTitle.text = title;
                sheetTitle.color = dark;
            }

            if (sheetMessage != null)
            {
                sheetMessage.text = message;
                sheetMessage.color = dark;
            }

            _primary = onPrimary;
            _secondary = onSecondary;
            ConfigureButton(sheetPrimaryButton, sheetPrimaryLabel, primaryLabel, accent, Color.white);
            ConfigureButton(sheetSecondaryButton, sheetSecondaryLabel, secondaryLabel, Color.white, dark);

            sheet.gameObject.SetActive(true);
            if (_sheetRoutine != null) StopCoroutine(_sheetRoutine);
            _sheetRoutine = StartCoroutine(SlideIn(sheet));
        }

        public void HideSheet()
        {
            if (sheet != null) sheet.gameObject.SetActive(false);
            _primary = _secondary = null;
        }

        // ------------------------------------------------------------------ modals

        public void ShowLessonComplete(LessonSession session, LessonReward reward, Action onContinue)
        {
            HideSheet();
            string title;
            if (session.Kind == SessionKind.GateTest) title = reward.Passed ? UIStrings.GatePassed : UIStrings.GateFailed;
            else title = UIStrings.LessonComplete;

            var stats = new System.Text.StringBuilder();
            if (reward.Xp > 0) stats.AppendLine(string.Format(UIStrings.XpGained, reward.Xp));
            stats.AppendLine(string.Format(UIStrings.Accuracy, Mathf.RoundToInt(reward.Accuracy * 100f)));
            if (reward.PerfectBonus) stats.AppendLine(UIStrings.Perfect);
            if (reward.HeartsRestored > 0) stats.AppendLine(string.Format(UIStrings.HeartsRestored, reward.HeartsRestored));
            if (reward.StreakIncreased && GameManager.Instance != null)
                stats.AppendLine(string.Format(UIStrings.StreakUp, GameManager.Instance.Streak.State.current));

            ShowModal(title, null, stats.ToString().TrimEnd(), UIStrings.Continue, onContinue, null, null);
        }

        public void ShowOutOfHearts(Action onBackToMap)
        {
            HideSheet();
            ShowModal(UIStrings.OutOfHeartsTitle, UIStrings.OutOfHeartsBody, null, UIStrings.BackToMap, onBackToMap, null, null);
        }

        public void ShowModal(string title, string body, string stats, string primaryLabel, Action onPrimary,
            string secondaryLabel, Action onSecondary)
        {
            if (modalRoot == null) return;
            if (modalTitle != null) modalTitle.text = title;
            SetOptionalText(modalBody, body);
            SetOptionalText(modalStats, stats);

            _modalPrimary = onPrimary;
            _modalSecondary = onSecondary;
            ConfigureButton(modalPrimaryButton, modalPrimaryLabel, primaryLabel, UIPalette.Green, Color.white);
            ConfigureButton(modalSecondaryButton, modalSecondaryLabel, secondaryLabel, Color.white, UIPalette.TextSecondary);

            modalRoot.SetActive(true);
            if (modalCard != null) StartCoroutine(Pop(modalCard));
        }

        // ------------------------------------------------------------------ heart animation

        public void SetHeartIcon(RectTransform icon)
        {
            heartIcon = icon;
        }

        public void PlayHeartLost()
        {
            if (_heartRoutine != null) StopCoroutine(_heartRoutine);
            _heartRoutine = StartCoroutine(HeartLostRoutine());
        }

        private IEnumerator HeartLostRoutine()
        {
            Graphic heartGraphic = heartIcon != null ? heartIcon.GetComponent<Graphic>() : null;
            Color baseColor = heartGraphic != null ? heartGraphic.color : Color.white;

            Vector2 labelStart = Vector2.zero;
            if (heartLossLabel != null)
            {
                heartLossLabel.gameObject.SetActive(true);
                heartLossLabel.text = "-1";
                labelStart = heartLossLabel.rectTransform.anchoredPosition;
            }

            const float duration = 0.7f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                if (heartIcon != null)
                {
                    // Punch: grow fast, shrink back with a small overshoot.
                    float punch = 1f + 0.45f * Mathf.Sin(Mathf.Min(k * 2.5f, 1f) * Mathf.PI) * (1f - k);
                    heartIcon.localScale = new Vector3(punch, punch, 1f);
                    heartIcon.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(k * Mathf.PI * 6f) * 12f * (1f - k));
                }

                if (heartGraphic != null) heartGraphic.color = Color.Lerp(Color.gray, baseColor, k);

                if (heartLossLabel != null)
                {
                    heartLossLabel.rectTransform.anchoredPosition = labelStart + new Vector2(0f, 60f * k);
                    heartLossLabel.alpha = 1f - k;
                }

                yield return null;
            }

            if (heartIcon != null)
            {
                heartIcon.localScale = Vector3.one;
                heartIcon.localEulerAngles = Vector3.zero;
            }

            if (heartGraphic != null) heartGraphic.color = baseColor;
            if (heartLossLabel != null)
            {
                heartLossLabel.rectTransform.anchoredPosition = labelStart;
                heartLossLabel.gameObject.SetActive(false);
            }

            _heartRoutine = null;
        }

        // ------------------------------------------------------------------ helpers

        private static void Palette(FeedbackSeverity severity, out Color background, out Color accent, out Color dark)
        {
            switch (severity)
            {
                case FeedbackSeverity.None:
                    background = UIPalette.GreenLight;
                    accent = UIPalette.Green;
                    dark = UIPalette.GreenDark;
                    break;
                case FeedbackSeverity.Neutral:
                    background = UIPalette.AmberLight;
                    accent = UIPalette.Amber;
                    dark = UIPalette.AmberDark;
                    break;
                default:
                    background = UIPalette.RedLight;
                    accent = UIPalette.Red;
                    dark = UIPalette.RedDark;
                    break;
            }
        }

        private static void ConfigureButton(Button button, TextMeshProUGUI label, string text, Color fill, Color textColor)
        {
            if (button == null) return;
            bool visible = !string.IsNullOrEmpty(text);
            button.gameObject.SetActive(visible);
            if (!visible) return;
            Image image = button.targetGraphic as Image;
            if (image != null) image.color = fill;
            if (label != null)
            {
                label.text = text;
                label.color = textColor;
            }
        }

        private static void SetOptionalText(TextMeshProUGUI label, string text)
        {
            if (label == null) return;
            label.text = text ?? string.Empty;
            label.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        private void Invoke(ref Action action, bool hideSheet)
        {
            Action callback = action;
            if (hideSheet) HideSheet();
            if (callback != null) callback();
        }

        private void InvokeModal(ref Action action)
        {
            Action callback = action;
            if (modalRoot != null) modalRoot.SetActive(false);
            _modalPrimary = _modalSecondary = null;
            if (callback != null) callback();
        }

        private IEnumerator SlideIn(RectTransform panel)
        {
            float height = panel.rect.height;
            Vector2 target = new Vector2(panel.anchoredPosition.x, 0f);
            for (float t = 0f; t < sheetSlideSeconds; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / sheetSlideSeconds, 3f);
                panel.anchoredPosition = new Vector2(target.x, Mathf.Lerp(-height, 0f, k));
                yield return null;
            }

            panel.anchoredPosition = target;
            _sheetRoutine = null;
        }

        private IEnumerator Pop(RectTransform card)
        {
            for (float t = 0f; t < modalPopSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / modalPopSeconds;
                float s = Mathf.Lerp(0.85f, 1f, 1f - Mathf.Pow(1f - k, 3f));
                card.localScale = new Vector3(s, s, 1f);
                yield return null;
            }

            card.localScale = Vector3.one;
        }
    }
}
