using System.Collections.Generic;
using Cevir.Core.Audio;
using Cevir.Core.Curriculum;
using Cevir.Core.Game;
using Cevir.Core.Gamification;
using Cevir.Core.LanguagePacks;
using Cevir.Core.Lesson;
using Cevir.Core.Recognition;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Cevir.Core.UI
{
    /// <summary>
    /// Runs the active <see cref="LessonSession"/> on screen: HUD (close, progress bar, hearts, XP), the
    /// exercise area (reference clip, live camera, answer choices), live recognition status, and hands
    /// results to <see cref="FeedbackOverlayUI"/>. Camera attempts go through <see cref="SignPracticeController"/>;
    /// heart rules stay in <see cref="LessonSession"/> / <see cref="HeartSystem"/>.
    /// </summary>
    public sealed class LessonScreenUI : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] private SignPracticeController practice;
        [SerializeField] private FeedbackOverlayUI overlay;
        [SerializeField] private CameraPreviewUI cameraPreview;
        [SerializeField] private LandmarkOverlayUI landmarkOverlay;

        [Header("HUD")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Image progressFill;
        [SerializeField] private TextMeshProUGUI heartsLabel;
        [SerializeField] private RectTransform heartIcon;
        [SerializeField] private TextMeshProUGUI xpLabel;

        [Header("Prompt")]
        [SerializeField] private TextMeshProUGUI promptLabel;
        [SerializeField] private TextMeshProUGUI glossLabel;
        [SerializeField] private TextMeshProUGUI statusLabel;

        [Header("Reference clip")]
        [SerializeField] private GameObject mediaPanel;
        [SerializeField] private RawImage videoImage;
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private Image thumbnailImage;
        [Tooltip("Row holding the playback speed buttons, shown while a lesson video plays.")]
        [SerializeField] private GameObject speedRow;
        [Tooltip("One button per entry of PlaybackSpeeds (1x, 0.75x, 0.5x), in that order.")]
        [SerializeField] private List<Button> speedButtons = new List<Button>();

        [Header("Camera")]
        [SerializeField] private GameObject cameraPanel;

        [Header("Choices")]
        [SerializeField] private GameObject choicesPanel;
        [SerializeField] private List<Button> choiceButtons = new List<Button>();

        [Header("Actions")]
        [SerializeField] private Button primaryButton;
        [SerializeField] private TextMeshProUGUI primaryLabel;
        [SerializeField] private Button skipButton;

        [SerializeField, Min(0.05f)] private float progressLerpSpeed = 6f;

        private GameManager _game;
        private LessonSession _session;
        private Exercise _shown;
        private RenderTexture _videoTexture;
        /// <summary>Speeds the learner can pick while a lesson video plays; every new video starts at the first (1x).</summary>
        public static readonly float[] PlaybackSpeeds = { 1f, 0.75f, 0.5f };

        private int _speedIndex;
        private bool _attempting;
        private bool _awaitingFeedback;
        private float _progressTarget;

        private static string UiLanguage => Localization.Loc.Language;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(OnClose);
            if (primaryButton != null) primaryButton.onClick.AddListener(OnPrimary);
            if (skipButton != null) skipButton.onClick.AddListener(OnSkip);
            for (int i = 0; i < speedButtons.Count; i++)
            {
                int index = i;
                if (speedButtons[i] != null) speedButtons[i].onClick.AddListener(() => SetSpeed(index));
            }
            for (int i = 0; i < choiceButtons.Count; i++)
            {
                int index = i;
                choiceButtons[i].onClick.AddListener(() => OnChoice(index));
            }
        }

        private void OnEnable()
        {
            _game = GameManager.Instance;
            if (_game == null) return;

            _game.SessionStarted += Bind;
            _game.Hearts.HeartsChanged += OnHeartsChanged;
            _game.Xp.XpGained += OnXpGained;
            RefreshHud();

            if (_game.CurrentSession != null) Bind(_game.CurrentSession);
        }

        private void OnDisable()
        {
            if (_game != null)
            {
                _game.SessionStarted -= Bind;
                _game.Hearts.HeartsChanged -= OnHeartsChanged;
                _game.Xp.XpGained -= OnXpGained;
            }

            Unbind();
            StopVideo();
        }

        private void OnDestroy()
        {
            if (_videoTexture != null)
            {
                _videoTexture.Release();
                Destroy(_videoTexture);
            }
        }

        // ------------------------------------------------------------------ session binding

        private void Bind(LessonSession session)
        {
            if (session == null || session == _session) return;
            Unbind();

            _session = session;
            _session.ExerciseChanged += OnExerciseChanged;
            _session.Finished += OnSessionFinished;
            _shown = null;
            _awaitingFeedback = false;
            _attempting = false;
            _progressTarget = 0f;
            if (progressFill != null) progressFill.fillAmount = 0f;

            if (overlay != null)
            {
                overlay.HideAll();
                overlay.SetHeartIcon(heartIcon);
            }

            if (practice != null && _game.ActivePack != null)
            {
                practice.Configure(_game.ActivePack);
                if (cameraPreview != null) cameraPreview.SetSource(practice.Provider as ICameraPreviewSource);
                if (landmarkOverlay != null)
                {
                    landmarkOverlay.DominantHand = _game.Progress.leftHanded ? HandSide.Left : HandSide.Right;
                    landmarkOverlay.Bind(practice.Provider);
                }
            }

            RefreshHud();
            if (_session.Current != null) Render(_session.Current);
        }

        private void Unbind()
        {
            if (_session == null) return;
            _session.ExerciseChanged -= OnExerciseChanged;
            _session.Finished -= OnSessionFinished;
            _session = null;
            _attempting = false;
            if (practice != null) practice.Cancel();
        }

        private void OnExerciseChanged(Exercise exercise)
        {
            // While a feedback sheet is up the learner hasn't pressed Continue yet: render after it.
            if (_awaitingFeedback) return;
            Render(exercise);
        }

        private void OnSessionFinished(LessonSession session)
        {
            if (_awaitingFeedback) return;
            FinishSession();
        }

        // ------------------------------------------------------------------ rendering

        private void Render(Exercise exercise)
        {
            if (exercise == null || exercise == _shown) return;
            _shown = exercise;
            _attempting = false;
            _progressTarget = _session != null ? _session.Progress01 : 0f;

            SignDefinition sign = exercise.Sign;
            bool isCamera = exercise.Type == ExerciseType.CameraSign;
            bool isChoice = exercise.Type == ExerciseType.Recognize;

            SetActive(cameraPanel, isCamera);
            SetActive(mediaPanel, !isCamera);
            SetActive(choicesPanel, isChoice);
            SetActive(skipButton, isCamera);
            SetActive(primaryButton, !isChoice);
            _speedIndex = 0; // every new video starts at 1x
            if (speedRow != null) speedRow.SetActive(!isCamera);

            if (promptLabel != null) promptLabel.text = PromptFor(exercise);
            if (glossLabel != null)
            {
                glossLabel.text = sign != null ? sign.Gloss : string.Empty;
                glossLabel.gameObject.SetActive(!isChoice);
            }

            bool selfCheck = isCamera && IsSelfCheck(exercise);
            SetStatus(isCamera ? (selfCheck ? UIStrings.LetterSelfCheck : UIStrings.WaitingForHand) : string.Empty);

            if (!isCamera) PlayClip(sign, true);
            else StopVideo();

            switch (exercise.Type)
            {
                case ExerciseType.Watch:
                    SetPrimary(UIStrings.GotIt, UIPalette.Green);
                    break;
                case ExerciseType.CameraSign:
                    // After "Anladım" the camera opens straight away and waits for the learner's hand.
                    SetPrimary(UIStrings.Start, UIPalette.Blue);
                    StartAttempt();
                    break;
                case ExerciseType.Recognize:
                    BindChoices(exercise);
                    break;
            }
        }

        private static bool IsSelfCheck(Exercise exercise)
        {
            return exercise.Sign == null || !exercise.Sign.HasRecognitionData;
        }

        private string PromptFor(Exercise exercise)
        {
            if (_session != null && _session.Kind == SessionKind.GateTest) return UIStrings.GatePrompt + " · " + UIStrings.SignPrompt;
            switch (exercise.Type)
            {
                case ExerciseType.Watch: return UIStrings.WatchPrompt;
                case ExerciseType.Recognize: return UIStrings.RecognizePrompt;
                default: return exercise.Step == LessonStep.Review ? UIStrings.ReviewPrompt + " · " + UIStrings.SignPrompt : UIStrings.SignPrompt;
            }
        }

        private void BindChoices(Exercise exercise)
        {
            List<SignDefinition> choices = exercise.Choices ?? new List<SignDefinition>();
            for (int i = 0; i < choiceButtons.Count; i++)
            {
                Button button = choiceButtons[i];
                bool visible = i < choices.Count;
                button.gameObject.SetActive(visible);
                button.interactable = visible;
                if (!visible) continue;
                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = choices[i].Gloss;
            }
        }

        private void Update()
        {
            if (progressFill != null)
            {
                progressFill.fillAmount = Mathf.MoveTowards(progressFill.fillAmount, _progressTarget, progressLerpSpeed * Time.unscaledDeltaTime * 0.25f);
            }

            if (!_attempting || practice == null) return;
            switch (practice.CurrentPhase)
            {
                case SignPracticeController.Phase.Calibrating: SetStatus(UIStrings.Calibrating); break;
                case SignPracticeController.Phase.WaitingForHand: SetStatus(UIStrings.WaitingForHand); break;
                case SignPracticeController.Phase.Recording: SetStatus(UIStrings.Recording); break;
                case SignPracticeController.Phase.Scoring: SetStatus(UIStrings.Scoring); break;
            }
        }

        // ------------------------------------------------------------------ actions

        private void OnPrimary()
        {
            Exercise exercise = _session != null ? _session.Current : null;
            if (exercise == null) return;

            if (exercise.Type == ExerciseType.Watch)
            {
                Play(SfxId.NodeTap);
                _session.CompleteWatch();
            }
            else if (exercise.Type == ExerciseType.CameraSign && !_attempting)
            {
                StartAttempt();
            }
        }

        private void StartAttempt()
        {
            if (practice == null || _session == null || _session.Current == null) return;
            _attempting = true;
            SetActive(primaryButton, false);
            // "Can't use the camera now" stays available while waiting (no camera, permission denied, ...).
            SetActive(skipButton, true);

            if (!practice.IsCalibrated)
            {
                practice.Calibrate(BeginAttempt);
            }
            else
            {
                BeginAttempt();
            }
        }

        private void BeginAttempt()
        {
            if (_session == null || _session.Current == null) return;
            Play(SfxId.Countdown);

            // Signs without reference recordings yet can only be checked for a clearly visible hand.
            if (IsSelfCheck(_session.Current)) practice.BeginPresenceCheck(_session.Current.Sign, OnAttemptScored);
            else practice.BeginAttempt(_session.Current.Sign, OnAttemptScored);
        }

        private void OnAttemptScored(MatchResult result)
        {
            _attempting = false;
            if (_session == null) return;

            _awaitingFeedback = true;
            AttemptOutcome outcome = _session.SubmitCameraAttempt(result);
            SetStatus(FeedbackMessages.Get(result.Feedback, UiLanguage));
            Play(outcome.Passed ? SfxId.Correct : SfxId.Incorrect);
            if (outcome.HeartLost) Play(SfxId.HeartLost);

            if (overlay != null) overlay.ShowCameraAttempt(outcome, result, UiLanguage, ContinueAfterFeedback, RetryAttempt);
            else ContinueAfterFeedback();
        }

        private void RetryAttempt()
        {
            _awaitingFeedback = false;
            if (_session == null) return;
            if (_session.IsFinished)
            {
                FinishSession();
                return;
            }

            StartAttempt();
        }

        private void OnChoice(int index)
        {
            Exercise exercise = _session != null ? _session.Current : null;
            if (exercise == null || exercise.Type != ExerciseType.Recognize || exercise.Choices == null || index >= exercise.Choices.Count) return;

            foreach (Button button in choiceButtons) button.interactable = false;

            _awaitingFeedback = true;
            AttemptOutcome outcome = _session.SubmitChoice(exercise.Choices[index]);
            Play(outcome.Passed ? SfxId.Correct : SfxId.Incorrect);
            if (outcome.HeartLost) Play(SfxId.HeartLost);
            RefreshHud();

            if (overlay != null) overlay.ShowChoiceResult(outcome.Passed, exercise.Sign.Gloss, outcome.HeartLost, ContinueAfterFeedback);
            else ContinueAfterFeedback();
        }

        private void ContinueAfterFeedback()
        {
            _awaitingFeedback = false;
            if (_session == null) return;
            if (_session.IsFinished)
            {
                FinishSession();
                return;
            }

            // Same exercise after a non-advancing result: re-enable it; otherwise show the next one.
            if (_session.Current == _shown) ResetCurrentExercise();
            else Render(_session.Current);
        }

        private void ResetCurrentExercise()
        {
            Exercise exercise = _session.Current;
            if (exercise == null) return;
            if (exercise.Type == ExerciseType.CameraSign)
            {
                SetActive(primaryButton, true);
                SetActive(skipButton, true);
                SetStatus(IsSelfCheck(exercise) ? UIStrings.LetterSelfCheck : UIStrings.PressToStart);
            }
            else if (exercise.Type == ExerciseType.Recognize)
            {
                foreach (Button button in choiceButtons) button.interactable = true;
            }
        }

        private void OnSkip()
        {
            if (_session == null || _awaitingFeedback) return;
            if (_attempting)
            {
                _attempting = false;
                if (practice != null) practice.Cancel();
            }

            _session.SkipCurrent();
        }

        private void OnClose()
        {
            if (practice != null) practice.StopCamera();
            StopVideo();
            Unbind();
            if (overlay != null) overlay.HideAll();
            if (_game != null) _game.AbandonSession();
        }

        private void FinishSession()
        {
            LessonSession session = _session;
            Unbind();
            StopVideo();
            if (practice != null) practice.StopCamera();
            if (_game == null || session == null) return;

            _progressTarget = 1f;
            LessonReward reward = _game.CompleteSession();

            if (overlay == null)
            {
                _game.ReturnToMap();
                return;
            }

            if (session.FailedOutOfHearts)
            {
                overlay.ShowOutOfHearts(_game.ReturnToMap);
                return;
            }

            if (session.Kind == SessionKind.GateTest && reward.Passed) Play(SfxId.GatePassed);
            else if (reward.PerfectBonus) Play(SfxId.PerfectLesson);
            else Play(SfxId.LessonComplete);
            if (reward.StreakIncreased) Play(SfxId.StreakIncreased);

            overlay.ShowLessonComplete(session, reward, _game.ReturnToMap);
        }

        // ------------------------------------------------------------------ media

        /// <summary>Shows the sign's video at the chosen speed; <paramref name="restart"/> plays it from the start.</summary>
        private void PlayClip(SignDefinition sign, bool restart)
        {
            float speed = PlaybackSpeeds[Mathf.Clamp(_speedIndex, 0, PlaybackSpeeds.Length - 1)];
            // The original recording is 1x; 0.75x and 0.5x are the same video played slower.
            VideoClip clip = sign != null ? sign.ReferenceClip : null;
            bool hasClip = clip != null && videoPlayer != null && videoImage != null;

            if (thumbnailImage != null)
            {
                thumbnailImage.sprite = sign != null ? sign.Thumbnail : null;
                thumbnailImage.gameObject.SetActive(!hasClip && thumbnailImage.sprite != null);
            }

            if (videoImage != null) videoImage.gameObject.SetActive(hasClip);
            if (speedRow != null) speedRow.SetActive(hasClip);
            RefreshSpeedButtons();
            if (!hasClip)
            {
                StopVideo();
                return;
            }

            EnsureVideoTexture((int)clip.width, (int)clip.height);
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = _videoTexture;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoPlayer.isLooping = true;
            if (restart || videoPlayer.clip != clip)
            {
                videoPlayer.Stop();
                videoPlayer.clip = clip;
            }

            videoPlayer.playbackSpeed = speed;
            videoImage.texture = _videoTexture;
            if (!videoPlayer.isPlaying) videoPlayer.Play();
        }

        private void RefreshSpeedButtons()
        {
            for (int i = 0; i < speedButtons.Count; i++)
            {
                Button button = speedButtons[i];
                if (button == null) continue;
                bool selected = i == _speedIndex;
                var image = button.targetGraphic as Image;
                if (image != null) image.color = selected ? UIPalette.Primary : Color.white;
                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    label.text = i < PlaybackSpeeds.Length ? UIStrings.Speed(PlaybackSpeeds[i]) : string.Empty;
                    label.color = selected ? Color.white : UIPalette.Primary;
                }
            }
        }

        private void EnsureVideoTexture(int width, int height)
        {
            width = Mathf.Max(16, width);
            height = Mathf.Max(16, height);
            if (_videoTexture != null && _videoTexture.width == width && _videoTexture.height == height) return;

            if (_videoTexture != null)
            {
                _videoTexture.Release();
                Destroy(_videoTexture);
            }

            _videoTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            _videoTexture.Create();

            AspectRatioFitter fitter = videoImage.GetComponent<AspectRatioFitter>();
            if (fitter != null) fitter.aspectRatio = width / (float)height;
        }

        private void StopVideo()
        {
            if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
        }

        /// <summary>The learner picked a speed: the video keeps playing from where it is, at the new speed.</summary>
        private void SetSpeed(int index)
        {
            if (index < 0 || index >= PlaybackSpeeds.Length || index == _speedIndex) return;
            _speedIndex = index;
            if (_session != null && _session.Current != null && _session.Current.Type != ExerciseType.CameraSign)
                PlayClip(_session.Current.Sign, false);
        }

        // ------------------------------------------------------------------ HUD

        private void RefreshHud()
        {
            if (_game == null) return;
            if (heartsLabel != null) heartsLabel.text = _game.Hearts.Hearts.ToString();

            if (xpLabel != null) xpLabel.text = _game.Xp.Total.ToString();
        }

        private void OnHeartsChanged(int hearts) => RefreshHud();
        private void OnXpGained(XpState state, int amount) => RefreshHud();

        private void SetPrimary(string text, Color color)
        {
            if (primaryLabel != null) primaryLabel.text = text;
            if (primaryButton != null)
            {
                Image image = primaryButton.targetGraphic as Image;
                if (image != null) image.color = color;
                primaryButton.interactable = true;
            }
        }

        private void SetStatus(string text)
        {
            if (statusLabel == null) return;
            statusLabel.text = text;
            statusLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        private static void SetActive(Component component, bool active)
        {
            if (component != null) component.gameObject.SetActive(active);
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null) go.SetActive(active);
        }

        private static void Play(SfxId id)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Play(id);
        }
    }
}
