using System;
using System.Collections.Generic;

namespace Cevir.Core.Localization
{
    /// <summary>
    /// Interface language (English or Azerbaijani). This is the language of buttons and instructions, not the
    /// sign language being learned: an RSL pack can still be taught with an Azerbaijani interface.
    /// </summary>
    public static class Loc
    {
        public const string English = "en";
        public const string Azerbaijani = "az";

        private static string _language = Azerbaijani; // the app is Azerbaijani-first

        public static string Language => _language;
        public static bool IsAz => _language == Azerbaijani;

        /// <summary>Raised after the language changes, so visible labels can refresh.</summary>
        public static event Action<string> LanguageChanged;

        public static void SetLanguage(string code)
        {
            string normalized = code == Azerbaijani ? Azerbaijani : English;
            if (normalized == _language) return;
            _language = normalized;
            if (LanguageChanged != null) LanguageChanged(_language);
        }

        public static string Pick(string en, string az)
        {
            return IsAz && !string.IsNullOrEmpty(az) ? az : en;
        }

        /// <summary>Upper-cases with Azerbaijani rules (i → İ, ı → I), which ToUpperInvariant gets wrong.</summary>
        public static string ToUpperAz(string value)
        {
            if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                chars[i] = c == 'i' ? 'İ' : c == 'ı' ? 'I' : char.ToUpperInvariant(c);
            }

            return new string(chars);
        }

        /// <summary>Upper-cases for the current interface language (Azerbaijani rules when in Azerbaijani).</summary>
        public static string Upper(string value)
        {
            return IsAz ? ToUpperAz(value) : (value ?? string.Empty).ToUpperInvariant();
        }
    }

    /// <summary>A string with an English and an Azerbaijani version, for content written in the inspector.</summary>
    [Serializable]
    public struct LocalizedString
    {
        public string en;
        public string az;

        public LocalizedString(string english, string azerbaijani)
        {
            en = english;
            az = azerbaijani;
        }

        public bool IsEmpty => string.IsNullOrEmpty(en) && string.IsNullOrEmpty(az);

        public string Get()
        {
            return Loc.IsAz && !string.IsNullOrEmpty(az) ? az : (!string.IsNullOrEmpty(en) ? en : (az ?? string.Empty));
        }

        public override string ToString() => Get();
    }

    /// <summary>Per-language title and description override for a curriculum asset.</summary>
    [Serializable]
    public struct LocalizedEntry
    {
        public string language;
        public string title;
        public string description;
    }

    public static class LocalizedEntries
    {
        public static bool TryFind(List<LocalizedEntry> entries, out LocalizedEntry entry)
        {
            entry = default;
            if (entries == null) return false;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].language == Loc.Language)
                {
                    entry = entries[i];
                    return true;
                }
            }

            return false;
        }
    }
}
