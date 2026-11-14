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
    /// entries (32 letters, numerals and A1 words), the 10 A1 stories (Story 1 complete,
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
        }

        private static readonly UnitSpec[] Units =
        {
            // Unit 1 teaches the 32 letters only (no facial expressions, no fingerspelled words), decided 2026-11-13.
            new UnitSpec { En = "Alphabet", Az = "Əlifba", Category = "daktil",
                GuideEn = "Show each letter with your dominant hand at shoulder height, palm towards the viewer.",
                GuideAz = "Hər hərfi əsas əlinlə, çiyin hündürlüyündə, ovucu qarşı tərəfə göstər." },
            new UnitSpec { En = "Greetings & Introductions", Az = "Salamlaşma və tanışlıq", Category = "salamlaşma",
                Words = new[] { "Salam", "Sağ ol", "Xoş gəldin", "Necəsən", "Yaxşı", "Ad", "Soyad", "Tanış olmaq", "Şad olmaq", "Xahiş edirəm", "Bağışlayın", "Görüşərik" } },
            new UnitSpec { En = "Pronouns & Basic Sentences", Az = "Əvəzliklər və sadə cümlələr", Category = "əvəzliklər",
                Words = new[] { "Mən", "Sən", "O", "Biz", "Siz", "Onlar", "Bəli", "Xeyr", "Var", "Yox", "İstəmək", "Bilmək" } },
            new UnitSpec { En = "Question Words", Az = "Sual sözləri", Category = "suallar",
                Words = new[] { "Nə", "Kim", "Harada", "Nə vaxt", "Niyə", "Necə", "Neçə", "Hansı" } },
            new UnitSpec { En = "Family & People", Az = "Ailə və insanlar", Category = "ailə",
                Words = new[] { "Ana", "Ata", "Qardaş", "Bacı", "Nənə", "Baba", "Uşaq", "Dost", "Kişi", "Qadın", "Ailə", "Müəllim" } },
            new UnitSpec { En = "Numbers & Counting", Az = "Rəqəmlər və sayma", Category = "rəqəmlər",
                Words = new[] { "On", "İyirmi", "Yüz" } },
            new UnitSpec { En = "Time & Schedules", Az = "Zaman və cədvəl", Category = "zaman",
                Words = new[] { "Bu gün", "Sabah", "Dünən", "Səhər", "Axşam", "Gün", "Həftə", "Saat", "İndi", "Sonra" } },
            new UnitSpec { En = "Daily Objects & Colors", Az = "Gündəlik əşyalar və rənglər", Category = "əşyalar",
                Words = new[] { "Qırmızı", "Mavi", "Yaşıl", "Sarı", "Ağ", "Qara", "Kitab", "Telefon", "Su", "Çörək", "Masa", "Qələm" } },
            new UnitSpec { En = "Essential Places", Az = "Vacib yerlər", Category = "yerlər",
                Words = new[] { "Ev", "Məktəb", "Xəstəxana", "Mağaza", "Kafe", "Park", "İş", "Küçə", "Avtobus", "Aptek" } },
            new UnitSpec { En = "A1 Review & Final Checkpoint", Az = "A1 təkrar və yekun imtahan", Category = "təkrar",
                GuideEn = "Review the whole level, then pass the checkpoint (20 random signs, 80% correct) to enter A2.",
                GuideAz = "Bütün səviyyəni təkrarla, sonra A2-yə keçmək üçün imtahanı (20 təsadüfi işarə, 80% düzgün) keç." }
        };

        /// <summary>Unit 1 lessons as in kurikulum/Tapilmali_media_A1_A2.txt: A–Ə, F–I, İ–M, N–S, Ş–Z (indices into the alphabet).</summary>
        private static readonly int[] Unit1LessonBounds = { 0, 7, 13, 19, 25, 32 };

        private static readonly string[][] Stories =
        {
            new[] { "Hello, my name is...", "Salam, mənim adım..." },
            new[] { "Meet My Family", "Ailəmlə tanış ol" },
            new[] { "At the Supermarket", "Supermarketdə" },
            new[] { "Ordering at a Café", "Kafedə sifariş" },
            new[] { "The First Meeting", "İlk görüş" },
            new[] { "A Walk Around the City", "Şəhərdə gəzinti" },
            new[] { "Birthday Party", "Ad günü" },
            new[] { "A Day at School", "Məktəbdə bir gün" },
            new[] { "At the Doctor's", "Həkimdə" },
            new[] { "Travel Plans", "Səyahət planları" }
        };

        [MenuItem("Çevir/AzSLD/Create Or Update Pack Skeleton")]
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
                unitSigns.Add(spec.Words != null ? CreateWordSigns(dictionary, spec.Words, spec.Category, SignKind.Word, words) : new List<SignDefinition>());
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

            Debug.Log("[Çevir] AzSLD pack ready: A1 with " + units.Count + " units, " + dictionary.Signs.Count + " signs, " +
                      pack.Stories.Count + " stories.");
        }

        [MenuItem("Çevir/AzSLD/Import Media (pictures and videos)")]
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
                Debug.Log("[Çevir] Created " + MediaRoot + ". Copy media here, e.g. A1/U01_Elifba/herf_a.png, then run Çevir > AzSLD > Import Media.");
                return;
            }

            SignMediaImporter.Report report = SignMediaImporter.Import(dictionary, MediaRoot, AzSLDLanguagePack.MediaSlug);
            Debug.Log("[Çevir] AzSLD media: " + report);
        }

        /// <summary>Unit 1 used to include facial expressions; those signs are no longer part of A1.</summary>
        private static void RemoveDroppedSigns(SignDictionary dictionary)
        {
            dictionary.EditorRemoveWhere(s => s.Category == "mimika");
            foreach (string guid in AssetDatabase.FindAssets("t:SignDefinition", new[] { Root + "/Signs/Dictionary/Words" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var sign = AssetDatabase.LoadAssetAtPath<SignDefinition>(path);
                if (sign != null && sign.Category == "mimika") AssetDatabase.DeleteAsset(path);
            }
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

                List<LessonData> lessons;
                if (u == 0) lessons = CreateUnit1Lessons(unitId, letters);
                else if (checkpoint) lessons = CreateReviewLessons(unitId, u, everything);
                else
                {
                    var signs = new List<SignDefinition>(unitSigns[u]);
                    if (u == 5) signs.InsertRange(0, numbers); // Numbers & Counting: numerals first
                    lessons = CreateTeachingLessons(unitId, u, spec, signs);
                }

                foreach (LessonData lesson in lessons)
                {
                    foreach (SignDefinition s in lesson.TargetSigns) if (!everything.Contains(s)) everything.Add(s);
                }

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
            // Letters only, in alphabet order, then a practice lesson over all 32. Always rewritten so older
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

        private static List<LessonData> CreateReviewLessons(string unitId, int unitIndex, List<SignDefinition> everything)
        {
            int half = everything.Count / 2;
            return new List<LessonData>
            {
                Lesson(unitId, unitIndex + 1, 1, LessonType.Practice, everything.GetRange(0, half), "A1 review 1", "A1 təkrar 1", true),
                Lesson(unitId, unitIndex + 1, 2, LessonType.Practice, everything.GetRange(half, everything.Count - half), "A1 review 2", "A1 təkrar 2", true)
            };
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
                if (number == 1 && NeedsStory1(story)) FillStory1(story, unlock, words);
                else if (story.UnlockUnit == null) story.EditorSetUnlock(number, unlock);

                EditorUtility.SetDirty(story);
                stories.Add(story);
            }

            return stories;
        }

        /// <summary>Story 1 is (re)built when empty or still in the older form that also asked for the surname.</summary>
        private static bool NeedsStory1(StoryData story)
        {
            if (story.Beats.Count == 0) return true;
            foreach (StoryBeat beat in story.Beats)
            {
                if (beat.type == StoryBeatType.LearnerFingerspellName && beat.nameScope == NameScope.FullName) return true;
            }

            return false;
        }

        /// <summary>
        /// Story 1 "Hello, my name is..." (end of Unit 1): on a street, Aysel walks up, greets the learner and asks
        /// their name; the learner shows it letter by letter with the Unit 1 alphabet; Aysel answers "Nice to meet you!".
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
                    text = new LocalizedString("Hello! What is your name?", "Salam! Adın nədir?"),
                    // The old voice clips also asked for the surname; record "story1_name_az/en.wav" to give this line a voice.
                    voice = new LocalizedAudio { en = LoadAudio("story1_name_en"), az = LoadAudio("story1_name_az") },
                    npcSigns = Signs(words, "Salam", "Sən", "Ad", "Nə")
                },
                new StoryBeat
                {
                    type = StoryBeatType.LearnerFingerspellName,
                    nameScope = NameScope.FirstName,
                    text = new LocalizedString("Show your name letter by letter", "Adını hərf-hərf göstər")
                },
                new StoryBeat
                {
                    type = StoryBeatType.NpcLine,
                    pose = NpcPose.Wave,
                    text = new LocalizedString("Nice to meet you, {name}!", "Tanış olduğuma şadam, {name}!"),
                    voice = new LocalizedAudio { en = LoadAudio("story1_nice_en"), az = LoadAudio("story1_nice_az") },
                    npcSigns = Signs(words, "Tanış olmaq", "Şad olmaq")
                }
            };

            story.EditorSetContent(1, unlock, 20, LoadSprite("story_street_bg"), LoadSprite("pov_hands_bag"), npc, beats);
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
            if (sprite == null) Debug.LogWarning("[Çevir] Story sprite not found (is it imported as a Sprite?): " + name);
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

        private static List<SignDefinition> CreateWordSigns(SignDictionary dictionary, string[] glosses, string category, SignKind kind,
            Dictionary<string, SignDefinition> index)
        {
            var result = new List<SignDefinition>();
            foreach (string gloss in glosses)
            {
                SignDefinition sign;
                if (!index.TryGetValue(gloss, out sign))
                {
                    string id = AzSLDLanguagePack.WordId(gloss);
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
