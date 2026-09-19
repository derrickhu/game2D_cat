using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 微信竖屏适配。设计稿按 1080×1920（9:16），Game 窗口和真机预览用 720×1280，
    /// 比例一样所以会铺满。更高的刘海屏按宽匹配，多出来的高度留给安全区。
    /// </summary>
    public static class ScreenFit
    {
        public const float DesignW = 1080f;
        public const float DesignH = 1920f;
        public const int PreviewW = 720;
        public const int PreviewH = 1280;

        public static float TopPad { get; private set; }
        public static float BottomPad { get; private set; }
        public static float CanvasW { get; private set; } = DesignW;
        public static float CanvasH { get; private set; } = DesignH;

        public static void Apply(Camera cam, Canvas canvas)
        {
            float w = Mathf.Max(1, Screen.width);
            float h = Mathf.Max(1, Screen.height);
            float aspect = w / h;

            if (cam != null)
            {
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Palette.Sky;
            }

            if (canvas != null)
            {
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(DesignW, DesignH);
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    // 竖屏按宽匹配，刘海机长出来的高度用来排安全区；横屏（编辑器误开 16:9）按高匹配，避免整页缩成一条
                    scaler.matchWidthOrHeight = aspect < 0.7f ? 0f : 1f;
                }
            }

            float scale = canvas != null && canvas.scaleFactor > 0.01f ? canvas.scaleFactor : 1f;
            CanvasW = w / scale;
            CanvasH = h / scale;

            Rect sa = Screen.safeArea;
            TopPad = (h - sa.yMax) / scale;
            BottomPad = sa.yMin / scale;

            Vector2 inset = WxBridge.SafeInsetFrac();
            if (inset.x >= 0f) TopPad = Mathf.Max(TopPad, inset.x * CanvasH);
            if (inset.y >= 0f) BottomPad = Mathf.Max(BottomPad, inset.y * CanvasH);

            if (TopPad < 24f) TopPad = 24f;
            if (BottomPad < 16f) BottomPad = 16f;

            float capsule = WxBridge.CapsuleBottomFrac();
            if (capsule > 0f) TopPad = Mathf.Max(TopPad, capsule * CanvasH + 12f);
        }

        public static void ApplySafeArea(RectTransform safe)
        {
            if (safe == null) return;
            safe.offsetMin = new Vector2(0f, BottomPad);
            safe.offsetMax = new Vector2(0f, -TopPad);
        }
    }
}
