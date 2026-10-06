using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace DressSort
{
    [Serializable]
    public sealed class RankRow
    {
        public int rank;
        public int cleared;
        public string displayName;
        public string avatarUrl;
        public bool isMe;
    }

    /// <summary>各接口的 data 字段并在一起：login 用 token 那几个，rank/list 用 list/mine，游戏圈用 postCount。</summary>
    [Serializable]
    public sealed class CloudData
    {
        public string token;
        public string userId;
        public string platform;
        public long expiresAt;
        public RankRow[] list;
        public RankRow mine;
        public int postCount;
    }

    [Serializable]
    public sealed class CloudReply
    {
        public bool ok;
        public string code;
        public string error;
        public CloudData data;
    }

    /// <summary>
    /// 登录拿 JWT，之后每次请求带 Bearer；401 清 token 重登一次。
    /// 真机走 wx.login 的 code，编辑器等其它环境用本机匿名 ID。
    /// </summary>
    public static class Backend
    {
        [Serializable]
        sealed class LoginBody
        {
            public string platform;
            public string code;
            public string anonId;
        }

        [Serializable]
        sealed class StoredToken
        {
            public string token;
            public string userId;
            public string platform;
            public long expiresAt;
        }

        static StoredToken _tok;

        static bool UseWx => WxBridge.IsMiniGame && !Application.isEditor;
        static string PlatformCode => UseWx ? "wx" : "anon";

        static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>done(null) 表示拿到了有效 token，否则是失败原因。</summary>
        public static IEnumerator EnsureToken(Action<string> done)
        {
            long now = NowMs();
            if (_tok != null && _tok.expiresAt - now > 60000L) { done(null); yield break; }
            StoredToken cached = LoadToken();
            if (cached != null && cached.expiresAt - now > 60000L)
            {
                _tok = cached;
                done(null);
                yield break;
            }

            var body = new LoginBody { platform = PlatformCode };
            if (UseWx)
            {
                string code = null, fail = null;
                bool back = false;
                WxBridge.Login(c => { code = c; back = true; }, e => { fail = e; back = true; });
                float until = Time.realtimeSinceStartup + CloudConfig.RequestTimeoutSec;
                while (!back && Time.realtimeSinceStartup < until) yield return null;
                if (string.IsNullOrEmpty(code))
                {
                    done("wx.login gave no code: " + (fail ?? "timeout"));
                    yield break;
                }
                body.code = code;
            }
            else body.anonId = AnonId();

            long status = 0;
            CloudReply reply = null;
            yield return Post(CloudConfig.LoginPath, JsonUtility.ToJson(body), null, (s, r) => { status = s; reply = r; });
            if (status != 200 || reply == null || !reply.ok || reply.data == null || string.IsNullOrEmpty(reply.data.token))
            {
                done(Describe(status, reply));
                yield break;
            }
            _tok = new StoredToken
            {
                token = reply.data.token,
                userId = reply.data.userId ?? "",
                platform = string.IsNullOrEmpty(reply.data.platform) ? body.platform : reply.data.platform,
                expiresAt = reply.data.expiresAt
            };
            PlayerPrefs.SetString(CloudConfig.TokenKey, JsonUtility.ToJson(_tok));
            PlayerPrefs.Save();
            done(null);
        }

        /// <summary>done(status, reply, err)：err 为 null 表示 200 且 ok。</summary>
        public static IEnumerator Call(string path, string json, Action<long, CloudReply, string> done)
        {
            string err = null;
            yield return EnsureToken(e => err = e);
            if (err != null) { done(0, null, err); yield break; }

            long status = 0;
            CloudReply reply = null;
            yield return Post(path, json, _tok.token, (s, r) => { status = s; reply = r; });
            if (status == 401)
            {
                ClearToken();
                yield return EnsureToken(e => err = e);
                if (err != null) { done(401, reply, err); yield break; }
                yield return Post(path, json, _tok.token, (s, r) => { status = s; reply = r; });
            }
            bool ok = status == 200 && reply != null && reply.ok;
            done(status, reply, ok ? null : Describe(status, reply));
        }

        public static void ClearToken()
        {
            _tok = null;
            PlayerPrefs.DeleteKey(CloudConfig.TokenKey);
        }

        static IEnumerator Post(string path, string json, string token, Action<long, CloudReply> done)
        {
            using (var req = new UnityWebRequest(CloudConfig.BaseUrl + path, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(token)) req.SetRequestHeader("Authorization", "Bearer " + token);
                req.timeout = CloudConfig.RequestTimeoutSec;
                yield return req.SendWebRequest();

                CloudReply reply = null;
                string text = req.downloadHandler != null ? req.downloadHandler.text : null;
                if (!string.IsNullOrEmpty(text))
                {
                    try { reply = JsonUtility.FromJson<CloudReply>(text); }
                    catch (Exception) { reply = null; }
                }
                done(req.responseCode, reply);
            }
        }

        static StoredToken LoadToken()
        {
            string raw = PlayerPrefs.GetString(CloudConfig.TokenKey, "");
            if (string.IsNullOrEmpty(raw)) return null;
            StoredToken t;
            try { t = JsonUtility.FromJson<StoredToken>(raw); }
            catch (Exception) { return null; }
            if (t == null || string.IsNullOrEmpty(t.token)) return null;
            // 同一台设备换了宿主（编辑器 anon ↔ 真机 wx）时旧 token 带的是别的身份，丢掉重登。
            if (!string.IsNullOrEmpty(t.platform) && t.platform != PlatformCode) return null;
            return t;
        }

        static string AnonId()
        {
            string id = PlayerPrefs.GetString(CloudConfig.AnonKey, "");
            if (!string.IsNullOrEmpty(id)) return id;
            id = "anon_" + NowMs().ToString("x") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            PlayerPrefs.SetString(CloudConfig.AnonKey, id);
            PlayerPrefs.Save();
            return id;
        }

        static string Describe(long status, CloudReply reply)
        {
            if (reply == null) return "HTTP " + status;
            return (reply.code ?? ("HTTP " + status)) + " " + (reply.error ?? "");
        }
    }
}
