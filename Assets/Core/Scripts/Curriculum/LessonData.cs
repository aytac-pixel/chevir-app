using System.Collections.Generic;
using Cevir.Core.LanguagePacks;
using UnityEngine;

namespace Cevir.Core.Curriculum
{
    public enum LessonType
    {
        /// <summary>Teaches new signs with the four-step structure.</summary>
        Standard,

        /// <summary>Review of earlier material; completing it restores hearts.</summary>
        Practice,

        /// <summary>"Sürətli İşarə": 60 s, sign as many prompted words as possible.</summary>
        MiniGameQuickSign,

        /// <summary>"İşarəni Tap": watch a clip, pick the right word of four.</summary>
        MiniGameFindSign,

        /// <summary>"Göz-gözə": scripted dialogue with the avatar.</summary>
        AvatarDialogue
    }

    /// <summary>The fixed steps every standard lesson follows, per the curriculum plan.</summary>
    public enum LessonStep
    {
        /// <summary>Baxış: watch the sign at slow and normal speed with landmarks overlaid.</summary>
        Watch,

        /// <summary>Təkrar: perform the sign on camera and get feedback.</summary>
        Practice,

        /// <summary>Tanıma: "İşarəni Tap" style recognition quiz.</summary>
        Recognize,

        /// <summary>Yığım: spaced-repetition review of earlier lessons.</summary>
        Review
    }

    [CreateAssetMenu(menuName = "Chevir/Curriculum/Lesson", fileName = "Lesson_")]
    public sealed class LessonData : CurriculumAsset
    {
        [SerializeField] private LessonType lessonType = LessonType.Standard;

        [SerializeField] private List<SignDefinition> targetSigns = new List<SignDefinition>();

        [SerializeField] private List<LessonStep> steps = new List<LessonStep>
        {
            LessonStep.Watch, LessonStep.Practice, LessonStep.Recognize, LessonStep.Review
        };

        [Tooltip("Answer options in recognition questions (1 correct + distractors).")]
        [Range(2, 6)]
        [SerializeField] private int recognitionChoices = 4;

        [Tooltip("Spaced-repetition items drawn from earlier lessons in the Review step.")]
        [Range(0, 20)]
        [SerializeField] private int reviewItemCount = 4;

        [Tooltip("Mini-games only.")]
        [Min(0)]
        [SerializeField] private float timeLimitSeconds = 60f;

        [Tooltip("Practice lessons only: hearts restored on completion.")]
        [Min(0)]
        [SerializeField] private int heartsRestored = 1;

        public LessonType LessonType => lessonType;
        public IReadOnlyList<SignDefinition> TargetSigns => targetSigns;
        public IReadOnlyList<LessonStep> Steps => steps;
        public int RecognitionChoices => recognitionChoices;
        public int ReviewItemCount => reviewItemCount;
        public float TimeLimitSeconds => timeLimitSeconds;
        public int HeartsRestored => heartsRestored;

        public bool IsMiniGame => lessonType == LessonType.MiniGameQuickSign || lessonType == LessonType.MiniGameFindSign;

        /// <summary>Lessons that cost no hearts to start, so a learner with zero hearts can recover.</summary>
        public bool IsHeartFree => lessonType == LessonType.Practice;

#if UNITY_EDITOR
        public void EditorSetContent(LessonType type, List<SignDefinition> signs)
        {
            lessonType = type;
            targetSigns = signs;
        }

        public void EditorSetSteps(List<LessonStep> lessonSteps)
        {
            steps = lessonSteps;
        }
#endif
    }
}
