using System.Collections.Generic;
using UnityEngine;

namespace Cevir.Core.Curriculum
{
    /// <summary>A themed group of lessons, shown on the map as one section with its own header and colour.</summary>
    [CreateAssetMenu(menuName = "Chevir/Curriculum/Unit", fileName = "Unit_")]
    public sealed class UnitData : CurriculumAsset
    {
        [SerializeField] private Color themeColor = new Color(0.345f, 0.8f, 0.008f);

        [TextArea(3, 10)]
        [Tooltip("Short guidebook shown from the unit header: tips, grammar notes, cultural context.")]
        [SerializeField] private string guidebook;

        [SerializeField] private List<LessonData> lessons = new List<LessonData>();

        [Tooltip("The level's closing unit (review + gate test). The home path draws it as the trophy checkpoint " +
                 "and it counts as complete only once the level's gate test is passed.")]
        [SerializeField] private bool checkpoint;

        public Color ThemeColor => themeColor;
        public string Guidebook => guidebook;
        public IReadOnlyList<LessonData> Lessons => lessons;
        public bool IsCheckpoint => checkpoint;

#if UNITY_EDITOR
        public void EditorSetContent(Color color, List<LessonData> lessonList)
        {
            themeColor = color;
            lessons = lessonList;
        }

        public void EditorSetCheckpoint(bool isCheckpoint, string guidebookText)
        {
            checkpoint = isCheckpoint;
            if (!string.IsNullOrEmpty(guidebookText)) guidebook = guidebookText;
        }
#endif
    }
}
