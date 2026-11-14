using System.Collections.Generic;
using System.Text;
using Cevir.Core.LanguagePacks;
using Cevir.Core.Recognition;
using UnityEngine;

namespace Cevir.LanguagePacks.AzSLD
{
    /// <summary>
    /// Azərbaycan İşarət Dili (AzSLD / AİD) pack. Content (levels, signs, references, rules, model) is data
    /// in this folder; this class only supplies AzSLD defaults and ids.
    /// Dataset: AzSLD (ADA University) + the dactyl alphabet dataset. See the licence note in the manifest:
    /// commercial release needs written permission from ADA University first.
    /// </summary>
    [CreateAssetMenu(menuName = "Çevir/Language Pack/AzSLD Pack", fileName = "AzSLD_LanguagePack")]
    public sealed class AzSLDLanguagePack : LanguagePackDefinition
    {
        public const string Code = "azsld";

        /// <summary>Azerbaijani Latin alphabet, in dictionary order (32 letters).</summary>
        public static readonly string[] Alphabet =
        {
            "A", "B", "C", "Ç", "D", "E", "Ə", "F", "G", "Ğ", "H", "X", "I", "İ", "J", "K",
            "Q", "L", "M", "N", "O", "Ö", "P", "R", "S", "Ş", "T", "U", "Ü", "V", "Y", "Z"
        };

        public static readonly string[] Numerals = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        private static readonly Dictionary<char, string> AsciiMap = new Dictionary<char, string>
        {
            { 'ç', "ch" }, { 'ə', "ae" }, { 'ğ', "gh" }, { 'ı', "ii" }, { 'i', "i" },
            { 'ö', "oe" }, { 'ş', "sh" }, { 'ü', "ue" }
        };

        protected override void ApplyDefaults(LanguagePackManifest target)
        {
            target.languageCode = Code;
            target.displayName = "Azərbaycan İşarət Dili";
            target.englishName = "Azerbaijani Sign Language";
            target.badge = "AZ";
            target.uiLanguage = "az";
            target.alphabet = new List<string>(Alphabet);
            target.numerals = new List<string>(Numerals);
            target.datasetLicense = "AzSLD: CC BY 4.0 (Zenodo) vs CC BY-NC (paper) — unresolved";
            target.commercialUseCleared = false;
            target.communityReviewed = false;
        }

        public override MatchResult PostProcess(MatchResult result, SignDefinition target)
        {
            // Hook for AzSLD-specific adjustments (e.g. letter pairs the community flags as easily confused).
            return result;
        }

        /// <summary>Stable ASCII id for a fingerspelled letter: "Ç" → "azsld.letter.ch", "I" → "azsld.letter.ii".</summary>
        public static string LetterId(string letter)
        {
            // "I" (dotless) and "İ" (dotted) must not collide, so lower-case with Azerbaijani rules first.
            string lower = letter == "I" ? "ı" : letter == "İ" ? "i" : letter.ToLowerInvariant();
            var sb = new StringBuilder(Code + ".letter.");
            foreach (char c in lower)
            {
                string mapped;
                sb.Append(AsciiMap.TryGetValue(c, out mapped) ? mapped : c.ToString());
            }

            return sb.ToString();
        }

        /// <summary>Stable ASCII id for a word or phrase gloss: "Sağ ol" → "azsld.word.sagh_ol".</summary>
        public static string WordId(string gloss)
        {
            string lower = gloss.Replace("I", "ı").Replace("İ", "i").ToLowerInvariant();
            var sb = new StringBuilder(Code + ".word.");
            foreach (char c in lower)
            {
                string mapped;
                if (AsciiMap.TryGetValue(c, out mapped)) sb.Append(mapped);
                else if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (sb[sb.Length - 1] != '_') sb.Append('_');
            }

            return sb.ToString().TrimEnd('_');
        }

        private static readonly Dictionary<char, string> MediaLetterMap = new Dictionary<char, string>
        {
            { 'ç', "ch" }, { 'ə', "ae" }, { 'ğ', "gh" }, { 'ı', "i_noqtesiz" }, { 'i', "i" },
            { 'ö', "oe" }, { 'ş', "sh" }, { 'ü', "ue" }
        };

        private static readonly Dictionary<char, string> MediaWordMap = new Dictionary<char, string>
        {
            { 'ç', "ch" }, { 'ə', "e" }, { 'ğ', "gh" }, { 'ı', "i" }, { 'i', "i" },
            { 'ö', "o" }, { 'ş', "sh" }, { 'ü', "u" }
        };

        /// <summary>
        /// Media file name (without extension) the content team uses for a sign, as in kurikulum/Media_siyahisi_A1_A2.csv:
        /// letters "herf_a", "herf_ch", "herf_i_noqtesiz" (I), "herf_i" (İ); numbers "reqem_5"; words "sagh_ol"; sentences "cumle_menim_adim".
        /// </summary>
        public static string MediaSlug(SignDefinition sign)
        {
            if (sign == null) return null;
            switch (sign.Kind)
            {
                case SignKind.Fingerspelling:
                    return "herf_" + Map(AzLower(sign.Gloss), MediaLetterMap);
                case SignKind.Number:
                    return "reqem_" + sign.Gloss;
                case SignKind.Sentence:
                    return "cumle_" + Map(AzLower(sign.Gloss), MediaWordMap);
                default:
                    return Map(AzLower(sign.Gloss), MediaWordMap);
            }
        }

        private static string AzLower(string text)
        {
            return text.Replace("I", "ı").Replace("İ", "i").ToLowerInvariant();
        }

        private static string Map(string lower, Dictionary<char, string> map)
        {
            var sb = new StringBuilder();
            foreach (char c in lower)
            {
                string mapped;
                if (map.TryGetValue(c, out mapped)) sb.Append(mapped);
                else if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
            }

            return sb.ToString().TrimEnd('_');
        }

        public static string NumberId(string numeral)
        {
            return Code + ".number." + numeral;
        }
    }
}
