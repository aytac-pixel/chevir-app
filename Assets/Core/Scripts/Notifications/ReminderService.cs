using Cevir.Core.Localization;
using System;
using System.Collections;
using System.Collections.Generic;
using Cevir.Core.Game;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
#endif

namespace Cevir.Core.Notifications
{
    /// <summary>
    /// Local (on-phone) practice reminders. Rebuilds the schedule from <see cref="ReminderPlanner"/> when the app goes
    /// to the background, after a lesson or story, and when the learner changes the settings. Nothing is sent from a
    /// server, so reminders also work offline.
    /// </summary>
    public sealed class ReminderService : MonoBehaviour
    {
        private const string ChannelId = "cevir_reminders";
        private const string GreetingChannelId = "cevir_greetings";
        private const string LeagueChannelId = "cevir_league";
        private const string LeagueRankKey = "chevir.league.lastRank";
        private const string LeagueWeekKey = "chevir.league.lastWeek";
        private const string PermissionAskedKey = "chevir.notifications.asked";

        private GameManager _game;
#if UNITY_ANDROID && !UNITY_EDITOR
        private bool _channelReady;
#endif

        public static ReminderService Instance { get; private set; }

        /// <summary>False when the phone refused notifications (Android 13+ asks once).</summary>
        public bool PermissionDenied { get; private set; }

        public static ReminderService Attach(GameManager game)
        {
            ReminderService service = game.GetComponent<ReminderService>();
            if (service == null) service = game.gameObject.AddComponent<ReminderService>();
            service._game = game;
            Instance = service;
            return service;
        }

        private void Start()
        {
            if (_game == null) return;
            _game.Streak.StreakChanged += state => Reschedule();
            _game.ProfileChanged += Reschedule;
            _game.ProfileChanged += CheckLeaguePlace;
            Reschedule();
            AskPermissionOnce();
        }

        /// <summary>Greetings are on from the start, so the phone is asked once for permission to show them.</summary>
        private void AskPermissionOnce()
        {
            if (PlayerPrefs.GetInt(PermissionAskedKey, 0) == 1) return;
            ReminderSettings r = _game.Progress.reminders;
            if (r == null || (!r.greetings && !r.leagueAlerts && !r.enabled)) return;
            PlayerPrefs.SetInt(PermissionAskedKey, 1);
            PlayerPrefs.Save();
            StartCoroutine(RequestPermissionThenSchedule(null));
        }

        /// <summary>Saves the greeting / league switches and rebuilds the schedule.</summary>
        public void SetExtras(bool greetings, bool leagueAlerts)
        {
            if (_game == null) return;
            ReminderSettings target = _game.Progress.reminders ?? (_game.Progress.reminders = new ReminderSettings());
            bool asked = (greetings && !target.greetings) || (leagueAlerts && !target.leagueAlerts);
            target.greetings = greetings;
            target.leagueAlerts = leagueAlerts;
            _game.Save();
            if (asked) StartCoroutine(RequestPermissionThenSchedule(null));
            else Reschedule();
        }

        /// <summary>
        /// When the weekly ranking comes in with a new place for the learner, says so at once. Works while the app runs
        /// or checks the ranking (a closed app cannot know about other learners' XP without a push server).
        /// </summary>
        private void CheckLeaguePlace()
        {
            if (_game == null || _game.Progress == null) return;
            Social.LeagueSnapshot league = _game.GetLeagueSnapshot();
            Social.LeagueEntry you = league != null ? league.You : null;
            if (league == null || league.Offline || you == null || you.Rank <= 0 || league.Entries == null || league.Entries.Count < 2) return;

            string week = StreakSystemWeek();
            int last = PlayerPrefs.GetString(LeagueWeekKey, string.Empty) == week ? PlayerPrefs.GetInt(LeagueRankKey, 0) : 0;
            if (last == you.Rank) return;
            PlayerPrefs.SetString(LeagueWeekKey, week);
            PlayerPrefs.SetInt(LeagueRankKey, you.Rank);
            PlayerPrefs.Save();

            ReminderSettings r = _game.Progress.reminders;
            if (last == 0 || r == null || !r.leagueAlerts) return; // the first look this week is the starting point
            bool up = you.Rank < last;
            string place = UI.LeagueScreenUI.Ordinal(you.Rank);
            string title = up
                ? Loc.Pick("You moved up in the league! 🏆", "Liqada irəlilədin! 🏆", "Ты поднялся в лиге! 🏆")
                : Loc.Pick("Someone passed you in the league", "Liqada səni ötdülər", "Тебя обогнали в лиге");
            string body = up
                ? Loc.Pick("You are now #" + you.Rank + " this week. Keep going!", "Həftəlik reytinqdə indi " + place + " yerdəsən. Belə davam et!",
                    "Теперь ты на " + you.Rank + "-м месте за неделю. Так держать!")
                : Loc.Pick("You are now #" + you.Rank + " this week. One lesson can win your place back.",
                    "Həftəlik reytinqdə indi " + place + " yerdəsən. Bir dərslə yerini geri ala bilərsən.",
                    "Теперь ты на " + you.Rank + "-м месте за неделю. Один урок вернёт тебе место.");
            SendNow(title, body);
        }

        private string StreakSystemWeek()
        {
            return Gamification.StreakSystem.IsoWeek(_game.LocalNow());
        }

        private void SendNow(string title, string body)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                EnsureChannel();
                AndroidNotificationCenter.SendNotification(new AndroidNotification
                {
                    Title = title,
                    Text = body,
                    FireTime = DateTime.Now,
                    ShouldAutoCancel = true
                }, LeagueChannelId);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Chevir] League message could not be shown: " + e.Message);
            }
#else
            Debug.Log("[Chevir] League message: " + title + " / " + body);
#endif
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Reschedule();
        }

        private void OnApplicationQuit()
        {
            Reschedule();
        }

        /// <summary>Saves new settings, asks for the notification permission if needed and rebuilds the schedule.</summary>
        public void Apply(ReminderSettings settings, Action<bool> done = null)
        {
            if (_game == null || settings == null) return;
            settings.configured = true;
            settings.Normalize();
            ReminderSettings target = _game.Progress.reminders ?? (_game.Progress.reminders = new ReminderSettings());
            target.configured = true;
            target.enabled = settings.enabled;
            target.startMinutes = settings.startMinutes;
            target.endMinutes = settings.endMinutes;
            _game.Save();

            if (!target.enabled)
            {
                Reschedule();
                if (done != null) done(true);
                return;
            }

            StartCoroutine(RequestPermissionThenSchedule(done));
        }

        public void Reschedule()
        {
            if (_game == null || _game.Progress == null) return;
            DateTime now = _game.LocalNow();
            List<PlannedReminder> plan = ReminderPlanner.Plan(_game.Progress.reminders, _game.Streak, now);
            plan.AddRange(ReminderPlanner.PlanGreetings(_game.Progress.reminders, _game.Streak, _game.Progress, now));
            Schedule(plan);
        }

        private IEnumerator RequestPermissionThenSchedule(Action<bool> done)
        {
            bool granted = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            var request = new PermissionRequest();
            while (request.Status == PermissionStatus.RequestPending) yield return null;
            granted = request.Status == PermissionStatus.Allowed;
#else
            yield return null;
#endif
            PermissionDenied = !granted;
            Reschedule();
            if (done != null) done(granted);
        }

        private void Schedule(List<PlannedReminder> plan)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                EnsureChannel();
                AndroidNotificationCenter.CancelAllScheduledNotifications();
                foreach (PlannedReminder reminder in plan)
                {
                    var notification = new AndroidNotification
                    {
                        Title = reminder.Title,
                        Text = reminder.Body,
                        FireTime = reminder.FireAt,
                        ShouldAutoCancel = true
                    };
                    AndroidNotificationCenter.SendNotification(notification, reminder.Greeting ? GreetingChannelId : ChannelId);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Chevir] Reminders could not be scheduled: " + e.Message);
            }
#else
            if (plan.Count > 0) Debug.Log("[Chevir] Next reminder: " + plan[0].FireAt + " (" + plan.Count + " planned).");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void EnsureChannel()
        {
            if (_channelReady) return;
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel
            {
                Id = ChannelId,
                Name = Loc.T("Məşq xatırlatmaları"),
                Description = Loc.T("Seçdiyin saatlarda gündəlik dərs xatırlatmaları"),
                Importance = Importance.Default
            });
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel
            {
                Id = GreetingChannelId,
                Name = Loc.Pick("Daily greetings", "Gündəlik salamlar", "Приветствия дня"),
                Description = Loc.Pick("Good morning, afternoon, evening and night messages",
                    "Sabah, günorta, axşam və gecə salamları", "Утренние, дневные, вечерние и ночные приветствия"),
                Importance = Importance.Default
            });
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel
            {
                Id = LeagueChannelId,
                Name = Loc.Pick("League", "Liqa", "Лига"),
                Description = Loc.Pick("When your place in the weekly league changes", "Həftəlik liqada yerin dəyişəndə",
                    "Когда меняется твоё место в лиге недели"),
                Importance = Importance.Default
            });
            _channelReady = true;
        }
#endif
    }
}
