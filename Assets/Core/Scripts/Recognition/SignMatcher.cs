using System.Collections.Generic;
using Cevir.Core.LanguagePacks;
using UnityEngine;

namespace Cevir.Core.Recognition
{
    /// <summary>
    /// Scores a learner's attempt against a target sign's reference recordings and picks one feedback message.
    ///
    /// Manual check: handshape (finger joint angles, spread, thumb contacts, palm orientation),
    /// trajectory (wrist path relative to its start, compared with DTW) and location (where the hand is
    /// relative to the face). Static signs such as most fingerspelled letters skip the trajectory term.
    /// Non-manual check: peak brow/mouth/head values compared with the sign's approved rule.
    /// Decision layer: merges both into the plan's feedback table. Optionally blends a learned classifier.
    ///
    /// Language-agnostic: everything language-specific comes from the pack's references, rules and config.
    /// </summary>
    public sealed class SignMatcher
    {
        private const int PalmNormalStart = SignFeatureFrame.HandShapeLength - 3;

        private readonly SignMatcherConfig _config;
        private readonly ISignClassifier _classifier;

        private readonly List<SignFeatureFrame> _userTracked = new List<SignFeatureFrame>(256);
        private readonly List<SignFeatureFrame> _refTracked = new List<SignFeatureFrame>(256);
        private readonly List<float> _scratchValues = new List<float>(256);
        private float[] _shapeWeights1;
        private float[] _shapeWeights2;

        public SignMatcher(SignMatcherConfig config, ISignClassifier classifier = null)
        {
            _config = config != null ? config : SignMatcherConfig.CreateDefault();
            _classifier = classifier;
            _shapeWeights1 = BuildShapeWeights(1);
            _shapeWeights2 = BuildShapeWeights(2);
        }

        public SignMatcherConfig Config => _config;

        /// <summary>
        /// Picks the best of a small candidate set (e.g. the expected answers at the current step of an
        /// avatar dialogue). A small, known answer space is what keeps recognition accurate there.
        /// </summary>
        public MatchResult MatchAmong(IReadOnlyList<SignFeatureFrame> attempt, IReadOnlyList<SignDefinition> candidates)
        {
            MatchResult best = default;
            bool hasBest = false;
            foreach (SignDefinition candidate in candidates)
            {
                MatchResult result = Match(attempt, candidate);
                if (!hasBest || Rank(result) > Rank(best))
                {
                    best = result;
                    hasBest = true;
                }
            }

            return best;
        }

        public MatchResult Match(IReadOnlyList<SignFeatureFrame> attempt, SignDefinition target)
        {
            var result = new MatchResult
            {
                SignId = target != null ? target.SignId : null,
                NonManualScore = 1f,
                BestReferenceIndex = -1,
                ClassifierProbability = -1f
            };

            if (target == null || !target.HasRecognitionData)
            {
                if (target != null)
                {
                    Debug.LogWarning("[Çevir] Sign '" + target.SignId + "' has no reference data; cannot score.");
                }

                return Finish(ref result, SignFeedback.Unsure, FeedbackSeverity.Neutral);
            }

            bool twoHanded = target.TwoHanded;

            // 1. Tracking quality.
            CollectTracked(attempt, twoHanded, _userTracked);
            int total = attempt != null ? attempt.Count : 0;
            result.HandVisibleRatio = total > 0 ? _userTracked.Count / (float)total : 0f;
            if (_userTracked.Count < _config.minTrackedFrames || result.HandVisibleRatio < _config.minHandVisibleRatio)
            {
                return Finish(ref result, SignFeedback.ShowHandToCamera, FeedbackSeverity.Neutral);
            }

            // 2. Manual score against every reference; keep the best.
            float bestManual = -1f;
            float bestDirectionCosine = 1f;
            bool bestHasDirection = false;
            IReadOnlyList<SignReference> references = target.References;
            for (int r = 0; r < references.Count; r++)
            {
                SignReference reference = references[r];
                if (reference == null || !reference.IsValid) continue;

                CollectTracked(reference.Frames, twoHanded, _refTracked);
                if (_refTracked.Count < 2) continue;

                float shape = ScoreHandShape(_userTracked, _refTracked, twoHanded, target.Dynamic);
                float trajectory = 1f;
                float directionCosine = 1f;
                bool hasDirection = false;
                if (target.Dynamic)
                {
                    trajectory = ScoreTrajectory(_userTracked, _refTracked, twoHanded, out directionCosine, out hasDirection);
                }

                bool locationKnown;
                float location = ScoreLocation(_userTracked, _refTracked, out locationKnown);

                float wShape = _config.handShapeWeight;
                float wTraj = target.Dynamic ? _config.trajectoryWeight : 0f;
                float wLoc = locationKnown ? _config.locationWeight : 0f;
                float manual = (wShape * shape + wTraj * trajectory + wLoc * location) / Mathf.Max(wShape + wTraj + wLoc, 1e-5f);

                if (manual > bestManual)
                {
                    bestManual = manual;
                    result.BestReferenceIndex = r;
                    result.HandShapeScore = shape;
                    result.TrajectoryScore = trajectory;
                    result.LocationScore = locationKnown ? location : 1f;
                    bestDirectionCosine = directionCosine;
                    bestHasDirection = hasDirection;
                }
            }

            if (result.BestReferenceIndex < 0)
            {
                return Finish(ref result, SignFeedback.Unsure, FeedbackSeverity.Neutral);
            }

            result.ManualScore = bestManual;

            // 3. Optional learned model.
            float probability;
            if (_classifier != null && _classifier.TryScore(_userTracked, target, out probability))
            {
                result.ClassifierProbability = probability;
                result.ManualScore = Mathf.Lerp(result.ManualScore, probability, _config.classifierWeight);
            }

            // 4. Non-manual check.
            bool faceMissing;
            bool nonManualPassed = EvaluateNonManual(attempt, target.NonManualRule, ref result, out faceMissing);

            // 5. Decision layer: one message. A high weighted score is not enough on its own: a correct
            // handshape must not carry a sign whose movement is clearly wrong (or the reverse).
            bool directionOk = !target.Dynamic || !bestHasDirection || bestDirectionCosine >= _config.minDirectionCosine;
            bool subScoresOk = result.HandShapeScore >= _config.handShapePassThreshold
                               && (!target.Dynamic || result.TrajectoryScore >= _config.motionPassThreshold)
                               && directionOk;
            if (result.ManualScore >= _config.passThreshold && subScoresOk)
            {
                if (nonManualPassed)
                {
                    result.IsPerfect = result.ManualScore >= _config.perfectThreshold;
                    return Finish(ref result, SignFeedback.Correct, FeedbackSeverity.None);
                }

                return faceMissing
                    ? Finish(ref result, SignFeedback.Unsure, FeedbackSeverity.Neutral)
                    : Finish(ref result, SignFeedback.AddFacialExpression, FeedbackSeverity.Soft);
            }

            if (!directionOk)
            {
                return Finish(ref result, SignFeedback.AdjustMovement, FeedbackSeverity.Soft);
            }

            if (result.HandShapeScore < _config.handShapePassThreshold)
            {
                return Finish(ref result, SignFeedback.AdjustHandShape, FeedbackSeverity.Soft);
            }

            float motion = target.Dynamic ? Mathf.Min(result.TrajectoryScore, result.LocationScore) : result.LocationScore;
            if (motion < _config.motionPassThreshold)
            {
                return Finish(ref result, SignFeedback.AdjustMovement, FeedbackSeverity.Soft);
            }

            if (result.ManualScore >= _config.unsureThreshold)
            {
                return Finish(ref result, SignFeedback.Unsure, FeedbackSeverity.Neutral);
            }

            return Finish(ref result, SignFeedback.AdjustHandShape, FeedbackSeverity.Soft);
        }

        private static MatchResult Finish(ref MatchResult result, SignFeedback feedback, FeedbackSeverity severity)
        {
            result.Feedback = feedback;
            result.Severity = severity;
            return result;
        }

        private static float Rank(MatchResult result)
        {
            return (result.IsCorrect ? 10f : 0f) + result.ManualScore;
        }

        private static void CollectTracked(IReadOnlyList<SignFeatureFrame> frames, bool twoHanded, List<SignFeatureFrame> output)
        {
            output.Clear();
            if (frames == null) return;
            for (int i = 0; i < frames.Count; i++)
            {
                SignFeatureFrame f = frames[i];
                if (f.dominantPresent && (!twoHanded || f.nonDominantPresent))
                {
                    output.Add(f);
                }
            }
        }

        // ---------------------------------------------------------------- handshape

        private float ScoreHandShape(List<SignFeatureFrame> user, List<SignFeatureFrame> reference, bool twoHanded, bool dynamic)
        {
            float[] weights = twoHanded ? _shapeWeights2 : _shapeWeights1;

            if (dynamic)
            {
                float[,] a = SequenceMath.Resample(ShapeRows(user, twoHanded), _config.resampleLength);
                float[,] b = SequenceMath.Resample(ShapeRows(reference, twoHanded), _config.resampleLength);
                float distance = SequenceMath.Dtw(a, b, weights, _config.dtwBandRatio);
                return SequenceMath.Similarity(distance, _config.handShapeTolerance);
            }

            // Static sign: the learner usually transitions into the shape, holds it and drops it.
            // Compare each frame with the reference's mean shape and keep the best-held quarter.
            float[] target = MeanShape(reference, twoHanded);
            var row = new float[target.Length];
            _scratchValues.Clear();
            for (int i = 0; i < user.Count; i++)
            {
                FillShapeRow(user[i], twoHanded, row);
                _scratchValues.Add(WeightedRms(row, target, weights));
            }

            float held = SequenceMath.Percentile(_scratchValues, 0.25f);
            return SequenceMath.Similarity(held, _config.handShapeTolerance);
        }

        private float[] BuildShapeWeights(int hands)
        {
            var weights = new float[SignFeatureFrame.HandShapeLength * hands];
            for (int h = 0; h < hands; h++)
            {
                for (int i = 0; i < SignFeatureFrame.HandShapeLength; i++)
                {
                    weights[h * SignFeatureFrame.HandShapeLength + i] = i >= PalmNormalStart ? _config.palmOrientationWeight : 1f;
                }
            }

            return weights;
        }

        private static List<float[]> ShapeRows(List<SignFeatureFrame> frames, bool twoHanded)
        {
            var rows = new List<float[]>(frames.Count);
            int dim = SignFeatureFrame.HandShapeLength * (twoHanded ? 2 : 1);
            for (int i = 0; i < frames.Count; i++)
            {
                var row = new float[dim];
                FillShapeRow(frames[i], twoHanded, row);
                rows.Add(row);
            }

            return rows;
        }

        private static void FillShapeRow(SignFeatureFrame frame, bool twoHanded, float[] row)
        {
            System.Array.Copy(frame.dominantShape, 0, row, 0, SignFeatureFrame.HandShapeLength);
            if (twoHanded)
            {
                System.Array.Copy(frame.nonDominantShape, 0, row, SignFeatureFrame.HandShapeLength, SignFeatureFrame.HandShapeLength);
            }
        }

        private static float[] MeanShape(List<SignFeatureFrame> frames, bool twoHanded)
        {
            int dim = SignFeatureFrame.HandShapeLength * (twoHanded ? 2 : 1);
            var mean = new float[dim];
            var row = new float[dim];
            for (int i = 0; i < frames.Count; i++)
            {
                FillShapeRow(frames[i], twoHanded, row);
                for (int d = 0; d < dim; d++) mean[d] += row[d];
            }

            for (int d = 0; d < dim; d++) mean[d] /= frames.Count;
            return mean;
        }

        private static float WeightedRms(float[] a, float[] b, float[] weights)
        {
            float sum = 0f;
            float weightSum = 0f;
            for (int d = 0; d < a.Length; d++)
            {
                float diff = a[d] - b[d];
                sum += diff * diff * weights[d];
                weightSum += weights[d];
            }

            return Mathf.Sqrt(sum / Mathf.Max(weightSum, 1e-5f));
        }

        // ---------------------------------------------------------------- trajectory and location

        private float ScoreTrajectory(List<SignFeatureFrame> user, List<SignFeatureFrame> reference, bool twoHanded,
            out float directionCosine, out bool hasDirection)
        {
            float[,] a = SequenceMath.Resample(TrajectoryRows(user, twoHanded), _config.resampleLength);
            float[,] b = SequenceMath.Resample(TrajectoryRows(reference, twoHanded), _config.resampleLength);
            float distance = SequenceMath.Dtw(a, b, null, _config.dtwBandRatio);

            Vector2 refNet = reference[reference.Count - 1].dominantPosition - reference[0].dominantPosition;
            Vector2 userNet = user[user.Count - 1].dominantPosition - user[0].dominantPosition;
            hasDirection = refNet.magnitude >= _config.minDirectionMagnitude;
            directionCosine = hasDirection && userNet.sqrMagnitude > 1e-6f
                ? Vector2.Dot(refNet.normalized, userNet.normalized)
                : (hasDirection ? 0f : 1f);

            return SequenceMath.Similarity(distance, _config.trajectoryTolerance);
        }

        private static List<float[]> TrajectoryRows(List<SignFeatureFrame> frames, bool twoHanded)
        {
            var rows = new List<float[]>(frames.Count);
            Vector2 d0 = frames[0].dominantPosition;
            Vector2 n0 = frames[0].nonDominantPosition;
            for (int i = 0; i < frames.Count; i++)
            {
                Vector2 d = frames[i].dominantPosition - d0;
                if (twoHanded)
                {
                    Vector2 n = frames[i].nonDominantPosition - n0;
                    rows.Add(new[] { d.x, d.y, n.x, n.y });
                }
                else
                {
                    rows.Add(new[] { d.x, d.y });
                }
            }

            return rows;
        }

        private float ScoreLocation(List<SignFeatureFrame> user, List<SignFeatureFrame> reference, out bool known)
        {
            Vector2 userMean;
            Vector2 refMean;
            bool userAnchored = MeanAnchoredPosition(user, out userMean);
            bool refAnchored = MeanAnchoredPosition(reference, out refMean);
            known = userAnchored && refAnchored;
            if (!known) return 1f;

            return SequenceMath.Similarity(Vector2.Distance(userMean, refMean), _config.locationTolerance);
        }

        private static bool MeanAnchoredPosition(List<SignFeatureFrame> frames, out Vector2 mean)
        {
            mean = Vector2.zero;
            int count = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                if (!frames[i].faceAnchored) continue;
                mean += frames[i].dominantPosition;
                count++;
            }

            if (count < frames.Count / 2 || count == 0) return false;
            mean /= count;
            return true;
        }

        // ---------------------------------------------------------------- non-manual

        private bool EvaluateNonManual(IReadOnlyList<SignFeatureFrame> attempt, NonManualRule rule, ref MatchResult result, out bool faceMissing)
        {
            faceMissing = false;
            result.NonManualScore = 1f;
            result.NonManualEvaluated = false;

            if (rule == null || rule.Requirements.Count == 0 || (!rule.Approved && !_config.enforceUnapprovedRules))
            {
                return true;
            }

            int faceFrames = 0;
            for (int i = 0; i < attempt.Count; i++)
            {
                if (attempt[i].nonManual.present) faceFrames++;
            }

            if (faceFrames < Mathf.Max(2, attempt.Count / 2))
            {
                faceMissing = true;
                result.NonManualScore = 0f;
                return false;
            }

            result.NonManualEvaluated = true;
            bool passed = true;
            float minScore = 1f;
            foreach (NonManualRequirement requirement in rule.Requirements)
            {
                _scratchValues.Clear();
                bool absolute = requirement.comparison == NonManualComparison.AbsoluteAtLeast;
                for (int i = 0; i < attempt.Count; i++)
                {
                    if (!attempt[i].nonManual.present) continue;
                    float value = attempt[i].nonManual.Get(requirement.feature);
                    _scratchValues.Add(absolute ? Mathf.Abs(value) : value);
                }

                float peak = SequenceMath.Percentile(_scratchValues, _config.nonManualPeakPercentile);
                float threshold = Mathf.Max(Mathf.Abs(requirement.threshold), 1e-4f);
                float score;
                bool ok;
                if (requirement.comparison == NonManualComparison.AtMost)
                {
                    ok = peak <= threshold;
                    score = ok ? 1f : Mathf.Clamp01(threshold / Mathf.Max(peak, 1e-4f));
                }
                else
                {
                    ok = peak >= threshold;
                    score = Mathf.Clamp01(peak / threshold);
                }

                passed &= ok;
                minScore = Mathf.Min(minScore, score);
            }

            result.NonManualScore = minScore;
            return passed;
        }
    }
}
