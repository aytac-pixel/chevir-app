using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>Dimmed backdrop + glass card from the bottom: unit preview, story locked info, edit profile.</summary>
    public sealed class BottomSheetUI : MonoBehaviour
    {
        [SerializeField] private RectTransform card;
        [SerializeField] private Image accent;
        [SerializeField] private TextMeshProUGUI kicker;
        [SerializeField] private TextMeshProUGUI title;
        [SerializeField] private TextMeshProUGUI body;
        [SerializeField] private Button primaryButton;
        [SerializeField] private TextMeshProUGUI primaryLabel;
        [SerializeField] private Button secondaryButton;
        [SerializeField] private TextMeshProUGUI secondaryLabel;
        [SerializeField] private Button backdrop;

        [Tooltip("Optional extra content (e.g. the edit-profile form), shown only when requested.")]
        [SerializeField] private GameObject extraContent;

        private Action _primary;
        private Action _secondary;
        private Vector2 _cardHome;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            if (card != null) _cardHome = card.anchoredPosition;
            if (primaryButton != null) primaryButton.onClick.AddListener(() => Fire(_primary));
            if (secondaryButton != null) secondaryButton.onClick.AddListener(() => Fire(_secondary));
            if (backdrop != null) backdrop.onClick.AddListener(Hide);
        }

        public void Show(string kickerText, string titleText, string bodyText, Color accentColor,
            string primary, Action onPrimary, string secondary = null, Action onSecondary = null, bool showExtra = false)
        {
            gameObject.SetActive(true);
            SetText(kicker, kickerText);
            SetText(title, titleText);
            SetText(body, bodyText);
            if (accent != null) accent.color = accentColor;
            if (extraContent != null) extraContent.SetActive(showExtra);

            _primary = onPrimary;
            _secondary = onSecondary;
            Configure(primaryButton, primaryLabel, primary);
            Configure(secondaryButton, secondaryLabel, secondary);

            if (card != null && isActiveAndEnabled)
            {
                StopAllCoroutines();
                StartCoroutine(SlideIn());
            }
        }

        public void Hide()
        {
            _primary = _secondary = null;
            gameObject.SetActive(false);
        }

        private void Fire(Action action)
        {
            Hide();
            if (action != null) action();
        }

        private static void Configure(Button button, TextMeshProUGUI label, string text)
        {
            if (button == null) return;
            button.gameObject.SetActive(!string.IsNullOrEmpty(text));
            if (label != null && text != null) label.text = text;
        }

        private static void SetText(TextMeshProUGUI label, string text)
        {
            if (label == null) return;
            label.text = text ?? string.Empty;
            label.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        private IEnumerator SlideIn()
        {
            const float seconds = 0.2f;
            Vector2 target = _cardHome;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / seconds, 3f);
                card.anchoredPosition = new Vector2(target.x, Mathf.Lerp(target.y - 400f, target.y, k));
                yield return null;
            }

            card.anchoredPosition = target;
        }
    }
}
