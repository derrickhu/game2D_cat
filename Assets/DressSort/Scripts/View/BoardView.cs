using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 棋盘的渲染与动画。只负责把 SortBoard 的状态画出来，规则判断都在 SortBoard 里。
    ///
    /// 叠放约定：同一列里越靠下的越靠前，所以最下面那件（下一个会被顶出来的）
    /// 压在它上面那件的裙摆之上，视觉上就能看出"下一个出来的是它"。
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        public struct Layout
        {
            public float itemSize;
            public float overlap;
        }

        enum EaseKind
        {
            OutCubic,
            OutBack,
            OutQuad,
        }

        struct Motion
        {
            public RectTransform rect;
            public Vector2 from;
            public Vector2 to;
            public Vector2 sizeFrom;
            public Vector2 sizeTo;
            public float delay;
            public float duration;
            public float arc;
            public bool scale;
            public EaseKind ease;
            /// <summary>飞行途中转的总角度，360 就是翻一整圈。</summary>
            public float spin;
            /// <summary>飞到一半时顺着飞行方向拉长一点。</summary>
            public bool stretch;
            /// <summary>飞的时候身后留几道残影。</summary>
            public bool trail;
            /// <summary>走到这个进度算「落地」，触发 onLand。默认 1。</summary>
            public float landAt;
            public System.Action onLand;
        }

        Layout layout;
        GameHud hud;
        SortBoard board;
        IReadOnlyList<ItemDef> palette;
        ItemDef mystery;
        Sprite solvedMark;

        RectTransform root;
        RectTransform holdRoot;
        readonly List<RectTransform> laneRoots = new List<RectTransform>();
        readonly List<List<Image>> laneItems = new List<List<Image>>();
        readonly List<Image> laneWashes = new List<Image>();
        readonly List<Image> laneChecks = new List<Image>();
        readonly List<List<Image>> laneSparkles = new List<List<Image>>();
        float sparkleClock;
        Image heldItem;

        readonly List<Image> laneBacks = new List<Image>();
        readonly List<LaneTap> laneTaps = new List<LaneTap>();
        readonly List<Image> coverViews = new List<Image>();
        readonly List<Text> coverCounts = new List<Text>();
        readonly List<Image> lockViews = new List<Image>();
        readonly List<Image> padlocks = new List<Image>();
        readonly List<Image> bubbles = new List<Image>();

        static readonly Color ParcelFallback = new Color32(0xC9, 0x9A, 0x6B, 0xFF);
        static readonly Color CoverFallback = new Color32(0xC9, 0xB6, 0xE4, 0xFF);
        static readonly Color CoverInk = new Color32(0x4A, 0x26, 0x2C, 0xFF);
        static readonly Color LockWash = new Color(1f, 0.82f, 0.28f, 0.42f);
        static readonly Color KeyFallback = new Color32(0xF2, 0xC2, 0x3A, 0xFF);
        static readonly Color AlarmFallback = new Color32(0xF0, 0x5A, 0x5E, 0xFF);
        static readonly Color AlarmUrgent = new Color32(0xE8, 0x2E, 0x3A, 0xFF);

        static readonly Color SolvedWash = new Color(1f, 1f, 1f, 0.34f);
        static readonly Color LaneIdle = new Color(1f, 1f, 1f, 0.34f);
        static readonly Color LanePick = new Color(1f, 0.82f, 0.86f, 0.78f);
        static readonly Color LanePress = new Color(1f, 1f, 1f, 0.72f);
        static readonly Color MintRing = new Color32(0x7F, 0xE0, 0xB8, 0xFF);
        static readonly Color GoldRing = new Color32(0xFF, 0xD3, 0x4E, 0xFF);

        static readonly string[] ComboWords = { "好看！", "精彩！", "太好了！", "绝了！" };
        static readonly Color[] ComboColors =
        {
            new Color32(0xFF, 0x7A, 0x9C, 0xFF),
            new Color32(0xFF, 0xA2, 0x3A, 0xFF),
            new Color32(0xB0, 0x6C, 0xFF, 0xFF),
            new Color32(0xFF, 0x4D, 0x5E, 0xFF),
        };

        BoardFx fx;
        int moveCount;
        int lastClearMove = -99;
        int combo;
        readonly List<float> pressValue = new List<float>();
        int pressedColumn = -1;
        float idleClock;
        Coroutine denyRoutine;
        readonly List<RectTransform> denyRects = new List<RectTransform>();
        readonly List<Vector2> denyHomes = new List<Vector2>();
        readonly Dictionary<RectTransform, Coroutine> squishes = new Dictionary<RectTransform, Coroutine>();

        public bool Busy { get; private set; }

        public void AttachFx(BoardFx next) => fx = next;
        public System.Action<int> OnColumnClicked;

        /// <summary>「交换」选格模式：点列时回调具体是哪一格。</summary>
        public System.Action<int, int> OnItemClicked;
        bool pickMode;

        /// <summary>记下按下的位置，Button 抬起时据此换算点中的是哪一格。</summary>
        class LaneTap : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler,
            UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IPointerExitHandler
        {
            public Vector2 screen;
            public Camera eventCamera;
            public BoardView owner;
            public int column;

            public void OnPointerDown(UnityEngine.EventSystems.PointerEventData data)
            {
                screen = data.position;
                eventCamera = data.pressEventCamera;
                if (owner != null) owner.pressedColumn = column;
            }

            public void OnPointerUp(UnityEngine.EventSystems.PointerEventData data) => Release();
            public void OnPointerExit(UnityEngine.EventSystems.PointerEventData data) => Release();

            void Release()
            {
                if (owner != null && owner.pressedColumn == column) owner.pressedColumn = -1;
            }
        }

        public bool PickMode
        {
            get => pickMode;
            set
            {
                pickMode = value;
                for (int i = 0; i < laneBacks.Count; i++)
                {
                    if (laneBacks[i] != null)
                        laneBacks[i].color = value ? LanePick : LaneIdle;
                }
            }
        }

        public static BoardView Create(Transform parent, Layout layout)
        {
            var existing = parent.GetComponent<BoardView>();
            if (existing != null)
            {
                existing.layout = layout;
                existing.root = parent as RectTransform;
                return existing;
            }

            var view = parent.gameObject.AddComponent<BoardView>();
            view.root = parent as RectTransform;
            view.layout = layout;
            return view;
        }

        public void AttachHud(GameHud next)
        {
            hud = next;
            if (hud != null && hud.boardRoot != null)
                root = hud.boardRoot;
        }

        public void Bind(SortBoard board, IReadOnlyList<ItemDef> palette, ItemDef mystery,
            Sprite solvedMark = null)
        {
            this.board = board;
            this.palette = palette;
            this.mystery = mystery;
            this.solvedMark = solvedMark;
            if (board != null)
            {
                float pitch = 920f / Mathf.Max(1, board.ColumnCount);
                // 对齐动物收纳那类竖列：棋子明显小于列距，列间留出缝。列多时宁可挨紧也别缩太小。
                float size = Mathf.Max(pitch * 0.74f, Mathf.Min(120f, pitch * 0.95f));
                float tallest = MaxStackHeight / (layout.overlap * (board.ColumnHeight - 1) + 1f);
                layout.itemSize = Mathf.Min(152f, size, tallest);
            }
            moveCount = 0;
            lastClearMove = -99;
            combo = 0;
            BuildLanes();
            Refresh();
        }

        /// <summary>挂杆下面到手里那件上沿之间，留给一整列的高度。</summary>
        const float MaxStackHeight = 780f;

        float LaneHeight => layout.itemSize * layout.overlap * (board.ColumnHeight - 1)
            + layout.itemSize;

        void BuildLanes()
        {
            for (int c = 0; c < laneItems.Count; c++)
            {
                List<Image> views = laneItems[c];
                for (int i = views.Count - 1; i >= 0; i--)
                    UiKit.Discard(views[i]);
            }
            UiKit.Discard(heldItem);
            heldItem = null;
            for (int i = laneWashes.Count - 1; i >= 0; i--)
                UiKit.Discard(laneWashes[i]);
            for (int i = laneChecks.Count - 1; i >= 0; i--)
                UiKit.Discard(laneChecks[i]);
            for (int i = laneBacks.Count - 1; i >= 0; i--)
                UiKit.Discard(laneBacks[i]);
            for (int i = laneRoots.Count - 1; i >= 0; i--)
                UiKit.Discard(laneRoots[i]);
            for (int i = coverViews.Count - 1; i >= 0; i--)
                UiKit.Discard(coverViews[i]);
            for (int i = lockViews.Count - 1; i >= 0; i--)
                UiKit.Discard(lockViews[i]);
            for (int i = padlocks.Count - 1; i >= 0; i--)
                UiKit.Discard(padlocks[i]);
            for (int i = bubbles.Count - 1; i >= 0; i--)
                UiKit.Discard(bubbles[i]);
            lockViews.Clear();
            padlocks.Clear();
            bubbles.Clear();
            laneRoots.Clear();
            laneItems.Clear();
            laneWashes.Clear();
            laneChecks.Clear();
            laneSparkles.Clear();
            laneBacks.Clear();
            laneTaps.Clear();
            pressValue.Clear();
            pressedColumn = -1;
            coverViews.Clear();
            coverCounts.Clear();

            for (int c = 0; c < board.ColumnCount; c++)
            {
                Image hanger = hud != null ? hud.Hanger(c) : null;
                float x = hanger != null ? hanger.rectTransform.anchoredPosition.x : LaneXFallback(c);
                float hangerTop = hanger != null ? hanger.rectTransform.anchoredPosition.y : -GameHud.RodFromTop;
                float hangerH = hanger != null ? hanger.rectTransform.sizeDelta.y : GameHud.HangerHeight;
                float washTop = hangerTop - GameHud.RodClearance;
                float washBottom = hangerTop - hangerH - LaneHeight - 12f;
                float washH = Mathf.Max(80f, washTop - washBottom);
                float laneHeight = hangerH + LaneHeight;

                Image back = UiKit.Slice(root, "LaneBack_" + c, UiKit.Rounded,
                    new Vector2(0.5f, 1f), new Vector2(x, (washTop + washBottom) * 0.5f),
                    new Vector2(layout.itemSize + 18f, washH + 6f), pickMode ? LanePick : LaneIdle);
                back.pixelsPerUnitMultiplier = 0.4f;
                back.raycastTarget = false;
                back.transform.SetSiblingIndex(0);
                laneBacks.Add(back);

                Sprite washSprite = hud != null ? hud.laneSolved : null;
                if (washSprite != null)
                {
                    Image wash = UiKit.Slice(root, "Wash_" + c, washSprite, new Vector2(0.5f, 1f),
                        new Vector2(x, (washTop + washBottom) * 0.5f),
                        new Vector2(layout.itemSize + 12f, washH),
                        new Color(1f, 1f, 1f, 0f));
                    wash.raycastTarget = false;
                    wash.transform.SetSiblingIndex(back.transform.GetSiblingIndex() + 1);
                    laneWashes.Add(wash);
                }
                else
                    laneWashes.Add(null);

                Sprite checkSprite = hud != null && hud.checkSolved != null
                    ? hud.checkSolved
                    : solvedMark;
                if (checkSprite != null)
                {
                    Image check = UiKit.Icon(root, "Check_" + c, checkSprite, new Vector2(0.5f, 1f),
                        new Vector2(x, washTop + 8f), new Vector2(96f, 96f));
                    check.preserveAspect = true;
                    check.raycastTarget = false;
                    check.transform.localScale = Vector3.zero;
                    laneChecks.Add(check);
                }
                else
                    laneChecks.Add(null);
                laneSparkles.Add(new List<Image>());

                int index = c;
                Button hit = UiKit.HitArea(root, "Lane_" + c, new Vector2(0.5f, 1f),
                    new Vector2(x, hangerTop - laneHeight * 0.5f),
                    new Vector2(Mathf.Max(GameHud.HangerWidth, layout.itemSize) + 12f, laneHeight),
                    () => OnLaneClicked(index));
                LaneTap tap = hit.gameObject.AddComponent<LaneTap>();
                tap.owner = this;
                tap.column = c;
                laneTaps.Add(tap);
                pressValue.Add(0f);
                laneRoots.Add(hit.transform as RectTransform);
                laneItems.Add(new List<Image>());
                BuildCover(c, x, washTop, washBottom);
                BuildLock(c, x, washTop, washBottom);
                BuildBubble(c, x, hangerTop, hangerH);
            }

            holdRoot = hud != null && hud.holdSlot != null ? hud.holdSlot : holdRoot;
            if (holdRoot == null)
                holdRoot = UiKit.Rect(root, "Hold", new Vector2(0.5f, 0f),
                    new Vector2(0f, 400f), new Vector2(200f, 200f));
        }

        /// <summary>防尘罩盖住整列，吊牌上写还差几列。不点它，点击照样落到下面的列热区。</summary>
        void BuildCover(int column, float x, float top, float bottom)
        {
            if (board.CoverNeed(column) <= 0)
            {
                coverViews.Add(null);
                coverCounts.Add(null);
                return;
            }

            Sprite sprite = hud != null ? hud.coverSprite : null;
            float w = layout.itemSize + 24f;
            float h = top - bottom + 20f;
            Image cover = UiKit.Slice(root, "Cover_" + column, sprite != null ? sprite : UiKit.Rounded,
                new Vector2(0.5f, 1f), new Vector2(x, (top + bottom) * 0.5f), new Vector2(w, h),
                sprite != null ? Color.white : CoverFallback);
            cover.raycastTarget = false;

            Sprite tagSprite = hud != null ? hud.tagSprite : null;
            float tagSize = Mathf.Min(112f, w * 0.86f);
            Image tag = UiKit.Icon(cover.transform, "Tag", tagSprite, new Vector2(0.5f, 0.5f),
                new Vector2(0f, h * 0.12f), new Vector2(tagSize, tagSize));
            if (tagSprite == null)
            {
                tag.sprite = UiKit.Circle;
                tag.color = new Color32(0xFF, 0xF4, 0xDC, 0xFF);
            }
            // 吊牌图上方是绳子，圆牌的圆心在图中心偏下。
            Text count = UiKit.Label(tag.transform, "Count", "", new Vector2(0.5f, 0.5f),
                new Vector2(0f, tagSprite != null ? -tagSize * 0.13f : 0f), new Vector2(tagSize, tagSize),
                Mathf.RoundToInt(tagSize * 0.42f), CoverInk);
            coverViews.Add(cover);
            coverCounts.Add(count);
        }

        /// <summary>锁住的列底色换成蜂蜜色，衣架上挂一把锁。</summary>
        void BuildLock(int column, float x, float top, float bottom)
        {
            if (!board.HasLock(column))
            {
                lockViews.Add(null);
                padlocks.Add(null);
                return;
            }

            float w = layout.itemSize + 18f;
            Image wash = UiKit.Slice(root, "Lock_" + column, UiKit.Rounded, new Vector2(0.5f, 1f),
                new Vector2(x, (top + bottom) * 0.5f), new Vector2(w, top - bottom + 6f), LockWash);
            wash.pixelsPerUnitMultiplier = 0.4f;
            wash.raycastTarget = false;
            wash.transform.SetSiblingIndex(laneBacks[column].transform.GetSiblingIndex() + 1);

            Sprite sprite = hud != null ? hud.lockSprite : null;
            float size = Mathf.Min(104f, w * 0.86f);
            Image padlock = UiKit.Icon(root, "Padlock_" + column, sprite, new Vector2(0.5f, 1f),
                new Vector2(x, top - size * 0.42f), new Vector2(size, size));
            padlock.preserveAspect = true;
            padlock.raycastTarget = false;
            if (sprite == null)
            {
                padlock.sprite = UiKit.Circle;
                padlock.color = KeyFallback;
            }
            lockViews.Add(wash);
            padlocks.Add(padlock);
        }

        /// <summary>专属列的衣架上挂一个气泡，里面是这列认的那款。</summary>
        void BuildBubble(int column, float x, float hangerTop, float hangerH)
        {
            int type = board.TargetOf(column);
            if (type < 0)
            {
                bubbles.Add(null);
                return;
            }

            float pitch = 920f / Mathf.Max(1, board.ColumnCount);
            float size = Mathf.Min(116f, pitch * 0.86f);
            Sprite sprite = hud != null ? hud.bubbleSprite : null;
            Image bubble = UiKit.Icon(root, "Bubble_" + column, sprite != null ? sprite : UiKit.Circle,
                new Vector2(0.5f, 1f), new Vector2(x, hangerTop - hangerH * 0.5f + 4f),
                new Vector2(size, size));
            bubble.preserveAspect = true;
            bubble.raycastTarget = false;
            Image icon = UiKit.Icon(bubble.transform, "Icon", SpriteFor(type), new Vector2(0.5f, 0.5f),
                new Vector2(0f, size * 0.085f), new Vector2(size * 0.62f, size * 0.62f));
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            bubbles.Add(bubble);
        }

        void SnapLocks()
        {
            for (int c = 0; c < bubbles.Count; c++)
            {
                if (bubbles[c] != null)
                    bubbles[c].rectTransform.SetAsLastSibling();
            }
            for (int c = 0; c < lockViews.Count; c++)
            {
                bool on = board.IsLocked(c);
                if (lockViews[c] != null)
                {
                    lockViews[c].gameObject.SetActive(on);
                    if (on)
                        lockViews[c].color = LockWash;
                }
                if (padlocks[c] != null)
                {
                    padlocks[c].gameObject.SetActive(on);
                    if (on)
                    {
                        padlocks[c].rectTransform.localScale = Vector3.one;
                        Color col = padlocks[c].color;
                        col.a = 1f;
                        padlocks[c].color = col;
                        padlocks[c].rectTransform.SetAsLastSibling();
                    }
                }
            }
        }

        IEnumerator OpenLocks()
        {
            int[] opened = board.LastUnlocked;
            if (opened == null || opened.Length == 0) yield break;
            Sfx.Play(SfxId.Unlock);
            var images = new List<Image>();
            for (int i = 0; i < opened.Length; i++)
            {
                int c = opened[i];
                if (c < 0 || c >= padlocks.Count) continue;
                if (padlocks[c] != null) images.Add(padlocks[c]);
                if (lockViews[c] != null) images.Add(lockViews[c]);
            }
            var alphas = new List<float>();
            for (int i = 0; i < images.Count; i++)
                alphas.Add(images[i].color.a);

            float t = 0f;
            const float duration = 0.4f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                for (int i = 0; i < images.Count; i++)
                {
                    if (images[i].name.StartsWith("Padlock"))
                        images[i].rectTransform.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.35f);
                    Color col = images[i].color;
                    col.a = alphas[i] * (1f - k * k);
                    images[i].color = col;
                }
                yield return null;
            }
            for (int i = 0; i < images.Count; i++)
                images[i].gameObject.SetActive(false);
        }

        /// <summary>
        /// 钥匙挂在裙子左边，闹钟挂在右边并写着还剩几步，跟着裙子一起移动。
        /// 裙子下半截会被下一件压住，所以都挂在上半截。
        /// </summary>
        void SnapBadges()
        {
            float size = layout.itemSize;
            for (int c = 0; c < laneItems.Count; c++)
            {
                List<Image> views = laneItems[c];
                for (int r = 0; r < views.Count; r++)
                {
                    Image view = views[r];
                    if (view == null) continue;
                    SetKey(view, board.HasKey(c, r), size);
                    SetAlarm(view, board.AlarmLeft(c, r), size);
                }
            }
            if (heldItem != null)
            {
                SetKey(heldItem, false, size);
                SetAlarm(heldItem, -1, size);
            }
        }

        void SetKey(Image view, bool on, float size)
        {
            Transform found = view.transform.Find("Key");
            if (!on)
            {
                if (found != null) found.gameObject.SetActive(false);
                return;
            }
            Image key = found != null ? found.GetComponent<Image>() : null;
            if (key == null)
            {
                Sprite sprite = hud != null ? hud.keySprite : null;
                key = UiKit.Icon(view.transform, "Key", sprite != null ? sprite : UiKit.Circle,
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
                key.preserveAspect = true;
                key.raycastTarget = false;
                if (sprite == null) key.color = KeyFallback;
            }
            key.gameObject.SetActive(true);
            key.rectTransform.anchoredPosition = new Vector2(-size * 0.26f, size * 0.12f);
            key.rectTransform.sizeDelta = new Vector2(size * 0.5f, size * 0.5f);
        }

        void SetAlarm(Image view, int left, float size)
        {
            Transform found = view.transform.Find("Alarm");
            if (left < 0)
            {
                if (found != null) found.gameObject.SetActive(false);
                return;
            }
            Image alarm = found != null ? found.GetComponent<Image>() : null;
            if (alarm == null)
            {
                Sprite sprite = hud != null ? hud.alarmSprite : null;
                alarm = UiKit.Icon(view.transform, "Alarm", sprite != null ? sprite : UiKit.Circle,
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
                alarm.preserveAspect = true;
                alarm.raycastTarget = false;
                if (sprite == null) alarm.color = AlarmFallback;
                Text label = UiKit.Label(alarm.transform, "Count", "", new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.one, 30, Color.white);
                label.raycastTarget = false;
                var rim = label.gameObject.AddComponent<Outline>();
                rim.effectColor = new Color(1f, 0.98f, 0.93f, 0.9f);
                rim.effectDistance = new Vector2(1.5f, -1.5f);
            }
            alarm.gameObject.SetActive(true);
            float s = size * 0.62f;
            alarm.rectTransform.anchoredPosition = new Vector2(size * 0.22f, size * 0.1f);
            alarm.rectTransform.sizeDelta = new Vector2(s, s);
            // 闹钟图上面是两个铃，钟面圆心在图中心偏下。
            bool art = hud != null && hud.alarmSprite != null;
            Text count = alarm.transform.Find("Count").GetComponent<Text>();
            RectTransform cr = count.rectTransform;
            cr.anchoredPosition = art ? new Vector2(0f, -s * 0.047f) : Vector2.zero;
            cr.sizeDelta = new Vector2(s, s);
            count.fontSize = Mathf.RoundToInt(s * 0.38f);
            count.text = Mathf.Max(0, left).ToString();
            count.color = left <= 3 ? AlarmUrgent : CoverInk;
            if (left <= 0)
                StartCoroutine(PopReveal(alarm.rectTransform));
        }

        void SnapCovers()
        {
            for (int c = 0; c < coverViews.Count; c++)
            {
                Image cover = coverViews[c];
                if (cover == null) continue;
                bool on = board.IsCovered(c);
                if (cover.gameObject.activeSelf != on)
                    cover.gameObject.SetActive(on);
                if (!on) continue;
                RectTransform rect = cover.rectTransform;
                rect.localScale = Vector3.one;
                cover.color = hud != null && hud.coverSprite != null ? Color.white : CoverFallback;
                if (coverCounts[c] != null)
                    coverCounts[c].text = board.CoverLeft(c).ToString();
                rect.SetAsLastSibling();
            }
        }

        IEnumerator OpenCovers()
        {
            int[] opened = board.LastOpened;
            if (opened == null || opened.Length == 0) yield break;
            Sfx.Play(SfxId.Cover);
            var rects = new List<RectTransform>();
            var images = new List<Image>();
            var starts = new List<Vector2>();
            for (int i = 0; i < opened.Length; i++)
            {
                int c = opened[i];
                if (c < 0 || c >= coverViews.Count || coverViews[c] == null) continue;
                if (coverCounts[c] != null)
                    coverCounts[c].text = "0";
                rects.Add(coverViews[c].rectTransform);
                images.Add(coverViews[c]);
                starts.Add(coverViews[c].rectTransform.anchoredPosition);
            }

            float t = 0f;
            const float duration = 0.42f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float e = k * k;
                for (int i = 0; i < rects.Count; i++)
                {
                    rects[i].anchoredPosition = starts[i] + new Vector2(0f, e * 260f);
                    rects[i].localScale = new Vector3(1f, 1f - e * 0.6f, 1f);
                    Color col = images[i].color;
                    col.a = 1f - e;
                    images[i].color = col;
                }
                yield return null;
            }
            for (int i = 0; i < rects.Count; i++)
            {
                rects[i].anchoredPosition = starts[i];
                rects[i].localScale = Vector3.one;
                rects[i].gameObject.SetActive(false);
            }
        }

        /// <summary>包裹格子换成包裹图；刚拆开的弹一下。</summary>
        void SnapParcels(bool animate)
        {
            for (int c = 0; c < laneItems.Count; c++)
            {
                List<Image> views = laneItems[c];
                IReadOnlyList<int> types = board.Column(c);
                for (int r = 0; r < views.Count && r < types.Count; r++)
                {
                    Image view = views[r];
                    if (view == null) continue;
                    bool wrapped = board.IsWrapped(c, r);
                    bool wasWrapped = view.name == "Parcel";
                    if (wrapped)
                    {
                        Sprite parcel = hud != null ? hud.parcelSprite : null;
                        view.sprite = parcel != null ? parcel : UiKit.Rounded;
                        view.color = parcel != null ? Color.white : ParcelFallback;
                        view.name = "Parcel";
                        continue;
                    }
                    if (!wasWrapped) continue;
                    view.sprite = SpriteFor(types[r]);
                    view.color = Color.white;
                    view.name = "Item";
                    if (animate)
                        StartCoroutine(PopReveal(view.rectTransform));
                }
            }
        }

        IEnumerator PopReveal(RectTransform rect)
        {
            float t = 0f;
            const float duration = 0.26f;
            while (t < duration && rect != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                rect.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.2f);
                yield return null;
            }
            if (rect != null)
                rect.localScale = Vector3.one;
        }

        void OnLaneClicked(int column)
        {
            if (!pickMode || OnItemClicked == null)
            {
                OnColumnClicked?.Invoke(column);
                return;
            }
            OnItemClicked(column, RowAt(column));
        }

        /// <summary>
        /// 同列越靠下的压在上面，所以第 r 件露出来的只有它顶边往下一个叠放步长那一截，
        /// 最下面那件整件可点。
        /// </summary>
        int RowAt(int column)
        {
            int count = board.CountIn(column);
            if (count == 0 || column >= laneTaps.Count || laneTaps[column] == null) return -1;
            LaneTap tap = laneTaps[column];
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, tap.screen,
                    tap.eventCamera, out Vector2 local))
                return count - 1;

            Rect r = root.rect;
            float y = local.y - r.yMax;
            float stackTop = SlotPosition(column, 0).y + layout.itemSize * 0.5f;
            float step = layout.itemSize * layout.overlap;
            int row = Mathf.FloorToInt((stackTop - y) / step);
            return Mathf.Clamp(row, 0, count - 1);
        }

        float LaneXFallback(int column)
        {
            float pitch = 920f / Mathf.Max(1, board.ColumnCount);
            return (column - (board.ColumnCount - 1) * 0.5f) * pitch;
        }

        Vector2 SlotPosition(int column, int row)
        {
            Image hanger = hud != null ? hud.Hanger(column) : null;
            float x = hanger != null ? hanger.rectTransform.anchoredPosition.x : LaneXFallback(column);
            float stackTop = hanger != null
                ? hanger.rectTransform.anchoredPosition.y - hanger.rectTransform.sizeDelta.y
                : -GameHud.RodFromTop - GameHud.HangerHeight;
            float step = layout.itemSize * layout.overlap;
            return new Vector2(x, stackTop - layout.itemSize * 0.5f - row * step);
        }

        // ------------------------------------------------------------ 渲染

        Sprite SpriteFor(int type)
        {
            if (type == board.MysteryIndex)
                return mystery != null ? mystery.ResolveIcon() : null;
            return type >= 0 && type < palette.Count ? palette[type].ResolveIcon() : null;
        }

        Image SpawnItem(Transform parent, int type, float size)
        {
            var image = UiKit.Icon(parent, "Item", SpriteFor(type), new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(size, size));
            return image;
        }

        /// <summary>整盘重画，用在发牌、洗牌、撤回之后。</summary>
        public void Refresh()
        {
            StopDeny();
            foreach (Coroutine running in squishes.Values)
                if (running != null) StopCoroutine(running);
            squishes.Clear();
            for (int c = 0; c < board.ColumnCount; c++)
            {
                HideSparkles(c);
                List<Image> views = laneItems[c];
                for (int i = views.Count - 1; i >= 0; i--)
                    UiKit.Discard(views[i]);
                views.Clear();

                IReadOnlyList<int> column = board.Column(c);
                for (int r = 0; r < column.Count; r++)
                {
                    Image item = SpawnItem(laneRoots[c].parent, column[r], layout.itemSize);
                    PlaceOnBoard(item.rectTransform, SlotPosition(c, r));
                    views.Add(item);
                }
                ApplySiblingOrder(c);
            }

            UiKit.Discard(heldItem);
            heldItem = null;
            if (board.Held != SortBoard.Empty)
            {
                heldItem = SpawnItem(holdRoot, board.Held, layout.itemSize * 1.15f);
                PlaceHold(heldItem.rectTransform);
            }

            SnapParcels(false);
            SnapBadges();
            SnapSolvedChrome();
            SnapCovers();
            SnapLocks();
        }

        void PlaceOnBoard(RectTransform rect, Vector2 position)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
        }

        void PlaceHold(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        void ApplySiblingOrder(int column)
        {
            List<Image> views = laneItems[column];
            for (int r = 0; r < views.Count; r++)
            {
                if (views[r] != null)
                    views[r].rectTransform.SetAsLastSibling();
            }
            if (column < laneChecks.Count && laneChecks[column] != null)
                laneChecks[column].transform.SetAsLastSibling();
        }

        void ShowSparkles(int column)
        {
            if (column < 0 || column >= laneSparkles.Count) return;
            if (laneSparkles[column].Count > 0) return;
            Sprite sprite = hud != null ? hud.sparkleSprite : null;
            if (sprite == null || column >= laneItems.Count) return;
            List<Image> views = laneItems[column];
            if (views.Count == 0) return;

            int count = Mathf.Min(4, views.Count);
            for (int i = 0; i < count; i++)
            {
                Image host = views[Mathf.Min(views.Count - 1, i * views.Count / count)];
                float size = Random.Range(34f, 56f);
                Image spark = UiKit.Icon(host.transform, "Twinkle", sprite, new Vector2(0.5f, 0.5f),
                    new Vector2(Random.Range(-26f, 26f), Random.Range(-34f, 30f)),
                    new Vector2(size, size));
                spark.raycastTarget = false;
                spark.color = new Color(1f, 1f, 1f, 0f);
                laneSparkles[column].Add(spark);
            }
        }

        void HideSparkles(int column)
        {
            if (column < 0 || column >= laneSparkles.Count) return;
            List<Image> list = laneSparkles[column];
            for (int i = list.Count - 1; i >= 0; i--)
                UiKit.Discard(list[i]);
            list.Clear();
        }

        void Update()
        {
            UpdatePress();
            UpdateIdleHeld();
            if (laneSparkles.Count == 0) return;
            sparkleClock += Time.deltaTime;
            for (int c = 0; c < laneSparkles.Count; c++)
            {
                List<Image> list = laneSparkles[c];
                for (int i = 0; i < list.Count; i++)
                {
                    Image spark = list[i];
                    if (spark == null) continue;
                    float wave = Mathf.Sin(sparkleClock * 2.6f + i * 1.8f + c * 0.7f);
                    float glow = wave > 0f ? wave * wave : 0f;
                    spark.color = new Color(1f, 1f, 1f, glow);
                    spark.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.15f, glow);
                    spark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, sparkleClock * 50f + i * 40f);
                }
            }
        }

        /// <summary>手指按住哪列，那列底色亮一下、衣架往下沉一点；松手弹回。</summary>
        void UpdatePress()
        {
            float dt = Time.deltaTime;
            for (int c = 0; c < pressValue.Count; c++)
            {
                float target = c == pressedColumn && !pickMode ? 1f : 0f;
                float before = pressValue[c];
                if (Mathf.Approximately(before, target)) continue;
                float now = Mathf.MoveTowards(before, target, dt * (target > before ? 14f : 7f));
                pressValue[c] = now;
                if (c < laneBacks.Count && laneBacks[c] != null)
                    laneBacks[c].color = Color.Lerp(LaneIdle, LanePress, now);
                Image hanger = hud != null ? hud.Hanger(c) : null;
                if (hanger != null)
                    hanger.rectTransform.localScale = new Vector3(1f + now * 0.06f, 1f - now * 0.1f, 1f);
            }
        }

        /// <summary>手里那件没事的时候轻轻上下浮、左右晃，告诉玩家「我在这儿」。</summary>
        void UpdateIdleHeld()
        {
            if (heldItem == null || Busy || denyRoutine != null) return;
            idleClock += Time.deltaTime;
            RectTransform rect = heldItem.rectTransform;
            rect.anchoredPosition = new Vector2(0f, Mathf.Sin(idleClock * 2.6f) * 7f);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(idleClock * 1.7f) * 3f);
        }

        // ------------------------------------------------------------ 小动作

        void Squish(RectTransform rect, float amount, float duration = 0.34f, float delay = 0f)
        {
            if (rect == null) return;
            if (squishes.TryGetValue(rect, out Coroutine running) && running != null)
                StopCoroutine(running);
            squishes[rect] = StartCoroutine(SquishRoutine(rect, amount, duration, delay));
        }

        IEnumerator SquishRoutine(RectTransform rect, float amount, float duration, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            float t = 0f;
            while (t < duration && rect != null)
            {
                t += Time.deltaTime;
                float s = Ease.Spring(t / duration, 2.2f, 4.5f) * amount;
                rect.localScale = new Vector3(1f + s, 1f - s, 1f);
                yield return null;
            }
            if (rect != null)
            {
                rect.localScale = Vector3.one;
                squishes.Remove(rect);
            }
        }

        void StopSquish(RectTransform rect)
        {
            if (rect == null) return;
            if (squishes.TryGetValue(rect, out Coroutine running) && running != null)
                StopCoroutine(running);
            squishes.Remove(rect);
            rect.localScale = Vector3.one;
        }

        IEnumerator Hop(RectTransform rect, Vector2 home, float delay, float height, float grow, float duration)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            StopSquish(rect);
            float t = 0f;
            while (t < duration && rect != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float up = Mathf.Sin(k * Mathf.PI);
                rect.anchoredPosition = home + new Vector2(0f, up * height);
                // 起跳前压扁、空中拉长、落地再压一下
                float s = k < 0.15f ? -k / 0.15f * 0.12f : k > 0.85f ? (k - 0.85f) / 0.15f * 0.1f : -up * 0.08f;
                rect.localScale = new Vector3((1f + up * grow) * (1f - s), (1f + up * grow) * (1f + s), 1f);
                yield return null;
            }
            if (rect != null)
            {
                rect.anchoredPosition = home;
                Squish(rect, 0.1f, 0.24f);
            }
        }

        void SpawnGhost(Image source)
        {
            if (source == null || source.sprite == null) return;
            RectTransform from = source.rectTransform;
            Image ghost = UiKit.Icon(from.parent, "Ghost", source.sprite, from.anchorMin,
                from.anchoredPosition, from.sizeDelta);
            ghost.preserveAspect = true;
            RectTransform rect = ghost.rectTransform;
            rect.anchorMax = from.anchorMax;
            rect.pivot = from.pivot;
            rect.anchoredPosition = from.anchoredPosition;
            rect.localRotation = from.localRotation;
            rect.localScale = from.localScale;
            rect.SetSiblingIndex(Mathf.Max(0, from.GetSiblingIndex()));
            StartCoroutine(FadeGhost(ghost));
        }

        IEnumerator FadeGhost(Image ghost)
        {
            float t = 0f;
            const float life = 0.18f;
            while (t < life && ghost != null)
            {
                t += Time.deltaTime;
                float k = t / life;
                ghost.color = new Color(1f, 0.92f, 0.96f, 0.42f * (1f - k));
                ghost.rectTransform.localScale *= 1f - Time.deltaTime * 1.2f;
                yield return null;
            }
            UiKit.Discard(ghost);
        }

        Vector3 WorldOf(RectTransform parentSpace, Vector2 anchored)
        {
            Rect r = parentSpace.rect;
            Vector2 local = new Vector2(anchored.x, r.yMax + anchored.y);
            return parentSpace.TransformPoint(local);
        }

        void StopDeny()
        {
            if (denyRoutine != null)
            {
                StopCoroutine(denyRoutine);
                denyRoutine = null;
            }
            for (int i = 0; i < denyRects.Count; i++)
            {
                if (denyRects[i] == null) continue;
                denyRects[i].anchoredPosition = denyHomes[i];
                denyRects[i].localRotation = Quaternion.identity;
            }
            denyRects.Clear();
            denyHomes.Clear();
        }

        /// <summary>点了不能放的列：那列左右甩两下，手里那件摇头。</summary>
        public void Deny(int column)
        {
            if (Busy) return;
            StopDeny();
            if (column >= 0 && column < laneItems.Count)
            {
                foreach (Image view in laneItems[column])
                    if (view != null) denyRects.Add(view.rectTransform);
                if (column < padlocks.Count && padlocks[column] != null && padlocks[column].gameObject.activeSelf)
                    denyRects.Add(padlocks[column].rectTransform);
                if (column < coverViews.Count && coverViews[column] != null && coverViews[column].gameObject.activeSelf)
                    denyRects.Add(coverViews[column].rectTransform);
            }
            foreach (RectTransform rect in denyRects)
                denyHomes.Add(rect.anchoredPosition);
            WxBridge.Vibrate(WxBridge.Buzz.Light);
            denyRoutine = StartCoroutine(DenyRoutine());
        }

        IEnumerator DenyRoutine()
        {
            float t = 0f;
            const float duration = 0.34f;
            RectTransform held = heldItem != null ? heldItem.rectTransform : null;
            while (t < duration)
            {
                t += Time.deltaTime;
                float fall = 1f - Mathf.Clamp01(t / duration);
                float wave = Mathf.Sin(t * 58f) * fall;
                for (int i = 0; i < denyRects.Count; i++)
                {
                    if (denyRects[i] == null) continue;
                    denyRects[i].anchoredPosition = denyHomes[i] + new Vector2(wave * 13f, 0f);
                }
                if (held != null)
                    held.localRotation = Quaternion.Euler(0f, 0f, wave * 14f);
                yield return null;
            }
            if (held != null)
                held.localRotation = Quaternion.identity;
            denyRoutine = null;
            StopDeny();
        }

        void SnapSolvedChrome()
        {
            for (int c = 0; c < board.ColumnCount; c++)
            {
                bool on = board.IsColumnSolved(c);
                if (c < laneWashes.Count && laneWashes[c] != null)
                    laneWashes[c].color = on ? SolvedWash : new Color(1f, 1f, 1f, 0f);
                if (on)
                    ShowSparkles(c);
                else
                    HideSparkles(c);
                if (c < laneChecks.Count && laneChecks[c] != null)
                    laneChecks[c].transform.localScale = on ? Vector3.one : Vector3.zero;
                Image hanger = hud != null ? hud.Hanger(c) : null;
                if (hanger != null)
                    hanger.color = Color.white;
            }
        }

        // ------------------------------------------------------------ 动画

        public Coroutine PlayColumn(int column)
        {
            return StartCoroutine(PlayRoutine(column));
        }

        IEnumerator PlayRoutine(int column)
        {
            StopDeny();
            Busy = true;

            Image incoming = heldItem;
            if (incoming == null)
            {
                Busy = false;
                yield break;
            }
            Sfx.Play(SfxId.Lift);
            Sfx.Play(SfxId.Whoosh);
            heldItem = null;
            moveCount++;

            List<Image> views = laneItems[column];
            Vector3 incomingWorld = incoming.rectTransform.position;
            incoming.rectTransform.SetParent(laneRoots[column].parent, false);
            PlaceOnBoard(incoming.rectTransform, SlotPosition(column, 0));
            incoming.rectTransform.position = incomingWorld;
            incoming.rectTransform.sizeDelta = new Vector2(layout.itemSize * 1.18f, layout.itemSize * 1.18f);
            incoming.rectTransform.SetAsLastSibling();
            views.Insert(0, incoming);

            Image outgoing = views[views.Count - 1];
            views.RemoveAt(views.Count - 1);
            ApplySiblingOrder(column);
            incoming.rectTransform.SetAsLastSibling();

            board.Play(column);

            var motions = new List<Motion>();
            Vector2 itemSize = new Vector2(layout.itemSize, layout.itemSize);
            Vector2 holdSize = itemSize * 1.15f;

            Vector2 landing = SlotPosition(column, 0);
            RectTransform incomingRect = incoming.rectTransform;
            float spinSign = landing.x >= incomingRect.anchoredPosition.x ? -1f : 1f;
            motions.Add(new Motion
            {
                rect = incomingRect,
                from = incomingRect.anchoredPosition,
                to = landing,
                sizeFrom = holdSize,
                sizeTo = itemSize,
                delay = 0f,
                duration = 0.3f,
                arc = 42f,
                scale = true,
                ease = EaseKind.OutBack,
                spin = 360f * spinSign,
                stretch = true,
                trail = true,
                landAt = 0.72f,
                onLand = () =>
                {
                    Sfx.Play(SfxId.Land);
                    WxBridge.Vibrate(WxBridge.Buzz.Light);
                    Squish(incomingRect, 0.24f, 0.36f);
                    if (fx != null)
                        fx.Puff(WorldOf(root, landing + new Vector2(0f, -layout.itemSize * 0.3f)), layout.itemSize);
                },
            });

            for (int r = 1; r < views.Count; r++)
            {
                RectTransform rect = views[r].rectTransform;
                float ripple = Mathf.Lerp(0.16f, 0.06f, r / Mathf.Max(1f, views.Count - 1f));
                motions.Add(new Motion
                {
                    rect = rect,
                    from = rect.anchoredPosition,
                    to = SlotPosition(column, r),
                    delay = 0.17f + r * 0.025f,
                    duration = 0.15f,
                    arc = 0f,
                    ease = EaseKind.OutBack,
                    onLand = () => Squish(rect, ripple, 0.3f),
                });
            }

            outgoing.rectTransform.SetParent(holdRoot, true);
            outgoing.rectTransform.SetAsLastSibling();
            Vector3 world = outgoing.rectTransform.position;
            outgoing.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            outgoing.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            outgoing.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            outgoing.rectTransform.position = world;
            outgoing.rectTransform.sizeDelta = itemSize;
            RectTransform outgoingRect = outgoing.rectTransform;
            motions.Add(new Motion
            {
                rect = outgoingRect,
                from = outgoingRect.anchoredPosition,
                to = Vector2.zero,
                sizeFrom = itemSize,
                sizeTo = holdSize,
                delay = 0.16f,
                duration = 0.3f,
                arc = -38f,
                scale = true,
                ease = EaseKind.OutCubic,
                spin = -360f * spinSign,
                stretch = true,
                trail = true,
                landAt = 0.82f,
                onLand = () =>
                {
                    Sfx.Play(SfxId.Drop);
                    Squish(outgoingRect, 0.28f, 0.4f);
                    if (fx != null)
                    {
                        fx.RingWave(holdRoot.position, layout.itemSize * 1.4f, new Color(1f, 1f, 1f, 0.8f), 0.32f);
                        fx.Puff(holdRoot.TransformPoint(new Vector3(0f, -layout.itemSize * 0.45f, 0f)),
                            layout.itemSize * 0.8f);
                    }
                },
            });

            yield return RunMotions(motions);
            idleClock = 0f;

            heldItem = outgoing;
            if (board.LastSettles != null && board.LastSettles.Length > 0)
            {
                yield return PulseHold();
                yield return AnimateReadySettles();
            }
            SnapParcels(true);
            SnapBadges();

            if (board.IsColumnSolved(column))
                yield return Celebrate(column);
            yield return OpenLocks();
            yield return OpenCovers();

            SnapSolvedChrome();
            SnapCovers();
            SnapLocks();
            Busy = false;
        }

        public Coroutine PlaySwap(int column, int row)
        {
            return StartCoroutine(SwapRoutine(column, row));
        }

        IEnumerator SwapRoutine(int column, int row)
        {
            Busy = true;
            List<Image> views = laneItems[column];
            Image incoming = heldItem;
            if (incoming == null || row < 0 || row >= views.Count)
            {
                Busy = false;
                yield break;
            }
            StopDeny();
            Image outgoing = views[row];
            heldItem = null;
            moveCount++;
            Sfx.Play(SfxId.Swap);
            Sfx.Play(SfxId.Whoosh);

            board.Swap(column, row);

            Vector2 itemSize = new Vector2(layout.itemSize, layout.itemSize);
            Vector2 holdSize = itemSize * 1.15f;

            Vector3 world = incoming.rectTransform.position;
            incoming.rectTransform.SetParent(laneRoots[column].parent, false);
            PlaceOnBoard(incoming.rectTransform, SlotPosition(column, row));
            incoming.rectTransform.position = world;
            views[row] = incoming;
            ApplySiblingOrder(column);

            world = outgoing.rectTransform.position;
            outgoing.rectTransform.SetParent(holdRoot, true);
            PlaceHold(outgoing.rectTransform);
            outgoing.rectTransform.position = world;
            outgoing.rectTransform.SetAsLastSibling();

            RectTransform inRect = incoming.rectTransform;
            RectTransform outRect = outgoing.rectTransform;
            Vector2 slot = SlotPosition(column, row);
            var motions = new List<Motion>
            {
                new Motion
                {
                    rect = inRect,
                    from = inRect.anchoredPosition,
                    to = slot,
                    sizeFrom = holdSize,
                    sizeTo = itemSize,
                    duration = 0.32f,
                    arc = 60f,
                    scale = true,
                    ease = EaseKind.OutBack,
                    spin = -360f,
                    stretch = true,
                    trail = true,
                    landAt = 0.72f,
                    onLand = () =>
                    {
                        Sfx.Play(SfxId.Land);
                        WxBridge.Vibrate(WxBridge.Buzz.Light);
                        Squish(inRect, 0.24f, 0.36f);
                        if (fx != null) fx.Puff(WorldOf(root, slot), layout.itemSize);
                    },
                },
                new Motion
                {
                    rect = outRect,
                    from = outRect.anchoredPosition,
                    to = Vector2.zero,
                    sizeFrom = itemSize,
                    sizeTo = holdSize,
                    delay = 0.04f,
                    duration = 0.32f,
                    arc = 60f,
                    scale = true,
                    ease = EaseKind.OutCubic,
                    spin = 360f,
                    stretch = true,
                    trail = true,
                    landAt = 0.82f,
                    onLand = () =>
                    {
                        Sfx.Play(SfxId.Drop);
                        Squish(outRect, 0.28f, 0.4f);
                        if (fx != null)
                            fx.RingWave(holdRoot.position, layout.itemSize * 1.4f, new Color(1f, 1f, 1f, 0.8f), 0.32f);
                    },
                },
            };
            yield return RunMotions(motions);
            idleClock = 0f;

            heldItem = outgoing;
            if (board.LastSettles != null && board.LastSettles.Length > 0)
            {
                yield return PulseHold();
                yield return AnimateReadySettles();
            }
            SnapParcels(true);
            SnapBadges();
            if (board.IsColumnSolved(column))
                yield return Celebrate(column);
            yield return OpenCovers();

            SnapSolvedChrome();
            SnapCovers();
            SnapLocks();
            Busy = false;
        }

        IEnumerator PulseHold()
        {
            if (heldItem == null) yield break;
            RectTransform rect = heldItem.rectTransform;
            float t = 0f;
            const float duration = 0.18f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float pulse = 1f + Mathf.Sin(k * Mathf.PI) * 0.12f;
                rect.localScale = Vector3.one * pulse;
                yield return null;
            }
            rect.localScale = Vector3.one;
        }

        IEnumerator AnimateReadySettles()
        {
            SortBoard.Settle[] settles = board.LastSettles;
            if (settles == null || settles.Length == 0) yield break;

            var motions = new List<Motion>();
            for (int i = 0; i < settles.Length; i++)
            {
                SortBoard.Settle settle = settles[i];
                CollectSettleMotions(settle.Column, settle.FromIndex, motions);
                StartCoroutine(FlashLane(settle.Column));
            }

            if (motions.Count == 0) yield break;
            yield return RunMotions(motions);
        }

        void CollectSettleMotions(int column, int oddIndex, List<Motion> motions)
        {
            if (column < 0 || column >= laneItems.Count) return;
            List<Image> views = laneItems[column];
            if (oddIndex < 0 || oddIndex >= views.Count) return;

            Image odd = views[oddIndex];
            views.RemoveAt(oddIndex);
            views.Add(odd);
            ApplySiblingOrder(column);
            odd.rectTransform.SetAsLastSibling();

            for (int r = 0; r < views.Count; r++)
            {
                bool isOdd = r == views.Count - 1;
                RectTransform rect = views[r].rectTransform;
                Vector2 to = SlotPosition(column, r);
                motions.Add(new Motion
                {
                    rect = rect,
                    from = rect.anchoredPosition,
                    to = to,
                    delay = isOdd ? 0f : 0.04f + r * 0.018f,
                    duration = isOdd ? 0.34f : 0.2f,
                    arc = isOdd ? 58f : 0f,
                    ease = isOdd ? EaseKind.OutBack : EaseKind.OutQuad,
                    spin = isOdd ? 360f : 0f,
                    trail = isOdd,
                    landAt = isOdd ? 0.75f : 1f,
                    onLand = isOdd
                        ? (System.Action)(() =>
                        {
                            Sfx.Play(SfxId.Land);
                            Squish(rect, 0.22f, 0.34f);
                            if (fx != null) fx.Puff(WorldOf(root, to), layout.itemSize * 0.8f);
                        })
                        : () => Squish(rect, 0.08f, 0.24f),
                });
            }
        }

        IEnumerator FlashLane(int column)
        {
            if (column < 0 || column >= laneWashes.Count) yield break;
            Image wash = laneWashes[column];
            if (wash == null || board.IsColumnSolved(column)) yield break;

            Color peek = new Color(1f, 1f, 1f, 0.55f);
            Color clear = new Color(1f, 1f, 1f, 0f);
            float t = 0f;
            const float duration = 0.42f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                wash.color = Color.Lerp(peek, clear, k);
                yield return null;
            }
            wash.color = clear;
        }

        /// <summary>
        /// 一列叠好：光柱冲上去、整列自下而上跳一遍、勾带着星爆和光环弹出来、彩纸喷一把、飘夸奖字。
        /// 两步之内接着又叠好一列算连击，和弦一档比一档高、字越夸越大。
        /// </summary>
        IEnumerator Celebrate(int column)
        {
            combo = moveCount - lastClearMove <= 2 ? combo + 1 : 1;
            lastClearMove = moveCount;
            Sfx.PlayClear(combo);
            WxBridge.Vibrate(WxBridge.Buzz.Medium);

            Image wash = column < laneWashes.Count ? laneWashes[column] : null;
            Image check = column < laneChecks.Count ? laneChecks[column] : null;
            List<Image> views = laneItems[column];
            Vector2 top = SlotPosition(column, 0);
            Vector2 bottom = SlotPosition(column, Mathf.Max(0, views.Count - 1));
            Vector2 middle = (top + bottom) * 0.5f;

            if (check != null)
                check.transform.localScale = Vector3.zero;

            Image pillar = null;
            if (wash != null)
            {
                RectTransform w = wash.rectTransform;
                pillar = UiKit.Slice(root, "Pillar", UiKit.Rounded, w.anchorMin, w.anchoredPosition,
                    w.sizeDelta, new Color(1f, 1f, 1f, 0f));
                pillar.pixelsPerUnitMultiplier = 0.4f;
                pillar.rectTransform.pivot = new Vector2(0.5f, 0f);
                pillar.rectTransform.anchoredPosition = w.anchoredPosition - new Vector2(0f, w.sizeDelta.y * 0.5f);
                pillar.rectTransform.SetAsLastSibling();
            }

            if (fx != null)
            {
                fx.Shake(root, 7f + combo * 2f, 0.22f);
                fx.Confetti(WorldOf(root, top), 14 + combo * 4, 1050f + combo * 80f, 0.7f);
                fx.Sparkles(WorldOf(root, middle), 6, 380f, 48f);
                int word = Mathf.Clamp(combo, 1, ComboWords.Length) - 1;
                string text = combo >= 2 ? ComboWords[word] + " ×" + combo : ComboWords[word];
                fx.FloatWord(WorldOf(root, middle + new Vector2(0f, 24f)), text, ComboColors[word],
                    46 + Mathf.Min(combo, 4) * 4);
            }

            const float wave = 0.045f;
            for (int i = views.Count - 1; i >= 0; i--)
            {
                if (views[i] == null) continue;
                int order = views.Count - 1 - i;
                StartCoroutine(Hop(views[i].rectTransform, SlotPosition(column, i), order * wave, 30f, 0.12f, 0.24f));
            }

            float checkAt = 0.1f + views.Count * wave * 0.5f;
            bool checkPopped = false;
            float t = 0f;
            float duration = Mathf.Max(0.5f, checkAt + 0.32f);
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                if (wash != null)
                {
                    float a = t < 0.12f ? Mathf.Lerp(0f, 0.95f, t / 0.12f) : Mathf.Lerp(0.95f, SolvedWash.a, (t - 0.12f) / 0.3f);
                    wash.color = new Color(1f, 1f, 1f, a);
                }
                if (pillar != null)
                {
                    float rise = Ease.OutCubic(Mathf.Clamp01(t / 0.22f));
                    float fade = 1f - Mathf.Clamp01((t - 0.14f) / 0.3f);
                    pillar.rectTransform.localScale = new Vector3(1f + rise * 0.18f, Mathf.Max(0.01f, rise), 1f);
                    pillar.color = new Color(1f, 1f, 0.92f, 0.7f * fade);
                }
                if (check != null && t >= checkAt)
                {
                    if (!checkPopped)
                    {
                        checkPopped = true;
                        if (fx != null)
                        {
                            Vector3 at = check.rectTransform.position;
                            fx.Burst(at, 250f, 0.36f);
                            fx.RingWave(at, 220f, MintRing, 0.42f);
                            fx.Sparkles(at, 8, 520f, 52f);
                        }
                    }
                    float c = Mathf.Clamp01((t - checkAt) / 0.3f);
                    check.transform.localScale = Vector3.one * Mathf.Max(0f, Ease.OutBack(c, 3.2f));
                    check.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-35f, 0f, Ease.OutCubic(c)));
                }
                yield return null;
            }

            if (pillar != null)
                UiKit.Discard(pillar);
            if (wash != null)
                wash.color = SolvedWash;
            if (check != null)
            {
                check.transform.localScale = Vector3.one;
                check.transform.localRotation = Quaternion.identity;
                check.transform.SetAsLastSibling();
            }
            if (fx == null)
                BurstSparks(column);
        }

        /// <summary>开局发牌：每列从上往下掉进来，底下那件先到；最后手里那件弹出来。</summary>
        public Coroutine PlayDealIn(float startDelay = 0f)
        {
            StopDeny();
            return StartCoroutine(DealInRoutine(startDelay));
        }

        IEnumerator DealInRoutine(float startDelay)
        {
            Busy = true;
            float longest = startDelay;
            var routines = new List<Coroutine>();
            for (int c = 0; c < laneItems.Count; c++)
            {
                List<Image> views = laneItems[c];
                for (int r = views.Count - 1; r >= 0; r--)
                {
                    if (views[r] == null) continue;
                    float delay = startDelay + c * 0.04f + (views.Count - 1 - r) * 0.03f;
                    longest = Mathf.Max(longest, delay + 0.3f);
                    routines.Add(StartCoroutine(DropIn(views[r], SlotPosition(c, r), delay)));
                }
            }
            if (startDelay > 0f)
                StartCoroutine(PlayLater(SfxId.DealIn, startDelay));
            else
                Sfx.Play(SfxId.DealIn);
            if (heldItem != null)
            {
                RectTransform held = heldItem.rectTransform;
                held.localScale = Vector3.zero;
                float t = 0f;
                float wait = Mathf.Max(0.1f, longest - 0.12f);
                while (t < wait)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
                t = 0f;
                while (t < 0.3f && held != null)
                {
                    t += Time.deltaTime;
                    held.localScale = Vector3.one * Mathf.Max(0f, Ease.OutBack(t / 0.3f, 2.6f));
                    yield return null;
                }
                if (held != null) held.localScale = Vector3.one;
                if (fx != null && holdRoot != null)
                    fx.Sparkles(holdRoot.position, 6, 300f, 44f);
            }
            for (int i = 0; i < routines.Count; i++)
                yield return routines[i];
            Busy = false;
        }

        IEnumerator PlayLater(SfxId id, float delay)
        {
            yield return new WaitForSeconds(delay);
            Sfx.Play(id);
        }

        IEnumerator DropIn(Image view, Vector2 home, float delay)
        {
            RectTransform rect = view.rectTransform;
            Color full = view.color;
            full.a = 1f;
            Color hidden = full;
            hidden.a = 0f;
            view.color = hidden;
            rect.anchoredPosition = home + new Vector2(0f, 420f);
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
            float t = 0f;
            const float duration = 0.3f;
            while (t < duration && rect != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                rect.anchoredPosition = home + new Vector2(0f, (1f - k * k) * 420f);
                view.color = Color.Lerp(hidden, full, k * 3f);
                yield return null;
            }
            if (rect == null) yield break;
            rect.anchoredPosition = home;
            view.color = full;
            Squish(rect, 0.18f, 0.3f);
        }

        /// <summary>
        /// 全部叠好后的收尾：每列从左到右依次跳一下、勾再弹一次，
        /// 然后礼炮彩纸、大横幅和过关和弦一起上。
        /// </summary>
        public IEnumerator Finale(string banner)
        {
            Busy = true;
            yield return new WaitForSeconds(0.12f);
            for (int c = 0; c < laneItems.Count; c++)
            {
                List<Image> views = laneItems[c];
                if (views.Count == 0) continue;
                for (int r = 0; r < views.Count; r++)
                {
                    if (views[r] != null)
                        StartCoroutine(Hop(views[r].rectTransform, SlotPosition(c, r), (views.Count - 1 - r) * 0.02f, 34f, 0.1f, 0.26f));
                }
                Image check = c < laneChecks.Count ? laneChecks[c] : null;
                if (check != null)
                {
                    StartCoroutine(PopReveal(check.rectTransform));
                    if (fx != null)
                        fx.Sparkles(check.rectTransform.position, 5, 300f, 40f);
                }
                Sfx.Play(SfxId.Land);
                yield return new WaitForSeconds(0.07f);
            }

            Sfx.Play(SfxId.Finale);
            Sfx.Play(SfxId.Confetti);
            WxBridge.Vibrate(WxBridge.Buzz.Heavy);
            if (fx != null)
            {
                Rect area = fx.Layer.rect;
                fx.Shake(root, 12f, 0.3f);
                Vector3 left = fx.Layer.TransformPoint(new Vector3(area.xMin + 40f, area.yMin + 260f, 0f));
                Vector3 right = fx.Layer.TransformPoint(new Vector3(area.xMax - 40f, area.yMin + 260f, 0f));
                fx.Confetti(left, 34, 2000f, 0.25f, 68f);
                fx.Confetti(right, 34, 2000f, 0.25f, 112f);
                StartCoroutine(fx.ConfettiRain(40, 0.9f));
                yield return fx.Banner(banner, 0.75f);
            }
            else
                yield return new WaitForSeconds(1f);
            Busy = false;
        }

        void BurstSparks(int column)
        {
            Vector2 origin = SlotPosition(column, 0);
            for (int i = 0; i < 6; i++)
            {
                float angle = (i / 6f) * Mathf.PI * 2f - Mathf.PI * 0.5f;
                Image spark = UiKit.Slice(root, "Spark", UiKit.Circle, new Vector2(0.5f, 1f),
                    origin, new Vector2(14f, 14f), new Color(1f, 0.92f, 0.45f, 1f));
                spark.raycastTarget = false;
                StartCoroutine(FlySpark(spark.rectTransform, origin,
                    origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 70f));
            }
        }

        IEnumerator FlySpark(RectTransform rect, Vector2 from, Vector2 to)
        {
            float t = 0f;
            Image image = rect.GetComponent<Image>();
            while (t < 0.32f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.32f);
                rect.anchoredPosition = Vector2.Lerp(from, to, 1f - Mathf.Pow(1f - k, 2f));
                float fade = 1f - k;
                if (image != null)
                    image.color = new Color(1f, 0.92f, 0.45f, fade);
                rect.localScale = Vector3.one * (1.1f - k * 0.8f);
                yield return null;
            }
            UiKit.Discard(rect);
        }

        IEnumerator RunMotions(List<Motion> motions)
        {
            float longest = 0f;
            for (int i = 0; i < motions.Count; i++)
                longest = Mathf.Max(longest, motions[i].delay + motions[i].duration);

            var landed = new bool[motions.Count];
            var ghostClock = new float[motions.Count];
            float t = 0f;
            while (t < longest)
            {
                t += Time.deltaTime;
                for (int i = 0; i < motions.Count; i++)
                {
                    Motion m = motions[i];
                    ApplyMotion(m, t);
                    float k = m.duration <= 0f ? 1f : (t - m.delay) / m.duration;
                    float landAt = m.landAt > 0f ? m.landAt : 1f;
                    if (m.trail && k > 0.05f && k < landAt)
                    {
                        ghostClock[i] -= Time.deltaTime;
                        if (ghostClock[i] <= 0f)
                        {
                            ghostClock[i] = 0.028f;
                            SpawnGhost(m.rect != null ? m.rect.GetComponent<Image>() : null);
                        }
                    }
                    if (!landed[i] && k >= landAt)
                    {
                        landed[i] = true;
                        m.onLand?.Invoke();
                    }
                }
                yield return null;
            }

            for (int i = 0; i < motions.Count; i++)
            {
                Motion m = motions[i];
                if (m.rect == null) continue;
                if (!landed[i])
                    m.onLand?.Invoke();
                m.rect.anchoredPosition = m.to;
                if (m.scale)
                    m.rect.sizeDelta = m.sizeTo;
                if (Mathf.Abs(m.spin) > 0.01f)
                    m.rect.localRotation = Quaternion.identity;
                if (m.stretch && !squishes.ContainsKey(m.rect))
                    m.rect.localScale = Vector3.one;
            }
        }

        static void ApplyMotion(Motion m, float time)
        {
            if (m.rect == null) return;
            float local = time - m.delay;
            if (local < 0f) return;
            float k = m.duration <= 0f ? 1f : Mathf.Clamp01(local / m.duration);
            float e = SampleEase(m.ease, k);
            if (Mathf.Abs(m.spin) > 0.01f)
                m.rect.localRotation = Quaternion.Euler(0f, 0f, m.spin * e);
            float landAt = m.landAt > 0f ? m.landAt : 1f;
            if (m.stretch && k < landAt)
            {
                float s = Mathf.Sin(k / landAt * Mathf.PI) * 0.16f;
                m.rect.localScale = new Vector3(1f - s, 1f + s, 1f);
            }
            Vector2 p = Vector2.LerpUnclamped(m.from, m.to, e);
            if (Mathf.Abs(m.arc) > 0.01f)
            {
                Vector2 dir = m.to - m.from;
                Vector2 n = new Vector2(-dir.y, dir.x);
                if (n.sqrMagnitude > 0.01f)
                    n.Normalize();
                p += n * (Mathf.Sin(k * Mathf.PI) * m.arc);
            }
            m.rect.anchoredPosition = p;
            if (m.scale)
                m.rect.sizeDelta = Vector2.Lerp(m.sizeFrom, m.sizeTo, e);
        }

        static float SampleEase(EaseKind ease, float k)
        {
            if (ease == EaseKind.OutBack)
            {
                const float s = 1.35f;
                k -= 1f;
                return k * k * ((s + 1f) * k + s) + 1f;
            }
            if (ease == EaseKind.OutQuad)
                return 1f - (1f - k) * (1f - k);
            return 1f - Mathf.Pow(1f - k, 3f);
        }
    }
}
