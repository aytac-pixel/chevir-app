using System.Collections.Generic;
using System.Reflection;
using Cevir.Core.LanguagePacks;
using Cevir.Core.Recognition;
using NUnit.Framework;
using UnityEngine;

namespace Cevir.Core.Tests
{
    public sealed class SignMatcherTests
    {
        private static readonly Vector2 Start = new Vector2(0.5f, 0.55f);
        private static readonly Vector2 End = new Vector2(0.5f, 0.35f);

        [Test]
        public void SameSignPasses()
        {
            SignDefinition sign = TestSigns.Sign("up", true, TestSigns.Sequence(Start, End, 0f, 0f));
            var matcher = new SignMatcher(SignMatcherConfig.CreateDefault());

            MatchResult result = matcher.Match(TestSigns.Sequence(Start, End, 0f, 0f, 24), sign);

            Assert.AreEqual(SignFeedback.Correct, result.Feedback, result.ToString());
            Assert.Greater(result.ManualScore, 0.9f);
        }

        [Test]
        public void ScaleAndPositionDoNotMatter()
        {
            SignDefinition sign = TestSigns.Sign("up", true, TestSigns.Sequence(Start, End, 0f, 0f));
            var matcher = new SignMatcher(SignMatcherConfig.CreateDefault());

            // Same motion, slower (more frames): DTW absorbs the speed difference.
            MatchResult result = matcher.Match(TestSigns.Sequence(Start, End, 0f, 0f, 50), sign);

            Assert.IsTrue(result.IsCorrect, result.ToString());
        }

        [Test]
        public void ReversedMovementAsksToFixMovement()
        {
            SignDefinition sign = TestSigns.Sign("up", true, TestSigns.Sequence(Start, End, 0f, 0f));
            var matcher = new SignMatcher(SignMatcherConfig.CreateDefault());

            MatchResult result = matcher.Match(TestSigns.Sequence(End, Start, 0f, 0f), sign);

            Assert.AreEqual(SignFeedback.AdjustMovement, result.Feedback, result.ToString());
            Assert.AreEqual(FeedbackSeverity.Soft, result.Severity);
        }

        [Test]
        public void WrongHandShapeAsksToFixFingers()
        {
            SignDefinition sign = TestSigns.Sign("flat", false, TestSigns.Sequence(Start, Start, 0f, 0f));
            var matcher = new SignMatcher(SignMatcherConfig.CreateDefault());

            MatchResult result = matcher.Match(TestSigns.Sequence(Start, Start, 1f, 1f), sign);

            Assert.AreEqual(SignFeedback.AdjustHandShape, result.Feedback, result.ToString());
        }

        [Test]
        public void NoHandAsksToShowHand()
        {
            SignDefinition sign = TestSigns.Sign("flat", false, TestSigns.Sequence(Start, Start, 0f, 0f));
            var matcher = new SignMatcher(SignMatcherConfig.CreateDefault());
            List<SignFeatureFrame> attempt = TestSigns.Sequence(Start, Start, 0f, 0f);
            for (int i = 0; i < attempt.Count; i++) attempt[i].dominantPresent = i % 4 == 0;

            MatchResult result = matcher.Match(attempt, sign);

            Assert.AreEqual(SignFeedback.ShowHandToCamera, result.Feedback);
            Assert.IsFalse(result.CountsAsFailedAttempt);
        }

        [Test]
        public void MissingApprovedBrowRaiseAsksForFacialExpression()
        {
            NonManualRule rule = Rule(approved: true);
            SignDefinition sign = TestSigns.Sign("question", true, TestSigns.Sequence(Start, End, 0f, 0f, 30, 0.8f), rule);
            var matcher = new SignMatcher(SignMatcherConfig.CreateDefault());

            MatchResult neutral = matcher.Match(TestSigns.Sequence(Start, End, 0f, 0f, 30, 0f), sign);
            MatchResult raised = matcher.Match(TestSigns.Sequence(Start, End, 0f, 0f, 30, 0.7f), sign);

            Assert.AreEqual(SignFeedback.AddFacialExpression, neutral.Feedback, neutral.ToString());
            Assert.AreEqual(SignFeedback.Correct, raised.Feedback, raised.ToString());
        }

        [Test]
        public void UnapprovedRulesAreNotEnforced()
        {
            NonManualRule rule = Rule(approved: false);
            SignDefinition sign = TestSigns.Sign("question", true, TestSigns.Sequence(Start, End, 0f, 0f), rule);
            var matcher = new SignMatcher(SignMatcherConfig.CreateDefault());

            MatchResult result = matcher.Match(TestSigns.Sequence(Start, End, 0f, 0f), sign);

            Assert.AreEqual(SignFeedback.Correct, result.Feedback);
            Assert.IsFalse(result.NonManualEvaluated);
        }

        [Test]
        public void LeftHandedLearnerMatchesRightHandedReference()
        {
            SignDefinition sign = TestSigns.Sign("up", true, TestSigns.Sequence(Start, End, 0.3f, 0.3f));
            var extractor = new SignFeatureExtractor { DominantHand = HandSide.Left };
            var attempt = new List<SignFeatureFrame>();
            for (int i = 0; i < 30; i++)
            {
                float t = i / 29f;
                LandmarkFrame frame = TestSigns.Frame(Vector2.Lerp(Start, End, t), 0.3f);

                // Mirror the right hand into a left hand on the other side of the face.
                for (int p = 0; p < HandLandmarks.Count; p++)
                {
                    Vector3 v = frame.RightHand.Points[p];
                    frame.LeftHand.Points[p] = new Vector3(1f - v.x, v.y, v.z);
                }

                frame.LeftHand.IsPresent = true;
                frame.RightHand.IsPresent = false;
                var f = new SignFeatureFrame();
                extractor.Extract(frame, f);
                attempt.Add(f);
            }

            MatchResult result = new SignMatcher(SignMatcherConfig.CreateDefault()).Match(attempt, sign);

            Assert.IsTrue(result.IsCorrect, result.ToString());
        }

        private static NonManualRule Rule(bool approved)
        {
            var rule = ScriptableObject.CreateInstance<NonManualRule>();
            var reqs = new List<NonManualRequirement>
            {
                new NonManualRequirement { feature = NonManualFeature.BrowRaise, comparison = NonManualComparison.AtLeast, threshold = 0.4f }
            };
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(NonManualRule).GetField("requirements", flags).SetValue(rule, reqs);
            typeof(NonManualRule).GetField("approved", flags).SetValue(rule, approved);
            return rule;
        }
    }
}
