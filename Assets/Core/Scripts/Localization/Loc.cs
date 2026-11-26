using System;
using System.Collections.Generic;

namespace Cevir.Core.Localization
{
    /// <summary>
    /// Interface language: Azerbaijani (default), English or Russian. This is the language of buttons and
    /// instructions, not the sign language being learned.
    /// Code passes English and Azerbaijani text (<see cref="Pick(string,string)"/>) or Azerbaijani only
    /// (<see cref="T"/>); the other languages come from <see cref="Translations"/>, keyed by the Azerbaijani text.
    /// </summary>
    public static class Loc
    {
        public const string English = "en";
        public const string Azerbaijani = "az";
        public const string Russian = "ru";

        /// <summary>Interface languages in the order the language picker shows them.</summary>
        public static readonly string[] Supported = { Azerbaijani, English, Russian };

        private static string _language = Azerbaijani; // the app is Azerbaijani-first

        public static string Language => _language;
        public static bool IsAz => _language == Azerbaijani;
        public static bool IsRu => _language == Russian;
        public static bool IsEn => _language == English;

        /// <summary>Raised after the language changes, so visible labels can refresh.</summary>
        public static event Action<string> LanguageChanged;

        /// <summary>Known code ("az", "en", "ru"), anything else becomes Azerbaijani.</summary>
        public static string Normalize(string code)
        {
            return code == English || code == Russian ? code : Azerbaijani;
        }

        public static void SetLanguage(string code)
        {
            string normalized = Normalize(code);
            if (normalized == _language) return;
            _language = normalized;
            if (LanguageChanged != null) LanguageChanged(_language);
        }

        /// <summary>Text for the current language from an English and an Azerbaijani version (Russian from the table).</summary>
        public static string Pick(string en, string az)
        {
            switch (_language)
            {
                case Azerbaijani:
                    return !string.IsNullOrEmpty(az) ? az : (en ?? string.Empty);
                case Russian:
                    return Translations.Russian(az) ?? Translations.Russian(en) ?? (!string.IsNullOrEmpty(en) ? en : (az ?? string.Empty));
                default:
                    return !string.IsNullOrEmpty(en) ? en : (Translations.English(az) ?? az ?? string.Empty);
            }
        }

        /// <summary>Text with all three versions given in code.</summary>
        public static string Pick(string en, string az, string ru)
        {
            if (IsRu && !string.IsNullOrEmpty(ru)) return ru;
            return Pick(en, az);
        }

        /// <summary>Azerbaijani source text in the current interface language.</summary>
        public static string T(string az)
        {
            return Pick(null, az);
        }

        /// <summary>Russian noun form for a count: 1 день, 2 дня, 5 дней ({0} is the number).</summary>
        public static string RussianPlural(int count, string one, string few, string many)
        {
            int n = Math.Abs(count) % 100;
            int last = n % 10;
            string form = n >= 11 && n <= 14 ? many : last == 1 ? one : last >= 2 && last <= 4 ? few : many;
            return string.Format(form, count);
        }

        /// <summary>Native name of an interface language, for the language picker.</summary>
        public static string NativeName(string code)
        {
            switch (code)
            {
                case English: return "English";
                case Russian: return "Русский";
                default: return "Azərbaycanca";
            }
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

        /// <summary>Lower-cases with Azerbaijani rules (İ → i, I → ı).</summary>
        public static string ToLowerAz(string value)
        {
            if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                chars[i] = c == 'İ' ? 'i' : c == 'I' ? 'ı' : char.ToLowerInvariant(c);
            }

            return new string(chars);
        }

        /// <summary>Upper-cases for the current interface language (Azerbaijani rules when in Azerbaijani).</summary>
        public static string Upper(string value)
        {
            return IsAz ? ToUpperAz(value) : (value ?? string.Empty).ToUpperInvariant();
        }

        /// <summary>Culture for dates and numbers in the current interface language.</summary>
        public static System.Globalization.CultureInfo Culture
        {
            get
            {
                try
                {
                    return System.Globalization.CultureInfo.GetCultureInfo(IsAz ? "az-Latn-AZ" : IsRu ? "ru-RU" : "en-US");
                }
                catch (Exception)
                {
                    return System.Globalization.CultureInfo.InvariantCulture;
                }
            }
        }
    }

    /// <summary>
    /// A string in English, Azerbaijani and (optionally) Russian, for content written in the inspector. An empty
    /// Russian version comes from <see cref="Translations"/>.
    /// </summary>
    [Serializable]
    public struct LocalizedString
    {
        public string en;
        public string az;
        public string ru;

        public LocalizedString(string english, string azerbaijani)
        {
            en = english;
            az = azerbaijani;
            ru = null;
        }

        public LocalizedString(string english, string azerbaijani, string russian)
        {
            en = english;
            az = azerbaijani;
            ru = russian;
        }

        public bool IsEmpty => string.IsNullOrEmpty(en) && string.IsNullOrEmpty(az);

        public string Get()
        {
            return Loc.Pick(en, az, ru);
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
            if (Find(entries, Loc.Language, out entry)) return true;

            // Russian titles not stored in the asset come from the table, through the Azerbaijani entry.
            LocalizedEntry az;
            if (Loc.IsRu && Find(entries, Loc.Azerbaijani, out az))
            {
                entry = new LocalizedEntry
                {
                    language = Loc.Russian,
                    title = Translations.Russian(az.title) ?? az.title,
                    description = string.IsNullOrEmpty(az.description) ? az.description : (Translations.Russian(az.description) ?? az.description)
                };
                return true;
            }

            return false;
        }

        private static bool Find(List<LocalizedEntry> entries, string language, out LocalizedEntry entry)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].language == language)
                {
                    entry = entries[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }
    }
}
