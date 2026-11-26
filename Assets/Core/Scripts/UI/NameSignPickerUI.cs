using System;
using Cevir.Core.Localization;
using System.Collections.Generic;
using Cevir.Core.Stories;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// "Ləqəbin / ən tanınan xüsusiyyətin": a full-screen list of <see cref="NameSignOptions"/> to pick from, used at
    /// sign-up, in account settings and in the stories. Builds its own content at runtime; the component sits on an
    /// always-active layer above the sign-in screen and switches its panel.
    /// </summary>
    public sealed class NameSignPickerUI : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private CevirTheme theme;
        [SerializeField] private Sprite background;
        [SerializeField] private Sprite iconBell;
        [SerializeField] private Sprite iconBack;

        private GameObject _panel;
        private readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>();
        private Action<string> _picked;

        public static NameSignPickerUI Instance { get; private set; }

        public static string Title => Loc.T("Ləqəbin / ən tanınan xüsusiyyətin nədir?");

        private string _current;

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
            bool open = _panel != null && _panel.activeSelf;
            if (_panel != null) Destroy(_panel);
            _buttons.Clear();
            Build();
            _panel.SetActive(open);
            if (open) Select(_current);
        }

        private void Select(string current)
        {
            _current = current;
            string selected = NameSignOptions.Find(current);
            foreach (KeyValuePair<string, Button> pair in _buttons) UIKit.Select(pair.Value, pair.Key == selected);
        }

        /// <summary>Shows the list with <paramref name="current"/> selected; <paramref name="picked"/> gets the chosen word.</summary>
        public void Open(string current, Action<string> picked)
        {
            _picked = picked;
            Select(current);
            _panel.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Close()
        {
            _picked = null;
            _panel.SetActive(false);
        }

        private void Pick(string word)
        {
            Action<string> picked = _picked;
            Close();
            if (picked != null) picked(word);
        }

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
            RectTransform content = kit.Scroll(safe, new RectOffset(60, 60, 40, 100), 26f);

            RectTransform top = UIKit.Rect("Top", content);
            UIKit.Row(top.gameObject, 20f);
            UIKit.Layout(top.gameObject, preferredHeight: 96f);
            kit.IconButton(top, "Back", iconBack, "‹", 96f, UIPalette.TextPrimary, Color.white, Close);
            TextMeshProUGUI heading = kit.Text(top, "Heading", Loc.T("Ləqəb"), 46f, UIPalette.TextPrimary);
            UIKit.Layout(heading.gameObject, flexibleWidth: 1f);

            RectTransform intro = kit.Card(content, "Intro", 40, 14f);
            kit.Text(intro, "Title", Title, 38f, UIPalette.TextPrimary);
            kit.Text(intro, "Body", Loc.T("Karlar bir-birini adla yox, ləqəb və ya ən tanınan xüsusiyyətlə çağırır. Sənə ən uyğun olanı seç: " +
                "hekayələrdə bu işarəni göstərəcəksən."), 30f, UIPalette.TextSecondary, TextAlignmentOptions.TopLeft, false);

            foreach (NameSignOptions.Group group in NameSignOptions.Groups)
            {
                RectTransform card = kit.Card(content, group.Title, 36, 16f);
                kit.Text(card, "Title", Loc.T(group.Title), 34f, UIPalette.TextPrimary);
                RectTransform row = null;
                for (int i = 0; i < group.Words.Count; i++)
                {
                    if (i % 3 == 0)
                    {
                        row = UIKit.Rect("Row" + (i / 3), card);
                        UIKit.Row(row.gameObject, 14f).childForceExpandWidth = true;
                    }

                    string word = group.Words[i];
                    Button button = kit.Button(row, word, NameSignOptions.Display(word), UIPalette.Surface, UIPalette.TextSecondary, 92f, false, () => Pick(word));
                    UIKit.Layout(button.gameObject, flexibleWidth: 1f);
                    _buttons[word] = button;
                }

                // Keep the last row's buttons the same width as the others.
                for (int pad = group.Words.Count % 3; pad != 0 && pad < 3; pad++)
                {
                    RectTransform spacer = UIKit.Rect("Spacer", row);
                    UIKit.Layout(spacer.gameObject, flexibleWidth: 1f);
                }
            }
        }
    }
}
