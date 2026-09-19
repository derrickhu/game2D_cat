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
