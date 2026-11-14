namespace Cevir.Core.Recognition
{
    /// <summary>The single message chosen by the decision layer, per the plan's feedback table.</summary>
    public enum SignFeedback
    {
        /// <summary>Düzgün.</summary>
        Correct,

        /// <summary>Barmaq bucağını düzəlt: finger angles outside the target.</summary>
        AdjustHandShape,

        /// <summary>Hərəkəti yuxarıdan başla: trajectory mismatch.</summary>
        AdjustMovement,

        /// <summary>Mimikanı əlavə et: face/head grammar below target.</summary>
        AddFacialExpression,

        /// <summary>Əlini kameraya göstər: hand left the frame / low tracking confidence.</summary>
        ShowHandToCamera,

        /// <summary>Əmin deyiləm: borderline score; never costs a heart.</summary>
        Unsure
    }

    public enum FeedbackSeverity
    {
        /// <summary>Passed.</summary>
        None,

        /// <summary>Tracking/borderline issue; not the learner's fault and not counted as a failed attempt.</summary>
        Neutral,

        /// <summary>Correctable mistake; costs a heart only after repeated failures.</summary>
        Soft
    }

    /// <summary>
    /// Outcome of one attempt. Sub-scores are 0..1. A non-manual score of 1 with
    /// <see cref="NonManualEvaluated"/> false means the sign has no (approved) rule.
    /// </summary>
    public struct MatchResult
    {
        public string SignId;
        public SignFeedback Feedback;
        public FeedbackSeverity Severity;

        public float ManualScore;
        public float HandShapeScore;
        public float TrajectoryScore;
        public float LocationScore;
        public float NonManualScore;
        public bool NonManualEvaluated;
        public float ClassifierProbability;
        public float HandVisibleRatio;

        public int BestReferenceIndex;

        public bool IsCorrect => Feedback == SignFeedback.Correct;
        public bool IsPerfect;

        /// <summary>
        /// Accepted without a handshape check (the sign has no reference recordings yet): only hand visibility was
        /// checked. Never counts as perfect.
        /// </summary>
        public bool Unverified;

        /// <summary>True when the attempt should count toward the "3 failed attempts" heart rule.</summary>
        public bool CountsAsFailedAttempt => Severity == FeedbackSeverity.Soft;

        public override string ToString()
        {
            return string.Format("{0}: {1} manual={2:F2} (shape {3:F2}, traj {4:F2}, loc {5:F2}) nm={6:F2} vis={7:F2}",
                SignId, Feedback, ManualScore, HandShapeScore, TrajectoryScore, LocationScore, NonManualScore, HandVisibleRatio);
        }
    }
}
