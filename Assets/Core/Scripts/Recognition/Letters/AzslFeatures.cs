using System;
using System.Collections.Generic;

namespace Cevir.Core.Recognition.Letters
{
    /// <summary>
    /// Landmarks → the word model's input: a C# port of the web translator's features.js <c>preprocess()</c>
    /// (feature_version 1), checked against its fixtures (ml/fixtures/features_v1).
    ///
    /// Raw input per frame, NaN = not detected: hand 2×21×3 (image x, y in [0, 1] of the upright unmirrored frame,
    /// z; slot 0 = signer's right hand), handWorld 2×21×3, pose 9×3 (<see cref="PoseIdx"/>), face 102×2, blend 52.
    /// </summary>
    public static class AzslFeatures
    {
        public const int MaxLen = 64;
        public const int AllFeatures = 656;

        /// <summary>MediaPipe Pose landmarks the models read: nose, shoulders, elbows, wrists, hips.</summary>
        public static readonly int[] PoseIdx = { 0, 11, 12, 13, 14, 15, 16, 23, 24 };

        public const int Hand = 126, Pose = 27, Face = 204, Blend = 52;

        private static readonly Dictionary<string, int[]> Groups = new Dictionary<string, int[]>
        {
            { "hands", new[] { 0, 126 } }, { "hand_shape", new[] { 126, 252 } }, { "hand_vel", new[] { 252, 378 } },
            { "pose", new[] { 378, 396 } }, { "face", new[] { 396, 600 } }, { "blend", new[] { 600, 652 } },
            { "flags", new[] { 652, 656 } }
        };

        /// <summary>Column indices for the groups, in the given order (null or empty = all).</summary>
        public static int[] Columns(string[] groups)
        {
            var cols = new List<int>();
            if (groups == null || groups.Length == 0)
            {
                for (int i = 0; i < AllFeatures; i++) cols.Add(i);
                return cols.ToArray();
            }

            foreach (string g in groups)
            {
                int[] r;
                if (!Groups.TryGetValue(g, out r)) throw new ArgumentException("unknown feature group: " + g);
                for (int i = r[0]; i < r[1]; i++) cols.Add(i);
            }

            return cols.ToArray();
        }

        /// <returns>T' × columns.Length features, row by row; <paramref name="frames"/> becomes T' (≤ MaxLen).</returns>
        public static float[] Preprocess(float[] hand, float[] handWorld, float[] pose, float[] face, float[] blend,
            ref int frames, float aspect, int[] columns, bool useFace)
        {
            int T0 = frames;
            int T = T0;

            // 1. Frame selection: longer clips are resampled to MaxLen frames with an even step (numpy rounding).
            var sel = new int[T];
            for (int i = 0; i < T; i++) sel[i] = i;
            if (T > MaxLen)
            {
                double step = (T - 1) / (double)(MaxLen - 1);
                sel = new int[MaxLen];
                for (int i = 0; i < MaxLen; i++) sel[i] = i == MaxLen - 1 ? T - 1 : RoundHalfEven(i * step);
                T = MaxLen;
            }

            // 2. Square space: x *= width / height.
            float a = aspect;
            float[] h = Pick(hand, Hand, sel, a, 3);
            float[] hw = Pick(handWorld, Hand, sel, 0f, 0);
            float[] p = Pick(pose, Pose, sel, a, 3);
            float[] fxy = useFace && face != null ? Pick(face, Face, sel, a, 2) : Fill(T * Face);
            float[] bl = useFace && blend != null ? Pick(blend, Blend, sel, 0f, 0) : Fill(T * Blend);

            // 3. Centre = median mid-shoulder, unit = median shoulder width.
            float cx, cy;
            Centre(T, p, h, out cx, out cy);
            float s = Scale(T, p);

            // 4. Columns.
            var x = new float[T * AllFeatures];
            var hn = new float[T * Hand];
            for (int i = 0; i < T * 42; i++)
            {
                hn[i * 3] = (h[i * 3] - cx) / s;
                hn[i * 3 + 1] = (h[i * 3 + 1] - cy) / s;
                hn[i * 3 + 2] = h[i * 3 + 2] / s;
            }

            for (int t = 0; t < T; t++)
            {
                int row = t * AllFeatures;
                for (int j = 0; j < Hand; j++)
                {
                    x[row + j] = hn[t * Hand + j];
                    x[row + 126 + j] = hw[t * Hand + j] * 10f;
                    x[row + 252 + j] = t == 0 ? 0f : hn[t * Hand + j] - hn[(t - 1) * Hand + j];
                }

                for (int k = 0; k < 9; k++)
                {
                    x[row + 378 + k * 2] = (p[t * Pose + k * 3] - cx) / s;
                    x[row + 378 + k * 2 + 1] = (p[t * Pose + k * 3 + 1] - cy) / s;
                }

                for (int k = 0; k < 102; k++)
                {
                    x[row + 396 + k * 2] = (fxy[t * Face + k * 2] - cx) / s;
                    x[row + 396 + k * 2 + 1] = (fxy[t * Face + k * 2 + 1] - cy) / s;
                }

                for (int j = 0; j < Blend; j++) x[row + 600 + j] = bl[t * Blend + j];
                x[row + 652] = Finite(h[t * Hand]) ? 1f : 0f;
                x[row + 653] = Finite(h[t * Hand + 63]) ? 1f : 0f;
                x[row + 654] = Finite(fxy[t * Face]) ? 1f : 0f;
                x[row + 655] = Finite(p[t * Pose + 3]) ? 1f : 0f;
            }

            // 5. NaN → 0, clip to ±10, keep the model's columns.
            int F = columns.Length;
            var output = new float[T * F];
            for (int t = 0; t < T; t++)
            {
                for (int c = 0; c < F; c++)
                {
                    float v = x[t * AllFeatures + columns[c]];
                    output[t * F + c] = !Finite(v) ? 0f : v > 10f ? 10f : v < -10f ? -10f : v;
                }
            }

            frames = T;
            return output;
        }

        private static float[] Pick(float[] src, int n, int[] sel, float scaleX, int stride)
        {
            var output = new float[sel.Length * n];
            for (int t = 0; t < sel.Length; t++)
            {
                for (int j = 0; j < n; j++)
                {
                    float v = src[sel[t] * n + j];
                    output[t * n + j] = stride > 0 && j % stride == 0 ? v * scaleX : v;
                }
            }

            return output;
        }

        private static float[] Fill(int n)
        {
            var a = new float[n];
            for (int i = 0; i < n; i++) a[i] = float.NaN;
            return a;
        }

        private static int RoundHalfEven(double v)
        {
            double r = Math.Round(v, MidpointRounding.ToEven);
            return (int)r;
        }

        private static float NanMedian(List<float> values)
        {
            var v = new List<float>();
            foreach (float f in values) if (Finite(f)) v.Add(f);
            if (v.Count == 0) return float.NaN;
            v.Sort();
            int m = v.Count >> 1;
            return v.Count % 2 == 1 ? v[m] : (v[m - 1] + v[m]) / 2f;
        }

        private static void Centre(int T, float[] pose, float[] hand, out float cx, out float cy)
        {
            var xs = new List<float>();
            var ys = new List<float>();
            for (int t = 0; t < T; t++)
            {
                for (int k = 1; k <= 2; k++)
                {
                    xs.Add(pose[t * Pose + k * 3]);
                    ys.Add(pose[t * Pose + k * 3 + 1]);
                }
            }

            cx = NanMedian(xs);
            cy = NanMedian(ys);
            if (Finite(cx) && Finite(cy)) return;

            // No body: the mean hand point, or the middle of the frame.
            double sx = 0, sy = 0;
            int nx = 0, ny = 0;
            for (int i = 0; i < T * 42; i++)
            {
                float hx = hand[i * 3], hy = hand[i * 3 + 1];
                if (Finite(hx)) { sx += hx; nx++; }
                if (Finite(hy)) { sy += hy; ny++; }
            }

            if (nx > 0 && ny > 0)
            {
                cx = (float)(sx / nx);
                cy = (float)(sy / ny);
            }
            else
            {
                cx = 0.5f;
                cy = 0.5f;
            }
        }

        private static float Scale(int T, float[] pose)
        {
            var w = new List<float>();
            for (int t = 0; t < T; t++)
            {
                float dx = pose[t * Pose + 3] - pose[t * Pose + 6];
                float dy = pose[t * Pose + 4] - pose[t * Pose + 7];
                w.Add((float)Math.Sqrt(dx * dx + dy * dy));
            }

            float m = NanMedian(w);
            return Finite(m) && m > 1e-3f ? m : 0.3f;
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
