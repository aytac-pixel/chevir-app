using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Cevir.Core.Cloud
{
    /// <summary>Result of one HTTP call: status, body and a learner-facing error message (Azerbaijani / English).</summary>
    public sealed class CloudResponse
    {
        public long Status;
        public string Body;
        public string Error;
        public bool Ok => string.IsNullOrEmpty(Error) && Status >= 200 && Status < 300;
        public bool IsNetworkError;
    }

    /// <summary>
    /// Minimal Supabase client over UnityWebRequest: e-mail + password accounts (Auth) and table / function calls
    /// (PostgREST). The session (tokens) is kept in PlayerPrefs; the access token is refreshed before it expires.
    /// No SDK is needed, so the project has no extra package to install.
    /// </summary>
    public sealed class SupabaseClient
    {
        private const string PrefsKey = "cevir.auth.session";
        private const int TimeoutSeconds = 20;

        [Serializable]
        private sealed class StoredSession
        {
            public string accessToken;
            public string refreshToken;
            public long expiresAtUnix;
            public string userId;
            public string email;
        }

        private readonly CloudConfig _config;
        private readonly MonoBehaviour _host;
        private StoredSession _session;
        private bool _refreshing;
        private readonly List<Action<bool>> _refreshWaiters = new List<Action<bool>>();

        public SupabaseClient(CloudConfig config, MonoBehaviour host)
        {
            _config = config;
            _host = host;
            LoadSession();
        }

        public bool HasSession => _session != null && !string.IsNullOrEmpty(_session.refreshToken);
        public string UserId => _session != null ? _session.userId : null;
        public string Email => _session != null ? _session.email : null;

        // ------------------------------------------------------------------ auth

        /// <summary>
        /// Creates an account. <paramref name="done"/> gets (response, signedIn): signedIn is false when the project
        /// requires e-mail confirmation first (the learner then confirms and signs in).
        /// </summary>
        public void SignUp(string email, string password, string displayName, string nameSign, bool dataConsent,
            Action<CloudResponse, bool> done)
        {
            var body = new SignUpRequest
            {
                email = email,
                password = password,
                data = new SignUpData { display_name = displayName, name_sign = nameSign ?? string.Empty, data_consent = dataConsent }
            };
            Send("POST", "/auth/v1/signup", JsonUtility.ToJson(body), null, false, response =>
            {
                bool signedIn = response.Ok && ApplyAuth(response.Body);
                done(response, signedIn);
            });
        }

        public void SignIn(string email, string password, Action<CloudResponse> done)
        {
            var body = new PasswordGrant { email = email, password = password };
            Send("POST", "/auth/v1/token?grant_type=password", JsonUtility.ToJson(body), null, false, response =>
            {
                if (response.Ok && !ApplyAuth(response.Body)) response.Error = CloudErrors.Unknown;
                done(response);
            });
        }

        public void SendPasswordReset(string email, Action<CloudResponse> done)
        {
            Send("POST", "/auth/v1/recover", JsonUtility.ToJson(new EmailOnly { email = email }), null, false, done);
        }

        /// <summary>Signs in with the 6-digit code from the password reset e-mail (the learner then sets a new password).</summary>
        public void VerifyRecoveryCode(string email, string code, Action<CloudResponse> done)
        {
            var body = new VerifyRequest { type = "recovery", email = email, token = code };
            Send("POST", "/auth/v1/verify", JsonUtility.ToJson(body), null, false, response =>
            {
                if (response.Ok && !ApplyAuth(response.Body)) response.Error = CloudErrors.Unknown;
                done(response);
            });
        }

        /// <summary>Changes the signed-in user's password and / or e-mail (PUT /auth/v1/user).</summary>
        public void UpdateUser(string json, Action<CloudResponse> done)
        {
            Authorized(() => Send("PUT", "/auth/v1/user", json, null, true, response =>
            {
                if (response.Ok && _session != null)
                {
                    AuthUser user = Parse<AuthUser>(response.Body);
                    if (user != null && !string.IsNullOrEmpty(user.email) && user.email != _session.email)
                    {
                        _session.email = user.email;
                        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(_session));
                        PlayerPrefs.Save();
                    }
                }

                done(response);
            }), done);
        }

        public void SignOut()
        {
            if (HasSession)
            {
                // Best effort: revoke the refresh token on the server too.
                Send("POST", "/auth/v1/logout", "{}", null, true, r => { });
            }

            _session = null;
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        /// <summary>Makes sure the access token is valid (refreshing it when it expires within a minute).</summary>
        public void EnsureFreshToken(Action<bool> done)
        {
            if (!HasSession)
            {
                done(false);
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (_session.expiresAtUnix - now > 60)
            {
                done(true);
                return;
            }

            _refreshWaiters.Add(done);
            if (_refreshing) return;
            _refreshing = true;

            var body = new RefreshGrant { refresh_token = _session.refreshToken };
            Send("POST", "/auth/v1/token?grant_type=refresh_token", JsonUtility.ToJson(body), null, false, response =>
            {
                _refreshing = false;
                bool ok = response.Ok && ApplyAuth(response.Body);

                // A rejected refresh token means the session is over (signed out elsewhere, user deleted).
                if (!ok && !response.IsNetworkError && response.Status >= 400 && response.Status < 500)
                {
                    _session = null;
                    PlayerPrefs.DeleteKey(PrefsKey);
                    PlayerPrefs.Save();
                }

                var waiters = new List<Action<bool>>(_refreshWaiters);
                _refreshWaiters.Clear();
                foreach (Action<bool> waiter in waiters) waiter(ok);
            });
        }

        private bool ApplyAuth(string json)
        {
            AuthResponse auth = Parse<AuthResponse>(json);
            if (auth == null || string.IsNullOrEmpty(auth.access_token) || auth.user == null) return false;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _session = new StoredSession
            {
                accessToken = auth.access_token,
                refreshToken = auth.refresh_token,
                expiresAtUnix = auth.expires_at > 0 ? auth.expires_at : now + Math.Max(60, auth.expires_in),
                userId = auth.user.id,
                email = auth.user.email
            };
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(_session));
            PlayerPrefs.Save();
            return true;
        }

        private void LoadSession()
        {
            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            _session = string.IsNullOrEmpty(json) ? null : Parse<StoredSession>(json);
        }

        // ------------------------------------------------------------------ database

        /// <summary>GET /rest/v1/&lt;pathAndQuery&gt; as the signed-in user.</summary>
        public void Select(string pathAndQuery, Action<CloudResponse> done)
        {
            Authorized(() => Send("GET", "/rest/v1/" + pathAndQuery, null, null, true, done), done);
        }

        /// <summary>Insert or update rows (POST with merge-duplicates) as the signed-in user.</summary>
        public void Upsert(string table, string onConflict, string json, Action<CloudResponse> done)
        {
            var headers = new Dictionary<string, string> { { "Prefer", "resolution=merge-duplicates,return=minimal" } };
            Authorized(() => Send("POST", "/rest/v1/" + table + "?on_conflict=" + onConflict, json, headers, true, done), done);
        }

        /// <summary>Inserts one row (POST, no upsert) as the signed-in user.</summary>
        public void Insert(string table, string json, Action<CloudResponse> done)
        {
            var headers = new Dictionary<string, string> { { "Prefer", "return=minimal" } };
            Authorized(() => Send("POST", "/rest/v1/" + table, json, headers, true, done), done);
        }

        /// <summary>Deletes the rows matching a PostgREST filter, e.g. "user_id=eq.&lt;id&gt;".</summary>
        public void DeleteRows(string table, string filter, Action<CloudResponse> done)
        {
            Authorized(() => Send("DELETE", "/rest/v1/" + table + "?" + filter, null, null, true, done), done);
        }

        /// <summary>Calls a database function (POST /rest/v1/rpc/&lt;name&gt;).</summary>
        public void Rpc(string function, string json, Action<CloudResponse> done)
        {
            Authorized(() => Send("POST", "/rest/v1/rpc/" + function, json ?? "{}", null, true, done), done);
        }

        /// <summary>Uploads (or replaces) a file in a storage bucket as the signed-in user.</summary>
        public void UploadFile(string bucket, string path, byte[] data, string contentType, Action<CloudResponse> done)
        {
            UploadFile(bucket, path, data, contentType, true, done);
        }

        /// <summary>Uploads a file; with <paramref name="replace"/> false an existing file of that name is an error.</summary>
        public void UploadFile(string bucket, string path, byte[] data, string contentType, bool replace, Action<CloudResponse> done)
        {
            var headers = new Dictionary<string, string> { { "x-upsert", replace ? "true" : "false" }, { "Cache-Control", "max-age=60" } };
            Authorized(() => SendRaw("POST", "/storage/v1/object/" + bucket + "/" + path, data, contentType, headers, true, done), done);
        }

        public void DeleteFile(string bucket, string path, Action<CloudResponse> done)
        {
            Authorized(() => Send("DELETE", "/storage/v1/object/" + bucket + "/" + path, null, null, true, done), done);
        }

        /// <summary>Deletes several files of a bucket in one call (paths inside the bucket).</summary>
        public void DeleteFiles(string bucket, IList<string> paths, Action<CloudResponse> done)
        {
            var body = new StringBuilder("{\"prefixes\":[");
            for (int i = 0; i < paths.Count; i++)
            {
                if (i > 0) body.Append(',');
                body.Append('"').Append(paths[i].Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
            }

            body.Append("]}");
            Authorized(() => Send("DELETE", "/storage/v1/object/" + bucket, body.ToString(), null, true, done), done);
        }

        /// <summary>Address of a file in a public bucket (no token needed to read it).</summary>
        public string PublicFileUrl(string bucket, string path)
        {
            return _config.BaseUrl + "/storage/v1/object/public/" + bucket + "/" + path;
        }

        private void Authorized(Action call, Action<CloudResponse> fail)
        {
            EnsureFreshToken(ok =>
            {
                if (ok) call();
                else fail(new CloudResponse { Status = 401, Error = CloudErrors.SignedOut });
            });
        }

        // ------------------------------------------------------------------ transport

        private void Send(string method, string path, string json, Dictionary<string, string> headers, bool asUser, Action<CloudResponse> done)
        {
            SendRaw(method, path, json != null ? Encoding.UTF8.GetBytes(json) : null, "application/json", headers, asUser, done);
        }

        private void SendRaw(string method, string path, byte[] body, string contentType, Dictionary<string, string> headers, bool asUser,
            Action<CloudResponse> done)
        {
            if (_host == null || !_host.isActiveAndEnabled)
            {
                done(new CloudResponse { Error = CloudErrors.Network, IsNetworkError = true });
                return;
            }

            _host.StartCoroutine(SendRoutine(method, path, body, contentType, headers, asUser, done));
        }

        private IEnumerator SendRoutine(string method, string path, byte[] body, string contentType, Dictionary<string, string> headers,
            bool asUser, Action<CloudResponse> done)
        {
            using (var request = new UnityWebRequest(_config.BaseUrl + path, method))
            {
                if (body != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(body);
                    request.SetRequestHeader("Content-Type", contentType);
                }

                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = TimeoutSeconds;
                request.SetRequestHeader("apikey", _config.anonKey);
                string token = asUser && _session != null && !string.IsNullOrEmpty(_session.accessToken) ? _session.accessToken : _config.anonKey;
                request.SetRequestHeader("Authorization", "Bearer " + token);
                if (headers != null)
                {
                    foreach (KeyValuePair<string, string> header in headers) request.SetRequestHeader(header.Key, header.Value);
                }

                yield return request.SendWebRequest();

                var response = new CloudResponse
                {
                    Status = request.responseCode,
                    Body = request.downloadHandler != null ? request.downloadHandler.text : null
                };

                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    response.IsNetworkError = true;
                    response.Error = CloudErrors.Network;
                }
                else if (request.responseCode < 200 || request.responseCode >= 300)
                {
                    response.Error = CloudErrors.FromBody(response.Body, request.responseCode);
                    Debug.LogWarning("[Chevir] Server " + method + " " + path.Split('?')[0] + " -> " + request.responseCode + ": " + response.Body);
                }

                done(response);
            }
        }

        public static T Parse<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// PostgREST returns top-level JSON arrays, which JsonUtility cannot read directly: wraps the array as
        /// {"items": [...]} and reads it into a wrapper class with an "items" list.
        /// </summary>
        public static TWrapper ParseArray<TWrapper>(string json) where TWrapper : class, new()
        {
            if (string.IsNullOrEmpty(json) || json.TrimStart().Length == 0 || json.TrimStart()[0] != '[') return new TWrapper();
            return Parse<TWrapper>("{\"items\":" + json + "}") ?? new TWrapper();
        }
    }
}
