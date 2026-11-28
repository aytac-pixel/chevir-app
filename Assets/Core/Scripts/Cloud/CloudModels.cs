using System;
using System.Collections.Generic;

namespace Cevir.Core.Cloud
{
    // Plain data for JsonUtility. Field names match the JSON of Supabase Auth / PostgREST and Tools/supabase/schema.sql.

    [Serializable]
    public sealed class AuthUser
    {
        public string id;
        public string email;
    }

    [Serializable]
    public sealed class AuthResponse
    {
        public string access_token;
        public string refresh_token;
        public long expires_in;
        public long expires_at;
        public AuthUser user;

        // Sign-up with e-mail confirmation turned on returns the user itself (no tokens).
        public string id;
        public string email;

        // Errors come in two shapes depending on the endpoint.
        public string error;
        public string error_description;
        public string error_code;
        public string msg;
    }

    [Serializable]
    public sealed class SignUpRequest
    {
        public string email;
        public string password;
        public SignUpData data;
    }

    [Serializable]
    public sealed class SignUpData
    {
        public string display_name;
        public string name_sign;
        public bool data_consent;
    }

    [Serializable]
    public sealed class PasswordGrant
    {
        public string email;
        public string password;
    }

    [Serializable]
    public sealed class VerifyRequest
    {
        public string type;
        public string email;
        public string token;
    }

    [Serializable]
    public sealed class PasswordUpdate
    {
        public string password;
    }

    [Serializable]
    public sealed class EmailUpdate
    {
        public string email;
    }

    [Serializable]
    public sealed class RefreshGrant
    {
        public string refresh_token;
    }

    [Serializable]
    public sealed class EmailOnly
    {
        public string email;
    }

    [Serializable]
    public sealed class ProgressRow
    {
        public string user_id;
        public string data;
        public long client_updated;
    }

    [Serializable]
    public sealed class ProfileRow
    {
        public string id;
        public string display_name;
        public int league_tier;
        public int total_xp;
        public int streak;
        public string avatar_url;
        public bool reminders_on;
        public string practice_from;
        public string practice_to;
    }

    /// <summary>Only the name sign column of a profile (sent on its own, see CloudService.UploadNow).</summary>
    [Serializable]
    public sealed class ProfileNameSignRow
    {
        public string id;
        public string name_sign;
    }

    /// <summary>The learner's answer to «Məlumatlarımla Chevir-i inkişaf etdirməyə kömək et» (sign samples).</summary>
    [Serializable]
    public sealed class ProfileConsentRow
    {
        public string id;
        public bool data_consent;
    }

    [Serializable]
    public sealed class SignSampleVideoRow
    {
        public string video_path;
    }

    [Serializable]
    public sealed class SignSampleVideoRows
    {
        public List<SignSampleVideoRow> items = new List<SignSampleVideoRow>();
    }

    [Serializable]
    public sealed class WeeklyXpRow
    {
        public string user_id;
        public string week;
        public int tier;
        public int xp;
    }

    /// <summary>XP in one month ("2026-11") or year ("2026"): the monthly and yearly rankings.</summary>
    [Serializable]
    public sealed class PeriodXpRow
    {
        public string user_id;
        public string period;
        public int xp;
    }

    [Serializable]
    public sealed class StandingRow
    {
        public string display_name;
        public string avatar_url;
        public int xp;
        public bool is_you;
        /// <summary>Place in the whole ranking (period_standings only; 0 from league_standings).</summary>
        public int rank;
    }

    [Serializable]
    public sealed class StandingsRequest
    {
        public string p_week;
        public int p_tier;
    }

    [Serializable]
    public sealed class PeriodStandingsRequest
    {
        public string p_period;
    }

    [Serializable]
    public sealed class SettleRequest
    {
        public string p_week;
    }

    [Serializable]
    public sealed class SettleResponse
    {
        public int tier;
        public int rank;
        public int group_size;
    }

    [Serializable]
    public sealed class ProgressRows
    {
        public List<ProgressRow> items = new List<ProgressRow>();
    }

    [Serializable]
    public sealed class StandingRows
    {
        public List<StandingRow> items = new List<StandingRow>();
    }

    [Serializable]
    public sealed class SettleRows
    {
        public List<SettleResponse> items = new List<SettleResponse>();
    }
}
