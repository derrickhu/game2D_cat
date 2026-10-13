using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 把 PackBoard 画进七条衣架，管所有动效：点选抬起、抛物线倒衣服、排满变绿、
    /// 一列飞进车窗、坐满开车换车、补充时衣服从门里一件件弹进列里、看广告收回。
    /// 动效期间 Busy，面板这时不接点击。
    /// </summary>
    public class PackView : MonoBehaviour
    {
        PackHud hud;
        BoardFx fx;
        PackBoard board;
        ItemDef[] palette;
        readonly List<Image>[] items = new List<Image>[PackBoard.ColumnCount];
        readonly bool[] laneWasReady = new bool[PackBoard.ColumnCount];
        readonly bool[] laneWasOpen = new bool[PackBoard.ColumnCount];
        bool painted;
        int readyStreak;

        public bool Busy { get; private set; }

        /// <summary>这一下没倒成时的提示。</summary>
        public string Blocked { get; private set; }

        public static PackView Attach(PackHud hud, BoardFx fx)
        {
            var view = hud.gameObject.AddComponent<PackView>();
            view.hud = hud;
            view.fx = fx;
            for (int i = 0; i < view.items.Length; i++)
                view.items[i] = new List<Image>();
            return view;
        }

        public void Bind(PackBoard next, ItemDef[] dresses)
        {
            StopAllCoroutines();
            Busy = false;
            board = next;
            palette = dresses;
            painted = false;
            readyStreak = 0;
            for (int i = 0; i < laneWasReady.Length; i++)
            {
                laneWasReady[i] = false;
                laneWasOpen[i] = false;
            }
            for (int c = 0; c < items.Length; c++)
            {
                ClearStack(c);
                if (hud.lanes[c] != null) hud.lanes[c].rectTransform.localScale = Vector3.one;
            }
            PaintLanes(false);
        }

        void Update()
        {
            if (board == null || Busy) return;
            ApplyLift();
        }

        public Sprite SpriteFor(int type)
        {
            if (palette == null || type < 0 || type >= palette.Length || palette[type] == null)
                return hud.Mystery;
            return palette[type].ResolveIcon();
        }

        // ------------------------------------------------------------ 画面

        void ClearStack(int c)
        {
            RectTransform stack = hud.stacks[c];
            if (stack != null)
            {
                for (int i = stack.childCount - 1; i >= 0; i--)
                    UiKit.Discard(stack.GetChild(i));
            }
            items[c].Clear();
        }

        public void Refresh(bool celebrate = true)
        {
            if (board == null) return;
            for (int c = 0; c < items.Length; c++)
            {
                ClearStack(c);
                if (!board.IsOpen(c)) continue;
                IReadOnlyList<PackBoard.Cell> column = board.Column(c);
                for (int row = 0; row < column.Count; row++)
                    items[c].Add(Spawn(c, row, column[row]));
            }
            PaintLanes(celebrate);
            ApplyLift();
        }

        Image Spawn(int column, int row, PackBoard.Cell cell)
        {
            Sprite sprite = cell.Revealed ? SpriteFor(cell.Type) : hud.Mystery;
            Image image = UiKit.Icon(hud.stacks[column], "Dress", sprite, new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(PackHud.Item, PackHud.Item));
            Image shadow = UiKit.Icon(image.transform, "Shadow", UiKit.Circle, new Vector2(0.5f, 0f),
                new Vector2(0f, 6f), new Vector2(PackHud.Item * 0.6f, 14f));
            shadow.preserveAspect = false;
            shadow.color = new Color(0.45f, 0.24f, 0.2f, 0.22f);
            shadow.transform.SetAsFirstSibling();
            Place(image.rectTransform, row, board.Column(column).Count);
            return image;
        }

        static void Place(RectTransform rect, int row, int count)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(PackHud.Item, PackHud.Item);
            rect.anchoredPosition = PackHud.SlotPosition(row, count);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        void PaintLanes(bool celebrate)
        {
            int newlyReady = 0;
            for (int c = 0; c < PackBoard.ColumnCount; c++)
            {
                bool open = board.IsOpen(c);
                bool ready = board.IsFullUniform(c);
                bool selected = open && c == board.Selected;
                hud.PaintLane(c, open, board.IsAdColumn(c), ready, selected, open ? null : board.LockHint(c));
                bool justReady = ready && !laneWasReady[c];
                bool justOpened = open && painted && !laneWasOpen[c];
                laneWasReady[c] = ready;
                laneWasOpen[c] = open;
                if (justReady && celebrate)
                {
                    newlyReady++;
                    StartCoroutine(PopReady(c, readyStreak + newlyReady));
                }
                if (justOpened && celebrate)
                    StartCoroutine(PopUnlock(c));
            }
            if (newlyReady > 0) readyStreak += newlyReady;
            if (!board.CanBoard) readyStreak = 0;
            painted = true;
        }

        void ApplyLift()
        {
            int column = board.Selected;
            if (column < 0 || column >= items.Length) return;
            int count = board.GroupSize(column);
            int total = board.Column(column).Count;
            float bob = Mathf.Sin(Time.unscaledTime * 6f) * 5f;
            List<Image> stack = items[column];
            for (int i = 0; i < count && i < stack.Count; i++)
            {
                if (stack[i] == null) continue;
                RectTransform rect = stack[i].rectTransform;
                Vector2 pos = PackHud.SlotPosition(i, total);
                pos.y += 44f + bob;
                rect.anchoredPosition = pos;
                rect.localScale = Vector3.one * 1.08f;
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 4f + i) * 3f);
            }
        }

        void SettleColumn(int column)
        {
            if (column < 0 || column >= items.Length) return;
            int total = items[column].Count;
            for (int i = 0; i < total; i++)
            {
                if (items[column][i] != null)
                    Place(items[column][i].rectTransform, i, total);
            }
        }

        // ------------------------------------------------------------ 点列

        public IEnumerator PlayTap(int column)
        {
            Blocked = null;
            if (board == null || Busy) yield break;
            int source = board.Selected;
            int pour = source >= 0 && source != column ? board.PourCount(source, column) : 0;

            if (pour <= 0)
            {
                if (source >= 0 && source != column && board.TopsMatch(source, column))
                {
                    Blocked = "这一列已经满了";
                    Sfx.Play(SfxId.Deny);
                    fx.Shake(hud.lanes[column].rectTransform, 9f, 0.22f);
                    yield break;
                }
                int before = board.Selected;
                board.Tap(column);
                if (before >= 0) SettleColumn(before);
                if (board.Selected >= 0)
                {
                    Sfx.Play(SfxId.Lift);
                    WxBridge.Vibrate(WxBridge.Buzz.Light);
                }
                else if (board.IsOpen(column) && board.Column(column).Count == 0 && before < 0)
                {
                    Sfx.Play(SfxId.Deny);
                }
                PaintLanes(true);
                yield break;
            }

            Busy = true;
            Sfx.Play(SfxId.Whoosh);
            bool[][] snap = SnapshotRevealed();
            var moving = new List<Image>();
            var origins = new List<Vector3>();
            for (int i = 0; i < pour && i < items[source].Count; i++)
            {
                moving.Add(items[source][i]);
                origins.Add(items[source][i].rectTransform.position);
            }
            board.Tap(column);

            RectTransform parent = hud.stacks[column];
            int destCount = board.Column(column).Count;
            var starts = new List<Vector2>();
            var goals = new List<Vector2>();
            for (int i = 0; i < moving.Count; i++)
            {
                RectTransform rect = moving[i].rectTransform;
                rect.SetParent(parent, true);
                rect.SetAsLastSibling();
                goals.Add(PackHud.SlotPosition(i, destCount));
                rect.position = origins[i];
                starts.Add(rect.anchoredPosition);
            }
            float span = Mathf.Abs(column - source) * PackHud.LanePitch;
            float duration = 0.2f + span / 4200f;
            float arc = 70f + span * 0.12f;
            const float gap = 0.035f;
            float total = duration + gap * Mathf.Max(0, moving.Count - 1);
            float dir = Mathf.Sign(column - source);
            var landed = new bool[moving.Count];
            float t = 0f;
            while (t < total)
            {
                t += Time.deltaTime;
                for (int i = moving.Count - 1; i >= 0; i--)
                {
                    float u = (t - (moving.Count - 1 - i) * gap) / duration;
                    if (u < 0f) continue;
                    RectTransform rect = moving[i].rectTransform;
                    if (u >= 1f)
                    {
                        if (!landed[i])
                        {
                            landed[i] = true;
                            rect.anchoredPosition = goals[i];
                            rect.localRotation = Quaternion.identity;
                            StartCoroutine(Squish(rect));
                        }
                        continue;
                    }
                    float e = Ease.OutCubic(u);
                    Vector2 pos = Vector2.Lerp(starts[i], goals[i], e);
                    pos.y += Mathf.Sin(u * Mathf.PI) * arc;
                    rect.anchoredPosition = pos;
                    rect.localRotation = Quaternion.Euler(0f, 0f, -dir * Mathf.Sin(u * Mathf.PI) * 14f);
                    rect.localScale = Vector3.one * (1.08f + Mathf.Sin(u * Mathf.PI) * 0.06f);
                }
                yield return null;
            }
            for (int i = 0; i < moving.Count; i++)
            {
                if (landed[i]) continue;
                moving[i].rectTransform.anchoredPosition = goals[i];
                moving[i].rectTransform.localRotation = Quaternion.identity;
                StartCoroutine(Squish(moving[i].rectTransform));
            }
            Sfx.Play(SfxId.Land);
            WxBridge.Vibrate(WxBridge.Buzz.Light);
            fx.Puff(moving[moving.Count - 1].rectTransform.position + new Vector3(0f, -PackHud.Item * 0.4f, 0f), 90f);
            fx.Shake(hud.lanes[column].rectTransform, 4f, 0.12f);
            yield return new WaitForSeconds(0.1f);

            Refresh();
            yield return PlayFlips(snap, source, column, pour);
            Busy = false;
        }

        bool[][] SnapshotRevealed()
        {
            var snap = new bool[PackBoard.ColumnCount][];
            for (int c = 0; c < snap.Length; c++)
            {
                IReadOnlyList<PackBoard.Cell> column = board.Column(c);
                snap[c] = new bool[column.Count];
                for (int i = 0; i < column.Count; i++)
                    snap[c][i] = column[i].Revealed;
            }
            return snap;
        }

        IEnumerator PlayFlips(bool[][] snap, int source, int dest, int pour)
        {
            var popping = new List<RectTransform>();
            CollectFlips(popping, source, pour, snap);
            CollectFlips(popping, dest, -pour, snap);
            if (popping.Count == 0) yield break;
            Sfx.Play(SfxId.Cover);
            for (int i = 0; i < popping.Count; i++)
                fx.Sparkles(popping[i].position, 4, 220f, 36f);
            yield return Flip(popping);
        }

        void CollectFlips(List<RectTransform> popping, int column, int shift, bool[][] snap)
        {
            if (column < 0 || column >= items.Length || snap[column] == null) return;
            IReadOnlyList<PackBoard.Cell> cells = board.Column(column);
            for (int i = 0; i < cells.Count && i < items[column].Count; i++)
            {
                int oldIndex = i + shift;
                if (oldIndex < 0 || oldIndex >= snap[column].Length) continue;
                if (snap[column][oldIndex] || !cells[i].Revealed) continue;
                if (items[column][i] != null) popping.Add(items[column][i].rectTransform);
            }
        }

        static IEnumerator Flip(List<RectTransform> popping)
        {
            float t = 0f;
            const float duration = 0.22f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float sx = k < 0.7f ? Mathf.Lerp(0.05f, 1.15f, k / 0.7f) : Mathf.Lerp(1.15f, 1f, (k - 0.7f) / 0.3f);
                for (int i = 0; i < popping.Count; i++)
                {
                    if (popping[i] != null) popping[i].localScale = new Vector3(sx, 1f, 1f);
                }
                yield return null;
            }
            for (int i = 0; i < popping.Count; i++)
            {
                if (popping[i] != null) popping[i].localScale = Vector3.one;
            }
        }

        static IEnumerator Squish(RectTransform rect)
        {
            float t = 0f;
            const float duration = 0.2f;
            while (t < duration && rect != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float y = 1f - Ease.Spring(k, 2.2f, 4f) * 0.22f;
                float x = 1f + (1f - y) * 0.6f;
                rect.localScale = new Vector3(x, y, 1f);
                yield return null;
            }
            if (rect != null) rect.localScale = Vector3.one;
        }

        // ------------------------------------------------------------ 排满、解锁

        IEnumerator PopReady(int column, int streak)
        {
            RectTransform lane = hud.lanes[column].rectTransform;
            Sfx.PlayClear(streak);
            WxBridge.Vibrate(WxBridge.Buzz.Medium);
            Vector3 top = lane.TransformPoint(new Vector3(0f, lane.rect.height * 0.5f - 60f, 0f));
            fx.Flash(lane.position, 420f, new Color(0.7f, 1f, 0.7f, 0.65f));
            fx.RingWave(top, 220f, new Color(0.5f, 0.95f, 0.5f, 1f));
            fx.Sparkles(top, 10, 420f);
            fx.FloatWord(top + new Vector3(0f, 40f, 0f), streak >= 2 ? "连排" + streak + "列！" : "排满啦！",
                new Color32(0x4C, 0xC2, 0x5A, 0xFF), 44);

            Image mark = hud.readyMarks[column];
            if (mark != null) mark.rectTransform.localScale = Vector3.zero;
            List<Image> stack = items[column];
            int n = stack.Count;
            float t = 0f;
            const float duration = 0.6f;
            while (t < duration)
            {
                t += Time.deltaTime;
                for (int i = 0; i < n && i < stack.Count; i++)
                {
                    if (stack[i] == null) continue;
                    float local = (t - (n - 1 - i) * 0.035f) / 0.22f;
                    float hop = local > 0f && local < 1f ? Mathf.Sin(local * Mathf.PI) * 26f : 0f;
                    stack[i].rectTransform.anchoredPosition = PackHud.SlotPosition(i, n) + new Vector2(0f, hop);
                }
                if (mark != null)
                {
                    float m = Mathf.Clamp01((t - 0.25f) / 0.3f);
                    mark.rectTransform.localScale = Vector3.one * Mathf.Max(0.01f, Ease.OutBack(m, 2.6f));
                }
                lane.localScale = new Vector3(1f, 1f + Mathf.Sin(Mathf.Clamp01(t / 0.3f) * Mathf.PI) * 0.03f, 1f);
                yield return null;
            }
            lane.localScale = Vector3.one;
            if (mark != null) mark.rectTransform.localScale = Vector3.one;
            for (int i = 0; i < n && i < stack.Count; i++)
            {
                if (stack[i] != null) stack[i].rectTransform.anchoredPosition = PackHud.SlotPosition(i, n);
            }
        }

        IEnumerator PopUnlock(int column)
        {
            RectTransform lane = hud.lanes[column].rectTransform;
            Sfx.Play(SfxId.Unlock);
            fx.Burst(lane.position, 300f);
            fx.Sparkles(lane.position, 12, 480f);
            fx.FloatWord(lane.position, "新衣架", new Color32(0xFF, 0x9E, 0x3D, 0xFF), 44);
            float t = 0f;
            const float duration = 0.4f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                lane.localScale = Vector3.one * (1f + Ease.Spring(k, 2f, 4f) * -0.12f);
                yield return null;
            }
            lane.localScale = Vector3.one;
        }

        // ------------------------------------------------------------ 上车

        /// <summary>
        /// 排满的列一列列飞进车窗。坐满一辆时先开走，onCarDone 里弹奖励，回来后开进下一辆。
        /// </summary>
        public IEnumerator PlayBoard(List<PackBoard.Boarding> list, System.Func<int, IEnumerator> onCarDone)
        {
            if (list == null || list.Count == 0) yield break;
            Busy = true;
            for (int i = 0; i < list.Count; i++)
            {
                PackBoard.Boarding b = list[i];
                yield return FlyToSeat(b, i);
                hud.SetProgress(b.CarDone ? b.Car + 1 : b.Car, b.CarDone ? 0 : b.Seat + 1, PackBoard.SeatsOf(b.Car));
                if (!b.CarDone) continue;

                yield return new WaitForSeconds(0.25f);
                yield return DriveAway(b.Car);
                if (onCarDone != null) yield return onCarDone(b.Car);
                if (b.Won) break;
                hud.ShowCar(b.Car + 1);
                yield return DriveIn();
                Refresh();
            }
            readyStreak = 0;
            Refresh();
            Busy = false;
        }

        IEnumerator FlyToSeat(PackBoard.Boarding b, int order)
        {
            int c = b.Column;
            if (hud.readyMarks[c] != null) hud.readyMarks[c].enabled = false;
            var flying = new List<Image>(items[c]);
            items[c].Clear();
            Sprite icon = SpriteFor(b.Type);
            RectTransform seat = hud.SeatRect(b.Seat);
            Vector3 target = hud.SeatWorld(b.Seat);
            RectTransform layer = fx.Layer;
            var starts = new List<Vector2>();
            for (int i = 0; i < flying.Count; i++)
            {
                RectTransform rect = flying[i].rectTransform;
                rect.SetParent(layer, true);
                rect.SetAsLastSibling();
                starts.Add(rect.anchoredPosition);
            }
            Vector2 goal = layer.InverseTransformPoint(target);
            hud.PaintLane(c, true, false, false, false, null);
            Sfx.Play(SfxId.Whoosh);
            fx.Flash(hud.lanes[c].rectTransform.position, 380f, new Color(1f, 1f, 0.85f, 0.6f), 0.22f);

            const float flight = 0.34f;
            const float gap = 0.04f;
            float total = flight + gap * Mathf.Max(0, flying.Count - 1);
            float t = 0f;
            bool stamped = false;
            var arrived = new bool[flying.Count];
            while (t < total || !stamped)
            {
                t += Time.deltaTime;
                for (int i = 0; i < flying.Count; i++)
                {
                    if (arrived[i] || flying[i] == null) continue;
                    float u = (t - i * gap) / flight;
                    if (u < 0f) continue;
                    RectTransform rect = flying[i].rectTransform;
                    if (u >= 1f)
                    {
                        arrived[i] = true;
                        rect.gameObject.SetActive(false);
                        if (!stamped)
                        {
                            stamped = true;
                            hud.SetSeat(b.Seat, icon);
                            Sfx.Play(SfxId.Seat);
                            WxBridge.Vibrate(WxBridge.Buzz.Medium);
                            fx.Burst(target, 200f);
                            fx.Sparkles(target, 10, 380f, 44f);
                            fx.FloatWord(target + new Vector3(0f, 60f, 0f), "上车啦", new Color32(0xFF, 0x7A, 0x9A, 0xFF), 40);
                            if (seat != null) StartCoroutine(Pop(seat, 1.35f));
                            StartCoroutine(CarBump());
                        }
                        continue;
                    }
                    float e = u * u * (3f - 2f * u);
                    Vector2 pos = Vector2.Lerp(starts[i], goal, e);
                    pos.y += Mathf.Sin(u * Mathf.PI) * 160f;
                    rect.anchoredPosition = pos;
                    rect.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.45f, e);
                    rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(u * Mathf.PI) * 20f);
                }
                if (flying.Count == 0 && !stamped)
                {
                    stamped = true;
                    hud.SetSeat(b.Seat, icon);
                }
                yield return null;
            }
            for (int i = flying.Count - 1; i >= 0; i--)
                UiKit.Discard(flying[i]);
            laneWasReady[c] = false;
        }

        static IEnumerator Pop(RectTransform rect, float peak)
        {
            float t = 0f;
            const float duration = 0.3f;
            while (t < duration && rect != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                rect.localScale = Vector3.one * (1f + (peak - 1f) * Ease.Spring(k, 1.6f, 3.5f) * (1f - k * 0.2f));
                yield return null;
            }
            if (rect != null) rect.localScale = Vector3.one;
        }

        IEnumerator CarBump()
        {
            RectTransform car = hud.carRoot;
            float t = 0f;
            const float duration = 0.32f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float s = Ease.Spring(k, 2.4f, 5f) * 0.07f;
                car.localScale = new Vector3(1f + s, 1f - s, 1f);
                yield return null;
            }
            car.localScale = Vector3.one;
        }

        IEnumerator DriveAway(int car)
        {
            RectTransform root = hud.carRoot;
            Sfx.Play(SfxId.CarDepart);
            WxBridge.Vibrate(WxBridge.Buzz.Heavy);
            fx.FloatWord(root.position + new Vector3(0f, root.rect.height * 0.9f, 0f), "满员发车！",
                new Color32(0xFF, 0x5D, 0x73, 0xFF), 56);
            fx.Confetti(root.position + new Vector3(0f, root.rect.height * 0.6f, 0f), 26, 1300f, 0.8f);
            Vector2 home = root.anchoredPosition;
            float t = 0f;
            while (t < 0.28f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.28f);
                float s = Mathf.Sin(k * Mathf.PI) * 0.08f;
                root.localScale = new Vector3(1f - s * 0.5f, 1f + s, 1f);
                root.anchoredPosition = home + new Vector2(Mathf.Sin(k * Mathf.PI * 6f) * 4f, 0f);
                yield return null;
            }
            root.localScale = Vector3.one;
            float away = -(ScreenFit.DesignW * 0.5f + hud.CarWidth * 0.6f + 80f);
            t = 0f;
            float puffEvery = 0.06f, nextPuff = 0f;
            const float duration = 0.75f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float e = k * k * k;
                root.anchoredPosition = new Vector2(Mathf.Lerp(home.x, away, e), home.y + Mathf.Sin(k * 30f) * 2f);
                root.localRotation = Quaternion.Euler(0f, 0f, -2.5f * (1f - k));
                if (t >= nextPuff)
                {
                    nextPuff += puffEvery;
                    Vector3 rear = root.TransformPoint(new Vector3(root.rect.width * 0.5f, root.rect.height * 0.12f, 0f));
                    fx.Puff(rear, 110f);
                }
                yield return null;
            }
            root.localRotation = Quaternion.identity;
            root.anchoredPosition = new Vector2(away, home.y);
            RectTransform node = hud.Node(car);
            if (node != null)
            {
                Sfx.Play(SfxId.GiftPop);
                fx.Burst(node.position, 180f);
                fx.Sparkles(node.position, 8, 320f, 40f);
                StartCoroutine(Pop(node, 1.5f));
            }
            yield return new WaitForSeconds(0.25f);
        }

        IEnumerator DriveIn()
        {
            RectTransform root = hud.carRoot;
            float y = root.anchoredPosition.y;
            float from = ScreenFit.DesignW * 0.5f + hud.CarWidth * 0.6f + 80f;
            root.anchoredPosition = new Vector2(from, y);
            for (int i = 0; i < hud.seats.Length; i++)
            {
                if (hud.seats[i] != null) hud.seats[i].rectTransform.localScale = Vector3.zero;
            }
            Sfx.Play(SfxId.CarArrive);
            float t = 0f;
            const float duration = 0.6f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                root.anchoredPosition = new Vector2(Mathf.LerpUnclamped(from, 0f, Ease.OutBack(k, 1.1f)), y);
                root.localRotation = Quaternion.Euler(0f, 0f, (1f - k) * 3f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            root.anchoredPosition = new Vector2(0f, y);
            root.localRotation = Quaternion.identity;
            yield return CarBump();
            int seats = PackBoard.SeatsOf(board.Car);
            for (int i = 0; i < seats && i < hud.seats.Length; i++)
            {
                RectTransform seat = hud.seats[i].rectTransform;
                StartCoroutine(PopFromZero(seat, i * 0.04f));
            }
            yield return new WaitForSeconds(0.04f * seats + 0.15f);
        }

        static IEnumerator PopFromZero(RectTransform rect, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            float t = 0f;
            while (t < 0.26f && rect != null)
            {
                t += Time.deltaTime;
                rect.localScale = Vector3.one * Mathf.Max(0.01f, Ease.OutBack(t / 0.26f, 2.4f));
                yield return null;
            }
            if (rect != null) rect.localScale = Vector3.one;
        }

        /// <summary>开局：车开进来，场上的衣服从门里一件件弹进列。</summary>
        public IEnumerator PlayIntro()
        {
            Busy = true;
            for (int c = 0; c < items.Length; c++)
                ClearStack(c);
            PaintLanes(false);
            yield return DriveIn();
            var counts = new int[PackBoard.ColumnCount];
            // 列与列交错着落，看起来像一把撒下去。
            var order = new List<PackBoard.Drop>();
            bool any = true;
            for (int depth = 0; any; depth++)
            {
                any = false;
                for (int c = 0; c < PackBoard.ColumnCount; c++)
                {
                    IReadOnlyList<PackBoard.Cell> column = board.Column(c);
                    int row = column.Count - 1 - depth;
                    if (row < 0) continue;
                    any = true;
                    order.Add(new PackBoard.Drop { Column = c, Type = column[row].Type });
                }
            }
            yield return Rain(order, counts, 0.035f);
            Refresh();
            Busy = false;
        }

        // ------------------------------------------------------------ 补充、收回

        public IEnumerator PlayRefill(List<PackBoard.Drop> drops)
        {
            if (drops == null || drops.Count == 0) yield break;
            Busy = true;
            SettleAll();
            var counts = new int[PackBoard.ColumnCount];
            for (int c = 0; c < counts.Length; c++)
                counts[c] = items[c].Count;
            Sfx.Play(SfxId.Gate);
            StartCoroutine(Wobble(hud.washer != null ? hud.washer.rectTransform : null, 0.6f));
            yield return new WaitForSeconds(0.12f);
            yield return Rain(drops, counts, 0.06f);
            Refresh();
            var flips = new List<RectTransform>();
            for (int c = 0; c < PackBoard.ColumnCount; c++)
            {
                IReadOnlyList<PackBoard.Cell> column = board.Column(c);
                int added = CountAdded(drops, c);
                for (int row = 0; row < added && row < items[c].Count; row++)
                {
                    if (column[row].Revealed && items[c][row] != null)
                        flips.Add(items[c][row].rectTransform);
                }
            }
            if (flips.Count > 0)
            {
                Sfx.Play(SfxId.Cover);
                yield return Flip(flips);
            }
            Busy = false;
        }

        static int CountAdded(List<PackBoard.Drop> drops, int column)
        {
            int n = 0;
            for (int i = 0; i < drops.Count; i++)
            {
                if (drops[i].Column == column) n++;
            }
            return n;
        }

        void SettleAll()
        {
            for (int c = 0; c < items.Length; c++)
                SettleColumn(c);
        }

        /// <summary>按顺序从门口弹出，先往上抛再落进列顶，落地小弹一下。counts 是每列现有件数，会被改写。</summary>
        IEnumerator Rain(List<PackBoard.Drop> drops, int[] counts, float interval)
        {
            RectTransform layer = fx.Layer;
            Vector2 mouth = layer.InverseTransformPoint(hud.GateMouth());
            var flyers = new List<RectTransform>();
            var goals = new List<Vector2>();
            var columns = new List<int>();
            var rows = new List<int>();
            for (int i = 0; i < drops.Count; i++)
            {
                int c = drops[i].Column;
                int fromBottom = counts[c]++;
                Vector2 goal = layer.InverseTransformPoint(hud.SlotWorld(c, fromBottom));
                Image img = UiKit.Icon(layer, "Drop", hud.Mystery, new Vector2(0.5f, 0.5f), mouth,
                    new Vector2(PackHud.Item, PackHud.Item));
                img.rectTransform.localScale = Vector3.zero;
                flyers.Add(img.rectTransform);
                goals.Add(goal);
                columns.Add(c);
                rows.Add(fromBottom);
            }

            const float flight = 0.42f;
            const float bounce = 0.14f;
            float total = interval * Mathf.Max(0, drops.Count - 1) + flight + bounce;
            var landed = new bool[drops.Count];
            var fed = 0;
            float t = 0f;
            while (t < total)
            {
                t += Time.deltaTime;
                while (fed < drops.Count && t >= fed * interval)
                {
                    if (fed % 3 == 0) StartCoroutine(hud.FeedQueue(Mathf.Min(0.16f, interval * 2.5f)));
                    fed++;
                }
                for (int i = 0; i < flyers.Count; i++)
                {
                    float u = (t - i * interval) / flight;
                    if (u < 0f) continue;
                    RectTransform rect = flyers[i];
                    if (u < 1f)
                    {
                        // 往上抛一点再落：横向匀速，竖向抛物线
                        Vector2 a = mouth;
                        Vector2 b = goals[i];
                        float x = Mathf.Lerp(a.x, b.x, u);
                        float apex = Mathf.Max(a.y, b.y) + 120f;
                        float y = (1f - u) * (1f - u) * a.y + 2f * (1f - u) * u * apex + u * u * b.y;
                        rect.anchoredPosition = new Vector2(x, y);
                        rect.localScale = Vector3.one * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(u * 2f));
                        rect.localRotation = Quaternion.Euler(0f, 0f, (1f - u) * 360f * (i % 2 == 0 ? 1f : -1f));
                        continue;
                    }
                    if (!landed[i])
                    {
                        landed[i] = true;
                        rect.localRotation = Quaternion.identity;
                        if (i % 2 == 0 || i == flyers.Count - 1) Sfx.Play(SfxId.Hop);
                        if (i % 4 == 0) fx.Puff(rect.position + new Vector3(0f, -PackHud.Item * 0.4f, 0f), 70f);
                    }
                    float v = Mathf.Clamp01((u - 1f) * flight / bounce);
                    float hop = Mathf.Sin(v * Mathf.PI) * 18f;
                    rect.anchoredPosition = goals[i] + new Vector2(0f, hop);
                    float sq = 1f - Mathf.Sin(v * Mathf.PI) * 0.12f;
                    rect.localScale = new Vector3(2f - sq, sq, 1f);
                }
                yield return null;
            }
            for (int i = flyers.Count - 1; i >= 0; i--)
                UiKit.Discard(flyers[i]);
        }

        static IEnumerator Wobble(RectTransform rect, float duration)
        {
            if (rect == null) yield break;
            Vector2 home = rect.anchoredPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = 1f - t / duration;
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 50f) * 4f * k);
                rect.anchoredPosition = home + new Vector2(Mathf.Sin(t * 70f) * 4f * k, 0f);
                yield return null;
            }
            rect.localRotation = Quaternion.identity;
            rect.anchoredPosition = home;
        }

        /// <summary>看广告收回：顶上的衣服飞回门里。</summary>
        public IEnumerator PlayRevive(List<PackBoard.Drop> taken)
        {
            Busy = true;
            RectTransform layer = fx.Layer;
            Vector2 mouth = layer.InverseTransformPoint(hud.GateMouth());
            var flyers = new List<RectTransform>();
            var starts = new List<Vector2>();
            var used = new int[PackBoard.ColumnCount];
            for (int i = 0; i < taken.Count; i++)
            {
                int c = taken[i].Column;
                int row = used[c]++;
                if (row >= items[c].Count || items[c][row] == null) continue;
                RectTransform rect = items[c][row].rectTransform;
                rect.SetParent(layer, true);
                flyers.Add(rect);
                starts.Add(rect.anchoredPosition);
            }
            Sfx.Play(SfxId.Whoosh);
            StartCoroutine(Wobble(hud.washer != null ? hud.washer.rectTransform : null, 0.5f));
            float t = 0f;
            const float flight = 0.4f;
            const float gap = 0.03f;
            float total = flight + gap * Mathf.Max(0, flyers.Count - 1);
            while (t < total)
            {
                t += Time.deltaTime;
                for (int i = 0; i < flyers.Count; i++)
                {
                    float u = Mathf.Clamp01((t - i * gap) / flight);
                    float e = u * u;
                    Vector2 pos = Vector2.Lerp(starts[i], mouth, e);
                    pos.y += Mathf.Sin(u * Mathf.PI) * 100f;
                    flyers[i].anchoredPosition = pos;
                    flyers[i].localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, e);
                }
                yield return null;
            }
            for (int i = flyers.Count - 1; i >= 0; i--)
                UiKit.Discard(flyers[i]);
            Sfx.Play(SfxId.Gate);
            Refresh();
            Busy = false;
        }

        /// <summary>全部送达：彩纸雨 + 横幅。</summary>
        public IEnumerator PlayFinale()
        {
            Busy = true;
            Sfx.Play(SfxId.Finale);
            StartCoroutine(fx.ConfettiRain(80, 1.6f));
            yield return fx.Banner("全部送达！", 1.1f);
            fx.ClearBanners();
            Busy = false;
        }
    }
}

