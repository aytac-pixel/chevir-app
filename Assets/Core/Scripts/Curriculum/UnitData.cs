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

        [Tooltip("Pixel icon on the unit's tile on the home path (white art, tinted grey while locked).")]
        [SerializeField] private Sprite icon;

        public Color ThemeColor => themeColor;
        public string Guidebook => guidebook;
        public IReadOnlyList<LessonData> Lessons => lessons;
        public bool IsCheckpoint => checkpoint;
        [Tooltip("Pixel picture that fills the unit's tile on the home path.")]
        [SerializeField] private Sprite picture;

        [Tooltip("Blurred, paler copy of the picture shown while the unit is locked (a padlock sits on top).")]
        [SerializeField] private Sprite lockedPicture;

        public Sprite Icon => icon;
        public Sprite Picture => picture;
        public Sprite LockedPicture => lockedPicture;

#if UNITY_EDITOR
        public void EditorSetIcon(Sprite unitIcon)
        {
            icon = unitIcon;
        }

        public void EditorSetPictures(Sprite unitPicture, Sprite lockedUnitPicture)
        {
            picture = unitPicture;
            lockedPicture = lockedUnitPicture;
        }

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
