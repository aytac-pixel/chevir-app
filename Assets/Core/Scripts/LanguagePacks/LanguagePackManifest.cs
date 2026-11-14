using System;
using System.Collections.Generic;

namespace Cevir.Core.LanguagePacks
{
    /// <summary>
    /// Pack metadata (part 1 of the five parts every pack has: manifest, landmark data, model,
    /// dictionary + lessons, non-manual rules). Serializable so remote packs can ship it as JSON.
    /// </summary>
    [Serializable]
    public sealed class LanguagePackManifest
    {
        public string languageCode = "azsld";
        public string displayName = "Azərbaycan İşarət Dili";
        public string englishName = "Azerbaijani Sign Language";

        /// <summary>Two-letter badge shown in the home top bar, e.g. "AZ". Empty = derived from the code.</summary>
        public string badge = "AZ";
        public string version = "0.1.0";

        /// <summary>UI language for glosses and instructions (BCP 47), e.g. "az" or "ru".</summary>
        public string uiLanguage = "az";

        public List<string> alphabet = new List<string>();
        public List<string> numerals = new List<string>();

        public int signCount;

        /// <summary>Licence of the underlying dataset, recorded per pack so releases can be gated on it.</summary>
        public string datasetLicense;
        public bool commercialUseCleared;

        /// <summary>Set once the language's Deaf community representatives have reviewed the pack.</summary>
        public bool communityReviewed;

        public string Badge => !string.IsNullOrEmpty(badge)
            ? badge
            : (string.IsNullOrEmpty(languageCode) ? "?" : languageCode.Substring(0, System.Math.Min(2, languageCode.Length)).ToUpperInvariant());

        /// <summary>Language name in the current interface language.</summary>
        public string LocalizedName => Localization.Loc.IsAz ? displayName : (string.IsNullOrEmpty(englishName) ? displayName : englishName);
    }
}
