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
