using System;
using System.Collections;
using UnityEngine;

namespace DressSort
{
    /// <summary>
    /// 游戏圈每日发帖：wx.getGameClubData 拿加密的「今日发帖数」，
    /// 云函数用登录时存下的 session_key 解开。session_key 过期或还没存就重登一次再拉。
    /// 编辑器里没有游戏圈，当作已发帖，方便验领奖。
    /// </summary>
    public sealed class GameClubService : MonoBehaviour
    {
        const int DailyPostType = 6;

        [Serializable]
        sealed class DailyBody
        {
            public string encryptedData;
            public string iv;
        }

        static GameClubService _it;
        static bool _hooked;

        public static event Action Shown;

        static GameClubService It
        {
            get
            {
                if (_it != null) return _it;
                var go = new GameObject("GameClubService");
                if (Application.isPlaying) DontDestroyOnLoad(go);
                _it = go.AddComponent<GameClubService>();
                return _it;
            }
        }

        /// <summary>从游戏圈切回游戏时通知。只注册一次，面板自己订阅 / 退订 Shown。</summary>
        public static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            WxBridge.OnShow(() => Shown?.Invoke());
        }

        /// <summary>done(今日发帖数, err)：err 为 null 表示查到了。</summary>
        public static void DailyPosts(Action<int, string> done)
        {
            if (!WxBridge.CanUseClub)
            {
                if (Application.isEditor) done(1, null);
                else done(0, "not wx");
                return;
            }
            It.StartCoroutine(It.Fetch(done));
        }

        IEnumerator Fetch(Action<int, string> done)
        {
            if (!WxBridge.PrivacyAgreed)
            {
                bool settled = false, agreed = false;
                WxBridge.EnsurePrivacy(ok => { agreed = ok; settled = true; });
                float privacyUntil = Time.realtimeSinceStartup + 20f;
                while (!settled && Time.realtimeSinceStartup < privacyUntil) yield return null;
                if (!agreed)
                {
                    done(0, "需同意隐私协议后才能同步游戏圈发帖");
                    yield break;
                }
            }
            for (int attempt = 0; attempt < 2; attempt++)
            {
                string enc = null, iv = null, err = null;
                bool back = false;
                WxBridge.GameClubData(DailyPostType, (e, i, x) => { enc = e; iv = i; err = x; back = true; });
                float until = Time.realtimeSinceStartup + CloudConfig.RequestTimeoutSec;
                while (!back && Time.realtimeSinceStartup < until) yield return null;
                if (!back) { done(0, "getGameClubData timeout"); yield break; }
                if (err != null) { done(0, err); yield break; }

                CloudReply reply = null;
                string callErr = null;
                string json = JsonUtility.ToJson(new DailyBody { encryptedData = enc, iv = iv });
                yield return Backend.Call(CloudConfig.GameClubDailyPath, json, (s, r, e) => { reply = r; callErr = e; });
                if (callErr == null)
                {
                    done(reply.data != null ? reply.data.postCount : 0, null);
                    yield break;
                }
                string code = reply != null ? reply.code : null;
                bool stale = code == "NO_WX_SESSION" || code == "DECRYPT_FAIL";
                if (!stale || attempt > 0) { done(0, callErr); yield break; }

                // 先重登换新 session_key，再重新拉加密数据；顺序反了新数据还是旧 key 加的密。
                Backend.ClearToken();
                string loginErr = null;
                yield return Backend.EnsureToken(e => loginErr = e);
                if (loginErr != null) { done(0, loginErr); yield break; }
            }
        }
    }
}
