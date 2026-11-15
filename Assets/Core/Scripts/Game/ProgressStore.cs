using System;
using System.IO;
using UnityEngine;

namespace Cevir.Core.Game
{
    /// <summary>
    /// Saves progress as JSON under persistentDataPath, writing to a temp file first so a crash mid-write
    /// cannot corrupt the save. Server sync (leagues, multi-device) can wrap this later.
    /// </summary>
    public sealed class ProgressStore
    {
        private readonly string _path;

        public ProgressStore(string fileName = "cevir_progress.json")
        {
            _path = Path.Combine(Application.persistentDataPath, fileName);
        }

        public PlayerProgress Load()
        {
            try
            {
                if (File.Exists(_path))
                {
                    PlayerProgress loaded = JsonUtility.FromJson<PlayerProgress>(File.ReadAllText(_path));
                    if (loaded != null)
                    {
                        return Migrate(loaded);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[Chevir] Could not read progress, starting fresh: " + e.Message);
            }

            return new PlayerProgress();
        }

        /// <summary>Removes the local save (sign-out).</summary>
        public void Delete()
        {
            try
            {
                if (File.Exists(_path)) File.Delete(_path);
            }
            catch (Exception e)
            {
                Debug.LogError("[Chevir] Could not delete progress: " + e.Message);
            }
        }

        public static PlayerProgress FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                PlayerProgress loaded = JsonUtility.FromJson<PlayerProgress>(json);
                return loaded != null ? Migrate(loaded) : null;
            }
            catch (Exception e)
            {
                Debug.LogError("[Chevir] Could not read cloud progress: " + e.Message);
                return null;
            }
        }

        public void Save(PlayerProgress progress)
        {
            try
            {
                string temp = _path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(progress));
                if (File.Exists(_path)) File.Delete(_path);
                File.Move(temp, _path);
            }
            catch (Exception e)
            {
                Debug.LogError("[Chevir] Could not save progress: " + e.Message);
            }
        }

        private static PlayerProgress Migrate(PlayerProgress progress)
        {
            if (progress.xp == null) progress.xp = new Gamification.XpState();
            if (progress.streak == null) progress.streak = new Gamification.StreakState();
            if (progress.packs == null) progress.packs = new System.Collections.Generic.List<PackProgress>();
            foreach (PackProgress pack in progress.packs)
            {
                if (pack.completedStoryIds == null) pack.completedStoryIds = new System.Collections.Generic.List<string>();
                if (pack.announcedStoryIds == null) pack.announcedStoryIds = new System.Collections.Generic.List<string>();
            }

            // The app is Azerbaijani-first; saves from before version 3 defaulted to English.
            if (string.IsNullOrEmpty(progress.uiLanguage) || progress.version < 3) progress.uiLanguage = "az";
            if (progress.accountId == null) progress.accountId = "";
            if (progress.firstName == null) progress.firstName = "";
            if (progress.lastName == null) progress.lastName = "";
            if (progress.leagueWeek == null) progress.leagueWeek = "";
            progress.version = PlayerProgress.CurrentVersion;
            return progress;
        }
    }
}
