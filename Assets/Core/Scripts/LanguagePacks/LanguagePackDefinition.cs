using System.Collections.Generic;
using Cevir.Core.Curriculum;
using Cevir.Core.Recognition;
using Cevir.Core.Stories;
using UnityEngine;

namespace Cevir.Core.LanguagePacks
{
    /// <summary>
    /// ScriptableObject base for language packs. Each pack lives in Assets/LanguagePacks/&lt;Code&gt;/ and
    /// subclasses this only to supply language-specific defaults and hooks.
    /// </summary>
    public abstract class LanguagePackDefinition : ScriptableObject, ILanguagePack
    {
        [SerializeField] protected LanguagePackManifest manifest = new LanguagePackManifest();
        [SerializeField] protected List<LevelData> levels = new List<LevelData>();
        [SerializeField] protected List<StoryData> stories = new List<StoryData>();
        [SerializeField] protected SignDictionary dictionary;
        [SerializeField] protected SignMatcherConfig matcherConfig;

        [Header("Models")]
        [SerializeField] protected TextAsset recognitionModel;
        [SerializeField] protected TextAsset handLandmarkerModelOverride;
        [SerializeField] protected TextAsset faceLandmarkerModelOverride;

        public string LanguageCode => manifest.languageCode;
        public LanguagePackManifest Manifest => manifest;
        public IReadOnlyList<LevelData> Levels => levels;
        public IReadOnlyList<StoryData> Stories => stories;
        public SignDictionary Dictionary => dictionary;
        public SignMatcherConfig MatcherConfig => matcherConfig;
        public TextAsset RecognitionModel => recognitionModel;
        public TextAsset HandLandmarkerModelOverride => handLandmarkerModelOverride;
        public TextAsset FaceLandmarkerModelOverride => faceLandmarkerModelOverride;

        public virtual bool IsPlayable => levels.Count > 0 && dictionary != null && dictionary.Signs.Count > 0;

        public virtual ISignClassifier CreateClassifier() => null;

        public virtual MatchResult PostProcess(MatchResult result, SignDefinition target) => result;

        /// <summary>Language defaults applied when the asset is created or reset in the inspector.</summary>
        protected abstract void ApplyDefaults(LanguagePackManifest target);

        protected virtual void Reset()
        {
            manifest = new LanguagePackManifest();
            ApplyDefaults(manifest);
        }

        protected virtual void OnValidate()
        {
            if (dictionary != null)
            {
                manifest.signCount = dictionary.Signs.Count;
            }
        }

#if UNITY_EDITOR
        public void EditorSetContent(List<LevelData> levelList, SignDictionary signDictionary, SignMatcherConfig config)
        {
            levels = levelList;
            dictionary = signDictionary;
            matcherConfig = config;
            if (string.IsNullOrEmpty(manifest.languageCode) || manifest.alphabet.Count == 0)
            {
                ApplyDefaults(manifest);
            }

            OnValidate();
        }

        public void EditorSetStories(List<StoryData> storyList)
        {
            stories = storyList;
        }
#endif
    }
}
