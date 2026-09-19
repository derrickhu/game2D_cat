using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 关卡页预制：底图（自带横杆）、衣架、顶栏和底栏按钮都在这里摆好。
    /// 换关只换底图或衣架图。
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        public const int MaxHangers = 7;
        public const float RodFromTop = 392f;
        public const float HangerWidth = 152f;
        public const float HangerHeight = 104f;
        public const float RodClearance = 40f;

        public Image backdrop;
        public Image gear;
        public Text levelLabel;
        public Image stepsPlate;
        public Text movesLabel;
        public RectTransform boardRoot;
        public Image[] hangers = new Image[MaxHangers];
        public RectTransform holdSlot;
        public Sprite laneSolved;
        public Sprite checkSolved;
        public Button undoButton;
        public Button shuffleButton;
        public Button backButton;
        public Text toastLabel;

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

            boardRoot = UiKit.Stretch(transform, "Board");

            gear = UiKit.Icon(transform, "Gear",
                db != null && db.btnGameGear != null ? db.btnGameGear : db != null ? db.iconGear : null,
                new Vector2(0f, 1f), new Vector2(80f, -80f), new Vector2(72f, 72f));

            levelLabel = UiKit.Label(transform, "Level", "第1关", new Vector2(0.5f, 1f),
                new Vector2(0f, -80f), new Vector2(320f, 72f), 44, Palette.Caption);

            stepsPlate = UiKit.Icon(transform, "Steps",
                db != null ? db.btnGameSteps : null,
                new Vector2(1f, 1f), new Vector2(-176f, -80f), new Vector2(260f, 72f));
            if (stepsPlate.sprite == null)
                stepsPlate.sprite = UiKit.SpriteOf(Chip.White);
            stepsPlate.type = Image.Type.Simple;
            stepsPlate.preserveAspect = true;
            movesLabel = UiKit.Label(stepsPlate.transform, "Text", "剩余40步",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 64f), 32, Palette.Cream);

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

            holdSlot = UiKit.Rect(boardRoot, "Hold", new Vector2(0.5f, 0f),
                new Vector2(0f, 400f), new Vector2(280f, 280f));

            undoButton = ArtButton("撤回", db != null ? db.btnGameUndo : null, new Vector2(-320f, 168f));
            shuffleButton = ArtButton("随机", db != null ? db.btnGameShuffle : null, new Vector2(0f, 168f));
            backButton = ArtButton("返回", db != null ? db.btnGameBack : null, new Vector2(320f, 168f));

            toastLabel = UiKit.Label(transform, "Toast", "", new Vector2(0.5f, 0f),
                new Vector2(0f, 300f), new Vector2(900f, 48f), 30, Palette.Caption);
        }

        Button ArtButton(string name, Sprite sprite, Vector2 offset)
        {
            RectTransform rect = UiKit.Rect(transform, "Btn_" + name, new Vector2(0.5f, 0f),
                offset, new Vector2(220f, 248f));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            return button;
        }

        public void ApplyTheme(Sprite background, Sprite hanger, int columns)
        {
            if (backdrop != null && background != null)
                backdrop.sprite = background;
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

        public void Wire(UnityAction onUndo, UnityAction onShuffle, UnityAction onBack)
        {
            Bind(undoButton, onUndo);
            Bind(shuffleButton, onShuffle);
            Bind(backButton, onBack);
        }

        static void Bind(Button button, UnityAction action)
        {
            if (button == null || action == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        public void ShowToast(string message)
        {
            if (toastLabel == null) return;
            toastLabel.text = message;
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
