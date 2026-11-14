using System;
using UnityEngine;

namespace Cevir.Core.Recognition
{
    /// <summary>
    /// Scale, position and rotation invariant description of one frame, in the canonical signer space:
    /// the dominant hand is always stored in the "dominant" slot and left-handed input is mirrored,
    /// so a pack's reference data only has to be recorded one way.
    /// The same structure is produced offline from AzSLD videos and live from the camera.
    /// </summary>
    [Serializable]
    public sealed class SignFeatureFrame
    {
        /// <summary>15 joint flexion angles + 4 finger spread angles + 4 thumb-to-fingertip distances + 3 palm normal.</summary>
        public const int HandShapeLength = 26;

        public float[] dominantShape = new float[HandShapeLength];
        public float[] nonDominantShape = new float[HandShapeLength];

        /// <summary>Wrist position relative to the nose tip, in face widths (x right, y down).</summary>
        public Vector2 dominantPosition;
        public Vector2 nonDominantPosition;

        public bool dominantPresent;
        public bool nonDominantPresent;

        /// <summary>False when no face was found and positions fall back to a hand-relative anchor.</summary>
        public bool faceAnchored;

        public NonManualSample nonManual;

        public double timestampSeconds;

        public void CopyFrom(SignFeatureFrame other)
        {
            Array.Copy(other.dominantShape, dominantShape, HandShapeLength);
            Array.Copy(other.nonDominantShape, nonDominantShape, HandShapeLength);
            dominantPosition = other.dominantPosition;
            nonDominantPosition = other.nonDominantPosition;
            dominantPresent = other.dominantPresent;
            nonDominantPresent = other.nonDominantPresent;
            faceAnchored = other.faceAnchored;
            nonManual = other.nonManual;
            timestampSeconds = other.timestampSeconds;
        }
    }

    public enum NonManualFeature
    {
        BrowRaise,
        BrowFurrow,
        MouthOpen,
        MouthRound,
        CheekPuff,
        HeadTiltRoll,
        HeadNodPitch,
        HeadShakeYaw
    }

    /// <summary>Non-manual (face/head) measurements for one frame. Expression values are 0..1, head angles in degrees.</summary>
    [Serializable]
    public struct NonManualSample
    {
        public bool present;
        public float browRaise;
        public float browFurrow;
        public float mouthOpen;
        public float mouthRound;
        public float cheekPuff;
        public float headRoll;
        public float headPitch;
        public float headYaw;

        public float Get(NonManualFeature feature)
        {
            switch (feature)
            {
                case NonManualFeature.BrowRaise: return browRaise;
                case NonManualFeature.BrowFurrow: return browFurrow;
                case NonManualFeature.MouthOpen: return mouthOpen;
                case NonManualFeature.MouthRound: return mouthRound;
                case NonManualFeature.CheekPuff: return cheekPuff;
                case NonManualFeature.HeadTiltRoll: return headRoll;
                case NonManualFeature.HeadNodPitch: return headPitch;
                case NonManualFeature.HeadShakeYaw: return headYaw;
                default: return 0f;
            }
        }

        public static NonManualSample operator -(NonManualSample a, NonManualSample b)
        {
            return new NonManualSample
            {
                present = a.present,
                browRaise = Mathf.Max(0f, a.browRaise - b.browRaise),
                browFurrow = Mathf.Max(0f, a.browFurrow - b.browFurrow),
                mouthOpen = Mathf.Max(0f, a.mouthOpen - b.mouthOpen),
                mouthRound = Mathf.Max(0f, a.mouthRound - b.mouthRound),
                cheekPuff = Mathf.Max(0f, a.cheekPuff - b.cheekPuff),
                headRoll = a.headRoll - b.headRoll,
                headPitch = a.headPitch - b.headPitch,
                headYaw = a.headYaw - b.headYaw
            };
        }
    }
}
