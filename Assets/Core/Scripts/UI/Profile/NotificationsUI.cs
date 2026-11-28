using Cevir.Core.Game;
using Cevir.Core.Localization;
using Cevir.Core.Notifications;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Profile > Notifications: the daily greetings (morning, afternoon, evening, night), the practice reminders in
    /// the learner's own hours, and the league message when the weekly place changes.
    /// </summary>
    public sealed class NotificationsUI : ProfilePanelBase
    {
        [SerializeField] private Sprite iconSun;
        [SerializeField] private Sprite iconTrophy;

        private Button[] _greetings;
        private Button[] _league;
        private TextMeshProUGUI _practiceStatus;
        private TextMeshProUGUI _denied;
        private float _nextRender;

        public static NotificationsUI Instance { get; private set; }

        protected override string Title => Loc.Pick("Notifications", "Bildirişlər", "Уведомления");

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

        private void Update()
        {
            // The practice hours are changed in their own panel; keep the line current.
            if (!IsOpen || Time.unscaledTime < _nextRender) return;
            _nextRender = Time.unscaledTime + 0.5f;
            Render();
        }

        protected override void BuildContent(UIKit kit, RectTransform content)
        {
            _denied = kit.Text(content, "Denied", Loc.Pick(
                    "Notifications are blocked on this phone. Allow them for Chevir in the phone's settings.",
                    "Bu telefonda bildirişlər bağlıdır. Telefonun ayarlarında Chevir üçün icazə ver.",
                    "Уведомления на этом телефоне выключены. Разреши их для Chevir в настройках телефона."),
                28f, UIPalette.RedDark, TextAlignmentOptions.Center, false);
            _denied.gameObject.SetActive(false);

            // Daily greetings.
            RectTransform greetings = kit.Card(content, "Greetings", 40, 18f);
            Header(kit, greetings, iconSun != null ? iconSun : iconBell, UIPalette.Amber, UIPalette.AmberLight,
                Loc.Pick("Daily greetings", "Gündəlik salamlar", "Приветствия дня"),
                Loc.Pick("A friendly message at four times of the day", "Günün dörd vaxtında səmimi bir mesaj", "Тёплое сообщение четыре раза в день"));
            Line(kit, greetings, "07:00", Loc.Pick("Good morning! Ready for today?", "Sabahınız xeyir! Bu gün bir işarə öyrənməyə nə deyirsən?", "Доброе утро! Готов к новому дню?"),
                Loc.Pick("every day, a different message each weekday", "hər gün, həftənin hər günü fərqli mesaj", "каждый день, в каждый день недели своё"));
            Line(kit, greetings, "13:00", Loc.Pick("Good afternoon! Would you like to start?", "Günortanız xeyir! Başlamaq istəyirsən?", "Добрый день! Хочешь начать?"),
                Loc.Pick("not if you came in the morning", "səhər girmisənsə, gəlmir", "не приходит, если ты заходил утром"));
            Line(kit, greetings, "19:00", Loc.Pick("Good evening! When shall we start?", "Axşamınız xeyir! Nə zaman başlayaq?", "Добрый вечер! Когда начнём?"),
                Loc.Pick("not if you came in the afternoon or did a lesson", "günortadan sonra girmisənsə və ya dərs keçmisənsə, gəlmir",
                    "не приходит, если ты заходил днём или прошёл урок"));
            Line(kit, greetings, "22:00", Loc.Pick("Good night!", "Gecəniz xeyrə qarşı! Bu gün əla işlədin", "Спокойной ночи!"),
                Loc.Pick("every day; other words when you did a lesson that day", "hər gün; o gün dərs keçmisənsə, başqa sözlərlə", "каждый день; другие слова, если ты занимался"));
            _greetings = OnOff(kit, greetings, on => Set(on, null));

            // Practice reminders in the learner's own hours.
            RectTransform practice = kit.Card(content, "Practice", 36, 10f);
            RectTransform practiceRow = UIKit.Rect("Row", practice);
            UIKit.Row(practiceRow.gameObject, 22f);
            IconTile(kit, practiceRow, iconBell, UIPalette.Primary, UIPalette.CyanLight);
            RectTransform texts = UIKit.Rect("Texts", practiceRow);
            UIKit.Column(texts.gameObject, 0, 4f, TextAnchor.MiddleLeft);
            UIKit.Layout(texts.gameObject, flexibleWidth: 1f);
            kit.Text(texts, "Title", Loc.T("Məşq xatırlatmaları"), 34f, UIPalette.TextPrimary);
            _practiceStatus = kit.Text(texts, "Status", "", 27f, UIPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, false);
            Button setup = kit.Button(practiceRow, "Open", Loc.T("AYARLA"), UIPalette.CyanLight, UIPalette.Primary, 84f, false, () =>
            {
                if (ReminderSettingsUI.Instance != null) ReminderSettingsUI.Instance.Open(false);
            });
            UIKit.Layout(setup.gameObject, preferredWidth: 210f, minWidth: 180f);

            // League place.
            RectTransform league = kit.Card(content, "League", 40, 18f);
            Header(kit, league, iconTrophy, UIPalette.Gold, UIPalette.AmberLight, Loc.Pick("League", "Liqa", "Лига"),
                Loc.Pick("Once a day at 07:00: how many places you moved up or down since yesterday morning",
                    "Gündə bir dəfə, səhər 07:00-da: dünən səhərdən bəri liqada neçə pillə qalxdığın və ya düşdüyün",
                    "Раз в день в 07:00: на сколько мест ты поднялся или опустился со вчерашнего утра"));
            _league = OnOff(kit, league, on => Set(null, on));
        }

        private void Header(UIKit kit, Transform card, Sprite icon, Color tint, Color fill, string title, string body)
        {
            RectTransform row = UIKit.Rect("Head", card);
            UIKit.Row(row.gameObject, 22f);
            IconTile(kit, row, icon, tint, fill);
            RectTransform texts = UIKit.Rect("Texts", row);
            UIKit.Column(texts.gameObject, 0, 4f, TextAnchor.MiddleLeft);
            UIKit.Layout(texts.gameObject, flexibleWidth: 1f);
            kit.Text(texts, "Title", title, 36f, UIPalette.TextPrimary);
            kit.Text(texts, "Body", body, 27f, UIPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, false);
        }

        private static void Line(UIKit kit, Transform card, string time, string message, string rule)
        {
            RectTransform row = UIKit.Rect("Line_" + time, card);
            HorizontalLayoutGroup layout = UIKit.Row(row.gameObject, 18f, TextAnchor.UpperLeft);
            layout.childForceExpandHeight = false;
            TextMeshProUGUI clock = kit.Text(row, "Time", time, 30f, UIPalette.Primary, TextAlignmentOptions.TopLeft);
            clock.enableWordWrapping = false;
            UIKit.Layout(clock.gameObject, preferredWidth: 110f, minWidth: 110f);
            RectTransform texts = UIKit.Rect("Texts", row);
            UIKit.Column(texts.gameObject, 0, 2f, TextAnchor.UpperLeft);
            UIKit.Layout(texts.gameObject, flexibleWidth: 1f);
            kit.Text(texts, "Message", message, 29f, UIPalette.TextPrimary, TextAlignmentOptions.TopLeft);
            kit.Text(texts, "Rule", rule, 25f, UIPalette.TextMuted, TextAlignmentOptions.TopLeft, false);
        }

        private void Set(bool? greetings, bool? league)
        {
            GameManager game = GameManager.Instance;
            ReminderService service = ReminderService.Instance;
            if (game == null || service == null) return;
            ReminderSettings r = game.Progress.reminders ?? (game.Progress.reminders = new ReminderSettings());
            service.SetExtras(greetings ?? r.greetings, league ?? r.leagueAlerts);
            Render();
        }

        protected override void Render()
        {
            GameManager game = GameManager.Instance;
            if (game == null || _greetings == null) return;
            ReminderSettings r = game.Progress.reminders ?? new ReminderSettings();
            UIKit.Select(_greetings[0], r.greetings);
            UIKit.Select(_greetings[1], !r.greetings);
            UIKit.Select(_league[0], r.leagueAlerts);
            UIKit.Select(_league[1], !r.leagueAlerts);
            _practiceStatus.text = r.configured && r.enabled
                ? string.Format(Loc.T("Hər gün {0} aralığında xatırladırıq"), r.WindowText)
                : Loc.T("Xatırlatmalar bağlıdır");
            ReminderService service = ReminderService.Instance;
            _denied.gameObject.SetActive(service != null && service.PermissionDenied);
        }
    }
}
