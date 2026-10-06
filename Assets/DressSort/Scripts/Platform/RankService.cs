using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace DressSort
{
    /// <summary>
    /// 通关排行榜：每次通关报一次已通关关数，同样的「关数 + 昵称头像」只报一次；
    /// 打开榜单先把没报上去的补报，再拉前 N 名和自己的名次。
    /// 服务端只升不降，本地清档不会把榜上成绩拉下来。
    /// </summary>
    public sealed class RankService : MonoBehaviour
    {
        [Serializable]
        sealed class SubmitBody
        {
            public int cleared;
            public string displayName;
            public string avatarUrl;
        }

        [Serializable]
        sealed class ListBody
        {
            public int limit;
        }

        [Serializable]
        sealed class Profile
        {
            public string nick;
            public string avatar;
        }

        public sealed class Board
        {
            public RankRow[] List;
            public RankRow Mine;
        }

        static RankService _it;
        static Profile _profile;
        readonly Dictionary<string, Sprite> _avatars = new Dictionary<string, Sprite>();
        readonly Dictionary<string, Action<Sprite>> _avatarWait = new Dictionary<string, Action<Sprite>>();
        bool _sending;

        static RankService It
        {
            get
            {
                if (_it != null) return _it;
                var go = new GameObject("RankService");
                if (Application.isPlaying) DontDestroyOnLoad(go);
                _it = go.AddComponent<RankService>();
                return _it;
            }
        }

        public static bool HasProfile => !string.IsNullOrEmpty(LoadProfile().nick);
        public static string Nick => LoadProfile().nick ?? "";
        public static string Avatar => LoadProfile().avatar ?? "";

        static Profile LoadProfile()
        {
            if (_profile != null) return _profile;
            string raw = PlayerPrefs.GetString(CloudConfig.ProfileKey, "");
            try { _profile = string.IsNullOrEmpty(raw) ? null : JsonUtility.FromJson<Profile>(raw); }
            catch (Exception) { _profile = null; }
            if (_profile == null) _profile = new Profile();
            return _profile;
        }

        /// <summary>微信拒绝授权也会回一个「微信用户」+ 灰头像，当没给。</summary>
        public static bool SetProfile(string nick, string avatar)
        {
            nick = (nick ?? "").Trim();
            if (nick.Length == 0 || nick == "微信用户") return false;
            _profile = new Profile { nick = nick, avatar = avatar ?? "" };
            PlayerPrefs.SetString(CloudConfig.ProfileKey, JsonUtility.ToJson(_profile));
            PlayerPrefs.Save();
            return true;
        }

        static string SentKey(int cleared) => cleared + ":" + Nick + ":" + Avatar;

        public static void Submit(int cleared)
        {
            if (cleared <= 0 || !Application.isPlaying) return;
            if (PlayerPrefs.GetString(CloudConfig.RankSentKey, "") == SentKey(cleared)) return;
            It.StartCoroutine(It.SubmitRoutine(cleared));
        }

        public static void Load(int cleared, Action<Board, string> done)
        {
            It.StartCoroutine(It.LoadRoutine(cleared, done));
        }

        IEnumerator SubmitRoutine(int cleared)
        {
            while (_sending) yield return null;
            string key = SentKey(cleared);
            if (cleared <= 0 || PlayerPrefs.GetString(CloudConfig.RankSentKey, "") == key) yield break;
            _sending = true;
            var body = new SubmitBody { cleared = cleared, displayName = Nick, avatarUrl = Avatar };
            string err = null;
            yield return Backend.Call(CloudConfig.RankSubmitPath, JsonUtility.ToJson(body), (s, r, e) => err = e);
            _sending = false;
            if (err == null)
            {
                PlayerPrefs.SetString(CloudConfig.RankSentKey, key);
                PlayerPrefs.Save();
            }
            else Debug.LogWarning("[Rank] submit failed: " + err);
        }

        IEnumerator LoadRoutine(int cleared, Action<Board, string> done)
        {
            yield return SubmitRoutine(cleared);
            CloudReply reply = null;
            string err = null;
            var body = new ListBody { limit = CloudConfig.RankListLimit };
            yield return Backend.Call(CloudConfig.RankListPath, JsonUtility.ToJson(body), (s, r, e) => { reply = r; err = e; });
            if (err != null || reply == null || reply.data == null)
            {
                done(null, err ?? "empty reply");
                yield break;
            }
            // JsonUtility 不会把缺省的对象字段留成 null，没上榜时 mine 是个 rank 为 0 的空壳。
            RankRow mine = reply.data.mine != null && reply.data.mine.rank > 0 ? reply.data.mine : null;
            done(new Board { List = reply.data.list ?? new RankRow[0], Mine = mine }, null);
        }

        /// <summary>头像按 URL 缓存。微信头像域名要加进小游戏后台的 downloadFile 合法域名。</summary>
        public static void AvatarOf(string url, Action<Sprite> done)
        {
            if (string.IsNullOrEmpty(url)) { done(null); return; }
            var s = It;
            if (s._avatars.TryGetValue(url, out Sprite hit)) { done(hit); return; }
            if (s._avatarWait.ContainsKey(url)) { s._avatarWait[url] += done; return; }
            s._avatarWait[url] = done;
            s.StartCoroutine(s.FetchAvatar(url));
        }

        IEnumerator FetchAvatar(string url)
        {
            Sprite sprite = null;
            using (var req = UnityWebRequestTexture.GetTexture(url, true))
            {
                req.timeout = CloudConfig.RequestTimeoutSec;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    Texture2D tex = DownloadHandlerTexture.GetContent(req);
                    if (tex != null)
                    {
                        tex.wrapMode = TextureWrapMode.Clamp;
                        sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    }
                }
            }
            if (sprite != null) _avatars[url] = sprite;
            if (_avatarWait.TryGetValue(url, out Action<Sprite> wait))
            {
                _avatarWait.Remove(url);
                wait?.Invoke(sprite);
            }
        }
    }
}
