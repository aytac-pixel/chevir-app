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
            _game.ProfileChanged += SyncLeagueWatch;
            if (Cloud.CloudService.Instance != null) Cloud.CloudService.Instance.StateChanged += state => SyncLeagueWatch();
            Reschedule();
            SyncLeagueWatch();
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
            SyncLeagueWatch();
        }

        // ------------------------------------------------------------------ league check while the app is closed

        private const string DeviceTokenKey = "chevir.league.deviceToken";
        private const string DeviceUserKey = "chevir.league.deviceUser";
        private bool _registering;

        /// <summary>
        /// Turns the 07:00 league message (one a day, also while the app is closed) on (signed in, league messages on) or off. The phone gets a random token
        /// the server links to the account, so the check can ask for the place without the learner's password or session.
        /// </summary>
        private void SyncLeagueWatch()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_game == null || _game.Progress == null || _registering) return;
            Cloud.CloudService cloud = Cloud.CloudService.Instance;
            ReminderSettings r = _game.Progress.reminders;
            bool wanted = cloud != null && cloud.State == Cloud.CloudState.SignedIn && r != null && r.leagueAlerts
                          && !string.IsNullOrEmpty(cloud.BaseUrl) && !string.IsNullOrEmpty(cloud.AnonKey);
            if (!wanted)
            {
                if (!_stopSent) CallWatch("stop");
                _stopSent = true;
                return;
            }

            _stopSent = false;

            string token = PlayerPrefs.GetString(DeviceTokenKey, string.Empty);
            bool sameUser = PlayerPrefs.GetString(DeviceUserKey, string.Empty) == cloud.UserId;
            if (token.Length == 0 || !sameUser)
            {
                // A new token for every account on this phone (a token stays linked to the account that registered it).
                token = Guid.NewGuid().ToString();
                PlayerPrefs.SetString(DeviceTokenKey, token);
                PlayerPrefs.Save();
            }

            if (sameUser)
            {
                StartWatch(cloud, token);
                return;
            }

            _registering = true;
            cloud.RegisterLeagueDevice(token, ok =>
            {
                _registering = false;
                if (!ok) return; // e.g. schema.sql not run again yet; tried again on the next profile change
                PlayerPrefs.SetString(DeviceUserKey, cloud.UserId);
                PlayerPrefs.Save();
                StartWatch(cloud, token);
            });
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private bool _watchStarted;
        private bool _stopSent;

        private void StartWatch(Cloud.CloudService cloud, string token)
        {
            if (_watchStarted) return;
            try
            {
                using (AndroidJavaObject context = AppContext())
                using (var watch = new AndroidJavaClass("com.chevir.leaguewatch.LeagueWatch"))
                {
                    watch.CallStatic("start", context, cloud.BaseUrl, cloud.AnonKey, token,
                        // {0} = places moved since yesterday morning, {1} = place now.
                        Loc.Pick("You moved up in the league! 🏆", "Liqada yüksəldiniz! 🏆", "Ты поднялся в лиге! 🏆"),
                        Loc.Pick("You moved down in the league", "Liqada geriləmisiniz", "Ты опустился в лиге"),
                        Loc.Pick("You moved up {0} places since yesterday. You are now #{1} this week.",
                            "Siz dünəndən bəri {0} pillə yüksəldiniz. İndi həftəlik liqada {1} sıradasınız.",
                            "Ты поднялся на {0} мест со вчерашнего дня. Сейчас ты на {1}-м месте за неделю."),
                        Loc.Pick("You moved down {0} places since yesterday. You are now #{1}. Shall we keep learning?",
                            "Siz dünəndən bəri {0} pillə geriləmisiniz, {1} sıradasınız. Öyrənməyə davam etmək istəyirsiniz?",
                            "Ты опустился на {0} мест со вчерашнего дня, сейчас ты на {1}-м месте. Продолжим учиться?"),
                        Loc.Pick("League", "Liqa", "Лига"), Loc.IsAz);
                }

                _watchStarted = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Chevir] League watch could not start: " + e.Message);
            }
        }

        private void CallWatch(string method)
        {
            _watchStarted = false;
            try
            {
                using (AndroidJavaObject context = AppContext())
                using (var watch = new AndroidJavaClass("com.chevir.leaguewatch.LeagueWatch"))
                {
                    watch.CallStatic(method, context);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Chevir] League watch: " + e.Message);
            }
        }

        private static AndroidJavaObject AppContext()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                return activity.Call<AndroidJavaObject>("getApplicationContext");
            }
        }
#endif

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
                Description = Loc.Pick("Every morning: how your place in the weekly league changed", "Hər səhər: həftəlik liqada yerinin necə dəyişdiyi", "Каждое утро: как изменилось твоё место в лиге недели"),
                Importance = Importance.Default
            });
            _channelReady = true;
        }
#endif
    }
}
