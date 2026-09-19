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
        Image heldItem;

        public bool Busy { get; private set; }
        public System.Action<int> OnColumnClicked;

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
                // 对齐动物收纳那类竖列：棋子明显小于列距，列间留出缝。
                layout.itemSize = Mathf.Clamp(pitch * 0.74f, 120f, 152f);
            }
            BuildLanes();
            Refresh();
        }

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
            for (int i = laneRoots.Count - 1; i >= 0; i--)
                UiKit.Discard(laneRoots[i]);
            laneRoots.Clear();
            laneItems.Clear();
            laneWashes.Clear();
            laneChecks.Clear();

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

                Sprite washSprite = hud != null ? hud.laneSolved : null;
                if (washSprite != null)
                {
                    Image wash = UiKit.Slice(root, "Wash_" + c, washSprite, new Vector2(0.5f, 1f),
                        new Vector2(x, (washTop + washBottom) * 0.5f),
                        new Vector2(layout.itemSize + 12f, washH),
                        new Color(1f, 1f, 1f, 0f));
                    wash.raycastTarget = false;
                    wash.transform.SetSiblingIndex(0);
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

                int index = c;
                Button hit = UiKit.HitArea(root, "Lane_" + c, new Vector2(0.5f, 1f),
                    new Vector2(x, hangerTop - laneHeight * 0.5f),
                    new Vector2(Mathf.Max(GameHud.HangerWidth, layout.itemSize) + 12f, laneHeight),
                    () => OnColumnClicked?.Invoke(index));
                laneRoots.Add(hit.transform as RectTransform);
                laneItems.Add(new List<Image>());
            }

            holdRoot = hud != null && hud.holdSlot != null ? hud.holdSlot : holdRoot;
            if (holdRoot == null)
                holdRoot = UiKit.Rect(root, "Hold", new Vector2(0.5f, 0f),
                    new Vector2(0f, 400f), new Vector2(200f, 200f));
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
            for (int c = 0; c < board.ColumnCount; c++)
            {
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

            SnapSolvedChrome();
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

        void SnapSolvedChrome()
        {
            for (int c = 0; c < board.ColumnCount; c++)
            {
                bool on = board.IsColumnSolved(c);
                if (c < laneWashes.Count && laneWashes[c] != null)
                    laneWashes[c].color = on
                        ? Color.white
                        : new Color(1f, 1f, 1f, 0f);
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
            Busy = true;

            Image incoming = heldItem;
            if (incoming == null)
            {
                Busy = false;
                yield break;
            }
            heldItem = null;

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

            motions.Add(new Motion
            {
                rect = incoming.rectTransform,
                from = incoming.rectTransform.anchoredPosition,
                to = SlotPosition(column, 0),
                sizeFrom = holdSize,
                sizeTo = itemSize,
                delay = 0f,
                duration = 0.28f,
                arc = 42f,
                scale = true,
                ease = EaseKind.OutBack,
            });

            for (int r = 1; r < views.Count; r++)
            {
                motions.Add(new Motion
                {
                    rect = views[r].rectTransform,
                    from = views[r].rectTransform.anchoredPosition,
                    to = SlotPosition(column, r),
                    delay = 0.03f + r * 0.012f,
                    duration = 0.18f,
                    arc = 0f,
                    ease = EaseKind.OutQuad,
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
            motions.Add(new Motion
            {
                rect = outgoing.rectTransform,
                from = outgoing.rectTransform.anchoredPosition,
                to = Vector2.zero,
                sizeFrom = itemSize,
                sizeTo = holdSize,
                delay = 0.12f,
                duration = 0.3f,
                arc = -38f,
                scale = true,
                ease = EaseKind.OutCubic,
            });

            yield return RunMotions(motions);

            heldItem = outgoing;
            if (board.LastSettles != null && board.LastSettles.Length > 0)
            {
                yield return PulseHold();
                yield return AnimateReadySettles();
            }

            if (board.IsColumnSolved(column))
                yield return Celebrate(column);

            SnapSolvedChrome();
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
                motions.Add(new Motion
                {
                    rect = views[r].rectTransform,
                    from = views[r].rectTransform.anchoredPosition,
                    to = SlotPosition(column, r),
                    delay = isOdd ? 0f : 0.04f + r * 0.018f,
                    duration = isOdd ? 0.34f : 0.2f,
                    arc = isOdd ? 58f : 0f,
                    ease = isOdd ? EaseKind.OutBack : EaseKind.OutQuad,
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

        IEnumerator Celebrate(int column)
        {
            Image wash = column < laneWashes.Count ? laneWashes[column] : null;
            Image check = column < laneChecks.Count ? laneChecks[column] : null;
            List<Image> views = laneItems[column];

            if (check != null)
                check.transform.localScale = Vector3.zero;

            float t = 0f;
            const float duration = 0.38f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float ease = 1f - Mathf.Pow(1f - k, 3f);
                if (wash != null)
                    wash.color = new Color(1f, 1f, 1f, ease);
                if (check != null)
                {
                    float pop = k < 0.6f
                        ? Mathf.Lerp(0f, 1.18f, k / 0.6f)
                        : Mathf.Lerp(1.18f, 1f, (k - 0.6f) / 0.4f);
                    check.transform.localScale = Vector3.one * pop;
                }
                float punch = 1f + Mathf.Sin(k * Mathf.PI) * 0.08f;
                for (int i = 0; i < views.Count; i++)
                {
                    if (views[i] != null)
                        views[i].rectTransform.localScale = Vector3.one * punch;
                }
                yield return null;
            }

            if (wash != null)
                wash.color = Color.white;
            if (check != null)
                check.transform.localScale = Vector3.one;
            for (int i = 0; i < views.Count; i++)
            {
                if (views[i] != null)
                    views[i].rectTransform.localScale = Vector3.one;
            }

            BurstSparks(column);
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

            float t = 0f;
            while (t < longest)
            {
                t += Time.deltaTime;
                for (int i = 0; i < motions.Count; i++)
                    ApplyMotion(motions[i], t);
                yield return null;
            }

            for (int i = 0; i < motions.Count; i++)
            {
                Motion m = motions[i];
                if (m.rect == null) continue;
                m.rect.anchoredPosition = m.to;
                if (m.scale)
                    m.rect.sizeDelta = m.sizeTo;
            }
        }

        static void ApplyMotion(Motion m, float time)
        {
            if (m.rect == null) return;
            float local = time - m.delay;
            if (local < 0f) return;
            float k = m.duration <= 0f ? 1f : Mathf.Clamp01(local / m.duration);
            float e = SampleEase(m.ease, k);
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
