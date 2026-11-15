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
            Reschedule();
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
            List<PlannedReminder> plan = ReminderPlanner.Plan(_game.Progress.reminders, _game.Streak, _game.LocalNow());
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
                    AndroidNotificationCenter.SendNotification(notification, ChannelId);
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
                Name = "Məşq xatırlatmaları",
                Description = "Seçdiyin saatlarda gündəlik dərs xatırlatmaları",
                Importance = Importance.Default
            });
            _channelReady = true;
        }
#endif
    }
}
