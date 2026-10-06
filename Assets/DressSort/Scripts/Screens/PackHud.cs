using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 活动装箱页的预制：底图、顶栏、六列、收纳箱和两个按钮。
    /// </summary>
    public class PackHud : MonoBehaviour
    {
        public const float LaneW = 150f;
        public const float LaneH = 1050f;
        public const float LaneTop = 236f;
        public const float LanePitch = 168f;

        static readonly Color Cocoa = new Color32(0x4A, 0x26, 0x2C, 0xFF);
        static readonly Color Shade = new Color(0.29f, 0.08f, 0.14f, 0.7f);

        public Image backdrop;
        public RectTransform topBar;
        public Button backButton;
        public Button restartButton;
        public Text progressLabel;
        public Text queueLabel;
        public Image[] queueIcons = new Image[8];
        public Image[] laneFaces = new Image[PackBoard.ColumnCount];
        public RectTransform[] stacks = new RectTransform[PackBoard.ColumnCount];
        public Image[] lockIcons = new Image[PackBoard.ColumnCount];
        public Text[] lockLabels = new Text[PackBoard.ColumnCount];
        public Image[] adBadges = new Image[PackBoard.ColumnCount];
        public Image[] readyMarks = new Image[PackBoard.ColumnCount];
        public Image box;
        public Image[] boxSlots = new Image[PackBoard.BoxSlots];
        public Button packButton;
        public Button refillButton;
        public Text toastLabel;
        public Sprite laneSprite;
        public Sprite laneOnSprite;
        public Sprite laneReadySprite;

        public static PackHud Assemble(RectTransform parent, GameDatabase db)
        {
            var go = new GameObject("PackScreen", typeof(RectTransform), typeof(PackHud));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform);
            var hud = go.GetComponent<PackHud>();
            hud.BuildChildren(db);
            return hud;
        }

        void BuildChildren(GameDatabase db)
        {
            laneSprite = db != null ? db.packLane : null;
            laneOnSprite = db != null ? db.packLaneOn : null;
            laneReadySprite = db != null ? db.packLaneReady : null;

            backdrop = UiKit.Backdrop(transform, db != null ? db.BoardBgFor(1) : null);
            backdrop.preserveAspect = false;
            backdrop.raycastTarget = false;

            BuildLanes(db);
            BuildBox(db);
            BuildButtons(db);
            BuildTop(db);

            toastLabel = UiKit.Label(transform, "Toast", "", new Vector2(0.5f, 0f),
                new Vector2(0f, 1480f), new Vector2(900f, 56f), 32, Cocoa);
            var glow = toastLabel.gameObject.AddComponent<Outline>();
            glow.effectColor = new Color(1f, 0.98f, 0.93f, 0.92f);
            glow.effectDistance = new Vector2(2f, -2f);
        }

        void BuildTop(GameDatabase db)
        {
            topBar = UiKit.Stretch(transform, "TopBar");

            Image back = UiKit.Icon(topBar, "Back", db != null ? db.dressupBack : null,
                new Vector2(0f, 1f), new Vector2(36f + 48f, -20f - 52f), new Vector2(96f, 104f));
            back.preserveAspect = true;
            backButton = MakeHit(back);

            progressLabel = UiKit.Label(topBar, "Progress", "0/3 箱", new Vector2(1f, 1f),
                new Vector2(-36f - 90f, -20f - 48f), new Vector2(200f, 72f), 40, Cocoa);
            var rim = progressLabel.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color(1f, 0.98f, 0.93f, 1f);
            rim.effectDistance = new Vector2(2f, -2f);

            restartButton = UiKit.Button(topBar, "重开", new Vector2(1f, 1f),
                new Vector2(-360f, -20f - 48f), new Vector2(120f, 64f), Chip.White, 28, () => { });

            queueLabel = UiKit.Label(topBar, "Queue", "待整理 0", new Vector2(0f, 1f),
                new Vector2(150f, -168f), new Vector2(200f, 48f), 28, Cocoa, TextAnchor.MiddleLeft);
            for (int i = 0; i < queueIcons.Length; i++)
            {
                queueIcons[i] = UiKit.Icon(topBar, "Queue_" + i, null, new Vector2(0f, 1f),
                    new Vector2(280f + i * 58f, -168f), new Vector2(52f, 52f));
                queueIcons[i].enabled = false;
            }

            Text title = UiKit.Label(topBar, "Title", "整理装箱", new Vector2(0.5f, 1f),
                new Vector2(-40f, -20f - 48f), new Vector2(280f, 72f), 44, Cocoa);
            var titleRim = title.gameObject.AddComponent<Outline>();
            titleRim.effectColor = new Color(1f, 0.98f, 0.93f, 1f);
            titleRim.effectDistance = new Vector2(2f, -2f);
        }

        void BuildLanes(GameDatabase db)
        {
            for (int i = 0; i < PackBoard.ColumnCount; i++)
            {
                float x = (i - (PackBoard.ColumnCount - 1) * 0.5f) * LanePitch;
                Image face = UiKit.Icon(transform, "Lane_" + i, laneSprite, new Vector2(0.5f, 1f),
                    new Vector2(x, -LaneTop - LaneH * 0.5f), new Vector2(LaneW, LaneH));
                face.preserveAspect = false;
                face.type = laneSprite != null ? Image.Type.Sliced : Image.Type.Simple;
                face.raycastTarget = true;
                var button = face.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                button.transition = Selectable.Transition.None;
                laneFaces[i] = face;

                RectTransform stack = UiKit.Stretch(face.transform, "Stack");
                stacks[i] = stack;

                lockIcons[i] = UiKit.Icon(face.transform, "Lock", db != null ? db.packLock : null,
                    new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(72f, 84f));
                lockLabels[i] = UiKit.Label(face.transform, "LockText", "临时解锁", new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -50f), new Vector2(LaneW - 8f, 64f), 24, Cocoa);
                adBadges[i] = UiKit.Icon(face.transform, "Ad", db != null ? db.packAd : null,
                    new Vector2(0.5f, 0.5f), new Vector2(36f, 64f), new Vector2(40f, 40f));
                readyMarks[i] = UiKit.Icon(face.transform, "Ready", db != null ? db.packCheck : null,
                    new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(56f, 56f));
                readyMarks[i].enabled = false;
                lockIcons[i].gameObject.SetActive(false);
            }
        }

        void BuildBox(GameDatabase db)
        {
            var boxSize = new Vector2(535f, 420f);
            box = UiKit.Icon(transform, "Box", db != null ? db.packBox : null, new Vector2(0.5f, 0f),
                new Vector2(0f, 406f), boxSize);
            box.preserveAspect = true;
            box.raycastTarget = false;
            // 四个窗格在箱子贴图上的相对位置（中心为 0）。
            float[] slotX = { -0.289f, -0.095f, 0.096f, 0.289f };
            const float slotY = -0.091f;
            for (int i = 0; i < boxSlots.Length; i++)
            {
                boxSlots[i] = UiKit.Icon(box.transform, "Slot_" + i, null, new Vector2(0.5f, 0.5f),
                    new Vector2(slotX[i] * boxSize.x, slotY * boxSize.y), new Vector2(78f, 78f));
                boxSlots[i].enabled = false;
            }
        }

        void BuildButtons(GameDatabase db)
        {
            packButton = ActionButton("装箱", db != null ? db.packBtn : null, -200f, Color.white);
            refillButton = ActionButton("补充衣服", db != null ? db.packRefill : null, 200f, Cocoa);
        }

        Button ActionButton(string caption, Sprite sprite, float x, Color ink)
        {
            Image face = UiKit.Icon(transform, "Btn_" + caption, sprite, new Vector2(0.5f, 0f),
                new Vector2(x, 100f), new Vector2(320f, 156f));
            face.preserveAspect = true;
            if (face.sprite == null)
                face.sprite = UiKit.Rounded;
            Text label = UiKit.Label(face.transform, "Text", caption, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 4f), new Vector2(320f, 72f), 40, ink);
            label.fontStyle = FontStyle.Normal;
            if (ink == Color.white)
            {
                var shadow = label.gameObject.AddComponent<Shadow>();
                shadow.effectColor = Shade;
                shadow.effectDistance = new Vector2(0f, -3f);
            }
            return MakeHit(face);
        }

        public void SetQueue(int count, Sprite mystery)
        {
            if (queueLabel != null)
                queueLabel.text = "待整理 " + count;
            int shown = Mathf.Min(queueIcons.Length, count);
            for (int i = 0; i < queueIcons.Length; i++)
            {
                if (queueIcons[i] == null) continue;
                bool on = i < shown && mystery != null;
                queueIcons[i].enabled = on;
                if (on)
                    queueIcons[i].sprite = mystery;
            }
        }

        public void SetProgress(int done, int goal)
        {
            if (progressLabel != null)
                progressLabel.text = done + "/" + goal + " 箱";
        }

        public void SetBoxSlot(int index, Sprite sprite)
        {
            if (index < 0 || index >= boxSlots.Length || boxSlots[index] == null) return;
            boxSlots[index].sprite = sprite;
            boxSlots[index].enabled = sprite != null;
        }

        public void ClearBox()
        {
            for (int i = 0; i < boxSlots.Length; i++)
                SetBoxSlot(i, null);
        }

        public void ShowToast(string message)
        {
            if (toastLabel == null) return;
            toastLabel.text = message;
        }

        public void Wire(UnityAction onBack, UnityAction onRestart, UnityAction onPack, UnityAction onRefill,
            UnityAction<int> onLane)
        {
            Bind(backButton, onBack);
            Bind(restartButton, onRestart);
            Bind(packButton, onPack);
            Bind(refillButton, onRefill);
            for (int i = 0; i < laneFaces.Length; i++)
            {
                int index = i;
                Button button = laneFaces[i] != null ? laneFaces[i].GetComponent<Button>() : null;
                Bind(button, () => onLane?.Invoke(index));
            }
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
