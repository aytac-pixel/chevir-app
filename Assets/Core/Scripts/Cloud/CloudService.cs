using System;
using System.Globalization;
using Cevir.Core.Game;
using Cevir.Core.Gamification;
using Cevir.Core.Social;
using UnityEngine;

namespace Cevir.Core.Cloud
{
    public enum CloudState
    {
        /// <summary>No server configured (CevirCloudConfig is empty): the app runs on this phone only.</summary>
        Disabled,
        SignedOut,
        SignedIn
    }

    /// <summary>
    /// Accounts and sync. Signed-in learners' progress is stored in the database (table progress) and uploaded a few
    /// seconds after every change; the league ranking comes from the server. Progress is also saved on the phone, so
    /// the app keeps working offline and uploads when the connection is back.
    /// </summary>
    public sealed class CloudService : MonoBehaviour
    {
        private GameManager _game;
        private CloudConfig _config;
        private SupabaseClient _client;
        private CloudLeagueService _league;
        private LocalLeagueService _localLeague;

        private bool _uploadPending;
        private bool _uploading;
        private float _uploadAt;
        private bool _synced;
        private float _nextRetry;
        private string _pendingName;
        private const string PendingNameKey = "cevir.auth.pendingName";
        private string _pendingNameSign;
        private const string PendingNameSignKey = "cevir.auth.pendingNameSign";
        private int _pendingConsent = -1; // -1 not asked, 0 refused, 1 allowed (answer given at sign-up)
        private const string PendingConsentKey = "cevir.auth.pendingConsent";

        public static CloudService Instance { get; private set; }

        public CloudState State { get; private set; }
        public string Email => _client != null ? _client.Email : null;
        public string UserId => _client != null ? _client.UserId : null;

        /// <summary>The server client, for the sign-sample uploads (null when no server is configured).</summary>
        internal SupabaseClient Client => _client;

        /// <summary>True once the first download after sign-in finished (or failed and the local copy is used).</summary>
        public bool Synced => _synced;

        public event Action<CloudState> StateChanged;

        /// <summary>Adds the cloud service next to the GameManager. Without a configured server it stays Disabled.</summary>
        public static CloudService Attach(GameManager game)
        {
            CloudService service = game.GetComponent<CloudService>();
            if (service == null) service = game.gameObject.AddComponent<CloudService>();
            service.Initialize(game);
            return service;
        }

        private void Initialize(GameManager game)
        {
            Instance = this;
            _game = game;
            _config = CloudConfig.Load();
            _localLeague = new LocalLeagueService(game.Config);

            if (_config == null || !_config.IsConfigured)
            {
                State = CloudState.Disabled;
                Debug.LogWarning("[Chevir] Server not configured (Assets/Core/Resources/CevirCloudConfig.asset): accounts, sync and the " +
                                 "league ranking are off. See QURASDIRMA.md, section 'Verilənlər bazası'.");
                return;
            }

            _client = new SupabaseClient(_config, this);
            _league = new CloudLeagueService(_client, game.Config);
            _league.Updated += () => _game.NotifyProfileChanged();
            _game.Saved += OnSaved;
            _game.LeagueRefreshRequested += RefreshLeague;
            SignSampleCollector.Attach(this, game);

            if (_client.HasSession)
            {
                // Signed in earlier: open the app at once and sync in the background.
                SetState(CloudState.SignedIn);
                _game.SetServices(_league, null);
                Sync();
            }
            else
            {
                SetState(CloudState.SignedOut);
            }
        }

        private void OnDestroy()
        {
            if (_game != null)
            {
                _game.Saved -= OnSaved;
                _game.LeagueRefreshRequested -= RefreshLeague;
            }

            if (Instance == this) Instance = null;
        }

        private void OnApplicationPause(bool paused)
        {
            if (State != CloudState.SignedIn) return;
            if (paused) UploadNow();
            else Sync();
        }

        private void Update()
        {
            if (State != CloudState.SignedIn) return;
            if (_uploadPending && !_uploading && Time.unscaledTime >= _uploadAt) UploadNow();
            if (!_synced && Time.unscaledTime >= _nextRetry) Sync();
        }

        // ------------------------------------------------------------------ accounts

        public void SignUp(string name, string email, string password, Action<string, bool> done)
        {
            SignUp(name, null, email, password, done);
        }

        /// <summary>Creates an account with the learner's name and name sign (nickname or best-known feature).</summary>
        public void SignUp(string name, string nameSign, string email, string password, Action<string, bool> done)
        {
            SignUp(name, nameSign, null, email, password, done);
        }

        /// <summary>
        /// Creates an account. <paramref name="dataConsent"/> is the answer to the data permission asked at sign-up
        /// (null: not asked; the app asks after the first sign-in).
        /// </summary>
        public void SignUp(string name, string nameSign, bool? dataConsent, string email, string password, Action<string, bool> done)
        {
            if (_client == null)
            {
                done(CloudErrors.Unknown, false);
                return;
            }

            name = (name ?? string.Empty).Trim();
            nameSign = (nameSign ?? string.Empty).Trim();
            _client.SignUp(email.Trim(), password, name, nameSign, dataConsent == true, (response, signedIn) =>
            {
                if (!response.Ok)
                {
                    done(response.Error, false);
                    return;
                }

                // The name typed at sign-up is the learner's name in the app and on the league table.
                _pendingName = name;
                PlayerPrefs.SetString(PendingNameKey, name); // kept until the first sign-in (after e-mail confirmation)
                _pendingNameSign = nameSign;
                PlayerPrefs.SetString(PendingNameSignKey, nameSign);
                _pendingConsent = dataConsent.HasValue ? (dataConsent.Value ? 1 : 0) : -1;
                PlayerPrefs.SetInt(PendingConsentKey, _pendingConsent);
                PlayerPrefs.Save();
                if (signedIn) OnSignedIn();
                done(null, !signedIn);
            });
        }

        public void SignIn(string email, string password, Action<string> done)
        {
            if (_client == null)
            {
                done(CloudErrors.Unknown);
                return;
            }

            _client.SignIn(email.Trim(), password, response =>
            {
                if (!response.Ok)
                {
                    done(response.Error);
                    return;
                }

                OnSignedIn();
                done(null);
            });
        }

        public void SendPasswordReset(string email, Action<string> done)
        {
            if (_client == null)
            {
                done(CloudErrors.Unknown);
                return;
            }

            _client.SendPasswordReset(email.Trim(), response => done(response.Ok ? null : response.Error));
        }

        /// <summary>After "forgot password": signs in with the e-mailed 6-digit code and sets the new password.</summary>
        public void ResetPasswordWithCode(string email, string code, string newPassword, Action<string> done)
        {
            if (_client == null)
            {
                done(CloudErrors.Unknown);
                return;
            }

            _client.VerifyRecoveryCode(email.Trim(), code.Trim(), response =>
            {
                if (!response.Ok)
                {
                    done(response.Error);
                    return;
                }

                _client.UpdateUser(JsonUtility.ToJson(new PasswordUpdate { password = newPassword }), update =>
                {
                    OnSignedIn();
                    done(update.Ok ? null : update.Error);
                });
            });
        }

        public void ChangePassword(string newPassword, Action<string> done)
        {
            if (_client == null || !_client.HasSession)
            {
                done(CloudErrors.SignedOut);
                return;
            }

            _client.UpdateUser(JsonUtility.ToJson(new PasswordUpdate { password = newPassword }), r => done(r.Ok ? null : r.Error));
        }

        /// <summary>Changes the e-mail. With confirmation on, the change applies after the link in the new inbox is opened.</summary>
        public void ChangeEmail(string newEmail, Action<string, bool> done)
        {
            if (_client == null || !_client.HasSession)
            {
                done(CloudErrors.SignedOut, false);
                return;
            }

            string before = _client.Email;
            _client.UpdateUser(JsonUtility.ToJson(new EmailUpdate { email = newEmail.Trim() }), r =>
            {
                if (!r.Ok)
                {
                    done(r.Error, false);
                    return;
                }

                bool pending = _client.Email == before;
                if (StateChanged != null) StateChanged(State);
                done(null, pending);
            });
        }

        /// <summary>Uploads the profile photo (JPEG) to the "avatars" bucket and stores its address in the profile.</summary>
        public void UploadAvatar(byte[] jpeg, Action<string> done)
        {
            if (_client == null || !_client.HasSession)
            {
                done(null); // no account: the photo stays on this phone only
                return;
            }

            string path = _client.UserId + "/avatar.jpg";
            _client.UploadFile("avatars", path, jpeg, "image/jpeg", r =>
            {
                if (!r.Ok)
                {
                    done(r.Error);
                    return;
                }

                _game.SetAvatarUrl(_client.PublicFileUrl("avatars", path) + "?v=" + DateTime.UtcNow.Ticks);
                done(null);
            });
        }

        public void RemoveAvatar(Action<string> done)
        {
            if (_client == null || !_client.HasSession)
            {
                done(null);
                return;
            }

            _client.DeleteFile("avatars", _client.UserId + "/avatar.jpg", r =>
            {
                _game.SetAvatarUrl(string.Empty);
                done(r.Ok || r.Status == 404 || r.Status == 400 ? null : r.Error);
            });
        }

        /// <summary>Deletes the account and everything stored for it on the server, then signs out.</summary>
        public void DeleteAccount(Action<string> done)
        {
            if (_client == null || !_client.HasSession)
            {
                done(CloudErrors.SignedOut);
                return;
            }

            DeleteSignSampleFiles(false, ignoredSamples => _client.DeleteFile("avatars", _client.UserId + "/avatar.jpg", ignored =>
            {
                _client.Rpc("delete_account", "{}", r =>
                {
                    if (!r.Ok)
                    {
                        done(r.Error);
                        return;
                    }

                    _uploadPending = false;
                    _client.SignOut();
                    _league.Clear();
                    _synced = false;
                    _game.SetServices(_localLeague, null);
                    _game.ResetLocalProgress();
                    AvatarStore.Delete();
                    SetState(CloudState.SignedOut);
                    done(null);
                });
            }));
        }

        // ------------------------------------------------------------------ sign samples (dataset)

        /// <summary>
        /// Saves the learner's answer to «Məlumatlarımla Chevir-i inkişaf etdirməyə kömək et». The profile on the server
        /// is updated at once (the database only accepts samples while it is on). Turning it off also deletes the
        /// samples already saved for this account.
        /// </summary>
        public void SetDataConsent(bool allowed, Action<string> done)
        {
            _game.SetDataConsent(allowed);
            if (_client == null || !_client.HasSession)
            {
                if (done != null) done(null);
                return;
            }

            UploadConsent(allowed, error =>
            {
                if (allowed || error != null)
                {
                    if (done != null) done(error);
                    return;
                }

                DeleteSignSampleFiles(true, done);
            });
        }

        private void UploadConsent(bool allowed, Action<string> done)
        {
            var row = new ProfileConsentRow { id = _client.UserId, data_consent = allowed };
            _client.Upsert("profiles", "id", JsonUtility.ToJson(row), r =>
            {
                if (done != null) done(r.Ok ? null : r.Error);
            });
        }

        /// <summary>Deletes this account's sample videos from storage and, with <paramref name="rows"/>, the table rows too.</summary>
        private void DeleteSignSampleFiles(bool rows, Action<string> done)
        {
            string userId = _client.UserId;
            _client.Select("sign_samples?select=video_path&video_path=not.is.null&user_id=eq." + userId + "&limit=1000", list =>
            {
                if (!list.Ok)
                {
                    // No table yet (schema.sql not run again) or offline: nothing to delete here.
                    if (done != null) done(list.Status == 404 || list.Status == 400 ? null : list.Error);
                    return;
                }

                var paths = new System.Collections.Generic.List<string>();
                foreach (SignSampleVideoRow row in SupabaseClient.ParseArray<SignSampleVideoRows>(list.Body).items)
                {
                    if (!string.IsNullOrEmpty(row.video_path)) paths.Add(row.video_path);
                }

                Action deleteRows = () =>
                {
                    if (!rows)
                    {
                        if (done != null) done(null);
                        return;
                    }

                    _client.DeleteRows("sign_samples", "user_id=eq." + userId, r =>
                    {
                        if (done != null) done(r.Ok ? null : r.Error);
                    });
                };

                if (paths.Count == 0) deleteRows();
                else _client.DeleteFiles(SignSampleCollector.Bucket, paths, ignored => deleteRows());
            });
        }

        public void SignOut()
        {
            if (_client == null) return;
            UploadNow();
            _client.SignOut();
            _league.Clear();
            _synced = false;
            _uploadPending = false;
            _game.SetServices(_localLeague, null);
            _game.ResetLocalProgress();
            AvatarStore.Delete();
            SetState(CloudState.SignedOut);
        }

        private void OnSignedIn()
        {
            if (string.IsNullOrEmpty(_pendingName)) _pendingName = PlayerPrefs.GetString(PendingNameKey, string.Empty);
            if (string.IsNullOrEmpty(_pendingNameSign)) _pendingNameSign = PlayerPrefs.GetString(PendingNameSignKey, string.Empty);
            if (_pendingConsent < 0) _pendingConsent = PlayerPrefs.GetInt(PendingConsentKey, -1);
            _synced = false;
            _game.SetServices(_league, null);
            SetState(CloudState.SignedIn);
            Sync();
        }

        // ------------------------------------------------------------------ sync

        /// <summary>Downloads the stored progress, keeps the newer copy, settles finished league weeks and loads the ranking.</summary>
        public void Sync()
        {
            if (_client == null || !_client.HasSession) return;
            _nextRetry = Time.unscaledTime + 30f;
            string userId = _client.UserId;

            _client.Select("progress?select=user_id,data,client_updated&user_id=eq." + userId, response =>
            {
                if (!response.Ok)
                {
                    if (response.Status == 401) HandleSessionLost();
                    return; // offline: keep using the local copy, retry later
                }

                PlayerProgress local = _game.Progress;
                ProgressRows rows = SupabaseClient.ParseArray<ProgressRows>(response.Body);
                ProgressRow remote = rows.items.Count > 0 ? rows.items[0] : null;
                bool sameAccount = local.accountId == userId;

                if (remote != null && !(sameAccount && local.updatedUtcTicks > remote.client_updated))
                {
                    PlayerProgress downloaded = ProgressStore.FromJson(remote.data);
                    if (downloaded != null)
                    {
                        downloaded.accountId = userId;
                        _game.ReplaceProgress(downloaded);
                    }
                }
                else
                {
                    // No copy on the server yet (new account) or the phone has newer changes: upload this one.
                    if (!sameAccount && !string.IsNullOrEmpty(local.accountId)) _game.ResetLocalProgress();
                    _game.Progress.accountId = userId;
                    if (!string.IsNullOrEmpty(_pendingName))
                    {
                        string first, last;
                        SplitName(_pendingName, out first, out last);
                        _game.SetLearnerName(first, last); // saves, which schedules the upload
                    }

                    if (!string.IsNullOrEmpty(_pendingNameSign)) _game.SetNameSign(_pendingNameSign);
                    if (_pendingConsent >= 0) _game.SetDataConsent(_pendingConsent == 1);

                    ScheduleUpload(0f);
                }

                _pendingName = null;
                PlayerPrefs.DeleteKey(PendingNameKey);
                _pendingNameSign = null;
                PlayerPrefs.DeleteKey(PendingNameSignKey);
                _pendingConsent = -1;
                PlayerPrefs.DeleteKey(PendingConsentKey);

                _synced = true;
                SettleLeague();
            });
        }

        private void SettleLeague()
        {
            string week = StreakSystem.IsoWeek(_game.LocalNow());
            _client.Rpc("settle_league", JsonUtility.ToJson(new SettleRequest { p_week = week }), response =>
            {
                int tier;
                if (response.Ok && int.TryParse((response.Body ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out tier))
                {
                    _game.ApplyLeagueSettlement(tier, week);
                }

                RefreshLeague();
            });
        }

        private void RefreshLeague()
        {
            if (State == CloudState.SignedIn && _league != null) _league.Refresh(_game.Progress.leagueTier, _game.LocalNow());
        }

        private void OnSaved(PlayerProgress progress)
        {
            if (State == CloudState.SignedIn) ScheduleUpload(_config.uploadDelaySeconds);
        }

        private void ScheduleUpload(float delay)
        {
            _uploadPending = true;
            _uploadAt = Time.unscaledTime + delay;
        }

        /// <summary>Uploads progress, the public profile (name, tier, XP) and this week's XP for the league.</summary>
        private void UploadNow()
        {
            if (_client == null || !_client.HasSession || _uploading || !_uploadPending) return;
            PlayerProgress progress = _game.Progress;
            string userId = _client.UserId;
            if (progress.accountId != userId) return; // not synced with this account yet

            _uploadPending = false;
            _uploading = true;

            var row = new ProgressRow { user_id = userId, data = JsonUtility.ToJson(progress), client_updated = progress.updatedUtcTicks };
            var profile = new ProfileRow
            {
                id = userId,
                display_name = PublicName(progress),
                league_tier = progress.leagueTier,
                total_xp = progress.xp != null ? progress.xp.total : 0,
                streak = progress.streak != null ? progress.streak.current : 0,
                avatar_url = progress.avatarUrl ?? string.Empty,
                reminders_on = progress.reminders != null && progress.reminders.enabled,
                practice_from = progress.reminders != null ? ReminderSettings.Clock(progress.reminders.startMinutes) : null,
                practice_to = progress.reminders != null ? ReminderSettings.Clock(progress.reminders.endMinutes) : null
            };

            _client.Upsert("progress", "user_id", JsonUtility.ToJson(row), r1 =>
            {
                _client.Upsert("profiles", "id", JsonUtility.ToJson(profile), r2 =>
                {
                    Action finish = () =>
                    {
                        _uploading = false;
                        if (!r1.Ok || !r2.Ok)
                        {
                            if (r1.Status == 401 || r2.Status == 401) HandleSessionLost();
                            else ScheduleUpload(30f); // offline: try again later
                        }
                    };

                    // The name sign goes in its own request: a database without the name_sign column yet
                    // (schema.sql not run again) then only loses this field, not the whole profile.
                    // It is also kept in the progress JSON above.
                    var nameSign = new ProfileNameSignRow { id = userId, name_sign = progress.nameSign ?? string.Empty };
                    _client.Upsert("profiles", "id", JsonUtility.ToJson(nameSign), r4 => { });
                    if (progress.dataConsentAsked) UploadConsent(progress.dataConsent, null);

                    if (progress.xp == null || string.IsNullOrEmpty(progress.xp.week))
                    {
                        finish();
                        return;
                    }

                    var weekly = new WeeklyXpRow { user_id = userId, week = progress.xp.week, tier = progress.leagueTier, xp = progress.xp.weekly };
                    _client.Upsert("weekly_xp", "user_id,week", JsonUtility.ToJson(weekly), r3 =>
                    {
                        finish();
                        if (r3.Ok) RefreshLeague();
                    });
                });
            });
        }

        private void HandleSessionLost()
        {
            if (_client.HasSession) return; // a temporary error; the token refresh decides
            _league.Clear();
            _synced = false;
            _game.SetServices(_localLeague, null);
            SetState(CloudState.SignedOut);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Name shown to other learners on the league table: first name and the surname's initial.</summary>
        public static string PublicName(PlayerProgress progress)
        {
            string first = (progress.firstName ?? string.Empty).Trim();
            string last = (progress.lastName ?? string.Empty).Trim();
            if (first.Length == 0) return "?";
            return last.Length > 0 ? first + " " + last.Substring(0, 1) + "." : first;
        }

        public static void SplitName(string name, out string first, out string last)
        {
            name = (name ?? string.Empty).Trim();
            int space = name.IndexOf(' ');
            first = space > 0 ? name.Substring(0, space) : name;
            last = space > 0 ? name.Substring(space + 1).Trim() : string.Empty;
        }

        private void SetState(CloudState state)
        {
            State = state;
            if (StateChanged != null) StateChanged(state);
        }
    }
}
