using System.Collections.Generic;
using Cevir.Core.Localization;
using UnityEngine;

namespace Cevir.Core.Curriculum
{
    /// <summary>
    /// Base for curriculum ScriptableObjects. The id is what save data stores, so lessons can be
    /// renamed, reordered or moved between units without losing learner progress.
    /// </summary>
    public abstract class CurriculumAsset : ScriptableObject
    {
        [Tooltip("Stable id, e.g. 'azsld.a1.u2.les3'. Never change it after release.")]
        [SerializeField] private string id;

        [Tooltip("Title in the default interface language (English).")]
        [SerializeField] private string title;

        [TextArea]
        [SerializeField] private string description;

        [Tooltip("Titles and descriptions for other interface languages, e.g. 'az'.")]
        [SerializeField] private List<LocalizedEntry> translations = new List<LocalizedEntry>();

        [SerializeField] private Sprite icon;

        public string Id => id;
        public Sprite Icon => icon;

        /// <summary>Title in the current interface language, falling back to the default title.</summary>
        public string Title
        {
            get
            {
                LocalizedEntry entry;
                return LocalizedEntries.TryFind(translations, out entry) && !string.IsNullOrEmpty(entry.title) ? entry.title : title;
            }
        }

        public string Description
        {
            get
            {
                LocalizedEntry entry;
                return LocalizedEntries.TryFind(translations, out entry) && !string.IsNullOrEmpty(entry.description) ? entry.description : description;
            }
        }

        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(id))
            {
                id = System.Guid.NewGuid().ToString("N");
            }
        }

#if UNITY_EDITOR
        public void EditorSetIdentity(string newId, string newTitle, string newDescription)
        {
            id = newId;
            title = newTitle;
            description = newDescription;
        }

        public void EditorSetTranslation(string language, string localizedTitle, string localizedDescription)
        {
            if (translations == null) translations = new List<LocalizedEntry>();
            translations.RemoveAll(t => t.language == language);
            translations.Add(new LocalizedEntry { language = language, title = localizedTitle, description = localizedDescription });
        }
#endif
    }
}
