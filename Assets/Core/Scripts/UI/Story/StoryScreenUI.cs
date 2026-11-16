using System.Collections;
using Cevir.Core.Audio;
using Cevir.Core.Game;
using Cevir.Core.LanguagePacks;
using Cevir.Core.Lesson;
using Cevir.Core.Localization;
using Cevir.Core.Recognition;
using Cevir.Core.Stories;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cevir.Core.UI
{
    /// <summary>
    /// First-person story player. The learner "walks" into the scene (bag straps and hands in the foreground), the
    /// NPC approaches, speaks (voice + typewriter bubble) and signs (clip panel). "Respond in Sign Language" opens
    /// the front camera inside the HUD, where the learner sees their own hands with the tracked skeleton and
    /// fingerspells their name letter by letter. Every attempt is scored by <see cref="SignPracticeController"/>
    /// against the current letter; tips appear live, correct letters pop green, and the story continues with the
    /// NPC's reply. Story logic lives in <see cref="StorySession"/>; rewards in <see cref="GameManager.CompleteStory"/>.
    /// </summary>
    public sealed class StoryScreenUI : MonoBehaviour
    {
        [Header("Systems")]
        [SerializeField] private SignPracticeController practice;
        [SerializeField] private CameraPreviewUI cameraPreview;
        [SerializeField] private LandmarkOverlayUI landmarkOverlay;
        [SerializeField] private AudioSource voice;
        [SerializeField] private FeedbackOverlayUI overlay;

        [Header("Scene")]
        [SerializeField] private RectTransform sceneRoot;
        [SerializeField] private Image background;
        [SerializeField] private NpcActorUI npc;
        [SerializeField] private RectTransform foreground;
        [SerializeField] private Image foregroundImage;
        [SerializeField] private float approachSeconds = 2.4f;

        [Header("HUD")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Image progressFill;
        [SerializeField] private TextMeshProUGUI titleLabel;

        [Header("Dialogue")]
        [SerializeField] private SpeechBubbleUI bubble;
        [SerializeField] private SignClipPanelUI signPanel;
        [SerializeField] private Button continueArea;
        [SerializeField] private GameObject continueHint;
        [SerializeField] private Button respondButton;

        [Header("Camera HUD")]
        [SerializeField] private CanvasGroup cameraHud;
        [SerializeField] private RectTransform cameraHudPanel;
        [SerializeField] private TextMeshProUGUI promptLabel;
        [SerializeField] private FingerspellTilesUI tiles;
        [SerializeField] private TextMeshProUGUI letterLabel;
        [SerializeField] private Image letterThumbnail;
        [SerializeField] private Image tipBackground;
        [SerializeField] private TextMeshProUGUI tipLabel;
        [SerializeField] private TextMeshProUGUI selfCheckNote;
        [SerializeField] private Button skipLetterButton;
        [SerializeField] private Button closeCameraButton;
        [SerializeField] private Button cantUseCameraButton;

        [Header("Name card")]
        [SerializeField] private GameObject nameCard;
        [SerializeField] private TMP_InputField firstNameField;
        [SerializeField] private TMP_InputField lastNameField;
        [SerializeField] private Button nameContinueButton;

        [Header("Celebration")]
        [SerializeField] private CelebrationUI celebration;

        private GameManager _game;
        private StorySession _session;
        private int _shownBeat = -1;
        private bool _busy;
        private bool _hudOpen;
        private bool _attempting;
        private bool _finished;
        private float _waitingSince;
        private Coroutine _lineRoutine;

        private void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(OnClose);
            if (continueArea != null) continueArea.onClick.AddListener(OnContinueTapped);
            if (respondButton != null) respondButton.onClick.AddListener(OnRespond);
            if (skipLetterButton != null) skipLetterButton.onClick.AddListener(OnSkipLetter);
            if (closeCameraButton != null) closeCameraButton.onClick.AddListener(OnCloseCamera);
            if (cantUseCameraButton != null) cantUseCameraButton.onClick.AddListener(OnCantUseCamera);
            if (nameContinueButton != null) nameContinueButton.onClick.AddListener(OnNameEntered);
            if (bubble != null) bubble.ReplayRequested += ReplayVoice;
        }

        private void OnEnable()
        {
            _game = GameManager.Instance;
            if (_game == null) return;
            _game.StoryStarted += Bind;
            if (_game.CurrentStory != null) Bind(_game.CurrentStory);
        }

        private void OnDisable()
        {
            if (_game != null) _game.StoryStarted -= Bind;
            Unbind();
        }

        // ------------------------------------------------------------------ binding

        private void Bind(StorySession session)
        {
            if (session == null || session == _session) return;
            Unbind();
            _session = session;
            _session.PhaseChanged += OnPhaseChanged;
            _shownBeat = -1;
            _busy = false;
            _finished = false;
            _hudOpen = false;

            StoryData story = session.Story;
            if (titleLabel != null) titleLabel.text = story.Title;
            if (background != null && story.Background != null) background.sprite = story.Background;
            if (foregroundImage != null && story.Foreground != null) foregroundImage.sprite = story.Foreground;
            if (npc != null) npc.Bind(story.Npc);
            if (overlay != null) overlay.HideAll();
            if (bubble != null) bubble.Hide();
            if (signPanel != null) signPanel.gameObject.SetActive(false);
            if (celebration != null) celebration.gameObject.SetActive(false);
            if (nameCard != null) nameCard.SetActive(false);
            SetActive(respondButton, false);
            SetContinue(false);
            SetHud(false, true);
            if (sceneRoot != null) sceneRoot.localScale = Vector3.one;
            if (foreground != null) foreground.anchoredPosition = Vector2.zero;

            Sync();
        }

        private void Unbind()
        {
            StopAllCoroutines();
            _lineRoutine = null;
            if (practice != null) practice.StopCamera();
            if (voice != null) voice.Stop();
            if (signPanel != null) signPanel.Stop();
            if (_session != null) _session.PhaseChanged -= OnPhaseChanged;
            _session = null;
            _attempting = false;
        }

        private void OnPhaseChanged(StoryPhase phase) => Sync();

        /// <summary>Brings the screen in line with the session, unless an animation is still running.</summary>
        private void Sync()
        {
            if (_session == null || _busy) return;
            if (progressFill != null) progressFill.fillAmount = _session.Progress01;

            switch (_session.Phase)
            {
                case StoryPhase.NpcSpeaking:
                    SetActive(respondButton, false);
                    if (_shownBeat != _session.BeatIndex)
                    {
                        _shownBeat = _session.BeatIndex;
                        _lineRoutine = StartCoroutine(PlayNpcLine(_session.CurrentBeat));
                    }

                    break;

                case StoryPhase.AwaitingResponse:
                    if (_hudOpen) SetHud(false, false);
                    SetContinue(false);
                    SetActive(respondButton, true);
                    break;

                case StoryPhase.Responding:
                    SetActive(respondButton, false);
                    if (!_hudOpen) OpenCameraHud();
                    break;

                case StoryPhase.Completed:
                    if (!_finished) Finish();
                    break;
            }
        }

        // ------------------------------------------------------------------ NPC lines

        private IEnumerator PlayNpcLine(StoryBeat beat)
        {
            _busy = true;
            SetContinue(false);

            if (beat.approach && npc != null)
            {
                if (bubble != null) bubble.Hide();
                if (signPanel != null) signPanel.gameObject.SetActive(false);
                npc.SetFar();
                yield return StartCoroutine(WalkIn());
            }

            if (npc != null)
            {
                npc.SetPose(beat.pose);
                npc.SetTalking(true);
            }

            string speaker = _session.Story.Npc != null ? _session.Story.Npc.displayName.Get() : string.Empty;
            AudioClip clip = beat.voice.Get();
            if (bubble != null) bubble.Show(speaker, _session.FormatText(beat), clip != null);
            PlayVoice(clip);
            if (signPanel != null) signPanel.Play(beat.npcSigns);

            float started = Time.time;
            while ((bubble != null && bubble.IsTyping) || (voice != null && voice.isPlaying) || Time.time - started < 1.2f)
            {
                yield return null;
            }

            if (npc != null) npc.SetTalking(false);
            _busy = false;
            _lineRoutine = null;

            StoryBeat next = NextBeat();
            if (next != null && next.type != StoryBeatType.NpcLine)
            {
                _session.NpcLineFinished(); // straight to "Respond in Sign Language"
            }
            else
            {
                SetContinue(true);
            }
        }

        private IEnumerator WalkIn()
        {
            // The learner walks forward: the street zooms in a little and the bag straps sway while the NPC approaches.
            Coroutine npcWalk = StartCoroutine(npc.Approach(approachSeconds));
            for (float t = 0f; t < approachSeconds; t += Time.deltaTime)
            {
                float k = t / approachSeconds;
                if (sceneRoot != null) sceneRoot.localScale = Vector3.one * Mathf.Lerp(1f, 1.06f, Mathf.SmoothStep(0f, 1f, k));
                if (foreground != null)
                {
                    float step = Mathf.Sin(t * Mathf.PI * 3.2f);
                    foreground.anchoredPosition = new Vector2(step * 10f, -Mathf.Abs(step) * 22f);
                }

                yield return null;
            }

            yield return npcWalk;
            if (foreground != null) foreground.anchoredPosition = Vector2.zero;
        }

        private StoryBeat NextBeat()
        {
            int next = _session.BeatIndex + 1;
            return next < _session.Story.Beats.Count ? _session.Story.Beats[next] : null;
        }

        private void OnContinueTapped()
        {
            if (_session == null) return;
            if (bubble != null && bubble.IsTyping)
            {
                bubble.Complete();
                return;
            }

            if (_busy || _session.Phase != StoryPhase.NpcSpeaking) return;
            SetContinue(false);
            _session.NpcLineFinished();
        }

        private void PlayVoice(AudioClip clip)
        {
            if (voice == null) return;
            voice.Stop();
            if (clip == null) return;
            float volume = AudioManager.Instance != null ? AudioManager.Instance.SfxVolume : 1f;
            voice.PlayOneShot(clip, volume);
        }

        private void ReplayVoice()
        {
            StoryBeat beat = _session != null ? _session.Story.Beats[Mathf.Clamp(_shownBeat, 0, _session.Story.Beats.Count - 1)] : null;
            if (beat != null) PlayVoice(beat.voice.Get());
        }

        // ------------------------------------------------------------------ responding

        private void OnRespond()
        {
            if (_session == null || _session.Phase != StoryPhase.AwaitingResponse) return;
            Play(SfxId.NodeTap);
            if (_session.NeedsName)
            {
                ShowNameCard();
                return;
            }

            _session.BeginResponse();
        }

        private void ShowNameCard()
        {
            if (nameCard == null) return;
            if (firstNameField != null) firstNameField.text = _session.FirstName;
            if (lastNameField != null)
            {
                lastNameField.text = _session.LastName;
                lastNameField.gameObject.SetActive(_session.AsksSurname);
            }

            nameCard.SetActive(true);
        }

        private void OnNameEntered()
        {
            string first = firstNameField != null ? firstNameField.text.Trim() : string.Empty;
            string last = lastNameField != null && _session.AsksSurname ? lastNameField.text.Trim() : _session.LastName;
            if (first.Length == 0) return;
            _session.SetName(first, last);
            if (_session.NeedsName) return; // surname still missing for a full-name beat
            if (nameCard != null) nameCard.SetActive(false);
            _game.SetLearnerName(first, last);
            _session.BeginResponse();
        }

        private void OpenCameraHud()
        {
            SetHud(true, false);
            if (promptLabel != null) promptLabel.text = _session.FormatText(_session.CurrentBeat);
            if (tiles != null) tiles.Build(_session.Spelling);
            SetTip(null, UIPalette.Primary);
            SetActive(skipLetterButton, false);

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

            StartCoroutine(SlideHands(true));
            if (practice == null) return;
            if (!practice.IsCalibrated) practice.Calibrate(NextAttempt);
            else NextAttempt();
        }

        private void NextAttempt()
        {
            if (_session == null || _session.Phase != StoryPhase.Responding || practice == null) return;
            StoryBeat beat = _session.CurrentBeat;
            SignDefinition target;
            if (beat.type == StoryBeatType.LearnerFingerspellName)
            {
                SpelledLetter letter = _session.Spelling != null ? _session.Spelling.Current : null;
                if (letter == null) return;
                target = letter.Sign;
                if (letterLabel != null) letterLabel.text = letter.Letter;
                if (letterThumbnail != null)
                {
                    letterThumbnail.sprite = target != null ? target.Thumbnail : null;
                    letterThumbnail.gameObject.SetActive(letterThumbnail.sprite != null);
                }

                if (tiles != null) tiles.Refresh();
            }
            else
            {
                target = beat.expectedSign;
                if (letterLabel != null) letterLabel.text = target != null ? Loc.ToUpperAz(target.Gloss) : string.Empty;
            }

            bool checkable = target != null && (target.HasRecognitionData || Recognition.Letters.AzslLetterRecognizer.LabelIndex(target.Gloss) >= 0 && Recognition.Letters.AzslLetterRecognizer.Available && target.Kind == LanguagePacks.SignKind.Fingerspelling);
            if (selfCheckNote != null)
            {
                selfCheckNote.gameObject.SetActive(!checkable);
                selfCheckNote.text = UIStrings.LetterSelfCheck;
            }

            _attempting = true;
            _waitingSince = Time.unscaledTime;
            if (checkable) practice.BeginAttempt(target, OnScored);
            else practice.BeginPresenceCheck(target, OnScored);
        }

        private void Update()
        {
            if (!_attempting || practice == null) return;
            switch (practice.CurrentPhase)
            {
                case SignPracticeController.Phase.Calibrating:
                    SetTip(UIStrings.Calibrating, UIPalette.Primary);
                    break;
                case SignPracticeController.Phase.WaitingForHand:
                    // Give the learner a moment before nagging.
                    if (Time.unscaledTime - _waitingSince > 1.5f) SetTip(UIStrings.ShowHandsClearly, UIPalette.Amber);
                    break;
                case SignPracticeController.Phase.Recording:
                    SetTip(UIStrings.Recording, UIPalette.Primary);
                    break;
                case SignPracticeController.Phase.Scoring:
                    SetTip(UIStrings.Scoring, UIPalette.Primary);
                    break;
            }
        }

        private void OnScored(MatchResult result)
        {
            _attempting = false;
            if (_session == null || _session.Phase != StoryPhase.Responding) return;

            SpelledLetter letter = _session.Spelling != null ? _session.Spelling.Current : null;
            if (!result.Unverified && letter != null && letter.Sign != null && result.Severity != FeedbackSeverity.Neutral)
            {
                _game.RecordSignPractice(letter.Sign.SignId, result.IsCorrect);
            }

            SpellOutcome outcome = _session.SubmitAttempt(result);
            if (tiles != null) tiles.Refresh();

            if (result.IsCorrect)
            {
                Play(SfxId.Correct);
                SetTip(FeedbackMessages.Get(SignFeedback.Correct, Loc.Language), UIPalette.Green);
                SetActive(skipLetterButton, false);
                if (outcome.TaskComplete) StartCoroutine(FinishResponse());
                else StartCoroutine(After(0.45f, NextAttempt));
                return;
            }

            if (result.Severity == FeedbackSeverity.Neutral)
            {
                SetTip(result.Feedback == SignFeedback.ShowHandToCamera ? UIStrings.ShowHandsClearly : FeedbackMessages.Get(result.Feedback, Loc.Language), UIPalette.Amber);
            }
            else
            {
                Play(SfxId.Incorrect);
                SetTip(FeedbackMessages.Get(result.Feedback, Loc.Language), UIPalette.Red);
            }

            SetActive(skipLetterButton, outcome.CanSkip);
            StartCoroutine(After(1.0f, NextAttempt));
        }

        private IEnumerator FinishResponse()
        {
            _busy = true;
            if (practice != null) practice.StopCamera();
            if (celebration != null) celebration.Play(FeedbackMessages.Get(SignFeedback.Correct, Loc.Language));
            yield return new WaitForSecondsRealtime(1.3f);
            SetHud(false, false);
            yield return StartCoroutine(SlideHands(false));
            _busy = false;
            Sync();
        }

        private void OnSkipLetter()
        {
            if (_session == null) return;
            if (practice != null) practice.Cancel();
            _attempting = false;
            StopAllCoroutinesExceptLine();
            SetActive(skipLetterButton, false);
            if (_session.SkipLetter()) StartCoroutine(FinishResponse());
            else NextAttempt();
        }

        private void OnCloseCamera()
        {
            if (_session == null) return;
            if (practice != null) practice.StopCamera();
            _attempting = false;
            StopAllCoroutinesExceptLine();
            SetHud(false, false);
            StartCoroutine(SlideHands(false));
            _session.CancelResponse();
        }

        private void OnCantUseCamera()
        {
            if (_session == null) return;
            if (practice != null) practice.StopCamera();
            _attempting = false;
            StopAllCoroutinesExceptLine();
            SetHud(false, false);
            StartCoroutine(SlideHands(false));
            _session.SkipResponse();
        }

        private void StopAllCoroutinesExceptLine()
        {
            // Retry timers only run while the HUD is open, when no NPC line is playing.
            if (_lineRoutine == null) StopAllCoroutines();
        }

        // ------------------------------------------------------------------ finish

        private void Finish()
        {
            _finished = true;
            if (practice != null) practice.StopCamera();
            StoryData story = _session.Story;
            StoryReward reward = _game.CompleteStory();

            Play(SfxId.LessonComplete);
            if (reward.StreakIncreased) Play(SfxId.StreakIncreased);
            if (celebration != null) celebration.Play(UIStrings.StoryComplete);

            var stats = new System.Text.StringBuilder();
            stats.AppendLine(string.Format(UIStrings.XpGained, reward.Xp));
            if (reward.PerfectBonus) stats.AppendLine(string.Format(UIStrings.StoryPerfect, _game.Config.storyPerfectBonusXp));
            if (reward.StreakIncreased) stats.AppendLine(string.Format(UIStrings.StreakUp, _game.Streak.State.current));

            if (overlay != null)
            {
                overlay.ShowModal(UIStrings.StoryComplete, string.Format(UIStrings.StoryCompleteBody, story.Title), stats.ToString().TrimEnd(),
                    UIStrings.BackToStories, BackToStories, null, null);
            }
            else
            {
                BackToStories();
            }
        }

        private void BackToStories()
        {
            if (CevirUIRoot.Instance != null) CevirUIRoot.Instance.ShowTab(MainTab.Stories);
            if (_game != null) _game.ReturnToMap();
        }

        private void OnClose()
        {
            if (CevirUIRoot.Instance != null) CevirUIRoot.Instance.ShowTab(MainTab.Stories);
            Unbind();
            if (_game != null) _game.AbandonStory();
        }

        // ------------------------------------------------------------------ helpers

        private IEnumerator After(float seconds, System.Action action)
        {
            yield return new WaitForSecondsRealtime(seconds);
            action();
        }

        private IEnumerator SlideHands(bool down)
        {
            if (foreground == null) yield break;
            Vector2 from = foreground.anchoredPosition;
            Vector2 to = down ? new Vector2(0f, -foreground.rect.height) : Vector2.zero;
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                foreground.anchoredPosition = Vector2.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / 0.3f));
                yield return null;
            }

            foreground.anchoredPosition = to;
        }

        private void SetHud(bool open, bool immediate)
        {
            _hudOpen = open;
            if (cameraHud == null) return;
            cameraHud.gameObject.SetActive(open);
            cameraHud.alpha = open ? 1f : 0f;
            cameraHud.blocksRaycasts = open;
            if (open && cameraHudPanel != null && !immediate) StartCoroutine(Rise(cameraHudPanel));
        }

        private static IEnumerator Rise(RectTransform panel)
        {
            Vector2 home = Vector2.zero;
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                float k = 1f - Mathf.Pow(1f - t / 0.25f, 3f);
                panel.anchoredPosition = Vector2.Lerp(new Vector2(0f, -500f), home, k);
                yield return null;
            }

            panel.anchoredPosition = home;
        }

        private void SetContinue(bool on)
        {
            if (continueHint != null) continueHint.SetActive(on);
        }

        private void SetTip(string text, Color color)
        {
            if (tipLabel == null) return;
            bool visible = !string.IsNullOrEmpty(text);
            if (tipBackground != null)
            {
                tipBackground.gameObject.SetActive(visible);
                tipBackground.color = color;
            }

            tipLabel.text = text ?? string.Empty;
        }

        private static void SetActive(Component component, bool active)
        {
            if (component != null) component.gameObject.SetActive(active);
        }

        private static void Play(SfxId id)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Play(id);
        }
    }
}
