using System.Collections.Generic;
using Cevir.Core.LanguagePacks;
using Cevir.Core.Recognition;
using UnityEngine;

namespace Cevir.Core.Tests
{
    /// <summary>Builds synthetic hands and signs so matcher tests do not need dataset files.</summary>
    internal static class TestSigns
    {
        /// <summary>A right hand at <paramref name="wrist"/>; curl 0 = flat open hand, 1 = fist-like.</summary>
        public static Vector3[] Hand(Vector2 wrist, float curl, float size = 0.1f)
        {
            var p = new Vector3[HandLandmarks.Count];
            p[0] = new Vector3(wrist.x, wrist.y, 0f);
            float[] fingerX = { -0.6f, -0.25f, 0f, 0.25f, 0.5f };
            for (int f = 0; f < 5; f++)
            {
                Vector3 basePoint = p[0] + new Vector3(fingerX[f] * size, -0.5f * size, 0f);
                p[1 + f * 4] = basePoint;
                Vector3 dir = new Vector3(0f, -1f, 0f);
                Vector3 cur = basePoint;
                for (int j = 1; j < 4; j++)
                {
                    float angle = curl * 80f * Mathf.Deg2Rad * j / 2f;
                    dir = new Vector3(0f, -Mathf.Cos(angle), -Mathf.Sin(angle));
                    cur += dir * 0.25f * size;
                    p[1 + f * 4 + j] = cur;
                }
            }

            return p;
        }

        public static LandmarkFrame Frame(Vector2 wrist, float curl, bool withFace = true, float browInnerUp = 0f)
        {
            var frame = new LandmarkFrame { ImageAspect = 1f };
            System.Array.Copy(Hand(wrist, curl), frame.RightHand.Points, HandLandmarks.Count);
            frame.RightHand.IsPresent = true;
            frame.RightHand.Confidence = 0.95f;
            if (withFace)
            {
                Vector3[] f = frame.Face.Points;
                f[FaceLandmarks.NoseTip] = new Vector3(0.5f, 0.3f, 0f);
                f[FaceLandmarks.CheekRight] = new Vector3(0.42f, 0.3f, 0f);
                f[FaceLandmarks.CheekLeft] = new Vector3(0.58f, 0.3f, 0f);
                f[FaceLandmarks.RightEyeOuter] = new Vector3(0.45f, 0.27f, 0f);
                f[FaceLandmarks.LeftEyeOuter] = new Vector3(0.55f, 0.27f, 0f);
                frame.Face.IsPresent = true;
                frame.Face.HasBlendshapes = true;
                frame.Face.Blendshapes = new FaceBlendshapes { BrowInnerUp = browInnerUp };
            }

            return frame;
        }

        /// <summary>A sign whose hand moves from <paramref name="from"/> to <paramref name="to"/> while curling.</summary>
        public static List<SignFeatureFrame> Sequence(Vector2 from, Vector2 to, float curlFrom, float curlTo, int frames = 30,
            float browInnerUp = 0f)
        {
            var extractor = new SignFeatureExtractor();
            var result = new List<SignFeatureFrame>();
            for (int i = 0; i < frames; i++)
            {
                float t = i / (float)(frames - 1);
                var f = new SignFeatureFrame();
                extractor.Extract(Frame(Vector2.Lerp(from, to, t), Mathf.Lerp(curlFrom, curlTo, t), true, browInnerUp), f);
                result.Add(f);
            }

            return result;
        }

        public static SignDefinition Sign(string id, bool dynamic, List<SignFeatureFrame> reference, NonManualRule rule = null)
        {
            var sign = ScriptableObject.CreateInstance<SignDefinition>();
            sign.EditorInitialize(id, id, SignKind.Word, dynamic, false, "test");
            var r = ScriptableObject.CreateInstance<SignReference>();
            r.Initialize(id, "test", "s0", reference);
            sign.EditorAddReference(r);
            if (rule != null)
            {
                typeof(SignDefinition).GetField("nonManualRule", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .SetValue(sign, rule);
            }

            return sign;
        }
    }
}
