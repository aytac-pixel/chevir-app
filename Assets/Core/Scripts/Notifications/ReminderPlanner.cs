using Cevir.Core.Localization;
using System;
using System.Collections.Generic;
using Cevir.Core.Game;
using Cevir.Core.Gamification;

namespace Cevir.Core.Notifications
{
    /// <summary>One reminder notification at a local time.</summary>
    public struct PlannedReminder
    {
        public DateTime FireAt;
        public string Title;
        public string Body;
        public bool Greeting;
    }

    /// <summary>
    /// Works out the practice reminders for the coming days (pure logic, so it is testable):
    /// inside the learner's time window, two reminders 30 minutes apart; when the flame is still burning and would
    /// be lost or put at risk by skipping that day, a third one 30 minutes later. A day the learner has already
    /// practised gets none. The plan is rebuilt every time the app goes to the background or a lesson is finished.
    /// </summary>
    public static class ReminderPlanner
    {
        public const int DaysAhead = 7;
        public const int Gap = 30;

        public static List<PlannedReminder> Plan(ReminderSettings settings, StreakSystem streak, DateTime now)
        {
            var plan = new List<PlannedReminder>();
            if (settings == null || !settings.enabled) return plan;
            settings.Normalize();

            for (int day = 0; day < DaysAhead; day++)
            {
                DateTime date = now.Date.AddDays(day);
                if (day == 0 && streak != null && streak.IsActiveToday(now)) continue;

                // Assume no practice before that day: the flame it would still have, and whether it is the last chance.
                int flame = streak != null ? streak.StreakAliveOn(date) : 0;
                bool lastChance = flame > 0 && streak.DaysSinceActive(date) == StreakSystem.GraceDays + 1;
                int count = flame > 0 ? 3 : 2;

                DateTime previous = DateTime.MinValue;
                for (int i = 0; i < count; i++)
                {
                    int minutes = Math.Min(settings.startMinutes + i * Gap, settings.endMinutes);
                    DateTime at = date.AddMinutes(minutes);
                    if (at <= now.AddMinutes(1) || at <= previous) continue;
                    previous = at;

                    string title, body;
                    Text(i, flame, lastChance, out title, out body);
                    plan.Add(new PlannedReminder { FireAt = at, Title = title, Body = body });
                }
            }

            return plan;
        }

        public const int MorningMinutes = 7 * 60;
        public const int NoonMinutes = 13 * 60;
        public const int EveningMinutes = 19 * 60;
        public const int NightMinutes = 22 * 60;

        /// <summary>
        /// The daily greetings: "good morning" at 07:00 and "good night" at 22:00 every day; "good afternoon, shall we
        /// start?" at 13:00 only when the app was not opened that morning, and "good evening, when do we start?" at 19:00
        /// only when it was not opened that afternoon and no lesson was done that day (those two invite the learner in,
        /// so they are not needed once the learner has come). Only today is known, so later days get all four; the plan
        /// is rebuilt every time the app goes to the background.
        /// </summary>
        public static List<PlannedReminder> PlanGreetings(ReminderSettings settings, StreakSystem streak, PlayerProgress progress,
            DateTime now)
        {
            var plan = new List<PlannedReminder>();
            if (settings == null || !settings.greetings) return plan;

            for (int day = 0; day < DaysAhead; day++)
            {
                DateTime date = now.Date.AddDays(day);
                DayActivity record = day == 0 && progress != null ? progress.FindDay(date) : null;
                bool playedToday = day == 0 && streak != null && streak.IsActiveToday(now);

                Add(plan, now, date, MorningMinutes, Loc.Pick("Good morning! ☀", "Sabahınız xeyir! ☀", "Доброе утро! ☀"),
                    Loc.Pick("Ready for today? A short lesson is waiting for you.", "Bu günə hazırsan? Qısa bir dərs səni gözləyir.",
                        "Готов к новому дню? Тебя ждёт короткий урок."));

                if (record == null || !record.openedMorning)
                {
                    Add(plan, now, date, NoonMinutes, Loc.Pick("Good afternoon!", "Günortanız xeyir!", "Добрый день!"),
                        Loc.Pick("Would you like to start? Just 5 minutes is enough.", "Başlamaq istəyirsən? Cəmi 5 dəqiqə bəs edir.",
                            "Хочешь начать? Хватит всего 5 минут."));
                }

                if ((record == null || !record.openedAfternoon) && !playedToday)
                {
                    Add(plan, now, date, EveningMinutes, Loc.Pick("Good evening!", "Axşamınız xeyir!", "Добрый вечер!"),
                        Loc.Pick("When shall we start? Today's lesson is waiting.", "Nə zaman başlayaq? Bugünkü dərs səni gözləyir.",
                            "Когда начнём? Сегодняшний урок ждёт тебя."));
                }

                Add(plan, now, date, NightMinutes, Loc.Pick("Good night! 🌙", "Gecəniz xeyrə qarşı! 🌙", "Спокойной ночи! 🌙"),
                    playedToday
                        ? Loc.Pick("You did great today. See you tomorrow!", "Bu gün əla işlədin. Sabah görüşənədək!",
                            "Сегодня ты молодец. До завтра!")
                        : Loc.Pick("Rest well. Tomorrow we'll learn new signs together.", "Yaxşı dincəl. Sabah yeni işarələri birlikdə öyrənərik.",
                            "Хорошо отдохни. Завтра выучим новые жесты вместе."));
            }

            return plan;
        }

        private static void Add(List<PlannedReminder> plan, DateTime now, DateTime date, int minutes, string title, string body)
        {
            DateTime at = date.AddMinutes(minutes);
            if (at <= now.AddMinutes(1)) return;
            plan.Add(new PlannedReminder { FireAt = at, Title = title, Body = body, Greeting = true });
        }

        private static void Text(int index, int flame, bool lastChance, out string title, out string body)
        {
            if (index == 0)
            {
                title = Loc.T("Məşq vaxtıdır!");
                body = flame > 0
                    ? string.Format(Loc.T("Bu günün dərsini keç, {0} günlük alovun davam etsin."), flame)
                    : Loc.T("Bu gün bir dərs keç, işarə dilini birlikdə öyrənək.");
                return;
            }

            if (index == 1)
            {
                title = Loc.T("Xatırlatma");
                body = Loc.T("Cəmi 5 dəqiqə: bir dərs keç və bugünkü məşqini tamamla.");
                return;
            }

            title = lastChance ? Loc.T("Son şans: alovun sönür!") : Loc.T("Alovun sönməsin!");
            body = lastChance
                ? string.Format(Loc.T("Dünən dərs keçmədin. Bu gün də keçməsən, {0} günlük alovun sönəcək."), flame)
                : string.Format(Loc.T("Bu gün hələ dərs keçməmisən. {0} günlük alovunu qoru."), flame);
        }
    }
}
