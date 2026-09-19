using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DressSort.EditorTools
{
    /// <summary>
    /// 微信小游戏打包入口。SDK 本体跟 black-rosa 一样走本地包
    /// Packages/com.qq.weixin.minigame（133MB，不入库），没有就从隔壁工程拷。
    /// 团结规定：微信子平台没 Active 时，顶栏不会出现「微信小游戏」。
    /// </summary>
    public static class WxMiniGameMenu
    {
        const string PackageName = "com.qq.weixin.minigame";
        const string ConfigPath = "Assets/WX-WASM-SDK-V2/Editor/MiniGameConfig.asset";
        const string DefaultAppId = "wxd7e8bab5c9f43673";
        const string LegacyAppId = "wxad27f529c95e582c";

        [MenuItem("叠叠裙/微信小游戏/接入 SDK", priority = 20)]
        public static void InstallSdk()
        {
            if (CopySdkIfNeeded())
                AssetDatabase.Refresh();
            PatchCopiedSdkConfig();
            EnsureConfig();
            ActivateWeChatSubplatform();
            Debug.Log("[叠叠裙] 微信 SDK 已接入。等脚本编译完后，顶栏会出现「微信小游戏」。");
        }

        [MenuItem("叠叠裙/微信小游戏/打开转换面板", priority = 21)]
        public static void OpenConvertWindow()
        {
            if (!Prepare()) return;
            if (!EditorApplication.ExecuteMenuItem("微信小游戏/转换小游戏"))
                InvokeWx("WeChatWASM.WXEditorWin", "Open", null);
        }

        [MenuItem("叠叠裙/微信小游戏/生成并转换", priority = 22)]
        public static void ConvertNow()
        {
            if (!Prepare()) return;
            Debug.Log("[叠叠裙] 开始转换微信小游戏，第一次编 WASM 可能要十几分钟。产物在 wechat-minigame/minigame/");
            object result = InvokeWx("WeChatWASM.WXConvertCore", "DoExport", new object[] { true });
            bool ok = VerifyExportedAppId();
            PatchLoadingVideo();
            Debug.Log("[叠叠裙] 转换结果：" + (result ?? "未找到转换接口，请用「打开转换面板」手动点生成并转换")
                + (ok ? "，AppID 已核对为 " + DefaultAppId : "，AppID 核对失败，请看上面的警告"));
        }

        [MenuItem("叠叠裙/微信小游戏/核对导出 AppID", priority = 24)]
        public static void VerifyAppIdMenu()
        {
            EnsureConfig();
            VerifyExportedAppId();
        }

        [MenuItem("叠叠裙/微信小游戏/切到 720x1280 预览", priority = 23)]
        public static void SelectPreviewSize()
        {
            WxGameViewSize.Select();
        }

        static bool Prepare()
        {
            if (!HasSdk())
            {
                if (!CopySdkIfNeeded())
                {
                    EditorUtility.DisplayDialog("微信 SDK",
                        "找不到 Packages/com.qq.weixin.minigame。\n把 black-rosa 里的同名目录拷过来，或先点「接入 SDK」。",
                        "好");
                    return false;
                }
                AssetDatabase.Refresh();
            }
            PatchCopiedSdkConfig();
            EnsureConfig();
            ActivateWeChatSubplatform();
            return HasSdk();
        }

        static bool HasSdk()
        {
            return Directory.Exists(Path.Combine(Application.dataPath, "..", "Packages", PackageName));
        }

        static bool CopySdkIfNeeded()
        {
            string dst = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", PackageName));
            if (Directory.Exists(dst) && File.Exists(Path.Combine(dst, "package.json")))
                return true;

            string[] sources =
            {
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "black-rosa", "Packages", PackageName)),
                "/Users/rosa/rosa_games/black-rosa/Packages/com.qq.weixin.minigame",
            };

            foreach (string src in sources)
            {
                if (!Directory.Exists(src)) continue;
                CopyDirectory(src, dst);
                PatchManifest();
                PatchCopiedSdkConfig();
                Debug.Log("[叠叠裙] 已拷贝微信 SDK → " + dst);
                return true;
            }
            return false;
        }

        static void PatchManifest()
        {
            string manifest = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", "manifest.json"));
            if (!File.Exists(manifest)) return;
            string text = File.ReadAllText(manifest);
            if (text.Contains(PackageName)) return;
            text = text.Replace(
                "  \"dependencies\": {",
                "  \"dependencies\": {\n    \"" + PackageName + "\": \"file:" + PackageName + "\",");
            File.WriteAllText(manifest, text);
        }

        static void EnsureConfig()
        {
            string dir = Path.Combine(Application.dataPath, "WX-WASM-SDK-V2", "Editor");
            Directory.CreateDirectory(dir);
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "WX-WASM-SDK-V2", "Runtime", "Plugins"));

            string link = Path.Combine(Application.dataPath, "WX-WASM-SDK-V2", "Runtime", "Plugins", "link.xml");
            if (!File.Exists(link))
            {
                File.WriteAllText(link,
                    "<linker>\n  <assembly fullname=\"wx-runtime\" preserve=\"all\"/>\n  <assembly fullname=\"LitJson\" preserve=\"all\"/>\n  <assembly fullname=\"Wx\" preserve=\"all\"/>\n</linker>\n");
            }

            var config = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ConfigPath);
            if (config == null)
            {
                Type t = Type.GetType("WeChatWASM.WXEditorScriptObject, WxEditor")
                    ?? Type.GetType("WeChatWASM.WXEditorScriptObject");
                if (t == null)
                {
                    Debug.LogWarning("[叠叠裙] 微信 SDK 还没编译完，等域重载后再点一次「接入 SDK」写配置。");
                    return;
                }
                config = ScriptableObject.CreateInstance(t);
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var so = new SerializedObject(config);
            Set(so, "ProjectConf.projectName", "dress-sort");
            Set(so, "ProjectConf.Appid", DefaultAppId);
            Set(so, "ProjectConf.relativeDST", "wechat-minigame");
            Set(so, "ProjectConf.DST", Path.Combine(project, "wechat-minigame"));
            Set(so, "ProjectConf.assetLoadType", 1);
            Set(so, "ProjectConf.Orientation", 0);
            Set(so, "ProjectConf.MemorySize", 256);
            Set(so, "ProjectConf.bgImageSrc", "Assets/DressSort/Art/Loading/loading.jpg");
            Set(so, "ProjectConf.VideoUrl", "images/loading.mp4");
            Set(so, "ProjectConf.loadingBarWidth", 800);
            Set(so, "CompileOptions.Il2CppOptimizeSize", true);
            Set(so, "CompileOptions.autoAdaptScreen", true);
            Set(so, "CompileOptions.CleanBuild", true);
            Set(so, "SDKOptions.PreloadWXFont", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        static void ActivateWeChatSubplatform()
        {
            try
            {
                var group = (BuildTargetGroup)Enum.Parse(typeof(BuildTargetGroup), "MiniGame");
                var target = (BuildTarget)Enum.Parse(typeof(BuildTarget), "WeixinMiniGame");
                EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[叠叠裙] 切换 MiniGame 平台失败，请在 Build Settings 里把 MiniGame / 微信小游戏点成 Active。 " + e.Message);
            }

            // ProjectSettings.activeSubplatform=1 才是微信；0 时转换 SDK 不编译
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings != null && settings.Length > 0)
            {
                var so = new SerializedObject(settings[0]);
                SerializedProperty sub = so.FindProperty("activeSubplatform");
                if (sub != null)
                {
                    sub.intValue = 1;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            PlayerSettings.companyName = "RosaGames";
            PlayerSettings.productName = "叠叠裙";
            PlayerSettings.defaultScreenWidth = ScreenFit.PreviewW;
            PlayerSettings.defaultScreenHeight = ScreenFit.PreviewH;
            PlayerSettings.defaultWebScreenWidth = ScreenFit.PreviewW;
            PlayerSettings.defaultWebScreenHeight = ScreenFit.PreviewH;
        }

        /// <summary>
        /// 从 black-rosa 拷来的 SDK 自带墨字防线 MiniGameConfig，每次拷完都改掉。
        /// </summary>
        static void PatchCopiedSdkConfig()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(project, "Packages", PackageName, "Editor", "MiniGameConfig.asset");
            if (!File.Exists(path)) return;
            string text = File.ReadAllText(path);
            string patched = text
                .Replace("projectName: ink-line", "projectName: dress-sort")
                .Replace("Appid: " + LegacyAppId, "Appid: " + DefaultAppId)
                .Replace("DST: /Users/rosa/rosa_games/black-rosa/wechat-minigame",
                    "DST: " + Path.Combine(project, "wechat-minigame"));
            if (patched == text) return;
            File.WriteAllText(path, patched);
            Debug.Log("[叠叠裙] 已把 SDK 自带的 MiniGameConfig 从墨字防线改成叠叠裙。");
        }

        static bool VerifyExportedAppId()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string minigame = Path.Combine(project, "wechat-minigame", "minigame");
            string configPath = Path.Combine(minigame, "project.config.json");
            string gameJs = Path.Combine(minigame, "game.js");
            if (!File.Exists(configPath))
            {
                Debug.LogWarning("[叠叠裙] 还没有导出 wechat-minigame/minigame/project.config.json，先转换一次。");
                return false;
            }

            bool ok = true;
            string json = File.ReadAllText(configPath);
            if (!json.Contains("\"appid\": \"" + DefaultAppId + "\""))
            {
                json = System.Text.RegularExpressions.Regex.Replace(
                    json, "\"appid\"\\s*:\\s*\"[^\"]*\"", "\"appid\": \"" + DefaultAppId + "\"");
                json = System.Text.RegularExpressions.Regex.Replace(
                    json, "\"projectname\"\\s*:\\s*\"[^\"]*\"", "\"projectname\": \"dress-sort\"");
                File.WriteAllText(configPath, json);
                ok = false;
                Debug.LogWarning("[叠叠裙] 导出的 project.config.json AppID 不对，已改成 " + DefaultAppId);
            }

            if (File.Exists(gameJs))
            {
                string js = File.ReadAllText(gameJs);
                if (!js.Contains("APPID: '" + DefaultAppId + "'") && !js.Contains("APPID: \"" + DefaultAppId + "\""))
                {
                    js = System.Text.RegularExpressions.Regex.Replace(
                        js, "APPID:\\s*['\"][^'\"]*['\"]", "APPID: '" + DefaultAppId + "'");
                    File.WriteAllText(gameJs, js);
                    ok = false;
                    Debug.LogWarning("[叠叠裙] 导出的 game.js APPID 不对，已改成 " + DefaultAppId);
                }
            }

            PatchLoadingVideo();

            if (ok)
                Debug.Log("[叠叠裙] 导出 AppID 正确：" + DefaultAppId + "，请用开发者工具打开 "
                    + minigame + "，不要打开 black-rosa 那个窗口。");
            return ok;
        }

        /// <summary>
        /// 转换只拷封面静图。花园 loading 视频要另外放进 images/，并写进封面配置。
        /// </summary>
        static void PatchLoadingVideo()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string src = Path.Combine(project, "Assets/DressSort/Art/Loading/loading.mp4");
            string minigame = Path.Combine(project, "wechat-minigame", "minigame");
            string images = Path.Combine(minigame, "images");
            string dst = Path.Combine(images, "loading.mp4");
            string gameJs = Path.Combine(minigame, "game.js");
            if (!File.Exists(src))
            {
                Debug.LogWarning("[叠叠裙] 找不到 Assets/DressSort/Art/Loading/loading.mp4，封面还是静图。");
                return;
            }

            Directory.CreateDirectory(images);
            File.Copy(src, dst, true);

            if (File.Exists(gameJs))
            {
                string js = File.ReadAllText(gameJs);
                string patched = System.Text.RegularExpressions.Regex.Replace(
                    js,
                    "backgroundVideo:\\s*['\"][^'\"]*['\"]",
                    "backgroundVideo: 'images/loading.mp4'");
                if (patched != js)
                    File.WriteAllText(gameJs, patched);
            }

            Debug.Log("[叠叠裙] 已写入封面视频 images/loading.mp4");
        }

        static object InvokeWx(string typeName, string method, object[] args)
        {
            Type t = Type.GetType(typeName + ", WxEditor") ?? Type.GetType(typeName);
            if (t == null) return null;
            MethodInfo m = t.GetMethod(method, BindingFlags.Public | BindingFlags.Static);
            if (m == null) return null;
            return m.Invoke(null, args ?? Array.Empty<object>());
        }

        static string ReadString(SerializedObject so, string path)
        {
            string[] parts = path.Split('.');
            SerializedProperty p = so.FindProperty(parts[0]);
            for (int i = 1; i < parts.Length && p != null; i++)
                p = p.FindPropertyRelative(parts[i]);
            return p != null ? p.stringValue : "";
        }

        static void Set(SerializedObject so, string path, object value)
        {
            string[] parts = path.Split('.');
            SerializedProperty p = so.FindProperty(parts[0]);
            for (int i = 1; i < parts.Length && p != null; i++)
                p = p.FindPropertyRelative(parts[i]);
            if (p == null) return;
            if (value is string s) p.stringValue = s;
            else if (value is int n) p.intValue = n;
            else if (value is bool b) p.boolValue = b;
        }

        static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (string file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                string rel = file.Substring(src.Length).TrimStart(Path.DirectorySeparatorChar, '/');
                string dest = Path.Combine(dst, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(file, dest, true);
            }
        }
    }
}
