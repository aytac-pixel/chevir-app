using System;
using System.IO;

namespace Cevir.Core.Recognition.Letters
{
    /// <summary>
    /// The sign classifiers of the Chevir web translator (chevirapp.com/tercume: letter_model, word_model), run on
    /// the phone in plain C#. Input: frames × features (<see cref="AzslLetterFeatures"/>, <see cref="AzslFeatures"/>);
    /// output: class probabilities. Network: linear stem, 2 × (3 depthwise-conv blocks + 1 transformer block),
    /// mean pooling, linear head. Weights and labels come from the web model's ONNX file and manifest
    /// (Tools/letter_model/export_weights.py), and match onnxruntime to about 1e-6.
    /// </summary>
    public sealed class SignNet
    {
        private const int Kernel = 17;
        private const int Heads = 4;
        private const float Eps = 1e-5f;

        private readonly int Dim;
        private readonly int Hidden;

        /// <summary>Input columns per frame.</summary>
        public int Features { get; private set; }

        /// <summary>Class names in output order.</summary>
        public string[] Labels { get; private set; }

        private sealed class ConvBlock
        {
            public float[] NormG, NormB, Pw1W, Pw1B, DwW, DwB, Eca, Pw2W, Pw2B;
        }

        private sealed class AttnBlock
        {
            public float[] N1G, N1B, QkvW, QkvB, OutW, OutB, N2G, N2B, Ff0W, Ff0B, Ff3W, Ff3B;
        }

        private readonly float[] _stemW, _stemB, _stemG, _stemBeta, _normG, _normB, _headW, _headB;
        private readonly object[] _blocks = new object[8];

        private SignNet(BinaryReader r)
        {
            if (System.Text.Encoding.ASCII.GetString(r.ReadBytes(4)) != "CHVL") throw new InvalidDataException("not a Chevir sign model");
            int version = r.ReadInt32();
            if (version != 2) throw new InvalidDataException("sign model version " + version);
            Features = r.ReadInt32();
            Dim = r.ReadInt32();
            Hidden = 2 * Dim;
            int classes = r.ReadInt32();
            int count = r.ReadInt32();
            if (count != 86) throw new InvalidDataException("sign model has " + count + " tensors");

            Func<int, float[]> next = expected =>
            {
                int n = r.ReadInt32();
                if (n != expected) throw new InvalidDataException("tensor size " + n + ", expected " + expected);
                var a = new float[n];
                byte[] bytes = r.ReadBytes(n * 4);
                Buffer.BlockCopy(bytes, 0, a, 0, bytes.Length);
                return a;
            };

            _stemW = next(Features * Dim);
            _stemB = next(Dim);
            _stemG = next(Dim);
            _stemBeta = next(Dim);
            for (int b = 0; b < 8; b++)
            {
                if (b == 3 || b == 7)
                {
                    _blocks[b] = new AttnBlock
                    {
                        N1G = next(Dim), N1B = next(Dim), QkvW = next(Dim * 3 * Dim), QkvB = next(3 * Dim),
                        OutW = next(Dim * Dim), OutB = next(Dim), N2G = next(Dim), N2B = next(Dim),
                        Ff0W = next(Dim * Hidden), Ff0B = next(Hidden), Ff3W = next(Hidden * Dim), Ff3B = next(Dim)
                    };
                }
                else
                {
                    _blocks[b] = new ConvBlock
                    {
                        NormG = next(Dim), NormB = next(Dim), Pw1W = next(Dim * Hidden), Pw1B = next(Hidden),
                        DwW = next(Hidden * Kernel), DwB = next(Hidden), Eca = next(5), Pw2W = next(Hidden * Dim), Pw2B = next(Dim)
                    };
                }
            }

            _normG = next(Dim);
            _normB = next(Dim);
            _headW = next(Dim * classes);
            _headB = next(classes);

            Labels = new string[classes];
            for (int i = 0; i < classes; i++) Labels[i] = System.Text.Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));
        }

        public static SignNet Load(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            using (var reader = new BinaryReader(stream))
            {
                return new SignNet(reader);
            }
        }

        /// <summary>Probabilities (softmax) for <see cref="Labels"/>; <paramref name="x"/> is T × 252, row by row.</summary>
        public float[] Predict(float[] x, int frames)
        {
            int T = frames;
            if (T <= 0 || x.Length < T * Features) throw new ArgumentException("input must be frames × " + Features);

            var h = new float[T * Dim];
            MatMul(x, T, Features, _stemW, Dim, _stemB, h);
            LayerNorm(h, T, Dim, _stemG, _stemBeta, h);

            var a = new float[T * Dim];
            var y = new float[T * Hidden];
            var z = new float[T * Hidden];
            var o = new float[T * Dim];
            var qkv = new float[T * 3 * Dim];
            var att = new float[T * Dim];
            var scores = new float[T];

            for (int b = 0; b < 8; b++)
            {
                var conv = _blocks[b] as ConvBlock;
                if (conv != null)
                {
                    LayerNorm(h, T, Dim, conv.NormG, conv.NormB, a);
                    MatMul(a, T, Dim, conv.Pw1W, Hidden, conv.Pw1B, y);
                    for (int i = 0; i < T * Hidden; i++) y[i] = Silu(y[i]);

                    // Depthwise conv over time (BatchNorm already folded in), then SiLU.
                    int pad = Kernel / 2;
                    for (int t = 0; t < T; t++)
                    {
                        for (int c = 0; c < Hidden; c++)
                        {
                            float s = conv.DwB[c];
                            for (int k = 0; k < Kernel; k++)
                            {
                                int tt = t + k - pad;
                                if (tt < 0 || tt >= T) continue;
                                s += y[tt * Hidden + c] * conv.DwW[c * Kernel + k];
                            }

                            z[t * Hidden + c] = Silu(s);
                        }
                    }

                    // Efficient channel attention: 5-tap conv across channels of the time average.
                    var avg = new float[Hidden];
                    for (int t = 0; t < T; t++)
                        for (int c = 0; c < Hidden; c++) avg[c] += z[t * Hidden + c];
                    for (int c = 0; c < Hidden; c++) avg[c] /= T;
                    var gate = new float[Hidden];
                    for (int c = 0; c < Hidden; c++)
                    {
                        float s = 0f;
                        for (int k = 0; k < 5; k++)
                        {
                            int cc = c + k - 2;
                            if (cc >= 0 && cc < Hidden) s += avg[cc] * conv.Eca[k];
                        }

                        gate[c] = Sigmoid(s);
                    }

                    for (int t = 0; t < T; t++)
                        for (int c = 0; c < Hidden; c++) z[t * Hidden + c] *= gate[c];

                    MatMul(z, T, Hidden, conv.Pw2W, Dim, conv.Pw2B, o);
                    for (int i = 0; i < T * Dim; i++) h[i] += o[i];
                    continue;
                }

                var attn = (AttnBlock)_blocks[b];
                LayerNorm(h, T, Dim, attn.N1G, attn.N1B, a);
                MatMul(a, T, Dim, attn.QkvW, 3 * Dim, attn.QkvB, qkv);
                int d = Dim / Heads;
                float scale = 1f / (float)Math.Sqrt(d);
                Array.Clear(att, 0, att.Length);
                for (int head = 0; head < Heads; head++)
                {
                    int off = head * d;
                    for (int i = 0; i < T; i++)
                    {
                        float max = float.NegativeInfinity;
                        for (int j = 0; j < T; j++)
                        {
                            float s = 0f;
                            for (int e = 0; e < d; e++) s += qkv[i * 3 * Dim + off + e] * qkv[j * 3 * Dim + Dim + off + e];
                            s *= scale;
                            scores[j] = s;
                            if (s > max) max = s;
                        }

                        float sum = 0f;
                        for (int j = 0; j < T; j++)
                        {
                            scores[j] = (float)Math.Exp(scores[j] - max);
                            sum += scores[j];
                        }

                        for (int j = 0; j < T; j++)
                        {
                            float w = scores[j] / sum;
                            for (int e = 0; e < d; e++) att[i * Dim + off + e] += w * qkv[j * 3 * Dim + 2 * Dim + off + e];
                        }
                    }
                }

                MatMul(att, T, Dim, attn.OutW, Dim, attn.OutB, o);
                for (int i = 0; i < T * Dim; i++) h[i] += o[i];

                LayerNorm(h, T, Dim, attn.N2G, attn.N2B, a);
                MatMul(a, T, Dim, attn.Ff0W, Hidden, attn.Ff0B, y);
                for (int i = 0; i < T * Hidden; i++) y[i] = Silu(y[i]);
                MatMul(y, T, Hidden, attn.Ff3W, Dim, attn.Ff3B, o);
                for (int i = 0; i < T * Dim; i++) h[i] += o[i];
            }

            var pooled = new float[Dim];
            for (int t = 0; t < T; t++)
                for (int c = 0; c < Dim; c++) pooled[c] += h[t * Dim + c];
            for (int c = 0; c < Dim; c++) pooled[c] /= T;
            LayerNorm(pooled, 1, Dim, _normG, _normB, pooled);

            var logits = new float[Labels.Length];
            MatMul(pooled, 1, Dim, _headW, Labels.Length, _headB, logits);
            return Softmax(logits);
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>out (rows × m) = x (rows × n) · w (n × m) + bias.</summary>
        private static void MatMul(float[] x, int rows, int n, float[] w, int m, float[] bias, float[] output)
        {
            var row = new float[m];
            for (int r = 0; r < rows; r++)
            {
                Array.Copy(bias, row, m);
                int xo = r * n;
                for (int k = 0; k < n; k++)
                {
                    float v = x[xo + k];
                    if (v == 0f) continue;
                    int wo = k * m;
                    for (int j = 0; j < m; j++) row[j] += v * w[wo + j];
                }

                Array.Copy(row, 0, output, r * m, m);
            }
        }

        private static void LayerNorm(float[] x, int rows, int n, float[] g, float[] b, float[] output)
        {
            for (int r = 0; r < rows; r++)
            {
                int o = r * n;
                float mean = 0f;
                for (int i = 0; i < n; i++) mean += x[o + i];
                mean /= n;
                float var = 0f;
                for (int i = 0; i < n; i++)
                {
                    float dv = x[o + i] - mean;
                    var += dv * dv;
                }

                float inv = 1f / (float)Math.Sqrt(var / n + Eps);
                for (int i = 0; i < n; i++) output[o + i] = (x[o + i] - mean) * inv * g[i] + b[i];
            }
        }

        private static float Sigmoid(float v) => 1f / (1f + (float)Math.Exp(-v));

        private static float Silu(float v) => v * Sigmoid(v);

        private static float[] Softmax(float[] logits)
        {
            float max = float.NegativeInfinity;
            foreach (float v in logits) if (v > max) max = v;
            var p = new float[logits.Length];
            float sum = 0f;
            for (int i = 0; i < p.Length; i++)
            {
                p[i] = (float)Math.Exp(logits[i] - max);
                sum += p[i];
            }

            for (int i = 0; i < p.Length; i++) p[i] /= sum;
            return p;
        }
    }
}
