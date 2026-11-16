using System;

namespace Cevir.Core.Recognition.Letters
{
    /// <summary>
    /// Landmarks → the letter model's input, exactly as the web translator does it (letters.js / features.js,
    /// letters_version 1, feature_version 1, groups hand_shape + hand_vel). No body tracker: the centre is the
    /// mean of the hand points, the unit is 0.3, and the hand seen in more frames goes to slot 0.
    ///
    /// Raw input per frame (NaN = not detected): hand 2×21×3 image coords (x, y in [0, 1] of the upright,
    /// NOT mirrored frame, z relative) and handWorld 2×21×3 metric coords. Slot 0 is the signer's right hand,
    /// which in an unmirrored picture is the hand further left.
    /// </summary>
    public static class AzslLetterFeatures
    {
        public const int Slot = 63;
        public const int PerFrame = 2 * Slot;
        private const float Unit = 0.3f;

        /// <returns>T × 252 features (hand_shape then hand_vel), row by row.</returns>
        public static float[] Build(float[] hand, float[] handWorld, int frames, float aspect)
        {
            int T = frames;
            if (hand.Length < T * PerFrame || handWorld.Length < T * PerFrame) throw new ArgumentException("hand arrays too short");

            // canonical(): the slot with the hand in more frames becomes slot 0 (ties keep the order).
            int n0 = 0, n1 = 0;
            for (int t = 0; t < T; t++)
            {
                if (IsFinite(hand[t * PerFrame])) n0++;
                if (IsFinite(hand[t * PerFrame + Slot])) n1++;
            }

            bool swap = n1 > n0;
            Func<int, int> slotOffset = s => (swap ? 1 - s : s) * Slot;

            // Square space (x *= width / height) and the centre: mean of all hand points (both slots).
            float a = aspect;
            double sx = 0, sy = 0;
            int nx = 0, ny = 0;
            var img = new float[T * PerFrame];
            for (int t = 0; t < T; t++)
            {
                for (int s = 0; s < 2; s++)
                {
                    int src = t * PerFrame + slotOffset(s);
                    int dst = t * PerFrame + s * Slot;
                    for (int j = 0; j < 21; j++)
                    {
                        float x = hand[src + j * 3] * a;
                        float y = hand[src + j * 3 + 1];
                        img[dst + j * 3] = x;
                        img[dst + j * 3 + 1] = y;
                        img[dst + j * 3 + 2] = hand[src + j * 3 + 2];
                        if (IsFinite(x)) { sx += x; nx++; }
                        if (IsFinite(y)) { sy += y; ny++; }
                    }
                }
            }

            float cx = 0.5f, cy = 0.5f;
            if (nx > 0 && ny > 0)
            {
                cx = (float)(sx / nx);
                cy = (float)(sy / ny);
            }

            var handN = new float[T * PerFrame];
            for (int i = 0; i < T * 42; i++)
            {
                handN[i * 3] = (img[i * 3] - cx) / Unit;
                handN[i * 3 + 1] = (img[i * 3 + 1] - cy) / Unit;
                handN[i * 3 + 2] = img[i * 3 + 2] / Unit;
            }

            const int F = AzslLetterModel.Features;
            var x0 = new float[T * F];
            for (int t = 0; t < T; t++)
            {
                int row = t * F;
                for (int s = 0; s < 2; s++)
                {
                    int src = t * PerFrame + slotOffset(s);
                    for (int j = 0; j < Slot; j++)
                    {
                        int col = s * Slot + j;
                        x0[row + col] = Clean(handWorld[src + j] * 10f);
                        int k = t * PerFrame + col;
                        x0[row + PerFrame + col] = t == 0 ? 0f : Clean(handN[k] - handN[k - PerFrame]);
                    }
                }
            }

            return x0;
        }

        private static float Clean(float v)
        {
            if (!IsFinite(v)) return 0f;
            return v > 10f ? 10f : (v < -10f ? -10f : v);
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
