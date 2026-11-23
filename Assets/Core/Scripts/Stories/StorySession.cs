using System;
using System.Collections.Generic;
using Cevir.Core.LanguagePacks;
using Cevir.Core.Localization;
using Cevir.Core.Recognition;

namespace Cevir.Core.Stories
{
    public enum StoryPhase
    {
        NotStarted,

        /// <summary>An NPC line is playing; the UI calls <see cref="StorySession.NpcLineFinished"/> when it is done.</summary>
        NpcSpeaking,

        /// <summary>A learner beat is waiting for "Respond in Sign Language".</summary>
        AwaitingResponse,

        /// <summary>The camera HUD is open and attempts are being checked.</summary>
        Responding,

        Completed
    }

    /// <summary>
    /// Runtime state of one story: walks the beats, owns the fingerspelling task for name beats and keeps the
    /// score. Pure C#, like <c>LessonSession</c>; the scene, camera and animation live in <c>StoryScreenUI</c>.
    /// Stories never cost hearts.
    /// </summary>
    public sealed class StorySession
    {
        private readonly ILanguagePack _pack;
        private SignDefinition _nameSignTarget;
        private int _index = -1;

        public event Action<StoryBeat> BeatChanged;
        public event Action<StoryPhase> PhaseChanged;
        public event Action<StorySession> Finished;

        public StorySession(StoryData story, ILanguagePack pack, string firstName, string lastName, string nameSign = null)
        {
            Story = story;
            _pack = pack;
            FirstName = firstName ?? string.Empty;
            LastName = lastName ?? string.Empty;
            NameSign = (nameSign ?? string.Empty).Trim();
        }

        public StoryData Story { get; private set; }
        public string FirstName { get; private set; }
        public string LastName { get; private set; }

        /// <summary>The learner's nickname / best-known feature, a word from <see cref="NameSignOptions"/> (e.g. "Eynək").</summary>
        public string NameSign { get; private set; }
        public StoryPhase Phase { get; private set; }
        public int BeatIndex => _index;
        public StoryBeat CurrentBeat => _index >= 0 && _index < Story.Beats.Count ? Story.Beats[_index] : null;
        public FingerspellingTask Spelling { get; private set; }

        /// <summary>The learner chose "I can't use the camera now" for at least one response.</summary>
        public bool UsedCameraSkip { get; private set; }

        public int Attempts { get; private set; }
        public int CorrectAttempts { get; private set; }
        public int SignBeatFailures { get; private set; }

        /// <summary>Every response was checked by the matcher and passed: earns the perfect bonus.</summary>
        public bool AllVerified { get; private set; } = true;

        public bool IsFinished => Phase == StoryPhase.Completed;
        public float Accuracy => Attempts == 0 ? 1f : CorrectAttempts / (float)Attempts;
        public float Progress01 => Story.Beats.Count == 0 ? 1f : Math.Max(0, _index) / (float)Story.Beats.Count;

        public bool NeedsName
        {
            get
            {
                foreach (StoryBeat beat in Story.Beats)
                {
                    if (beat.type != StoryBeatType.LearnerFingerspellName) continue;
                    if (string.IsNullOrEmpty(FirstName)) return true;
                    if (beat.nameScope == NameScope.FullName && string.IsNullOrEmpty(LastName)) return true;
                }

                return false;
            }
        }

        /// <summary>True when a name-sign beat is coming and the learner has not picked a word from the list yet.</summary>
        public bool NeedsNameSign
        {
            get
            {
                if (NameSignOptions.Find(NameSign) != null) return false;
                foreach (StoryBeat beat in Story.Beats)
                {
                    if (beat.type == StoryBeatType.LearnerNameSign) return true;
                }

                return false;
            }
        }

        public void SetNameSign(string nameSign)
        {
            NameSign = NameSignOptions.Find(nameSign) ?? (nameSign ?? string.Empty).Trim();
        }

        /// <summary>
        /// The sign to check for the learner's nickname / best-known feature: the pack's own sign with that gloss when
        /// there is one, otherwise a gloss-only sign the word model can judge. Null when no name sign is set.
        /// </summary>
        public SignDefinition NameSignTarget
        {
            get
            {
                if (string.IsNullOrEmpty(NameSign)) return null;
                if (_nameSignTarget != null && _nameSignTarget.Gloss == NameSign) return _nameSignTarget;
                _nameSignTarget = null;
                if (_pack != null && _pack.Dictionary != null)
                {
                    string key = Loc.ToLowerAz(NameSign);
                    foreach (SignDefinition sign in _pack.Dictionary.Signs)
                    {
                        if (sign == null || sign.Kind == SignKind.Fingerspelling || sign.Kind == SignKind.Number) continue;
                        if (Loc.ToLowerAz(sign.Gloss ?? string.Empty) == key)
                        {
                            _nameSignTarget = sign;
                            break;
                        }
                    }
                }

                if (_nameSignTarget == null) _nameSignTarget = SignDefinition.CreateForGloss("namesign." + Loc.ToLowerAz(NameSign), NameSign, SignKind.Word);
                return _nameSignTarget;
            }
        }

        /// <summary>True when a beat in this story asks for the surname too (the name card hides the field otherwise).</summary>
        public bool AsksSurname
        {
            get
            {
                foreach (StoryBeat beat in Story.Beats)
                {
                    if (beat.type == StoryBeatType.LearnerFingerspellName && beat.nameScope == NameScope.FullName) return true;
                }

                return false;
            }
        }

        public void SetName(string firstName, string lastName)
        {
            FirstName = (firstName ?? string.Empty).Trim();
            LastName = (lastName ?? string.Empty).Trim();
        }

        public void Begin()
        {
            if (Phase != StoryPhase.NotStarted) return;
            Advance();
        }

        /// <summary>Text of a beat in the current interface language, with {name} filled in.</summary>
        public string FormatText(StoryBeat beat)
        {
            string text = beat != null ? beat.text.Get() : string.Empty;
            text = text.Replace("{namesign}", NameSign);
            return text.Replace("{name}", string.IsNullOrEmpty(FirstName) ? Loc.Pick("friend", "dost") : FirstName);
        }

        public void NpcLineFinished()
        {
            if (Phase != StoryPhase.NpcSpeaking) return;
            Advance();
        }

        /// <summary>Opens the response for the current learner beat (the camera HUD).</summary>
        public bool BeginResponse()
        {
            StoryBeat beat = CurrentBeat;
            if (Phase != StoryPhase.AwaitingResponse || beat == null) return false;

            if (beat.type == StoryBeatType.LearnerNameSign && NeedsNameSign) return false;

            if (beat.type == StoryBeatType.LearnerFingerspellName)
            {
                if (NeedsName) return false;
                var words = new List<string> { FirstName };
                if (beat.nameScope == NameScope.FullName && !string.IsNullOrEmpty(LastName)) words.Add(LastName);
                Spelling = new FingerspellingTask(words, AlphabetOf(_pack), FindLetterSign);
                if (Spelling.IsComplete)
                {
                    // Nothing spellable in the name (e.g. only foreign characters): don't block the story.
                    AllVerified = false;
                    Advance();
                    return true;
                }
            }

            SetPhase(StoryPhase.Responding);
            return true;
        }

        /// <summary>Applies one scored camera attempt to the current learner beat.</summary>
        public SpellOutcome SubmitAttempt(MatchResult result)
        {
            var outcome = new SpellOutcome { Feedback = result.Feedback, Severity = result.Severity };
            StoryBeat beat = CurrentBeat;
            if (Phase != StoryPhase.Responding || beat == null) return outcome;

            // Tracking problems ("show your hand") are not the learner's mistake and are not counted.
            if (result.Severity != FeedbackSeverity.Neutral || result.IsCorrect)
            {
                Attempts++;
                if (result.IsCorrect) CorrectAttempts++;
            }

            if (result.IsCorrect && result.Unverified) AllVerified = false;

            if (beat.type == StoryBeatType.LearnerFingerspellName && Spelling != null)
            {
                outcome = Spelling.Submit(result);
                if (outcome.TaskComplete) CompleteResponse();
                return outcome;
            }

            // LearnerSign
            if (result.IsCorrect)
            {
                outcome.LetterDone = true;
                outcome.TaskComplete = true;
                CompleteResponse();
            }
            else if (result.CountsAsFailedAttempt)
            {
                SignBeatFailures++;
                outcome.CanSkip = SignBeatFailures >= 3;
            }

            return outcome;
        }

        /// <summary>Skips the current letter; returns true when that finished the response.</summary>
        public bool SkipLetter()
        {
            if (Phase != StoryPhase.Responding || Spelling == null) return false;
            Spelling.SkipCurrent();
            AllVerified = false;
            if (!Spelling.IsComplete) return false;
            CompleteResponse();
            return true;
        }

        /// <summary>"I can't use the camera now": finishes the response without checking (no perfect bonus).</summary>
        public void SkipResponse()
        {
            if (Phase != StoryPhase.Responding && Phase != StoryPhase.AwaitingResponse) return;
            if (Spelling != null) Spelling.SkipAll();
            UsedCameraSkip = true;
            AllVerified = false;
            CompleteResponse();
        }

        /// <summary>Closes the camera without answering; the beat waits for another "Respond".</summary>
        public void CancelResponse()
        {
            if (Phase == StoryPhase.Responding) SetPhase(StoryPhase.AwaitingResponse);
        }

        private void CompleteResponse()
        {
            Spelling = null;
            SignBeatFailures = 0;
            Advance();
        }

        private void Advance()
        {
            _index++;
            StoryBeat beat = CurrentBeat;
            if (beat == null)
            {
                SetPhase(StoryPhase.Completed);
                if (Finished != null) Finished(this);
                return;
            }

            if (BeatChanged != null) BeatChanged(beat);
            SetPhase(beat.type == StoryBeatType.NpcLine ? StoryPhase.NpcSpeaking : StoryPhase.AwaitingResponse);
        }

        private void SetPhase(StoryPhase phase)
        {
            Phase = phase;
            if (PhaseChanged != null) PhaseChanged(phase);
        }

        private SignDefinition FindLetterSign(string letter)
        {
            if (_pack == null || _pack.Dictionary == null) return null;
            foreach (SignDefinition sign in _pack.Dictionary.Signs)
            {
                if (sign != null && sign.Kind == SignKind.Fingerspelling && sign.Gloss == letter) return sign;
            }

            return null;
        }

        private static IList<string> AlphabetOf(ILanguagePack pack)
        {
            return pack != null && pack.Manifest != null && pack.Manifest.alphabet != null
                ? (IList<string>)pack.Manifest.alphabet
                : new List<string>();
        }
    }
}
