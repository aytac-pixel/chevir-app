using System;
using System.Collections.Generic;
using Cevir.Core.Curriculum;
using Cevir.Core.Game;
using Cevir.Core.Gamification;
using Cevir.Core.LanguagePacks;
using Cevir.Core.Localization;
using Cevir.Core.Recognition;
using Cevir.Core.Social;
using Cevir.Core.Stories;
using NUnit.Framework;
using UnityEngine;

namespace Cevir.Core.Tests
{
    /// <summary>A pack built in memory: one A1-like level, a few letter signs and stories.</summary>
    internal sealed class StoryTestPack : ILanguagePack
    {
        public readonly List<LevelData> LevelList = new List<LevelData>();
        public readonly List<StoryData> StoryList = new List<StoryData>();
        public readonly LanguagePackManifest PackManifest = new LanguagePackManifest();
        public SignDictionary PackDictionary;

        public string LanguageCode => "test";
        public LanguagePackManifest Manifest => PackManifest;
        public IReadOnlyList<LevelData> Levels => LevelList;
        public IReadOnlyList<StoryData> Stories => StoryList;
        public SignDictionary Dictionary => PackDictionary;
        public SignMatcherConfig MatcherConfig => null;
        public TextAsset RecognitionModel => null;
        public TextAsset HandLandmarkerModelOverride => null;
        public TextAsset FaceLandmarkerModelOverride => null;
        public bool IsPlayable => true;
        public ISignClassifier CreateClassifier() => null;
        public MatchResult PostProcess(MatchResult result, SignDefinition target) => result;

        public static readonly string[] Alphabet =
        {
            "A", "B", "C", "Ç", "D", "E", "Ə", "F", "G", "Ğ", "H", "X", "I", "İ", "J", "K", "Q", "L", "M", "N", "O", "Ö", "P", "R",
            "S", "Ş", "T", "U", "Ü", "V", "Y", "Z"
        };

        /// <summary>Letter signs for the whole alphabet; none has reference recordings (self-check letters).</summary>
        public static StoryTestPack WithAlphabet()
        {
            var pack = new StoryTestPack();
            pack.PackManifest.alphabet = new List<string>(Alphabet);
            var signs = new List<SignDefinition>();
            foreach (string letter in Alphabet)
            {
                var sign = ScriptableObject.CreateInstance<SignDefinition>();
                sign.EditorInitialize("letter." + letter, letter, SignKind.Fingerspelling, false, false, "alphabet");
                signs.Add(sign);
            }

            pack.PackDictionary = ScriptableObject.CreateInstance<SignDictionary>();
            typeof(SignDictionary).GetField("signs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(pack.PackDictionary, signs);
            return pack;
        }

        public static UnitData Unit(string id, int lessons, bool checkpoint = false)
        {
            var list = new List<LessonData>();
            for (int i = 1; i <= lessons; i++)
            {
                var lesson = ScriptableObject.CreateInstance<LessonData>();
                lesson.EditorSetIdentity(id + ".les" + i, "Lesson " + i, "");
                list.Add(lesson);
            }

            var unit = ScriptableObject.CreateInstance<UnitData>();
            unit.EditorSetIdentity(id, id, "");
            unit.EditorSetContent(Color.white, list);
            if (checkpoint) unit.EditorSetCheckpoint(true, "");
            return unit;
        }

        public LevelData AddLevel(string id, params UnitData[] units)
        {
            var level = ScriptableObject.CreateInstance<LevelData>();
            level.EditorSetIdentity(id, id, "");
            level.EditorSetContent(CurriculumLevel.Daktil, Color.white, new GateTestConfig { allowTestOut = false }, false, new List<UnitData>(units));
            LevelList.Add(level);
            return level;
        }

        /// <summary>Story 1's shape: NPC asks, learner fingerspells the full name, NPC answers.</summary>
        public StoryData AddStory(int number, UnitData unlock, bool playable = true)
        {
            var story = ScriptableObject.CreateInstance<StoryData>();
            story.EditorSetIdentity("story" + number, "Story " + number, "");
            var beats = new List<StoryBeat>();
            if (playable)
            {
                beats.Add(new StoryBeat { type = StoryBeatType.NpcLine, text = new LocalizedString { en = "Hello! What is your name and surname?", az = "Salam! Adın və soyadın nədir?" } });
                beats.Add(new StoryBeat { type = StoryBeatType.LearnerFingerspellName, nameScope = NameScope.FullName });
                beats.Add(new StoryBeat { type = StoryBeatType.NpcLine, text = new LocalizedString { en = "Nice to meet you, {name}!", az = "Tanış olmağıma şadam, {name}!" } });
            }

            story.EditorSetContent(number, unlock, 20, null, null, new StoryCharacter(), beats);
            StoryList.Add(story);
            return story;
        }
    }

    public sealed class FingerspellingTests
    {
        private static MatchResult Correct(bool unverified) => new MatchResult { Feedback = SignFeedback.Correct, Severity = FeedbackSeverity.None, Unverified = unverified };
        private static MatchResult Mistake() => new MatchResult { Feedback = SignFeedback.AdjustHandShape, Severity = FeedbackSeverity.Soft };
        private static MatchResult NoHand() => new MatchResult { Feedback = SignFeedback.ShowHandToCamera, Severity = FeedbackSeverity.Neutral };

        [Test]
        public void SplitsWithAzerbaijaniCaseRules()
        {
            List<string> letters = FingerspellingTask.SplitLetters("əli", StoryTestPack.Alphabet);
            Assert.AreEqual(3, letters.Count);
            Assert.AreEqual("Ə", letters[0]);
            Assert.AreEqual("L", letters[1]);
            Assert.AreEqual("İ", letters[2], "Azerbaijani i uppercases to İ, not I");

            Assert.AreEqual("I", FingerspellingTask.SplitLetters("ı", StoryTestPack.Alphabet)[0]);
            Assert.AreEqual(5, FingerspellingTask.SplitLetters("Ana-Li", StoryTestPack.Alphabet).Count, "hyphens are dropped");
        }

        [Test]
        public void ForeignLettersAreShownButNotAsked()
        {
            StoryTestPack pack = StoryTestPack.WithAlphabet();
            var task = new FingerspellingTask(new[] { "Wa" }, StoryTestPack.Alphabet, l => pack.Dictionary.Find("letter." + l));
            Assert.AreEqual(2, task.Letters.Count);
            Assert.AreEqual(LetterStatus.Unsupported, task.Letters[0].Status);
            Assert.AreEqual(1, task.AskedCount);
            Assert.AreEqual("A", task.Current.Letter, "starts at the first askable letter");
        }

        [Test]
        public void TrackingProblemsNeverCountAndMistakesUnlockSkip()
        {
            StoryTestPack pack = StoryTestPack.WithAlphabet();
            var task = new FingerspellingTask(new[] { "Al" }, StoryTestPack.Alphabet, l => pack.Dictionary.Find("letter." + l), 3);

            for (int i = 0; i < 5; i++) Assert.IsFalse(task.Submit(NoHand()).CanSkip);
            Assert.AreEqual(0, task.Current.Failures);

            task.Submit(Mistake());
            task.Submit(Mistake());
            Assert.IsTrue(task.Submit(Mistake()).CanSkip);

            task.SkipCurrent();
            Assert.AreEqual("L", task.Current.Letter);
            SpellOutcome last = task.Submit(Correct(true));
            Assert.IsTrue(last.TaskComplete);
            Assert.AreEqual(LetterStatus.Skipped, task.Letters[0].Status);
            Assert.AreEqual(LetterStatus.SelfChecked, task.Letters[1].Status, "letters without references are self-checked");
            Assert.IsFalse(task.AllVerified);
        }
    }

    public sealed class StorySessionTests
    {
        private static MatchResult Ok() => new MatchResult { Feedback = SignFeedback.Correct, Severity = FeedbackSeverity.None, Unverified = true };

        [SetUp]
        public void English()
        {
            Loc.SetLanguage("en");
        }

        [Test]
        public void StoryOneFlowsFromQuestionToReply()
        {
            StoryTestPack pack = StoryTestPack.WithAlphabet();
            StoryData story = pack.AddStory(1, null);
            var session = new StorySession(story, pack, "", "");
            var phases = new List<StoryPhase>();
            session.PhaseChanged += phases.Add;
            bool finished = false;
            session.Finished += s => finished = true;

            session.Begin();
            Assert.AreEqual(StoryPhase.NpcSpeaking, session.Phase);
            session.NpcLineFinished();
            Assert.AreEqual(StoryPhase.AwaitingResponse, session.Phase);

            Assert.IsTrue(session.NeedsName);
            Assert.IsFalse(session.BeginResponse(), "the camera can't open before the name is known");
            session.SetName("Əli", "Quliyev");
            Assert.IsFalse(session.NeedsName);
            Assert.IsTrue(session.BeginResponse());
            Assert.AreEqual(StoryPhase.Responding, session.Phase);
            Assert.AreEqual(10, session.Spelling.Letters.Count);
            Assert.AreEqual(1, session.Spelling.Letters[3].WordIndex, "surname is the second row");

            SpellOutcome outcome = default(SpellOutcome);
            for (int i = 0; i < 10; i++) outcome = session.SubmitAttempt(Ok());
            Assert.IsTrue(outcome.TaskComplete);
            Assert.AreEqual(StoryPhase.NpcSpeaking, session.Phase);
            Assert.AreEqual("Nice to meet you, Əli!", session.FormatText(session.CurrentBeat));

            session.NpcLineFinished();
            Assert.IsTrue(session.IsFinished);
            Assert.IsTrue(finished);
            Assert.AreEqual(1f, session.Progress01, 1e-5);
            Assert.IsFalse(session.AllVerified, "self-checked letters never earn the perfect bonus");
            Assert.AreEqual(StoryPhase.Completed, phases[phases.Count - 1]);
        }

        [Test]
        public void NameSignBeatAsksForTheNameSignAndMatchesAKnownFeature()
        {
            StoryTestPack pack = StoryTestPack.WithAlphabet();
            var glasses = ScriptableObject.CreateInstance<SignDefinition>();
            glasses.EditorInitialize("word.eynek", "Eynək", SignKind.Word, false, false, "test");
            pack.PackDictionary.EditorAdd(glasses);

            var story = ScriptableObject.CreateInstance<StoryData>();
            story.EditorSetIdentity("story.ns", "Story", "");
            var beats = new List<StoryBeat>
            {
                new StoryBeat { type = StoryBeatType.NpcLine, text = new LocalizedString { en = "What is your name sign?", az = "Sənin ad işarən nədir?" } },
                new StoryBeat { type = StoryBeatType.LearnerNameSign },
                new StoryBeat { type = StoryBeatType.NpcLine, text = new LocalizedString { en = "Nice to meet you, {namesign}!", az = "Tanış olduğuma şadam, {namesign}!" } }
            };
            story.EditorSetContent(1, null, 20, null, null, new StoryCharacter(), beats);

            var session = new StorySession(story, pack, "Ana", "", "");
            session.Begin();
            session.NpcLineFinished();
            Assert.IsFalse(session.NeedsName);
            Assert.IsTrue(session.NeedsNameSign);
            Assert.IsFalse(session.BeginResponse(), "the camera waits for the name sign");

            session.SetNameSign("Eynəkli Ana");
            Assert.IsFalse(session.NeedsNameSign);
            Assert.AreSame(glasses, session.NameSignTarget);
            Assert.IsTrue(session.BeginResponse());
            Assert.IsNull(session.Spelling, "no letter-by-letter spelling");

            SpellOutcome outcome = session.SubmitAttempt(Ok());
            Assert.IsTrue(outcome.TaskComplete);
            Assert.AreEqual("Nice to meet you, Eynəkli Ana!", session.FormatText(session.CurrentBeat));

            session.SetNameSign("Gülüş");
            Assert.IsNull(session.NameSignTarget, "an own name sign is not a dictionary sign");
        }

        [Test]
        public void ClosingTheCameraWaitsAndCantUseCameraSkips()
        {
            StoryTestPack pack = StoryTestPack.WithAlphabet();
            var session = new StorySession(pack.AddStory(1, null), pack, "Ana", "Li");
            session.Begin();
            session.NpcLineFinished();
            session.BeginResponse();

            session.CancelResponse();
            Assert.AreEqual(StoryPhase.AwaitingResponse, session.Phase);

            session.BeginResponse();
            session.SkipResponse();
            Assert.IsTrue(session.UsedCameraSkip);
            Assert.IsFalse(session.AllVerified);
            Assert.AreEqual(StoryPhase.NpcSpeaking, session.Phase);
        }

        [Test]
        public void TrackingProblemsAreNotCountedAsAttempts()
        {
            StoryTestPack pack = StoryTestPack.WithAlphabet();
            var session = new StorySession(pack.AddStory(1, null), pack, "Ana", "Li");
            session.Begin();
            session.NpcLineFinished();
            session.BeginResponse();
            session.SubmitAttempt(new MatchResult { Feedback = SignFeedback.ShowHandToCamera, Severity = FeedbackSeverity.Neutral });
            Assert.AreEqual(0, session.Attempts);
            session.SubmitAttempt(new MatchResult { Feedback = SignFeedback.AdjustHandShape, Severity = FeedbackSeverity.Soft });
            Assert.AreEqual(1, session.Attempts);
            Assert.AreEqual(0f, session.Accuracy, 1e-5);
        }
    }

    public sealed class StoryUnlockTests
    {
        [Test]
        public void StoryUnlocksWhenItsUnitIsDoneAndIsAnnouncedOnce()
        {
            StoryTestPack pack = StoryTestPack.WithAlphabet();
            UnitData unit1 = StoryTestPack.Unit("u1", 2);
            UnitData unit2 = StoryTestPack.Unit("u2", 2);
            pack.AddLevel("a1", unit1, unit2);
            StoryData story1 = pack.AddStory(1, unit1);
            pack.AddStory(2, unit2, playable: false);
            var progress = new PackProgress();

            Assert.AreEqual(StoryState.Locked, StoryProgression.Build(pack, progress)[0].State);

            progress.MarkLessonCompleted("u1.les1");
            progress.MarkLessonCompleted("u1.les2");
            List<StoryEntry> entries = StoryProgression.Build(pack, progress);
            Assert.AreEqual(StoryState.Unlocked, entries[0].State);
            Assert.IsTrue(entries[0].IsNew);
            Assert.AreEqual(StoryState.Locked, entries[1].State);
            Assert.AreEqual(1, StoryProgression.NewlyUnlocked(pack, progress).Count);

            progress.MarkStoryAnnounced(story1.Id);
            Assert.AreEqual(0, StoryProgression.NewlyUnlocked(pack, progress).Count);

            progress.MarkStoryCompleted(story1.Id);
            Assert.AreEqual(StoryState.Completed, StoryProgression.Build(pack, progress)[0].State);
        }

        [Test]
        public void CheckpointUnitCompletesOnlyThroughTheGate()
        {
            StoryTestPack pack = StoryTestPack.WithAlphabet();
            UnitData review = StoryTestPack.Unit("u10", 2, checkpoint: true);
            LevelData level = pack.AddLevel("a1", StoryTestPack.Unit("u1", 1), review);
            var progress = new PackProgress();
            progress.MarkLessonCompleted("u10.les1");
            progress.MarkLessonCompleted("u10.les2");
            Assert.IsFalse(StoryProgression.IsUnitComplete(review, level, progress));
            progress.MarkGatePassed(level.GateTestId);
            Assert.IsTrue(StoryProgression.IsUnitComplete(review, level, progress));
        }

        [Test]
        public void GateJoinsTheCheckpointUnitOnThePath()
        {
            StoryTestPack pack = StoryTestPack.WithAlphabet();
            pack.AddLevel("a1", StoryTestPack.Unit("u1", 2), StoryTestPack.Unit("u2", 1), StoryTestPack.Unit("u3", 2, checkpoint: true));
            var progress = new PackProgress();
            progress.MarkLessonCompleted("u1.les1");
            progress.MarkLessonCompleted("u1.les2");

            List<LevelPathSummary> levels = UnitPathBuilder.Build(new CurriculumProgression().Build(pack, progress));
            Assert.AreEqual(1, levels.Count);
            List<UnitPathEntry> units = levels[0].Units;
            Assert.AreEqual(3, units.Count, "the gate is part of unit 3, not a fourth entry");
            Assert.AreEqual(3, units[2].Nodes.Count);
            Assert.IsTrue(units[2].IsCheckpoint);
            Assert.AreEqual(NodeState.Completed, units[0].State);
            Assert.AreEqual(NodeState.Active, units[1].State);
            Assert.AreEqual(NodeState.Locked, units[2].State);
            Assert.AreEqual(1, levels[0].CompletedUnits);
            Assert.AreSame(levels[0], UnitPathBuilder.CurrentLevel(levels));
        }
    }

    public sealed class LeagueTests
    {
        private static GamificationConfig Config() => ScriptableObject.CreateInstance<GamificationConfig>();

        [SetUp]
        public void English()
        {
            Loc.SetLanguage("en");
        }

        [Test]
        public void OfflineLeagueShowsOnlyYouWithoutFakeRivals()
        {
            var service = new LocalLeagueService(Config());
            LeagueSnapshot snapshot = service.GetSnapshot(0, "User", 350, new DateTime(2026, 10, 23, 12, 0, 0));
            Assert.AreEqual(1, snapshot.Entries.Count);
            Assert.IsTrue(snapshot.Offline);
            Assert.AreEqual(1, snapshot.You.Rank);
            Assert.AreEqual(LeagueZone.Stay, snapshot.You.Zone, "alone in a league: no promotion");
            Assert.AreEqual("Bronze League", LeagueTiers.LeagueName(0));
            Assert.AreEqual("2 days left", LocalLeagueService.FormatTimeLeft(snapshot.TimeLeft), "Friday noon, 2.5 days before Monday");
        }

        [Test]
        public void ZonesScaleWithGroupSize()
        {
            var snapshot = new LeagueSnapshot();
            for (int i = 0; i < 9; i++) snapshot.Entries.Add(new LeagueEntry { DisplayName = "P" + i, Xp = 100 * (i + 1) });
            snapshot.Entries.Add(new LeagueEntry { DisplayName = "You", Xp = 650, IsYou = true });
            LocalLeagueService.Rank(snapshot, Config());
            Assert.AreEqual(3, snapshot.PromoteCount);
            Assert.AreEqual(2, snapshot.DemoteCount);
            Assert.AreEqual(4, snapshot.You.Rank);
            Assert.AreEqual(LeagueZone.Promote, snapshot.Entries[2].Zone);
            Assert.AreEqual(LeagueZone.Demote, snapshot.Entries[9].Zone);
        }

        [Test]
        public void NoOneMovesWhenAloneInTheLeague()
        {
            GamificationConfig config = Config();
            var service = new LocalLeagueService(config);
            int tier = 1;
            string settled = "";
            var xp = new XpState { weekly = 600, week = "2026-W43" };
            DateTime now = new DateTime(2026, 10, 27);

            Assert.AreEqual(0, LeagueSettlement.Settle(ref tier, ref settled, xp, "2026-W43", now, service, config), "current week not settled");
            Assert.AreEqual(0, LeagueSettlement.Settle(ref tier, ref settled, xp, "2026-W44", now, service, config));
            Assert.AreEqual(1, tier);
            Assert.AreEqual("2026-W43", settled);
        }

        [Test]
        public void InitialsUseAzerbaijaniUppercase()
        {
            Assert.AreEqual("İA", LocalLeagueService.InitialsOf("ilkin aliyev"));
            Assert.AreEqual("İI", Loc.ToUpperAz("iı"));
        }
    }
}
