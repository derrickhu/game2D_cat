using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DressSort.EditorTools
{
    /// <summary>
    /// 把 Art 目录里的图打成 ItemDef / GameDatabase，再建好场景。关卡由 LevelCatalog 按关号生成。
    /// 美术管线跑完之后点一次这个菜单，工程就全接上了。
    /// </summary>
    public static class DressSortBuilder
    {
        const string Root = "Assets/DressSort";
        const string DataDir = Root + "/Data";
        const string ItemDir = DataDir + "/Items";
        const string DatabasePath = DataDir + "/GameDatabase.asset";
        const string ScenePath = Root + "/DressSort.unity";
        const int DefaultBgIndex = 4;
        static readonly string[] BgFiles =
        {
            "Ui/Bgs/bg_room",
            "Ui/Bgs/bg_boutique",
            "Ui/Bgs/bg_sky",
            "Ui/Bgs/bg_garden",
            "Ui/Bgs/bg_stage",
        };

        struct DressInfo
        {
            public string id;
            public string label;
            public Color accent;
            public bool fromStart;
        }

        // 游戏内资源只用短 id，对齐工具的长文件名不许进 Portraits / Icons。
        // 裙子：Icons/dress_{id}.png
        //       Portraits/body_{id}.png      无头身体，有这张就能换发
        //       Portraits/portrait_{id}.png  未拆头的全身兜底
        // 发型：Icons/hair_{key}.png
        //       Portraits/head_{key}.png
        //       Portraits/hairback_{key}.png 长发后片，没有就留空
        // 翅膀：Wings/{id}.png
        // id 与中文名一一对应，见下面三张表。
        static readonly DressInfo[] Dresses =
        {
            new DressInfo { id = "teal_sailor", label = "泳池水手裙", accent = new Color32(0x6F, 0xCF, 0xC8, 0xFF), fromStart = true },
            new DressInfo { id = "black_ribbon", label = "黑裙白结", accent = new Color32(0x2C, 0x24, 0x33, 0xFF) },
            new DressInfo { id = "pink_gingham", label = "粉格蛋糕裙", accent = new Color32(0xF3, 0xA7, 0xB8, 0xFF) },
            new DressInfo { id = "lemon_print", label = "柠檬衬衫裙", accent = new Color32(0xF0, 0xC8, 0x4A, 0xFF) },
            new DressInfo { id = "orange_slice", label = "橙子背心裙", accent = new Color32(0xFF, 0x8A, 0x3D, 0xFF) },
            new DressInfo { id = "ivory_lace", label = "象牙蕾丝裙", accent = new Color32(0xF2, 0xE3, 0xC0, 0xFF) },
            new DressInfo { id = "strawberry", label = "草莓开衫裙", accent = new Color32(0xE8, 0x6A, 0x8A, 0xFF) },
            new DressInfo { id = "grape_school", label = "葡萄校服", accent = new Color32(0xB9, 0x8C, 0xE8, 0xFF) },
            new DressInfo { id = "mint_bow", label = "薄荷蝴蝶结裙", accent = new Color32(0x9F, 0xE0, 0xC0, 0xFF) },
            new DressInfo { id = "sky_dot", label = "天空波点裙", accent = new Color32(0x8C, 0xC8, 0xF0, 0xFF) },
            new DressInfo { id = "cherry_red", label = "樱桃红裙", accent = new Color32(0xD8, 0x30, 0x40, 0xFF) },
            new DressInfo { id = "navy_stripe", label = "海军条纹裙", accent = new Color32(0x2E, 0x3F, 0x7A, 0xFF) },
            new DressInfo { id = "peach_puff", label = "蜜桃泡泡裙", accent = new Color32(0xFF, 0xB0, 0x90, 0xFF) },
            new DressInfo { id = "lilac_lace", label = "丁香蕾丝裙", accent = new Color32(0xD0, 0xB8, 0xEC, 0xFF) },
            new DressInfo { id = "matcha_pinafore", label = "抹茶背带裙", accent = new Color32(0x9C, 0xB8, 0x64, 0xFF) },
            new DressInfo { id = "coral_sun", label = "珊瑚太阳裙", accent = new Color32(0xFF, 0x70, 0x5A, 0xFF) },
            new DressInfo { id = "cocoa_check", label = "可可格纹裙", accent = new Color32(0x8A, 0x5A, 0x3C, 0xFF) },
            new DressInfo { id = "lavender_star", label = "薰衣草星星裙", accent = new Color32(0x9A, 0x7C, 0xD8, 0xFF) },
            new DressInfo { id = "cream_cloud", label = "奶油云朵裙", accent = new Color32(0xF6, 0xEE, 0xD8, 0xFF) },
            new DressInfo { id = "blueberry", label = "蓝莓背心裙", accent = new Color32(0x3A, 0x4E, 0xB8, 0xFF) },
            new DressInfo { id = "rose_velvet", label = "玫瑰丝绒裙", accent = new Color32(0xB8, 0x2A, 0x50, 0xFF) },
            new DressInfo { id = "honey_bow", label = "蜂蜜蝴蝶结裙", accent = new Color32(0xF2, 0xB8, 0x3A, 0xFF) },
        };

        /// <summary>身上穿着的这一头。杏橙、紫藤、雾蓝改到任务里领。奶茶长发给图纸，粉红马尾留给活动。</summary>
        static readonly string[] StartHairs = { "hair_milktea" };

        static readonly (string id, string label)[] Wings =
        {
            ("wing_aqua", "湖水蝶翼"),
            ("wing_rose", "蜜桃花瓣翼"),
            ("wing_wisteria", "紫藤心翼"),
        };

        static readonly (string id, string label)[] Hairs =
        {
            ("hair_apricot", "杏橙"),
            ("hair_milktea", "奶茶棕"),
            ("hair_milktea_long", "奶茶长发"),
            ("hair_wisteria", "紫藤"),
            ("hair_caramel", "焦糖栗"),
            ("hair_baguette", "奶油金"),
            ("hair_denim", "雾蓝"),
            ("hair_pink", "粉红马尾"),
        };

        [InitializeOnLoadMethod]
        static void UseDressSortAsPlayScene()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += TryRebuildChrome;
            };
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying) return;
                BindPlayScene();
                TryRebuildChrome();

                var scene = EditorSceneManager.GetActiveScene();
                if (scene.path == ScenePath) return;
                if (!File.Exists(ScenePath)) return;

                // 编辑器还停在旧的动物场景时，自动切到一裙又一裙
                if (string.IsNullOrEmpty(scene.path) || scene.path.Contains("AnimalSort"))
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

                WxGameViewSize.Select();
            };
        }

        static void TryRebuildChrome()
        {
            if (Application.isPlaying) return;
            if (LoadSprite("Ui/bg") == null) return;
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            TryBindCatalog(database);
            if (TryBindBackgrounds(database))
                return;
            if (database != null && database.uiBg != null) return;
            RebuildAll();
        }

        [MenuItem("一裙又一裙/重建资源与场景", priority = 0)]
        public static void RebuildAll()
        {
            Directory.CreateDirectory(ItemDir);
            AssetDatabase.ImportAsset(Root + "/Art/Ui", ImportAssetOptions.ImportRecursive);
            AssetDatabase.Refresh();

            var items = new List<ItemDef>();
            var byId = new Dictionary<string, ItemDef>();

            foreach (DressInfo info in Dresses)
            {
                ItemDef item = Upsert(info.id);
                item.id = info.id;
                item.displayName = info.label;
                item.slot = ItemSlot.Dress;
                item.icon = LoadSprite("Icons/dress_" + info.id);
                Sprite body = TryLoadSprite("Portraits/body_" + info.id);
                item.worn = body != null ? body : LoadSprite("Portraits/portrait_" + info.id);
                item.accent = info.accent;
                item.boardEligible = true;
                item.unlockedFromStart = info.fromStart;
                item.layeredWithHair = body != null;
                EditorUtility.SetDirty(item);
                items.Add(item);
                byId[info.id] = item;
            }

            foreach ((string id, string label) in Wings)
            {
                ItemDef item = Upsert(id);
                item.id = id;
                item.displayName = label;
                item.slot = ItemSlot.Wings;
                item.icon = LoadSprite("Icons/ui_" + id);
                item.worn = LoadSprite("Wings/" + id);
                item.accent = Palette.Aqua;
                item.boardEligible = false;
                item.unlockedFromStart = false;
                EditorUtility.SetDirty(item);
                items.Add(item);
                byId[id] = item;
            }

            foreach ((string id, string label) in Hairs)
            {
                string key = HairKey(id);
                ItemDef item = Upsert(id);
                item.id = id;
                item.displayName = label;
                item.slot = ItemSlot.Hair;
                item.icon = TryLoadSprite("Icons/hair_" + key);
                item.worn = LoadSprite("Portraits/head_" + key);
                item.wornBack = TryLoadSprite("Portraits/hairback_" + key);
                item.accent = Palette.Lemon;
                item.boardEligible = false;
                item.unlockedFromStart = IsStartHair(id);
                EditorUtility.SetDirty(item);
                items.Add(item);
                byId[id] = item;
            }

            // 问号那件只在棋盘上出现，不进衣柜，所以不放进 items
            ItemDef mystery = Upsert("mystery");
            mystery.id = "mystery";
            mystery.displayName = "神秘礼盒";
            mystery.slot = ItemSlot.Dress;
            mystery.icon = LoadSprite("Icons/dress_mystery");
            mystery.worn = null;
            mystery.boardEligible = false;
            mystery.unlockedFromStart = false;
            EditorUtility.SetDirty(mystery);

            var database = LoadOrCreate<GameDatabase>(DatabasePath);
            database.items = items;
            database.mysteryItem = mystery;
            database.defaultDress = byId["teal_sailor"];
            database.defaultHair = byId.ContainsKey("hair_milktea") && byId["hair_milktea"].worn != null
                ? byId["hair_milktea"]
                : byId["hair_apricot"];
            BindBackgroundList(database, forceDefault: true);
            database.uiBtnTeal = LoadSprite("Ui/btn_teal");
            database.uiBtnPink = LoadSprite("Ui/btn_pink");
            database.uiBtnWhite = LoadSprite("Ui/btn_white");
            database.uiCard = LoadSprite("Ui/card");
            database.uiCardOn = LoadSprite("Ui/card_on");
            database.uiLane = LoadSprite("Ui/lane");
            database.iconBack = LoadSprite("Icons/ui_back");
            database.iconGear = LoadSprite("Icons/ui_gear");
            database.iconLock = LoadSprite("Icons/ui_lock");
            database.iconCheck = LoadSprite("Icons/ui_check");
            database.iconUndo = LoadSprite("Icons/ui_undo");
            database.iconShuffle = LoadSprite("Icons/ui_shuffle");
            database.iconEject = LoadSprite("Icons/ui_hanger");
            database.iconHanger = LoadSprite("Icons/ui_hanger");
            database.iconStar = LoadSprite("Icons/ui_star");
            database.iconMystery = LoadSprite("Icons/dress_mystery");
            database.btnHomeStart = LoadSprite("Ui/Home/btn_home_start");
            database.btnHomeDress = LoadSprite("Ui/Home/btn_home_dress");
            database.iconHomeCircle = LoadSprite("Ui/Home/icon_home_circle");
            database.iconHomeRank = LoadSprite("Ui/Home/icon_home_rank");
            database.iconHomeCheckin = LoadSprite("Ui/Home/icon_home_checkin");
            database.iconHomeWorkshop = LoadSprite("Ui/Home/icon_home_workshop");
            database.iconHomeQuest = LoadSprite("Ui/Home/icon_home_quest");
            database.iconHomeEvent = LoadSprite("Ui/Home/icon_home_event");
            database.iconHomeEnergy = LoadSprite("Ui/Home/icon_home_energy");
            database.labelHomeCircle = LoadSprite("Ui/Home/label_home_circle");
            database.labelHomeRank = LoadSprite("Ui/Home/label_home_rank");
            database.labelHomeCheckin = LoadSprite("Ui/Home/label_home_checkin");
            database.labelHomeWorkshop = LoadSprite("Ui/Home/label_home_workshop");
            database.labelHomeQuest = LoadSprite("Ui/Home/label_home_quest");
            database.labelHomeEvent = LoadSprite("Ui/Home/label_home_event");
            GamePrefabBuilder.BindGameSprites(database);
            DressUpPrefabBuilder.BindSprites(database);
            EditorUtility.SetDirty(database);

            AssetDatabase.SaveAssets();
            BuildScene(database);
            Debug.Log($"[一裙又一裙] 重建完成：{items.Count} 件物品，{LevelCatalog.Count} 关");
        }

        [MenuItem("一裙又一裙/绑定背景", priority = 2)]
        public static void BindBackgroundsMenu()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (!TryBindBackgrounds(database))
            {
                Debug.LogWarning("[一裙又一裙] 还没有背景图，确认 Assets/DressSort/Art/Ui/Bgs/ 里有 bg_stage");
                return;
            }
            Debug.Log("[一裙又一裙] 已绑定五套背景，当前用秀台");
        }

        [MenuItem("一裙又一裙/打开场景", priority = 1)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath))
            {
                RebuildAll();
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                BindPlayScene();
            }
        }

        static void BuildScene(GameDatabase database)
        {
            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Sky;
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);

            var appGo = new GameObject("App");
            var app = appGo.AddComponent<App>();
            var so = new SerializedObject(app);
            so.FindProperty("database").objectReferenceValue = database;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            BindPlayScene();
        }

        static void BindPlayScene()
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            if (sceneAsset != null)
                EditorSceneManager.playModeStartScene = sceneAsset;
        }

        // -------------------------------------------------------------- 工具

        static ItemDef Upsert(string id) => LoadOrCreate<ItemDef>($"{ItemDir}/{id}.asset");

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        static bool TryBindCatalog(GameDatabase database)
        {
            if (database == null) return false;
            if (LoadSprite("Portraits/head_apricot") == null) return false;

            if (database.items == null)
                database.items = new List<ItemDef>();

            foreach (DressInfo info in Dresses)
            {
                ItemDef item = Upsert(info.id);
                item.id = info.id;
                item.displayName = info.label;
                item.slot = ItemSlot.Dress;
                item.icon = LoadSprite("Icons/dress_" + info.id);
                Sprite body = TryLoadSprite("Portraits/body_" + info.id);
                item.worn = body != null ? body : LoadSprite("Portraits/portrait_" + info.id);
                item.layeredWithHair = body != null;
                item.boardEligible = true;
                item.unlockedFromStart = info.fromStart;
                EditorUtility.SetDirty(item);
                if (!database.items.Contains(item))
                    database.items.Add(item);
            }

            ItemDef mystery = Upsert("mystery");
            mystery.id = "mystery";
            mystery.displayName = "神秘礼盒";
            mystery.icon = LoadSprite("Icons/dress_mystery");
            EditorUtility.SetDirty(mystery);
            database.mysteryItem = mystery;

            foreach ((string id, string label) in Wings)
            {
                ItemDef item = Upsert(id);
                item.id = id;
                item.displayName = label;
                item.slot = ItemSlot.Wings;
                item.icon = TryLoadSprite("Icons/ui_" + id);
                item.worn = TryLoadSprite("Wings/" + id);
                item.unlockedFromStart = false;
                EditorUtility.SetDirty(item);
                if (!database.items.Contains(item))
                    database.items.Add(item);
            }

            ItemDef apricot = null;
            for (int i = 0; i < Hairs.Length; i++)
            {
                (string id, string label) = Hairs[i];
                string key = HairKey(id);
                ItemDef item = Upsert(id);
                item.id = id;
                item.displayName = label;
                item.slot = ItemSlot.Hair;
                item.icon = TryLoadSprite("Icons/hair_" + key);
                item.worn = LoadSprite("Portraits/head_" + key);
                item.wornBack = TryLoadSprite("Portraits/hairback_" + key);
                item.accent = Palette.Lemon;
                item.boardEligible = false;
                item.unlockedFromStart = IsStartHair(id);
                EditorUtility.SetDirty(item);
                if (!database.items.Contains(item))
                    database.items.Add(item);
                if (id == "hair_apricot")
                    apricot = item;
            }

            ItemDef milktea = database.Find("hair_milktea");
            database.defaultHair = milktea != null && milktea.worn != null ? milktea : apricot;
            if (database.defaultDress == null)
                database.defaultDress = database.Find("teal_sailor");

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            return true;
        }

        static bool IsStartHair(string id)
        {
            for (int i = 0; i < StartHairs.Length; i++)
                if (StartHairs[i] == id) return true;
            return false;
        }

        static string HairKey(string id) =>
            id != null && id.StartsWith("hair_") ? id.Substring(5) : id;

        static bool TryBindBackgrounds(GameDatabase database)
        {
            if (database == null) return false;
            if (LoadSprite(BgFiles[DefaultBgIndex]) == null) return false;
            bool dirty = database.uiBgs == null || database.uiBgs.Count != BgFiles.Length
                || database.uiBg == null
                || database.uiBg.name == "bg";
            if (database.pageLoading == null)
            {
                database.pageLoading = LoadSprite("Loading/page_yellow");
                if (database.pageLoading != null)
                    dirty = true;
            }
            BindBackgroundList(database, forceDefault: dirty);
            if (dirty)
            {
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
            }
            return true;
        }

        static void BindBackgroundList(GameDatabase database, bool forceDefault = true)
        {
            var list = new List<Sprite>(BgFiles.Length);
            for (int i = 0; i < BgFiles.Length; i++)
                list.Add(LoadSprite(BgFiles[i]));
            database.uiBgs = list;
            if (forceDefault || database.uiBg == null || database.uiBg.name == "bg")
            {
                database.uiBgIndex = DefaultBgIndex;
                database.uiBg = list[DefaultBgIndex] != null ? list[DefaultBgIndex] : LoadSprite("Ui/bg");
                return;
            }

            if (database.uiBgIndex >= 0 && database.uiBgIndex < list.Count && list[database.uiBgIndex] != null)
                database.uiBg = list[database.uiBgIndex];
        }

        static Sprite TryLoadSprite(string relative)
        {
            foreach (string ext in new[] { "png", "jpg" })
            {
                string path = $"{Root}/Art/{relative}.{ext}";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                    return sprite;
            }
            return null;
        }

        static Sprite LoadSprite(string relative)
        {
            var sprite = TryLoadSprite(relative);
            if (sprite == null)
                Debug.LogWarning("[一裙又一裙] 缺图：" + relative);
            return sprite;
        }
    }
}
