using System;
using System.Collections;
using UnityEngine;

namespace DressSort
{
    /// <summary>
    /// 云存档同步，逻辑照墨字防线 CloudSync：
    /// 启动先登录拉云，按 updatedAt 决定下行覆盖本地还是保留本地待上行，然后才读档进首页；
    /// 本地每次 Save 标脏，防抖上行；切后台、清档立即上行；
    /// 登录或拉云失败 / 超时进 cacheOnly，这时禁止上行，免得新档盖掉云端进度；
    /// 服务端 409 STALE_UPDATE 说明云端更新，改为下行覆盖。
    /// </summary>
    public sealed class CloudSync : MonoBehaviour
    {
        enum Authority { Unknown, Confirmed, CacheOnly }

        [Serializable]
        sealed class SyncMeta
        {
            public long updatedAt;
            public long baseRemote;
            public bool dirty;
        }

        /// <summary>进首页之后云端覆盖了本地存档才会发，内存里的衣柜要重读。</summary>
        public static event Action Imported;

        static CloudSync _it;

        Authority _auth = Authority.Unknown;
        SyncMeta _m;
        bool _booted;
        bool _initDone;
        bool _ready;
        bool _syncing;
        bool _pending;
        bool _disabled;
        bool _hideHooked;
        int _fail;
        float _pushAt = -1f;
        float _retryAt = -1f;

        static CloudSync It
        {
            get
            {
                if (_it != null) return _it;
                var go = new GameObject("CloudSync");
                DontDestroyOnLoad(go);
                _it = go.AddComponent<CloudSync>();
                _it.LoadMeta();
                return _it;
            }
        }

        public static void Startup(Action ready) => It.StartCoroutine(It.RunStartup(ready));

        /// <summary>WardrobeService.Save 调。只改内存和 PlayerPrefs 键值，落盘由调用方 PlayerPrefs.Save。</summary>
        public static void Touch()
        {
            var s = It;
            s._m.updatedAt = Math.Max(Backend.NowMs(), s._m.updatedAt + 1);
            s._m.dirty = true;
            s.WriteMeta();
            s.Schedule("dirty");
        }

        public static void FlushNow(string reason)
        {
            var s = It;
            if (!s._ready || s._auth == Authority.CacheOnly) return;
            s._pushAt = -1f;
            s.StartCoroutine(s.Push(reason, true));
        }

        IEnumerator RunStartup(Action ready)
        {
            HookHide();
            bool done = false;
            StartCoroutine(Initialize(() => done = true));
            float until = Time.realtimeSinceStartup + CloudConfig.StartupTimeout;
            while (!done && Time.realtimeSinceStartup < until) yield return null;
            if (!done) EnterCacheOnly("startup-timeout");
            _booted = true;
            ready();
        }

        IEnumerator Initialize(Action done)
        {
            string err = null;
            yield return Backend.EnsureToken(e => err = e);
            _ready = err == null && !string.IsNullOrEmpty(Backend.UserId);
            Analytics.BindUser(_ready ? Backend.UserId : "");
            if (!_ready)
            {
                Debug.LogWarning("[一裙又一裙] 登录失败，先用本地存档：" + err);
                EnterCacheOnly("login-failed");
            }
            else yield return PullOnStartup();

            _initDone = true;
            done();
            if (_pending && _ready)
            {
                _pending = false;
                Schedule("pending-after-init");
            }
        }

        IEnumerator PullOnStartup()
        {
            CloudReply reply = null;
            string err = null;
            yield return Backend.Call(CloudConfig.PullPath, "{}", (s, r, e) => { reply = r; err = e; });
            if (err != null)
            {
                Debug.LogWarning("[一裙又一裙] 启动拉云失败，先用本地存档：" + err);
                EnterCacheOnly("startup-pull-failed");
                yield break;
            }

            CloudData d = reply.data;
            bool hasLocal = WardrobeService.HasLocal;
            string meta = d != null && d.payload != null ? d.payload.meta : null;
            long remoteAt = d != null ? d.updatedAt : 0L;
            // 云端没档或空档：保留本地并上行。不能按「云端权威」清本地，
            // 真机和开发者工具各有一份本地缓存，一边还没推上去就清另一边，两边永远对不上。
            if (d == null || !d.exists || string.IsNullOrEmpty(meta))
            {
                Confirm();
                if (!hasLocal) yield break;
                if (!_m.dirty) Touch();
                else Schedule("startup-no-remote");
                yield break;
            }

            // 只在本地无档或云端明确更新时下行。
            if (!hasLocal || remoteAt > _m.updatedAt)
            {
                Import(remoteAt, meta, "startup");
                yield break;
            }

            Confirm();
            if (_m.updatedAt > remoteAt && !_m.dirty) Touch();
            else if (_m.dirty) Schedule("startup-local-newer");
        }

        void Schedule(string reason)
        {
            if (_auth == Authority.CacheOnly || !_initDone)
            {
                _pending = true;
                return;
            }
            if (!_ready || _disabled) return;
            float delay = _fail > 0
                ? Mathf.Min(CloudConfig.BaseDelay * Mathf.Pow(2f, _fail - 1), CloudConfig.MaxBackoff)
                : CloudConfig.Debounce;
            _pushAt = Time.realtimeSinceStartup + delay;
        }

        void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (_pushAt > 0f && now >= _pushAt)
            {
                _pushAt = -1f;
                StartCoroutine(Push("debounce", false));
            }
            if (_disabled && _retryAt > 0f && now >= _retryAt)
            {
                _retryAt = now + CloudConfig.RetryInterval;
                if (!_syncing && _m.dirty) StartCoroutine(Push("retry-interval", true));
            }
        }

        IEnumerator Push(string reason, bool force)
        {
            if (!_ready || (!force && _disabled)) yield break;
            if (_auth == Authority.CacheOnly || _syncing)
            {
                _pending = true;
                yield break;
            }
            if (!_m.dirty) yield break;
            string raw = WardrobeService.RawLocal;
            if (string.IsNullOrEmpty(raw)) yield break;

            _syncing = true;
            long sent = _m.updatedAt;
            var body = new PushBody
            {
                schemaVersion = CloudConfig.SchemaVersion,
                updatedAt = sent,
                baseRemoteUpdatedAt = _m.baseRemote,
                clientFingerprint = Fingerprint(),
                payload = new CloudPayload { meta = raw }
            };
            CloudReply reply = null;
            string err = null;
            yield return Backend.Call(CloudConfig.PushPath, JsonUtility.ToJson(body), (s, r, e) => { reply = r; err = e; });

            if (err == null)
            {
                _m.baseRemote = reply.data != null && reply.data.updatedAt > 0L ? reply.data.updatedAt : sent;
                // 上行途中又存过档就还是脏的，等下一轮。
                if (_m.updatedAt == sent) _m.dirty = false;
                WriteMeta();
                PlayerPrefs.Save();
                Confirm();
                _fail = 0;
                _disabled = false;
                _retryAt = -1f;
            }
            else if (reply != null && reply.code == "STALE_UPDATE" && reply.data != null && reply.data.remote != null)
            {
                CloudRemote remote = reply.data.remote;
                string meta = remote.payload != null ? remote.payload.meta : null;
                _fail = 0;
                _disabled = false;
                if (string.IsNullOrEmpty(meta))
                    Debug.LogWarning("[一裙又一裙] 云端更新但内容是空的，保留本地");
                else
                {
                    Debug.LogWarning("[一裙又一裙] 云端更新，覆盖本地存档");
                    Import(remote.updatedAt > 0L ? remote.updatedAt : Backend.NowMs(), meta, "stale-update");
                }
            }
            else
            {
                _fail++;
                if (_fail <= 3) Debug.LogWarning($"[一裙又一裙] 存档上行失败 ({_fail}/{CloudConfig.MaxFail}) {reason}: {err}");
                if (_fail >= CloudConfig.MaxFail)
                {
                    _disabled = true;
                    _retryAt = Time.realtimeSinceStartup + CloudConfig.RetryInterval;
                }
                else if (_m.dirty) Schedule("retry-after-fail");
            }

            _syncing = false;
            if (_pending && !_disabled)
            {
                _pending = false;
                Schedule("pending-resume");
            }
        }

        void Import(long remoteAt, string meta, string reason)
        {
            WardrobeService.WriteRaw(meta);
            _m.updatedAt = remoteAt;
            _m.baseRemote = remoteAt;
            _m.dirty = false;
            WriteMeta();
            PlayerPrefs.Save();
            Confirm();
            Debug.Log("[一裙又一裙] 已从云端恢复存档 " + reason);
            if (_booted && Imported != null) Imported();
        }

        void Confirm() => _auth = Authority.Confirmed;

        void EnterCacheOnly(string reason)
        {
            if (_auth == Authority.CacheOnly) return;
            _auth = Authority.CacheOnly;
            Debug.LogWarning("[一裙又一裙] 只读本地，暂停上行 " + reason);
        }

        void HookHide()
        {
            if (_hideHooked) return;
            _hideHooked = WxBridge.OnHide(() => FlushNow("hide"));
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && !_hideHooked) FlushNow("pause");
        }

        void LoadMeta()
        {
            string raw = PlayerPrefs.GetString(CloudConfig.SyncMetaKey, "");
            _m = null;
            if (!string.IsNullOrEmpty(raw))
            {
                try { _m = JsonUtility.FromJson<SyncMeta>(raw); }
                catch (Exception) { _m = null; }
            }
            if (_m == null) _m = new SyncMeta();
        }

        void WriteMeta() => PlayerPrefs.SetString(CloudConfig.SyncMetaKey, JsonUtility.ToJson(_m));

        static string Fingerprint()
        {
            string s = SystemInfo.deviceModel + "|" + SystemInfo.operatingSystem;
            return s.Length > 160 ? s.Substring(0, 160) : s;
        }
    }
}
