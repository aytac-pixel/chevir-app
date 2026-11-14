using UnityEngine;

namespace Cevir.Core.Recognition
{
    /// <summary>
    /// Converts raw landmarks into <see cref="SignFeatureFrame"/>s.
    /// Normalization (per the project plan): positions are expressed relative to the head and scaled by
    /// face width, so distance to the camera and the user's height do not change the result.
    /// Handshape features are joint angles and palm-relative distances, which are invariant to scale and rotation.
    /// </summary>
    public sealed class SignFeatureExtractor
    {
        private static readonly int[][] FingerChains =
        {
            new[] { HandLandmarks.Wrist, HandLandmarks.ThumbCmc, HandLandmarks.ThumbMcp, HandLandmarks.ThumbIp, HandLandmarks.ThumbTip },
            new[] { HandLandmarks.Wrist, HandLandmarks.IndexMcp, HandLandmarks.IndexPip, HandLandmarks.IndexDip, HandLandmarks.IndexTip },
            new[] { HandLandmarks.Wrist, HandLandmarks.MiddleMcp, HandLandmarks.MiddlePip, HandLandmarks.MiddleDip, HandLandmarks.MiddleTip },
            new[] { HandLandmarks.Wrist, HandLandmarks.RingMcp, HandLandmarks.RingPip, HandLandmarks.RingDip, HandLandmarks.RingTip },
            new[] { HandLandmarks.Wrist, HandLandmarks.PinkyMcp, HandLandmarks.PinkyPip, HandLandmarks.PinkyDip, HandLandmarks.PinkyTip }
        };

        private static readonly int[] FingerTips =
        {
            HandLandmarks.IndexTip, HandLandmarks.MiddleTip, HandLandmarks.RingTip, HandLandmarks.PinkyTip
        };

        private readonly Vector3[] _scratch = new Vector3[HandLandmarks.Count];
        private readonly Vector3[] _faceScratch = new Vector3[8];

        /// <summary>Which physical hand the user signs with. Left-handed input is mirrored into canonical space.</summary>
        public HandSide DominantHand { get; set; } = HandSide.Right;

        /// <summary>Neutral-face measurements subtracted from expression values; see <see cref="NonManualBaseline"/>.</summary>
        public NonManualSample Baseline { get; set; }

        public void Extract(LandmarkFrame source, SignFeatureFrame target)
        {
            bool mirror = DominantHand == HandSide.Left;
            HandSide nonDominantSide = DominantHand == HandSide.Right ? HandSide.Left : HandSide.Right;

            HandLandmarks dominant = source.GetHand(DominantHand);
            HandLandmarks nonDominant = source.GetHand(nonDominantSide);

            // Handedness labels are unreliable for a single visible hand; treat a lone hand as dominant.
            if (!dominant.IsPresent && nonDominant.IsPresent)
            {
                HandLandmarks swap = dominant;
                dominant = nonDominant;
                nonDominant = swap;
            }

            target.timestampSeconds = source.TimestampSeconds;

            Vector2 anchor;
            float scale;
            target.faceAnchored = TryGetFaceAnchor(source, mirror, out anchor, out scale);
            if (!target.faceAnchored)
            {
                if (dominant.IsPresent)
                {
                    LoadIsotropic(dominant.Points, source.ImageAspect, mirror, _scratch);
                    anchor = _scratch[HandLandmarks.Wrist];
                    scale = Mathf.Max(PalmSize(_scratch) * 3f, 1e-4f);
                }
                else
                {
                    anchor = Vector2.zero;
                    scale = 1f;
                }
            }

            target.dominantPresent = dominant.IsPresent;
            if (dominant.IsPresent)
            {
                LoadIsotropic(dominant.Points, source.ImageAspect, mirror, _scratch);
                ComputeHandShape(_scratch, target.dominantShape);
                target.dominantPosition = ((Vector2)_scratch[HandLandmarks.Wrist] - anchor) / scale;
            }

            target.nonDominantPresent = nonDominant.IsPresent;
            if (nonDominant.IsPresent)
            {
                LoadIsotropic(nonDominant.Points, source.ImageAspect, mirror, _scratch);
                ComputeHandShape(_scratch, target.nonDominantShape);
                target.nonDominantPosition = ((Vector2)_scratch[HandLandmarks.Wrist] - anchor) / scale;
            }

            target.nonManual = source.Face.IsPresent ? ComputeNonManual(source, mirror) - Baseline : default;
        }

        /// <summary>Raw (un-baselined) non-manual sample, used for calibration.</summary>
        public NonManualSample ComputeNonManual(LandmarkFrame source, bool mirror)
        {
            FaceLandmarks face = source.Face;
            var sample = new NonManualSample { present = face.IsPresent };
            if (!face.IsPresent)
            {
                return sample;
            }

            if (face.HasBlendshapes)
            {
                FaceBlendshapes b = face.Blendshapes;
                sample.browRaise = Mathf.Max(b.BrowInnerUp, 0.5f * (b.BrowOuterUpLeft + b.BrowOuterUpRight));
                sample.browFurrow = 0.5f * (b.BrowDownLeft + b.BrowDownRight);
                sample.mouthOpen = b.JawOpen;
                sample.mouthRound = Mathf.Max(b.MouthFunnel, b.MouthPucker);
                sample.cheekPuff = b.CheekPuff;
            }
            else
            {
                // Geometric fallback when the model was built without blendshapes.
                float faceHeight = Vector2.Distance(Iso(face.Points[FaceLandmarks.ForeheadTop], source.ImageAspect),
                    Iso(face.Points[FaceLandmarks.Chin], source.ImageAspect));
                faceHeight = Mathf.Max(faceHeight, 1e-4f);
                float browRight = Vector2.Distance(Iso(face.Points[FaceLandmarks.RightBrowMid], source.ImageAspect),
                    Iso(face.Points[FaceLandmarks.RightEyeTop], source.ImageAspect));
                float browLeft = Vector2.Distance(Iso(face.Points[FaceLandmarks.LeftBrowMid], source.ImageAspect),
                    Iso(face.Points[FaceLandmarks.LeftEyeTop], source.ImageAspect));
                float lips = Vector2.Distance(Iso(face.Points[FaceLandmarks.UpperLipInner], source.ImageAspect),
                    Iso(face.Points[FaceLandmarks.LowerLipInner], source.ImageAspect));

                // Typical neutral ratios are ~0.06 (brow) and ~0 (lips); the ranges below map to roughly 0..1.
                sample.browRaise = Mathf.Clamp01((0.5f * (browLeft + browRight) / faceHeight - 0.05f) / 0.05f);
                sample.mouthOpen = Mathf.Clamp01(lips / faceHeight / 0.15f);
            }

            if (face.HasHeadPose)
            {
                sample.headPitch = WrapDegrees(face.HeadEuler.x);
                sample.headYaw = WrapDegrees(face.HeadEuler.y);
                sample.headRoll = WrapDegrees(face.HeadEuler.z);
            }
            else
            {
                Vector2 rightEye = Iso(face.Points[FaceLandmarks.RightEyeOuter], source.ImageAspect);
                Vector2 leftEye = Iso(face.Points[FaceLandmarks.LeftEyeOuter], source.ImageAspect);
                Vector2 eyeLine = leftEye - rightEye;
                sample.headRoll = Mathf.Atan2(eyeLine.y, eyeLine.x) * Mathf.Rad2Deg;
            }

            if (mirror)
            {
                sample.headRoll = -sample.headRoll;
                sample.headYaw = -sample.headYaw;
            }

            return sample;
        }

        private bool TryGetFaceAnchor(LandmarkFrame source, bool mirror, out Vector2 anchor, out float scale)
        {
            anchor = Vector2.zero;
            scale = 1f;
            if (!source.Face.IsPresent)
            {
                return false;
            }

            Vector3[] p = source.Face.Points;
            _faceScratch[0] = p[FaceLandmarks.NoseTip];
            _faceScratch[1] = p[FaceLandmarks.CheekRight];
            _faceScratch[2] = p[FaceLandmarks.CheekLeft];
            for (int i = 0; i < 3; i++)
            {
                _faceScratch[i] = Iso(_faceScratch[i], source.ImageAspect, mirror);
            }

            anchor = _faceScratch[0];
            scale = Vector2.Distance(_faceScratch[1], _faceScratch[2]);
            return scale > 1e-4f;
        }

        private static void LoadIsotropic(Vector3[] points, float aspect, bool mirror, Vector3[] output)
        {
            for (int i = 0; i < HandLandmarks.Count; i++)
            {
                output[i] = Iso(points[i], aspect, mirror);
            }
        }

        private static Vector3 Iso(Vector3 p, float aspect, bool mirror = false)
        {
            float x = mirror ? 1f - p.x : p.x;
            return new Vector3(x * aspect, p.y, p.z * aspect);
        }

        private static float PalmSize(Vector3[] hand)
        {
            return Vector3.Distance(hand[HandLandmarks.Wrist], hand[HandLandmarks.MiddleMcp]);
        }

        /// <summary>Writes the <see cref="SignFeatureFrame.HandShapeLength"/> handshape values, each roughly in 0..1.</summary>
        public static void ComputeHandShape(Vector3[] hand, float[] output)
        {
            int k = 0;

            // 15 flexion angles: three joints per finger. 0 = straight, 1 = fully folded back.
            for (int f = 0; f < FingerChains.Length; f++)
            {
                int[] chain = FingerChains[f];
                for (int j = 1; j <= 3; j++)
                {
                    Vector3 a = hand[chain[j]] - hand[chain[j - 1]];
                    Vector3 b = hand[chain[j + 1]] - hand[chain[j]];
                    output[k++] = Vector3.Angle(a, b) / 180f;
                }
            }

            // 4 spread angles between neighbouring fingers (thumb-index, index-middle, middle-ring, ring-pinky).
            for (int f = 0; f < 4; f++)
            {
                Vector3 a = hand[FingerChains[f][4]] - hand[FingerChains[f][1]];
                Vector3 b = hand[FingerChains[f + 1][4]] - hand[FingerChains[f + 1][1]];
                output[k++] = Vector3.Angle(a, b) / 90f;
            }

            // 4 thumb-tip to fingertip distances in palm sizes: separates shapes like O, F and closed fists.
            float palm = Mathf.Max(PalmSize(hand), 1e-4f);
            for (int t = 0; t < FingerTips.Length; t++)
            {
                output[k++] = Mathf.Clamp01(Vector3.Distance(hand[HandLandmarks.ThumbTip], hand[FingerTips[t]]) / palm / 2f);
            }

            // Palm normal, so the same shape facing the camera vs facing sideways is distinguishable.
            Vector3 normal = Vector3.Cross(hand[HandLandmarks.IndexMcp] - hand[HandLandmarks.Wrist],
                hand[HandLandmarks.PinkyMcp] - hand[HandLandmarks.Wrist]).normalized;
            output[k++] = normal.x * 0.5f + 0.5f;
            output[k++] = normal.y * 0.5f + 0.5f;
            output[k] = normal.z * 0.5f + 0.5f;
        }

        private static float WrapDegrees(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f) degrees -= 360f;
            if (degrees < -180f) degrees += 360f;
            return degrees;
        }
    }
}
