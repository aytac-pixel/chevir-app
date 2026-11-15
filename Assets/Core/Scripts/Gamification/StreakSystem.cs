using System;
using System.Globalization;

namespace Cevir.Core.Gamification
{
    [Serializable]
    public sealed class StreakState
    {
        public int current;
        public int longest;

        /// <summary>Local calendar date of the last day that counted, as yyyy-MM-dd; empty if never.</summary>
        public string lastActiveDate = "";

        public int freezes;

        /// <summary>ISO week (yyyy-Www) of the last freeze purchase.</summary>
        public string lastFreezePurchaseWeek = "";
    }

    /// <summary>
    /// Daily streak (the flame): grows when at least one lesson or mini-game is completed on a calendar day.
    /// One missed day is forgiven: after 5 days in a row, missing day 6 keeps the flame (the learner gets extra
    /// reminders), and it goes out only when day 7 is missed as well. Longer gaps are covered by stored freezes
    /// (bought with XP, limited per week); otherwise the streak resets.
    /// Dates are the device's local calendar days, passed in so the logic is testable.
    /// </summary>
    public sealed class StreakSystem
    {
        private const string DateFormat = "yyyy-MM-dd";

        private readonly GamificationConfig _config;
        private readonly StreakState _state;

        public event Action<StreakState> StreakChanged;

        public StreakSystem(GamificationConfig config, StreakState state)
        {
            _config = config;
            _state = state;
        }

        public StreakState State => _state;

        /// <summary>Missed days that do not break the flame.</summary>
        public const int GraceDays = 1;

        public bool IsActiveToday(DateTime today)
        {
            return _state.lastActiveDate == today.Date.ToString(DateFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>Days since the last active day (0 = today, 1 = yesterday); -1 if never active.</summary>
        public int DaysSinceActive(DateTime today)
        {
            DateTime last;
            if (!TryParse(_state.lastActiveDate, out last)) return -1;
            return (today.Date - last).Days;
        }

        /// <summary>
        /// True when yesterday was missed and the flame is still burning: it goes out unless the learner practises today.
        /// </summary>
        public bool IsAtRisk(DateTime today)
        {
            return _state.current > 0 && DaysSinceActive(today) == GraceDays + 1;
        }

        /// <summary>
        /// The flame a given future day would still have if the learner does not practise before it:
        /// the current streak while the gap stays within the grace day, otherwise 0.
        /// </summary>
        public int StreakAliveOn(DateTime day)
        {
            int since = DaysSinceActive(day);
            if (_state.current <= 0 || since < 0) return 0;
            return since <= GraceDays + 1 ? _state.current : 0;
        }

        /// <summary>
        /// Call on app start/resume. Consumes freezes for missed days, or resets the streak when there are not enough.
        /// Returns the number of freezes consumed.
        /// </summary>
        public int Evaluate(DateTime today)
        {
            DateTime last;
            if (!TryParse(_state.lastActiveDate, out last) || _state.current == 0)
            {
                return 0;
            }

            // One missed day is forgiven; only the days after it count.
            int missedDays = (today.Date - last).Days - 1 - GraceDays;
            if (missedDays <= 0)
            {
                return 0;
            }

            if (_state.freezes >= missedDays)
            {
                _state.freezes -= missedDays;

                // Frozen days keep the chain alive: treat yesterday as the last active day.
                _state.lastActiveDate = today.Date.AddDays(-1).ToString(DateFormat, CultureInfo.InvariantCulture);
                Raise();
                return missedDays;
            }

            int used = _state.freezes;
            _state.freezes = 0;
            _state.current = 0;
            Raise();
            return used;
        }

        /// <summary>Call when a lesson or mini-game is completed. Returns true if the streak grew today.</summary>
        public bool RegisterActivity(DateTime today)
        {
            Evaluate(today);
            if (IsActiveToday(today))
            {
                return false;
            }

            _state.current += 1;
            _state.longest = Math.Max(_state.longest, _state.current);
            _state.lastActiveDate = today.Date.ToString(DateFormat, CultureInfo.InvariantCulture);
            Raise();
            return true;
        }

        public bool CanBuyFreeze(DateTime today, int xpBalance)
        {
            return _config.freezesPerWeek > 0
                   && _state.freezes < _config.maxStoredFreezes
                   && xpBalance >= _config.freezeCostXp
                   && _state.lastFreezePurchaseWeek != IsoWeek(today);
        }

        /// <summary>Spends XP and stores a freeze. Returns false if not allowed this week or not affordable.</summary>
        public bool TryBuyFreeze(DateTime today, XpSystem xp)
        {
            if (!CanBuyFreeze(today, xp.Balance) || !xp.TrySpend(_config.freezeCostXp))
            {
                return false;
            }

            _state.freezes += 1;
            _state.lastFreezePurchaseWeek = IsoWeek(today);
            Raise();
            return true;
        }

        public static string IsoWeek(DateTime date)
        {
            // ISO 8601: the week belongs to the year of its Thursday; weeks start on Monday.
            int daysFromMonday = ((int)date.DayOfWeek + 6) % 7;
            DateTime thursday = date.Date.AddDays(3 - daysFromMonday);
            int week = (thursday.DayOfYear - 1) / 7 + 1;
            return thursday.Year.ToString(CultureInfo.InvariantCulture) + "-W" + week.ToString("00", CultureInfo.InvariantCulture);
        }

        private static bool TryParse(string value, out DateTime date)
        {
            return DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        private void Raise()
        {
            if (StreakChanged != null) StreakChanged(_state);
        }
    }
}
