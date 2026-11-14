using System.Collections.Generic;
using Cevir.Core.Curriculum;
using Cevir.Core.Game;
using Cevir.Core.LanguagePacks;

namespace Cevir.Core.Stories
{
    public enum StoryState
    {
        Locked,
        Unlocked,
        Completed
    }

    public sealed class StoryEntry
    {
        public StoryData Story;
        public StoryState State;

        /// <summary>Unlocked, not finished and not announced to the learner yet.</summary>
        public bool IsNew;
    }

    /// <summary>
    /// Derives story states from pack progress. A story unlocks when every lesson of its unit is completed, or when
    /// the unit's level gate was passed (tested out). Pure C#, so it is unit-testable.
    /// </summary>
    public static class StoryProgression
    {
        public static List<StoryEntry> Build(ILanguagePack pack, PackProgress progress)
        {
            var entries = new List<StoryEntry>();
            if (pack == null || pack.Stories == null) return entries;

            foreach (StoryData story in pack.Stories)
            {
                if (story == null) continue;
                StoryState state;
                if (progress.IsStoryCompleted(story.Id)) state = StoryState.Completed;
                else if (IsUnlocked(story, pack, progress)) state = StoryState.Unlocked;
                else state = StoryState.Locked;

                entries.Add(new StoryEntry
                {
                    Story = story,
                    State = state,
                    IsNew = state == StoryState.Unlocked && !progress.IsStoryAnnounced(story.Id)
                });
            }

            entries.Sort((a, b) => a.Story.Number.CompareTo(b.Story.Number));
            return entries;
        }

        public static bool IsUnlocked(StoryData story, ILanguagePack pack, PackProgress progress)
        {
            if (story.UnlockUnit == null) return true;
            return IsUnitComplete(story.UnlockUnit, FindLevel(pack, story.UnlockUnit), progress);
        }

        public static bool IsUnitComplete(UnitData unit, LevelData level, PackProgress progress)
        {
            if (level != null && progress.IsGatePassed(level.GateTestId)) return true;
            if (unit.IsCheckpoint) return false; // a checkpoint unit is finished by its gate test only

            int counted = 0;
            foreach (LessonData lesson in unit.Lessons)
            {
                if (lesson == null) continue;
                if (!progress.IsLessonCompleted(lesson.Id)) return false;
                counted++;
            }

            return counted > 0;
        }

        public static LevelData FindLevel(ILanguagePack pack, UnitData unit)
        {
            if (pack == null) return null;
            foreach (LevelData level in pack.Levels)
            {
                if (level == null) continue;
                foreach (UnitData candidate in level.Units)
                {
                    if (candidate == unit) return level;
                }
            }

            return null;
        }

        /// <summary>Stories that are newly unlocked (not announced yet), lowest number first.</summary>
        public static List<StoryData> NewlyUnlocked(ILanguagePack pack, PackProgress progress)
        {
            var result = new List<StoryData>();
            foreach (StoryEntry entry in Build(pack, progress))
            {
                if (entry.IsNew && entry.Story.IsPlayable) result.Add(entry.Story);
            }

            return result;
        }
    }
}
