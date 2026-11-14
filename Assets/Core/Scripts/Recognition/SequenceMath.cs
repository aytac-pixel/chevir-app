using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cevir.Core.Recognition
{
    /// <summary>Resampling and dynamic time warping used by <see cref="SignMatcher"/>.</summary>
    public static class SequenceMath
    {
        /// <summary>Linearly resamples a sequence of equal-length vectors to <paramref name="length"/> rows.</summary>
        public static float[,] Resample(IReadOnlyList<float[]> rows, int length)
        {
            int dim = rows[0].Length;
            var result = new float[length, dim];
            int n = rows.Count;
            for (int i = 0; i < length; i++)
            {
                float t = length == 1 ? 0f : i * (n - 1) / (float)(length - 1);
                int i0 = Mathf.FloorToInt(t);
                int i1 = Mathf.Min(i0 + 1, n - 1);
                float w = t - i0;
                float[] a = rows[i0];
                float[] b = rows[i1];
                for (int d = 0; d < dim; d++)
                {
                    result[i, d] = a[d] + (b[d] - a[d]) * w;
                }
            }

            return result;
        }

        /// <summary>
        /// DTW with a Sakoe-Chiba band. Returns the mean per-step distance along the best path, where the
        /// step distance is the weighted RMS difference between rows (so values are comparable across dims).
        /// </summary>
        public static float Dtw(float[,] a, float[,] b, float[] weights, float bandRatio)
        {
            int n = a.GetLength(0);
            int m = b.GetLength(0);
            int dim = a.GetLength(1);
            if (b.GetLength(1) != dim)
            {
                throw new ArgumentException("Sequences must have the same dimension.");
            }

            int band = Mathf.Max(Mathf.CeilToInt(Mathf.Max(n, m) * bandRatio), Mathf.Abs(n - m));
            float weightSum = 0f;
            for (int d = 0; d < dim; d++)
            {
                weightSum += weights != null ? weights[d] : 1f;
            }

            var cost = new float[n + 1, m + 1];
            var steps = new int[n + 1, m + 1];
            for (int i = 0; i <= n; i++)
            for (int j = 0; j <= m; j++)
            {
                cost[i, j] = float.PositiveInfinity;
            }

            cost[0, 0] = 0f;

            for (int i = 1; i <= n; i++)
            {
                int jStart = Mathf.Max(1, i - band);
                int jEnd = Mathf.Min(m, i + band);
                for (int j = jStart; j <= jEnd; j++)
                {
                    float sq = 0f;
                    for (int d = 0; d < dim; d++)
                    {
                        float diff = a[i - 1, d] - b[j - 1, d];
                        sq += diff * diff * (weights != null ? weights[d] : 1f);
                    }

                    float local = Mathf.Sqrt(sq / weightSum);

                    float best = cost[i - 1, j - 1];
                    int bestSteps = steps[i - 1, j - 1];
                    if (cost[i - 1, j] < best)
                    {
                        best = cost[i - 1, j];
                        bestSteps = steps[i - 1, j];
                    }

                    if (cost[i, j - 1] < best)
                    {
                        best = cost[i, j - 1];
                        bestSteps = steps[i, j - 1];
                    }

                    cost[i, j] = best + local;
                    steps[i, j] = bestSteps + 1;
                }
            }

            return steps[n, m] > 0 ? cost[n, m] / steps[n, m] : float.PositiveInfinity;
        }

        /// <summary>Converts a distance into a 0..1 similarity; <paramref name="tolerance"/> is the distance that scores ~0.37.</summary>
        public static float Similarity(float distance, float tolerance)
        {
            if (float.IsInfinity(distance) || float.IsNaN(distance))
            {
                return 0f;
            }

            return Mathf.Exp(-distance / Mathf.Max(tolerance, 1e-5f));
        }

        public static float Percentile(List<float> values, float percentile)
        {
            if (values.Count == 0)
            {
                return 0f;
            }

            values.Sort();
            int index = Mathf.Clamp(Mathf.RoundToInt(percentile * (values.Count - 1)), 0, values.Count - 1);
            return values[index];
        }
    }
}
