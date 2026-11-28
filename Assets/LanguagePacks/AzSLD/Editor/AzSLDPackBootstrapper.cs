using System.Collections.Generic;
using Cevir.Core.Curriculum;
using Cevir.Core.Editor;
using Cevir.Core.Gamification;
using Cevir.Core.LanguagePacks;
using Cevir.Core.Localization;
using Cevir.Core.Recognition;
using Cevir.Core.Stories;
using UnityEditor;
using UnityEngine;

namespace Cevir.LanguagePacks.AzSLD.Editor
{
    /// <summary>
    /// Creates the AzSLD pack: the A1 level as 10 units (the home path of the design), its vocabulary as sign
    /// entries (25 still letters, numerals and A1 words), the 10 A1 stories (Story 1 complete,
    /// the rest listed as coming soon) plus the shared registry and gamification config. Safe to re-run:
    /// existing assets and content edits are kept. Word lists are proposals for the content team; every sign's
    /// AzSLD form must be confirmed with Deaf signers before recording references.
    /// </summary>
    public static class AzSLDPackBootstrapper
    {
        private const string Root = "Assets/LanguagePacks/AzSLD";
        private const string A1 = Root + "/Curriculum/A1";
        private const string CoreData = "Assets/Core/Data";
        private const string StorySprites = "Assets/Core/Sprites/Story";
        private const string StoryAudio = Root + "/Stories/Audio";

        /// <summary>Drop media here with the checklist layout, e.g. Media/A1/U01_Elifba/herf_a.png.</summary>
        public const string MediaRoot = Root + "/Media";
        private const int SignsPerLesson = 3;

        private sealed class UnitSpec
        {
            public string En;
            public string Az;
            public string Category;
            public string[] Words;
            public string GuideEn;
            public string GuideAz;

            /// <summary>Pixel icon on the home path: Assets/Core/Sprites/Units/unit_&lt;Icon&gt;.png.</summary>
            public string Icon;

            /// <summary>Fixed lesson plan (title EN, title AZ, glosses...) when the unit follows the curriculum table exactly.</summary>
            public LessonSpec[] Lessons;
        }

        private sealed class LessonSpec
        {
            public string En;
            public string Az;
            public string[] Glosses;

            public LessonSpec(string en, string az, params string[] glosses)
            {
                En = en;
                Az = az;
                Glosses = glosses;
            }
        }

        /// <summary>Glosses taught as whole sentences (media prefix "cumle_").</summary>
        private static readonly HashSet<string> Sentences = new HashSet<string>
        {
            "Mənim adım ...", "Sənin adın nədir?", "Tanış olduğuma şadam",
            "Bilmirəm", "İstəmirəm", "Sən bilirsən?",
            "O kimdir?", "Sən haradasan?", "Bu nədir?",
            "Bu mənim anamdır", "Mənim bir qardaşım var", "Sənin bacın varmı?",
            "Saat neçədir?", "Saat beşdir",
            "Qırmızı çanta", "Bu kitabdır", "Telefonum yoxdur",
            "Mən evdəyəm", "Aptek haradadır?"
        };

        /// <summary>Signs taken out of the course; their assets are deleted on the next run.</summary>
        private static readonly string[] DroppedGlosses = { "Xoş gəldin", "Dəyməz", "Mən", "Sən", "Mənim", "Sənin", "On", "İyirmi", "Yüz", "Sabah", "Dünən", "Ağ", "Qara", "Küçə", "Park", "Tualet" };

        // Unit 2 as in kurikulum (L01–L05 + review), without "Xoş gəldin" and "Dəyməz" (removed 2026-11-14).
        private static readonly LessonSpec[] Unit2Lessons =
        {
            new LessonSpec("Hello and goodbye", "Salam və sağollaşma", "Salam", "Sabahınız xeyir", "Axşamınız xeyir", "Gecəniz xeyrə", "Görüşərik"),
            new LessonSpec("How are you?", "Necəsən?", "Necəsən", "Yaxşı", "Pis", "Normal", "Sağ ol"),
            new LessonSpec("My name is...", "Adım...", "Ad", "Soyad", "Mənim adım ...", "Sənin adın nədir?"),
            new LessonSpec("Getting acquainted", "Tanışlıq", "Tanış olmaq", "Şad olmaq", "Tanış olduğuma şadam"),
            new LessonSpec("Polite words", "Nəzakət sözləri", "Xahiş edirəm", "Bağışlayın")
        };

        // Unit 3 as in kurikulum, with the media delivered 2026-11-14: «Mən bilirəm», «Bəli, var», «Xeyr, yoxdur» are left out
        // (repeats of words already taught) and so are the head nod / head shake. "Mən" and "mənim" ("sən" and "sənin")
        // are one sign in AzSL, taught as "Mən/mənim" and "Sən/sənin".
        private static readonly LessonSpec[] Unit3Lessons =
        {
            new LessonSpec("I, you, he/she", "Mən, sən, o", "Mən/mənim", "Sən/sənin", "O"),
            new LessonSpec("We, you, they", "Biz, siz, onlar", "Biz", "Siz", "Onlar"),
            new LessonSpec("Yes and no", "Bəli və xeyr", "Bəli", "Xeyr"),
            new LessonSpec("There is, there isn't", "Var və yox", "Var", "Yox"),
            new LessonSpec("To want and to know", "İstəmək və bilmək", "İstəmək", "Bilmək"),
            new LessonSpec("First sentences", "İlk cümlələr", "Bilmirəm", "İstəmirəm", "Sən bilirsən?")
        };

        // Unit 4 as in kurikulum, without the question facial expressions (no mimika in A1).
        private static readonly LessonSpec[] Unit4Lessons =
        {
            new LessonSpec("What? Who?", "Nə? Kim?", "Nə", "Kim"),
            new LessonSpec("Where? When?", "Harada? Nə vaxt?", "Harada", "Nə vaxt"),
            new LessonSpec("Why? How?", "Niyə? Necə?", "Niyə", "Necə"),
            new LessonSpec("How many? Which?", "Neçə? Hansı?", "Neçə", "Hansı"),
            new LessonSpec("Questions", "Sual cümlələri", "O kimdir?", "Sən haradasan?", "Bu nədir?")
        };

        // Unit 5 as in kurikulum.
        private static readonly LessonSpec[] Unit5Lessons =
        {
            new LessonSpec("Mother, father, family", "Ana, ata, ailə", "Ana", "Ata", "Ailə"),
            new LessonSpec("Brother, sister, child", "Qardaş, bacı, uşaq", "Qardaş", "Bacı", "Uşaq"),
            new LessonSpec("Grandparents, baby", "Nənə, baba, körpə", "Nənə", "Baba", "Körpə"),
            new LessonSpec("Man, woman, boy, girl", "Kişi, qadın, oğlan, qız", "Kişi", "Qadın", "Oğlan", "Qız"),
            new LessonSpec("Friend, teacher, spouse", "Dost, müəllim, həyat yoldaşı", "Dost", "Müəllim", "Həyat yoldaşı"),
            new LessonSpec("My family", "Mənim ailəm", "Bu mənim anamdır", "Mənim bir qardaşım var", "Sənin bacın varmı?")
        };

        // Unit 6: 37 number videos were delivered (1–20, tens, hundreds, 1000). To keep the unit light, 21 are taught;
        // the rest (13, 14, 16–19, 60–90, 300, 400, 600–900) are kept outside the project for a later unit.
        private static readonly LessonSpec[] Unit6Lessons =
        {
            new LessonSpec("1–5", "1–5", "1", "2", "3", "4", "5"),
            new LessonSpec("6–10", "6–10", "6", "7", "8", "9", "10"),
            new LessonSpec("11–20", "11–20", "11", "12", "15", "20"),
            new LessonSpec("Tens", "Onluqlar", "30", "40", "50", "100"),
            new LessonSpec("Big numbers", "Böyük rəqəmlər", "200", "500", "1000")
        };

        // Unit 7 as in kurikulum, without "Sabah", "Dünən" and «Sabah səhər» (not recorded); "Bu gün" joins lesson 1.
        private static readonly LessonSpec[] Unit7Lessons =
        {
            new LessonSpec("Now, before, after", "İndi, əvvəl, sonra", "İndi", "Əvvəl", "Sonra", "Bu gün"),
            new LessonSpec("Parts of the day", "Günün hissələri", "Səhər", "Günorta", "Axşam", "Gecə"),
            new LessonSpec("Day, week, month, year", "Gün, həftə, ay, il", "Gün", "Həftə", "Ay", "İl"),
            new LessonSpec("Days of the week 1", "Həftənin günləri 1", "Bazar ertəsi", "Çərşənbə axşamı", "Çərşənbə", "Cümə axşamı"),
            new LessonSpec("Days of the week 2", "Həftənin günləri 2", "Cümə", "Şənbə", "Bazar"),
            new LessonSpec("What time is it?", "Saat neçədir?", "Saat", "Saat neçədir?", "Saat beşdir")
        };

        // Unit 8 as in kurikulum, without Ağ, Qara, Boz, Narıncı, «Hansı rəng?» (not recorded) and "Bu", «Bu nədir?» (repeats).
        private static readonly LessonSpec[] Unit8Lessons =
        {
            new LessonSpec("Colours 1", "Rənglər 1", "Rəng", "Qırmızı", "Mavi", "Yaşıl"),
            new LessonSpec("Colours 2", "Rənglər 2", "Sarı", "Qəhvəyi", "Bənövşəyi", "Çəhrayı"),
            new LessonSpec("School things", "Məktəb əşyaları", "Kitab", "Dəftər", "Qələm", "Çanta"),
            new LessonSpec("Things at home", "Evdə əşyalar", "Telefon", "Açar", "Masa", "Stul"),
            new LessonSpec("Water, bread, tea", "Su, çörək, çay", "Su", "Çörək", "Çay"),
            new LessonSpec("Colour + thing", "Rəng + əşya", "Qırmızı çanta", "Bu kitabdır", "Telefonum yoxdur")
        };

        // Unit 9 as in kurikulum, without Küçə, Park, Tualet and «Tualet haradadır?» (not recorded).
        private static readonly LessonSpec[] Unit9Lessons =
        {
            new LessonSpec("Home, school, work", "Ev, məktəb, iş", "Ev", "Məktəb", "İş", "Universitet"),
            new LessonSpec("Shop, café, bank", "Mağaza, kafe, bank", "Mağaza", "Kafe", "Bank"),
            new LessonSpec("Hospital, pharmacy, police", "Xəstəxana, aptek, polis", "Xəstəxana", "Aptek", "Polis"),
            new LessonSpec("Bus, metro, where?", "Avtobus, metro, harada?", "Avtobus", "Metro", "Mən evdəyəm", "Aptek haradadır?")
        };

        private static readonly UnitSpec[] Units =
        {
            // Unit 1 teaches letters only (no facial expressions, no fingerspelled words), decided 2026-11-13.
            // The moving letters (Ç, G, Ğ, Ö, Ü, Y, Z) are left out, decided 2026-11-23: 25 letters remain.
            new UnitSpec { En = "Alphabet", Az = "Əlifba", Category = "daktil", Icon = "alphabet",
                GuideEn = "Show each letter with your dominant hand at shoulder height, palm towards the viewer.",
                GuideAz = "Hər hərfi əsas əlinlə, çiyin hündürlüyündə, ovucu qarşı tərəfə göstər." },
            new UnitSpec { En = "Greetings & Introductions", Az = "Salamlaşma və tanışlıq", Category = "salamlaşma", Icon = "greetings",
                Lessons = Unit2Lessons },
            new UnitSpec { En = "Pronouns & Basic Sentences", Az = "Əvəzliklər və sadə cümlələr", Category = "əvəzliklər", Icon = "pronouns",
                Lessons = Unit3Lessons },
            new UnitSpec { En = "Question Words", Az = "Sual sözləri", Category = "suallar", Icon = "questions",
                Lessons = Unit4Lessons },
            new UnitSpec { En = "Family & People", Az = "Ailə və insanlar", Category = "ailə", Icon = "family",
                Lessons = Unit5Lessons },
            new UnitSpec { En = "Numbers & Counting", Az = "Rəqəmlər və sayma", Category = "rəqəmlər", Icon = "numbers",
                Lessons = Unit6Lessons },
            new UnitSpec { En = "Time & Schedules", Az = "Zaman və cədvəl", Category = "zaman", Icon = "time",
                Lessons = Unit7Lessons },
            new UnitSpec { En = "Daily Objects & Colors", Az = "Gündəlik əşyalar və rənglər", Category = "əşyalar", Icon = "objects",
                Lessons = Unit8Lessons },
            new UnitSpec { En = "Essential Places", Az = "Vacib yerlər", Category = "yerlər", Icon = "places",
                Lessons = Unit9Lessons },
            new UnitSpec { En = "A1 Review & Final Checkpoint", Az = "A1 təkrar və yekun imtahan", Category = "təkrar", Icon = "review",
                GuideEn = "Review the whole level, then pass the checkpoint (20 random signs, 80% correct) to enter A2.",
                GuideAz = "Bütün səviyyəni təkrarla, sonra A2-yə keçmək üçün imtahanı (20 təsadüfi işarə, 80% düzgün) keç." }
        };

        /// <summary>Unit 1 lessons over the 25 still letters: A–E, Ə–I, İ–L, M–R, S–V (indices into that list).</summary>
        private static readonly int[] Unit1LessonBounds = { 0, 5, 10, 15, 20, 25 };

        // Story N closes unit N and uses only signs taught up to that unit (scripts: kurikulum/Hekayeler_A1_ssenari.md).
        private static readonly string[][] Stories =
        {
            new[] { "Hello, my name is...", "Salam, mənim adım..." },
            new[] { "Good morning", "Sabahınız xeyir" },
            new[] { "Do you know?", "Sən bilirsən?" },
            new[] { "Who is that?", "O kimdir?" },
            new[] { "Meet my family", "Ailəmlə tanış ol" },
            new[] { "How many?", "Neçə?" },
            new[] { "What time is it?", "Saat neçədir?" },
            new[] { "My school bag", "Məktəb çantası" },
            new[] { "A walk around the city", "Şəhərdə gəzinti" },
            new[] { "A1 is done!", "A1 bitdi!" }
        };

        // Beat script: "npc|AZ text|EN text|sign;sign", "you|AZ instruction|EN instruction|expected sign", "namesign|AZ|EN"
        // (the learner signs the nickname / best-known feature picked at sign-up).
        private static readonly string[][] StoryScripts =
        {
            null, // Story 1 is built by FillStory1
            new[]
            {
                "npc|Sabahınız xeyir! Necəsən?|Good morning! How are you?|Sabahınız xeyir;Necəsən",
                "you|«Yaxşı» işarəsini göstər|Sign \"fine\"|Yaxşı",
                "npc|Sağ ol! Görüşərik!|Thank you! See you!|Sağ ol;Görüşərik",
                "you|«Görüşərik» işarəsini göstər|Sign \"see you\"|Görüşərik"
            },
            new[]
            {
                "npc|Salam! Sən bilirsən?|Hi! Do you know?|Salam;Sən bilirsən?",
                "you|«Bilmirəm» işarəsini göstər|Sign \"I don't know\"|Bilmirəm",
                "npc|O bilir. Onlar da bilir.|He knows. They know too.|O;Bilmək;Onlar",
                "you|«Bəli» işarəsini göstər|Sign \"yes\"|Bəli"
            },
            new[]
            {
                "npc|O kimdir?|Who is that?|O kimdir?",
                "you|«Kim» işarəsini göstər|Sign \"who\"|Kim",
                "npc|Sən haradasan?|Where are you?|Sən haradasan?",
                "you|«Harada» işarəsini göstər|Sign \"where\"|Harada",
                "npc|Nə vaxt görüşərik?|When shall we meet?|Nə vaxt;Görüşərik"
            },
            new[]
            {
                "npc|Bu mənim anamdır.|This is my mother.|Bu mənim anamdır",
                "you|«Ana» işarəsini göstər|Sign \"mother\"|Ana",
                "npc|Mənim bir qardaşım var. Sənin bacın varmı?|I have a brother. Do you have a sister?|Mənim bir qardaşım var;Sənin bacın varmı?",
                "you|«Bəli» işarəsini göstər|Sign \"yes\"|Bəli",
                "npc|Ailə yaxşıdır!|Family is good!|Ailə;Yaxşı"
            },
            new[]
            {
                "npc|Salam! Neçə? Üç!|Hi! How many? Three!|Salam;Neçə",
                "you|«3» rəqəmini göstər|Sign \"3\"|3",
                "npc|Bəs on neçədir?|And ten?|Neçə",
                "you|«10» rəqəmini göstər|Sign \"10\"|10",
                "npc|Sağ ol!|Thank you!|Sağ ol"
            },
            new[]
            {
                "npc|Saat neçədir?|What time is it?|Saat neçədir?",
                "you|Saat beşdir: «5» rəqəmini göstər|It is five o'clock: sign \"5\"|5",
                "npc|Bu gün cümədir. Axşam görüşərik?|Today is Friday. See you in the evening?|Bu gün;Cümə;Axşam;Görüşərik",
                "you|«Bəli» işarəsini göstər|Sign \"yes\"|Bəli"
            },
            new[]
            {
                "npc|Bu kitabdır.|This is a book.|Bu kitabdır",
                "you|«Kitab» işarəsini göstər|Sign \"book\"|Kitab",
                "npc|Qırmızı çanta. Telefonum yoxdur!|A red bag. I don't have my phone!|Qırmızı çanta;Telefonum yoxdur",
                "you|«Telefon» işarəsini göstər|Sign \"phone\"|Telefon",
                "npc|Sağ ol! Su?|Thank you! Water?|Sağ ol;Su",
                "you|«Çay» işarəsini göstər|Sign \"tea\"|Çay"
            },
            new[]
            {
                "npc|Bağışlayın, aptek haradadır?|Excuse me, where is the pharmacy?|Bağışlayın;Aptek haradadır?",
                "you|«Aptek» işarəsini göstər|Sign \"pharmacy\"|Aptek",
                "npc|Avtobus? Metro?|Bus? Metro?|Avtobus;Metro",
                "you|«Metro» işarəsini göstər|Sign \"metro\"|Metro",
                "npc|Sağ ol! Mən evdəyəm, görüşərik.|Thanks! I'm at home, see you.|Sağ ol;Mən evdəyəm;Görüşərik"
            },
            new[]
            {
                "npc|Salam! Necəsən?|Hi! How are you?|Salam;Necəsən",
                "you|«Yaxşı» işarəsini göstər|Sign \"fine\"|Yaxşı",
                "npc|Sənin ləqəbin / ən tanınan xüsusiyyətin nədir?|What is your nickname / best-known feature?|Sənin adın nədir?",
                "namesign|«{namesign}» işarəsini göstər|Sign \"{namesign}\"",
                "npc|Tanış olduğuma şadam, {name}! Sən bilirsən!|Nice to meet you, {name}! You know it!|Tanış olduğuma şadam;Sən bilirsən?",
                "you|«Sağ ol» işarəsini göstər|Sign \"thank you\"|Sağ ol"
            }
        };

        [MenuItem("Chevir/AzSLD/Create Or Update Pack Skeleton")]
        public static void Create()
        {
            CreateInternal();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateInternal()
        {
            var config = CevirAssetUtility.LoadOrCreate<SignMatcherConfig>(Root + "/AzSLD_SignMatcherConfig.asset");
            var dictionary = CevirAssetUtility.LoadOrCreate<SignDictionary>(Root + "/Signs/AzSLD_SignDictionary.asset");

            List<SignDefinition> letters = CreateFingerspellingSigns(dictionary);
            List<SignDefinition> numbers = CreateNumberSigns(dictionary);
            var words = new Dictionary<string, SignDefinition>();
            RemoveDroppedSigns(dictionary);
            var unitSigns = new List<List<SignDefinition>>();
            foreach (UnitSpec spec in Units)
            {
                string[] glosses = spec.Lessons != null ? AllGlosses(spec.Lessons) : spec.Words;
                unitSigns.Add(glosses != null ? CreateWordSigns(dictionary, glosses, spec.Category, words) : new List<SignDefinition>());
            }

            EditorUtility.SetDirty(dictionary);

            List<UnitData> units;
            LevelData a1 = CreateA1(letters, numbers, unitSigns, out units);

            var pack = CevirAssetUtility.LoadOrCreate<AzSLDLanguagePack>(Root + "/AzSLD_LanguagePack.asset");
            pack.EditorSetContent(new List<LevelData> { a1 }, dictionary, config);
            pack.EditorSetStories(CreateStories(units, words));
            EditorUtility.SetDirty(pack);

            ImportMediaInternal(dictionary);

            CevirAssetUtility.LoadOrCreate<GamificationConfig>(CoreData + "/GamificationConfig.asset");
            var registry = CevirAssetUtility.LoadOrCreate<LanguagePackRegistry>(CoreData + "/LanguagePackRegistry.asset");
            AddToRegistry(registry, pack);

            Debug.Log("[Chevir] AzSLD pack ready: A1 with " + units.Count + " units, " + dictionary.Signs.Count + " signs, " +
                      pack.Stories.Count + " stories.");
        }

        [MenuItem("Chevir/AzSLD/Import Media (pictures and videos)")]
        public static void ImportMedia()
        {
            var dictionary = AssetDatabase.LoadAssetAtPath<SignDictionary>(Root + "/Signs/AzSLD_SignDictionary.asset");
            if (dictionary == null)
            {
                Create();
                return;
            }

            ImportMediaInternal(dictionary);
            AssetDatabase.SaveAssets();
        }

        private static void ImportMediaInternal(SignDictionary dictionary)
        {
            if (!AssetDatabase.IsValidFolder(MediaRoot))
            {
                AssetDatabase.CreateFolder(Root, "Media");
                Debug.Log("[Chevir] Created " + MediaRoot + ". Copy media here, e.g. A1/U01_Elifba/herf_a.png, then run Chevir > AzSLD > Import Media.");
                return;
            }

            SignMediaImporter.Report report = SignMediaImporter.Import(dictionary, MediaRoot, AzSLDLanguagePack.MediaSlug);
            Debug.Log("[Chevir] AzSLD media: " + report);
            ImportReferencesInternal(dictionary);
        }

        /// <summary>
        /// Landmark references extracted from the lesson media (Tools/landmark_extraction/extract_pack_media.py), kept
        /// outside Assets in Tools/references/azsld. With them the camera compares the learner's sign with the video
        /// instead of only checking that a hand is visible.
        /// </summary>
        private static void ImportReferencesInternal(SignDictionary dictionary)
        {
            string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Tools/references/azsld"));
            if (!System.IO.Directory.Exists(folder)) return;
            int count = SignReferenceImporter.ImportFolder(folder, dictionary, Root + "/Signs/References", AzSLDLanguagePack.MediaSlug);
            Debug.Log("[Chevir] AzSLD sign references: " + count + " imported from " + folder);
        }

        /// <summary>Signs no longer in A1: Unit 1 facial expressions and the words taken out of Unit 2.</summary>
        private static void RemoveDroppedSigns(SignDictionary dictionary)
        {
            System.Predicate<SignDefinition> dropped = s => s.Category == "mimika" || System.Array.IndexOf(DroppedGlosses, s.Gloss) >= 0;
            dictionary.EditorRemoveWhere(dropped);
            foreach (string guid in AssetDatabase.FindAssets("t:SignDefinition", new[] { Root + "/Signs/Dictionary/Words" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var sign = AssetDatabase.LoadAssetAtPath<SignDefinition>(path);
                if (sign != null && dropped(sign)) AssetDatabase.DeleteAsset(path);
            }
        }

        private static string[] AllGlosses(LessonSpec[] lessons)
        {
            var all = new List<string>();
            foreach (LessonSpec lesson in lessons) all.AddRange(lesson.Glosses);
            return all.ToArray();
        }

        // ------------------------------------------------------------------ A1 level

        private static LevelData CreateA1(List<SignDefinition> letters, List<SignDefinition> numbers,
            List<List<SignDefinition>> unitSigns, out List<UnitData> units)
        {
            Color theme = new Color32(0x00, 0x6E, 0xF0, 0xFF);
            var level = CevirAssetUtility.LoadOrCreate<LevelData>(A1 + "/Level_A1.asset", l =>
            {
                l.EditorSetIdentity("azsld.a1", "A1", "Beginner: alphabet, greetings and everyday words");
                l.EditorSetTranslation(Loc.Azerbaijani, "A1", "Başlanğıc: əlifba, salamlaşma və gündəlik sözlər");
            });

            units = new List<UnitData>();
            var everything = new List<SignDefinition>();
            var taught = new List<List<SignDefinition>>(); // signs taught in each unit, for the Unit 10 review
            for (int u = 0; u < Units.Length; u++)
            {
                UnitSpec spec = Units[u];
                string unitId = "azsld.a1.u" + (u + 1);
                bool checkpoint = u == Units.Length - 1;
                var unit = CevirAssetUtility.LoadOrCreate<UnitData>(A1 + "/Units/U" + (u + 1).ToString("00") + ".asset", x =>
                {
                    x.EditorSetIdentity(unitId, spec.En, string.Empty);
                    x.EditorSetTranslation(Loc.Azerbaijani, spec.Az, string.Empty);
                    x.EditorSetCheckpoint(checkpoint, spec.GuideEn);
                });
                if (u == 0)
                {
                    // Projects created before Unit 1 became letters-only still carry the old title.
                    unit.EditorSetIdentity(unitId, spec.En, string.Empty);
                    unit.EditorSetTranslation(Loc.Azerbaijani, spec.Az, string.Empty);
                }

                unit.EditorSetIcon(string.IsNullOrEmpty(spec.Icon) ? null
                    : AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Core/Sprites/Units/unit_" + spec.Icon + ".png"));
                unit.EditorSetPictures(string.IsNullOrEmpty(spec.Icon) ? null
                        : AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Core/Sprites/Units/unitpic_" + spec.Icon + ".png"),
                    string.IsNullOrEmpty(spec.Icon) ? null
                        : AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Core/Sprites/Units/unitpic_" + spec.Icon + "_locked.png"));

                List<LessonData> lessons;
                if (u == 0) lessons = CreateUnit1Lessons(unitId, letters);
                else if (spec.Lessons != null) lessons = CreateFixedLessons(unitId, u, spec, unitSigns[u]);
                else if (checkpoint) lessons = CreateReviewLessons(unitId, u, taught, everything);
                else
                {
                    var signs = new List<SignDefinition>(unitSigns[u]);
                    lessons = CreateTeachingLessons(unitId, u, spec, signs);
                }

                var inUnit = new List<SignDefinition>();
                foreach (LessonData lesson in lessons)
                {
                    foreach (SignDefinition s in lesson.TargetSigns)
                    {
                        if (!everything.Contains(s)) everything.Add(s);
                        if (!inUnit.Contains(s)) inUnit.Add(s);
                    }
                }

                taught.Add(inUnit);

                unit.EditorSetContent(theme, lessons);
                EditorUtility.SetDirty(unit);
                units.Add(unit);
            }

            // Beginners finish A1 before the checkpoint; no testing out of the first level.
            var gate = new GateTestConfig { type = GateTestType.RandomVocabularySample, questionCount = 20, passRatio = 0.8f, allowTestOut = false };
            level.EditorSetContent(CurriculumLevel.Daktil, theme, gate, false, units);
            EditorUtility.SetDirty(level);
            return level;
        }

        private static List<LessonData> CreateUnit1Lessons(string unitId, List<SignDefinition> letters)
        {
            // Letters only, in alphabet order, then a practice lesson over all of them. Always rewritten so older
            // projects (7 letter lessons + facial expressions) are brought in line.
            int[] bounds = Unit1LessonBounds;
            var lessons = new List<LessonData>();
            for (int i = 0; i + 1 < bounds.Length; i++)
            {
                List<SignDefinition> slice = letters.GetRange(bounds[i], bounds[i + 1] - bounds[i]);
                string range = slice[0].Gloss + "–" + slice[slice.Count - 1].Gloss;
                lessons.Add(Lesson(unitId, 1, lessons.Count + 1, LessonType.Standard, slice,
                    "Letters " + range, "Hərflər " + range, true));
            }

            lessons.Add(Lesson(unitId, 1, lessons.Count + 1, LessonType.Practice, letters, "Unit 1 practice", "1-ci bölmə təkrarı", true));

            for (int n = lessons.Count + 1; n <= 12; n++)
            {
                string stale = A1 + "/Lessons/U01_L" + n.ToString("00") + ".asset";
                if (AssetDatabase.LoadAssetAtPath<LessonData>(stale) != null) AssetDatabase.DeleteAsset(stale);
            }

            return lessons;
        }

        /// <summary>
        /// Units with a fixed plan (the curriculum table): one lesson per row, then a review of the whole unit.
        /// Always rewritten so the lessons follow the media that was delivered.
        /// </summary>
        private static List<LessonData> CreateFixedLessons(string unitId, int unitIndex, UnitSpec spec, List<SignDefinition> unitSigns)
        {
            int unitNumber = unitIndex + 1;
            var lessons = new List<LessonData>();
            int next = 0;
            foreach (LessonSpec row in spec.Lessons)
            {
                List<SignDefinition> slice = unitSigns.GetRange(next, row.Glosses.Length);
                next += row.Glosses.Length;
                lessons.Add(Lesson(unitId, unitNumber, lessons.Count + 1, LessonType.Standard, slice, row.En, row.Az, true));
            }

            lessons.Add(Lesson(unitId, unitNumber, lessons.Count + 1, LessonType.Practice, unitSigns,
                "Unit " + unitNumber + " practice", unitNumber + "-ci bölmə təkrarı", true));

            for (int n = lessons.Count + 1; n <= 12; n++)
            {
                string stale = A1 + "/Lessons/U" + unitNumber.ToString("00") + "_L" + n.ToString("00") + ".asset";
                if (AssetDatabase.LoadAssetAtPath<LessonData>(stale) != null) AssetDatabase.DeleteAsset(stale);
            }

            return lessons;
        }

        private static List<LessonData> CreateTeachingLessons(string unitId, int unitIndex, UnitSpec spec, List<SignDefinition> signs)
        {
            var lessons = new List<LessonData>();
            int count = Mathf.Max(1, Mathf.CeilToInt(signs.Count / (float)SignsPerLesson));
            for (int i = 0; i < count; i++)
            {
                int start = i * signs.Count / count;
                int end = (i + 1) * signs.Count / count;
                lessons.Add(Lesson(unitId, unitIndex + 1, i + 1, LessonType.Standard, signs.GetRange(start, end - start),
                    spec.En + " " + (i + 1), spec.Az + " " + (i + 1)));
            }

            lessons.Add(Lesson(unitId, unitIndex + 1, count + 1, LessonType.Practice, signs, "Practice", "Təkrar"));
            return lessons;
        }

        /// <summary>Unit 10 review lessons: (title EN, title AZ, unit numbers reviewed). Only already recorded videos are used.</summary>
        private static readonly object[][] ReviewPlan =
        {
            new object[] { "Alphabet review", "Əlifba təkrarı", new[] { 1 } },
            new object[] { "Greetings and pronouns", "Salamlaşma və əvəzliklər", new[] { 2, 3 } },
            new object[] { "Questions and family", "Suallar və ailə", new[] { 4, 5 } },
            new object[] { "Numbers and time", "Rəqəmlər və zaman", new[] { 6, 7 } },
            new object[] { "Things and places", "Əşyalar və yerlər", new[] { 8, 9 } }
        };

        /// <summary>
        /// Unit 10 (A1 review): one review per pair of units, then a mixed review of the whole level. Each review is
        /// sampled (12 signs, the ones due for review first), so a session stays short. The checkpoint follows.
        /// </summary>
        private static List<LessonData> CreateReviewLessons(string unitId, int unitIndex, List<List<SignDefinition>> taught,
            List<SignDefinition> everything)
        {
            int unitNumber = unitIndex + 1;
            var lessons = new List<LessonData>();
            foreach (object[] row in ReviewPlan)
            {
                var signs = new List<SignDefinition>();
                foreach (int n in (int[])row[2])
                {
                    if (n - 1 < taught.Count) signs.AddRange(taught[n - 1]);
                }

                if (signs.Count == 0) continue;
                lessons.Add(Lesson(unitId, unitNumber, lessons.Count + 1, LessonType.Practice, signs, (string)row[0], (string)row[1], true));
            }

            lessons.Add(Lesson(unitId, unitNumber, lessons.Count + 1, LessonType.Practice, everything, "A1 mixed review", "A1 qarışıq təkrar", true));

            for (int n = lessons.Count + 1; n <= 12; n++)
            {
                string stale = A1 + "/Lessons/U" + unitNumber.ToString("00") + "_L" + n.ToString("00") + ".asset";
                if (AssetDatabase.LoadAssetAtPath<LessonData>(stale) != null) AssetDatabase.DeleteAsset(stale);
            }

            return lessons;
        }

        private static LessonData Lesson(string unitId, int unitNumber, int number, LessonType type, List<SignDefinition> signs, string en, string az,
            bool overwrite = false)
        {
            string id = unitId + ".les" + number;
            var lesson = CevirAssetUtility.LoadOrCreate<LessonData>(A1 + "/Lessons/U" + unitNumber.ToString("00") + "_L" + number.ToString("00") + ".asset", x =>
            {
                x.EditorSetIdentity(id, en, string.Empty);
                x.EditorSetTranslation(Loc.Azerbaijani, az, string.Empty);
            });

            if (overwrite)
            {
                lesson.EditorSetIdentity(id, en, string.Empty);
                lesson.EditorSetTranslation(Loc.Azerbaijani, az, string.Empty);
            }

            // Russian titles: generated ones ("Hərflər A–E", "2-ci bölmə təkrarı") are written here, the rest come from
            // the translation table at runtime.
            string ru = RussianTitle(az);
            if (!string.IsNullOrEmpty(ru)) lesson.EditorSetTranslation(Loc.Russian, ru, string.Empty);

            if (overwrite || lesson.TargetSigns.Count == 0)
            {
                // Unit 1 letters: show the letter, "Anladım", then sign it on camera (no quiz steps).
                if (unitNumber == 1 && type == LessonType.Standard)
                    lesson.EditorSetSteps(new List<LessonStep> { LessonStep.Watch, LessonStep.Practice });
                lesson.EditorSetContent(type, new List<SignDefinition>(signs));
                EditorUtility.SetDirty(lesson);
            }

            return lesson;
        }

        // ------------------------------------------------------------------ stories

        private static List<StoryData> CreateStories(List<UnitData> units, Dictionary<string, SignDefinition> words)
        {
            var stories = new List<StoryData>();
            for (int i = 0; i < Stories.Length; i++)
            {
                int number = i + 1;
                string id = "azsld.a1.story" + number;
                string en = Stories[i][0], az = Stories[i][1];
                var story = CevirAssetUtility.LoadOrCreate<StoryData>(Root + "/Stories/Story" + number.ToString("00") + ".asset", x =>
                {
                    x.EditorSetIdentity(id, en, string.Empty);
                    x.EditorSetTranslation(Loc.Azerbaijani, az, string.Empty);
                });

                UnitData unlock = i < units.Count ? units[i] : null;
                // Titles follow the unit each story closes; rewritten so older projects pick up the new names.
                story.EditorSetIdentity(id, en, string.Empty);
                story.EditorSetTranslation(Loc.Azerbaijani, az, string.Empty);
                if (number == 1)
                {
                    if (NeedsStory1(story)) FillStory1(story, unlock, words);
                    else if (story.UnlockUnit == null) story.EditorSetUnlock(number, unlock);
                }
                else if (i < StoryScripts.Length && StoryScripts[i] != null) FillScriptedStory(story, number, unlock, StoryScripts[i], words);
                else if (story.UnlockUnit == null) story.EditorSetUnlock(number, unlock);

                // Every story has its own pixel-art scene (Assets/Core/Sprites/Story/story_bg_NN.png).
                Sprite scene = LoadSprite("story_bg_" + number.ToString("00"));
                story.EditorSetArt(scene != null ? scene : LoadSprite("story_bg_01"), LoadSprite("pov_hands_bag"));

                EditorUtility.SetDirty(story);
                stories.Add(story);
            }

            return stories;
        }

        /// <summary>Story 1 is (re)built when empty or still in an older form that spelled the name letter by letter.</summary>
        private static bool NeedsStory1(StoryData story)
        {
            if (story.Beats.Count == 0) return true;
            foreach (StoryBeat beat in story.Beats)
            {
                if (beat.type == StoryBeatType.LearnerFingerspellName) return true;
            }

            return false;
        }

        /// <summary>
        /// Story 1 "Hello, my name is..." (end of Unit 1): on a street, Aysel walks up, greets the learner, explains that
        /// Deaf people call each other by a nickname or best-known feature and asks for the learner's; the learner signs
        /// the word picked at sign-up (a dictionary sign); Aysel answers "Nice to meet you!".
        /// The NPC's sign sequence is a gloss proposal and must be checked by AzSLD signers.
        /// </summary>
        private static void FillStory1(StoryData story, UnitData unlock, Dictionary<string, SignDefinition> words)
        {
            var npc = new StoryCharacter
            {
                displayName = new LocalizedString("Aysel", "Aysel"),
                idle = LoadSprite("npc_idle"),
                talking = LoadSprite("npc_talk"),
                wave = LoadSprite("npc_wave_idle"),
                waveTalking = LoadSprite("npc_wave")
            };

            var beats = new List<StoryBeat>
            {
                new StoryBeat
                {
                    type = StoryBeatType.NpcLine,
                    approach = true,
                    pose = NpcPose.Wave,
                    text = new LocalizedString("Hello! Deaf people don't spell each other's names: we call each other by a nickname or best-known feature.",
                        "Salam! Karlar bir-birinin adını hərf-hərf demir: bir-birimizi ləqəb və ya ən tanınan xüsusiyyətlə çağırırıq."),
                    npcSigns = Signs(words, "Salam")
                },
                new StoryBeat
                {
                    type = StoryBeatType.NpcLine,
                    pose = NpcPose.Idle,
                    text = new LocalizedString("What is your nickname / best-known feature?", "Sənin ləqəbin / ən tanınan xüsusiyyətin nədir?"),
                    // Record "story1_name_az/en.wav" with this question to give the line a voice.
                    voice = new LocalizedAudio { en = LoadAudio("story1_name_en"), az = LoadAudio("story1_name_az") },
                    // Until a «Sənin ləqəbin nədir?» clip exists, the NPC signs the closest lesson sentence.
                    npcSigns = Signs(words, "Sənin adın nədir?")
                },
                new StoryBeat
                {
                    type = StoryBeatType.LearnerNameSign,
                    text = new LocalizedString("Sign \"{namesign}\"", "«{namesign}» işarəsini göstər")
                },
                new StoryBeat
                {
                    type = StoryBeatType.NpcLine,
                    pose = NpcPose.Wave,
                    text = new LocalizedString("Nice to meet you, {name}!", "Tanış olduğuma şadam, {name}!"),
                    voice = new LocalizedAudio { en = LoadAudio("story1_nice_en"), az = LoadAudio("story1_nice_az") },
                    npcSigns = Signs(words, "Tanış olduğuma şadam")
                }
            };

            story.EditorSetContent(1, unlock, 20, LoadSprite("story_bg_01"), LoadSprite("pov_hands_bag"), npc, beats);
        }

        /// <summary>Builds a story from a beat script (see <see cref="StoryScripts"/>): Aysel on the street, as in Story 1.</summary>
        private static void FillScriptedStory(StoryData story, int number, UnitData unlock, string[] script, Dictionary<string, SignDefinition> words)
        {
            var beats = new List<StoryBeat>();
            foreach (string line in script)
            {
                string[] part = line.Split('|');
                var beat = new StoryBeat { text = new LocalizedString(part[2], part[1]) };
                switch (part[0])
                {
                    case "npc":
                        beat.type = StoryBeatType.NpcLine;
                        beat.approach = beats.Count == 0;
                        beat.pose = beats.Count == 0 ? NpcPose.Wave : NpcPose.Idle;
                        beat.npcSigns = Signs(words, part[3].Split(';'));
                        if (beat.npcSigns.Count < part[3].Split(';').Length)
                            Debug.LogWarning("[Chevir] Story " + number + ": a sign in \"" + part[3] + "\" is not in the dictionary.");
                        break;
                    case "you":
                        beat.type = StoryBeatType.LearnerSign;
                        SignDefinition expected;
                        if (!words.TryGetValue(part[3], out expected)) Debug.LogWarning("[Chevir] Story " + number + ": no sign \"" + part[3] + "\".");
                        beat.expectedSign = expected;
                        break;
                    default:
                        beat.type = StoryBeatType.LearnerNameSign;
                        break;
                }

                beats.Add(beat);
            }

            var npc = new StoryCharacter
            {
                displayName = new LocalizedString("Aysel", "Aysel"),
                idle = LoadSprite("npc_idle"),
                talking = LoadSprite("npc_talk"),
                wave = LoadSprite("npc_wave_idle"),
                waveTalking = LoadSprite("npc_wave")
            };
            story.EditorSetContent(number, unlock, 20, LoadSprite("story_bg_01"), LoadSprite("pov_hands_bag"), npc, beats);
        }

        private static string RussianTitle(string az)
        {
            if (string.IsNullOrEmpty(az)) return null;
            if (az.StartsWith("Hərflər ")) return "Буквы " + az.Substring("Hərflər ".Length);
            System.Text.RegularExpressions.Match review = System.Text.RegularExpressions.Regex.Match(az, @"^(\d+)-c[iıuü] bölmə təkrarı$");
            if (review.Success) return "Повторение раздела " + review.Groups[1].Value;
            string direct = Translations.Russian(az);
            if (direct != null) return direct;
            System.Text.RegularExpressions.Match numbered = System.Text.RegularExpressions.Regex.Match(az, @"^(.*) (\d+)$");
            if (numbered.Success && Translations.Russian(numbered.Groups[1].Value) != null)
                return Translations.Russian(numbered.Groups[1].Value) + " " + numbered.Groups[2].Value;
            return null;
        }

        private static List<SignDefinition> Signs(Dictionary<string, SignDefinition> words, params string[] glosses)
        {
            var list = new List<SignDefinition>();
            foreach (string gloss in glosses)
            {
                SignDefinition sign;
                if (words.TryGetValue(gloss, out sign)) list.Add(sign);
            }

            return list;
        }

        private static Sprite LoadSprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(StorySprites + "/" + name + ".png");
            if (sprite == null) Debug.LogWarning("[Chevir] Story sprite not found (is it imported as a Sprite?): " + name);
            return sprite;
        }

        private static AudioClip LoadAudio(string name)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>(StoryAudio + "/" + name + ".wav");
        }

        // ------------------------------------------------------------------ signs

        private static List<SignDefinition> CreateFingerspellingSigns(SignDictionary dictionary)
        {
            var result = new List<SignDefinition>();
            foreach (string letter in AzSLDLanguagePack.Alphabet)
            {
                string id = AzSLDLanguagePack.LetterId(letter);
                if (AzSLDLanguagePack.IsMovingLetter(letter))
                {
                    // Moving letters are not taught: out of the dictionary, so no lesson, review or quiz picks them.
                    string movingId = id;
                    dictionary.EditorRemoveWhere(s => s != null && s.SignId == movingId);
                    string stale = Root + "/Signs/Dictionary/Letters/" + CevirAssetUtility.SafeFileName(id) + ".asset";
                    if (AssetDatabase.LoadAssetAtPath<SignDefinition>(stale) != null) AssetDatabase.DeleteAsset(stale);
                    continue;
                }

                // Static by default; set Dynamic on letters whose AzSLD form includes movement once verified with signers.
                SignDefinition sign = CevirAssetUtility.LoadOrCreate<SignDefinition>(
                    Root + "/Signs/Dictionary/Letters/" + CevirAssetUtility.SafeFileName(id) + ".asset",
                    s => s.EditorInitialize(id, letter, SignKind.Fingerspelling, false, false, "daktil"));
                dictionary.EditorAdd(sign);
                result.Add(sign);
            }

            return result;
        }

        private static List<SignDefinition> CreateNumberSigns(SignDictionary dictionary)
        {
            var result = new List<SignDefinition>();
            foreach (string numeral in AzSLDLanguagePack.Numerals)
            {
                string id = AzSLDLanguagePack.NumberId(numeral);
                SignDefinition sign = CevirAssetUtility.LoadOrCreate<SignDefinition>(
                    Root + "/Signs/Dictionary/Numbers/" + CevirAssetUtility.SafeFileName(id) + ".asset",
                    s => s.EditorInitialize(id, numeral, SignKind.Number, false, false, "rəqəmlər"));
                dictionary.EditorAdd(sign);
                result.Add(sign);
            }

            return result;
        }

        private static List<SignDefinition> CreateWordSigns(SignDictionary dictionary, string[] glosses, string category,
            Dictionary<string, SignDefinition> index)
        {
            var result = new List<SignDefinition>();
            foreach (string gloss in glosses)
            {
                SignDefinition sign;
                if (!index.TryGetValue(gloss, out sign) && IsNumber(gloss))
                {
                    // Numbers share the numeral assets (Numbers folder, media "reqem_N").
                    string numberId = AzSLDLanguagePack.NumberId(gloss);
                    bool multiDigit = gloss.Length > 1;
                    sign = CevirAssetUtility.LoadOrCreate<SignDefinition>(
                        Root + "/Signs/Dictionary/Numbers/" + CevirAssetUtility.SafeFileName(numberId) + ".asset",
                        s => s.EditorInitialize(numberId, gloss, SignKind.Number, multiDigit, false, "rəqəmlər"));
                    dictionary.EditorAdd(sign);
                    index[gloss] = sign;
                }
                else if (sign == null)
                {
                    string id = AzSLDLanguagePack.WordId(gloss);
                    SignKind kind = Sentences.Contains(gloss) ? SignKind.Sentence : SignKind.Word;
                    sign = CevirAssetUtility.LoadOrCreate<SignDefinition>(
                        Root + "/Signs/Dictionary/Words/" + CevirAssetUtility.SafeFileName(id) + ".asset",
                        s => s.EditorInitialize(id, gloss, kind, true, false, category));
                    dictionary.EditorAdd(sign);
                    index[gloss] = sign;
                }

                result.Add(sign);
            }

            return result;
        }

        private static bool IsNumber(string gloss)
        {
            if (string.IsNullOrEmpty(gloss)) return false;
            foreach (char c in gloss) if (c < '0' || c > '9') return false;
            return true;
        }

        private static void AddToRegistry(LanguagePackRegistry registry, LanguagePackDefinition pack)
        {
            var so = new SerializedObject(registry);
            SerializedProperty list = so.FindProperty("builtInPacks");
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == pack) return;
            }

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = pack;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
