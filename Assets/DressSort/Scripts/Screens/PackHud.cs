using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 活动「衣服排排上车」的页面，全部用代码搭。
    /// 从上到下：洗衣店墙（洗衣机、衣物门和排队的问号）、七条衣架、街上的车和进度、两个大按钮。
    /// 墙贴顶、街贴底，中间的地板随屏幕高度伸缩，衣架在地板里居中。
    /// </summary>
    public class PackHud : MonoBehaviour
    {
        public const float LaneW = 136f;
        public const float LaneH = 596f;
        public const float LanePitch = 148f;
        public const float Item = 104f;
        public const float Step = 60f;
        public const float StackPad = 14f;
        const float WallH = 549f;
        const float StreetH = 764f;
        const float MachineFloor = 528f;
        const float ButtonY = 100f;
        const float TrackY = 240f;
        const float CarFloor = 300f;
        const int QueueShown = 3;

        static readonly Color Cocoa = new Color32(0x4A, 0x26, 0x2C, 0xFF);
        static readonly Color Shade = new Color(0.29f, 0.08f, 0.14f, 0.7f);
        static readonly Color LaneIdle = new Color(1f, 1f, 1f, 0.82f);
        static readonly Color LaneSelected = new Color(1f, 0.86f, 0.9f, 1f);
        static readonly Color LaneReady = new Color(0.62f, 0.92f, 0.62f, 1f);
        static readonly Color LaneLocked = new Color(0.78f, 0.75f, 0.76f, 0.9f);
        static readonly Color LaneAd = new Color(0.45f, 0.86f, 0.5f, 1f);
        static readonly Color NodeIdle = new Color(1f, 0.97f, 0.93f, 1f);

        /// <summary>每辆车窗户那一排在贴图里的位置（左、下、右、上，按宽高归一）。双层车两排。</summary>
        static readonly Vector4[][] SeatBands =
        {
            new[] { new Vector4(0.4155f, 0.5688f, 0.9144f, 0.8375f) },
            new[] { new Vector4(0.3252f, 0.6081f, 0.9375f, 0.8717f) },
            new[] { new Vector4(0.1745f, 0.5545f, 0.9618f, 0.8536f) },
            new[] { new Vector4(0.086f, 0.5943f, 0.9244f, 0.7116f), new Vector4(0.2373f, 0.3005f, 0.9254f, 0.4218f) },
        };
        static readonly Vector4[] DriverWindows =
        {
            new Vector4(0.191f, 0.5688f, 0.3634f, 0.8397f),
            new Vector4(0.1186f, 0.6081f, 0.2839f, 0.8717f),
            new Vector4(0.0556f, 0.4486f, 0.1363f, 0.8536f),
            new Vector4(0.0715f, 0.3005f, 0.1834f, 0.4434f),
        };
        static readonly float[] CarHeights = { 380f, 360f, 273f, 440f };
        const float CarMaxW = 980f;

        GameDatabase db;
        Sprite seatSprite;
        Sprite laneSprite;
        Sprite[] carSprites = new Sprite[PackBoard.CarCount];

        public Sprite Mystery { get; private set; }
        public Sprite Check { get; private set; }
        public Sprite Crown { get; private set; }

        public RectTransform topBar;
        public Button backButton;
        public Button restartButton;

        public RectTransform machine;
        public Image washer;
        public Image gate;
        public Image[] queue = new Image[QueueShown];

        public Image[] lanes = new Image[PackBoard.ColumnCount];
        public RectTransform[] stacks = new RectTransform[PackBoard.ColumnCount];
        public Image[] chevrons = new Image[PackBoard.ColumnCount];
        public Image[] lockIcons = new Image[PackBoard.ColumnCount];
        public Text[] lockLabels = new Text[PackBoard.ColumnCount];
        public Image[] adBadges = new Image[PackBoard.ColumnCount];
        public Image[] readyMarks = new Image[PackBoard.ColumnCount];

        public RectTransform carRoot;
        public Image carBody;
        public Image driverKey;
        public Image[] seats = new Image[PackBoard.MaxSeats];
        public Image[] seatIcons = new Image[PackBoard.MaxSeats];
        public Image[] seatChecks = new Image[PackBoard.MaxSeats];
        public RectTransform track;
        public Image trackFill;
        public Image[] nodes = new Image[PackBoard.CarCount];
        public Image[] nodeCars = new Image[PackBoard.CarCount];
        public Image[] nodeChecks = new Image[PackBoard.CarCount];
        const float TrackW = 720f;

        public Button boardButton;
        public Button refillButton;
        bool boardHot;

        public Image toastPlate;
        public Text toastLabel;
        public Text playsLabel;
        Coroutine toastRoutine;

        public RectTransform popupLayer;
        public RectTransform fxLayer;

        public static PackHud Assemble(RectTransform parent, RectTransform safe, GameDatabase db)
        {
            var go = new GameObject("PackScreen", typeof(RectTransform), typeof(PackHud));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var hud = go.GetComponent<PackHud>();
            hud.Build(db, safe);
            return hud;
        }

        static Sprite Res(string name) => Resources.Load<Sprite>("Pack/" + name);

        void Build(GameDatabase database, RectTransform safe)
        {
            db = database;
            Mystery = Res("mystery");
            Crown = Res("crown");
            Check = db != null && db.packCheck != null ? db.packCheck : null;
            seatSprite = Res("seat");
            laneSprite = Res("lane");
            for (int i = 0; i < carSprites.Length; i++)
                carSprites[i] = Res("car_" + i);

            BuildBackdrop();
            BuildMachine();
            BuildLanes();
            BuildCar();
            BuildTrack();
            BuildButtons();
            BuildToast();
            BuildTop(safe);

            fxLayer = UiKit.Stretch(transform, "FxHost");
            popupLayer = UiKit.Stretch(transform, "Popups");
        }

        // ------------------------------------------------------------ 背景

        void BuildBackdrop()
        {
            Image floor = UiKit.Backdrop(transform, Res("bg_floor"));
            floor.name = "Floor";
            floor.rectTransform.offsetMax = new Vector2(0f, -WallH + 30f);
            floor.rectTransform.offsetMin = new Vector2(0f, StreetH - 30f);
            if (floor.sprite == null) floor.color = new Color32(0xFF, 0xF3, 0xD8, 0xFF);

            Image wall = UiKit.Icon(transform, "Wall", Res("bg_wall"), new Vector2(0.5f, 1f),
                new Vector2(0f, -WallH * 0.5f), new Vector2(ScreenFit.DesignW, WallH));
            wall.preserveAspect = false;
            if (wall.sprite == null) wall.color = new Color32(0xFF, 0xDA, 0xD7, 0xFF);
            // 刘海屏墙上方多出来的部分用墙色补满。
            Image cap = UiKit.Icon(transform, "WallCap", UiKit.Rounded, new Vector2(0.5f, 1f),
                new Vector2(0f, 400f), new Vector2(ScreenFit.DesignW + 40f, 800f));
            cap.color = new Color32(0xFF, 0xDA, 0xD7, 0xFF);
            cap.transform.SetSiblingIndex(0);

            Image street = UiKit.Icon(transform, "Street", Res("bg_street"), new Vector2(0.5f, 0f),
                new Vector2(0f, StreetH * 0.5f), new Vector2(ScreenFit.DesignW, StreetH));
            street.preserveAspect = false;
            if (street.sprite == null) street.color = new Color32(0xD7, 0xCD, 0xC8, 0xFF);
            Image under = UiKit.Icon(transform, "StreetCap", UiKit.Rounded, new Vector2(0.5f, 0f),
                new Vector2(0f, -400f), new Vector2(ScreenFit.DesignW + 40f, 800f));
            under.color = new Color32(0xD7, 0xCD, 0xC8, 0xFF);
            under.transform.SetSiblingIndex(0);
        }

        // ------------------------------------------------------------ 洗衣机和衣物门

        void BuildMachine()
        {
            machine = UiKit.Rect(transform, "Machine", new Vector2(0.5f, 1f), new Vector2(0f, -MachineFloor),
                new Vector2(ScreenFit.DesignW, 10f));
            machine.pivot = new Vector2(0.5f, 0f);
            machine.anchoredPosition = new Vector2(0f, -MachineFloor);

            washer = UiKit.Icon(machine, "Washer", Res("washer"), new Vector2(0.5f, 0f),
                new Vector2(-300f, 139f), new Vector2(220f, 278f));
            gate = UiKit.Icon(machine, "Gate", Res("gate"), new Vector2(0.5f, 0f),
                new Vector2(30f, 164f), new Vector2(240f, 327f));
            for (int i = 0; i < queue.Length; i++)
            {
                queue[i] = UiKit.Icon(gate.transform, "Queue_" + i, Mystery, new Vector2(0.5f, 0f),
                    QueueSlot(i), new Vector2(96f, 96f));
            }
        }

        /// <summary>门里排队的第 i 个（0 是最下面、下一件要落的）。</summary>
        public Vector2 QueueSlot(int i) => new Vector2(0f, 62f + i * 74f);

        /// <summary>补充的衣服从门底下出来。</summary>
        public Vector3 GateMouth()
        {
            if (gate == null) return transform.position;
            return gate.rectTransform.TransformPoint(new Vector3(0f, -gate.rectTransform.rect.height * 0.5f + 40f, 0f));
        }

        /// <summary>屏幕顶上被刘海和胶囊占掉以后，洗衣机和门按剩下的高度缩一点。</summary>
        void FitMachine()
        {
            if (machine == null) return;
            float room = MachineFloor - ScreenFit.TopPad - 12f;
            float scale = Mathf.Clamp(room / 340f, 0.62f, 1f);
            machine.localScale = new Vector3(1f, scale, 1f);
            if (washer != null) washer.rectTransform.localScale = new Vector3(scale, 1f, 1f);
            if (gate != null) gate.rectTransform.localScale = new Vector3(scale, 1f, 1f);
        }

        // ------------------------------------------------------------ 衣架

        float LaneCenterFromTop()
        {
            float floorTop = WallH;
            float floorBottom = Mathf.Max(ScreenFit.CanvasH, ScreenFit.DesignH) - StreetH;
            return (floorTop + floorBottom) * 0.5f;
        }

        void BuildLanes()
        {
            Sprite lockSprite = db != null ? db.packLock : null;
            Sprite adSprite = db != null ? db.packAd : null;
            for (int i = 0; i < PackBoard.ColumnCount; i++)
            {
                Image face = UiKit.Icon(transform, "Lane_" + i, laneSprite != null ? laneSprite : UiKit.Rounded,
                    new Vector2(0.5f, 1f), Vector2.zero, new Vector2(LaneW, LaneH));
                face.preserveAspect = false;
                face.type = Image.Type.Sliced;
                face.color = LaneIdle;
                face.raycastTarget = true;
                var button = face.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                button.transition = Selectable.Transition.None;
                lanes[i] = face;

                chevrons[i] = UiKit.Icon(face.transform, "Chevron", Res("chevron"), new Vector2(0.5f, 1f),
                    new Vector2(0f, -26f), new Vector2(54f, 36f));
                stacks[i] = UiKit.Stretch(face.transform, "Stack");

                lockIcons[i] = UiKit.Icon(face.transform, "Lock", lockSprite, new Vector2(0.5f, 1f),
                    new Vector2(0f, -70f), new Vector2(70f, 84f));
                lockLabels[i] = UiKit.Label(face.transform, "LockText", "", new Vector2(0.5f, 0.5f),
                    new Vector2(0f, -30f), new Vector2(LaneW, 360f), 30, Color.white);
                lockLabels[i].lineSpacing = 0.92f;
                var rim = lockLabels[i].gameObject.AddComponent<Outline>();
                rim.effectColor = new Color(0.25f, 0.18f, 0.2f, 0.55f);
                rim.effectDistance = new Vector2(2f, -2f);
                adBadges[i] = UiKit.Icon(face.transform, "Ad", adSprite, new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 130f), new Vector2(64f, 70f));
                readyMarks[i] = UiKit.Icon(face.transform, "Ready", Check, new Vector2(0.5f, 1f),
                    new Vector2(0f, -36f), new Vector2(76f, 82f));
                readyMarks[i].enabled = false;
                lockIcons[i].gameObject.SetActive(false);
                lockLabels[i].gameObject.SetActive(false);
                adBadges[i].gameObject.SetActive(false);
            }
            PlaceLanes();
        }

        void PlaceLanes()
        {
            float center = LaneCenterFromTop();
            for (int i = 0; i < lanes.Length; i++)
            {
                if (lanes[i] == null) continue;
                float x = (i - (PackBoard.ColumnCount - 1) * 0.5f) * LanePitch;
                lanes[i].rectTransform.anchoredPosition = new Vector2(x, -center);
            }
        }

        /// <summary>第 row 件（从顶上数）在列里的本地位置，列一共 count 件。</summary>
        public static Vector2 SlotPosition(int row, int count)
        {
            int fromBottom = Mathf.Max(0, count - 1 - row);
            return new Vector2(0f, StackPad + Item * 0.5f + fromBottom * Step);
        }

        /// <summary>列顶上第 depth 个空位（0 是紧贴着现有衣服那一格）的世界坐标。</summary>
        public Vector3 SlotWorld(int column, int fromBottom)
        {
            RectTransform stack = stacks[column];
            var local = new Vector2(0f, StackPad + Item * 0.5f + fromBottom * Step);
            Vector2 pivotOffset = new Vector2(0f, -stack.rect.height * 0.5f);
            return stack.TransformPoint(local + pivotOffset);
        }

        public void PaintLane(int column, bool open, bool ad, bool ready, bool selected, string lockText)
        {
            Image face = lanes[column];
            if (face == null) return;
            face.color = !open ? (ad ? LaneAd : LaneLocked)
                : ready ? LaneReady
                : selected ? LaneSelected
                : LaneIdle;
            if (chevrons[column] != null) chevrons[column].gameObject.SetActive(open && !ready);
            if (lockIcons[column] != null) lockIcons[column].gameObject.SetActive(!open);
            if (adBadges[column] != null) adBadges[column].gameObject.SetActive(!open && ad);
            if (lockLabels[column] != null)
            {
                lockLabels[column].gameObject.SetActive(!open);
                lockLabels[column].text = Vertical(lockText);
                lockLabels[column].rectTransform.anchoredPosition = new Vector2(0f, ad ? -60f : -30f);
            }
            if (readyMarks[column] != null) readyMarks[column].enabled = ready;
        }

        static string Vertical(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new System.Text.StringBuilder(text.Length * 2);
            for (int i = 0; i < text.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(text[i]);
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------ 车

        void BuildCar()
        {
            carRoot = UiKit.Rect(transform, "Car", new Vector2(0.5f, 0f), new Vector2(0f, CarFloor), new Vector2(600f, 300f));
            carRoot.pivot = new Vector2(0.5f, 0f);
            Image shadow = UiKit.Icon(carRoot, "Shadow", UiKit.Circle, new Vector2(0.5f, 0f), new Vector2(0f, 6f),
                new Vector2(560f, 36f));
            shadow.preserveAspect = false;
            shadow.color = new Color(0.3f, 0.22f, 0.24f, 0.25f);
            carBody = UiKit.Icon(carRoot, "Body", carSprites[0], new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 300f));
            carBody.preserveAspect = false;
            carBody.rectTransform.anchorMin = Vector2.zero;
            carBody.rectTransform.anchorMax = Vector2.one;
            carBody.rectTransform.offsetMin = Vector2.zero;
            carBody.rectTransform.offsetMax = Vector2.zero;
            driverKey = UiKit.Icon(carBody.transform, "Key", db != null ? db.gameKey : null, new Vector2(0f, 0f),
                Vector2.zero, new Vector2(60f, 60f));
            for (int i = 0; i < seats.Length; i++)
            {
                seats[i] = UiKit.Icon(carBody.transform, "Seat_" + i, seatSprite, new Vector2(0f, 0f), Vector2.zero,
                    new Vector2(80f, 80f));
                seatIcons[i] = UiKit.Icon(seats[i].transform, "Dress", null, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f),
                    new Vector2(70f, 70f));
                seatIcons[i].enabled = false;
                seatChecks[i] = UiKit.Icon(seats[i].transform, "Ok", Check, new Vector2(1f, 0f), new Vector2(-4f, 6f),
                    new Vector2(34f, 36f));
                seatChecks[i].enabled = false;
            }
        }

        /// <summary>换成第 car 辆车，座位按窗户那一排均分。</summary>
        public void ShowCar(int car)
        {
            car = Mathf.Clamp(car, 0, PackBoard.CarCount - 1);
            Sprite sprite = carSprites[car];
            float h = CarHeights[car];
            float w = sprite != null ? h * sprite.rect.width / sprite.rect.height : h * 2f;
            if (w > CarMaxW)
            {
                h *= CarMaxW / w;
                w = CarMaxW;
            }
            carRoot.sizeDelta = new Vector2(w, h);
            carRoot.anchoredPosition = new Vector2(0f, CarFloor + ScreenFit.BottomPad);
            carRoot.localScale = Vector3.one;
            carBody.sprite = sprite;
            Transform shadow = carRoot.Find("Shadow");
            if (shadow != null) ((RectTransform)shadow).sizeDelta = new Vector2(w * 0.92f, 36f);

            Vector4 dw = DriverWindows[car];
            if (driverKey != null)
            {
                float kh = (dw.w - dw.y) * h;
                driverKey.rectTransform.sizeDelta = new Vector2(kh * 0.82f, kh * 0.82f);
                driverKey.rectTransform.anchoredPosition = new Vector2((dw.x + dw.z) * 0.5f * w, (dw.y + dw.w) * 0.5f * h);
            }

            int count = PackBoard.SeatsOf(car);
            Vector4[] bands = SeatBands[car];
            int perRow = Mathf.CeilToInt(count / (float)bands.Length);
            int index = 0;
            for (int b = 0; b < bands.Length; b++)
            {
                Vector4 band = bands[b];
                float bw = (band.z - band.x) * w;
                float bh = (band.w - band.y) * h;
                int n = Mathf.Min(perRow, count - index);
                float size = Mathf.Min(bw / n * 0.9f, bh * 1.5f);
                float gap = bw / n;
                for (int k = 0; k < n; k++, index++)
                {
                    RectTransform seat = seats[index].rectTransform;
                    seat.gameObject.SetActive(true);
                    seat.sizeDelta = new Vector2(size, size);
                    seat.anchoredPosition = new Vector2(band.x * w + gap * (k + 0.5f), (band.y + band.w) * 0.5f * h);
                    seat.localScale = Vector3.one;
                    seatIcons[index].rectTransform.sizeDelta = new Vector2(size * 0.86f, size * 0.86f);
                    seatChecks[index].rectTransform.sizeDelta = new Vector2(size * 0.44f, size * 0.47f);
                    SetSeat(index, null);
                }
            }
            for (; index < seats.Length; index++)
                seats[index].gameObject.SetActive(false);
        }

        public void SetSeat(int index, Sprite dress)
        {
            if (index < 0 || index >= seats.Length || seats[index] == null) return;
            seatIcons[index].sprite = dress;
            seatIcons[index].enabled = dress != null;
            seatChecks[index].enabled = dress != null;
        }

        public Vector3 SeatWorld(int index)
        {
            if (index < 0 || index >= seats.Length || seats[index] == null) return carRoot.position;
            return seats[index].transform.position;
        }

        public RectTransform SeatRect(int index) =>
            index >= 0 && index < seats.Length && seats[index] != null ? seats[index].rectTransform : null;

        public float CarWidth => carRoot != null ? carRoot.sizeDelta.x : 600f;

        // ------------------------------------------------------------ 进度条

        void BuildTrack()
        {
            Image line = UiKit.Icon(transform, "Track", UiKit.Rounded, new Vector2(0.5f, 0f),
                new Vector2(0f, TrackY), new Vector2(TrackW, 22f));
            line.type = Image.Type.Sliced;
            line.preserveAspect = false;
            line.color = new Color(1f, 1f, 1f, 0.92f);
            var lineRim = line.gameObject.AddComponent<Outline>();
            lineRim.effectColor = new Color(0.55f, 0.45f, 0.45f, 0.6f);
            lineRim.effectDistance = new Vector2(2f, -2f);
            track = line.rectTransform;

            trackFill = UiKit.Icon(track, "Fill", UiKit.Rounded, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 14f));
            trackFill.type = Image.Type.Sliced;
            trackFill.preserveAspect = false;
            trackFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            trackFill.rectTransform.anchoredPosition = new Vector2(4f, 0f);
            trackFill.color = new Color(0.55f, 0.85f, 0.95f, 1f);

            for (int i = 0; i < nodes.Length; i++)
            {
                Image node = UiKit.Icon(track, "Node_" + i, UiKit.Circle, new Vector2(0f, 0.5f),
                    new Vector2(NodeX(i), 0f), new Vector2(84f, 84f));
                node.color = NodeIdle;
                var rim = node.gameObject.AddComponent<Outline>();
                rim.effectColor = new Color(0.55f, 0.45f, 0.45f, 0.7f);
                rim.effectDistance = new Vector2(2f, -2f);
                nodes[i] = node;
                Sprite car = carSprites[i];
                float cw = 66f;
                float ch = car != null ? cw * car.rect.height / car.rect.width : 33f;
                nodeCars[i] = UiKit.Icon(node.transform, "Car", car, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f),
                    new Vector2(cw, Mathf.Min(ch, 60f)));
                nodeChecks[i] = UiKit.Icon(node.transform, "Check", Check, new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(70f, 76f));
                nodeChecks[i].enabled = false;
            }
            Image crown = UiKit.Icon(nodes[nodes.Length - 1].transform, "Crown", Crown, new Vector2(0.5f, 1f),
                new Vector2(0f, 8f), new Vector2(64f, 42f));
            crown.rectTransform.pivot = new Vector2(0.5f, 0f);
        }

        static float NodeX(int i) => 40f + i * (TrackW - 80f) / (PackBoard.CarCount - 1);

        /// <summary>done 辆已送走，当前这辆坐了 seats / total。</summary>
        public void SetProgress(int done, int seatsFilled, int seatsTotal)
        {
            float k = done >= PackBoard.CarCount ? 1f
                : Mathf.Clamp01((done + (seatsTotal > 0 ? seatsFilled / (float)seatsTotal : 0f)) / (PackBoard.CarCount - 1));
            float x = Mathf.Lerp(0f, NodeX(PackBoard.CarCount - 1) - 4f, k);
            if (trackFill != null) trackFill.rectTransform.sizeDelta = new Vector2(x, 14f);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] == null) continue;
                bool past = i < done;
                bool current = i == done;
                nodes[i].color = past ? new Color(0.72f, 0.93f, 0.68f, 1f) : current ? Color.white : NodeIdle;
                nodes[i].rectTransform.localScale = Vector3.one * (current ? 1.12f : 1f);
                if (nodeChecks[i] != null) nodeChecks[i].enabled = past;
                if (nodeCars[i] != null)
                    nodeCars[i].color = past ? new Color(1f, 1f, 1f, 0.35f) : current ? Color.white : new Color(1f, 1f, 1f, 0.7f);
            }
        }

        public RectTransform Node(int i) => i >= 0 && i < nodes.Length && nodes[i] != null ? nodes[i].rectTransform : null;

        // ------------------------------------------------------------ 按钮和提示

        void BuildButtons()
        {
            boardButton = ActionButton("上车", db != null ? db.packBtn : null, -250f, Color.white);
            refillButton = ActionButton("补充", db != null ? db.packRefill : null, 250f, Cocoa);
        }

        Button ActionButton(string caption, Sprite sprite, float x, Color ink)
        {
            Image face = UiKit.Icon(transform, "Btn_" + caption, sprite, new Vector2(0.5f, 0f),
                new Vector2(x, ButtonY), new Vector2(330f, 150f));
            face.preserveAspect = true;
            if (face.sprite == null) face.sprite = UiKit.Rounded;
            Text label = UiKit.Label(face.transform, "Text", caption, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 4f), new Vector2(320f, 72f), 48, ink);
            if (ink == Color.white)
            {
                var shadow = label.gameObject.AddComponent<Shadow>();
                shadow.effectColor = Shade;
                shadow.effectDistance = new Vector2(0f, -3f);
            }
            face.raycastTarget = true;
            var button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            PressScale.Attach(button, 0.92f);
            return button;
        }

        void BuildToast()
        {
            toastPlate = UiKit.Icon(transform, "Toast", UiKit.Rounded, new Vector2(0.5f, 1f), Vector2.zero,
                new Vector2(640f, 64f));
            toastPlate.type = Image.Type.Sliced;
            toastPlate.preserveAspect = false;
            toastPlate.color = new Color(0.29f, 0.15f, 0.17f, 0.82f);
            toastLabel = UiKit.Label(toastPlate.transform, "Text", "", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(620f, 60f), 30, Color.white);
            toastPlate.gameObject.SetActive(false);
        }

        void BuildTop(RectTransform safe)
        {
            topBar = UiKit.Stretch(safe != null ? safe : (RectTransform)transform, "PackTop");
            Image back = UiKit.Icon(topBar, "Back", db != null ? db.dressupBack : null,
                new Vector2(0f, 1f), new Vector2(84f, -60f), new Vector2(96f, 104f));
            back.raycastTarget = true;
            backButton = back.gameObject.AddComponent<Button>();
            backButton.targetGraphic = back;
            backButton.transition = Selectable.Transition.None;
            PressScale.Attach(backButton, 0.9f);

            restartButton = UiKit.Button(topBar, "重开", new Vector2(1f, 1f), new Vector2(-96f, -150f),
                new Vector2(140f, 68f), Chip.White, 30, () => { });
            restartButton.onClick.RemoveAllListeners();
            PressScale.Attach(restartButton, 0.9f);

            playsLabel = UiKit.Label(topBar, "Plays", "", new Vector2(0.5f, 1f), new Vector2(0f, -60f),
                new Vector2(320f, 56f), 32, Cocoa);
            var playsRim = playsLabel.gameObject.AddComponent<Outline>();
            playsRim.effectColor = new Color(1f, 0.96f, 0.93f, 0.95f);
            playsRim.effectDistance = new Vector2(2f, -2f);
        }

        public void SetPlays(int used, int total)
        {
            if (playsLabel == null) return;
            playsLabel.text = "今日 " + Mathf.Max(0, used) + "/" + total;
        }

        /// <summary>按当前屏幕高度和安全区摆位置。每次进页面调一次。</summary>
        public void Fit()
        {
            FitMachine();
            PlaceLanes();
            float pad = ScreenFit.BottomPad;
            if (boardButton != null) ((RectTransform)boardButton.transform).anchoredPosition = new Vector2(-250f, ButtonY + pad);
            if (refillButton != null) ((RectTransform)refillButton.transform).anchoredPosition = new Vector2(250f, ButtonY + pad);
            if (track != null) track.anchoredPosition = new Vector2(0f, TrackY + pad);
            if (carRoot != null) carRoot.anchoredPosition = new Vector2(carRoot.anchoredPosition.x, CarFloor + pad);
            if (toastPlate != null)
                toastPlate.rectTransform.anchoredPosition = new Vector2(0f, -(LaneCenterFromTop() - LaneH * 0.5f) + 4f);
            if (popupLayer != null) popupLayer.SetAsLastSibling();
        }

        public void SetActions(bool canBoard, bool canRefill)
        {
            boardHot = canBoard;
            Tint(boardButton, canBoard);
            Tint(refillButton, canRefill);
        }

        static void Tint(Button button, bool on)
        {
            if (button == null) return;
            var image = button.targetGraphic as Image;
            if (image != null) image.color = on ? Color.white : new Color(0.66f, 0.64f, 0.66f, 1f);
        }

        void LateUpdate()
        {
            if (boardButton != null)
            {
                var press = boardButton.GetComponent<PressScale>();
                if (press != null)
                    press.Rest = boardHot ? 1.04f + Mathf.Sin(Time.unscaledTime * 6f) * 0.05f : 1f;
            }
            if (gate != null && queue != null)
            {
                for (int i = 0; i < queue.Length; i++)
                {
                    if (queue[i] == null || queueBusy) continue;
                    Vector2 slot = QueueSlot(i);
                    slot.x = Mathf.Sin(Time.unscaledTime * 2.2f + i * 1.3f) * 3f;
                    queue[i].rectTransform.anchoredPosition = slot;
                }
            }
        }

        bool queueBusy;

        /// <summary>门里排队的问号往下挪一格，最下面那件掉出门口。</summary>
        public IEnumerator FeedQueue(float duration)
        {
            if (queue == null || queue.Length == 0) yield break;
            queueBusy = true;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Ease.OutCubic(t / duration);
                for (int i = 0; i < queue.Length; i++)
                {
                    if (queue[i] == null) continue;
                    Vector2 from = QueueSlot(i);
                    Vector2 to = QueueSlot(i - 1);
                    queue[i].rectTransform.anchoredPosition = Vector2.Lerp(from, to, k);
                    if (i == 0)
                    {
                        Color c = queue[i].color;
                        c.a = 1f - k;
                        queue[i].color = c;
                    }
                }
                yield return null;
            }
            for (int i = 0; i < queue.Length; i++)
            {
                if (queue[i] == null) continue;
                queue[i].rectTransform.anchoredPosition = QueueSlot(i);
                queue[i].color = Color.white;
            }
            queueBusy = false;
        }

        public void ShowToast(string message, float hold = 2.2f)
        {
            if (toastPlate == null) return;
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = null;
            bool show = !string.IsNullOrEmpty(message);
            toastPlate.gameObject.SetActive(show);
            if (!show || !isActiveAndEnabled) return;
            toastLabel.text = message;
            float w = Mathf.Clamp(toastLabel.preferredWidth + 64f, 260f, 980f);
            toastPlate.rectTransform.sizeDelta = new Vector2(w, 64f);
            toastLabel.rectTransform.sizeDelta = new Vector2(w - 24f, 60f);
            toastRoutine = StartCoroutine(ToastRoutine(hold));
        }

        IEnumerator ToastRoutine(float hold)
        {
            RectTransform rect = toastPlate.rectTransform;
            float t = 0f;
            toastPlate.color = new Color(0.29f, 0.15f, 0.17f, 0.82f);
            toastLabel.color = Color.white;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                rect.localScale = Vector3.one * Mathf.Max(0.01f, Ease.OutBack(t / 0.2f, 2.4f));
                yield return null;
            }
            rect.localScale = Vector3.one;
            yield return new WaitForSeconds(hold);
            t = 0f;
            while (t < 0.25f)
            {
                t += Time.deltaTime;
                float a = 1f - t / 0.25f;
                toastPlate.color = new Color(0.29f, 0.15f, 0.17f, 0.82f * a);
                toastLabel.color = new Color(1f, 1f, 1f, a);
                yield return null;
            }
            toastPlate.gameObject.SetActive(false);
            toastRoutine = null;
        }

        public void Wire(UnityAction onBack, UnityAction onRestart, UnityAction onBoard, UnityAction onRefill,
            UnityAction<int> onLane)
        {
            Sfx.BindClick(backButton, onBack);
            Sfx.BindClick(restartButton, onRestart);
            Sfx.BindSilent(boardButton, onBoard);
            Sfx.BindSilent(refillButton, onRefill);
            for (int i = 0; i < lanes.Length; i++)
            {
                int index = i;
                Button button = lanes[i] != null ? lanes[i].GetComponent<Button>() : null;
                Sfx.BindSilent(button, () => onLane?.Invoke(index));
            }
        }

        // ------------------------------------------------------------ 弹窗

        public struct PopupItem
        {
            public Sprite Icon;
            public int Count;
        }

        /// <summary>
        /// 暗底 + 转动的光 + 标题条 + 奖励一排 + 两个按钮。primary 是金色按钮，ad 带视频角标。
        /// 任一按钮按下都先关掉弹窗再回调。
        /// </summary>
        public IEnumerator Popup(string title, string sub, List<PopupItem> items, string primary, UnityAction onPrimary,
            string ad, UnityAction onAd, bool rays)
        {
            RectTransform layer = UiKit.Stretch(popupLayer, "Popup");
            layer.SetAsLastSibling();
            Image dim = layer.gameObject.AddComponent<Image>();
            dim.color = new Color(0.12f, 0.06f, 0.08f, 0f);
            dim.raycastTarget = true;

            RectTransform card = UiKit.Rect(layer, "Card", new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(860f, 760f));
            Image glow = null;
            if (rays && db != null && db.rewardRays != null)
            {
                glow = UiKit.Icon(card, "Rays", db.rewardRays, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f),
                    new Vector2(1100f, 1100f));
                glow.color = new Color(1f, 0.93f, 0.6f, 0.75f);
            }
            Image plate = UiKit.Icon(card, "Plate", UiKit.Rounded, new Vector2(0.5f, 0.5f), new Vector2(0f, -30f),
                new Vector2(780f, 520f));
            plate.type = Image.Type.Sliced;
            plate.preserveAspect = false;
            plate.color = new Color(1f, 0.97f, 0.93f, 1f);
            var plateRim = plate.gameObject.AddComponent<Outline>();
            plateRim.effectColor = Cocoa;
            plateRim.effectDistance = new Vector2(4f, -4f);

            Image ribbon = UiKit.Icon(card, "Ribbon", db != null ? db.rewardRibbon : null, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 250f), new Vector2(700f, 170f));
            if (ribbon.sprite == null)
            {
                ribbon.sprite = UiKit.Rounded;
                ribbon.type = Image.Type.Sliced;
                ribbon.color = new Color(0.93f, 0.33f, 0.4f, 1f);
                ribbon.rectTransform.sizeDelta = new Vector2(600f, 110f);
            }
            Text head = UiKit.Label(ribbon.transform, "Title", title, new Vector2(0.5f, 0.5f), new Vector2(0f, 10f),
                new Vector2(680f, 90f), 56, Color.white);
            var headShadow = head.gameObject.AddComponent<Outline>();
            headShadow.effectColor = new Color(0.45f, 0.08f, 0.16f, 0.9f);
            headShadow.effectDistance = new Vector2(3f, -3f);

            Text subLabel = UiKit.Label(card, "Sub", sub ?? "", new Vector2(0.5f, 0.5f), new Vector2(0f, 120f),
                new Vector2(720f, 60f), 34, Cocoa);

            var pops = new List<RectTransform>();
            int n = items != null ? items.Count : 0;
            float pitch = n > 3 ? 170f : 200f;
            for (int i = 0; i < n; i++)
            {
                float x = (i - (n - 1) * 0.5f) * pitch;
                Image disc = UiKit.Icon(card, "Item_" + i, UiKit.Circle, new Vector2(0.5f, 0.5f), new Vector2(x, -10f),
                    new Vector2(150f, 150f));
                disc.color = new Color(1f, 0.9f, 0.8f, 1f);
                UiKit.Icon(disc.transform, "Icon", items[i].Icon, new Vector2(0.5f, 0.5f), new Vector2(0f, 6f),
                    new Vector2(118f, 118f));
                Text count = UiKit.Label(disc.transform, "Count", "×" + items[i].Count, new Vector2(0.5f, 0f),
                    new Vector2(0f, -10f), new Vector2(150f, 50f), 40, Cocoa);
                var countRim = count.gameObject.AddComponent<Outline>();
                countRim.effectColor = Color.white;
                countRim.effectDistance = new Vector2(2f, -2f);
                disc.rectTransform.localScale = Vector3.zero;
                pops.Add(disc.rectTransform);
            }

            bool closed = false;
            UnityAction close = () =>
            {
                closed = true;
            };
            float by = -210f;
            Button main = null, side = null;
            if (!string.IsNullOrEmpty(ad))
            {
                side = PopupButton(card, ad, new Vector2(-180f, by), db != null ? db.packBtn : null, Color.white, true);
                main = PopupButton(card, primary, new Vector2(180f, by), db != null ? db.rewardBtn : null, Cocoa, false);
                Sfx.BindClick(side, () =>
                {
                    close();
                    onAd?.Invoke();
                });
            }
            else
            {
                main = PopupButton(card, primary, new Vector2(0f, by), db != null ? db.rewardBtn : null, Cocoa, false);
            }
            Sfx.BindClick(main, () =>
            {
                close();
                onPrimary?.Invoke();
            });

            card.localScale = Vector3.zero;
            float t = 0f;
            while (t < 0.32f)
            {
                t += Time.deltaTime;
                float k = t / 0.32f;
                dim.color = new Color(0.12f, 0.06f, 0.08f, 0.62f * Mathf.Clamp01(k * 1.6f));
                card.localScale = Vector3.one * Mathf.Max(0.01f, Ease.OutBack(k, 1.9f));
                yield return null;
            }
            card.localScale = Vector3.one;
            for (int i = 0; i < pops.Count; i++)
                StartCoroutine(PopIn(pops[i], i * 0.09f));

            while (!closed)
            {
                if (glow != null) glow.rectTransform.Rotate(0f, 0f, -24f * Time.deltaTime);
                if (main != null)
                {
                    float s = 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.035f;
                    main.transform.localScale = Vector3.one * s;
                }
                yield return null;
            }

            t = 0f;
            while (t < 0.16f)
            {
                t += Time.deltaTime;
                float k = 1f - t / 0.16f;
                card.localScale = Vector3.one * Mathf.Max(0.01f, k);
                dim.color = new Color(0.12f, 0.06f, 0.08f, 0.62f * k);
                yield return null;
            }
            UiKit.Discard(layer);
        }

        Button PopupButton(RectTransform parent, string caption, Vector2 at, Sprite sprite, Color ink, bool adBadge)
        {
            Image face = UiKit.Icon(parent, "Btn_" + caption, sprite, new Vector2(0.5f, 0.5f), at, new Vector2(320f, 130f));
            face.preserveAspect = true;
            if (face.sprite == null) face.sprite = UiKit.Rounded;
            face.raycastTarget = true;
            Text label = UiKit.Label(face.transform, "Text", caption, new Vector2(0.5f, 0.5f),
                new Vector2(adBadge ? 22f : 0f, 4f), new Vector2(300f, 64f), 40, ink);
            if (ink == Color.white)
            {
                var shadow = label.gameObject.AddComponent<Shadow>();
                shadow.effectColor = Shade;
                shadow.effectDistance = new Vector2(0f, -3f);
            }
            if (adBadge && db != null && db.packAd != null)
            {
                UiKit.Icon(face.transform, "Ad", db.packAd, new Vector2(0.5f, 0.5f), new Vector2(-label.preferredWidth * 0.5f - 18f, 4f),
                    new Vector2(48f, 52f));
            }
            var button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.None;
            return button;
        }

        static IEnumerator PopIn(RectTransform rect, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            Sfx.Play(SfxId.Hop);
            float t = 0f;
            while (t < 0.3f && rect != null)
            {
                t += Time.deltaTime;
                rect.localScale = Vector3.one * Mathf.Max(0.01f, Ease.OutBack(t / 0.3f, 2.6f));
                yield return null;
            }
            if (rect != null) rect.localScale = Vector3.one;
        }
    }
}
