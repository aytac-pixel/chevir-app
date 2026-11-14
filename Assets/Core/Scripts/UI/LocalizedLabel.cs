using Cevir.Core.Localization;
using TMPro;
using UnityEngine;

namespace Cevir.Core.UI
{
    /// <summary>Static label text in English and Azerbaijani; follows <see cref="Loc.LanguageChanged"/>.</summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class LocalizedLabel : MonoBehaviour
    {
        [SerializeField] private string en;
        [SerializeField] private string az;

        private TextMeshProUGUI _label;

        private void OnEnable()
        {
            Loc.LanguageChanged += OnLanguageChanged;
            Apply();
        }

        private void OnDisable()
        {
            Loc.LanguageChanged -= OnLanguageChanged;
        }

        public void Set(string english, string azerbaijani)
        {
            en = english;
            az = azerbaijani;
            Apply();
        }

        private void OnLanguageChanged(string language) => Apply();

        private void Apply()
        {
            if (_label == null) _label = GetComponent<TextMeshProUGUI>();
            if (_label != null) _label.text = Loc.Pick(en, az);
        }
    }
}
