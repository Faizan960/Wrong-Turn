using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace WrongDirection.Leaderboards
{
    /// <summary>
    /// Firebase Anonymous Auth transport (REST, no SDK) + an authenticated HTTP
    /// helper for calling the Cloudflare Worker. This class ONLY does Firebase
    /// identity (anonymous sign-in + token refresh) and attaches the current ID
    /// token as a Bearer to Worker requests. It knows nothing about leaderboards
    /// or D1 — CloudLeaderboardProvider maps calls to the Worker API. Callbacks
    /// fire on the main thread (UnityWebRequest async op).
    /// </summary>
    public sealed class FirebaseRestClient
    {
        private readonly LeaderboardConfig _cfg;

        private string _idToken;
        private string _refreshToken;
        private string _uid;
        private DateTime _expiryUtc = DateTime.MinValue;

        public FirebaseRestClient(LeaderboardConfig cfg) { _cfg = cfg; }

        public string Uid => _uid;
        public string RefreshToken => _refreshToken;
        public bool IsAuthed => !string.IsNullOrEmpty(_idToken) && !string.IsNullOrEmpty(_uid);

        private string IdentityBase => "https://identitytoolkit.googleapis.com/v1/accounts:";
        private string SecureTokenUrl => "https://securetoken.googleapis.com/v1/token?key=" + _cfg.webApiKey;

        // -------------------------------------------------------------------
        // Anonymous auth (restore saved account, else create a fresh one)
        // -------------------------------------------------------------------
        public void InitializeAnonymous(string savedRefreshToken, Action<bool> onDone)
        {
            if (!string.IsNullOrEmpty(savedRefreshToken))
                RefreshIdToken(savedRefreshToken, ok => { if (ok) onDone?.Invoke(true); else SignUpAnonymous(onDone); });
            else SignUpAnonymous(onDone);
        }

        private void SignUpAnonymous(Action<bool> onDone)
        {
            string url = IdentityBase + "signUp?key=" + _cfg.webApiKey;
            Post(url, "{\"returnSecureToken\":true}", null, (ok, code, text) =>
            {
                if (!ok) { LogFail("signUp", code, text); onDone?.Invoke(false); return; }
                var d = MiniJson.AsDict(MiniJson.Deserialize(text));
                _idToken = MiniJson.GetString(d, "idToken");
                _refreshToken = MiniJson.GetString(d, "refreshToken");
                _uid = MiniJson.GetString(d, "localId");
                SetExpiry(MiniJson.GetString(d, "expiresIn"));
                onDone?.Invoke(IsAuthed);
            });
        }

        private void RefreshIdToken(string refreshToken, Action<bool> onDone)
        {
            string body = "grant_type=refresh_token&refresh_token=" + UnityWebRequest.EscapeURL(refreshToken);
            PostForm(SecureTokenUrl, body, (ok, code, text) =>
            {
                if (!ok) { onDone?.Invoke(false); return; }
                var d = MiniJson.AsDict(MiniJson.Deserialize(text));
                _idToken = MiniJson.GetString(d, "id_token");
                _refreshToken = MiniJson.GetString(d, "refresh_token") ?? refreshToken;
                _uid = MiniJson.GetString(d, "user_id");
                SetExpiry(MiniJson.GetString(d, "expires_in"));
                onDone?.Invoke(IsAuthed);
            });
        }

        private void SetExpiry(string expiresInSeconds)
        {
            int secs = int.TryParse(expiresInSeconds, out var s) ? s : 3600;
            _expiryUtc = DateTime.UtcNow.AddSeconds(secs);
        }

        private void EnsureFreshToken(Action<bool> ready)
        {
            if (IsAuthed && DateTime.UtcNow < _expiryUtc.AddSeconds(-60)) { ready(true); return; }
            if (!string.IsNullOrEmpty(_refreshToken)) RefreshIdToken(_refreshToken, ready);
            else ready(false);
        }

        // -------------------------------------------------------------------
        // Authenticated Worker request (attaches Bearer ID token)
        // onDone(ok, httpStatus, responseText)
        // -------------------------------------------------------------------
        public void AuthedRequest(string method, string url, string jsonBody,
            Action<bool, long, string> onDone)
        {
            EnsureFreshToken(ok =>
            {
                if (!ok) { onDone?.Invoke(false, 0, "NOT_AUTHENTICATED"); return; }
                Send(method, url, jsonBody, _idToken, onDone);
            });
        }

        // -------------------------------------------------------------------
        // HTTP helpers (main-thread completion; no coroutine needed)
        // -------------------------------------------------------------------
        private static void Send(string method, string url, string json, string bearer,
            Action<bool, long, string> cb)
        {
            var req = new UnityWebRequest(url, method);
            if (!string.IsNullOrEmpty(json))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.SetRequestHeader("Content-Type", "application/json");
            }
            req.downloadHandler = new DownloadHandlerBuffer();
            if (!string.IsNullOrEmpty(bearer)) req.SetRequestHeader("Authorization", "Bearer " + bearer);
            req.timeout = 15;
            var op = req.SendWebRequest();
            op.completed += _ =>
            {
                bool ok = req.result == UnityWebRequest.Result.Success;
                long code = req.responseCode;
                string text = req.downloadHandler != null ? req.downloadHandler.text : null;
                req.Dispose();
                cb(ok, code, text);
            };
        }

        private static void Post(string url, string json, string bearer, Action<bool, long, string> cb) =>
            Send("POST", url, json, bearer, cb);

        private static void PostForm(string url, string formBody, Action<bool, long, string> cb)
        {
            var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(formBody));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
            req.timeout = 15;
            var op = req.SendWebRequest();
            op.completed += _ =>
            {
                bool ok = req.result == UnityWebRequest.Result.Success;
                long code = req.responseCode;
                string text = req.downloadHandler != null ? req.downloadHandler.text : null;
                req.Dispose();
                cb(ok, code, text);
            };
        }

        private static void LogFail(string what, long code, string text) =>
            Debug.LogWarning($"[FirebaseAuth] {what} failed ({code}): {(string.IsNullOrEmpty(text) ? "" : (text.Length > 300 ? text.Substring(0, 300) : text))}");
    }
}
