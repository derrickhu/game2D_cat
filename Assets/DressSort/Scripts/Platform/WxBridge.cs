using System;
using UnityEngine;

namespace DressSort
{
    /// <summary>
    /// 微信 SDK 用反射调用，编辑器里没有 Wx 程序集也能编译。
    /// 做法对齐 black-rosa：胶囊、安全区、触摸覆盖、启动保活。
    /// </summary>
    public static class WxBridge
    {
        static Type WxType()
        {
            return Type.GetType("WeChatWASM.WX, Wx") ?? Type.GetType("WeChatWASM.WX");
        }

        public static bool IsMiniGame
        {
            get
            {
#if UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
                return true;
#else
                string p = Application.platform.ToString();
                return p.IndexOf("MiniGame", StringComparison.OrdinalIgnoreCase) >= 0
                    || p.IndexOf("Weixin", StringComparison.OrdinalIgnoreCase) >= 0;
#endif
            }
        }

        public static void InitSdk(Action ready)
        {
            var wx = WxType();
            var init = wx != null ? wx.GetMethod("InitSDK", new[] { typeof(Action<int>) }) : null;
            if (init == null)
            {
                ready?.Invoke();
                return;
            }
            Action<int> cb = _ => ready?.Invoke();
            init.Invoke(null, new object[] { cb });
        }

        public static void KeepRuntime()
        {
            WxType()?.GetMethod("GetSystemInfoSync", Type.EmptyTypes)?.Invoke(null, null);
        }

        public static void OverrideTouch(GameObject eventSystem)
        {
            var t = Type.GetType("WXTouchInputOverride, Wx") ?? Type.GetType("WXTouchInputOverride");
            if (t == null || eventSystem == null || eventSystem.GetComponent(t) != null) return;
            eventSystem.AddComponent(t);
        }

        public static void ShowShareMenu()
        {
            var wx = WxType();
            wx?.GetMethod("ShowShareMenu", Type.EmptyTypes)?.Invoke(null, null);
        }

        /// <summary>wx.login 拿一次性 code，换 openid 在云函数里做。非微信构建直接报失败。</summary>
        public static void Login(Action<string> ok, Action<string> fail)
        {
#if UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            WeChatWASM.WX.Login(new WeChatWASM.LoginOption
            {
                success = r => ok(r.code),
                fail = e => fail(e.errMsg)
            });
#else
            fail("not minigame");
#endif
        }

        /// <summary>从游戏圈切回来时刷新进度。编辑器里没有，返回 false。</summary>
        public static bool OnShow(Action show)
        {
#if UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            if (Application.isEditor) return false;
            WeChatWASM.WX.OnShow(_ => show());
            return true;
#else
            return false;
#endif
        }

        /// <summary>真机上才有微信原生的授权按钮和游戏圈按钮。</summary>
        public static bool CanAskProfile
        {
            get
            {
#if UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
                return !Application.isEditor;
#else
                return false;
#endif
            }
        }

        public static bool CanUseClub => CanAskProfile;

        /// <summary>
        /// 昵称和游戏圈数据都要先过隐私授权，才会弹出微信自己的隐私弹窗。
        /// 同意过的这次会话里不再问。编辑器没有这套接口，直接当已同意。
        /// </summary>
        public static bool PrivacyAgreed { get; private set; }

        static bool _privacyBusy;
        static Action<bool> _privacyWait;

        public static void EnsurePrivacy(Action<bool> done)
        {
            if (!CanAskProfile || PrivacyAgreed)
            {
                if (!CanAskProfile) PrivacyAgreed = true;
                done?.Invoke(true);
                return;
            }
            _privacyWait += done;
            if (_privacyBusy) return;
            _privacyBusy = true;
#if UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            try
            {
                WeChatWASM.WX.RequirePrivacyAuthorize(new WeChatWASM.RequirePrivacyAuthorizeOption
                {
                    success = _ => FinishPrivacy(true),
                    fail = _ => FinishPrivacy(false),
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Privacy] " + e.Message);
                FinishPrivacy(true);
            }
#else
            FinishPrivacy(true);
#endif
        }

        static void FinishPrivacy(bool ok)
        {
            _privacyBusy = false;
            if (ok) PrivacyAgreed = true;
            Action<bool> wait = _privacyWait;
            _privacyWait = null;
            wait?.Invoke(ok);
        }

#if UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
        static WeChatWASM.WXUserInfoButton _infoBtn;
        static WeChatWASM.WXGameClubButton _clubBtn;
#endif

        /// <summary>
        /// 微信只允许用它自己画的透明按钮拿昵称头像，所以在 Unity 按钮正上方盖一个同样大小的。
        /// SDK 里会把坐标再除一次 devicePixelRatio，所以这里传物理像素。
        /// got(nick, avatarUrl, err)：没拿到时 nick 为空。
        /// </summary>
        public static void ShowProfileButton(Rect screenRect, Action<string, string, string> got)
        {
            HideProfileButton();
#if UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            if (Application.isEditor) return;
            RectInt r = ToPhysical(screenRect);
            _infoBtn = WeChatWASM.WX.CreateUserInfoButton(r.x, r.y, r.width, r.height, "zh_CN", false);
            _infoBtn.OnTap(res =>
            {
                // userInfo 是结构体，不能和 null 写在同一个三元表达式里。
                if (res == null || string.IsNullOrEmpty(res.userInfo.nickName))
                {
                    string err = res == null ? "no response" : (res.errCode + " " + res.errMsg);
                    got(null, null, err);
                    return;
                }
                got(res.userInfo.nickName, res.userInfo.avatarUrl ?? "", null);
            });
            _infoBtn.Show();
#endif
        }

        public static void HideProfileButton()
        {
#if UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            if (_infoBtn == null) return;
            _infoBtn.Destroy();
            _infoBtn = null;
#endif
        }

        /// <summary>拒绝过一次之后微信不会再弹授权框，只能引去设置页重新打开。done(true) 表示回来时已允许。</summary>
        public static void AskOpenSetting(string content, Action<bool> done)
        {
#if UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            if (!Application.isEditor)
            {
                WeChatWASM.WX.ShowModal(new WeChatWASM.ShowModalOption
                {
                    title = "需要你的授权",
                    content = content,
                    confirmText = "去设置",
                    cancelText = "取消",
                    showCancel = true,
                    success = m =>
                    {
                        if (!m.confirm) { done?.Invoke(false); return; }
                        WeChatWASM.WX.OpenSetting(new WeChatWASM.OpenSettingOption
                        {
                            success = s =>
                            {
                                bool ok = s.authSetting != null && s.authSetting.ContainsKey("scope.userInfo")
                                          && s.authSetting["scope.userInfo"];
                                done?.Invoke(ok);
                            },
                            fail = _ => done?.Invoke(false),
                        });
                    },
                    fail = _ => done?.Invoke(false),
                });
                return;
            }
#endif
            done?.Invoke(false);
        }

        /// <summary>游戏圈入口也只能用微信原生按钮打开，同样透明地盖在 Unity 按钮上。</summary>
        public static void ShowClubButton(Rect screenRect)
        {
            HideClubButton();
#if UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            if (Application.isEditor) return;
            RectInt r = ToWindow(screenRect);
            _clubBtn = WeChatWASM.WX.CreateGameClubButton(new WeChatWASM.WXCreateGameClubButtonParam
            {
                type = WeChatWASM.GameClubButtonType.text,
                text = " ",
                style = new WeChatWASM.GameClubButtonStyle
                {
                    left = r.x,
                    top = r.y,
                    width = r.width,
                    height = r.height,
                    backgroundColor = "rgba(0,0,0,0)",
                    borderColor = "rgba(0,0,0,0)",
                    borderWidth = 0,
                    borderRadius = 0,
                    color = "rgba(0,0,0,0)",
                    fontSize = 12,
                    lineHeight = r.height,
                },
            });
            _clubBtn?.Show();
#endif
        }

        public static void HideClubButton()
        {
#if UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            if (_clubBtn == null) return;
            _clubBtn.Destroy();
            _clubBtn = null;
#endif
        }

        /// <summary>游戏圈数据是加密的，用登录时的 session_key 在云函数里解。done(encryptedData, iv, err)。</summary>
        public static void GameClubData(int dataType, Action<string, string, string> done)
        {
#if UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            if (!Application.isEditor)
            {
                WeChatWASM.WX.GetGameClubData(new WeChatWASM.GetGameClubDataOption
                {
                    dataTypeList = new[] { new WeChatWASM.DataType { type = dataType } },
                    success = r => done(r.encryptedData, r.iv, string.IsNullOrEmpty(r.encryptedData) ? "empty" : null),
                    fail = e => done(null, null, e != null ? e.errMsg : "fail"),
                });
                return;
            }
#endif
            done(null, null, "not minigame");
        }

        static Font _sysFont;
        static bool _sysFontAsked;
        static Action<Font> _sysFontWait;

        /// <summary>
        /// 玩家昵称什么字都可能有，游戏字体只切了用到的几百个字。
        /// 真机拿微信的系统字体；编辑器里用本机字体。拿不到回调 null。
        /// </summary>
        public static void SystemFont(Action<Font> done)
        {
            if (_sysFont != null || (_sysFontAsked && _sysFontWait == null)) { done(_sysFont); return; }
            _sysFontWait += done;
            if (_sysFontAsked) return;
            _sysFontAsked = true;
#if UNITY_MINIGAME || WEIXINMINIGAME || UNITY_WEIXINMINIGAME || MINIGAME_SUBPLATFORM_WEIXIN
            if (!Application.isEditor)
            {
                WeChatWASM.WX.GetWXFont(null, f => FinishFont(f));
                return;
            }
#endif
            FinishFont(Font.CreateDynamicFontFromOSFont(
                new[] { "PingFang SC", "Heiti SC", "Noto Sans CJK SC", "Arial Unicode MS" }, 28));
        }

        static void FinishFont(Font f)
        {
            _sysFont = f;
            Action<Font> wait = _sysFontWait;
            _sysFontWait = null;
            wait?.Invoke(f);
        }

        /// <summary>游戏圈按钮的 style 是逻辑像素，SDK 不再缩放。</summary>
        static RectInt ToWindow(Rect screenRect)
        {
            float winW = Num(SystemInfo(), "windowWidth");
            float k = winW > 0f ? winW / Mathf.Max(1, Screen.width) : 1f;
            return TopLeft(screenRect, k);
        }

        /// <summary>昵称按钮的 JS 会再除 devicePixelRatio。Unity 屏幕若已是逻辑像素，这里先乘回去。</summary>
        static RectInt ToPhysical(Rect screenRect)
        {
            float winW = Num(SystemInfo(), "windowWidth");
            float dpr = Num(SystemInfo(), "pixelRatio");
            if (dpr < 1f) dpr = 1f;
            float scale = winW > 0f && Screen.width < winW * 1.5f ? dpr : 1f;
            return TopLeft(screenRect, scale);
        }

        static RectInt TopLeft(Rect screenRect, float scale)
        {
            return new RectInt(
                Mathf.RoundToInt(screenRect.xMin * scale),
                Mathf.RoundToInt((Screen.height - screenRect.yMax) * scale),
                Mathf.Max(1, Mathf.RoundToInt(screenRect.width * scale)),
                Mathf.Max(1, Mathf.RoundToInt(screenRect.height * scale)));
        }

        /// <summary>UI 节点在屏幕上的像素矩形，左下为原点。原生按钮按它摆位置。</summary>
        public static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        static float _capsule = float.NaN;

        public static float CapsuleBottomFrac()
        {
            if (!float.IsNaN(_capsule)) return _capsule;
            _capsule = IsMiniGame ? 0.095f : -1f;
            try
            {
                object rect = WxType()?.GetMethod("GetMenuButtonBoundingClientRect", Type.EmptyTypes)
                    ?.Invoke(null, null);
                float bottom = Num(rect, "bottom");
                float screenH = Num(SystemInfo(), "screenHeight");
                if (bottom > 0f && screenH > 0f) _capsule = bottom / screenH;
            }
            catch (Exception)
            {
            }
            return _capsule;
        }

        static Vector2 _inset = new Vector2(float.NaN, float.NaN);

        public static Vector2 SafeInsetFrac()
        {
            if (!float.IsNaN(_inset.x)) return _inset;
            _inset = new Vector2(-1f, -1f);
            try
            {
                object sys = SystemInfo();
                float screenH = Num(sys, "screenHeight");
                object area = Member(sys, "safeArea");
                float top = Num(area, "top");
                float bottom = Num(area, "bottom");
                if (screenH > 0f && bottom > top && bottom <= screenH)
                    _inset = new Vector2(top / screenH, (screenH - bottom) / screenH);
            }
            catch (Exception)
            {
            }
            return _inset;
        }

        static object SystemInfo()
        {
            return WxType()?.GetMethod("GetSystemInfoSync", Type.EmptyTypes)?.Invoke(null, null);
        }

        static object Member(object o, string name)
        {
            if (o == null) return null;
            Type t = o.GetType();
            var f = t.GetField(name);
            if (f != null) return f.GetValue(o);
            var p = t.GetProperty(name);
            return p?.GetValue(o);
        }

        static float Num(object o, string name)
        {
            object v = Member(o, name);
            return v == null ? 0f : Convert.ToSingle(v);
        }
    }

    public static class WxRuntimeKeep
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Keep()
        {
            Application.targetFrameRate = 60;
            Input.simulateMouseWithTouches = true;
            WxBridge.KeepRuntime();
        }
    }
}
