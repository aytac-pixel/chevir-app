using System;
using Cevir.Core.LanguagePacks;
using UnityEngine;

namespace Cevir.Core.Recognition
{
    /// <summary>
    /// Source of live landmarks. Core never references MediaPipe directly; the MediaPipe bridge
    /// (Assets/Integrations/MediaPipe) implements this, and tests or editor tools can supply their own.
    /// </summary>
    public interface ILandmarkProvider
    {
        bool IsRunning { get; }

        /// <summary>
        /// Raised on the Unity main thread. The frame is reused after the handler returns,
        /// so copy anything you keep.
        /// </summary>
        event Action<LandmarkFrame> FrameReady;

        void StartCapture();
        void StopCapture();
    }

    /// <summary>Providers that can use a pack's landmarker model overrides implement this too.</summary>
    public interface IPackAwareLandmarkProvider
    {
        void ApplyPack(ILanguagePack pack);
    }

    /// <summary>
    /// Providers that already read the camera picture on the CPU can share it: <c>UprightFrame(pixels, width, height)</c>
    /// with rows top-down, upright as the learner holds the phone and not mirrored. Raised on the main thread; the
    /// buffer is reused, so copy what you keep. Used to save short sample videos (only with the learner's permission).
    /// </summary>
    public interface ICameraFrameSource
    {
        event Action<Color32[], int, int> UprightFrame;
    }

    /// <summary>Optional: a one-line live status of the recognizer (frames, results, hands, last error) for testing.</summary>
    public interface IRecognitionDiagnostics
    {
        string DiagnosticText { get; }
    }

    /// <summary>Providers that own a camera expose it for the lesson screen's preview.</summary>
    public interface ICameraPreviewSource
    {
        /// <summary>Live camera texture, or null before the camera has started.</summary>
        Texture PreviewTexture { get; }

        /// <summary>Clockwise degrees the texture must be rotated to appear upright.</summary>
        int PreviewRotation { get; }

        /// <summary>True when the texture's rows are bottom-up relative to the screen.</summary>
        bool PreviewVerticallyFlipped { get; }

        /// <summary>True for front cameras, so the preview behaves like a mirror (what learners expect).</summary>
        bool PreviewMirrored { get; }

        /// <summary>True when the learner refused camera access; the UI then explains how to allow it.</summary>
        bool CameraDenied { get; }
    }
}
