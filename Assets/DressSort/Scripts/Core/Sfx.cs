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
        // 微信播放器不带 Unity 的网络音频模块。长音乐交给 InnerAudioContext：
        // needDownload 让它整首下完落盘再播，下次秒开。每首只建一个，切走时暂停不销毁。
        static readonly Dictionary<string, WeChatWASM.WXInnerAudioContext> Streams =
            new Dictionary<string, WeChatWASM.WXInnerAudioContext>();
        static readonly HashSet<string> Ready = new HashSet<string>();
        static WeChatWASM.WXInnerAudioContext stream;
        static bool streamOn;
        static float streamVol;

        static void PlayStream(string name, float vol)
        {
            stream = OpenStream(name);
            streamOn = true;
            streamVol = vol;
            stream.volume = vol;
            if (Ready.Contains(name)) stream.Play();
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
                needDownload = true,
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
            ctx.OnError(e => Debug.LogWarning("[叠叠裙] " + name + " 音乐拉取失败 " + e.errCode));
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
                    return 0.9f;
                case SfxId.Clear:
                    return 0.8f;
                default:
                    return 0.74f;
            }
        }
    }
}
