using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Cevir.Core.Recognition.Letters
{
    /// <summary>
    /// Live word check with the web translator's word model (757 AzSL words), the same way its word mode works
    /// (s2w.js, segment.js, landmarks.js): hands, hand shapes and the body go into a buffer; a sign starts when
    /// a hand rises above the rest line and ends when the hands are down again; that stretch, and shorter windows
    /// inside it, are classified. Forms of one sign (sign_merge.json) count as one word.
    /// </summary>
    public sealed class AzslWordRecognizer
    {
        public const string ResourceName = "azsl_words";
        public const string GroupsResource = "azsl_words_groups";

        // segment.js SEGMENT (v2) with fewer windows, so a phone CPU finishes in a second or two.
        private const float RestLine = 1f;
        private const int MinOn = 3, MinOff = 8, Margin = 8, MinSpan = 8, MaxSpan = 240;
        private static readonly int[] Lens = { 24, 32 };
        private const int Stride = 8;

        private const int Capacity = 300;
        private const int H = AzslFeatures.Hand, P = AzslFeatures.Pose;
        private static readonly string[] Groups = { "hands", "hand_shape", "hand_vel", "flags", "pose" };

        private static SignNet _model;
        private static bool _loadFailed;
        private static int[] _columns;
        private static int[] _classOf;
        private static string[] _classNames;
        private static Dictionary<string, int> _classIndex;
        private static Dictionary<string, string> _groupOf;

        private readonly float[] _hand = new float[Capacity * H];
        private readonly float[] _world = new float[Capacity * H];
        private readonly float[] _pose = new float[Capacity * P];
        private float _aspect = 0.75f;

        private int _n;
        private int _on, _off;
        private int _start = -1, _last = -1;

        public struct Span
        {
            public int Lo, Hi, CoreLo, CoreHi;
        }

        /// <summary>Result of one judged attempt, filled in on a worker thread.</summary>
        public sealed class Job
        {
            private volatile bool _done;
            public bool Done => _done;
            public bool Passed;
            public float BestTarget;
            public string Guess;
            public float GuessP;
            public string Error;

            internal void Finish() { _done = true; }
        }

        public static SignNet Model
        {
            get
            {
                if (_model != null || _loadFailed) return _model;
                try
                {
                    var asset = Resources.Load<TextAsset>(ResourceName);
                    if (asset == null) throw new InvalidOperationException("Resources/" + ResourceName + ".bytes is missing");
                    SignNet model = SignNet.Load(asset.bytes);
                    int[] columns = AzslFeatures.Columns(Groups);
                    if (model.Features != columns.Length) throw new InvalidOperationException("unexpected word model shape");
                    LoadGroups(model.Labels);
                    _columns = columns;
                    _model = model;
                }
                catch (Exception e)
                {
                    _loadFailed = true;
                    Debug.LogError("[Chevir] Word model could not load: " + e.Message);
                }

                return _model;
            }
        }

        public static bool Available => Model != null;

        private static void LoadGroups(string[] labels)
        {
            _groupOf = new Dictionary<string, string>();
            var text = Resources.Load<TextAsset>(GroupsResource);
            if (text != null)
            {
                foreach (string line in text.text.Split('\n'))
                {
                    int tab = line.IndexOf('\t');
                    if (tab <= 0) continue;
                    _groupOf[line.Substring(0, tab)] = line.Substring(tab + 1).Trim();
                }
            }

            _classOf = new int[labels.Length];
            _classIndex = new Dictionary<string, int>();
            var names = new List<string>();
            for (int i = 0; i < labels.Length; i++)
            {
                string g = GroupOf(labels[i]);
                int c;
                if (!_classIndex.TryGetValue(g, out c))
                {
                    c = names.Count;
                    _classIndex[g] = c;
                    names.Add(g);
                }

                _classOf[i] = c;
            }

            _classNames = names.ToArray();
        }

        private static string GroupOf(string label)
        {
            string g;
            return _groupOf != null && _groupOf.TryGetValue(label, out g) ? g : label;
        }

        /// <summary>Model class for a lesson gloss ("Salam" → SALAM, "Kim" → KİM?), or -1 if the model does not know it.</summary>
        public static int ClassIndex(string gloss)
        {
            if (string.IsNullOrEmpty(gloss) || Model == null) return -1;
            foreach (string form in Forms(gloss))
            {
                int c;
                if (_classIndex.TryGetValue(GroupOf(form), out c) && _classNames[c] != "<unk>") return c;
                if (_classIndex.TryGetValue(GroupOf(form + "?"), out c)) return c;
            }

            return -1;
        }

        private static IEnumerable<string> Forms(string gloss)
        {
            string g = Upper(gloss);
            var clean = new System.Text.StringBuilder();
            foreach (char ch in g)
            {
                if (ch == '?' || ch == '!' || ch == '.' || ch == ',' || ch == '…') continue;
                clean.Append(ch);
            }

            string s = clean.ToString().Trim();
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            yield return s;
            if (s.IndexOf('/') >= 0)
            {
                foreach (string part in s.Split('/')) yield return part.Trim();
            }
        }

        private static string Upper(string s)
        {
            return s.Replace('i', 'İ').Replace('ı', 'I').ToUpperInvariant();
        }

        public static string ClassName(int c) => _classNames != null && c >= 0 && c < _classNames.Length ? _classNames[c] : "?";

        public bool Open => _start >= 0;
        public int FrameCount => _n;

        public void Reset()
        {
            _n = 0;
            _on = 0;
            _off = 0;
            _start = -1;
            _last = -1;
        }

        /// <summary>Adds a frame. Returns true and the sign's frames when a sign has just ended.</summary>
        public bool Push(LandmarkFrame frame, out Span span)
        {
            int slot = _n % Capacity;
            Write(frame, slot);
            bool active = Active(slot);

            int n = _n++;
            span = default(Span);
            if (active)
            {
                _on++;
                _off = 0;
                if (_start < 0 && _on >= MinOn) _start = n - _on + 1;
                if (_start >= 0) _last = n;
            }
            else
            {
                _on = 0;
                _off++;
            }

            if (_start >= 0 && (_off >= MinOff || n - _start + 1 >= MaxSpan))
            {
                _on = 0;
                _off = 0;
                return Close(n + 1, out span);
            }

            return false;
        }

        /// <summary>Ends an open sign now (time is up).</summary>
        public bool Flush(out Span span)
        {
            span = default(Span);
            return _start >= 0 && Close(_n, out span);
        }

        private bool Close(int end, out Span span)
        {
            int start = _start, last = _last;
            _start = -1;
            _last = -1;
            span = new Span
            {
                Lo = Math.Max(Math.Max(0, start - Margin), _n - Capacity),
                Hi = Math.Min(end, last + 1 + Margin),
                CoreLo = start,
                CoreHi = last + 1
            };
            return last - start + 1 >= MinSpan;
        }

        private void Write(LandmarkFrame frame, int slot)
        {
            int ho = slot * H, po = slot * P;
            for (int i = 0; i < H; i++)
            {
                _hand[ho + i] = float.NaN;
                _world[ho + i] = float.NaN;
            }

            for (int i = 0; i < P; i++) _pose[po + i] = float.NaN;
            if (frame.ImageAspect > 0f) _aspect = frame.ImageAspect;

            bool body = frame.Pose.IsPresent;
            if (body)
            {
                for (int k = 0; k < AzslFeatures.PoseIdx.Length; k++)
                {
                    Vector3 p = frame.Pose.Points[AzslFeatures.PoseIdx[k]];
                    _pose[po + k * 3] = p.x;
                    _pose[po + k * 3 + 1] = p.y;
                    _pose[po + k * 3 + 2] = p.z;
                }
            }

            // landmarks.js assignHands: slot 0 = signer's right hand, decided by the pose wrists when there is a body,
            // otherwise the hand further left in the (unmirrored) picture.
            HandLandmarks a = frame.LeftHand.IsPresent && frame.LeftHand.HasWorld ? frame.LeftHand : null;
            HandLandmarks b = frame.RightHand.IsPresent && frame.RightHand.HasWorld ? frame.RightHand : null;
            if (a == null) { a = b; b = null; }
            if (a == null) return;

            HandLandmarks right, left;
            if (body)
            {
                Vector3 lw = frame.Pose.Points[PoseLandmarks.LeftWrist], rw = frame.Pose.Points[PoseLandmarks.RightWrist];
                if (b != null)
                {
                    bool keep = Dist(a, rw) + Dist(b, lw) <= Dist(a, lw) + Dist(b, rw);
                    right = keep ? a : b;
                    left = keep ? b : a;
                }
                else
                {
                    bool isRight = Dist(a, rw) <= Dist(a, lw);
                    right = isRight ? a : null;
                    left = isRight ? null : a;
                }
            }
            else
            {
                if (b != null && b.Points[0].x < a.Points[0].x) { HandLandmarks t = a; a = b; b = t; }
                right = a;
                left = b;
            }

            Copy(right, ho);
            Copy(left, ho + 63);
        }

        private float Dist(HandLandmarks hand, Vector3 wrist)
        {
            float dx = (hand.Points[0].x - wrist.x) * _aspect, dy = hand.Points[0].y - wrist.y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private void Copy(HandLandmarks hand, int offset)
        {
            if (hand == null) return;
            for (int j = 0; j < HandLandmarks.Count; j++)
            {
                Vector3 p = hand.Points[j], w = hand.WorldPoints[j];
                int k = offset + j * 3;
                _hand[k] = p.x;
                _hand[k + 1] = p.y;
                _hand[k + 2] = p.z;
                _world[k] = w.x;
                _world[k + 1] = w.y;
                _world[k + 2] = w.z;
            }
        }

        /// <summary>segment.js frameActive: a hand above the rest line (shoulders + RestLine shoulder widths), or any hand without a body.</summary>
        private bool Active(int slot)
        {
            int po = slot * P, ho = slot * H;
            float shY = (_pose[po + 4] + _pose[po + 7]) / 2f;
            float dx = (_pose[po + 3] - _pose[po + 6]) * _aspect, dy = _pose[po + 4] - _pose[po + 7];
            float sw = (float)Math.Sqrt(dx * dx + dy * dy);
            bool body = Finite(shY) && Finite(sw) && sw > 1e-6f;
            for (int s = 0; s < 2; s++)
            {
                if (!Finite(_hand[ho + s * 63])) continue;
                if (!body || (_hand[ho + s * 63 + 1] - shY) / sw < RestLine) return true;
            }

            return false;
        }

        /// <summary>Classifies the sign on a worker thread. <paramref name="target"/> is a <see cref="ClassIndex"/>.</summary>
        public Job Judge(Span span, int target, float minProbability)
        {
            var job = new Job();
            SignNet model = Model;
            int T = span.Hi - span.Lo;
            if (model == null || T <= 0)
            {
                job.Finish();
                return job;
            }

            // Copy the frames now; the buffer keeps filling while the model runs.
            var hand = new float[T * H];
            var world = new float[T * H];
            var pose = new float[T * P];
            for (int t = 0; t < T; t++)
            {
                int slot = (span.Lo + t) % Capacity;
                Array.Copy(_hand, slot * H, hand, t * H, H);
                Array.Copy(_world, slot * H, world, t * H, H);
                Array.Copy(_pose, slot * P, pose, t * P, P);
            }

            float aspect = _aspect;
            var windows = Windows(span);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    Run(model, job, hand, world, pose, aspect, span.Lo, windows, target, minProbability);
                }
                catch (Exception e)
                {
                    job.Error = e.Message;
                }

                job.Finish();
            });
            return job;
        }

        private static List<int[]> Windows(Span s)
        {
            var output = new List<int[]> { new[] { s.Lo, s.Hi } };
            foreach (int n in Lens)
            {
                for (int a = s.Lo; a <= s.Hi - n; a += Stride)
                {
                    float centre = a + n / 2f;
                    if (s.CoreLo <= centre && centre < s.CoreHi && !(a == s.Lo && a + n == s.Hi)) output.Add(new[] { a, a + n });
                }
            }

            return output;
        }

        private static void Run(SignNet model, Job job, float[] hand, float[] world, float[] pose, float aspect, int lo,
            List<int[]> windows, int target, float minProbability)
        {
            int classes = _classNames.Length;
            var merged = new float[classes];
            foreach (int[] w in windows)
            {
                int a = w[0] - lo, T = w[1] - w[0];
                var h = new float[T * H];
                var hw = new float[T * H];
                var p = new float[T * P];
                Array.Copy(hand, a * H, h, 0, T * H);
                Array.Copy(world, a * H, hw, 0, T * H);
                Array.Copy(pose, a * P, p, 0, T * P);

                int frames = T;
                float[] x = AzslFeatures.Preprocess(h, hw, p, null, null, ref frames, aspect, _columns, false);
                float[] probs = model.Predict(x, frames);

                Array.Clear(merged, 0, classes);
                for (int i = 0; i < probs.Length; i++) merged[_classOf[i]] += probs[i];
                int best = 0;
                for (int c = 1; c < classes; c++) if (merged[c] > merged[best]) best = c;

                if (merged[best] > job.GuessP)
                {
                    job.GuessP = merged[best];
                    job.Guess = _classNames[best];
                }

                if (target >= 0)
                {
                    float pt = merged[target];
                    if (pt > job.BestTarget) job.BestTarget = pt;
                    if (best == target && pt >= minProbability)
                    {
                        job.Passed = true;
                        return;
                    }
                }
            }
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
