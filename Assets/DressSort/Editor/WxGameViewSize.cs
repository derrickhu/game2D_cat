using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DressSort.EditorTools
{
    /// <summary>
    /// 跟 black-rosa 一样：给 Game 窗口注册并选中 720×1280。
    /// 微信开发者工具预览就是这个分辨率；16:9 横屏会把竖版界面挤成一条。
    /// </summary>
    [InitializeOnLoad]
    public static class WxGameViewSize
    {
        const int W = ScreenFit.PreviewW;
        const int H = ScreenFit.PreviewH;
        const string Label = "微信竖屏 720x1280";

        static WxGameViewSize()
        {
            EditorApplication.delayCall += RegisterAndSelect;
        }

        static void RegisterAndSelect()
        {
            try
            {
                foreach (GameViewSizeGroupType group in Enum.GetValues(typeof(GameViewSizeGroupType)))
                    EnsureSize(group);
                Select();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[叠叠裙] 无法设置 Game 竖屏预设: " + e.Message);
            }
        }

        static void EnsureSize(GameViewSizeGroupType groupType)
        {
            try
            {
                EnsureSizeInner(groupType);
            }
            catch (Exception)
            {
            }
        }

        static void EnsureSizeInner(GameViewSizeGroupType groupType)
        {
            if (IndexOf(groupType) >= 0) return;

            Assembly asm = typeof(Editor).Assembly;
            Type sizesType = asm.GetType("UnityEditor.GameViewSizes");
            Type sizeType = asm.GetType("UnityEditor.GameViewSize");
            Type sizeTypeEnum = asm.GetType("UnityEditor.GameViewSizeType");
            if (sizesType == null || sizeType == null || sizeTypeEnum == null) return;

            object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType)
                .GetProperty("instance").GetValue(null, null);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { (int)groupType });
            object fixedRes = Enum.Parse(sizeTypeEnum, "FixedResolution");
            object size = Activator.CreateInstance(sizeType, fixedRes, W, H, Label);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
        }

        static int IndexOf(GameViewSizeGroupType groupType)
        {
            Assembly asm = typeof(Editor).Assembly;
            Type sizesType = asm.GetType("UnityEditor.GameViewSizes");
            object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType)
                .GetProperty("instance").GetValue(null, null);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { (int)groupType });
            Type groupT = group.GetType();
            int count = (int)groupT.GetMethod("GetTotalCount").Invoke(group, null);
            for (int i = 0; i < count; i++)
            {
                object existing = groupT.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                int w = (int)existing.GetType().GetProperty("width").GetValue(existing, null);
                int h = (int)existing.GetType().GetProperty("height").GetValue(existing, null);
                if (w == W && h == H) return i;
            }
            return -1;
        }

        public static void Select()
        {
            Type gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null) return;

            var window = EditorWindow.GetWindow(gameViewType, false, "Game", false);
            int index = IndexOf(CurrentGroup());
            if (index < 0) return;

            var callback = gameViewType.GetMethod("SizeSelectionCallback",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (callback != null)
            {
                callback.Invoke(window, new object[] { index, null });
                return;
            }

            var prop = gameViewType.GetProperty("selectedSizeIndex",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            prop?.SetValue(window, index, null);
        }

        static GameViewSizeGroupType CurrentGroup()
        {
            switch (EditorUserBuildSettings.activeBuildTarget)
            {
                case BuildTarget.Android: return GameViewSizeGroupType.Android;
                case BuildTarget.iOS: return GameViewSizeGroupType.iOS;
                default: return GameViewSizeGroupType.Standalone;
            }
        }
    }
}
