using System;
using System.Collections.Generic;
using Cevir.Core.Game;
using Cevir.Core.Gamification;
using Cevir.Core.Localization;
using Cevir.Core.Social;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// League: three tabs (this week / this month / this year; the week always opens first), a header card (cup,
    /// league or period name, time left), a line saying the learner's place in the chosen ranking, the rows ordered by
    /// XP with profile photos and the learner highlighted as "Name (you)", and in the weekly league the demotion note.
    /// The background is the same time-of-day / weather sky as the Learn page.
    /// </summary>
    public sealed class LeagueScreenUI : MonoBehaviour
    {
        [Tooltip("Tab buttons: weekly, monthly, yearly.")]
        [SerializeField] private Button[] periodTabs;
        [SerializeField] private TextMeshProUGUI[] periodTabLabels;
        [SerializeField] private TextMeshProUGUI kicker;
        [SerializeField] private Image tierCup;
        [SerializeField] private Image tierCupBackdrop;
        [SerializeField] private TextMeshProUGUI leagueName;
        [SerializeField] private TextMeshProUGUI timeLeft;
        [Tooltip("\"You are 3rd in the weekly ranking\" line under the header.")]
        [SerializeField] private TextMeshProUGUI promoteNote;
        [SerializeField] private TextMeshProUGUI demoteNote;
        [SerializeField] private RectTransform list;
        [SerializeField] private LeagueRowUI rowPrefab;
        [SerializeField] private CevirTheme theme;

        private readonly List<LeagueRowUI> _rows = new List<LeagueRowUI>();
        private GameManager _game;
        private LeaguePeriod _period = LeaguePeriod.Week;
        private HomeSkyUI _sky;
        private bool _tabsWired;

        private void OnEnable()
        {
            _game = GameManager.Instance;
            _period = LeaguePeriod.Week; // the weekly ranking always opens first
            if (_sky == null) _sky = HomeSkyUI.Create((RectTransform)transform, theme);
            WireTabs();
            Loc.LanguageChanged += OnLanguageChanged;
            if (_game != null)
            {
                _game.Xp.XpGained += OnXpGained;
                _game.ProfileChanged += Refresh;
            }

            Refresh();
            if (_game != null) _game.RequestLeagueRefresh();
        }

        private void OnDisable()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
            if (_game == null) return;
            _game.Xp.XpGained -= OnXpGained;
            _game.ProfileChanged -= Refresh;
        }

        private void OnLanguageChanged(string language) => Refresh();
        private void OnXpGained(XpState state, int amount) => Refresh();

        private void WireTabs()
        {
            if (_tabsWired || periodTabs == null) return;
            _tabsWired = true;
            for (int i = 0; i < periodTabs.Length; i++)
            {
                var period = (LeaguePeriod)i;
                if (periodTabs[i] != null) periodTabs[i].onClick.AddListener(() =>
                {
                    if (_period == period) return;
                    _period = period;
                    Refresh();
                    if (_game != null) _game.RequestLeagueRefresh();
                });
            }
        }

        private void RefreshTabs()
        {
            if (periodTabs == null) return;
            string[] names = { Loc.Pick("Weekly", "Həftəlik", "Неделя"), Loc.Pick("Monthly", "Aylıq", "Месяц"), Loc.Pick("Yearly", "İllik", "Год") };
            for (int i = 0; i < periodTabs.Length; i++)
            {
                bool on = (int)_period == i;
                var image = periodTabs[i] != null ? periodTabs[i].targetGraphic as Image : null;
                if (image != null)
                {
                    image.sprite = on ? theme.buttonGradient : theme.panel;
                    image.color = Color.white;
                }

                if (periodTabLabels != null && i < periodTabLabels.Length && periodTabLabels[i] != null)
                {
                    periodTabLabels[i].text = i < names.Length ? names[i] : string.Empty;
                    periodTabLabels[i].color = on ? Color.white : UIPalette.TextPrimary;
                }
            }
        }

        private void Refresh()
        {
            if (_game == null) return;
            RefreshTabs();
            LeagueSnapshot snapshot = _game.GetLeagueSnapshot(_period);
            bool week = _period == LeaguePeriod.Week;
            Color cupColor = week ? LeagueTiers.Color(snapshot.Tier)
                : _period == LeaguePeriod.Month ? UIPalette.Gold : LeagueTiers.Colors[LeagueTiers.Colors.Length - 1];
            DateTime now = _game.LocalNow();

            if (kicker != null)
            {
                kicker.text = week ? Loc.Pick("WEEKLY RANKING", "HƏFTƏLİK REYTİNQ", "РЕЙТИНГ НЕДЕЛИ")
                    : _period == LeaguePeriod.Month ? Loc.Pick("MONTHLY RANKING", "AYLIQ REYTİNQ", "РЕЙТИНГ МЕСЯЦА")
                    : Loc.Pick("YEARLY RANKING", "İLLİK REYTİNQ", "РЕЙТИНГ ГОДА");
            }

            if (tierCup != null) tierCup.color = cupColor;
            // Opaque tint, so the square's dark border stays solid.
            if (tierCupBackdrop != null) tierCupBackdrop.color = Color.Lerp(Color.white, cupColor, 0.22f);
            if (leagueName != null)
            {
                leagueName.text = week ? LeagueTiers.LeagueName(snapshot.Tier)
                    : _period == LeaguePeriod.Month ? MonthName(now.Month) + " " + now.Year
                    : now.Year.ToString();
            }

            if (timeLeft != null) timeLeft.text = LocalLeagueService.FormatTimeLeft(snapshot.TimeLeft);

            // Where the learner stands in this ranking (finding yourself in a long list is hard).
            if (promoteNote != null)
            {
                LeagueEntry you = snapshot.You;
                promoteNote.text = snapshot.Offline || you == null ? UIStrings.LeagueOffline : PlaceText(_period, you.Rank);
            }

            if (demoteNote != null)
            {
                demoteNote.text = string.Format(UIStrings.BottomDemoted, snapshot.DemoteCount);
                // The label sits in its note row (icon + text): hide the whole row.
                demoteNote.transform.parent.gameObject.SetActive(week && !snapshot.Offline && snapshot.DemoteCount > 0);
            }

            while (_rows.Count < snapshot.Entries.Count) _rows.Add(Instantiate(rowPrefab, list));
            for (int i = 0; i < _rows.Count; i++)
            {
                bool visible = i < snapshot.Entries.Count;
                _rows[i].gameObject.SetActive(visible);
                if (visible) _rows[i].Bind(snapshot.Entries[i], theme);
            }
        }

        private static string PlaceText(LeaguePeriod period, int place)
        {
            switch (period)
            {
                case LeaguePeriod.Month:
                    return Loc.Pick("You are #" + place + " in the monthly ranking.", "Aylıq reytinqdə " + Ordinal(place) + " yerdəsən.",
                        "Ты на " + place + "-м месте в рейтинге месяца.");
                case LeaguePeriod.Year:
                    return Loc.Pick("You are #" + place + " in the yearly ranking.", "İllik reytinqdə " + Ordinal(place) + " yerdəsən.",
                        "Ты на " + place + "-м месте в рейтинге года.");
                default:
                    return Loc.Pick("You are #" + place + " in this week's league.", "Həftəlik reytinqdə " + Ordinal(place) + " yerdəsən.",
                        "Ты на " + place + "-м месте в лиге этой недели.");
            }
        }

        /// <summary>Azerbaijani ordinal: 1-ci, 2-ci, 3-cü, 6-cı, 9-cu, 10-cu, 40-cı, 100-cü ...</summary>
        public static string Ordinal(int n)
        {
            string suffix;
            int units = n % 10, tens = (n / 10) % 10, hundreds = (n / 100) % 10;
            if (units != 0) suffix = new[] { "", "ci", "ci", "cü", "cü", "ci", "cı", "ci", "ci", "cu" }[units];
            else if (tens != 0) suffix = new[] { "", "cu", "ci", "cu", "cı", "ci", "cı", "ci", "ci", "cı" }[tens];
            else if (hundreds != 0) suffix = "cü";
            else suffix = "ci";
            return n + "-" + suffix;
        }

        private static string MonthName(int month)
        {
            string[] en = { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
            string[] az = { "Yanvar", "Fevral", "Mart", "Aprel", "May", "İyun", "İyul", "Avqust", "Sentyabr", "Oktyabr", "Noyabr", "Dekabr" };
            string[] ru = { "Январь", "Февраль", "Март", "Апрель", "Май", "Июнь", "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь" };
            int i = Mathf.Clamp(month, 1, 12) - 1;
            return Loc.Pick(en[i], az[i], ru[i]);
        }
    }
}
