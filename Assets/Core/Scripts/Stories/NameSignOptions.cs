using System;
using System.Collections.Generic;
using Cevir.Core.Localization;

namespace Cevir.Core.Stories
{
    /// <summary>
    /// Nicknames and best-known features a learner can pick as the sign they are called by (Deaf people call each
    /// other by such a sign instead of spelling the name). Every word is a sign of the chevirapp.com dictionary, so a
    /// story can check it with the word model when the model knows it. The list is picked at sign-up.
    /// Source and dictionary ids: kurikulum/ad-isaresi/Ad_isaresi_sozler.md.
    /// </summary>
    public static class NameSignOptions
    {
        public sealed class Group
        {
            public Group(string title, params string[] words)
            {
                Title = title;
                Words = words;
            }

            public string Title { get; private set; }
            public IReadOnlyList<string> Words { get; private set; }
        }

        public static readonly IReadOnlyList<Group> Groups = new[]
        {
            new Group("Görünüş", "Eynək", "Hündür", "Alçaq", "Uzun", "Qısa", "Balaca", "Kök", "Arıq", "Güclü", "Saç",
                "Saç hörüyü", "Saqqal", "Keçəl", "Çillər", "Qaş", "Kirpik", "Göz", "Burun", "Qulaq", "Dodaq", "Diş",
                "Gözəl", "Yaraşıqlı", "Solaxay", "Əkiz", "Tatu"),
            new Group("Geyim və aksesuar", "Papaq", "Şlyapa", "Kepka", "Şərf", "Qalstuk", "Sırğa", "Üzük", "Bilərzik",
                "Saat", "Çanta", "Ətir"),
            new Group("Xasiyyət", "Gülümsəmək", "Gülmək", "Şən", "Sakit", "Mehriban", "Utancaq", "Cəsur", "Ciddi",
                "Zarafat", "Sevimli", "Şirin", "Çalışqan", "Tez"),
            new Group("Maraq və peşə", "Futbol", "Basketbol", "Voleybol", "Şahmat", "Boks", "Karate", "İdman", "Gitara",
                "Mahnı", "Rəssam", "Aşpaz", "Həkim", "Müəllim", "Kitab", "Telefon", "Kompüter", "Velosiped", "Çiçək",
                "Şokolad", "Çay", "Qəhvə"),
            new Group("Heyvanlar", "Pişik", "İt", "Dovşan", "Ayı", "Aslan", "Meymun", "Fil", "Quş", "At", "Canavar")
        };

        /// <summary>
        /// The word as the learner reads it: Azerbaijani as stored, translated in the English and Russian interface
        /// (the sign itself, and the stored value, stay Azerbaijani).
        /// </summary>
        public static string Display(string word)
        {
            return string.IsNullOrEmpty(word) ? word ?? string.Empty : Loc.T(word);
        }

        /// <summary>The list's own spelling of <paramref name="word"/> (any case), or null when it is not in the list.</summary>
        public static string Find(string word)
        {
            if (string.IsNullOrEmpty(word)) return null;
            string key = Loc.ToLowerAz(word.Trim());
            foreach (Group group in Groups)
            {
                foreach (string option in group.Words)
                {
                    if (Loc.ToLowerAz(option) == key) return option;
                }
            }

            return null;
        }
    }
}
