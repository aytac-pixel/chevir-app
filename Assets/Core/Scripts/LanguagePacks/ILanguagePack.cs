using System.Collections.Generic;
using Cevir.Core.Curriculum;
using Cevir.Core.Recognition;
using Cevir.Core.Stories;
using UnityEngine;

namespace Cevir.Core.LanguagePacks
{
    /// <summary>
    /// The only surface Core uses to talk to a sign language. AzSLD, RSL and future packs all implement
    /// this, so adding a language never changes Core code.
    /// </summary>
    public interface ILanguagePack
    {
        string LanguageCode { get; }
        LanguagePackManifest Manifest { get; }

        /// <summary>Curriculum, ordered (for AzSLD: A1 first).</summary>
        IReadOnlyList<LevelData> Levels { get; }

        /// <summary>First-person practice stories, unlocked by finishing units. May be empty.</summary>
        IReadOnlyList<StoryData> Stories { get; }

        SignDictionary Dictionary { get; }

        /// <summary>Per-language thresholds for the matcher (signing speed and handshape inventories differ).</summary>
        SignMatcherConfig MatcherConfig { get; }

        /// <summary>Pack's own sign recognition model bytes (.tflite / .onnx renamed to .bytes), or null.</summary>
        TextAsset RecognitionModel { get; }

        /// <summary>Optional override of the MediaPipe hand/face landmarker models; null uses the app defaults.</summary>
        TextAsset HandLandmarkerModelOverride { get; }
        TextAsset FaceLandmarkerModelOverride { get; }

        /// <summary>False for structural placeholders (e.g. RSL before content lands); hidden from the picker.</summary>
        bool IsPlayable { get; }

        /// <summary>Creates the learned classifier for this pack, or null to use template matching only.</summary>
        ISignClassifier CreateClassifier();

        /// <summary>Lets a pack adjust a result with language-specific knowledge before it is shown.</summary>
        MatchResult PostProcess(MatchResult result, SignDefinition target);
    }
}
