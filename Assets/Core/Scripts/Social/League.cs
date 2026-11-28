using System;
using System.Collections.Generic;
using Cevir.Core.Gamification;
using Cevir.Core.Localization;
using UnityEngine;

namespace Cevir.Core.Social
{
    public enum LeagueZone
    {
        Stay,
        Promote,
        Demote
    }

    /// <summary>The three rankings on the League page: this week (the tier league), this month and this year.</summary>
    public enum LeaguePeriod
    {
        Week,
        Month,
        Year
    }

    public sealed class LeagueEntry
    {
        public int Rank;
        /// <summary>Profile photo address (public "avatars" bucket); empty shows the initials.</summary>
        public string AvatarUrl = "";
        public string DisplayName;
        public string Initials;
        public int Xp;
        public Color AvatarColor;
        public bool IsYou;
        public LeagueZone Zone;
    }

    public sealed class LeagueSnapshot
    {
        public LeaguePeriod Period;
        public int Tier;
        public TimeSpan TimeLeft;
        public int PromoteCount;
        public int DemoteCount;

        /// <summary>True when the ranking could not be loaded from the server (only the learner is listed).</summary>
        public bool Offline;
        public readonly List<LeagueEntry> Entries = new List<LeagueEntry>();

        public LeagueEntry You
        {
            get
            {
                foreach (LeagueEntry e in Entries) if (e.IsYou) return e;
                return null;
            }
        }
    }

    /// <summary>Weekly leaderboard source: the server ranking when signed in, <see cref="LocalLeagueService"/> otherwise.</summary>
    public interface ILeagueService
    {
        /// <summary>Standings of the learner's group. <paramref name="youWeeklyXp"/> is the learner's XP this week.</summary>
        LeagueSnapshot GetSnapshot(int tier, string youName, int youWeeklyXp, DateTime localNow);

        /// <summary>
        /// Monthly or yearly ranking of all learners by the XP earned in that month / year (no tiers, no zones).
        /// <paramref name="youXp"/> is the learner's XP in that period.
        /// </summary>
        LeagueSnapshot GetPeriodSnapshot(LeaguePeriod period, string youName, int youXp, DateTime localNow);

        /// <summary>True when finished weeks are settled by the server (promotion needs the whole group's results).</summary>
        bool SettlesOnServer { get; }
    }

    public static class LeagueTiers
    {
        public static readonly Color[] Colors =
        {
            new Color32(0xC0, 0x7A, 0x45, 0xFF), // bronze
            new Color32(0x9A, 0xA8, 0xBA, 0xFF), // silver
            new Color32(0xF2, 0xB5, 0x2E, 0xFF), // gold
            new Color32(0x2E, 0x7B, 0xF0, 0xFF), // sapphire
            new Color32(0x7C, 0xD3, 0xF7, 0xFF)  // diamond
        };

        private static readonly string[] En = { "Bronze", "Silver", "Gold", "Sapphire", "Diamond" };
        private static readonly string[] Az = { "Bürünc", "Gümüş", "Qızıl", "Sapfir", "Almaz" };
        private static readonly string[] Ru = { "Бронза", "Серебро", "Золото", "Сапфир", "Алмаз" };
        private static readonly string[] RuLeague = { "Бронзовая лига", "Серебряная лига", "Золотая лига", "Сапфировая лига", "Алмазная лига" };

        public static int Clamp(int tier, int tierCount) => Mathf.Clamp(tier, 0, Mathf.Max(0, Mathf.Min(tierCount, En.Length) - 1));
        public static string Name(int tier) => Loc.Pick(En[Clamp(tier, En.Length)], Az[Clamp(tier, Az.Length)], Ru[Clamp(tier, Ru.Length)]);
        public static string LeagueName(int tier) => Loc.Pick(Name(tier) + " League", Name(tier) + " Liqası", RuLeague[Clamp(tier, RuLeague.Length)]);
        public static Color Color(int tier) => Colors[Clamp(tier, Colors.Length)];
    }

    /// <summary>
    /// League without a connection to the server: only the learner's own row (real weekly XP) is shown and the
    /// screen says the ranking needs the internet. Weeks end on Monday 00:00 local time (ISO weeks, like the XP counter).
    /// </summary>
    public sealed class LocalLeagueService : ILeagueService
    {
        private readonly GamificationConfig _config;

        public LocalLeagueService(GamificationConfig config)
        {
            _config = config;
        }

        public bool SettlesOnServer => false;

        public LeagueSnapshot GetSnapshot(int tier, string youName, int youWeeklyXp, DateTime localNow)
        {
            var snapshot = new LeagueSnapshot
            {
                Tier = LeagueTiers.Clamp(tier, _config.leagueTiers),
                TimeLeft = TimeUntilWeekEnd(localNow),
                Offline = true
            };
            snapshot.Entries.Add(You(youName, youWeeklyXp));
            Rank(snapshot, _config);
            return snapshot;
        }

        public LeagueSnapshot GetPeriodSnapshot(LeaguePeriod period, string youName, int youXp, DateTime localNow)
        {
            var snapshot = new LeagueSnapshot { Period = period, TimeLeft = TimeUntilPeriodEnd(period, localNow), Offline = true };
            snapshot.Entries.Add(You(youName, youXp));
            RankPlain(snapshot);
            return snapshot;
        }

        /// <summary>The learner's row: their own name with "(you)" after it, e.g. "Aysel Məmmədova (sən)".</summary>
        public static string YouLabel(string youName)
        {
            string you = Loc.Pick("you", "sən", "ты");
            return string.IsNullOrEmpty(youName) ? Loc.Pick("You", "Sən", "Ты") : youName + " (" + you + ")";
        }

        public static LeagueEntry You(string youName, int youWeeklyXp)
        {
            return new LeagueEntry
            {
                DisplayName = YouLabel(youName),
                Initials = string.IsNullOrEmpty(youName) ? Loc.Pick("Y", "S") : InitialsOf(youName),
                Xp = youWeeklyXp,
                AvatarColor = new Color32(0x00, 0x6E, 0xF0, 0xFF),
                IsYou = true
            };
        }

        /// <summary>
        /// Sorts the group and marks promotion / demotion zones. Small groups move fewer people (a third of the group at
        /// most), so a learner alone in a league is never promoted or demoted just for being there.
        /// </summary>
        public static void Rank(LeagueSnapshot snapshot, GamificationConfig config)
        {
            // Ties go to the learner, which is what the screenshot of the design shows and feels fair.
            snapshot.Entries.Sort((a, b) => a.Xp != b.Xp ? b.Xp.CompareTo(a.Xp) : (a.IsYou ? -1 : b.IsYou ? 1 : 0));
            int count = snapshot.Entries.Count;
            snapshot.PromoteCount = Math.Min(config.leaguePromoteCount, count / 3);
            snapshot.DemoteCount = Math.Min(config.leagueDemoteCount, count / 3);
            for (int i = 0; i < count; i++)
            {
                LeagueEntry e = snapshot.Entries[i];
                e.Rank = i + 1;
                // The demotion zone is shown in Bronze too (as in the design); settlement just can't go lower.
                if (i < snapshot.PromoteCount) e.Zone = LeagueZone.Promote;
                else if (i >= count - snapshot.DemoteCount) e.Zone = LeagueZone.Demote;
                else e.Zone = LeagueZone.Stay;
            }
        }

        /// <summary>Sorts by XP (ties to the learner) and numbers the rows; monthly / yearly rankings have no zones.</summary>
        public static void RankPlain(LeagueSnapshot snapshot)
        {
            snapshot.Entries.Sort((a, b) => a.Xp != b.Xp ? b.Xp.CompareTo(a.Xp) : (a.IsYou ? -1 : b.IsYou ? 1 : 0));
            snapshot.PromoteCount = snapshot.DemoteCount = 0;
            for (int i = 0; i < snapshot.Entries.Count; i++)
            {
                snapshot.Entries[i].Rank = i + 1;
                snapshot.Entries[i].Zone = LeagueZone.Stay;
            }
        }

        public static TimeSpan TimeUntilPeriodEnd(LeaguePeriod period, DateTime localNow)
        {
            if (period == LeaguePeriod.Week) return TimeUntilWeekEnd(localNow);
            DateTime end = period == LeaguePeriod.Month
                ? new DateTime(localNow.Year, localNow.Month, 1).AddMonths(1)
                : new DateTime(localNow.Year + 1, 1, 1);
            return end - localNow;
        }

        /// <summary>"2026-11" for the month, "2026" for the year: the keys of the server's period_xp table.</summary>
        public static string PeriodKey(LeaguePeriod period, DateTime localNow)
        {
            return period == LeaguePeriod.Month
                ? localNow.Year.ToString("0000") + "-" + localNow.Month.ToString("00")
                : localNow.Year.ToString("0000");
        }

        public static TimeSpan TimeUntilWeekEnd(DateTime localNow)
        {
            int daysFromMonday = ((int)localNow.DayOfWeek + 6) % 7;
            DateTime nextMonday = localNow.Date.AddDays(7 - daysFromMonday);
            return nextMonday - localNow;
        }

        /// <summary>"2 days left" / "5 hours left", in the interface language.</summary>
        public static string FormatTimeLeft(TimeSpan left)
        {
            if (left.TotalDays >= 1)
            {
                int days = (int)Math.Floor(left.TotalDays);
                return Loc.Pick(days == 1 ? "1 day left" : days + " days left", days + " gün qalıb",
                    Loc.RussianPlural(days, "Остался {0} день", "Осталось {0} дня", "Осталось {0} дней"));
            }

            int hours = Math.Max(1, (int)Math.Ceiling(left.TotalHours));
            return Loc.Pick(hours == 1 ? "1 hour left" : hours + " hours left", hours + " saat qalıb",
                Loc.RussianPlural(hours, "Остался {0} час", "Осталось {0} часа", "Осталось {0} часов"));
        }

        public static string InitialsOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            string[] parts = name.Replace(".", " ").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            string first = Loc.ToUpperAz(parts[0].Substring(0, 1));
            return parts.Length > 1 ? first + Loc.ToUpperAz(parts[parts.Length - 1].Substring(0, 1)) : first;
        }
    }

    /// <summary>
    /// Settles finished league weeks: top <c>leaguePromoteCount</c> move up a tier, bottom
    /// <c>leagueDemoteCount</c> move down (never below Bronze).
    /// </summary>
    public static class LeagueSettlement
    {
        /// <summary>
        /// Settles the week held in <paramref name="xp"/> once it has ended (call before XP is added in a new week).
        /// Returns the tier change: -1, 0 or +1. <paramref name="settledWeek"/> is the last week already settled.
        /// </summary>
        public static int Settle(ref int tier, ref string settledWeek, XpState xp, string currentWeek, DateTime localNow,
            ILeagueService service, GamificationConfig config)
        {
            // Only a week that has ended and was not settled yet; weeks without any XP are simply skipped.
            if (string.IsNullOrEmpty(xp.week) || xp.week == currentWeek || xp.week == settledWeek) return 0;

            int change = 0;
            LeagueEntry you = service.GetSnapshot(tier, null, xp.weekly, localNow).You;
            if (you != null && you.Zone == LeagueZone.Promote && tier < config.leagueTiers - 1) change = 1;
            else if (you != null && you.Zone == LeagueZone.Demote && tier > 0) change = -1;
            tier += change;
            settledWeek = xp.week;
            return change;
        }
    }
}
