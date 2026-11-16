using UnityEngine;

namespace Cevir.Core.Recognition
{
    public enum HandSide
    {
        Left,
        Right
    }

    /// <summary>
    /// 21 MediaPipe hand landmarks in normalized image space (x, y in [0, 1], y down, z relative depth).
    /// Index map: 0 wrist; thumb 1-4; index 5-8; middle 9-12; ring 13-16; pinky 17-20.
    /// </summary>
    public sealed class HandLandmarks
    {
        public const int Count = 21;

        public const int Wrist = 0;
        public const int ThumbCmc = 1, ThumbMcp = 2, ThumbIp = 3, ThumbTip = 4;
        public const int IndexMcp = 5, IndexPip = 6, IndexDip = 7, IndexTip = 8;
        public const int MiddleMcp = 9, MiddlePip = 10, MiddleDip = 11, MiddleTip = 12;
        public const int RingMcp = 13, RingPip = 14, RingDip = 15, RingTip = 16;
        public const int PinkyMcp = 17, PinkyPip = 18, PinkyDip = 19, PinkyTip = 20;

        public readonly Vector3[] Points = new Vector3[Count];

        /// <summary>Metric 3D hand shape (MediaPipe world landmarks, metres, origin near the hand centre).</summary>
        public readonly Vector3[] WorldPoints = new Vector3[Count];

        public bool HasWorld;
        public bool IsPresent;

        /// <summary>Handedness classification score from the landmarker, 0..1.</summary>
        public float Confidence;

        public void CopyFrom(HandLandmarks other)
        {
            System.Array.Copy(other.Points, Points, Count);
            System.Array.Copy(other.WorldPoints, WorldPoints, Count);
            HasWorld = other.HasWorld;
            IsPresent = other.IsPresent;
            Confidence = other.Confidence;
        }

        public void Clear()
        {
            IsPresent = false;
            HasWorld = false;
            Confidence = 0f;
        }
    }

    /// <summary>
    /// The face blendshape scores that the non-manual checks use (subset of the 52 ARKit-style
    /// blendshapes produced by MediaPipe FaceLandmarker).
    /// </summary>
    public struct FaceBlendshapes
    {
        public float BrowInnerUp;
        public float BrowOuterUpLeft;
        public float BrowOuterUpRight;
        public float BrowDownLeft;
        public float BrowDownRight;
        public float JawOpen;
        public float MouthFunnel;
        public float MouthPucker;
        public float CheekPuff;

        /// <summary>Maps a MediaPipe category name onto this struct. Unknown names are ignored.</summary>
        public void Set(string categoryName, float score)
        {
            switch (categoryName)
            {
                case "browInnerUp": BrowInnerUp = score; break;
                case "browOuterUpLeft": BrowOuterUpLeft = score; break;
                case "browOuterUpRight": BrowOuterUpRight = score; break;
                case "browDownLeft": BrowDownLeft = score; break;
                case "browDownRight": BrowDownRight = score; break;
                case "jawOpen": JawOpen = score; break;
                case "mouthFunnel": MouthFunnel = score; break;
                case "mouthPucker": MouthPucker = score; break;
                case "cheekPuff": CheekPuff = score; break;
            }
        }
    }

    /// <summary>
    /// 478 MediaPipe FaceLandmarker points (468 mesh + 10 iris) in normalized image space,
    /// plus optional blendshapes and head pose.
    /// </summary>
    public sealed class FaceLandmarks
    {
        public const int Count = 478;

        public const int NoseTip = 1;
        public const int ForeheadTop = 10;
        public const int Chin = 152;
        public const int CheekRight = 234;
        public const int CheekLeft = 454;
        public const int RightEyeOuter = 33;
        public const int LeftEyeOuter = 263;
        public const int RightEyeTop = 159;
        public const int LeftEyeTop = 386;
        public const int RightBrowMid = 105;
        public const int LeftBrowMid = 334;
        public const int UpperLipInner = 13;
        public const int LowerLipInner = 14;

        public readonly Vector3[] Points = new Vector3[Count];

        /// <summary>Metric 3D hand shape (MediaPipe world landmarks, metres, origin near the hand centre).</summary>
        public readonly Vector3[] WorldPoints = new Vector3[Count];

        public bool HasWorld;
        public bool IsPresent;

        public FaceBlendshapes Blendshapes;
        public bool HasBlendshapes;

        /// <summary>Head rotation in degrees (pitch, yaw, roll) from the facial transformation matrix.</summary>
        public Vector3 HeadEuler;
        public bool HasHeadPose;

        public void CopyFrom(FaceLandmarks other)
        {
            System.Array.Copy(other.Points, Points, Count);
            System.Array.Copy(other.WorldPoints, WorldPoints, Count);
            HasWorld = other.HasWorld;
            IsPresent = other.IsPresent;
            Blendshapes = other.Blendshapes;
            HasBlendshapes = other.HasBlendshapes;
            HeadEuler = other.HeadEuler;
            HasHeadPose = other.HasHeadPose;
        }

        public void Clear()
        {
            IsPresent = false;
            HasBlendshapes = false;
            HasHeadPose = false;
            Blendshapes = default;
        }
    }

    /// <summary>
    /// 33 MediaPipe Pose landmarks in normalized image space (x, y in [0, 1], y down). The word model reads
    /// the nose (0), shoulders (11, 12), elbows (13, 14), wrists (15 = left, 16 = right) and hips (23, 24).
    /// </summary>
    public sealed class PoseLandmarks
    {
        public const int Count = 33;
        public const int LeftWrist = 15, RightWrist = 16;

        public readonly Vector3[] Points = new Vector3[Count];
        public bool IsPresent;

        public void CopyFrom(PoseLandmarks other)
        {
            System.Array.Copy(other.Points, Points, Count);
            IsPresent = other.IsPresent;
        }

        public void Clear()
        {
            IsPresent = false;
        }
    }

    /// <summary>
    /// One camera frame worth of landmarks. Instances are pooled and reused by providers and
    /// buffers, so consumers must copy what they keep.
    /// Hand slots follow the landmarker's handedness labels after the provider's own correction.
    /// </summary>
    public sealed class LandmarkFrame
    {
        public double TimestampSeconds;

        /// <summary>Source image width / height. Normalized x is multiplied by this to make space isotropic.</summary>
        public float ImageAspect = 1f;

        public readonly HandLandmarks LeftHand = new HandLandmarks();
        public readonly HandLandmarks RightHand = new HandLandmarks();
        public readonly FaceLandmarks Face = new FaceLandmarks();
        public readonly PoseLandmarks Pose = new PoseLandmarks();

        public HandLandmarks GetHand(HandSide side) => side == HandSide.Left ? LeftHand : RightHand;

        public bool AnyHandPresent => LeftHand.IsPresent || RightHand.IsPresent;

        public void CopyFrom(LandmarkFrame other)
        {
            TimestampSeconds = other.TimestampSeconds;
            ImageAspect = other.ImageAspect;
            LeftHand.CopyFrom(other.LeftHand);
            RightHand.CopyFrom(other.RightHand);
            Face.CopyFrom(other.Face);
            Pose.CopyFrom(other.Pose);
        }

        public void Clear()
        {
            LeftHand.Clear();
            RightHand.Clear();
            Face.Clear();
            Pose.Clear();
        }
    }
}
