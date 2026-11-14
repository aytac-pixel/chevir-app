using System;
using System.Collections.Generic;
using Cevir.Core.Curriculum;
using Cevir.Core.Game;
using Cevir.Core.LanguagePacks;

namespace Cevir.Core.Lesson
{
    /// <summary>
    /// Turns curriculum data into an ordered exercise list. Standard lessons follow the plan's four steps:
    /// Baxış (watch) → Təkrar (camera) → Tanıma (recognize) → Yığım (spaced review).
    /// </summary>
    public sealed class LessonPlanBuilder
    {
        private readonly System.Random _random;
        /// <summary>Exercises in one practice or review lesson.</summary>
        public const int PracticeLength = 12;

        private readonly ReviewScheduler _reviews;

        public LessonPlanBuilder(ReviewScheduler reviews, int? seed = null)
        {
            _reviews = reviews;
            _random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        public List<Exercise> Build(LessonData lesson, SignDictionary dictionary, PackProgress progress, DateTime utcNow)
        {
            var plan = new List<Exercise>();
            IReadOnlyList<SignDefinition> targets = lesson.TargetSigns;

            if (lesson.LessonType == LessonType.Practice)
            {
                // Big practice/review lessons (a whole unit or level) are sampled, so one session stays short.
                int count = Math.Min(PracticeLength, Math.Max(lesson.ReviewItemCount, targets.Count));
                var exclude = new List<SignDefinition>();
                List<SignDefinition> due = _reviews.PickDue(progress, dictionary, count, utcNow, exclude);
                foreach (SignDefinition t in Shuffled(targets)) if (due.Count < count && !due.Contains(t)) due.Add(t);
                AddReview(plan, due, lesson.RecognitionChoices, dictionary);
                return plan;
            }

            if (lesson.LessonType == LessonType.MiniGameFindSign)
            {
                foreach (SignDefinition sign in Shuffled(targets))
                {
                    plan.Add(Recognize(sign, LessonStep.Recognize, lesson.RecognitionChoices, dictionary));
                }

                return plan;
            }

            if (lesson.LessonType == LessonType.MiniGameQuickSign)
            {
                // Prompt pool; the mini-game controller cycles through it until the timer runs out.
                foreach (SignDefinition sign in Shuffled(targets))
                {
                    plan.Add(new Exercise { Type = ExerciseType.CameraSign, Step = LessonStep.Practice, Sign = sign });
                }

                return plan;
            }

            // Watch + Practice run per sign: show it, "Anladım", then sign it on camera before the next one.
            bool paired = Contains(lesson.Steps, LessonStep.Watch) && Contains(lesson.Steps, LessonStep.Practice);
            if (paired)
            {
                foreach (SignDefinition sign in targets)
                {
                    if (sign == null) continue;
                    plan.Add(new Exercise { Type = ExerciseType.Watch, Step = LessonStep.Watch, Sign = sign });
                    plan.Add(new Exercise { Type = ExerciseType.CameraSign, Step = LessonStep.Practice, Sign = sign });
                }
            }

            foreach (LessonStep step in lesson.Steps)
            {
                if (paired && (step == LessonStep.Watch || step == LessonStep.Practice)) continue;
                switch (step)
                {
                    case LessonStep.Watch:
                        foreach (SignDefinition sign in targets)
                            plan.Add(new Exercise { Type = ExerciseType.Watch, Step = step, Sign = sign });
                        break;

                    case LessonStep.Practice:
                        foreach (SignDefinition sign in targets)
                            plan.Add(new Exercise { Type = ExerciseType.CameraSign, Step = step, Sign = sign });
                        break;

                    case LessonStep.Recognize:
                        foreach (SignDefinition sign in Shuffled(targets))
                            plan.Add(Recognize(sign, step, lesson.RecognitionChoices, dictionary));
                        break;

                    case LessonStep.Review:
                        var exclude = new List<SignDefinition>(targets);
                        List<SignDefinition> due = _reviews.PickDue(progress, dictionary, lesson.ReviewItemCount, utcNow, exclude);
                        AddReview(plan, due, lesson.RecognitionChoices, dictionary);
                        break;
                }
            }

            return plan;
        }

        /// <summary>
        /// Gate test for a level: camera checks of the level's vocabulary, sampled when the config asks
        /// for a question count (e.g. 20 random words out of 150).
        /// </summary>
        public List<Exercise> BuildGate(LevelData level)
        {
            var pool = new List<SignDefinition>();
            foreach (LessonData lesson in level.AllLessons())
            {
                foreach (SignDefinition sign in lesson.TargetSigns)
                {
                    if (sign != null && !pool.Contains(sign)) pool.Add(sign);
                }
            }

            List<SignDefinition> picked = Shuffled(pool);
            int count = level.GateTest.questionCount > 0 ? Math.Min(level.GateTest.questionCount, picked.Count) : picked.Count;
            var plan = new List<Exercise>(count);
            for (int i = 0; i < count; i++)
            {
                plan.Add(new Exercise { Type = ExerciseType.CameraSign, Step = LessonStep.Practice, Sign = picked[i] });
            }

            return plan;
        }

        private static bool Contains(IReadOnlyList<LessonStep> steps, LessonStep step)
        {
            for (int i = 0; i < steps.Count; i++) if (steps[i] == step) return true;
            return false;
        }

        private void AddReview(List<Exercise> plan, List<SignDefinition> signs, int choices, SignDictionary dictionary)
        {
            // Alternate production (camera) and recognition so review exercises both skills.
            for (int i = 0; i < signs.Count; i++)
            {
                plan.Add(i % 2 == 0
                    ? new Exercise { Type = ExerciseType.CameraSign, Step = LessonStep.Review, Sign = signs[i] }
                    : Recognize(signs[i], LessonStep.Review, choices, dictionary));
            }
        }

        private Exercise Recognize(SignDefinition sign, LessonStep step, int choices, SignDictionary dictionary)
        {
            List<SignDefinition> options = dictionary != null
                ? dictionary.PickDistractors(sign, choices - 1, _random)
                : new List<SignDefinition>();
            options.Insert(_random.Next(options.Count + 1), sign);
            return new Exercise { Type = ExerciseType.Recognize, Step = step, Sign = sign, Choices = options };
        }

        private List<SignDefinition> Shuffled(IReadOnlyList<SignDefinition> source)
        {
            var list = new List<SignDefinition>();
            foreach (SignDefinition s in source) if (s != null) list.Add(s);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                SignDefinition tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }

            return list;
        }
    }
}
