using System;
using System.Collections.Generic;
using Cevir.Core.Gamification;

namespace Cevir.Core.Game
{
    /// <summary>Everything the app persists for one learner. Plain data so JsonUtility can save it.</summary>
    [Serializable]
    public sealed class PlayerProgress
    {
        public const int CurrentVersion = 3;

        public int version = CurrentVersion;
        public string activeLanguageCode = "";
        public string nickname = "";

        /// <summary>Learner's name, fingerspelled in Story 1 and shown on the profile.</summary>
        public string firstName = "";
        public string lastName = "";

        /// <summary>Interface language: "az" (default) or "en".</summary>
        public string uiLanguage = "az";

        /// <summary>Cloud account the progress belongs to (empty when not signed in).</summary>
        public string accountId = "";

        /// <summary>UTC ticks of the last local change, used to pick the newer copy when syncing with the cloud.</summary>
        public long updatedUtcTicks;

        /// <summary>UTC ticks of the first launch ("Joined October 2026").</summary>
        public long joinedUtcTicks;

        /// <summary>League tier (0 = Bronze) and the ISO week it was last settled for.</summary>
        public int leagueTier;
        public string leagueWeek = "";

        /// <summary>Hand the learner signs with; left-handed input is mirrored before matching.</summary>
        public bool leftHanded;

        public int hearts = -1;

        /// <summary>UTC ticks when the current heart regeneration period started; 0 while hearts are full.</summary>
        public long heartsRegenUtcTicks;
        public XpState xp = new XpState();
        public StreakState streak = new StreakState();

        /// <summary>Opt-in to share anonymised landmark data (never video). Off by default.</summary>
        public bool shareAnonymousLandmarks;

        public List<PackProgress> packs = new List<PackProgress>();

        public PackProgress GetOrCreatePack(string languageCode)
        {
            foreach (PackProgress pack in packs)
            {
                if (pack.languageCode == languageCode) return pack;
            }

            var created = new PackProgress { languageCode = languageCode };
            packs.Add(created);
            return created;
        }
    }

    /// <summary>Progress within one language pack. Packs are independent: learning RSL does not touch AzSLD progress.</summary>
    [Serializable]
    public sealed class PackProgress
    {
        public string languageCode;
        public List<string> completedLessonIds = new List<string>();
        public List<string> passedGateIds = new List<string>();
        public List<SignMastery> mastery = new List<SignMastery>();
        public List<string> completedStoryIds = new List<string>();

        /// <summary>Unlocked stories the learner has already been told about (the "new story" prompt shows once).</summary>
        public List<string> announcedStoryIds = new List<string>();

        [NonSerialized] private HashSet<string> _completed;
        [NonSerialized] private HashSet<string> _gates;

        public bool IsStoryCompleted(string storyId)
        {
            return completedStoryIds != null && completedStoryIds.Contains(storyId);
        }

        public bool IsStoryAnnounced(string storyId)
        {
            return announcedStoryIds != null && announcedStoryIds.Contains(storyId);
        }

        public bool MarkStoryCompleted(string storyId)
        {
            if (completedStoryIds == null) completedStoryIds = new List<string>();
            MarkStoryAnnounced(storyId);
            if (completedStoryIds.Contains(storyId)) return false;
            completedStoryIds.Add(storyId);
            return true;
        }

        public void MarkStoryAnnounced(string storyId)
        {
            if (announcedStoryIds == null) announcedStoryIds = new List<string>();
            if (!announcedStoryIds.Contains(storyId)) announcedStoryIds.Add(storyId);
        }

        /// <summary>Signs answered correctly at least once (the profile's "Signs Learned").</summary>
        public int SignsLearned
        {
            get
            {
                int count = 0;
                foreach (SignMastery m in mastery)
                {
                    if (m.correct > 0) count++;
                }

                return count;
            }
        }

        public bool IsLessonCompleted(string lessonId)
        {
            if (_completed == null) _completed = new HashSet<string>(completedLessonIds);
            return _completed.Contains(lessonId);
        }

        public bool IsGatePassed(string gateId)
        {
            if (_gates == null) _gates = new HashSet<string>(passedGateIds);
            return _gates.Contains(gateId);
        }

        public bool MarkLessonCompleted(string lessonId)
        {
            if (IsLessonCompleted(lessonId)) return false;
            completedLessonIds.Add(lessonId);
            _completed.Add(lessonId);
            return true;
        }

        public bool MarkGatePassed(string gateId)
        {
            if (IsGatePassed(gateId)) return false;
            passedGateIds.Add(gateId);
            _gates.Add(gateId);
            return true;
        }

        public SignMastery GetOrCreateMastery(string signId)
        {
            foreach (SignMastery m in mastery)
            {
                if (m.signId == signId) return m;
            }

            var created = new SignMastery { signId = signId };
            mastery.Add(created);
            return created;
        }
    }

    /// <summary>Leitner-box spaced repetition state for one sign.</summary>
    [Serializable]
    public sealed class SignMastery
    {
        public string signId;

        /// <summary>0 = new/forgotten ... 5 = mastered.</summary>
        public int box;

        /// <summary>UTC ticks when the sign is next due for review.</summary>
        public long dueUtcTicks;

        public int correct;
        public int attempts;
    }
}
