using System;
using UnityEngine;

namespace Cevir.Core.Gamification
{
    [Serializable]
    public sealed class XpState
    {
        /// <summary>Lifetime XP, never decreases.</summary>
        public int total;

        /// <summary>Spendable XP (streak freezes). Spending does not reduce league or lifetime XP.</summary>
        public int balance;

        /// <summary>XP earned in the current league week, and which ISO week that is.</summary>
        public int weekly;
        public string week = "";

        /// <summary>XP earned in the current month ("2026-11") and year ("2026"): the monthly and yearly rankings.</summary>
        public int monthly;
        public string month = "";
        public int yearly;
        public string year = "";
    }

    public sealed class XpSystem
    {
        private readonly GamificationConfig _config;
        private readonly XpState _state;

        public event Action<XpState, int> XpGained;

        public XpSystem(GamificationConfig config, XpState state)
        {
            _config = config;
            _state = state;
        }

        public int Total => _state.total;
        public int Balance => _state.balance;
        public int Weekly => _state.weekly;
        public XpState State => _state;

        /// <summary>XP earned in the ISO week of <paramref name="today"/> (0 if nothing was earned yet that week).</summary>
        public int WeeklyFor(DateTime today)
        {
            return _state.week == StreakSystem.IsoWeek(today) ? _state.weekly : 0;
        }

        /// <summary>XP earned in the month of <paramref name="today"/>.</summary>
        public int MonthlyFor(DateTime today)
        {
            Roll(today);
            return _state.month == MonthKey(today) ? _state.monthly : 0;
        }

        /// <summary>XP earned in the year of <paramref name="today"/>.</summary>
        public int YearlyFor(DateTime today)
        {
            Roll(today);
            return _state.year == YearKey(today) ? _state.yearly : 0;
        }

        public static string MonthKey(DateTime d) => d.Year.ToString("0000") + "-" + d.Month.ToString("00");
        public static string YearKey(DateTime d) => d.Year.ToString("0000");

        /// <summary>
        /// Starts a new month / year counter when the calendar moved on. Progress saved before these counters existed
        /// starts the year with the lifetime XP (the app is from this year) and the month with this week's XP.
        /// </summary>
        private void Roll(DateTime today)
        {
            string month = MonthKey(today), year = YearKey(today);
            if (string.IsNullOrEmpty(_state.year))
            {
                _state.year = year;
                _state.yearly = _state.total;
            }
            else if (_state.year != year)
            {
                _state.year = year;
                _state.yearly = 0;
            }

            if (string.IsNullOrEmpty(_state.month))
            {
                _state.month = month;
                _state.monthly = _state.week == StreakSystem.IsoWeek(today) ? Math.Min(_state.weekly, _state.yearly) : 0;
            }
            else if (_state.month != month)
            {
                _state.month = month;
                _state.monthly = 0;
            }
        }

        public int LessonReward(bool perfectCamera)
        {
            return _config.lessonXp + (perfectCamera ? _config.perfectCameraBonusXp : 0);
        }

        /// <summary>Mini-games pay between the configured min and max based on a 0..1 performance score.</summary>
        public int MiniGameReward(float normalizedScore)
        {
            return Mathf.RoundToInt(Mathf.Lerp(_config.miniGameMinXp, _config.miniGameMaxXp, Mathf.Clamp01(normalizedScore)));
        }

        public void Add(int amount, DateTime today)
        {
            if (amount <= 0) return;

            string week = StreakSystem.IsoWeek(today);
            if (_state.week != week)
            {
                _state.week = week;
                _state.weekly = 0;
            }

            Roll(today);
            _state.total += amount;
            _state.balance += amount;
            _state.weekly += amount;
            _state.monthly += amount;
            _state.yearly += amount;
            if (XpGained != null) XpGained(_state, amount);
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || _state.balance < amount) return false;
            _state.balance -= amount;
            return true;
        }
    }
}
