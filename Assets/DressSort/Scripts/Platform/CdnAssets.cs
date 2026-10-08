using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 全身立绘的高清图在云上。包里的小图先顶上，下完再换。
    /// 拉不到就一直用小图。编辑器直接读 Art 里的原图。
    /// </summary>
    public sealed class CdnAssets : MonoBehaviour
    {
        const int MaxParallel = 2;
        const int TimeoutSec = 12;
        const int MaxTextures = 8;
        const float RetryAfter = 30f;

        static CdnAssets runner;
        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        static readonly List<string> Recent = new List<string>();
        static readonly Dictionary<string, List<Action>> Waiting = new Dictionary<string, List<Action>>();
        static readonly Dictionary<string, float> FailedAt = new Dictionary<string, float>();
        static readonly List<string> Pending = new List<string>();
        static readonly Dictionary<UnityEngine.Object, string> Bound = new Dictionary<UnityEngine.Object, string>();
        static int running;

        public static bool Has(string name) => CdnManifest.Url(name) != null;

        public static void Bind(Image target, string name, Action ready)
        {
            if (target == null || !Has(name)) return;
            Bound[target] = name;
            Sprite cached = Cached(name);
            if (cached != null)
            {
                target.sprite = cached;
                ready?.Invoke();
                return;
            }
            Fetch(name, true, () =>
            {
                if (target == null || !Bound.TryGetValue(target, out string want) || want != name) return;
                Sprite hd = Cached(name);
                if (hd == null) return;
                target.sprite = hd;
                ready?.Invoke();
            });
        }

        public static void Prefetch(string name)
        {
            if (Has(name) && Cached(name) == null)
                Fetch(name, false, null);
        }

        static Sprite Cached(string name)
        {
            if (!Sprites.TryGetValue(name, out Sprite s) || s == null) return null;
            Recent.Remove(name);
            Recent.Add(name);
            return s;
        }

        static void Fetch(string name, bool urgent, Action done)
        {
            if (Cached(name) != null)
            {
                done?.Invoke();
                return;
            }
            if (FailedAt.TryGetValue(name, out float at) && Time.unscaledTime - at < RetryAfter)
                return;
            if (Waiting.TryGetValue(name, out var list))
            {
                if (done != null) list.Add(done);
                int queued = Pending.IndexOf(name);
                if (urgent && queued > 0)
                {
                    Pending.RemoveAt(queued);
                    Pending.Insert(0, name);
                }
                return;
            }
            Waiting[name] = new List<Action>();
            if (done != null) Waiting[name].Add(done);
            if (urgent) Pending.Insert(0, name);
            else Pending.Add(name);
            Pump();
        }

        static void Pump()
        {
            if (runner == null)
            {
                var go = new GameObject("CdnAssets");
                DontDestroyOnLoad(go);
                runner = go.AddComponent<CdnAssets>();
            }
            while (running < MaxParallel && Pending.Count > 0)
            {
                string name = Pending[0];
                Pending.RemoveAt(0);
                running++;
                runner.StartCoroutine(Load(name));
            }
        }

        static IEnumerator Load(string name)
        {
            string url = LocalUrl(name) ?? CdnManifest.Url(name);
            UnityWebRequest req = UnityWebRequestTexture.GetTexture(url, true);
            req.timeout = TimeoutSec;
            yield return req.SendWebRequest();

            bool ok = req.result == UnityWebRequest.Result.Success;
            if (ok)
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(req);
                ok = tex != null && tex.width > 8;
                if (ok)
                {
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.filterMode = FilterMode.Bilinear;
                    Sprites[name] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                    Recent.Remove(name);
                    Recent.Add(name);
                    Trim();
                }
            }
            if (!ok)
            {
                FailedAt[name] = Time.unscaledTime;
                Debug.LogWarning("[一裙又一裙] 立绘没拉到 " + name + " " + req.error);
            }
            req.Dispose();

            running--;
            if (Waiting.TryGetValue(name, out var list))
            {
                Waiting.Remove(name);
                if (ok)
                {
                    for (int i = 0; i < list.Count; i++)
                        list[i]?.Invoke();
                }
            }
            Pump();
        }

        static string LocalUrl(string name)
        {
            if (!Application.isEditor) return null;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "DressSort/Art", name + ".png"));
            if (!File.Exists(path)) return null;
            return new Uri(path).AbsoluteUri;
        }

        static readonly List<UnityEngine.Object> Dead = new List<UnityEngine.Object>();

        static void Trim()
        {
            for (int i = 0; i < Recent.Count && Sprites.Count > MaxTextures;)
            {
                string name = Recent[i];
                Sprite s = Sprites[name];
                if (InUse(s)) { i++; continue; }
                Recent.RemoveAt(i);
                Sprites.Remove(name);
                if (s != null)
                {
                    if (s.texture != null) Destroy(s.texture);
                    Destroy(s);
                }
            }
        }

        static bool InUse(Sprite s)
        {
            bool used = false;
            Dead.Clear();
            foreach (var kv in Bound)
            {
                if (kv.Key == null) { Dead.Add(kv.Key); continue; }
                if (kv.Key is Image img && img.sprite == s) used = true;
            }
            for (int i = 0; i < Dead.Count; i++) Bound.Remove(Dead[i]);
            return used;
        }
    }
}
