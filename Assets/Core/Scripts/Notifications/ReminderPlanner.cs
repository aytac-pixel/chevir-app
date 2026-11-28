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

                int weekday = ((int)date.DayOfWeek + 6) % 7; // Monday = 0
                AddSplit(plan, now, date, MorningMinutes, Loc.Pick(MorningEn[weekday], MorningAz[weekday], MorningRu[weekday]));

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

                AddSplit(plan, now, date, NightMinutes, playedToday
                    ? Loc.Pick(NightPlayedEn[weekday], NightPlayedAz[weekday], NightPlayedRu[weekday])
                    : Loc.Pick(NightNotPlayedEn[weekday], NightNotPlayedAz[weekday], NightNotPlayedRu[weekday]));
            }

            return plan;
        }

        // The 07:00 and 22:00 messages change with the day of the week (Monday first); 22:00 depends on whether the
        // learner did a lesson that day. Texts chosen by Gulshan (2026-11-28), with English and Russian versions.
        private static readonly string[] MorningAz =
        {
            "Sabahınız xeyir! Yeni həftə, yeni işarələr. Hazırsan? ☀",
            "Sabahınız xeyir! Bu gün bir işarə öyrənməyə nə deyirsən?",
            "Günaydın! Əllərin isinsin, dərs səni gözləyir 👋",
            "Sabahınız xeyir! Alovunu söndürmə, bu gün də davam 🔥",
            "Xeyirli səhərlər! 5 dəqiqə də kifayətdir, başlayaq?",
            "Sabahınız xeyir! Həftə sonu öyrənmək üçün ən yaxşı vaxtdır 🌤",
            "Günaydın! Həftəni gözəl bitirək, bir dərs keçək?"
        };

        private static readonly string[] NightNotPlayedAz =
        {
            "Gecəniz xeyrə qarşı! Sabah yenidən başlayaq 🌙",
            "Gecəniz xeyrə qarşı! Sabah bir işarə səni gözləyir.",
            "Şirin yuxular! Sabah birlikdə davam edək ✨",
            "Gecəniz xeyrə qarşı! Alovunu sabah yandırmağı unutma 🔥",
            "Gecəniz xeyrə qarşı! Sabah yeni bir gün, yeni bir dərs.",
            "Rahat yuxular! Sabah 5 dəqiqə ayıraq? 🌙",
            "Gecəniz xeyrə qarşı! Yeni həftəyə hazır ol."
        };

        private static readonly string[] NightPlayedAz =
        {
            "Gecəniz xeyrə qarşı! Bu gün əla işlədin 🌟",
            "Afərin! Bu gün də öyrəndin. Şirin yuxular 🌙",
            "Gecəniz xeyrə qarşı! Alovun yanır, sabah da davam 🔥",
            "Bu gün çox yaxşı idin! Rahat yuxular ✨",
            "Gecəniz xeyrə qarşı! Hər gün bir az daha irəlidəsən.",
            "Əla iş! Gecəniz xeyrə qarşı 🌟",
            "Həftəni gözəl bitirdin! Gecəniz xeyrə qarşı 🌙"
        };

        private static readonly string[] MorningEn =
        {
            "Good morning! New week, new signs. Ready? ☀",
            "Good morning! How about learning one sign today?",
            "Morning! Warm up your hands, a lesson is waiting 👋",
            "Good morning! Keep your flame alive, let's go on today too 🔥",
            "Have a nice morning! 5 minutes is enough, shall we start?",
            "Good morning! The weekend is the best time to learn 🌤",
            "Morning! Let's end the week well, one lesson?"
        };

        private static readonly string[] MorningRu =
        {
            "Доброе утро! Новая неделя, новые жесты. Готов? ☀",
            "Доброе утро! Как насчёт выучить сегодня один жест?",
            "С добрым утром! Разомни руки, урок ждёт 👋",
            "Доброе утро! Не дай огоньку погаснуть, продолжаем и сегодня 🔥",
            "Хорошего утра! Хватит и 5 минут, начнём?",
            "Доброе утро! Выходные — лучшее время учиться 🌤",
            "С добрым утром! Закончим неделю красиво, один урок?"
        };

        private static readonly string[] NightNotPlayedEn =
        {
            "Good night! Let's start again tomorrow 🌙",
            "Good night! A sign is waiting for you tomorrow.",
            "Sweet dreams! Let's continue together tomorrow ✨",
            "Good night! Don't forget to light your flame tomorrow 🔥",
            "Good night! Tomorrow is a new day, a new lesson.",
            "Sleep well! Shall we take 5 minutes tomorrow? 🌙",
            "Good night! Get ready for a new week."
        };

        private static readonly string[] NightNotPlayedRu =
        {
            "Спокойной ночи! Завтра начнём снова 🌙",
            "Спокойной ночи! Завтра тебя ждёт новый жест.",
            "Сладких снов! Завтра продолжим вместе ✨",
            "Спокойной ночи! Не забудь завтра зажечь огонёк 🔥",
            "Спокойной ночи! Завтра новый день и новый урок.",
            "Спокойного сна! Найдём завтра 5 минут? 🌙",
            "Спокойной ночи! Готовься к новой неделе."
        };

        private static readonly string[] NightPlayedEn =
        {
            "Good night! You did great today 🌟",
            "Well done! You learned today too. Sweet dreams 🌙",
            "Good night! Your flame is burning, let's go on tomorrow 🔥",
            "You were great today! Sleep well ✨",
            "Good night! Every day you are a bit further ahead.",
            "Great job! Good night 🌟",
            "You ended the week beautifully! Good night 🌙"
        };

        private static readonly string[] NightPlayedRu =
        {
            "Спокойной ночи! Сегодня ты отлично поработал 🌟",
            "Молодец! Сегодня ты тоже учился. Сладких снов 🌙",
            "Спокойной ночи! Огонёк горит, завтра продолжим 🔥",
            "Сегодня ты был молодцом! Спокойного сна ✨",
            "Спокойной ночи! С каждым днём ты всё дальше.",
            "Отличная работа! Спокойной ночи 🌟",
            "Ты красиво закончил неделю! Спокойной ночи 🌙"
        };

        /// <summary>One message as title (up to the first "!") and text (the rest).</summary>
        private static void AddSplit(List<PlannedReminder> plan, DateTime now, DateTime date, int minutes, string message)
        {
            int cut = message.IndexOf('!');
            string title = cut > 0 ? message.Substring(0, cut + 1) : "Chevir I/O";
            string body = cut > 0 ? message.Substring(cut + 1).Trim() : message;
            Add(plan, now, date, minutes, title, body.Length > 0 ? body : message);
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
