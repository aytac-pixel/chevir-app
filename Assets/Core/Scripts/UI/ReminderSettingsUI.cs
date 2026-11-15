using System.Collections.Generic;
using Cevir.Core.Cloud;
using Cevir.Core.Game;
using Cevir.Core.Notifications;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// "When can you practise?": the daily time window for reminders. Opens by itself once after sign-up (and for any
    /// learner who has not answered yet), and from Profile > Bildirişlər, where reminders can also be turned off.
    /// Builds its own content at runtime; the component sits on an always-active layer and switches its panel.
    /// </summary>
    public sealed class ReminderSettingsUI : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private CevirTheme theme;
        [SerializeField] private Sprite background;
        [SerializeField] private Sprite iconBell;
        [SerializeField] private Sprite iconBack;

        private static readonly int[][] Presets =
        {
            new[] { 8 * 60, 10 * 60 }, new[] { 12 * 60, 14 * 60 }, new[] { 18 * 60, 20 * 60 }, new[] { 21 * 60, 23 * 60 }
        };

        private static readonly string[] PresetNames = { "Səhər", "Günorta", "Axşam", "Gecə" };

        private GameObject _panel;
        private GameObject _backButton;
        private GameObject _toggleRow;
        private GameObject _laterButton;
        private Button _toggleButton;
        private TextMeshProUGUI _toggleLabel;
        private TextMeshProUGUI _startLabel;
        private TextMeshProUGUI _endLabel;
        private TextMeshProUGUI _summary;
        private TextMeshProUGUI _hint;
        private GameObject _timeSection;
        private readonly List<Button> _presetButtons = new List<Button>();

        private readonly ReminderSettings _draft = new ReminderSettings();
        private bool _onboarding;
        private bool _autoChecked;
        private float _nextCheck;

        public static ReminderSettingsUI Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Build();
            _panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Ask once, after sign-up / first launch, as soon as the learner is in the app.
            if (_autoChecked || _panel.activeSelf || Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 1f;
            GameManager game = GameManager.Instance;
            if (game == null || game.Progress == null || game.State != GameState.Map) return;

            CloudService cloud = CloudService.Instance;
            bool ready = cloud == null || cloud.State == CloudState.Disabled || (cloud.State == CloudState.SignedIn && cloud.Synced);
            if (!ready) return;

            _autoChecked = true;
            if (game.Progress.reminders == null || !game.Progress.reminders.configured) Open(true);
        }

        /// <summary>Opens the panel. Onboarding mode (after sign-up) hides the back button and offers "not now".</summary>
        public void Open(bool onboarding)
        {
            GameManager game = GameManager.Instance;
            if (game == null) return;
            _onboarding = onboarding;
            ReminderSettings current = game.Progress.reminders ?? new ReminderSettings();
            _draft.enabled = onboarding || current.enabled;
            _draft.startMinutes = current.startMinutes;
            _draft.endMinutes = current.endMinutes;
            _draft.Normalize();
            _hint.gameObject.SetActive(false);
            _panel.SetActive(true);
            Render();
        }

        public void Close()
        {
            _panel.SetActive(false);
        }

        // ------------------------------------------------------------------ actions

        private void Save()
        {
            ReminderService service = ReminderService.Instance;
            if (service == null)
            {
                Close();
                return;
            }

            service.Apply(_draft, granted =>
            {
                if (!granted && _draft.enabled)
                {
                    _hint.text = "Bildirişlərə icazə verilmədi. Telefonun Ayarlar > Tətbiqlər > Chevir > Bildirişlər bölməsində aç.";
                    _hint.gameObject.SetActive(true);
                    if (CevirUIRoot.Instance != null) CevirUIRoot.Instance.ShowToast("Bildirişlərə icazə verilmədi");
                    return;
                }

                Close();
                if (CevirUIRoot.Instance != null)
                {
                    CevirUIRoot.Instance.ShowToast(_draft.enabled
                        ? "Xatırlatmalar " + _draft.WindowText + " aralığında gələcək"
                        : "Xatırlatmalar söndürüldü");
                }
            });
        }

        private void NotNow()
        {
            _draft.enabled = false;
            if (ReminderService.Instance != null) ReminderService.Instance.Apply(_draft);
            Close();
        }

        private void Shift(bool start, int delta)
        {
            if (start)
            {
                int length = _draft.endMinutes - _draft.startMinutes;
                _draft.startMinutes = Mathf.Clamp(_draft.startMinutes + delta, 0, 24 * 60 - ReminderSettings.MinLength);
                _draft.endMinutes = Mathf.Min(24 * 60, _draft.startMinutes + Mathf.Max(length, ReminderSettings.MinLength));
            }
            else
            {
                _draft.endMinutes = Mathf.Clamp(_draft.endMinutes + delta, _draft.startMinutes + ReminderSettings.MinLength, 24 * 60);
            }

            Render();
        }

        private void Render()
        {
            _backButton.SetActive(!_onboarding);
            _laterButton.SetActive(_onboarding);
            _toggleRow.SetActive(!_onboarding);
            _toggleLabel.text = _draft.enabled ? "AÇIQ" : "BAĞLI";
            UIKit.Select(_toggleButton, _draft.enabled);
            _timeSection.SetActive(_draft.enabled);

            _startLabel.text = ReminderSettings.Clock(_draft.startMinutes);
            _endLabel.text = ReminderSettings.Clock(_draft.endMinutes);
            for (int i = 0; i < _presetButtons.Count; i++)
            {
                UIKit.Select(_presetButtons[i], _draft.startMinutes == Presets[i][0] && _draft.endMinutes == Presets[i][1]);
            }

            int first = _draft.startMinutes;
            int second = Mathf.Min(first + ReminderPlanner.Gap, _draft.endMinutes);
            int third = Mathf.Min(first + 2 * ReminderPlanner.Gap, _draft.endMinutes);
            _summary.text = _draft.enabled
                ? "Xatırlatmalar: <b>" + ReminderSettings.Clock(first) + "</b> və <b>" + ReminderSettings.Clock(second) + "</b>.\n" +
                  "Alovun varsa və həmin gün dərs keçməmisənsə, <b>" + ReminderSettings.Clock(third) + "</b>-da bir dəfə də xatırladacağıq."
                : "Xatırlatmalar söndürülüb. Seriyanı özün izləməli olacaqsan.";
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
            RectTransform content = kit.Scroll(safe, new RectOffset(48, 48, 40, 80), 30f);

            RectTransform top = UIKit.Rect("Top", content);
            UIKit.Row(top.gameObject, 20f);
            UIKit.Layout(top.gameObject, preferredHeight: 96f);
            _backButton = kit.IconButton(top, "Back", iconBack, "‹", 96f, UIPalette.TextPrimary, Color.white, Close).gameObject;
            TextMeshProUGUI heading = kit.Text(top, "Heading", "Bildirişlər", 46f, UIPalette.TextPrimary);
            UIKit.Layout(heading.gameObject, flexibleWidth: 1f);

            RectTransform hero = kit.Card(content, "Intro", 44, 18f);
            hero.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
            Image tile = kit.Image(hero, "BellTile", theme != null ? theme.tileGradient : null, Color.white, false);
            tile.preserveAspect = false;
            UIKit.Layout(tile.gameObject, preferredWidth: 140f, preferredHeight: 140f).flexibleWidth = 0f;
            hero.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;
            Image bell = kit.Image(tile.transform, "Bell", iconBell, Color.white, false);
            bell.rectTransform.anchorMin = bell.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            bell.rectTransform.sizeDelta = new Vector2(76f, 76f);
            TextMeshProUGUI title = kit.Text(hero, "Title", "Gün ərzində hansı saatlarda məşq edə bilərsən?", 44f, UIPalette.TextPrimary,
                TextAlignmentOptions.Center);
            UIKit.Layout(title.gameObject, flexibleWidth: 1f);
            TextMeshProUGUI body = kit.Text(hero, "Body",
                "Seçdiyin saat aralığında sənə 30 dəqiqə arayla 2 xatırlatma göndərəcəyik ki, dərsi unutmayasan.", 32f,
                UIPalette.TextSecondary, TextAlignmentOptions.Center, false);
            UIKit.Layout(body.gameObject, flexibleWidth: 1f);

            // On / off (Profile only).
            RectTransform toggleCard = kit.Card(content, "Toggle", 36, 10f);
            _toggleRow = toggleCard.gameObject;
            RectTransform toggleRow = UIKit.Rect("Row", toggleCard);
            UIKit.Row(toggleRow.gameObject, 20f);
            TextMeshProUGUI toggleTitle = kit.Text(toggleRow, "Label", "Gündəlik xatırlatmalar", 36f, UIPalette.TextPrimary);
            UIKit.Layout(toggleTitle.gameObject, flexibleWidth: 1f);
            _toggleButton = kit.Button(toggleRow, "Switch", "AÇIQ", UIPalette.Primary, Color.white, 88f, false, out _toggleLabel,
                () => { _draft.enabled = !_draft.enabled; Render(); });
            UIKit.Layout(_toggleButton.gameObject, preferredWidth: 220f);

            // Presets and custom window.
            RectTransform times = UIKit.Rect("Times", content);
            UIKit.Column(times.gameObject, 0, 26f);
            _timeSection = times.gameObject;

            kit.Text(times, "PresetsTitle", "Hazır aralıqlar", 34f, UIPalette.TextSecondary);
            for (int rowIndex = 0; rowIndex < 2; rowIndex++)
            {
                RectTransform row = UIKit.Rect("PresetRow" + rowIndex, times);
                HorizontalLayoutGroup layout = UIKit.Row(row.gameObject, 20f);
                layout.childForceExpandWidth = true;
                for (int i = rowIndex * 2; i < rowIndex * 2 + 2; i++)
                {
                    int index = i;
                    string text = PresetNames[i] + "\n<size=80%>" + ReminderSettings.Clock(Presets[i][0]) + "–" + ReminderSettings.Clock(Presets[i][1]) + "</size>";
                    TextMeshProUGUI label;
                    Button preset = kit.Button(row, "Preset" + i, text, UIPalette.Surface, UIPalette.TextSecondary, 140f, false, out label, () =>
                    {
                        _draft.startMinutes = Presets[index][0];
                        _draft.endMinutes = Presets[index][1];
                        Render();
                    });
                    label.enableWordWrapping = true;
                    label.fontSize = 32f;
                    var presetImage = preset.targetGraphic as Image;
                    if (presetImage != null && theme != null) { presetImage.sprite = theme.panel; presetImage.pixelsPerUnitMultiplier = 1.4f; }
                    UIKit.Layout(preset.gameObject, flexibleWidth: 1f);
                    _presetButtons.Add(preset);
                }
            }

            kit.Text(times, "CustomTitle", "Və ya özün seç", 34f, UIPalette.TextSecondary);
            RectTransform custom = kit.Card(times, "Custom", 36, 24f);
            _startLabel = TimeRow(kit, custom, "Başlanğıc", true);
            _endLabel = TimeRow(kit, custom, "Son", false);

            _summary = kit.Text(content, "Summary", "", 31f, UIPalette.TextSecondary, TextAlignmentOptions.Center, false);
            _hint = kit.Text(content, "Hint", "", 30f, UIPalette.Red, TextAlignmentOptions.Center, false);

            kit.Button(content, "Save", "YADDA SAXLA", Color.white, Color.white, 128f, true, Save);
            _laterButton = kit.Button(content, "Later", "İNDİ YOX", new Color(1f, 1f, 1f, 0f), UIPalette.TextSecondary, 96f, false, NotNow).gameObject;
        }

        private TextMeshProUGUI TimeRow(UIKit kit, Transform parent, string title, bool start)
        {
            RectTransform row = UIKit.Rect(title, parent);
            UIKit.Row(row.gameObject, 18f);
            UIKit.Layout(row.gameObject, preferredHeight: 100f);
            TextMeshProUGUI name = kit.Text(row, "Title", title, 36f, UIPalette.TextPrimary);
            UIKit.Layout(name.gameObject, flexibleWidth: 1f);
            kit.IconButton(row, "Minus", null, "−", 96f, UIPalette.Primary, UIPalette.CyanLight, () => Shift(start, -ReminderSettings.Step));
            TextMeshProUGUI value = kit.Text(row, "Value", "18:00", 44f, UIPalette.TextPrimary, TextAlignmentOptions.Center);
            value.enableWordWrapping = false;
            UIKit.Layout(value.gameObject, preferredWidth: 170f, minWidth: 150f);
            kit.IconButton(row, "Plus", null, "+", 96f, UIPalette.Primary, UIPalette.CyanLight, () => Shift(start, ReminderSettings.Step));
            return value;
        }
    }
}
