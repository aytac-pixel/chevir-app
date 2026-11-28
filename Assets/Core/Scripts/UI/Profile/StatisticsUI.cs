using System;
using Cevir.Core.Game;
using Cevir.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Profile > Statistics: current and longest flame, level, signs learned and total XP, then this week day by day
    /// (Monday to Sunday): minutes spent in the app and XP earned.
    /// </summary>
    public sealed class StatisticsUI : ProfilePanelBase
    {
        private const float ChartHeight = 230f;

        [SerializeField] private Sprite iconFlame;
        [SerializeField] private Sprite iconCrown;
        [SerializeField] private Sprite iconHand;
        [SerializeField] private Sprite iconZap;
        [SerializeField] private Sprite iconTrophy;
        [SerializeField] private Sprite iconChart;

        private TextMeshProUGUI _current;
        private TextMeshProUGUI _longest;
        private TextMeshProUGUI _level;
        private TextMeshProUGUI _signs;
        private TextMeshProUGUI _totalXp;
        private TextMeshProUGUI _weekMinutes;
        private TextMeshProUGUI _weekXp;
        private Chart _minutesChart;
        private Chart _xpChart;
        private float _nextRender;

        public static StatisticsUI Instance { get; private set; }

        protected override string Title => Loc.Pick("Statistics", "Statistika", "Статистика");

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
            // Minutes go up while the page is open.
            if (!IsOpen || Time.unscaledTime < _nextRender) return;
            _nextRender = Time.unscaledTime + 5f;
            Render();
        }

        private sealed class Chart
        {
            public RectTransform[] Bars = new RectTransform[7];
            public Image[] Fills = new Image[7];
            public TextMeshProUGUI[] Values = new TextMeshProUGUI[7];
            public TextMeshProUGUI[] Days = new TextMeshProUGUI[7];
        }

        protected override void BuildContent(UIKit kit, RectTransform content)
        {
            // Flames, level, signs, XP.
            RectTransform grid = UIKit.Rect("Summary", content);
            GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(468f, 200f);
            layout.spacing = new Vector2(24f, 24f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            grid.gameObject.AddComponent<GridAutoFit>();
            UIKit.Layout(grid.gameObject, preferredHeight: 3 * 200f + 2 * 24f);

            _current = Tile(kit, grid, "Current", iconFlame, UIPalette.Streak, Loc.Pick("Current flame", "İndiki alov", "Текущий огонёк"));
            _longest = Tile(kit, grid, "Longest", iconTrophy, UIPalette.Gold, Loc.Pick("Longest flame", "Ən uzun alov", "Самый долгий огонёк"));
            _level = Tile(kit, grid, "Level", iconCrown, UIPalette.Primary, Loc.Pick("Level", "Səviyyə", "Уровень"));
            _signs = Tile(kit, grid, "Signs", iconHand, UIPalette.Green, Loc.Pick("Signs learned", "Öyrənilən işarələr", "Выучено жестов"));
            _totalXp = Tile(kit, grid, "TotalXp", iconZap, UIPalette.Xp, Loc.Pick("Total XP", "Ümumi XP", "Всего XP"));
            _weekMinutes = Tile(kit, grid, "WeekMinutes", iconChart, UIPalette.Primary, Loc.Pick("Minutes this week", "Bu həftə dəqiqə", "Минут за неделю"));

            _minutesChart = BuildChart(kit, content, "MinutesChart", Loc.Pick("Minutes per day", "Hər gün neçə dəqiqə", "Минут в день"),
                Loc.Pick("Time with Chevir open, this week", "Bu həftə Chevir-də keçirdiyin vaxt", "Время в Chevir на этой неделе"), out _);
            _xpChart = BuildChart(kit, content, "XpChart", Loc.Pick("XP per day", "Hər gün yığılan XP", "XP в день"),
                null, out _weekXp);
        }

        private TextMeshProUGUI Tile(UIKit kit, Transform parent, string name, Sprite icon, Color tint, string caption)
        {
            RectTransform card = kit.Card(parent, name, 28, 6f);
            VerticalLayoutGroup column = card.GetComponent<VerticalLayoutGroup>();
            column.childAlignment = TextAnchor.MiddleLeft;
            column.childForceExpandHeight = false;

            RectTransform row = UIKit.Rect("Row", card);
            UIKit.Row(row.gameObject, 14f);
            UIKit.Layout(row.gameObject, preferredHeight: 80f);
            kit.Icon(row, "Icon", icon, 60f, tint);
            TextMeshProUGUI value = kit.Text(row, "Value", "0", 56f, UIPalette.TextPrimary);
            value.enableWordWrapping = false;
            value.overflowMode = TextOverflowModes.Ellipsis;
            UIKit.Layout(value.gameObject, flexibleWidth: 1f, preferredHeight: 80f);

            TextMeshProUGUI label = kit.Text(card, "Caption", caption, 28f, UIPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, false);
            UIKit.Layout(label.gameObject, preferredHeight: 70f);
            return value;
        }

        private Chart BuildChart(UIKit kit, Transform parent, string name, string title, string note, out TextMeshProUGUI total)
        {
            var chart = new Chart();
            RectTransform card = kit.Card(parent, name, 40, 14f);

            RectTransform head = UIKit.Rect("Head", card);
            UIKit.Row(head.gameObject, 12f);
            UIKit.Layout(head.gameObject, preferredHeight: 60f);
            TextMeshProUGUI heading = kit.Text(head, "Title", title, 36f, UIPalette.TextPrimary);
            UIKit.Layout(heading.gameObject, flexibleWidth: 1f);
            total = kit.Text(head, "Total", "", 32f, UIPalette.Primary, TextAlignmentOptions.MidlineRight);
            total.enableWordWrapping = false;
            UIKit.Layout(total.gameObject, preferredWidth: 260f);
            if (note != null) kit.Text(card, "Note", note, 26f, UIPalette.TextSecondary, TextAlignmentOptions.MidlineLeft, false);

            RectTransform columns = UIKit.Rect("Days", card);
            HorizontalLayoutGroup row = UIKit.Row(columns.gameObject, 10f, TextAnchor.LowerCenter);
            row.childForceExpandWidth = true;
            UIKit.Layout(columns.gameObject, preferredHeight: ChartHeight + 110f);

            for (int i = 0; i < 7; i++)
            {
                RectTransform column = UIKit.Rect("Day" + i, columns);
                VerticalLayoutGroup stack = UIKit.Column(column.gameObject, 0, 6f, TextAnchor.LowerCenter);
                stack.childForceExpandHeight = false;
                UIKit.Layout(column.gameObject, flexibleWidth: 1f);

                chart.Values[i] = kit.Text(column, "Value", "0", 24f, UIPalette.TextSecondary, TextAlignmentOptions.Bottom);
                chart.Values[i].enableWordWrapping = false;
                UIKit.Layout(chart.Values[i].gameObject, preferredHeight: 34f);

                RectTransform area = UIKit.Rect("Area", column);
                UIKit.Layout(area.gameObject, preferredHeight: ChartHeight);
                Image track = area.gameObject.AddComponent<Image>();
                track.color = UIPalette.Surface;
                track.raycastTarget = false;
                Image fill = kit.Image(area, "Bar", null, UIPalette.Primary, false);
                RectTransform bar = fill.rectTransform;
                bar.anchorMin = new Vector2(0f, 0f);
                bar.anchorMax = new Vector2(1f, 0f);
                bar.pivot = new Vector2(0.5f, 0f);
                bar.anchoredPosition = Vector2.zero;
                bar.sizeDelta = new Vector2(0f, 0f);
                chart.Bars[i] = bar;
                chart.Fills[i] = fill;

                chart.Days[i] = kit.Text(column, "Day", "", 24f, UIPalette.TextSecondary, TextAlignmentOptions.Top);
                chart.Days[i].enableWordWrapping = false;
                UIKit.Layout(chart.Days[i].gameObject, preferredHeight: 40f);
            }

            return chart;
        }

        protected override void Render()
        {
            GameManager game = GameManager.Instance;
            if (game == null || _current == null) return;
            PlayerProgress progress = game.Progress;

            _current.text = game.Streak.State.current.ToString();
            _longest.text = Math.Max(game.Streak.State.longest, game.Streak.State.current).ToString();
            LevelPathSummary level = game.CurrentLevel;
            _level.text = level != null ? level.Level.Title : "—";
            _signs.text = game.ActivePackProgress != null ? game.ActivePackProgress.SignsLearned.ToString() : "0";
            _totalXp.text = game.Xp.Total.ToString();

            DateTime today = game.LocalNow().Date;
            DateTime monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            var minutes = new int[7];
            var xp = new int[7];
            for (int i = 0; i < 7; i++)
            {
                DayActivity day = progress.FindDay(monday.AddDays(i));
                if (day == null) continue;
                minutes[i] = (day.seconds + 30) / 60;
                xp[i] = day.xp;
            }

            int todayIndex = (today - monday).Days;
            int weekMinutes = Sum(minutes), weekXp = Sum(xp);
            _weekMinutes.text = weekMinutes.ToString();
            _weekXp.text = weekXp + " XP";
            Fill(_minutesChart, minutes, todayIndex, Loc.Pick("min", "dəq", "мин"), UIPalette.Xp, UIPalette.PrimaryDark);
            Fill(_xpChart, xp, todayIndex, null, UIPalette.Amber, UIPalette.AmberDark);
        }

        private static int Sum(int[] values)
        {
            int total = 0;
            foreach (int v in values) total += v;
            return total;
        }

        private static readonly string[] DaysEn = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        private static readonly string[] DaysAz = { "B.e.", "Ç.a.", "Ç.", "C.a.", "C.", "Ş.", "B." };
        private static readonly string[] DaysRu = { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };

        private static void Fill(Chart chart, int[] values, int todayIndex, string unit, Color color, Color todayColor)
        {
            int max = 1;
            foreach (int v in values) max = Math.Max(max, v);
            for (int i = 0; i < 7; i++)
            {
                bool future = i > todayIndex;
                float height = values[i] > 0 ? Mathf.Max(8f, ChartHeight * values[i] / max) : 0f;
                chart.Bars[i].sizeDelta = new Vector2(0f, height);
                chart.Fills[i].color = i == todayIndex ? todayColor : color; // palette colours only, so the night theme maps them
                chart.Values[i].text = future ? string.Empty : unit != null ? values[i] + " " + unit : values[i].ToString();
                chart.Days[i].text = Loc.Pick(DaysEn[i], DaysAz[i], DaysRu[i]);
                Color dayColor = i == todayIndex ? UIPalette.Primary : UIPalette.TextSecondary;
                chart.Days[i].color = dayColor;
                chart.Days[i].fontStyle = i == todayIndex ? FontStyles.Bold : FontStyles.Normal;
            }
        }
    }
}
