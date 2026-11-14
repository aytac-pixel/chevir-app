using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

namespace Cevir.Core.LanguagePacks
{
    public enum SignKind
    {
        Fingerspelling,
        Number,
        Word,
        Phrase,
        Sentence
    }

    /// <summary>
    /// One sign in a language pack's dictionary: what to show (clips) and what to check (references + rules).
    /// </summary>
    [CreateAssetMenu(menuName = "Çevir/Language Pack/Sign Definition", fileName = "Sign_")]
    public sealed class SignDefinition : ScriptableObject
    {
        [Tooltip("Stable id used in save data and reference files, e.g. 'azsld.letter.a' or 'azsld.word.salam'.")]
        [SerializeField] private string signId;

        [Tooltip("Written form shown to the learner (in the UI language).")]
        [SerializeField] private string gloss;

        [SerializeField] private SignKind kind = SignKind.Word;

        [Tooltip("Dictionary category, e.g. 'salamlaşma', 'ailə', 'rənglər'.")]
        [SerializeField] private string category;

        [Tooltip("Signs in the same group are used as distractors in 'İşarəni Tap' (similar-looking signs).")]
        [SerializeField] private string similarityGroup;

        [SerializeField] private bool twoHanded;

        [Tooltip("Static signs (most fingerspelled letters) are scored on handshape and location only.")]
        [SerializeField] private bool dynamic = true;

        [Header("Media")]
        [SerializeField] private VideoClip referenceClip;
        [SerializeField] private VideoClip slowClip;
        [SerializeField] private Sprite thumbnail;

        [Header("Recognition")]
        [Tooltip("Reference recordings from different signers. The best-matching one is used.")]
        [SerializeField] private List<SignReference> references = new List<SignReference>();

        [Tooltip("Optional face/head grammar for this sign. Leave empty for manual-only signs.")]
        [SerializeField] private NonManualRule nonManualRule;

        public string SignId => signId;
        public string Gloss => gloss;
        public SignKind Kind => kind;
        public string Category => category;
        public string SimilarityGroup => similarityGroup;
        public bool TwoHanded => twoHanded;
        public bool Dynamic => dynamic;
        public VideoClip ReferenceClip => referenceClip;
        public VideoClip SlowClip => slowClip != null ? slowClip : referenceClip;
        public Sprite Thumbnail => thumbnail;
        public IReadOnlyList<SignReference> References => references;
        public NonManualRule NonManualRule => nonManualRule;

        public bool HasRecognitionData
        {
            get
            {
                for (int i = 0; i < references.Count; i++)
                {
                    if (references[i] != null && references[i].IsValid) return true;
                }

                return false;
            }
        }

#if UNITY_EDITOR
        public void EditorInitialize(string id, string displayGloss, SignKind signKind, bool isDynamic, bool isTwoHanded, string signCategory)
        {
            signId = id;
            gloss = displayGloss;
            kind = signKind;
            dynamic = isDynamic;
            twoHanded = isTwoHanded;
            category = signCategory;
        }

        /// <summary>Assigns media found by the media importer. Null keeps the current value.</summary>
        public void EditorSetMedia(VideoClip clip, VideoClip slow, Sprite picture)
        {
            if (clip != null) referenceClip = clip;
            if (slow != null) slowClip = slow;
            if (picture != null) thumbnail = picture;
        }

        public void EditorAddReference(SignReference reference)
        {
            if (!references.Contains(reference)) references.Add(reference);
        }
#endif
    }
}
