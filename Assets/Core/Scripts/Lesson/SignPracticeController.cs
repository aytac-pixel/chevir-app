using System;
using System.Collections.Generic;
using Cevir.Core.Game;
using Cevir.Core.LanguagePacks;
using Cevir.Core.Recognition;
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
        public bool IsCalibrated => _baseline.SampleCount > 0;

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
            SetPhase(Phase.WaitingForHand);
        }

        private void Update()
        {
            // Timeouts are checked here as well, so they fire even if the camera stops delivering frames.
            double now = Time.realtimeSinceStartupAsDouble;
            if (CurrentPhase == Phase.WaitingForHand && now - _phaseStart > waitForHandTimeout)
            {
                Score();
            }
            else if (CurrentPhase == Phase.Recording && now - _phaseStart > maxAttemptSeconds)
            {
                Score();
            }
        }

        private void OnFrame(LandmarkFrame frame)
        {
            double now = Time.realtimeSinceStartupAsDouble;

            switch (CurrentPhase)
            {
                case Phase.Calibrating:
                    _baseline.Add(_extractor.ComputeNonManual(frame, _extractor.DominantHand == HandSide.Left));
                    if (now - _phaseStart >= calibrationSeconds)
                    {
                        _extractor.Baseline = _baseline.Average();
                        SetPhase(Phase.Idle);
                        Action done = _calibrationDone;
                        _calibrationDone = null;
                        if (done != null) done();
                    }

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
            if (_presenceOnly)
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
