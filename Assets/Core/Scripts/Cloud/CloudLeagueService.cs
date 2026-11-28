using System;
using Cevir.Core.Gamification;
using Cevir.Core.Social;


namespace Cevir.Core.Cloud
{
    /// <summary>
    /// Weekly ranking from the server: every learner in the same tier and ISO week, ordered by that week's XP.
    /// <see cref="GetSnapshot"/> returns the last loaded ranking at once (the screen never waits); <see cref="Refresh"/>
    /// loads a new one and raises <see cref="Updated"/>. Finished weeks are settled by the server (settle_league).
    /// </summary>
    public sealed class CloudLeagueService : ILeagueService
    {
        private readonly SupabaseClient _client;
        private readonly GamificationConfig _config;
        private StandingRows _rows;
        private string _rowsWeek;
        private int _rowsTier = -1;
        private bool _loading;
        private readonly System.Collections.Generic.Dictionary<string, StandingRows> _periodRows =
            new System.Collections.Generic.Dictionary<string, StandingRows>();
        private readonly System.Collections.Generic.HashSet<string> _periodLoading = new System.Collections.Generic.HashSet<string>();

        public event Action Updated;

        public CloudLeagueService(SupabaseClient client, GamificationConfig config)
        {
            _client = client;
            _config = config;
        }

        public bool SettlesOnServer => true;
        public bool HasData => _rows != null;

        public LeagueSnapshot GetSnapshot(int tier, string youName, int youWeeklyXp, DateTime localNow)
        {
            string week = StreakSystem.IsoWeek(localNow);
            var snapshot = new LeagueSnapshot
            {
                Tier = LeagueTiers.Clamp(tier, _config.leagueTiers),
                TimeLeft = LocalLeagueService.TimeUntilWeekEnd(localNow),
                Offline = _rows == null || _rowsWeek != week || _rowsTier != tier
            };

            if (!snapshot.Offline)
            {
                foreach (StandingRow row in _rows.items)
                {
                    if (row.is_you) continue; // the learner's own row uses the live local XP below
                    string name = string.IsNullOrEmpty(row.display_name) ? "?" : row.display_name;
                    snapshot.Entries.Add(Entry(row));
                }
            }

            snapshot.Entries.Add(LocalLeagueService.You(youName, youWeeklyXp));
            LocalLeagueService.Rank(snapshot, _config);
            return snapshot;
        }

        /// <summary>
        /// Monthly / yearly ranking of everyone by XP in that period (top 50 from the server). The learner's row uses
        /// the live local XP; when they are below the loaded rows, their place comes from the server.
        /// </summary>
        public LeagueSnapshot GetPeriodSnapshot(LeaguePeriod period, string youName, int youXp, DateTime localNow)
        {
            string key = LocalLeagueService.PeriodKey(period, localNow);
            StandingRows rows;
            var snapshot = new LeagueSnapshot
            {
                Period = period,
                TimeLeft = LocalLeagueService.TimeUntilPeriodEnd(period, localNow),
                Offline = !_periodRows.TryGetValue(key, out rows) || rows == null
            };

            int serverRank = 0, others = 0;
            if (!snapshot.Offline)
            {
                foreach (StandingRow row in rows.items)
                {
                    if (row.is_you)
                    {
                        serverRank = row.rank;
                        continue;
                    }

                    snapshot.Entries.Add(Entry(row));
                    others++;
                }
            }

            LeagueEntry you = LocalLeagueService.You(youName, youXp);
            snapshot.Entries.Add(you);
            LocalLeagueService.RankPlain(snapshot);
            // Only the top of the ranking is loaded: below it, the server knows the learner's place.
            if (others >= PeriodTop && you.Rank == snapshot.Entries.Count && serverRank > you.Rank) you.Rank = serverRank;
            return snapshot;
        }

        private const int PeriodTop = 50;

        private LeagueEntry Entry(StandingRow row)
        {
            string name = string.IsNullOrEmpty(row.display_name) ? "?" : row.display_name;
            return new LeagueEntry
            {
                DisplayName = name,
                Initials = LocalLeagueService.InitialsOf(name),
                Xp = row.xp,
                AvatarColor = AvatarColor(name),
                AvatarUrl = row.avatar_url ?? string.Empty
            };
        }

        public void Refresh(int tier, DateTime localNow)
        {
            RefreshPeriod(LocalLeagueService.PeriodKey(LeaguePeriod.Month, localNow));
            RefreshPeriod(LocalLeagueService.PeriodKey(LeaguePeriod.Year, localNow));
            RefreshWeek(tier, localNow);
        }

        private void RefreshPeriod(string key)
        {
            if (!_client.HasSession || _periodLoading.Contains(key)) return;
            _periodLoading.Add(key);
            _client.Rpc("period_standings", UnityEngine.JsonUtility.ToJson(new PeriodStandingsRequest { p_period = key }), response =>
            {
                _periodLoading.Remove(key);
                if (!response.Ok) return; // e.g. schema.sql not run again yet: the screen says the ranking is offline
                _periodRows[key] = SupabaseClient.ParseArray<StandingRows>(response.Body);
                if (Updated != null) Updated();
            });
        }

        private void RefreshWeek(int tier, DateTime localNow)
        {
            if (_loading || !_client.HasSession) return;
            _loading = true;
            string week = StreakSystem.IsoWeek(localNow);
            var request = new StandingsRequest { p_week = week, p_tier = tier };
            _client.Rpc("league_standings", UnityEngine.JsonUtility.ToJson(request), response =>
            {
                _loading = false;
                if (!response.Ok) return;
                _rows = SupabaseClient.ParseArray<StandingRows>(response.Body);
                _rowsWeek = week;
                _rowsTier = tier;
                if (Updated != null) Updated();
            });
        }

        public void Clear()
        {
            _periodRows.Clear();
            _rows = null;
            _rowsWeek = null;
            _rowsTier = -1;
        }

        private static readonly UnityEngine.Color32[] Palette =
        {
            new UnityEngine.Color32(0x00, 0x6E, 0xF0, 0xFF), new UnityEngine.Color32(0x00, 0x2A, 0xC4, 0xFF),
            new UnityEngine.Color32(0x1E, 0xB9, 0xF0, 0xFF), new UnityEngine.Color32(0xFF, 0x8A, 0x3D, 0xFF),
            new UnityEngine.Color32(0x2B, 0xB6, 0x73, 0xFF), new UnityEngine.Color32(0xA3, 0x6B, 0xDB, 0xFF)
        };

        private static UnityEngine.Color AvatarColor(string name)
        {
            int hash = 0;
            foreach (char c in name) hash = hash * 31 + c;
            return Palette[(hash & 0x7fffffff) % Palette.Length];
        }
    }
}
