using System.Collections.Generic;
using Cevir.Core.Curriculum;

namespace Cevir.Core.Game
{
    /// <summary>One node on the home path: a whole unit (its lessons are played from the node's popup).</summary>
    public sealed class UnitPathEntry
    {
        public LevelData Level;
        public UnitData Unit;

        /// <summary>1-based number inside the level ("UNIT 2").</summary>
        public int Number;

        public NodeState State;
        public int LessonsCompleted;
        public int LessonsTotal;

        /// <summary>Level checkpoint (review + gate test), drawn as the trophy.</summary>
        public bool IsCheckpoint;

        /// <summary>What START plays: the active node, else the first open one, else the first lesson (replay).</summary>
        public PathNode NextNode;

        public readonly List<PathNode> Nodes = new List<PathNode>();
    }

    public sealed class LevelPathSummary
    {
        public LevelData Level;
        public readonly List<UnitPathEntry> Units = new List<UnitPathEntry>();

        public int CompletedUnits
        {
            get
            {
                int n = 0;
                foreach (UnitPathEntry u in Units) if (u.State == NodeState.Completed) n++;
                return n;
            }
        }

        public bool IsCurrent
        {
            get
            {
                foreach (UnitPathEntry u in Units) if (u.State == NodeState.Active) return true;
                return false;
            }
        }
    }

    /// <summary>
    /// Groups the lesson path (<see cref="CurriculumProgression"/>) into unit nodes for the home screen. A unit is
    /// Completed when all its lessons are, Active when it holds the learner's next step, Unlocked when it is
    /// reachable but not next (tested out), else Locked. The level's gate test belongs to its checkpoint unit
    /// (or forms its own checkpoint node when the level has none).
    /// </summary>
    public static class UnitPathBuilder
    {
        public static List<LevelPathSummary> Build(IReadOnlyList<PathNode> path)
        {
            var levels = new List<LevelPathSummary>();
            LevelPathSummary current = null;
            UnitPathEntry unitEntry = null;

            foreach (PathNode node in path)
            {
                if (current == null || current.Level != node.Level)
                {
                    current = new LevelPathSummary { Level = node.Level };
                    levels.Add(current);
                    unitEntry = null;
                }

                if (node.Kind == PathNodeKind.GateTest)
                {
                    UnitPathEntry checkpoint = FindCheckpoint(current);
                    if (checkpoint == null)
                    {
                        checkpoint = new UnitPathEntry { Level = node.Level, Unit = null, Number = current.Units.Count + 1, IsCheckpoint = true };
                        current.Units.Add(checkpoint);
                    }

                    checkpoint.Nodes.Add(node);
                    continue;
                }

                if (unitEntry == null || unitEntry.Unit != node.Unit)
                {
                    unitEntry = new UnitPathEntry
                    {
                        Level = node.Level,
                        Unit = node.Unit,
                        Number = current.Units.Count + 1,
                        IsCheckpoint = node.Unit != null && node.Unit.IsCheckpoint
                    };
                    current.Units.Add(unitEntry);
                }

                unitEntry.Nodes.Add(node);
            }

            foreach (LevelPathSummary level in levels)
            {
                foreach (UnitPathEntry entry in level.Units) Resolve(entry);
            }

            return levels;
        }

        /// <summary>The level holding the learner's next step, else the last level that has any progress, else the first.</summary>
        public static LevelPathSummary CurrentLevel(List<LevelPathSummary> levels)
        {
            if (levels == null || levels.Count == 0) return null;
            foreach (LevelPathSummary level in levels)
            {
                if (level.IsCurrent) return level;
            }

            for (int i = levels.Count - 1; i >= 0; i--)
            {
                if (levels[i].CompletedUnits > 0) return levels[i];
            }

            return levels[0];
        }

        private static UnitPathEntry FindCheckpoint(LevelPathSummary level)
        {
            for (int i = level.Units.Count - 1; i >= 0; i--)
            {
                if (level.Units[i].IsCheckpoint) return level.Units[i];
            }

            return null;
        }

        private static void Resolve(UnitPathEntry entry)
        {
            bool anyActive = false, anyOpen = false, allLessonsDone = true, gateDone = false, hasGate = false;
            PathNode active = null, firstOpen = null, first = null;

            foreach (PathNode node in entry.Nodes)
            {
                if (first == null) first = node;
                if (node.Kind == PathNodeKind.GateTest)
                {
                    hasGate = true;
                    gateDone = node.State == NodeState.Completed;
                }
                else
                {
                    entry.LessonsTotal++;
                    if (node.State == NodeState.Completed) entry.LessonsCompleted++;
                    else allLessonsDone = false;
                }

                if (node.State == NodeState.Active && active == null)
                {
                    active = node;
                    anyActive = true;
                }

                if (node.State != NodeState.Locked)
                {
                    anyOpen = true;
                    if (node.State != NodeState.Completed && firstOpen == null) firstOpen = node;
                }
            }

            bool completed = entry.IsCheckpoint && hasGate ? gateDone : allLessonsDone && entry.LessonsTotal > 0;
            if (completed) entry.State = NodeState.Completed;
            else if (anyActive) entry.State = NodeState.Active;
            else if (anyOpen) entry.State = NodeState.Unlocked;
            else entry.State = NodeState.Locked;

            entry.NextNode = active ?? firstOpen ?? (entry.State == NodeState.Locked ? null : first);
        }
    }
}
