using System;
using Cevir.Core.Localization;
using Cevir.Core.Cloud;
using Cevir.Core.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Permission dialog «Məlumatlarımla Chevir-i inkişaf etdirməyə kömək et», shown like the phone's camera permission:
    /// at sign-up (before the account is created), and once for any signed-in learner who has not answered yet. The
    /// answer can be changed in account settings. Builds its own content at runtime; sits on the top layer.
    /// </summary>
    public sealed class DataConsentUI : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private CevirTheme theme;
        [SerializeField] private Sprite background;
        [SerializeField] private Sprite iconBell;
        [SerializeField] private Sprite iconBack;
        [SerializeField] private Sprite iconCamera;

        public static string Title => Loc.T("Məlumatlarımla Chevir I/O-nu inkişaf etdirməyə kömək et");

        public static string Body => Loc.T(
            "Kamera qarşısında işarə göstərəndə Chevir I/O bu cəhdi saxlaya bilər: əlinin, üzünün və bədəninin nöqtələri və " +
            "3 saniyəlik kiçik video. Bunlarla Chevir I/O işarə dilini daha yaxşı tanımağı öyrənir. Məlumatlar yalnız Chevir I/O " +
            "komandasında qalır, heç kimə göstərilmir. İstədiyin vaxt Hesab ayarlarından söndürə bilərsən.");

        private GameObject _panel;
        private Action<bool> _answered;
        private bool _autoChecked;
        private float _nextCheck;

        public static DataConsentUI Instance { get; private set; }

        public bool IsOpen => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            Instance = this;
            Build();
            _panel.SetActive(false);
            Loc.LanguageChanged += Rebuild;
        }

        private void OnDestroy()
        {
            Loc.LanguageChanged -= Rebuild;
            if (Instance == this) Instance = null;
        }

        /// <summary>Content is built once in the interface language; a new language builds it again.</summary>
        private void Rebuild(string language)
        {
            bool open = IsOpen;
            if (_panel != null) Destroy(_panel);
            Build();
            _panel.SetActive(open);
        }

        private void Update()
        {
            // Learners who signed up before this question existed (or on another phone) are asked once.
            if (_autoChecked || IsOpen || Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 1f;
            GameManager game = GameManager.Instance;
            CloudService cloud = CloudService.Instance;
            if (game == null || game.Progress == null || game.State != GameState.Map) return;
            if (cloud == null || cloud.State != CloudState.SignedIn || !cloud.Synced) return;

            _autoChecked = true;
            if (!game.Progress.dataConsentAsked)
            {
                Ask(allowed => cloud.SetDataConsent(allowed, error =>
                {
                    if (CevirUIRoot.Instance != null && error == null)
                    {
                        CevirUIRoot.Instance.ShowToast(allowed ? Loc.T("Təşəkkürlər! Chevir I/O-nu birlikdə inkişaf etdiririk") : Loc.T("Yadda saxlandı"));
                    }
                }));
            }
        }

        /// <summary>Shows the dialog; <paramref name="answered"/> gets true for «İcazə ver», false for «İcazə vermə».</summary>
        public void Ask(Action<bool> answered)
        {
            _answered = answered;
            _panel.SetActive(true);
            transform.SetAsLastSibling();
        }

        private void Answer(bool allowed)
        {
            Action<bool> answered = _answered;
            _answered = null;
            _panel.SetActive(false);
            if (answered != null) answered(allowed);
        }

        private void Build()
        {
            var kit = new UIKit(font, theme);
            RectTransform panel = UIKit.Rect("Panel", transform);
            UIKit.Stretch(panel);
            Image dim = panel.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.62f);
            dim.raycastTarget = true; // blocks the screen below until the learner answers
            _panel = panel.gameObject;

            RectTransform card = kit.Card(panel, "Dialog", 56, 30f);
            card.anchorMin = new Vector2(0.06f, 0.5f);
            card.anchorMax = new Vector2(0.94f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.anchoredPosition = Vector2.zero;
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            VerticalLayoutGroup column = card.GetComponent<VerticalLayoutGroup>();
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlHeight = true;
            column.childForceExpandHeight = false;

            Sprite icon = iconCamera != null ? iconCamera : iconBell;
            Image tile = kit.Image(card, "IconTile", theme != null ? theme.circle : null, UIPalette.CyanLight, false);
            UIKit.Layout(tile.gameObject, preferredWidth: 150f, preferredHeight: 150f);
            tile.preserveAspect = true;
            if (icon != null)
            {
                Image glyph = kit.Image(tile.transform, "Icon", icon, UIPalette.Primary, false);
                glyph.rectTransform.anchorMin = glyph.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                glyph.rectTransform.sizeDelta = new Vector2(80f, 80f);
            }

            kit.Text(card, "Title", Title, 40f, UIPalette.TextPrimary, TextAlignmentOptions.Center);
            kit.Text(card, "Body", Body, 30f, UIPalette.TextSecondary, TextAlignmentOptions.Top, false);

            Button allow = kit.Button(card, "Allow", Loc.T("İCAZƏ VER"), Color.white, Color.white, 112f, true, () => Answer(true));
            UIKit.Layout(allow.gameObject, flexibleWidth: 1f);
            Button deny = kit.Button(card, "Deny", Loc.T("İCAZƏ VERMƏ"), UIPalette.Surface, UIPalette.TextPrimary, 104f, false, () => Answer(false));
            UIKit.Layout(deny.gameObject, flexibleWidth: 1f);
        }
    }
}
