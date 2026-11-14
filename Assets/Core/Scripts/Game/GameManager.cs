using System;
using System.Collections.Generic;
using Cevir.Core.Curriculum;
using Cevir.Core.Gamification;
using Cevir.Core.LanguagePacks;
using Cevir.Core.Lesson;
using Cevir.Core.Localization;
using Cevir.Core.Social;
using Cevir.Core.Stories;
using UnityEngine;

namespace Cevir.Core.Game
{
    public enum GameState
    {
        Boot,
        Map,
        Lesson,
        Results,
        Story
    }

    public struct LessonReward
    {
        public bool Completed;
        public bool Passed;
        public bool FirstCompletion;
        public int Xp;
        public bool PerfectBonus;
        public bool StreakIncreased;
        public int HeartsRestored;
        public float Accuracy;

        /// <summary>A story this completion unlocked (announced once), or null.</summary>
        public StoryData UnlockedStory;
    }

    public struct StoryReward
    {
        public bool Completed;
        public bool FirstCompletion;
        public int Xp;
        public bool PerfectBonus;
        public bool StreakIncreased;
        public float Accuracy;
    }

    /// <summary>
    /// App root: owns progress, the gamification systems and the active language pack, and runs the
    /// lesson lifecycle. UI and gameplay controllers talk to this; it talks to packs only through
    /// <see cref="ILanguagePack"/>, so switching AzSLD → RSL is a data change.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private LanguagePackRegistry registry;
        [SerializeField] private GamificationConfig gamificationConfig;
        [SerializeField] private bool persistAcrossScenes = true;

        private ProgressStore _store;
        private readonly CurriculumProgression _progression = new CurriculumProgression();
        private List<PathNode> _path = new List<PathNode>();
        private List<LevelPathSummary> _unitPath = new List<LevelPathSummary>();
        private List<StoryEntry> _stories = new List<StoryEntry>();

        public static GameManager Instance { get; private set; }

        /// <summary>Overridable clocks (local date for streaks, UTC for review scheduling) for tests and debug tools.</summary>
        public Func<DateTime> LocalNow = () => DateTime.Now;
        public Func<DateTime> UtcNow = () => DateTime.UtcNow;

        public GameState State { get; private set; }
        public PlayerProgress Progress { get; private set; }
        public ILanguagePack ActivePack { get; private set; }
        public PackProgress ActivePackProgress { get; private set; }
        public LanguagePackRegistry Registry => registry;
        public GamificationConfig Config => gamificationConfig;

        public HeartSystem Hearts { get; private set; }
        public StreakSystem Streak { get; private set; }
        public XpSystem Xp { get; private set; }
        public ReviewScheduler Reviews { get; private set; }

        public LessonSession CurrentSession { get; private set; }
        public StorySession CurrentStory { get; private set; }

        public ILeagueService League { get; private set; }
        public ISocialService Social { get; private set; }

        /// <summary>Why the last <see cref="StartNode"/> returned null: "locked", "no_hearts", "session_running" or "no_content".</summary>
        public string LastStartFailure { get; private set; }
        public IReadOnlyList<PathNode> Path => _path;

        /// <summary>The path grouped into unit nodes per level, for the home screen.</summary>
        public IReadOnlyList<LevelPathSummary> UnitPath => _unitPath;

        public LevelPathSummary CurrentLevel => UnitPathBuilder.CurrentLevel(_unitPath);
        public IReadOnlyList<StoryEntry> Stories => _stories;

        public string LearnerName
        {
            get
            {
                string full = (Progress.firstName + " " + Progress.lastName).Trim();
                return full.Length > 0 ? full : Loc.Pick("User", "İstifadəçi");
            }
        }

        public event Action<GameState> StateChanged;
        public event Action<ILanguagePack> LanguagePackChanged;
        public event Action<IReadOnlyList<PathNode>> PathChanged;
        public event Action<LessonSession> SessionStarted;
        public event Action<LessonSession, LessonReward> SessionCompleted;
        public event Action<StorySession> StoryStarted;
        public event Action<StorySession, StoryReward> StoryCompleted;

        /// <summary>Name, language, hand or league tier changed.</summary>
        public event Action ProfileChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (persistAcrossScenes)
            {
                DontDestroyOnLoad(gameObject);
            }

            Application.targetFrameRate = 60;
            Initialize(new ProgressStore());
            Cloud.CloudService.Attach(this);
        }

        /// <summary>Separate from Awake so tests can inject a store.</summary>
        public void Initialize(ProgressStore store)
        {
            if (gamificationConfig == null)
            {
                gamificationConfig = GamificationConfig.CreateDefault();
            }

            _store = store;
            Progress = _store.Load();
            if (Progress.hearts < 0) Progress.hearts = gamificationConfig.maxHearts;
            if (Progress.joinedUtcTicks == 0) Progress.joinedUtcTicks = UtcNow().Ticks;
            Loc.SetLanguage(Progress.uiLanguage);

            Hearts = new HeartSystem(gamificationConfig, () => Progress.hearts, v => Progress.hearts = v,
                () => Progress.heartsRegenUtcTicks, v => Progress.heartsRegenUtcTicks = v, () => UtcNow());
            Hearts.Tick();
            Streak = new StreakSystem(gamificationConfig, Progress.streak);
            Xp = new XpSystem(gamificationConfig, Progress.xp);
            Reviews = new ReviewScheduler();
            if (League == null) League = new LocalLeagueService(gamificationConfig);
            if (Social == null) Social = new InviteOnlySocialService();

            Streak.Evaluate(LocalNow());
            SettleLeague();

            string code = !string.IsNullOrEmpty(Progress.activeLanguageCode)
                ? Progress.activeLanguageCode
                : (registry != null ? registry.DefaultLanguageCode : "azsld");
            if (!SelectLanguagePack(code))
            {
                Debug.LogError("[Çevir] No playable language pack found for '" + code + "'. Check the LanguagePackRegistry asset.");
            }

            SetState(GameState.Map);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Save();
                Instance = null;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Save();
            }
            else if (Streak != null)
            {
                Streak.Evaluate(LocalNow());
                Hearts.Tick();
                SettleLeague();
            }
        }

        private float _nextHeartTick;

        private void Update()
        {
            // Hearts regenerate on a clock; check a few times a minute so the counters stay current.
            if (Hearts == null || Time.unscaledTime < _nextHeartTick) return;
            _nextHeartTick = Time.unscaledTime + 10f;
            Hearts.Tick();
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        /// <summary>Raised after every save, so the cloud sync can upload the new state.</summary>
        public event Action<PlayerProgress> Saved;

        public void Save()
        {
            if (_store == null || Progress == null) return;
            Progress.updatedUtcTicks = UtcNow().Ticks;
            _store.Save(Progress);
            if (Saved != null) Saved(Progress);
        }

        /// <summary>
        /// Replaces the local progress with a copy from the cloud (sign-in on a new phone, or a newer copy from another
        /// device) and rebuilds everything that depends on it.
        /// </summary>
        public void ReplaceProgress(PlayerProgress progress)
        {
            if (progress == null) return;
            if ((CurrentSession != null && !CurrentSession.IsFinished) || CurrentStory != null) return;

            // Copy into the existing objects: the heart, streak and XP systems (and the screens listening to them)
            // keep their references.
            XpState xp = Progress.xp;
            StreakState streak = Progress.streak;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(progress), Progress);
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(progress.xp ?? new XpState()), xp);
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(progress.streak ?? new StreakState()), streak);
            Progress.xp = xp;
            Progress.streak = streak;

            if (Progress.hearts < 0) Progress.hearts = gamificationConfig.maxHearts;
            if (Progress.joinedUtcTicks == 0) Progress.joinedUtcTicks = UtcNow().Ticks;
            Loc.SetLanguage(Progress.uiLanguage);
            Hearts.Tick();
            Streak.Evaluate(LocalNow());
            _store.Save(Progress);

            string code = !string.IsNullOrEmpty(Progress.activeLanguageCode) ? Progress.activeLanguageCode
                : (registry != null ? registry.DefaultLanguageCode : "azsld");
            SelectLanguagePack(code);
            RaiseProfileChanged();
        }

        /// <summary>Signs out: the local copy is deleted (it lives in the cloud) and a fresh progress starts.</summary>
        public void ResetLocalProgress()
        {
            _store.Delete();
            var fresh = new PlayerProgress { hearts = gamificationConfig.maxHearts, joinedUtcTicks = UtcNow().Ticks };
            ReplaceProgress(fresh);
        }


        // ------------------------------------------------------------------ language packs

        public bool SelectLanguagePack(string languageCode)
        {
            if (registry == null) return false;
            ILanguagePack pack = registry.Find(languageCode);
            if (pack == null || !pack.IsPlayable) return false;

            ActivePack = pack;
            ActivePackProgress = Progress.GetOrCreatePack(pack.LanguageCode);
            Progress.activeLanguageCode = pack.LanguageCode;
            RebuildPath();
            if (LanguagePackChanged != null) LanguagePackChanged(pack);
            return true;
        }

        public void SetLeftHanded(bool leftHanded)
        {
            Progress.leftHanded = leftHanded;
            Save();
            RaiseProfileChanged();
        }

        public void SetLearnerName(string firstName, string lastName)
        {
            Progress.firstName = (firstName ?? string.Empty).Trim();
            Progress.lastName = (lastName ?? string.Empty).Trim();
            Save();
            RaiseProfileChanged();
        }

        public void SetUiLanguage(string code)
        {
            Progress.uiLanguage = code == Loc.Azerbaijani ? Loc.Azerbaijani : Loc.English;
            Loc.SetLanguage(Progress.uiLanguage);
            Save();
            RaiseProfileChanged();
        }

        /// <summary>Replaces the offline league / social stand-ins with server implementations.</summary>
        public void SetServices(ILeagueService league, ISocialService social)
        {
            if (league != null) League = league;
            if (social != null) Social = social;
            RaiseProfileChanged();
        }

        public LeagueSnapshot GetLeagueSnapshot()
        {
            DateTime now = LocalNow();
            return League.GetSnapshot(Progress.leagueTier, LearnerName, Xp.WeeklyFor(now), now);
        }

        /// <summary>The server settled a finished week: store the learner's new tier.</summary>
        public void ApplyLeagueSettlement(int tier, string settledWeek)
        {
            if (tier == Progress.leagueTier && settledWeek == Progress.leagueWeek) return;
            Progress.leagueTier = LeagueTiers.Clamp(tier, gamificationConfig.leagueTiers);
            Progress.leagueWeek = settledWeek ?? string.Empty;
            Save();
            RaiseProfileChanged();
        }

        /// <summary>Lets screens ask for a fresh ranking (the cloud service loads it in the background).</summary>
        public event Action LeagueRefreshRequested;

        public void RequestLeagueRefresh()
        {
            if (LeagueRefreshRequested != null) LeagueRefreshRequested();
        }

        private void SettleLeague()
        {
            if (League != null && League.SettlesOnServer) return;
            DateTime now = LocalNow();
            int tier = Progress.leagueTier;
            string week = Progress.leagueWeek;
            int change = LeagueSettlement.Settle(ref tier, ref week, Progress.xp, StreakSystem.IsoWeek(now), now, League, gamificationConfig);
            Progress.leagueTier = tier;
            Progress.leagueWeek = week;
            if (change != 0) RaiseProfileChanged();
        }

        /// <summary>Lets services outside the manager (cloud sync, league) tell the screens to refresh.</summary>
        public void NotifyProfileChanged()
        {
            RaiseProfileChanged();
        }

        private void RaiseProfileChanged()
        {
            if (ProfileChanged != null) ProfileChanged();
        }

        public void RebuildPath()
        {
            _path = ActivePack != null
                ? _progression.Build(ActivePack, ActivePackProgress)
                : new List<PathNode>();
            _unitPath = UnitPathBuilder.Build(_path);
            _stories = ActivePack != null ? StoryProgression.Build(ActivePack, ActivePackProgress) : new List<StoryEntry>();
            if (PathChanged != null) PathChanged(_path);
        }

        public StoryEntry FindStory(string storyId)
        {
            foreach (StoryEntry entry in _stories)
            {
                if (entry.Story.Id == storyId) return entry;
            }

            return null;
        }

        public PathNode FindNode(string nodeId)
        {
            foreach (PathNode node in _path)
            {
                if (node.Id == nodeId) return node;
            }

            return null;
        }

        // ------------------------------------------------------------------ sessions

        public bool CanStart(PathNode node, out string reason)
        {
            reason = null;
            if (!CurriculumProgression.CanStart(node))
            {
                reason = "locked";
                return false;
            }

            if ((CurrentSession != null && !CurrentSession.IsFinished) || CurrentStory != null)
            {
                reason = "session_running";
                return false;
            }

            bool heartFree = node.Kind == PathNodeKind.GateTest || node.Lesson.IsHeartFree;
            if (!heartFree && !Hearts.HasHearts)
            {
                reason = "no_hearts";
                return false;
            }

            return true;
        }

        public LessonSession StartNode(PathNode node)
        {
            string reason;
            LastStartFailure = null;
            if (!CanStart(node, out reason))
            {
                LastStartFailure = reason;
                Debug.LogWarning("[Çevir] Cannot start node " + (node != null ? node.Id : "null") + ": " + reason);
                return null;
            }

            var builder = new LessonPlanBuilder(Reviews);
            LessonSession session;
            if (node.Kind == PathNodeKind.GateTest)
            {
                session = new LessonSession(SessionKind.GateTest, null, node.Level, builder.BuildGate(node.Level),
                    Hearts, Reviews, ActivePackProgress, UtcNow);
            }
            else
            {
                List<Exercise> plan = builder.Build(node.Lesson, ActivePack.Dictionary, ActivePackProgress, UtcNow());
                session = new LessonSession(SessionKind.Lesson, node.Lesson, node.Level, plan,
                    Hearts, Reviews, ActivePackProgress, UtcNow);
            }

            if (session.Exercises.Count == 0)
            {
                // Skeleton lessons without signs yet: don't let them complete instantly for free XP.
                LastStartFailure = "no_content";
                return null;
            }

            CurrentSession = session;
            SetState(GameState.Lesson);
            if (SessionStarted != null) SessionStarted(session);
            session.Begin();
            return session;
        }

        /// <summary>Applies rewards for a finished session and updates progression. Safe to call once per session.</summary>
        public LessonReward CompleteSession()
        {
            LessonSession session = CurrentSession;
            var reward = new LessonReward();
            if (session == null) return reward;
            CurrentSession = null;

            reward.Accuracy = session.Accuracy;
            reward.Completed = session.IsFinished && !session.FailedOutOfHearts;
            DateTime today = LocalNow();

            if (reward.Completed)
            {
                if (session.Kind == SessionKind.GateTest)
                {
                    reward.Passed = session.GatePassed;
                    if (reward.Passed)
                    {
                        reward.FirstCompletion = ActivePackProgress.MarkGatePassed(session.Level.GateTestId);
                        reward.Xp = Xp.LessonReward(false);
                    }
                }
                else
                {
                    LessonData lesson = session.Lesson;
                    reward.Passed = true;
                    reward.FirstCompletion = ActivePackProgress.MarkLessonCompleted(lesson.Id);

                    if (lesson.IsMiniGame)
                    {
                        reward.Xp = Xp.MiniGameReward(session.MiniGameScore);
                    }
                    else
                    {
                        reward.PerfectBonus = session.AllCameraPerfect;
                        reward.Xp = Xp.LessonReward(reward.PerfectBonus);
                    }

                    if (lesson.LessonType == LessonType.Practice)
                    {
                        int before = Hearts.Hearts;
                        Hearts.Restore(lesson.HeartsRestored);
                        reward.HeartsRestored = Hearts.Hearts - before;
                    }
                }

                if (reward.Passed)
                {
                    SettleLeague();
                    Xp.Add(reward.Xp, today);
                    reward.StreakIncreased = Streak.RegisterActivity(today);
                }
            }

            RebuildPath();

            // Finishing a unit can unlock a story: announce it once (the results screen offers to open it).
            List<StoryData> unlocked = StoryProgression.NewlyUnlocked(ActivePack, ActivePackProgress);
            if (unlocked.Count > 0)
            {
                reward.UnlockedStory = unlocked[0];
                foreach (StoryData story in unlocked) ActivePackProgress.MarkStoryAnnounced(story.Id);
                RebuildPath();
            }

            Save();
            SetState(GameState.Results);
            if (SessionCompleted != null) SessionCompleted(session, reward);
            return reward;
        }

        public void AbandonSession()
        {
            if (CurrentSession == null) return;
            CurrentSession.Abandon();
            CurrentSession = null;
            Save();
            SetState(GameState.Map);
        }

        public void ReturnToMap()
        {
            SetState(GameState.Map);
        }

        // ------------------------------------------------------------------ stories

        public bool CanStartStory(StoryData story, out string reason)
        {
            reason = null;
            StoryEntry entry = story != null ? FindStory(story.Id) : null;
            if (entry == null || entry.State == StoryState.Locked)
            {
                reason = "locked";
                return false;
            }

            if (!story.IsPlayable)
            {
                reason = "no_content";
                return false;
            }

            if (CurrentStory != null || (CurrentSession != null && !CurrentSession.IsFinished))
            {
                reason = "session_running";
                return false;
            }

            return true;
        }

        /// <summary>Opens a story in the first-person story screen. Stories never cost hearts.</summary>
        public StorySession StartStory(StoryData story)
        {
            string reason;
            LastStartFailure = null;
            if (!CanStartStory(story, out reason))
            {
                LastStartFailure = reason;
                Debug.LogWarning("[Çevir] Cannot start story " + (story != null ? story.Id : "null") + ": " + reason);
                return null;
            }

            ActivePackProgress.MarkStoryAnnounced(story.Id);
            CurrentStory = new StorySession(story, ActivePack, Progress.firstName, Progress.lastName);
            SetState(GameState.Story);
            if (StoryStarted != null) StoryStarted(CurrentStory);
            CurrentStory.Begin();
            return CurrentStory;
        }

        /// <summary>Rewards a finished story: XP (+ bonus when every answer was camera-checked), streak, completion.</summary>
        public StoryReward CompleteStory()
        {
            StorySession story = CurrentStory;
            var reward = new StoryReward();
            if (story == null) return reward;
            CurrentStory = null;

            reward.Accuracy = story.Accuracy;
            reward.Completed = story.IsFinished;
            if (reward.Completed)
            {
                DateTime today = LocalNow();
                reward.FirstCompletion = ActivePackProgress.MarkStoryCompleted(story.Story.Id);
                reward.PerfectBonus = story.AllVerified;

                // Replays still count for the streak, but pay a smaller XP reward.
                int xp = reward.FirstCompletion ? story.Story.XpReward : Mathf.Max(1, story.Story.XpReward / 2);
                if (reward.PerfectBonus) xp += gamificationConfig.storyPerfectBonusXp;
                reward.Xp = xp;

                SettleLeague();
                Xp.Add(xp, today);
                reward.StreakIncreased = Streak.RegisterActivity(today);
            }

            // Learners may have typed their name in the story; keep it for the profile.
            if (!string.IsNullOrEmpty(story.FirstName) && story.FirstName != Progress.firstName)
            {
                Progress.firstName = story.FirstName;
                Progress.lastName = story.LastName;
                RaiseProfileChanged();
            }

            RebuildPath();
            Save();
            if (StoryCompleted != null) StoryCompleted(story, reward);
            return reward;
        }

        /// <summary>Counts a camera-checked sign from a story toward mastery ("Signs Learned") and review scheduling.</summary>
        public void RecordSignPractice(string signId, bool correct)
        {
            if (string.IsNullOrEmpty(signId) || ActivePackProgress == null) return;
            Reviews.Record(ActivePackProgress, signId, correct, UtcNow());
        }

        public void AbandonStory()
        {
            if (CurrentStory == null) return;
            CurrentStory = null;
            Save();
            SetState(GameState.Map);
        }

        private void SetState(GameState state)
        {
            if (State == state) return;
            State = state;
            if (StateChanged != null) StateChanged(state);
        }
    }
}
