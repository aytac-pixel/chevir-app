using System;
using System.Collections.Generic;
using Cevir.Core.Recognition;
using UnityEngine;

namespace Cevir.Core.LanguagePacks
{
    public enum NonManualComparison
    {
        /// <summary>Peak value must reach the threshold (e.g. brows raised for a yes/no question).</summary>
        AtLeast,

        /// <summary>Peak value must stay under the threshold.</summary>
        AtMost,

        /// <summary>Absolute peak (either direction) must reach the threshold, for head tilt/shake.</summary>
        AbsoluteAtLeast
    }

    [Serializable]
    public struct NonManualRequirement
    {
        public NonManualFeature feature;
        public NonManualComparison comparison;
        public float threshold;
    }

    /// <summary>
    /// Non-manual grammar for a sign or sentence type: brow raise, mouth shape, head tilt and so on.
    /// The project plan requires these to be written by sign-language linguists and Deaf community
    /// members, so a rule is only enforced once it is marked approved.
    /// </summary>
    [CreateAssetMenu(menuName = "Chevir/Language Pack/Non-Manual Rule", fileName = "NMRule_")]
    public sealed class NonManualRule : ScriptableObject
    {
        [SerializeField] private string ruleId;

        [TextArea]
        [SerializeField] private string description;

        [SerializeField] private List<NonManualRequirement> requirements = new List<NonManualRequirement>();

        [Tooltip("Set only after review by the language's linguists / Deaf community advisers.")]
        [SerializeField] private bool approved;

        [SerializeField] private string reviewedBy;

        public string RuleId => ruleId;
        public string Description => description;
        public IReadOnlyList<NonManualRequirement> Requirements => requirements;
        public bool Approved => approved;
        public string ReviewedBy => reviewedBy;
    }
}
