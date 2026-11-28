using UnityEngine;

namespace Cevir.Core.Gamification
{
    /// <summary>
    /// Game economy. Values are the plan's starting proposals, to be tuned by A/B tests.
    /// </summary>
    [CreateAssetMenu(menuName = "Chevir/Gamification Config", fileName = "GamificationConfig")]
    public sealed class GamificationConfig : ScriptableObject
    {
        [Header("Hearts")]
        [Min(1)] public int maxHearts = 10;

        [Tooltip("One heart comes back every this many minutes until the hearts are full.")]
        [Min(1)] public int heartRegenMinutes = 12; // +1 heart every 12 min: empty to full (10) in 2 hours

        [Tooltip("A wrong camera attempt costs a heart after this many failed attempts on one exercise (1 = every wrong attempt).")]
        [Min(1)] public int softFailuresPerHeart = 1;

        [Tooltip("After a wrong camera attempt the learner tries the same sign again instead of moving on.")]
        public bool retryCameraSignInPlace = true;

        [Tooltip("Hearts restore through practice lessons, never through payment.")]
        public bool allowPaidRefill = false;

        [Header("XP")]
        [Min(0)] public int lessonXp = 10;
        [Min(0)] public int perfectCameraBonusXp = 5;
        [Min(0)] public int miniGameMinXp = 5;
        [Min(0)] public int miniGameMaxXp = 20;

        [Tooltip("Added to a story's own XP reward when every response was checked by the camera and passed.")]
        [Min(0)] public int storyPerfectBonusXp = 5;

        [Header("Streak")]
        [Tooltip("Streak freezes are bought with XP, at most this many per week.")]
        [Min(0)] public int freezesPerWeek = 1;
        [Min(0)] public int freezeCostXp = 50;
        [Min(0)] public int maxStoredFreezes = 2;

        [Header("League (server-side; mirrored for UI)")]
        [Tooltip("Learners per weekly group, including the learner. The design shows groups of 10.")]
        [Min(2)] public int leagueGroupSize = 10;
        [Min(1)] public int leagueTiers = 5;
        [Min(0)] public int leaguePromoteCount = 3;
        [Min(0)] public int leagueDemoteCount = 2;

        public static GamificationConfig CreateDefault()
        {
            return CreateInstance<GamificationConfig>();
        }
    }
}
