using System;
using UnityEngine;

namespace Cevir.Core.Recognition.Letters
{
    /// <summary>
    /// Live fingerspelling check with the web translator's letter model: keeps the last
    /// <see cref="Window"/> frames and classifies them on request.
    /// </summary>
    public sealed class AzslLetterRecognizer
    {
        public const string ResourceName = "azsl_letters";
        /// <summary>Frames the web translator classifies at a time (0.8 s at 30 fps).</summary>
        public const int Window = 24;

        /// <summary>Model labels in output order; the last one is "no letter".</summary>
        public static readonly string[] Labels =
        {
            "a", "b", "c", "ç", "d", "e", "ə", "f", "g", "ğ", "h", "ı", "i", "j", "k", "l", "m", "n", "o", "ö",
            "p", "q", "r", "s", "ş", "t", "u", "ü", "v", "x", "y", "z", "<none>"
        };

        public const int NoneIndex = 32;
        private const int Per = AzslLetterFeatures.PerFrame;

        private static SignNet _model;
        private static bool _loadFailed;

        private readonly float[] _hand = new float[Window * Per];
        private readonly float[] _world = new float[Window * Per];
        private readonly float[] _flatHand = new float[Window * Per];
        private readonly float[] _flatWorld = new float[Window * Per];
        private int _count;
        private int _head;
        private float _aspect = 0.75f;

        /// <summary>The latest model guess (letters or words), for the test status line ("R 92%").</summary>
        public static string LastGuessText { get; set; }

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
                    if (model.Features != AzslLetterFeatures.Features || model.Labels.Length != Labels.Length)
                        throw new InvalidOperationException("unexpected letter model shape");
                    _model = model;
                }
                catch (Exception e)
                {
                    _loadFailed = true;
                    Debug.LogError("[Chevir] Letter model could not load: " + e.Message);
                }

                return _model;
            }
        }

        public static bool Available => Model != null;

        public int FrameCount => _count;

        /// <summary>Lowercase model label for an AzSL letter gloss ("A" → "a", "İ" → "i", "I" → "ı"), or -1.</summary>
        public static int LabelIndex(string gloss)
        {
            if (string.IsNullOrEmpty(gloss) || gloss.Length != 1) return -1;
            string letter = gloss == "İ" ? "i" : gloss == "I" ? "ı" : gloss.ToLowerInvariant();
            return Array.IndexOf(Labels, letter);
        }

        public void Reset()
        {
            _count = 0;
            _head = 0;
        }

        public void Push(LandmarkFrame frame)
        {
            int o = _head * Per;
            for (int i = 0; i < Per; i++)
            {
                _hand[o + i] = float.NaN;
                _world[o + i] = float.NaN;
            }

            // Slot 0 = the signer's right hand = the hand further left in the unmirrored picture.
            HandLandmarks a = frame.LeftHand.IsPresent ? frame.LeftHand : null;
            HandLandmarks b = frame.RightHand.IsPresent ? frame.RightHand : null;
            if (a == null) { a = b; b = null; }
            if (a != null && b != null && b.Points[0].x < a.Points[0].x)
            {
                HandLandmarks t = a;
                a = b;
                b = t;
            }

            Write(a, o);
            Write(b, o + AzslLetterFeatures.Slot);
            if (frame.ImageAspect > 0f) _aspect = frame.ImageAspect;

            _head = (_head + 1) % Window;
            if (_count < Window) _count++;
        }

        private void Write(HandLandmarks hand, int offset)
        {
            if (hand == null || !hand.IsPresent || !hand.HasWorld) return;
            for (int j = 0; j < HandLandmarks.Count; j++)
            {
                Vector3 p = hand.Points[j];
                Vector3 w = hand.WorldPoints[j];
                int k = offset + j * 3;
                _hand[k] = p.x;
                _hand[k + 1] = p.y;
                _hand[k + 2] = p.z;
                _world[k] = w.x;
                _world[k + 1] = w.y;
                _world[k + 2] = w.z;
            }
        }

        public static string Upper(string letter)
        {
            return letter == "i" ? "İ" : letter == "ı" ? "I" : letter.ToUpperInvariant();
        }

        /// <summary>Probabilities over <see cref="Labels"/> for the buffered frames, or null.</summary>
        public float[] Evaluate()
        {
            SignNet model = Model;
            if (model == null || _count == 0) return null;

            int start = (_head - _count + Window) % Window;
            for (int t = 0; t < _count; t++)
            {
                int src = ((start + t) % Window) * Per;
                Array.Copy(_hand, src, _flatHand, t * Per, Per);
                Array.Copy(_world, src, _flatWorld, t * Per, Per);
            }

            float[] probs = model.Predict(AzslLetterFeatures.Build(_flatHand, _flatWorld, _count, _aspect), _count);
            int best = 0;
            for (int i = 1; i < probs.Length; i++) if (probs[i] > probs[best]) best = i;
            string label = best == NoneIndex ? "—" : Upper(Labels[best]);
            LastGuessText = label + " " + Mathf.RoundToInt(probs[best] * 100f) + "%";
            return probs;
        }
    }
}
