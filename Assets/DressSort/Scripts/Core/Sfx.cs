using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DressSort
{
    public enum SfxId
    {
        Tap,
        Deny,
        Lift,
        Place,
        Clear,
        Unlock,
        Cover,
        Alarm,
        AlarmOff,
        Undo,
        Shuffle,
        Swap,
        Win,
        GiftLand,
        GiftPop,
        Claim,
        Craft,
        Equip,
        Popup,
        Pack,
        Heart,
        Stuck,
        Whoosh,
        Land,
        Drop,
        Clear2,
        Clear3,
        Clear4,
        Finale,
        Confetti,
        DealIn,
        StepsWarn,
        HardLevel,
    }

    public enum BgmId
    {
        None,
        Lobby,
        Play,
        Event,
    }

    /// <summary>
    /// 短音效在 Resources/Audio，按下即播。
    /// 背景音乐在云存储，清单是 CdnManifest。编辑器读 CdnArt，微信用播放器缓存后再循环。
    /// 音色是一套的：棉花、绸带、小银铃、八音盒。
    /// </summary>
    public static class Sfx
    {
        // 编辑器里由 CdnAudioHook 填上。微信包不走这里。
        public static Func<string, AudioClip> EditorMusic;
        const string Folder = "Audio/";

        static readonly string[] Files =
        {
            "ui_tap", "ui_deny", "cloth_lift", "cloth_place", "column_clear",
            "lock_open", "cover_slide", "alarm", "alarm_off", "undo",
            "shuffle", "swap", "win", "gift_land", "gift_pop",
            "claim", "craft", "equip", "popup", "pack", "heart", "stuck",
            "dress_whoosh", "dress_land", "dress_drop", "column_clear_2", "column_clear_3", "column_clear_4",
            "level_finale", "confetti_pop", "deal_in", "steps_warn", "hard_level",
        };

        static AudioSource oneShot;
        static AudioSource music;
        static AudioClip[] clips;
        static bool clipsLoaded;
        static BgmId want = BgmId.Lobby;
        static bool heard;
        static string currentTrack;

        public static void Install()
        {
            if (oneShot != null) return;
            var go = new GameObject("Sfx");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<AudioListener>();
            oneShot = go.AddComponent<AudioSource>();
            oneShot.playOnAwake = false;
            oneShot.spatialBlend = 0f;
            music = go.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            music.spatialBlend = 0f;
            music.volume = 0.32f;
            if (Application.isEditor)
                heard = true;
        }

        public static void Play(SfxId id)
        {
            if (!Application.isPlaying) return;
            Install();
            heard = true;
            AudioClip clip = Clip(id);
            if (clip != null)
                oneShot.PlayOneShot(clip, Gain(id));
            ApplyBgm();
        }

        /// <summary>连着叠好的列一列比一列高：C、D、E、G。</summary>
        public static void PlayClear(int combo)
        {
            switch (Mathf.Clamp(combo, 1, 4))
            {
                case 1: Play(SfxId.Clear); break;
                case 2: Play(SfxId.Clear2); break;
                case 3: Play(SfxId.Clear3); break;
                default: Play(SfxId.Clear4); break;
            }
        }

        public static void SetBgm(BgmId id)
        {
            want = id;
            if (!Application.isPlaying) return;
            Install();
            if (heard)
                ApplyBgm();
        }

        public static void BindClick(Button button, UnityAction action)
        {
            Bind(button, action, SfxId.Tap, true);
        }

        public static void BindSilent(Button button, UnityAction action)
        {
            Bind(button, action, SfxId.Tap, false);
        }

        static void Bind(Button button, UnityAction action, SfxId sound, bool play)
        {
            if (button == null || action == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (play)
                    Play(sound);
                action();
            });
        }

        static string TrackName(BgmId id)
        {
            switch (id)
            {
                case BgmId.Lobby: return "bgm_lobby";
                case BgmId.Play: return "bgm_play";
                case BgmId.Event: return "bgm_event";
                default: return null;
            }
        }

        static float TrackVolume(BgmId id)
        {
            return id == BgmId.Lobby ? 0.32f : 0.26f;
        }

        static void ApplyBgm()
        {
            string name = TrackName(want);
            float vol = TrackVolume(want);
            if (name == currentTrack && BedPlaying())
            {
                SetBedVolume(vol);
                // 上一拍没真正响起来（文件还在下，或被系统拦了），这次点击再试。
                if (!StreamAudible())
                    KickBed();
                return;
            }
            StopBed();
            currentTrack = name;
            if (name == null) return;
#if UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            if (!Application.isEditor)
            {
                PlayStream(name, vol);
                return;
            }
#endif
            AudioClip clip = EditorMusic != null ? EditorMusic(name) : null;
            if (clip == null) return;
            music.clip = clip;
            music.loop = true;
            music.volume = vol;
            music.Play();
        }

        static bool BedPlaying()
        {
#if UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            if (!Application.isEditor)
                return streamOn && stream != null;
#endif
            return music != null && music.clip != null && music.isPlaying;
        }

        static bool StreamAudible()
        {
#if UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            if (!Application.isEditor)
                return streamAudible;
#endif
            return music != null && music.isPlaying;
        }

        static void KickBed()
        {
#if UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            if (!Application.isEditor && stream != null)
                stream.Play();
#endif
        }

        static void SetBedVolume(float vol)
        {
#if UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            if (!Application.isEditor)
            {
                streamVol = vol;
                if (stream != null) stream.volume = vol;
                return;
            }
#endif
            if (music != null) music.volume = vol;
        }

        static void StopBed()
        {
#if UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            if (stream != null)
            {
                stream.Pause();
                stream.volume = 0f;
                stream = null;
                streamOn = false;
                streamAudible = false;
            }
#endif
            if (music != null && music.isPlaying)
                music.Stop();
            if (music != null)
                music.clip = null;
        }

        static AudioClip Clip(SfxId id)
        {
            LoadClips();
            int index = (int)id;
            if (clips == null || index < 0 || index >= clips.Length)
                return null;
            return clips[index];
        }

        static void LoadClips()
        {
            if (clipsLoaded) return;
            clipsLoaded = true;
            clips = new AudioClip[Files.Length];
            for (int i = 0; i < Files.Length; i++)
                clips[i] = Resources.Load<AudioClip>(Folder + Files[i]);
        }

#if UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
        // 微信播放器不带 Unity 的网络音频模块。长音乐交给 InnerAudioContext，直接播云上的地址。
        // 每首只建一个，切走时暂停不销毁。
        static readonly Dictionary<string, WeChatWASM.WXInnerAudioContext> Streams =
            new Dictionary<string, WeChatWASM.WXInnerAudioContext>();
        static readonly HashSet<string> Ready = new HashSet<string>();
        static WeChatWASM.WXInnerAudioContext stream;
        static bool streamOn;
        static bool streamAudible;
        static float streamVol;

        static void PlayStream(string name, float vol)
        {
            stream = OpenStream(name);
            streamOn = true;
            streamVol = vol;
            stream.volume = vol;
            // 必须赶在这次点击里调用。等下载回调再播，iOS 会当成自动播放直接丢掉。
            stream.Play();
            WarmOthers(name);
        }

        static void WarmOthers(string playing)
        {
            if (playing != "bgm_lobby") OpenStream("bgm_lobby");
            if (playing != "bgm_play") OpenStream("bgm_play");
            if (playing != "bgm_event") OpenStream("bgm_event");
        }

        static WeChatWASM.WXInnerAudioContext OpenStream(string name)
        {
            if (Streams.TryGetValue(name, out var ctx)) return ctx;
            string url = CdnManifest.Url("Audio/" + name);
            ctx = WeChatWASM.WX.CreateInnerAudioContext(new WeChatWASM.InnerAudioContextParam
            {
                src = url,
                loop = true,
                volume = 0f,
                // 直接播网络地址。走缓存时缓存目录还没配好，下载会失败，音乐就一直不出声。
                needDownload = false,
            });
            ctx.OnCanplay(() =>
            {
                Ready.Add(name);
                if (currentTrack == name && stream == ctx && streamOn)
                {
                    ctx.volume = streamVol;
                    ctx.Play();
                }
            });
            ctx.OnPlay(() =>
            {
                if (stream == ctx)
                    streamAudible = true;
            });
            ctx.OnError(e =>
            {
                if (stream == ctx)
                    streamAudible = false;
                Debug.LogWarning("[一裙又一裙] " + name + " 音乐拉取失败 " + e.errCode);
            });
            Streams[name] = ctx;
            return ctx;
        }
#endif

        static float Gain(SfxId id)
        {
            switch (id)
            {
                case SfxId.Tap:
                case SfxId.Popup:
                    return 0.62f;
                case SfxId.Deny:
                    return 0.5f;
                case SfxId.Win:
                case SfxId.GiftPop:
                case SfxId.Finale:
                case SfxId.Clear:
                case SfxId.Clear2:
                case SfxId.Clear3:
                case SfxId.Clear4:
                    return 1f;
                case SfxId.Whoosh:
                    return 0.55f;
                case SfxId.Drop:
                    return 0.6f;
                case SfxId.Land:
                case SfxId.Confetti:
                case SfxId.HardLevel:
                    return 0.85f;
                default:
                    return 0.74f;
            }
        }
    }
}
