using Cevir.Core.Localization;
using UnityEngine;

namespace Cevir.Core.Cloud
{
    /// <summary>Turns Supabase error codes into short messages for the learner.</summary>
    public static class CloudErrors
    {
        public static string Network => Loc.Pick("No internet connection. Check it and try again.", "İnternet bağlantısı yoxdur. Yoxla və yenidən cəhd et.");
        public static string SignedOut => Loc.Pick("Please sign in again.", "Zəhmət olmasa yenidən daxil ol.");
        public static string Unknown => Loc.Pick("Something went wrong. Try again.", "Xəta baş verdi. Yenidən cəhd et.");

        public static string FromBody(string body, long status)
        {
            AuthResponse error = SupabaseClient.Parse<AuthResponse>(body);
            string code = error != null ? (error.error_code ?? error.error ?? string.Empty) : string.Empty;
            string text = error != null ? ((error.msg ?? error.error_description ?? string.Empty).ToLowerInvariant()) : string.Empty;

            if (code == "invalid_credentials" || text.Contains("invalid login"))
                return Loc.Pick("Wrong e-mail or password.", "E-poçt və ya şifrə səhvdir.");
            if (code == "email_not_confirmed" || text.Contains("not confirmed"))
                return Loc.Pick("Confirm your e-mail first (check your inbox).", "Əvvəlcə e-poçtunu təsdiqlə (poçt qutuna bax).");
            if (code == "user_already_exists" || code == "email_exists" || text.Contains("already registered"))
                return Loc.Pick("An account with this e-mail already exists. Sign in instead.", "Bu e-poçtla hesab artıq var. Daxil ol.");
            if (code == "weak_password" || text.Contains("password should"))
                return Loc.Pick("The password must be at least 6 characters.", "Şifrə ən azı 6 simvol olmalıdır.");
            if (code == "validation_failed" || text.Contains("invalid format") || text.Contains("unable to validate email"))
                return Loc.Pick("Check the e-mail address.", "E-poçt ünvanını yoxla.");
            if (code == "over_email_send_rate_limit" || code == "over_request_rate_limit" || status == 429)
                return Loc.Pick("Too many attempts. Wait a minute and try again.", "Çox cəhd oldu. Bir dəqiqə gözlə və yenidən yoxla.");
            if (code == "signup_disabled")
                return Loc.Pick("New accounts are closed right now.", "Hazırda yeni hesab açmaq bağlıdır.");
            if (code == "same_password" || text.Contains("different from the old"))
                return Loc.Pick("The new password must be different from the old one.", "Yeni şifrə köhnəsindən fərqli olmalıdır.");
            if (code == "otp_expired" || code == "otp_disabled" || text.Contains("token has expired") || text.Contains("invalid token"))
                return Loc.Pick("The code is wrong or has expired. Request a new one.", "Kod səhvdir və ya vaxtı keçib. Yenisini istə.");
            if (code == "reauthentication_needed")
                return Loc.Pick("Sign out and sign in again, then change the password.", "Hesabdan çıx, yenidən daxil ol, sonra şifrəni dəyiş.");
            if (status == 401 || status == 403)
                return SignedOut;
            return Unknown;
        }
    }
}
