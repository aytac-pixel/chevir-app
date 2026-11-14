using System;
using System.Collections.Generic;
using Cevir.Core.LanguagePacks;
using Cevir.Core.Localization;
using Cevir.Core.Recognition;

namespace Cevir.Core.Stories
{
    public enum LetterStatus
    {
        Pending,
        Correct,

        /// <summary>Accepted without a handshape check, because the letter has no reference recordings yet.</summary>
        SelfChecked,

        Skipped,

        /// <summary>Not in the pack's alphabet (e.g. "W"); shown, but not asked.</summary>
        Unsupported
    }

    public sealed class SpelledLetter
    {
        public string Letter;
        public SignDefinition Sign;
        public int WordIndex;
        public LetterStatus Status;
        public int Failures;

        public bool IsDone => Status != LetterStatus.Pending;
        public bool CanBeChecked => Sign != null && Sign.HasRecognitionData;
    }

    public struct SpellOutcome
    {
        public bool LetterDone;
        public bool TaskComplete;
        public SignFeedback Feedback;
        public FeedbackSeverity Severity;

        /// <summary>The learner may skip the letter (repeated mistakes, never tracking problems).</summary>
        public bool CanSkip;
    }

    /// <summary>
    /// Spelling words (the learner's name) letter by letter with the pack's fingerspelling signs. Each attempt is
    /// matched against the current letter only; mistakes give a tip and retry, tracking problems never count.
    /// Pure C#, no camera, so the flow is unit-testable.
    /// </summary>
    public sealed class FingerspellingTask
    {
        private readonly List<SpelledLetter> _letters = new List<SpelledLetter>();
        private readonly int _failuresBeforeSkip;
        private int _index;

        public FingerspellingTask(IList<string> words, IList<string> alphabet, Func<string, SignDefinition> letterSign, int failuresBeforeSkip = 3)
        {
            _failuresBeforeSkip = Math.Max(1, failuresBeforeSkip);
            for (int w = 0; w < words.Count; w++)
            {
                foreach (string letter in SplitLetters(words[w], alphabet))
                {
                    bool supported = alphabet.Contains(letter);
                    SignDefinition sign = supported && letterSign != null ? letterSign(letter) : null;
                    _letters.Add(new SpelledLetter
                    {
                        Letter = letter,
                        Sign = sign,
                        WordIndex = w,
                        Status = supported && sign != null ? LetterStatus.Pending : LetterStatus.Unsupported
                    });
                }
            }

            _index = -1;
            MoveNext();
        }

        public IReadOnlyList<SpelledLetter> Letters => _letters;
        public int CurrentIndex => _index;
        public SpelledLetter Current => _index >= 0 && _index < _letters.Count ? _letters[_index] : null;
        public bool IsComplete => Current == null;

        public int AskedCount
        {
            get
            {
                int n = 0;
                foreach (SpelledLetter l in _letters) if (l.Status != LetterStatus.Unsupported) n++;
                return n;
            }
        }

        public int CountWith(LetterStatus status)
        {
            int n = 0;
            foreach (SpelledLetter l in _letters) if (l.Status == status) n++;
            return n;
        }

        /// <summary>Every asked letter was recognised by the matcher (no self-checks, no skips).</summary>
        public bool AllVerified => AskedCount > 0 && CountWith(LetterStatus.Correct) == AskedCount;

        public float Progress01 => _letters.Count == 0 ? 1f : (IsComplete ? 1f : _index / (float)_letters.Count);

        public SpellOutcome Submit(MatchResult result)
        {
            var outcome = new SpellOutcome { Feedback = result.Feedback, Severity = result.Severity };
            SpelledLetter letter = Current;
            if (letter == null)
            {
                outcome.TaskComplete = true;
                return outcome;
            }

            if (result.IsCorrect)
            {
                letter.Status = result.Unverified || !letter.CanBeChecked ? LetterStatus.SelfChecked : LetterStatus.Correct;
                outcome.LetterDone = true;
                MoveNext();
            }
            else if (result.CountsAsFailedAttempt)
            {
                letter.Failures++;
            }

            outcome.TaskComplete = IsComplete;
            outcome.CanSkip = !IsComplete && Current.Failures >= _failuresBeforeSkip;
            return outcome;
        }

        public bool CanSkipCurrent => Current != null && Current.Failures >= _failuresBeforeSkip;

        /// <summary>Skips the current letter (after repeated mistakes, or when the camera can't be used).</summary>
        public void SkipCurrent()
        {
            if (Current == null) return;
            Current.Status = LetterStatus.Skipped;
            MoveNext();
        }

        public void SkipAll()
        {
            while (Current != null) SkipCurrent();
        }

        private void MoveNext()
        {
            do
            {
                _index++;
            } while (_index < _letters.Count && _letters[_index].Status != LetterStatus.Pending);
        }

        /// <summary>
        /// Splits a word into alphabet letters with Azerbaijani case rules ("Əli" → Ə, L, İ). Multi-character
        /// alphabet entries are matched first; anything else becomes a single unsupported character.
        /// </summary>
        public static List<string> SplitLetters(string word, IList<string> alphabet)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(word)) return result;
            string upper = Loc.ToUpperAz(word.Trim());

            int longest = 1;
            foreach (string a in alphabet) longest = Math.Max(longest, a.Length);

            int i = 0;
            while (i < upper.Length)
            {
                if (char.IsWhiteSpace(upper[i]) || upper[i] == '-' || upper[i] == '\'')
                {
                    i++;
                    continue;
                }

                string match = null;
                for (int len = Math.Min(longest, upper.Length - i); len >= 1 && match == null; len--)
                {
                    string candidate = upper.Substring(i, len);
                    if (alphabet.Contains(candidate)) match = candidate;
                }

                if (match == null) match = upper.Substring(i, 1);
                result.Add(match);
                i += match.Length;
            }

            return result;
        }
    }
}
