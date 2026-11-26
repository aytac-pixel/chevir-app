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

    public sealed class LeagueEntry
    {
        public int Rank;
        public string DisplayName;
        public string Initials;
        public int Xp;
        public Color AvatarColor;
        public bool IsYou;
        public LeagueZone Zone;
    }

    public sealed class LeagueSnapshot
    {
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

        public static LeagueEntry You(string youName, int youWeeklyXp)
        {
            return new LeagueEntry
            {
                DisplayName = Loc.Pick("You", "Sən"),
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
