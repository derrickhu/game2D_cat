using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace DressSort
{
    /// <summary>
    /// 标准经分。POST 到 analytics-ingest/track，信封和 @gp/analytics-sdk 一致。
    /// game_key 用 CloudConfig.GameKey（dresssort）。编辑器里不发。
    /// 先登录拿到 user_id 再记 session_start，避免一次启动被算成两个人。
    /// </summary>
    public static class Analytics
    {
        const string Endpoint = "/analytics-ingest/track";
        const string AnonKey = "dresssort_ga_anon";
        const string QueueKey = "dresssort_ga_queue";
        const int FlushSeconds = 15;
        const int MaxBatch = 20;
        const int MaxQueue = 200;

        static string _anon;
        static string _user;
        static string _session;
        static string _levelId = "";
        static string _levelName = "";
        static bool _ready;
        static bool _flushing;
        static float _nextFlush;
        static readonly List<string> _queue = new List<string>();

        public static void Ensure()
        {
            if (_ready || Application.isEditor) return;
            _ready = true;
            _anon = PlayerPrefs.GetString(AnonKey, "");
            if (string.IsNullOrEmpty(_anon))
            {
                _anon = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(AnonKey, _anon);
            }
            LoadQueue();
            _session = Guid.NewGuid().ToString("N");
            var host = new GameObject("DressSortAnalytics");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<AnalyticsHost>();
            WxBridge.OnHide(() =>
            {
                SessionEnd("hide");
                Flush();
            });
        }

        /// <summary>登录完成后调用。userId 为空也记一次 session_start。</summary>
        public static void BindUser(string userId)
        {
            Ensure();
            if (!_ready || !string.IsNullOrEmpty(_user)) return;
            _user = userId ?? "";
            Track("login", _user.Length > 0 ? "user_set" : "anon");
            SessionStart();
            Flush();
        }

        public static void LevelStart(int level, string name)
        {
            _levelId = level > 0 ? level.ToString() : "";
            _levelName = name ?? "";
            Track("level_start", _levelId, _levelName);
        }

        public static void LevelClear(int stars)
        {
            Track("level_clear", _levelId, _levelName, "stars", stars);
            _levelId = "";
            _levelName = "";
        }

        public static void LevelFail(string reason, float progress)
        {
            int pct = Mathf.Clamp(Mathf.RoundToInt(progress * 100f), 0, 100);
            Track("level_fail", _levelId, _levelName, "reason", reason ?? "", "progress", pct);
            _levelId = "";
            _levelName = "";
        }

        public static void AdRequest(string scene, string adUnitId)
        {
            Track("ad_request", "", "", "ad_type", "reward", "scene", scene ?? "", "ad_unit_id", adUnitId ?? "");
        }

        public static void AdShow(string scene, string adUnitId)
        {
            Track("ad_show", "", "", "ad_type", "reward", "scene", scene ?? "", "ad_unit_id", adUnitId ?? "", "result", "success");
        }

        public static void AdClose(string scene, string adUnitId, bool ended)
        {
            Track("ad_close", "", "", "ad_type", "reward", "scene", scene ?? "", "ad_unit_id", adUnitId ?? "",
                "result", ended ? "ended" : "skipped");
        }

        public static void AdError(string scene, string adUnitId, string message)
        {
            Track("ad_error", "", "", "ad_type", "reward", "scene", scene ?? "", "ad_unit_id", adUnitId ?? "",
                "error_code", message ?? "");
        }

        static void SessionStart()
        {
            Track("session_start", "", "", "session_id", _session);
        }

        static void SessionEnd(string reason)
        {
            if (string.IsNullOrEmpty(_session)) return;
            Track("session_end", "", "", "session_id", _session, "reason", reason ?? "");
            _session = "";
        }

        static void Track(string eventName, string levelId, string levelName, params object[] extras)
        {
            if (!_ready) return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var sb = new StringBuilder(256);
            sb.Append("{\"event_id\":\"").Append(Guid.NewGuid().ToString("N")).Append('"');
            sb.Append(",\"event_name\":\"").Append(eventName).Append('"');
            sb.Append(",\"event_time\":").Append(now);
            sb.Append(",\"game_key\":\"").Append(CloudConfig.GameKey).Append('"');
            sb.Append(",\"user_id\":\"").Append(Esc(_user)).Append('"');
            sb.Append(",\"anon_id\":\"").Append(_anon).Append('"');
            sb.Append(",\"session_id\":\"").Append(_session ?? "").Append('"');
            sb.Append(",\"platform\":\"wechat\"");
            if (!string.IsNullOrEmpty(levelId))
                sb.Append(",\"level_id\":\"").Append(Esc(levelId)).Append('"');
            if (!string.IsNullOrEmpty(levelName))
                sb.Append(",\"level_name\":\"").Append(Esc(levelName)).Append('"');
            if (extras != null && extras.Length >= 2)
            {
                sb.Append(",\"params\":{");
                bool first = true;
                for (int i = 0; i + 1 < extras.Length; i += 2)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append('"').Append(extras[i]).Append("\":");
                    object v = extras[i + 1];
                    if (v is int || v is long || v is float || v is double)
                        sb.Append(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture));
                    else
                        sb.Append('"').Append(Esc(v != null ? v.ToString() : "")).Append('"');
                }
                sb.Append('}');
            }
            sb.Append('}');
            _queue.Add(sb.ToString());
            while (_queue.Count > MaxQueue) _queue.RemoveAt(0);
            SaveQueue();
        }

        public static void Flush()
        {
            if (!_ready || _flushing || _queue.Count == 0) return;
            AnalyticsHost.Run(Send(_queue.Count < MaxBatch ? _queue.Count : MaxBatch));
        }

        static IEnumerator Send(int count)
        {
            _flushing = true;
            var batch = new StringBuilder(count * 280);
            batch.Append("{\"batch\":[");
            for (int i = 0; i < count; i++)
            {
                if (i > 0) batch.Append(',');
                batch.Append(_queue[i]);
            }
            batch.Append("]}");
            string body = batch.ToString();
            using (var req = new UnityWebRequest(CloudConfig.BaseUrl + Endpoint, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 8;
                yield return req.SendWebRequest();
                bool ok = req.result == UnityWebRequest.Result.Success && req.responseCode >= 200 && req.responseCode < 300;
                if (ok)
                {
                    _queue.RemoveRange(0, count);
                    SaveQueue();
                }
            }
            _flushing = false;
            _nextFlush = Time.realtimeSinceStartup + FlushSeconds;
        }

        static void LoadQueue()
        {
            string raw = PlayerPrefs.GetString(QueueKey, "");
            if (string.IsNullOrEmpty(raw)) return;
            int i = 0;
            while (i < raw.Length)
            {
                int end = raw.IndexOf('\n', i);
                if (end < 0) end = raw.Length;
                if (end > i) _queue.Add(raw.Substring(i, end - i));
                i = end + 1;
            }
        }

        static void SaveQueue()
        {
            PlayerPrefs.SetString(QueueKey, string.Join("\n", _queue));
            PlayerPrefs.Save();
        }

        static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        sealed class AnalyticsHost : MonoBehaviour
        {
            static AnalyticsHost _host;

            void Awake()
            {
                _host = this;
            }

            void Update()
            {
                if (Time.realtimeSinceStartup < _nextFlush) return;
                _nextFlush = Time.realtimeSinceStartup + FlushSeconds;
                Flush();
            }

            void OnApplicationPause(bool paused)
            {
                if (!paused) return;
                SessionEnd("pause");
                Flush();
            }

            public static void Run(IEnumerator routine)
            {
                if (_host != null) _host.StartCoroutine(routine);
            }
        }
    }
}
