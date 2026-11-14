using System;
using System.Collections.Generic;
using Cevir.Core.LanguagePacks;

namespace Cevir.Core.Game
{
    /// <summary>
    /// Spaced repetition for the "Yığım" step: a correct answer moves a sign up a box (longer interval),
    /// a miss drops it back to box 0.
    /// </summary>
    public sealed class ReviewScheduler
    {
        private static readonly double[] IntervalDays = { 0, 1, 2, 4, 8, 16 };

        public void Record(PackProgress pack, string signId, bool correct, DateTime utcNow)
        {
            SignMastery m = pack.GetOrCreateMastery(signId);
            m.attempts++;
            if (correct)
            {
                m.correct++;
                m.box = Math.Min(m.box + 1, IntervalDays.Length - 1);
            }
            else
            {
                m.box = 0;
            }

            m.dueUtcTicks = utcNow.AddDays(IntervalDays[m.box]).Ticks;
        }

        /// <summary>Most overdue signs first, excluding the current lesson's own targets.</summary>
        public List<SignDefinition> PickDue(PackProgress pack, SignDictionary dictionary, int count, DateTime utcNow,
            ICollection<SignDefinition> exclude)
        {
            var due = new List<SignMastery>();
            foreach (SignMastery m in pack.mastery)
            {
                if (m.dueUtcTicks <= utcNow.Ticks) due.Add(m);
            }

            due.Sort((a, b) => a.dueUtcTicks.CompareTo(b.dueUtcTicks));

            var result = new List<SignDefinition>(count);
            foreach (SignMastery m in due)
            {
                if (result.Count >= count) break;
                SignDefinition sign = dictionary.Find(m.signId);
                if (sign != null && (exclude == null || !exclude.Contains(sign))) result.Add(sign);
            }

            return result;
        }
    }
}
