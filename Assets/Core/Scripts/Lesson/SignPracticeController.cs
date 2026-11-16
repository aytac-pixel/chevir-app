using System;
using System.Collections.Generic;
using Cevir.Core.Game;
using Cevir.Core.LanguagePacks;
using Cevir.Core.Recognition;
using Cevir.Core.Recognition.Letters;
using UnityEngine;

namespace Cevir.Core.Lesson
{
    /// <summary>
    /// Connects the live landmark provider to the matcher for one camera exercise (Təkrar, review, gate).
    /// Each frame is converted to features as it arrives, so the end of an attempt only runs the match:
    /// this is what keeps feedback inside the plan's 300 ms target.
    /// </summary>
    public sealed class SignPracticeController : MonoBehaviour
    {
        public enum Phase
        {
            Idle,
            Calibrating,
            WaitingForHand,
            Recording,
            Scoring
        }

        [Tooltip("Component implementing ILandmarkProvider, e.g. MediaPipeLandmarkProvider.")]
        [SerializeField] private MonoBehaviour landmarkProvider;

        [Header("Attempt window")]
        [SerializeField, Min(0.5f)] private float maxAttemptSeconds = 3f;
        [SerializeField, Min(0.2f)] private float minAttemptSeconds = 0.8f;

        [Tooltip("Attempt ends early once the hand has been out of frame this long after recording started.")]
        [SerializeField, Min(0.1f)] private float handLostEndSeconds = 0.5f;

        [Tooltip("Give up waiting for a hand after this long and report 'show your hand'.")]
        [SerializeField, Min(1f)] private float waitForHandTimeout = 6f;

        [SerializeField, Min(0.2f)] private float calibrationSeconds = 1f;

        private const int MaxFrames = 240;

        [Header("Fingerspelling (letter model)")]
        [Tooltip("A letter passes when the model names it with at least this probability...")]
        [SerializeField, Range(0.2f, 0.95f)] private float letterMinProbability = 0.5f;

        [Tooltip("...on this many evaluations in a row.")]
        [SerializeField, Min(1)] private int letterHitsNeeded = 2;

        [Tooltip("Longest a letter attempt may last once the hand is seen.")]
        [SerializeField, Min(2f)] private float letterMaxSeconds = 8f;

        private readonly AzslLetterRecognizer _letters = new AzslLetterRecognizer();
        private int _letterIndex = -1;
        private int _letterHits;
        private int _letterEvalTick;
        private int _letterHandFrames;
        private float _letterBest;
        private bool _letterPassed;

        /// <summary>True while the current attempt is judged by the letter model.</summary>
        public bool LetterMode => _letterIndex >= 0;

        [Header("Words (word model)")]
        [Tooltip("A word passes when the model's top word is the target with at least this probability.")]
        [SerializeField, Range(0.1f, 0.95f)] private float wordMinProbability = 0.3f;

        [Tooltip("Below the pass line but at least this sure: 'not sure', which costs no heart.")]
        [SerializeField, Range(0.05f, 0.9f)] private float wordUnsureProbability = 0.15f;

        [Tooltip("Longest a word may be signed before it is checked anyway.")]
        [SerializeField, Min(2f)] private float wordMaxSeconds = 10f;

        [Tooltip("Give up when no word was signed in this long.")]
        [SerializeField, Min(2f)] private float wordWaitSeconds = 12f;

        private readonly AzslWordRecognizer _words = new AzslWordRecognizer();
        private int _wordClass = -1;
        private int _wordHandFrames;
        private double _wordStart;
        private AzslWordRecognizer.Job _wordJob;

        /// <summary>True while the current attempt is judged by the word model.</summary>
        public bool WordMode => _wordClass >= 0;

        /// <summary>True when one of the web translator's models can judge this sign (letters or words).</summary>
        public static bool ModelCanJudge(SignDefinition sign)
        {
            if (sign == null) return false;
            if (sign.Kind == SignKind.Fingerspelling)
                return AzslLetterRecognizer.Available && AzslLetterRecognizer.LabelIndex(sign.Gloss) >= 0;
            return AzslWordRecognizer.Available && AzslWordRecognizer.ClassIndex(sign.Gloss) >= 0;
        }

        private readonly SignFeatureExtractor _extractor = new SignFeatureExtractor();
        private readonly NonManualBaseline _baseline = new NonManualBaseline();
        private readonly SignFeatureFrame[] _pool = new SignFeatureFrame[MaxFrames];
        private readonly List<SignFeatureFrame> _attempt = new List<SignFeatureFrame>(MaxFrames);

        private ILandmarkProvider _provider;
        private ILanguagePack _pack;
        private SignMatcher _matcher;
        private SignDefinition _target;
        private IReadOnlyList<SignDefinition> _candidates;
        private Action<MatchResult> _callback;
        private bool _presenceOnly;

        private double _phaseStart;
        private double _lastHandSeen;
        private Action _calibrationDone;

        public Phase CurrentPhase { get; private set; }
        public ILandmarkProvider Provider => _provider;
        public bool IsCalibrated { get; private set; }

        /// <summary>Raised after every scored attempt (for analytics and debug overlays).</summary>
        public event Action<MatchResult> AttemptScored;

        /// <summary>Raised for every processed frame, for the live landmark overlay.</summary>
        public event Action<LandmarkFrame, SignFeatureFrame> FrameProcessed;

        private void Awake()
        {
            for (int i = 0; i < MaxFrames; i++) _pool[i] = new SignFeatureFrame();
            _provider = landmarkProvider as ILandmarkProvider;
            if (landmarkProvider != null && _provider == null)
            {
                Debug.LogError("[Chevir] " + landmarkProvider.name + " does not implement ILandmarkProvider.");
            }
        }

        private void OnEnable()
        {
            if (_provider != null) _provider.FrameReady += OnFrame;
        }

        private void OnDisable()
        {
            if (_provider != null) _provider.FrameReady -= OnFrame;
        }

        public void SetProvider(ILandmarkProvider provider)
        {
            if (_provider != null) _provider.FrameReady -= OnFrame;
            _provider = provider;
            if (_provider != null && isActiveAndEnabled) _provider.FrameReady += OnFrame;
        }

        /// <summary>Prepares matching for a pack. Call when a lesson starts.</summary>
        public void Configure(ILanguagePack pack)
        {
            _pack = pack;
            _matcher = new SignMatcher(pack.MatcherConfig, pack.CreateClassifier());
            bool leftHanded = GameManager.Instance != null && GameManager.Instance.Progress.leftHanded;
            _extractor.DominantHand = leftHanded ? HandSide.Left : HandSide.Right;
            var packAware = _provider as IPackAwareLandmarkProvider;
            if (packAware != null && !_provider.IsRunning) packAware.ApplyPack(pack);
            if (_provider != null && !_provider.IsRunning) _provider.StartCapture();
        }

        /// <summary>Measures the learner's neutral face so non-manual checks are relative to it.</summary>
        public void Calibrate(Action done)
        {
            _baseline.Reset();
            _calibrationDone = done;
            SetPhase(Phase.Calibrating);
        }

        /// <summary>Waits for a hand, records one attempt and scores it against <paramref name="target"/>.</summary>
        public void BeginAttempt(SignDefinition target, Action<MatchResult> onResult)
        {
            _target = target;
            _candidates = null;
            _presenceOnly = false;
            _callback = onResult;
            StartWaiting();
        }

        /// <summary>
        /// For signs without reference data: records one attempt and only checks that the hand was clearly
        /// visible. The result is Correct with <see cref="MatchResult.Unverified"/> set, or "show your hand".
        /// </summary>
        public void BeginPresenceCheck(SignDefinition target, Action<MatchResult> onResult)
        {
            _target = target;
            _candidates = null;
            _presenceOnly = true;
            _callback = onResult;
            StartWaiting();
        }

        /// <summary>Like BeginAttempt, but picks the best of a small expected-answer set (avatar dialogue).</summary>
        public void BeginAttemptAmong(IReadOnlyList<SignDefinition> candidates, Action<MatchResult> onResult)
        {
            _target = null;
            _candidates = candidates;
            _presenceOnly = false;
            _callback = onResult;
            StartWaiting();
        }

        public void Cancel()
        {
            _callback = null;
            _wordClass = -1;
            _wordJob = null;
            SetPhase(Phase.Idle);
        }

        public void StopCamera()
        {
            Cancel();
            if (_provider != null && _provider.IsRunning) _provider.StopCapture();
        }

        private void StartWaiting()
        {
            if (_matcher == null)
            {
                Debug.LogError("[Chevir] SignPracticeController.Configure must be called before an attempt.");
                return;
            }

            _attempt.Clear();
            _letterIndex = -1;
            if (_target != null && _candidates == null && _target.Kind == SignKind.Fingerspelling && AzslLetterRecognizer.Available)
            {
                _letterIndex = AzslLetterRecognizer.LabelIndex(_target.Gloss);
            }

            _wordClass = -1;
            if (_target != null && _candidates == null && !_presenceOnly && _target.Kind != SignKind.Fingerspelling && AzslWordRecognizer.Available)
            {
                _wordClass = AzslWordRecognizer.ClassIndex(_target.Gloss);
            }

            _words.Reset();
            _wordHandFrames = 0;
            _wordJob = null;
            _wordStart = Time.realtimeSinceStartupAsDouble;

            _letters.Reset();
            _letterHits = 0;
            _letterEvalTick = 0;
            _letterHandFrames = 0;
            _letterBest = 0f;
            _letterPassed = false;
            SetPhase(Phase.WaitingForHand);
        }

        private void Update()
        {
            // Timeouts are checked here as well, so they fire even if the camera stops delivering frames.
            double now = Time.realtimeSinceStartupAsDouble;
            if (WordMode)
            {
                WordUpdate(now);
                return;
            }

            if (CurrentPhase == Phase.Calibrating && now - _phaseStart > calibrationSeconds + CalibrationGrace)
            {
                // No face frames arrived in time: carry on with a neutral baseline so the lesson never stalls.
                FinishCalibration();
            }
            else if (CurrentPhase == Phase.WaitingForHand && now - _phaseStart > waitForHandTimeout)
            {
                Score();
            }
            else if (CurrentPhase == Phase.Recording && now - _phaseStart > (LetterMode ? letterMaxSeconds : maxAttemptSeconds))
            {
                Score();
            }
        }

        private void OnFrame(LandmarkFrame frame)
        {
            double now = Time.realtimeSinceStartupAsDouble;

            if (LetterMode && (CurrentPhase == Phase.WaitingForHand || CurrentPhase == Phase.Recording))
            {
                LetterFrame(frame, now);
                return;
            }

            if (WordMode && (CurrentPhase == Phase.WaitingForHand || CurrentPhase == Phase.Recording))
            {
                WordFrame(frame);
                return;
            }

            switch (CurrentPhase)
            {
                case Phase.Calibrating:
                    _baseline.Add(_extractor.ComputeNonManual(frame, _extractor.DominantHand == HandSide.Left));
                    if (now - _phaseStart >= calibrationSeconds) FinishCalibration();

                    break;

                case Phase.WaitingForHand:
                    if (frame.AnyHandPresent)
                    {
                        SetPhase(Phase.Recording);
                        _lastHandSeen = now;
                        Capture(frame);
                    }

                    break;

                case Phase.Recording:
                    Capture(frame);
                    if (frame.AnyHandPresent) _lastHandSeen = now;

                    bool longEnough = now - _phaseStart >= minAttemptSeconds;
                    bool handGone = now - _lastHandSeen >= handLostEndSeconds;
                    if ((longEnough && handGone) || _attempt.Count >= MaxFrames)
                    {
                        Score();
                    }

                    break;
            }

            if (FrameProcessed != null && _attempt.Count > 0) FrameProcessed(frame, _attempt[_attempt.Count - 1]);
        }

        private const float CalibrationGrace = 2f;

        private void FinishCalibration()
        {
            if (_baseline.SampleCount > 0) _extractor.Baseline = _baseline.Average();
            IsCalibrated = true;
            SetPhase(Phase.Idle);
            Action done = _calibrationDone;
            _calibrationDone = null;
            if (done != null) done();
        }

        private void LetterFrame(LandmarkFrame frame, double now)
        {
            _letters.Push(frame);
            if (frame.AnyHandPresent)
            {
                _lastHandSeen = now;
                _letterHandFrames++;
                if (CurrentPhase == Phase.WaitingForHand) SetPhase(Phase.Recording);
            }

            if (CurrentPhase != Phase.Recording) return;

            if (frame.AnyHandPresent && _letters.FrameCount >= 8 && ++_letterEvalTick % 3 == 0)
            {
                float[] probs = _letters.Evaluate();
                if (probs != null)
                {
                    int best = 0;
                    for (int i = 1; i < probs.Length; i++) if (probs[i] > probs[best]) best = i;
                    float p = probs[_letterIndex];
                    if (p > _letterBest) _letterBest = p;
                    _letterHits = best == _letterIndex && p >= letterMinProbability ? _letterHits + 1 : 0;
                    if (_letterHits >= letterHitsNeeded)
                    {
                        _letterPassed = true;
                        Score();
                        return;
                    }
                }
            }

            // The hand left for a while: end the attempt.
            if (now - _lastHandSeen >= 1.5) Score();
        }

        private void WordFrame(LandmarkFrame frame)
        {
            if (frame.AnyHandPresent) _wordHandFrames++;
            AzslWordRecognizer.Span span;
            bool ended = _words.Push(frame, out span);
            if (ended)
            {
                StartWordJudge(span);
                return;
            }

            // A sign starts when a hand rises above the rest line; a too short one goes back to waiting.
            if (_words.Open && CurrentPhase == Phase.WaitingForHand) SetPhase(Phase.Recording);
            else if (!_words.Open && CurrentPhase == Phase.Recording) SetPhase(Phase.WaitingForHand);
        }

        private void StartWordJudge(AzslWordRecognizer.Span span)
        {
            SetPhase(Phase.Scoring);
            _wordJob = _words.Judge(span, _wordClass, wordMinProbability);
        }

        private void WordUpdate(double now)
        {
            switch (CurrentPhase)
            {
                case Phase.WaitingForHand:
                    if (now - _wordStart > wordWaitSeconds) Score();
                    break;
                case Phase.Recording:
                    if (now - _phaseStart > wordMaxSeconds)
                    {
                        AzslWordRecognizer.Span span;
                        if (_words.Flush(out span)) StartWordJudge(span);
                        else SetPhase(Phase.WaitingForHand);
                    }

                    break;
                case Phase.Scoring:
                    if (_wordJob != null && _wordJob.Done) Score();
                    break;
            }
        }

        private MatchResult ScoreWord()
        {
            AzslWordRecognizer.Job job = _wordJob;
            bool judged = job != null && job.Done;
            bool passed = judged && job.Passed;
            if (judged)
            {
                if (!string.IsNullOrEmpty(job.Error)) Debug.LogError("[Chevir] Word check failed: " + job.Error);
                AzslLetterRecognizer.LastGuessText = (job.Guess ?? "-") + " " + Mathf.RoundToInt(job.GuessP * 100f) + "% · " +
                                                     AzslWordRecognizer.ClassName(_wordClass) + " " + Mathf.RoundToInt(job.BestTarget * 100f) + "%";
            }

            float p = judged ? job.BestTarget : 0f;
            SignFeedback feedback;
            FeedbackSeverity severity;
            if (passed) { feedback = SignFeedback.Correct; severity = FeedbackSeverity.None; }
            else if (!judged) { feedback = SignFeedback.ShowHandToCamera; severity = FeedbackSeverity.Neutral; }
            else if (p >= wordUnsureProbability) { feedback = SignFeedback.Unsure; severity = FeedbackSeverity.Neutral; }
            else { feedback = SignFeedback.AdjustMovement; severity = FeedbackSeverity.Soft; }

            return new MatchResult
            {
                SignId = _target != null ? _target.SignId : null,
                Feedback = feedback,
                Severity = severity,
                ManualScore = p,
                HandShapeScore = p,
                ClassifierProbability = p,
                HandVisibleRatio = _wordHandFrames >= 8 ? 1f : 0f,
                NonManualScore = 1f,
                BestReferenceIndex = -1,
                IsPerfect = passed && p >= 0.8f
            };
        }

        private MatchResult ScoreLetter()
        {
            bool shown = _letterHandFrames >= 8;
            SignFeedback feedback = _letterPassed ? SignFeedback.Correct : shown ? SignFeedback.AdjustHandShape : SignFeedback.ShowHandToCamera;
            return new MatchResult
            {
                SignId = _target != null ? _target.SignId : null,
                Feedback = feedback,
                Severity = _letterPassed ? FeedbackSeverity.None : shown ? FeedbackSeverity.Soft : FeedbackSeverity.Neutral,
                ManualScore = _letterBest,
                HandShapeScore = _letterBest,
                ClassifierProbability = _letterBest,
                HandVisibleRatio = shown ? 1f : 0f,
                NonManualScore = 1f,
                BestReferenceIndex = -1,
                IsPerfect = _letterPassed && _letterBest >= 0.8f
            };
        }

        private void Capture(LandmarkFrame frame)
        {
            if (_attempt.Count >= MaxFrames) return;
            SignFeatureFrame features = _pool[_attempt.Count];
            _extractor.Extract(frame, features);
            _attempt.Add(features);
        }

        private void Score()
        {
            SetPhase(Phase.Scoring);

            // Drop trailing frames after the hand left, so the end of the attempt isn't padded with nothing.
            while (_attempt.Count > 0 && !_attempt[_attempt.Count - 1].dominantPresent)
            {
                _attempt.RemoveAt(_attempt.Count - 1);
            }

            MatchResult result;
            if (LetterMode)
            {
                result = ScoreLetter();
                _letterIndex = -1;
            }
            else if (WordMode)
            {
                result = ScoreWord();
                _wordClass = -1;
                _wordJob = null;
            }
            else if (_presenceOnly)
            {
                result = ScorePresence();
            }
            else if (_candidates != null)
            {
                result = _matcher.MatchAmong(_attempt, _candidates);
            }
            else
            {
                result = _matcher.Match(_attempt, _target);
                if (_pack != null) result = _pack.PostProcess(result, _target);
            }

            SetPhase(Phase.Idle);
            if (AttemptScored != null) AttemptScored(result);
            Action<MatchResult> callback = _callback;
            _callback = null;
            if (callback != null) callback(result);
        }

        private MatchResult ScorePresence()
        {
            const int minFrames = 8;
            int visible = 0;
            foreach (SignFeatureFrame frame in _attempt)
            {
                if (frame.dominantPresent) visible++;
            }

            bool shown = visible >= minFrames;
            return new MatchResult
            {
                SignId = _target != null ? _target.SignId : null,
                Feedback = shown ? SignFeedback.Correct : SignFeedback.ShowHandToCamera,
                Severity = shown ? FeedbackSeverity.None : FeedbackSeverity.Neutral,
                HandVisibleRatio = _attempt.Count > 0 ? visible / (float)_attempt.Count : 0f,
                NonManualScore = 1f,
                BestReferenceIndex = -1,
                ClassifierProbability = -1f,
                Unverified = shown
            };
        }

        private void SetPhase(Phase phase)
        {
            CurrentPhase = phase;
            _phaseStart = Time.realtimeSinceStartupAsDouble;
        }
    }
}
