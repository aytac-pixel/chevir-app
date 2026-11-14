using UnityEngine;

namespace Cevir.Core.Recognition
{
    /// <summary>
    /// Matcher thresholds. Starting values are proposals; the plan calls for calibrating them with the
    /// Deaf community and pilot users, so each language pack carries its own asset.
    /// </summary>
    [CreateAssetMenu(menuName = "Çevir/Recognition/Sign Matcher Config", fileName = "SignMatcherConfig")]
    public sealed class SignMatcherConfig : ScriptableObject
    {
        [Header("Tracking")]
        [Tooltip("Minimum share of attempt frames in which the required hand(s) were tracked.")]
        [Range(0f, 1f)] public float minHandVisibleRatio = 0.6f;

        [Min(2)] public int minTrackedFrames = 6;

        [Header("Sequence")]
        [Tooltip("Both sequences are resampled to this many steps before DTW.")]
        [Range(8, 64)] public int resampleLength = 32;

        [Range(0.05f, 1f)] public float dtwBandRatio = 0.3f;

        [Header("Score tolerances (distance that scores ~0.37)")]
        [Min(0.001f)] public float handShapeTolerance = 0.12f;
        [Min(0.001f)] public float trajectoryTolerance = 0.35f;
        [Min(0.001f)] public float locationTolerance = 0.8f;

        [Header("Manual score weights")]
        [Min(0f)] public float handShapeWeight = 0.6f;
        [Min(0f)] public float trajectoryWeight = 0.25f;
        [Min(0f)] public float locationWeight = 0.15f;

        [Tooltip("How much of the learned classifier's probability to blend in, when the pack has one.")]
        [Range(0f, 1f)] public float classifierWeight = 0.5f;

        [Header("Decision")]
        [Range(0f, 1f)] public float passThreshold = 0.7f;
        [Range(0f, 1f)] public float perfectThreshold = 0.88f;

        [Tooltip("Scores between this and pass are reported as 'unsure' rather than wrong, unless a sub-score clearly failed.")]
        [Range(0f, 1f)] public float unsureThreshold = 0.6f;

        [Range(0f, 1f)] public float handShapePassThreshold = 0.65f;
        [Range(0f, 1f)] public float motionPassThreshold = 0.55f;

        [Tooltip("If the learner's net movement points away from the reference's (cosine below this), say so explicitly.")]
        [Range(-1f, 1f)] public float minDirectionCosine = 0.2f;

        [Tooltip("Reference displacement (face widths) below which direction is not checked.")]
        [Min(0f)] public float minDirectionMagnitude = 0.25f;

        [Header("Non-manual")]
        [Tooltip("Percentile of the per-frame values used as the 'peak' (robust to single-frame spikes).")]
        [Range(0.5f, 1f)] public float nonManualPeakPercentile = 0.85f;

        [Tooltip("Enforce rules that are not yet linguist-approved. Keep off in release builds.")]
        public bool enforceUnapprovedRules;

        [Header("Feature weights")]
        [Tooltip("Relative weight of the palm-normal features vs. finger angles/distances.")]
        [Min(0f)] public float palmOrientationWeight = 0.5f;

        public static SignMatcherConfig CreateDefault()
        {
            return CreateInstance<SignMatcherConfig>();
        }
    }
}
