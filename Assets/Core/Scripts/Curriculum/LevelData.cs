using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cevir.Core.Curriculum
{
    /// <summary>
    /// The four curriculum modules. Values are the module order; packs for other languages reuse them
    /// so the map, gate tests and analytics stay language-agnostic.
    /// </summary>
    public enum CurriculumLevel
    {
        /// <summary>1. Barmaq İzləri: fingerspelled alphabet and numbers.</summary>
        Daktil = 1,

        /// <summary>2. Təməl İşarələr: greetings, thanks, family, colours, emergencies, core words.</summary>
        Temel = 2,

        /// <summary>3. Cümlə və İfadə: syntax, questions, negation, non-manual grammar.</summary>
        Cumle = 3,

        /// <summary>4. Səlis Ünsiyyət: real dialogues (doctor, shop, transport, school, work).</summary>
        Selis = 4
    }

    public enum GateTestType
    {
        /// <summary>Every target sign checked on camera; pass when the recognized ratio reaches the threshold.</summary>
        CameraAccuracy,

        /// <summary>Random sample of the level's vocabulary (e.g. 20 of 150 words).</summary>
        RandomVocabularySample,

        /// <summary>A fixed sequence of sentences, manual and non-manual both required.</summary>
        SentenceSequence,

        /// <summary>Timed free dialogue with the avatar.</summary>
        AvatarDialogue
    }

    [Serializable]
    public sealed class GateTestConfig
    {
        public GateTestType type = GateTestType.CameraAccuracy;

        [Tooltip("Questions asked. 0 = every sign in the level.")]
        [Min(0)] public int questionCount;

        [Range(0f, 1f)] public float passRatio = 0.8f;

        public bool requireNonManual;

        [Tooltip("Only for AvatarDialogue.")]
        [Min(0)] public float durationSeconds;

        [Tooltip("Allows learners to take the gate early and skip the level ('test out').")]
        public bool allowTestOut = true;
    }

    /// <summary>A curriculum module, closed by a gate test that unlocks the next level.</summary>
    [CreateAssetMenu(menuName = "Çevir/Curriculum/Level", fileName = "Level_")]
    public sealed class LevelData : CurriculumAsset
    {
        [SerializeField] private CurriculumLevel level = CurriculumLevel.Daktil;
        [SerializeField] private Color themeColor = new Color(0.345f, 0.8f, 0.008f);
        [SerializeField] private List<UnitData> units = new List<UnitData>();
        [SerializeField] private GateTestConfig gateTest = new GateTestConfig();

        [Tooltip("Grammar levels (Cümlə) must not ship lessons whose rules are not approved by linguists.")]
        [SerializeField] private bool requiresLinguistApproval;

        public CurriculumLevel Level => level;
        public Color ThemeColor => themeColor;
        public IReadOnlyList<UnitData> Units => units;
        public GateTestConfig GateTest => gateTest;
        public bool RequiresLinguistApproval => requiresLinguistApproval;

        public string GateTestId => Id + ".gate";

        public IEnumerable<LessonData> AllLessons()
        {
            foreach (UnitData unit in units)
            {
                if (unit == null) continue;
                foreach (LessonData lesson in unit.Lessons)
                {
                    if (lesson != null) yield return lesson;
                }
            }
        }

#if UNITY_EDITOR
        public void EditorSetContent(CurriculumLevel levelType, Color color, GateTestConfig gate, bool linguistApproval, List<UnitData> unitList)
        {
            level = levelType;
            themeColor = color;
            gateTest = gate;
            requiresLinguistApproval = linguistApproval;
            units = unitList;
        }
#endif
    }
}
