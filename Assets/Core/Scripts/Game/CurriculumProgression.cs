using System.Collections.Generic;
using Cevir.Core.Curriculum;
using Cevir.Core.LanguagePacks;

namespace Cevir.Core.Game
{
    public enum NodeState
    {
        /// <summary>Not reachable yet.</summary>
        Locked,

        /// <summary>Reachable but not the recommended next step (tested out, or an early gate attempt).</summary>
        Unlocked,

        /// <summary>The learner's next step; highlighted on the map.</summary>
        Active,

        Completed
    }

    public enum PathNodeKind
    {
        Lesson,
        GateTest
    }

    public sealed class PathNode
    {
        public PathNodeKind Kind;
        public LevelData Level;
        public UnitData Unit;
        public LessonData Lesson;
        public NodeState State;

        /// <summary>Position along the whole path, used for the winding layout.</summary>
        public int PathIndex;

        /// <summary>Position inside its unit (lessons) or -1 (gate).</summary>
        public int IndexInUnit;

        public string Id => Kind == PathNodeKind.GateTest ? Level.GateTestId : Lesson.Id;
    }

    /// <summary>
    /// Derives map node states from progress. Rules: lessons unlock in order; each level is closed by a
    /// gate test, and the next level opens only when that gate is passed. Passing a gate early
    /// ("test out") leaves the skipped lessons Unlocked.
    /// </summary>
    public sealed class CurriculumProgression
    {
        public List<PathNode> Build(ILanguagePack pack, PackProgress progress)
        {
            var nodes = new List<PathNode>();
            bool levelOpen = true;
            bool activeAssigned = false;
            int pathIndex = 0;

            foreach (LevelData level in pack.Levels)
            {
                if (level == null) continue;
                bool gatePassed = progress.IsGatePassed(level.GateTestId);
                bool allLessonsDone = true;

                foreach (UnitData unit in level.Units)
                {
                    if (unit == null) continue;
                    for (int i = 0; i < unit.Lessons.Count; i++)
                    {
                        LessonData lesson = unit.Lessons[i];
                        if (lesson == null) continue;

                        NodeState state;
                        bool completed = progress.IsLessonCompleted(lesson.Id);
                        if (!levelOpen) state = NodeState.Locked;
                        else if (completed) state = NodeState.Completed;
                        else if (gatePassed) state = NodeState.Unlocked;
                        else if (!activeAssigned)
                        {
                            state = NodeState.Active;
                            activeAssigned = true;
                        }
                        else state = NodeState.Locked;

                        allLessonsDone &= completed;
                        nodes.Add(new PathNode
                        {
                            Kind = PathNodeKind.Lesson,
                            Level = level,
                            Unit = unit,
                            Lesson = lesson,
                            State = state,
                            PathIndex = pathIndex++,
                            IndexInUnit = i
                        });
                    }
                }

                NodeState gateState;
                if (!levelOpen) gateState = NodeState.Locked;
                else if (gatePassed) gateState = NodeState.Completed;
                else if (allLessonsDone && !activeAssigned)
                {
                    gateState = NodeState.Active;
                    activeAssigned = true;
                }
                else gateState = level.GateTest.allowTestOut ? NodeState.Unlocked : NodeState.Locked;

                nodes.Add(new PathNode
                {
                    Kind = PathNodeKind.GateTest,
                    Level = level,
                    State = gateState,
                    PathIndex = pathIndex++,
                    IndexInUnit = -1
                });

                levelOpen = levelOpen && gatePassed;
            }

            return nodes;
        }

        public static bool CanStart(PathNode node)
        {
            return node != null && node.State != NodeState.Locked;
        }
    }
}
