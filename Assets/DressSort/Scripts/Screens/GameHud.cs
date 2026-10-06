using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 关卡页预制：底图、横杆、衣架、顶栏、底部花边托盘和三个道具按钮、暂停面板都在这里摆好。
    /// 换关只换底图或衣架图。
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        public const int MaxHangers = 8;
        public const float RodFromTop = 392f;
        public const float HangerWidth = 152f;
        public const float HangerHeight = 104f;
        public const float RodClearance = 40f;

        const float TrayTop = 330f;
        const float ToolSize = 196f;
        const float ToolY = 134f;

        static readonly Color Cocoa = new Color32(0x4A, 0x26, 0x2C, 0xFF);
        static readonly Color TextShade = new Color(0.29f, 0.08f, 0.14f, 0.7f);

        public Image backdrop;
        public Image rod;
        public RectTransform topBar;
        public Image gear;
        public Text levelLabel;
        public Image stepsPlate;
        public Text movesLabel;
        public RectTransform boardRoot;
        public Image[] hangers = new Image[MaxHangers];
        public RectTransform holdSlot;
        public Sprite laneSolved;
        public Sprite checkSolved;
        public Sprite sparkleSprite;
        public Sprite parcelSprite;
        public Sprite coverSprite;
        public Sprite tagSprite;
        public Sprite lockSprite;
        public Sprite keySprite;
        public Sprite alarmSprite;
        public Sprite bubbleSprite;
        public Image tray;
        public Button swapButton;
        public Button undoButton;
        public Button shuffleButton;
        public Text swapCount;
        public Text shuffleCount;
        public Text toastLabel;

        public RectTransform pauseLayer;
        public Button resumeButton;
        public Button restartButton;
        public Button mapButton;

        public static GameHud Assemble(RectTransform parent, GameDatabase db)
        {
            var go = new GameObject("GameScreen", typeof(RectTransform), typeof(GameHud));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var hud = go.GetComponent<GameHud>();
            hud.BuildChildren(db);
            return hud;
        }

        void BuildChildren(GameDatabase db)
        {
            backdrop = UiKit.Backdrop(transform, db != null ? db.BoardBgFor(1) : null);
            backdrop.preserveAspect = false;
            backdrop.raycastTarget = false;

            rod = UiKit.Icon(transform, "Rod", db != null ? db.gameRod : null, new Vector2(0.5f, 1f),
                new Vector2(0f, -RodFromTop + 4f), new Vector2(1010f, 84f));
            rod.preserveAspect = false;
            rod.raycastTarget = false;
            rod.enabled = rod.sprite != null;

            boardRoot = UiKit.Stretch(transform, "Board");

            hangers = new Image[MaxHangers];
            Sprite hangerSprite = db != null ? db.BoardHangerFor(1) : null;
            for (int i = 0; i < MaxHangers; i++)
            {
                hangers[i] = UiKit.Icon(boardRoot, "Hanger_" + i, hangerSprite,
                    new Vector2(0.5f, 1f), Vector2.zero, new Vector2(HangerWidth, HangerHeight));
                hangers[i].rectTransform.pivot = new Vector2(0.5f, 1f);
            }
            LayoutHangers(5);

            laneSolved = db != null ? db.boardLaneSolved : null;
            checkSolved = db != null ? db.boardCheckSolved : null;
            sparkleSprite = db != null ? db.rewardSparkle : null;
            parcelSprite = db != null ? db.gameParcel : null;
            coverSprite = db != null ? db.gameDustCover : null;
            tagSprite = db != null ? db.gameCoverTag : null;
            BindMechanics(db);

            BuildTray(db);

            holdSlot = UiKit.Rect(boardRoot, "Hold", new Vector2(0.5f, 0f),
                new Vector2(0f, TrayTop + 180f), new Vector2(280f, 280f));

            toastLabel = UiKit.Label(transform, "Toast", "", new Vector2(0.5f, 0f),
                new Vector2(0f, TrayTop + 350f), new Vector2(900f, 52f), 32, Cocoa);
            var glow = toastLabel.gameObject.AddComponent<Outline>();
            glow.effectColor = new Color(1f, 0.98f, 0.93f, 0.9f);
            glow.effectDistance = new Vector2(2f, -2f);

            BuildTopBar(db);
            BuildPause(db);
        }

        public void BindMechanics(GameDatabase db)
        {
            if (db == null) return;
            parcelSprite = db.gameParcel;
            coverSprite = db.gameDustCover;
            tagSprite = db.gameCoverTag;
            lockSprite = db.gameLock;
            keySprite = db.gameKey;
            alarmSprite = db.gameAlarm;
            bubbleSprite = db.gameBubble;
        }

        void BuildTray(GameDatabase db)
        {
            tray = UiKit.Icon(transform, "Tray", db != null ? db.dressupPanel : null, new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(0f, 860f));
            var rect = tray.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, TrayTop);
            rect.sizeDelta = new Vector2(0f, 860f);
            tray.preserveAspect = false;
            tray.raycastTarget = true;

            swapButton = Tool("交换", db != null ? db.btnGameSwap : null, -330f, db, out swapCount);
            undoButton = Tool("撤回", db != null ? db.btnGameUndo : null, 0f, db, out _);
            shuffleButton = Tool("随机", db != null ? db.btnGameShuffle : null, 330f, db, out shuffleCount);
        }

        /// <summary>方形道具键：图标在上半，字压在底部可可色条上，右上角挂剩余次数。</summary>
        Button Tool(string name, Sprite sprite, float x, GameDatabase db, out Text count)
        {
            Image face = UiKit.Icon(transform, "Btn_" + name, sprite, new Vector2(0.5f, 0f),
                new Vector2(x, ToolY), new Vector2(ToolSize, ToolSize));
            face.preserveAspect = true;
            Text label = Label(face.transform, name, new Vector2(0f, -ToolSize * 0.355f),
                new Vector2(ToolSize, 56f), 36, Color.white);
            Shade(label);

            count = null;
            if (name != "撤回")
            {
                Image badge = UiKit.Icon(face.transform, "Badge", db != null ? db.gameBadge : null,
                    new Vector2(0.5f, 0.5f), new Vector2(ToolSize * 0.42f, ToolSize * 0.42f), new Vector2(66f, 66f));
                badge.preserveAspect = true;
                badge.raycastTarget = false;
                if (badge.sprite == null)
                {
                    badge.sprite = UiKit.Circle;
                    badge.color = Cocoa;
                }
                count = Label(badge.transform, "0", new Vector2(0f, 1f), new Vector2(66f, 60f), 32, Color.white);
            }
            return MakeHit(face);
        }

        void BuildTopBar(GameDatabase db)
        {
            topBar = UiKit.Stretch(transform, "TopBar");

            gear = UiKit.Icon(topBar, "Gear",
                db != null && db.btnGameGear != null ? db.btnGameGear : db != null ? db.iconGear : null,
                new Vector2(0f, 1f), new Vector2(36f + 48f, -24f - 50f), new Vector2(96f, 104f));
            gear.preserveAspect = true;
            MakeHit(gear);

            levelLabel = UiKit.Label(topBar, "Level", "第1关", new Vector2(0f, 1f),
                new Vector2(150f + 160f, -24f - 48f), new Vector2(320f, 80f), 48, Cocoa);
            levelLabel.alignment = TextAnchor.MiddleLeft;
            var rim = levelLabel.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color(1f, 0.98f, 0.93f, 1f);
            rim.effectDistance = new Vector2(3f, -3f);

            stepsPlate = Capsule(topBar, "Steps", db != null ? db.btnGameSteps : null, Vector2.zero,
                new Vector2(300f, 88f));
            var sr = stepsPlate.rectTransform;
            sr.anchorMin = sr.anchorMax = new Vector2(1f, 1f);
            sr.anchoredPosition = new Vector2(-36f - 150f, -24f - 48f);
            stepsPlate.raycastTarget = false;
            movesLabel = Label(stepsPlate.transform, "剩余 40 步", new Vector2(0f, 3f), new Vector2(280f, 70f),
                36, Color.white);
        }

        void BuildPause(GameDatabase db)
        {
            pauseLayer = UiKit.Stretch(transform, "Pause");
            var dim = pauseLayer.gameObject.AddComponent<Image>();
            dim.color = new Color(0.16f, 0.07f, 0.1f, 0.55f);
            dim.raycastTarget = true;

            Image board = UiKit.Icon(pauseLayer, "Board", db != null ? db.gamePauseBoard : null,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(680f, 860f));
            board.preserveAspect = true;
            board.raycastTarget = true;
            if (board.sprite == null)
                board.sprite = UiKit.Rounded;

            Text title = Label(board.transform, "暂停一下", new Vector2(0f, 312f), new Vector2(560f, 90f), 50,
                Color.white);
            Shade(title);

            resumeButton = PauseButton(board.transform, "继续", db != null ? db.dressupSave : null, 120f,
                Color.white);
            restartButton = PauseButton(board.transform, "重新开始", db != null ? db.dressupTabOn : null, -35f,
                Cocoa);
            mapButton = PauseButton(board.transform, "返回首页", db != null ? db.dressupTabTrack : null, -190f,
                Color.white);

            pauseLayer.gameObject.SetActive(false);
        }

        Button PauseButton(Transform parent, string text, Sprite sprite, float y, Color ink)
        {
            Image face = Capsule(parent, "Btn_" + text, sprite, new Vector2(0f, y), new Vector2(440f, 116f));
            Text label = Label(face.transform, text, new Vector2(0f, 6f), new Vector2(420f, 80f), 42, ink);
            if (ink == Color.white)
                Shade(label);
            return MakeHit(face);
        }

        public void ShowPause(bool on)
        {
            if (pauseLayer == null) return;
            pauseLayer.gameObject.SetActive(on);
            if (on)
                pauseLayer.SetAsLastSibling();
        }

        public void SetCounts(int swaps, int shuffles)
        {
            if (swapCount != null) swapCount.text = swaps.ToString();
            if (shuffleCount != null) shuffleCount.text = shuffles.ToString();
            Dim(swapButton, swaps > 0);
            Dim(shuffleButton, shuffles > 0);
        }

        public void SetSwapArmed(bool armed)
        {
            if (swapButton == null) return;
            swapButton.transform.localScale = armed ? Vector3.one * 1.08f : Vector3.one;
        }

        static void Dim(Button button, bool ready)
        {
            if (button == null) return;
            var face = button.targetGraphic as Image;
            if (face != null)
                face.color = ready ? Color.white : new Color(0.78f, 0.74f, 0.76f, 1f);
        }

        public void ApplyTheme(Sprite background, Sprite hanger, Sprite rodSprite, int columns)
        {
            if (backdrop != null && background != null)
                backdrop.sprite = background;
            if (rod != null && rodSprite != null)
                rod.sprite = rodSprite;
            if (hanger != null && hangers != null)
            {
                for (int i = 0; i < hangers.Length; i++)
                {
                    if (hangers[i] != null)
                        hangers[i].sprite = hanger;
                }
            }
            LayoutHangers(columns);
        }

        public void LayoutHangers(int columns)
        {
            columns = Mathf.Clamp(columns, 1, MaxHangers);
            float pitch = 920f / columns;
            float width = Mathf.Min(HangerWidth, pitch * 0.88f);
            float height = HangerHeight * (width / HangerWidth);
            for (int i = 0; i < hangers.Length; i++)
            {
                if (hangers[i] == null) continue;
                bool on = i < columns;
                hangers[i].gameObject.SetActive(on);
                if (!on) continue;
                float x = (i - (columns - 1) * 0.5f) * pitch;
                var rect = hangers[i].rectTransform;
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(x, -RodFromTop);
                rect.sizeDelta = new Vector2(width, height);
            }
        }

        public Image Hanger(int index)
        {
            if (hangers == null || index < 0 || index >= hangers.Length)
                return null;
            return hangers[index];
        }

        public void Wire(UnityAction onSwap, UnityAction onUndo, UnityAction onShuffle, UnityAction onGear,
            UnityAction onResume, UnityAction onRestart, UnityAction onMap)
        {
            Bind(swapButton, onSwap);
            Bind(undoButton, onUndo);
            Bind(shuffleButton, onShuffle);
            Bind(gear != null ? gear.GetComponent<Button>() : null, onGear);
            Bind(resumeButton, onResume);
            Bind(restartButton, onRestart);
            Bind(mapButton, onMap);
        }

        public void ShowToast(string message)
        {
            if (toastLabel == null) return;
            toastLabel.text = message;
        }

        static Image Capsule(Transform parent, string name, Sprite sprite, Vector2 offset, Vector2 size)
        {
            Image image = UiKit.Icon(parent, name, sprite, new Vector2(0.5f, 0.5f), offset, size);
            image.preserveAspect = false;
            if (sprite != null && sprite.border.sqrMagnitude > 0f)
            {
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = sprite.rect.height / size.y;
            }
            return image;
        }

        static Text Label(Transform parent, string text, Vector2 offset, Vector2 size, int fontSize, Color color)
        {
            Text label = UiKit.Label(parent, "Text", text, new Vector2(0.5f, 0.5f), offset, size, fontSize, color);
            label.fontStyle = FontStyle.Normal;
            label.raycastTarget = false;
            return label;
        }

        static void Shade(Text label)
        {
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = TextShade;
            shadow.effectDistance = new Vector2(0f, -3f);
        }

        static Button MakeHit(Image image)
        {
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            return button;
        }

        static void Bind(Button button, UnityAction action)
        {
            if (button == null || action == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
