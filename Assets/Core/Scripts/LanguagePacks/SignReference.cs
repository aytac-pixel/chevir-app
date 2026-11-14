using System.Collections.Generic;
using Cevir.Core.Recognition;
using UnityEngine;

namespace Cevir.Core.LanguagePacks
{
    /// <summary>
    /// A reference performance of a sign as feature frames, produced offline from dataset videos
    /// (AzSLD) or the app's own recordings. Videos never ship for recognition; only these features do.
    /// </summary>
    [CreateAssetMenu(menuName = "Çevir/Language Pack/Sign Reference", fileName = "Ref_")]
    public sealed class SignReference : ScriptableObject
    {
        [SerializeField] private string signId;

        [Tooltip("Dataset provenance, e.g. 'AzSLD/word/0412.mp4'. Needed for licence tracking.")]
        [SerializeField] private string source;

        [SerializeField] private string signerId;

        [SerializeField] private List<SignFeatureFrame> frames = new List<SignFeatureFrame>();

        public string SignId => signId;
        public string Source => source;
        public string SignerId => signerId;
        public IReadOnlyList<SignFeatureFrame> Frames => frames;

        public bool IsValid => frames != null && frames.Count >= 2;

        public void Initialize(string id, string sourcePath, string signer, List<SignFeatureFrame> featureFrames)
        {
            signId = id;
            source = sourcePath;
            signerId = signer;
            frames = featureFrames ?? new List<SignFeatureFrame>();
        }
    }
}
