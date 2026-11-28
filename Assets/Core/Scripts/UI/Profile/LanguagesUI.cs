using Cevir.Core.Game;
using Cevir.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Profile > Languages: the interface language in use and the other ones; a tap switches every text in the app
    /// (lessons, stories, notifications) at once.
    /// </summary>
    public sealed class LanguagesUI : ProfilePanelBase
    {
        [SerializeField] private Sprite iconCheck;

        private TextMeshProUGUI _current;
        private Image[] _rows = new Image[0];
        private Image[] _checks = new Image[0];

        public static LanguagesUI Instance { get; private set; }

        protected override string Title => Loc.Pick("Languages", "Dillər", "Языки");

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
            RectTransform card = kit.Card(content, "Languages", 40, 18f);
            _current = kit.Text(card, "Current", "", 32f, UIPalette.TextPrimary, TextAlignmentOptions.MidlineLeft, false);
            _current.richText = true;
            kit.Text(card, "Hint", Loc.Pick("Lessons, stories and notifications change too.", "Dərslər, hekayələr və bildirişlər də dəyişir.",
                "Уроки, истории и уведомления тоже сменятся."), 27f, UIPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, false);

            _rows = new Image[Loc.Supported.Length];
            _checks = new Image[Loc.Supported.Length];
            for (int i = 0; i < Loc.Supported.Length; i++)
            {
                string code = Loc.Supported[i];
                RectTransform row = UIKit.Rect("Language_" + code, card);
                Image bg = row.gameObject.AddComponent<Image>();
                bg.sprite = theme != null ? theme.panel : null;
                bg.type = Image.Type.Sliced;
                bg.pixelsPerUnitMultiplier = 1.4f;
                bg.color = UIPalette.Surface;
                Button button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = bg;
                button.onClick.AddListener(() => Choose(code));
                HorizontalLayoutGroup layout = UIKit.Row(row.gameObject, 24f);
                layout.padding = new RectOffset(24, 30, 16, 16);
                UIKit.Layout(row.gameObject, preferredHeight: 124f, minHeight: 124f);

                Image tile = kit.Image(row, "Code", theme != null ? theme.panel : null, Color.white, true);
                UIKit.Layout(tile.gameObject, preferredWidth: 92f, preferredHeight: 92f, minWidth: 92f);
                TextMeshProUGUI codeLabel = kit.Text(tile.transform, "Label", code.ToUpperInvariant(), 32f, UIPalette.Primary, TextAlignmentOptions.Center);
                UIKit.Stretch(codeLabel.rectTransform);

                TextMeshProUGUI name = kit.Text(row, "Name", Loc.NativeName(code), 36f, UIPalette.TextPrimary);
                UIKit.Layout(name.gameObject, flexibleWidth: 1f);
                _checks[i] = kit.Icon(row, "Check", iconCheck, 52f, UIPalette.GreenDark);
                _rows[i] = bg;
            }
        }

        private static void Choose(string code)
        {
            if (code == Loc.Language) return;
            if (GameManager.Instance != null) GameManager.Instance.SetUiLanguage(code);
            else Loc.SetLanguage(code);
        }

        protected override void Render()
        {
            if (_current == null) return;
            _current.text = Loc.Pick("Language in use: ", "Hazırkı dil: ", "Сейчас: ") + "<b>" + Loc.NativeName(Loc.Language) + "</b>";
            for (int i = 0; i < _rows.Length; i++)
            {
                bool on = Loc.Supported[i] == Loc.Language;
                _rows[i].color = on ? UIPalette.CyanLight : UIPalette.Surface;
                _checks[i].enabled = on;
            }
        }
    }
}
