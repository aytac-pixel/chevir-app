using System.Collections.Generic;
using UnityEngine;

namespace Cevir.Core.LanguagePacks
{
    /// <summary>
    /// Lists the packs available to the app. Built-in packs are referenced here; downloadable packs
    /// (planned: Addressables remote catalog) register at runtime through <see cref="Register"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Chevir/Language Pack Registry", fileName = "LanguagePackRegistry")]
    public sealed class LanguagePackRegistry : ScriptableObject
    {
        [SerializeField] private List<LanguagePackDefinition> builtInPacks = new List<LanguagePackDefinition>();
        [SerializeField] private string defaultLanguageCode = "azsld";

        private readonly List<ILanguagePack> _runtimePacks = new List<ILanguagePack>();

        public string DefaultLanguageCode => defaultLanguageCode;

        public IEnumerable<ILanguagePack> All
        {
            get
            {
                foreach (LanguagePackDefinition pack in builtInPacks)
                {
                    if (pack != null) yield return pack;
                }

                foreach (ILanguagePack pack in _runtimePacks)
                {
                    yield return pack;
                }
            }
        }

        public IEnumerable<ILanguagePack> Playable
        {
            get
            {
                foreach (ILanguagePack pack in All)
                {
                    if (pack.IsPlayable) yield return pack;
                }
            }
        }

        public ILanguagePack Find(string languageCode)
        {
            foreach (ILanguagePack pack in All)
            {
                if (string.Equals(pack.LanguageCode, languageCode, System.StringComparison.OrdinalIgnoreCase))
                {
                    return pack;
                }
            }

            return null;
        }

        public void Register(ILanguagePack pack)
        {
            if (pack == null || Find(pack.LanguageCode) != null)
            {
                return;
            }

            _runtimePacks.Add(pack);
        }

        private void OnDisable()
        {
            _runtimePacks.Clear();
        }
    }
}
