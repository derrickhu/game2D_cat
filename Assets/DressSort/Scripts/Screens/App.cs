using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    public enum ScreenId
    {
        Home,
        LevelMap,
        Game,
        Reward,
        DressUp,
    }

    /// <summary>
    /// 所有页面都是同一个 Canvas 下的面板，靠显示隐藏切换，没有场景加载，
    /// 这样在微信小游戏上不会有切页面的卡顿。
    /// </summary>
    public abstract class Panel : MonoBehaviour
    {
        protected App app;
        protected RectTransform root;

        public void Init(App owner, RectTransform frame)
        {
            app = owner;
            root = frame;
            Build();
        }

        protected abstract void Build();

        public virtual void OnShow() { }

        public virtual void OnHide() { }
    }

    public class App : MonoBehaviour
    {
        [SerializeField] GameDatabase database;

        public GameDatabase Database => database;
        public WardrobeService Wardrobe { get; private set; }

        /// <summary>刚通关的那一关，Reward 页要用。</summary>
        public LevelDef PendingLevel { get; set; }

        public LevelDef CurrentLevel { get; set; }

        readonly Dictionary<ScreenId, Panel> panels = new Dictionary<ScreenId, Panel>();
        readonly Dictionary<ScreenId, RectTransform> safeRoots = new Dictionary<ScreenId, RectTransform>();
        ScreenId current = ScreenId.Home;
        Canvas canvas;
        Image pageCover;
        bool firstShow = true;

        bool booted;

        void Awake()
        {
            if (Application.isPlaying)
                Boot(null, null);
        }

        /// <summary>
        /// 建界面。传入相机时 Canvas 走 ScreenSpaceCamera，
        /// 这样编辑器脚本不进播放模式也能把界面渲到 RenderTexture 上截图验证。
        /// </summary>
        public void Boot(Camera captureCamera, GameDatabase overrideDatabase)
        {
            if (booted) return;
            if (overrideDatabase != null)
                database = overrideDatabase;

            if (database == null)
            {
                Debug.LogError("[叠叠裙] 没有接上 GameDatabase，先跑菜单里的「重建资源与场景」");
                enabled = false;
                return;
            }

            booted = true;
            BindChrome(database);
            UiKit.Skin = database;
            Wardrobe = new WardrobeService(database);
            ClearBuiltUi();
            BuildCanvas(captureCamera);
            BuildPanels();
            BuildPageCover();
            Show(ScreenId.Home);
#if UNITY_EDITOR
            if (Application.isPlaying && captureCamera == null)
                StartCoroutine(PlayBootSplash());
#endif
        }

        public void SetDatabase(GameDatabase value) => database = value;

        static void BindChrome(GameDatabase db)
        {
            if (db == null) return;
#if UNITY_EDITOR
            const string ui = "Assets/DressSort/Art/Ui/";
            if (db.uiBtnTeal == null)
            {
                db.uiBtnTeal = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(ui + "btn_teal.png");
                db.uiBtnPink = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(ui + "btn_pink.png");
                db.uiBtnWhite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(ui + "btn_white.png");
                db.uiCard = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(ui + "card.png");
                db.uiCardOn = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(ui + "card_on.png");
                db.uiLane = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(ui + "lane.png");
            }

            if (db.uiBgs == null || db.uiBgs.Count == 0 || db.uiBg == null || db.uiBg.name == "bg")
            {
                db.uiBgs = new List<Sprite>
                {
                    LoadUiSprite(ui + "Bgs/bg_room"),
                    LoadUiSprite(ui + "Bgs/bg_boutique"),
                    LoadUiSprite(ui + "Bgs/bg_sky"),
                    LoadUiSprite(ui + "Bgs/bg_garden"),
                    LoadUiSprite(ui + "Bgs/bg_stage"),
                };
                db.uiBgIndex = 4;
                db.uiBg = db.uiBgs[4] != null ? db.uiBgs[4] : LoadUiSprite(ui + "bg");
            }

            if (db.pageLoading == null)
                db.pageLoading = LoadUiSprite("Assets/DressSort/Art/Loading/page_yellow");

            {
                const string dressup = ui + "DressUp/";
                db.dressupTray = LoadUiSprite(dressup + "tray");
                db.dressupBtnCream = LoadUiSprite(dressup + "btn_cream");
                db.dressupBtnPeach = LoadUiSprite(dressup + "btn_peach");
                db.dressupSave = LoadUiSprite(dressup + "btn_save");
                db.dressupCard = LoadUiSprite(dressup + "card");
                db.dressupCardOn = LoadUiSprite(dressup + "card_on");
                db.dressupCardLock = LoadUiSprite(dressup + "card_lock");
            }

            {
                const string home = "Assets/DressSort/Art/Ui/Home/";
                if (db.btnHomeStart == null)
                {
                    db.btnHomeStart = LoadUiSprite(home + "btn_home_start");
                    db.btnHomeDress = LoadUiSprite(home + "btn_home_dress");
                    db.iconHomeCircle = LoadUiSprite(home + "icon_home_circle");
                    db.iconHomeRank = LoadUiSprite(home + "icon_home_rank");
                    db.iconHomeCheckin = LoadUiSprite(home + "icon_home_checkin");
                    db.iconHomeWorkshop = LoadUiSprite(home + "icon_home_workshop");
                    db.iconHomeQuest = LoadUiSprite(home + "icon_home_quest");
                    db.iconHomeEvent = LoadUiSprite(home + "icon_home_event");
                }
                if (db.iconHomeEnergy == null)
                    db.iconHomeEnergy = LoadUiSprite(home + "icon_home_energy");
            }

            if (db.btnGameUndo == null || db.btnGameGear == null)
            {
                db.btnGameUndo = LoadUiSprite(ui + "Game/btn_undo");
                db.btnGameShuffle = LoadUiSprite(ui + "Game/btn_shuffle");
                db.btnGameBack = LoadUiSprite(ui + "Game/btn_back");
                db.btnGameSteps = LoadUiSprite(ui + "Game/btn_steps");
                db.btnGameGear = LoadUiSprite(ui + "Game/btn_gear");
            }
            if (db.boardBgs == null || db.boardBgs.Count < 2 || db.boardHangers == null || db.boardHangers.Count < 1)
            {
                db.boardBgs = new List<Sprite>
                {
                    LoadUiSprite(ui + "Bgs/bg_closet"),
                    LoadUiSprite(ui + "Bgs/bg_runway"),
                };
                db.boardHangers = new List<Sprite>
                {
                    LoadUiSprite(ui + "Game/hanger_closet"),
                    LoadUiSprite(ui + "Game/hanger_closet"),
                };
                db.boardBgBand = 10;
                if (db.iconHanger == null)
                    db.iconHanger = LoadUiSprite(ui + "Game/hanger_closet");
            }

            if (db.uiBtnTeal != null || db.uiBg != null)
                UnityEditor.EditorUtility.SetDirty(db);
#endif
        }

#if UNITY_EDITOR
        static Sprite LoadUiSprite(string pathNoExt)
        {
            var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(pathNoExt + ".jpg");
            return sprite != null
                ? sprite
                : UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(pathNoExt + ".png");
        }
#endif

        void ClearBuiltUi()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
            panels.Clear();
            safeRoots.Clear();
            canvas = null;
        }

        void BuildCanvas(Camera captureCamera)
        {
            var go = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);

            canvas = go.GetComponent<Canvas>();
            if (captureCamera != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = captureCamera;
                canvas.planeDistance = 10f;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            ScreenFit.Apply(captureCamera != null ? captureCamera : Camera.main, canvas);

            var es = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (es == null)
            {
                var esGo = new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
                es = esGo.GetComponent<UnityEngine.EventSystems.EventSystem>();
            }
            WxBridge.OverrideTouch(es.gameObject);
            if (WxBridge.IsMiniGame)
                WxBridge.ShowShareMenu();
        }

        void LateUpdate()
        {
            if (!booted || canvas == null) return;
            ScreenFit.Apply(Camera.main, canvas);
            foreach (var pair in safeRoots)
                ScreenFit.ApplySafeArea(pair.Value);
        }

        void BuildPanels()
        {
            Add<HomePanel>(ScreenId.Home);
            Add<LevelMapPanel>(ScreenId.LevelMap);
            Add<GamePanel>(ScreenId.Game);
            Add<RewardPanel>(ScreenId.Reward);
            Add<DressUpPanel>(ScreenId.DressUp);
        }

        void Add<T>(ScreenId id) where T : Panel
        {
            RectTransform frame = UiKit.Stretch(canvas.transform, id.ToString());

            UiKit.Backdrop(frame, database.ActiveBg);

            RectTransform safe = UiKit.Stretch(frame, "Safe");
            ScreenFit.ApplySafeArea(safe);
            safeRoots[id] = safe;

            var panel = frame.gameObject.AddComponent<T>();
            panel.Init(this, safe);
            frame.gameObject.SetActive(false);
            panels[id] = panel;
        }

#if UNITY_EDITOR
        IEnumerator PlayBootSplash()
        {
            string path = System.IO.Path.GetFullPath("Assets/DressSort/Art/Loading/loading.mp4");
            if (!System.IO.File.Exists(path) || canvas == null)
                yield break;

            RectTransform frame = UiKit.Stretch(canvas.transform, "BootSplash");
            frame.SetAsLastSibling();
            var raw = frame.gameObject.AddComponent<RawImage>();
            raw.color = Color.white;
            raw.raycastTarget = true;

            var player = frame.gameObject.AddComponent<UnityEngine.Video.VideoPlayer>();
            player.playOnAwake = false;
            player.renderMode = UnityEngine.Video.VideoRenderMode.RenderTexture;
            player.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.None;
            player.isLooping = false;
            player.url = "file://" + path;
            var rt = new RenderTexture(720, 1280, 0);
            player.targetTexture = rt;
            raw.texture = rt;
            player.Prepare();
            float wait = 0f;
            while (!player.isPrepared && wait < 2f)
            {
                wait += Time.unscaledDeltaTime;
                yield return null;
            }

            if (player.isPrepared)
            {
                player.Play();
                while (player.isPlaying)
                    yield return null;
            }

            player.Stop();
            if (rt != null)
                rt.Release();
            if (Application.isPlaying)
                Destroy(frame.gameObject);
            else
                DestroyImmediate(frame.gameObject);
        }
#endif

        void BuildPageCover()
        {
            if (database.pageLoading == null) return;
            RectTransform frame = UiKit.Stretch(canvas.transform, "PageLoading");
            pageCover = frame.gameObject.AddComponent<Image>();
            pageCover.sprite = database.pageLoading;
            pageCover.color = Color.white;
            pageCover.raycastTarget = true;
            frame.SetAsLastSibling();
            frame.gameObject.SetActive(false);
        }

        public void Show(ScreenId id)
        {
            if (firstShow || pageCover == null || !Application.isPlaying)
            {
                firstShow = false;
                ApplyShow(id);
                return;
            }

            StopCoroutine("ShowCovered");
            StartCoroutine(ShowCovered(id));
        }

        IEnumerator ShowCovered(ScreenId id)
        {
            pageCover.gameObject.SetActive(true);
            pageCover.transform.SetAsLastSibling();
            yield return null;
            ApplyShow(id);
            yield return new WaitForSeconds(0.2f);
            pageCover.gameObject.SetActive(false);
        }

        void ApplyShow(ScreenId id)
        {
            if (panels.TryGetValue(current, out Panel previous) && previous != null)
            {
                previous.OnHide();
                previous.gameObject.SetActive(false);
            }

            current = id;
            if (panels.TryGetValue(id, out Panel next) && next != null)
            {
                next.gameObject.SetActive(true);
                next.OnShow();
            }
        }

        public bool StartLevel(LevelDef level)
        {
            if (level == null) return false;
            if (!Wardrobe.SpendEnergy())
                return false;
            CurrentLevel = level;
            Show(ScreenId.Game);
            return true;
        }

        public T PanelOf<T>(ScreenId id) where T : Panel =>
            panels.TryGetValue(id, out Panel panel) ? panel as T : null;

        public LevelDef LevelAt(int index)
        {
            List<LevelDef> all = database.AllLevels();
            return index >= 1 && index <= all.Count ? all[index - 1] : null;
        }

        public int LevelCount => database.AllLevels().Count;

        /// <summary>切换全页背景。后面做换装背景页时直接调这个。</summary>
        public void SetBackground(int index)
        {
            database.SelectBg(index);
            Sprite bg = database.ActiveBg;
            foreach (var pair in panels)
            {
                Transform backdrop = pair.Value.transform.Find("Backdrop");
                if (backdrop == null) continue;
                var image = backdrop.GetComponent<Image>();
                if (image != null)
                    image.sprite = bg;
            }
        }
    }
}
