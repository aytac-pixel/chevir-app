using System;
using System.Collections.Generic;
using Cevir.Core.Curriculum;
using Cevir.Core.Game;
using Cevir.Core.Gamification;
using Cevir.Core.LanguagePacks;
using Cevir.Core.Lesson;
using Cevir.Core.Recognition;
using Cevir.Core.Stories;
using NUnit.Framework;
using UnityEngine;

namespace Cevir.Core.Tests
{
    public sealed class ProgressionTests
    {
        private sealed class FakePack : ILanguagePack
        {
            public List<LevelData> LevelList = new List<LevelData>();
            public List<StoryData> StoryList = new List<StoryData>();
            public string LanguageCode => "test";
            public LanguagePackManifest Manifest => new LanguagePackManifest();
            public IReadOnlyList<LevelData> Levels => LevelList;
            public IReadOnlyList<StoryData> Stories => StoryList;
            public SignDictionary Dictionary => null;
            public SignMatcherConfig MatcherConfig => null;
            public TextAsset RecognitionModel => null;
            public TextAsset HandLandmarkerModelOverride => null;
            public TextAsset FaceLandmarkerModelOverride => null;
            public bool IsPlayable => true;
            public ISignClassifier CreateClassifier() => null;
            public MatchResult PostProcess(MatchResult result, SignDefinition target) => result;
        }

        private static FakePack TwoLevels(bool allowTestOut)
        {
            var pack = new FakePack();
            for (int l = 1; l <= 2; l++)
            {
                var lessons = new List<LessonData>();
                for (int i = 1; i <= 3; i++)
                {
                    var lesson = ScriptableObject.CreateInstance<LessonData>();
                    lesson.EditorSetIdentity("l" + l + ".les" + i, "Lesson", "");
                    lessons.Add(lesson);
                }

                var unit = ScriptableObject.CreateInstance<UnitData>();
                unit.EditorSetIdentity("l" + l + ".u1", "Unit", "");
                unit.EditorSetContent(Color.white, lessons);

                var level = ScriptableObject.CreateInstance<LevelData>();
                level.EditorSetIdentity("l" + l, "Level", "");
                level.EditorSetContent((CurriculumLevel)l, Color.white, new GateTestConfig { allowTestOut = allowTestOut }, false,
                    new List<UnitData> { unit });
                pack.LevelList.Add(level);
            }

            return pack;
        }

        [Test]
        public void FreshLearnerHasFirstLessonActiveAndRestLocked()
        {
            List<PathNode> path = new CurriculumProgression().Build(TwoLevels(false), new PackProgress());

            Assert.AreEqual(8, path.Count);
            Assert.AreEqual(NodeState.Active, path[0].State);
            Assert.AreEqual(NodeState.Locked, path[1].State);
            Assert.AreEqual(NodeState.Locked, path[3].State, "gate locked without test-out");
            Assert.AreEqual(NodeState.Locked, path[4].State, "next level locked");
        }

        [Test]
        public void GateBecomesActiveAfterAllLessonsAndOpensNextLevel()
        {
            FakePack pack = TwoLevels(false);
            var progress = new PackProgress();
            foreach (string id in new[] { "l1.les1", "l1.les2", "l1.les3" }) progress.MarkLessonCompleted(id);

            List<PathNode> path = new CurriculumProgression().Build(pack, progress);
            Assert.AreEqual(NodeState.Active, path[3].State);
            Assert.AreEqual(NodeState.Locked, path[4].State);

            progress.MarkGatePassed(pack.LevelList[0].GateTestId);
            path = new CurriculumProgression().Build(pack, progress);
            Assert.AreEqual(NodeState.Completed, path[3].State);
            Assert.AreEqual(NodeState.Active, path[4].State);
        }

        [Test]
        public void TestingOutLeavesSkippedLessonsUnlocked()
        {
            FakePack pack = TwoLevels(true);
            var progress = new PackProgress();
            List<PathNode> path = new CurriculumProgression().Build(pack, progress);
            Assert.AreEqual(NodeState.Unlocked, path[3].State, "test-out gate is available early");

            progress.MarkGatePassed(pack.LevelList[0].GateTestId);
            path = new CurriculumProgression().Build(pack, progress);
            Assert.AreEqual(NodeState.Unlocked, path[0].State);
            Assert.AreEqual(NodeState.Active, path[4].State);
        }

        [Test]
        public void WrongSignCostsAHeartAndRetriesTheSameSign()
        {
            int hearts = 10;
            var heartSystem = new HeartSystem(GamificationConfig.CreateDefault(), () => hearts, v => hearts = v);
            var lesson = ScriptableObject.CreateInstance<LessonData>();
            var sign = ScriptableObject.CreateInstance<SignDefinition>();
            var plan = new List<Exercise> { new Exercise { Type = ExerciseType.CameraSign, Sign = sign } };
            var session = new LessonSession(SessionKind.Lesson, lesson, null, plan, heartSystem, null, null, () => DateTime.UtcNow);
            session.Begin();

            var soft = new MatchResult { Feedback = SignFeedback.AddFacialExpression, Severity = FeedbackSeverity.Soft };
            var tracking = new MatchResult { Feedback = SignFeedback.ShowHandToCamera, Severity = FeedbackSeverity.Neutral };

            session.SubmitCameraAttempt(tracking);
            Assert.AreEqual(10, hearts, "tracking problems are free");

            AttemptOutcome wrong = session.SubmitCameraAttempt(soft);
            Assert.IsTrue(wrong.HeartLost);
            Assert.IsFalse(wrong.Advance, "the learner tries the same sign again");
            Assert.AreEqual(9, hearts);
            Assert.AreSame(sign, session.Current.Sign);

            session.SubmitCameraAttempt(new MatchResult { Feedback = SignFeedback.Correct, IsPerfect = true });
            Assert.IsTrue(session.IsFinished);
            Assert.IsFalse(session.AllCameraPerfect, "a retried sign does not earn the perfect bonus");
        }

        [Test]
        public void RunningOutOfHeartsEndsTheLesson()
        {
            int hearts = 1;
            var heartSystem = new HeartSystem(GamificationConfig.CreateDefault(), () => hearts, v => hearts = v);
            var lesson = ScriptableObject.CreateInstance<LessonData>();
            var sign = ScriptableObject.CreateInstance<SignDefinition>();
            var plan = new List<Exercise> { new Exercise { Type = ExerciseType.CameraSign, Sign = sign } };
            var session = new LessonSession(SessionKind.Lesson, lesson, null, plan, heartSystem, null, null, () => DateTime.UtcNow);
            session.Begin();

            AttemptOutcome outcome = session.SubmitCameraAttempt(new MatchResult { Feedback = SignFeedback.AdjustHandShape, Severity = FeedbackSeverity.Soft });
            Assert.IsTrue(outcome.OutOfHearts);
            Assert.IsTrue(session.FailedOutOfHearts);
        }

        [Test]
        public void LettersAreShownThenSignedOneByOne()
        {
            var a = ScriptableObject.CreateInstance<SignDefinition>();
            var b = ScriptableObject.CreateInstance<SignDefinition>();
            var lesson = ScriptableObject.CreateInstance<LessonData>();
            lesson.EditorSetContent(LessonType.Standard, new List<SignDefinition> { a, b });
            lesson.EditorSetSteps(new List<LessonStep> { LessonStep.Watch, LessonStep.Practice });

            List<Exercise> plan = new LessonPlanBuilder(new ReviewScheduler(), 1).Build(lesson, null, new PackProgress(), DateTime.UtcNow);
            Assert.AreEqual(4, plan.Count);
            Assert.AreEqual(ExerciseType.Watch, plan[0].Type);
            Assert.AreSame(a, plan[0].Sign);
            Assert.AreEqual(ExerciseType.CameraSign, plan[1].Type);
            Assert.AreSame(a, plan[1].Sign);
            Assert.AreEqual(ExerciseType.Watch, plan[2].Type);
            Assert.AreSame(b, plan[2].Sign);
            Assert.AreEqual(ExerciseType.CameraSign, plan[3].Type);
        }
    }
}
