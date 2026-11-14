using System;
using System.Collections.Generic;
using Cevir.Core.Curriculum;
using Cevir.Core.Game;
using Cevir.Core.Gamification;
using Cevir.Core.LanguagePacks;
using Cevir.Core.Recognition;

namespace Cevir.Core.Lesson
{
    public enum SessionKind
    {
        Lesson,
        GateTest
    }

    public struct AttemptOutcome
    {
        public bool Passed;
        public bool HeartLost;
        public bool OutOfHearts;

        /// <summary>True when the UI should move to the next exercise (passed, or given up after losing a heart).</summary>
        public bool Advance;

        /// <summary>Camera attempts only: the message to show.</summary>
        public SignFeedback Feedback;
    }

    /// <summary>
    /// Runtime state of one lesson or gate test: walks the exercise list and applies the heart rules.
    /// Pure C#, so the lesson flow is unit-testable without scenes or a camera.
    /// </summary>
    public sealed class LessonSession
    {
        private readonly List<Exercise> _exercises;
        private readonly HeartSystem _hearts;
        private readonly ReviewScheduler _reviews;
        private readonly PackProgress _packProgress;
        private readonly Func<DateTime> _utcNow;
        private readonly bool _usesHearts;

        private int _index;
        private float _miniGameScore = -1f;

        public event Action<Exercise> ExerciseChanged;
        public event Action<LessonSession> Finished;

        public LessonSession(SessionKind kind, LessonData lesson, LevelData level, List<Exercise> exercises,
            HeartSystem hearts, ReviewScheduler reviews, PackProgress packProgress, Func<DateTime> utcNow)
        {
            Kind = kind;
            Lesson = lesson;
            Level = level;
            _exercises = exercises;
            _hearts = hearts;
            _reviews = reviews;
            _packProgress = packProgress;
            _utcNow = utcNow;

            // Gate tests are exams and practice lessons are how hearts come back; neither costs hearts.
            _usesHearts = kind == SessionKind.Lesson && lesson != null && !lesson.IsHeartFree;
        }

        public SessionKind Kind { get; private set; }
        public LessonData Lesson { get; private set; }
        public LevelData Level { get; private set; }

        public IReadOnlyList<Exercise> Exercises => _exercises;
        public int Index => _index;
        public Exercise Current => _index < _exercises.Count ? _exercises[_index] : null;
        public bool IsFinished { get; private set; }
        public bool FailedOutOfHearts { get; private set; }

        public float Progress01 => _exercises.Count == 0 ? 1f : _index / (float)_exercises.Count;

        public int ScoredCount
        {
            get
            {
                int n = 0;
                foreach (Exercise e in _exercises) if (e.IsScored && !e.Requeued) n++;
                return n;
            }
        }

        public int PassedFirstTryCount
        {
            get
            {
                int n = 0;
                foreach (Exercise e in _exercises) if (e.PassedFirstTry && !e.Requeued) n++;
                return n;
            }
        }

        public float Accuracy => ScoredCount == 0 ? 1f : PassedFirstTryCount / (float)ScoredCount;

        /// <summary>True if every camera exercise was passed perfectly on the first try (the +5 XP bonus).</summary>
        public bool AllCameraPerfect
        {
            get
            {
                bool any = false;
                foreach (Exercise e in _exercises)
                {
                    if (e.Type != ExerciseType.CameraSign || e.Requeued) continue;
                    any = true;
                    if (!e.Perfect || !e.PassedFirstTry) return false;
                }

                return any;
            }
        }

        public bool GatePassed => Kind == SessionKind.GateTest && Level != null && Accuracy >= Level.GateTest.passRatio;

        /// <summary>Set by mini-game controllers (0..1) before the session completes.</summary>
        public float MiniGameScore
        {
            get { return _miniGameScore >= 0f ? _miniGameScore : Accuracy; }
            set { _miniGameScore = UnityEngine.Mathf.Clamp01(value); }
        }

        public void Begin()
        {
            _index = 0;
            if (_exercises.Count == 0)
            {
                Finish();
                return;
            }

            RaiseChanged();
        }

        public void CompleteWatch()
        {
            Exercise e = Current;
            if (e == null || e.Type != ExerciseType.Watch) return;
            e.Passed = true;
            Advance();
        }

        public AttemptOutcome SubmitCameraAttempt(MatchResult result)
        {
            var outcome = new AttemptOutcome { Feedback = result.Feedback };
            Exercise e = Current;
            if (e == null || e.Type != ExerciseType.CameraSign) return outcome;

            if (result.IsCorrect)
            {
                e.Passed = true;
                e.PassedFirstTry = e.SoftFailures == 0;
                e.Perfect = result.IsPerfect;
                Record(e, e.PassedFirstTry);
                outcome.Passed = true;
                outcome.Advance = true;
                Advance();
                return outcome;
            }

            if (!result.CountsAsFailedAttempt)
            {
                // Tracking problems and "unsure" are never held against the learner.
                return outcome;
            }

            e.SoftFailures++;
            if (Kind == SessionKind.GateTest)
            {
                if (e.SoftFailures >= _hearts.SoftFailuresPerHeart)
                {
                    Record(e, false);
                    outcome.Advance = true;
                    Advance();
                }

                return outcome;
            }

            if (_usesHearts && _hearts.RegisterSoftFailure(e.SoftFailures))
            {
                outcome.HeartLost = true;
            }

            if (_hearts.RetryCameraSignInPlace)
            {
                // Duolingo-style: a wrong sign costs a heart and the learner tries the same sign again.
                if (e.SoftFailures == 1) Record(e, false);
                CheckOutOfHearts(ref outcome);
                return outcome;
            }

            if (e.SoftFailures >= _hearts.SoftFailuresPerHeart)
            {
                Record(e, false);
                Requeue(e);
                outcome.Advance = true;
                if (!CheckOutOfHearts(ref outcome)) Advance();
            }

            return outcome;
        }

        public AttemptOutcome SubmitChoice(SignDefinition chosen)
        {
            var outcome = new AttemptOutcome();
            Exercise e = Current;
            if (e == null || e.Type != ExerciseType.Recognize) return outcome;

            bool correct = chosen == e.Sign;
            e.Passed = correct;
            e.PassedFirstTry = correct;
            Record(e, correct);
            outcome.Passed = correct;
            outcome.Advance = true;

            if (!correct)
            {
                // A wrong quiz answer is a hard error.
                if (_usesHearts) outcome.HeartLost = _hearts.LoseHeart();
                Requeue(e);
                if (CheckOutOfHearts(ref outcome)) return outcome;
            }

            Advance();
            return outcome;
        }

        /// <summary>"Can't use the camera now": skips the camera exercise without a penalty or reward.</summary>
        public void SkipCurrent()
        {
            if (Current == null) return;
            Advance();
        }

        public void Abandon()
        {
            IsFinished = true;
        }

        private bool CheckOutOfHearts(ref AttemptOutcome outcome)
        {
            if (!_usesHearts || _hearts.HasHearts) return false;
            outcome.OutOfHearts = true;
            FailedOutOfHearts = true;
            Finish();
            return true;
        }

        private void Requeue(Exercise e)
        {
            if (Kind == SessionKind.Lesson && !e.Requeued)
            {
                _exercises.Add(e.CloneForRetry());
            }
        }

        private void Record(Exercise e, bool correct)
        {
            if (_reviews != null && _packProgress != null && e.Sign != null)
            {
                _reviews.Record(_packProgress, e.Sign.SignId, correct, _utcNow());
            }
        }

        private void Advance()
        {
            _index++;
            if (_index >= _exercises.Count)
            {
                Finish();
                return;
            }

            RaiseChanged();
        }

        private void Finish()
        {
            if (IsFinished) return;
            IsFinished = true;
            if (Finished != null) Finished(this);
        }

        private void RaiseChanged()
        {
            if (ExerciseChanged != null) ExerciseChanged(Current);
        }
    }
}
