using System.Collections.Generic;
using Cevir.Core.Curriculum;
using Cevir.Core.LanguagePacks;

namespace Cevir.Core.Lesson
{
    public enum ExerciseType
    {
        /// <summary>Baxış: watch clip (slow + normal) with landmark overlay. Not scored.</summary>
        Watch,

        /// <summary>Təkrar / review on camera: perform the sign and get feedback.</summary>
        CameraSign,

        /// <summary>Tanıma: clip plays, pick the right word among choices.</summary>
        Recognize
    }

    public sealed class Exercise
    {
        public ExerciseType Type;
        public LessonStep Step;
        public SignDefinition Sign;

        /// <summary>Recognize only: shuffled options including <see cref="Sign"/>.</summary>
        public List<SignDefinition> Choices;

        /// <summary>Failed camera attempts so far; drives the soft-failure heart rule.</summary>
        public int SoftFailures;

        public bool Passed;
        public bool PassedFirstTry;
        public bool Perfect;
        public bool Requeued;

        public bool IsScored => Type != ExerciseType.Watch;

        public Exercise CloneForRetry()
        {
            return new Exercise { Type = Type, Step = Step, Sign = Sign, Choices = Choices, Requeued = true };
        }
    }
}
