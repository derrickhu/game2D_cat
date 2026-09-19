using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DressSort.EditorTools
{
    /// <summary>
    /// 把 Art 目录里的图打成 ItemDef / LevelDef / ChapterDef / GameDatabase，再建好场景。
    /// 美术管线跑完之后点一次这个菜单，工程就全接上了。
    /// </summary>
    public static class DressSortBuilder
    {
        const string Root = "Assets/DressSort";
        const string DataDir = Root + "/Data";
        const string ItemDir = DataDir + "/Items";
        const string LevelDir = DataDir + "/Levels";
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
            new DressInfo { id = "black_ribbon", label = "黑裙白结", accent = new Color32(0x2C, 0x24, 0x33, 0xFF), fromStart = true },
            new DressInfo { id = "pink_gingham", label = "粉格蛋糕裙", accent = new Color32(0xF3, 0xA7, 0xB8, 0xFF), fromStart = true },
            new DressInfo { id = "lemon_print", label = "柠檬衬衫裙", accent = new Color32(0xF0, 0xC8, 0x4A, 0xFF) },
            new DressInfo { id = "orange_slice", label = "橙子背心裙", accent = new Color32(0xFF, 0x8A, 0x3D, 0xFF) },
            new DressInfo { id = "ivory_lace", label = "象牙蕾丝裙", accent = new Color32(0xF2, 0xE3, 0xC0, 0xFF) },
            new DressInfo { id = "strawberry", label = "草莓开衫裙", accent = new Color32(0xE8, 0x6A, 0x8A, 0xFF) },
            new DressInfo { id = "grape_school", label = "葡萄校服", accent = new Color32(0xB9, 0x8C, 0xE8, 0xFF) },
        };

        static readonly (string id, string label)[] Wings =
        {
            ("wing_aqua", "湖水蝶翼"),
            ("wing_rose", "蜜桃花瓣翼"),
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
        };

        // 每关用哪五款、给什么奖励。奖励顺序保证十件都拿得到。
        static readonly (int[] palette, string reward, int height, int scramble, int limit)[] Levels =
        {
            (new[] { 0, 1, 2, 3, 4 }, "pink_gingham", 5, 16, 40),
            (new[] { 1, 2, 3, 4, 5 }, "lemon_print", 5, 20, 44),
            (new[] { 0, 2, 4, 5, 6 }, "orange_slice", 6, 24, 50),
            (new[] { 1, 3, 4, 6, 7 }, "ivory_lace", 6, 28, 54),
            (new[] { 0, 2, 5, 6, 7 }, "strawberry", 6, 32, 58),
            (new[] { 2, 3, 4, 5, 7 }, "grape_school", 6, 36, 62),
            (new[] { 0, 1, 5, 6, 7 }, "wing_aqua", 6, 40, 66),
            (new[] { 1, 2, 4, 6, 7 }, "wing_rose", 6, 44, 70),
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

                // 编辑器还停在旧的动物场景时，自动切到叠叠裙
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

        [MenuItem("叠叠裙/重建资源与场景", priority = 0)]
        public static void RebuildAll()
        {
            Directory.CreateDirectory(ItemDir);
            Directory.CreateDirectory(LevelDir);
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
                item.unlockedFromStart = true;
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
                item.unlockedFromStart = true;
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

            var levels = new List<LevelDef>();
            for (int i = 0; i < Levels.Length; i++)
            {
                (int[] palette, string reward, int height, int scramble, int limit) config = Levels[i];
                LevelDef level = UpsertLevel(i + 1);
                level.index = i + 1;
                level.themeName = "裙子";
                level.columns = config.palette.Length;
                level.columnHeight = config.height;
                level.moveLimit = config.limit;
                level.scrambleMoves = config.scramble;
                level.shuffles = 3;
                level.palette = new List<ItemDef>();
                foreach (int index in config.palette)
                    level.palette.Add(byId[Dresses[index].id]);
                level.mystery = mystery;
                level.reward = byId.TryGetValue(config.reward, out ItemDef r) ? r : null;
                EditorUtility.SetDirty(level);
                levels.Add(level);
            }

            var chapter = LoadOrCreate<ChapterDef>(DataDir + "/Chapter01.asset");
            chapter.chapterName = "粉裙日记";
            chapter.levels = levels;
            EditorUtility.SetDirty(chapter);

            var database = LoadOrCreate<GameDatabase>(DatabasePath);
            database.items = items;
            database.chapters = new List<ChapterDef> { chapter };
            database.defaultDress = byId["teal_sailor"];
            database.defaultHair = byId.ContainsKey("hair_milktea_long") && byId["hair_milktea_long"].worn != null
                ? byId["hair_milktea_long"]
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
            GamePrefabBuilder.BindGameSprites(database);
            DressUpPrefabBuilder.BindSprites(database);
            EditorUtility.SetDirty(database);

            AssetDatabase.SaveAssets();
            BuildScene(database);
            Debug.Log($"[叠叠裙] 重建完成：{items.Count} 件物品，{levels.Count} 关");
        }

        [MenuItem("叠叠裙/绑定背景", priority = 2)]
        public static void BindBackgroundsMenu()
        {
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (!TryBindBackgrounds(database))
            {
                Debug.LogWarning("[叠叠裙] 还没有背景图，确认 Assets/DressSort/Art/Ui/Bgs/ 里有 bg_stage");
                return;
            }
            Debug.Log("[叠叠裙] 已绑定五套背景，当前用秀台");
        }

        [MenuItem("叠叠裙/打开场景", priority = 1)]
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

        static LevelDef UpsertLevel(int index) => LoadOrCreate<LevelDef>($"{LevelDir}/Level{index:00}.asset");

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
                EditorUtility.SetDirty(item);
                if (!database.items.Contains(item))
                    database.items.Add(item);
            }

            ItemDef mystery = Upsert("mystery");
            mystery.id = "mystery";
            mystery.displayName = "神秘礼盒";
            mystery.icon = LoadSprite("Icons/dress_mystery");
            EditorUtility.SetDirty(mystery);

            foreach ((string id, string label) in Wings)
            {
                ItemDef item = Upsert(id);
                item.id = id;
                item.displayName = label;
                item.slot = ItemSlot.Wings;
                item.icon = TryLoadSprite("Icons/ui_" + id);
                item.worn = TryLoadSprite("Wings/" + id);
                EditorUtility.SetDirty(item);
                if (!database.items.Contains(item))
                    database.items.Add(item);
            }

            ItemDef apricot = null;
            ItemDef milkteaLong = null;
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
                item.unlockedFromStart = true;
                EditorUtility.SetDirty(item);
                if (!database.items.Contains(item))
                    database.items.Add(item);
                if (id == "hair_apricot")
                    apricot = item;
                if (id == "hair_milktea_long")
                    milkteaLong = item;
            }

            database.defaultHair = milkteaLong != null && milkteaLong.worn != null
                ? milkteaLong
                : apricot;
            if (database.defaultDress == null)
                database.defaultDress = database.Find("teal_sailor");

            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            return true;
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
                Debug.LogWarning("[叠叠裙] 缺图：" + relative);
            return sprite;
        }
    }
}
