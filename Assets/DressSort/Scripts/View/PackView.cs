using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 把 PackBoard 画进装箱页的六列里。先点的一列会把最上面的同款微微抬起，再点另一列就倒过去。
    /// </summary>
    public class PackView : MonoBehaviour
    {
        const float Item = 134f;
        const float Step = 78f;
        const float Pad = 22f;

        PackHud hud;
        PackBoard board;
        ItemDef[] palette;
        Sprite mystery;
        readonly List<Image>[] items = new List<Image>[PackBoard.ColumnCount];
        readonly bool[] laneWasReady = new bool[PackBoard.ColumnCount];
        readonly bool[] laneWasOpen = new bool[PackBoard.ColumnCount];
        bool paintedOnce;

        public bool Busy { get; private set; }

        /// <summary>这次点击没倒成时的提示。空位不够整组同色时会写上。</summary>
        public string Blocked { get; private set; }

        public static PackView Attach(PackHud hud)
        {
            var view = hud.gameObject.AddComponent<PackView>();
            view.hud = hud;
            for (int i = 0; i < view.items.Length; i++)
                view.items[i] = new List<Image>();
            return view;
        }

        public void Bind(PackBoard next, ItemDef[] dresses, Sprite mysterySprite)
        {
            board = next;
            palette = dresses;
            mystery = mysterySprite;
            paintedOnce = false;
            for (int i = 0; i < laneWasReady.Length; i++)
            {
                laneWasReady[i] = false;
                laneWasOpen[i] = false;
            }
            Refresh();
        }

        void Update()
        {
            if (board == null || Busy) return;
            ApplyLift();
        }

        public void Refresh()
        {
            if (board == null || hud == null) return;
            for (int c = 0; c < items.Length; c++)
            {
                // 补充时飞进来的那件不在列表里，只清列表会留在列顶。点选时它不动，下面那截反而抬起来。
                RectTransform stack = hud.stacks[c];
                if (stack != null)
                {
                    for (int i = stack.childCount - 1; i >= 0; i--)
                        UiKit.Discard(stack.GetChild(i));
                }
                items[c].Clear();
                if (!board.IsOpen(c)) continue;
                IReadOnlyList<PackBoard.Cell> column = board.Column(c);
                for (int row = 0; row < column.Count; row++)
                    items[c].Add(Spawn(c, row, column[row]));
            }
            PaintLanes();
            ApplyLift();
        }

        public IEnumerator PlayMove(int column)
        {
            Blocked = null;
            if (board == null || Busy) yield break;
            int source = board.Selected;
            int pour = source >= 0 ? board.PourCount(source, column) : 0;
            if (pour <= 0 || source >= items.Length || items[source].Count < pour)
            {
                if (source >= 0 && board.TopsMatch(source, column))
                {
                    Blocked = "这一列已经满了";
                    Sfx.Play(SfxId.Deny);
                    Busy = true;
                    if (column >= 0 && column < hud.laneFaces.Length && hud.laneFaces[column] != null)
                        yield return Shake(hud.laneFaces[column].rectTransform);
                    Busy = false;
                    yield break;
                }
                board.Tap(column);
                Sfx.Play(SfxId.Lift);
                Refresh();
                yield break;
            }

            Busy = true;
            Sfx.Play(SfxId.Lift);
            bool[][] snap = SnapshotRevealed();
            var moving = new List<Image>();
            var origins = new List<Vector3>();
            for (int i = 0; i < pour; i++)
            {
                Image image = items[source][i];
                if (image == null) continue;
                moving.Add(image);
                origins.Add(image.rectTransform.position);
            }

            PackBoard.TapResult result = board.Tap(column);
            if (result != PackBoard.TapResult.Moved || moving.Count == 0)
            {
                Refresh();
                Busy = false;
                yield break;
            }

            RectTransform parent = hud.stacks[column];
            var starts = new List<Vector2>();
            var goals = new List<Vector2>();
            int destCount = board.Column(column).Count;
            for (int i = 0; i < moving.Count; i++)
            {
                Image image = moving[i];
                image.rectTransform.SetParent(parent, true);
                Place(image.rectTransform, i, destCount);
                goals.Add(image.rectTransform.anchoredPosition);
                image.rectTransform.position = origins[i];
                starts.Add(image.rectTransform.anchoredPosition);
            }

            float duration = 0.2f;
            float gap = 0.028f;
            float total = duration + gap * Mathf.Max(0, moving.Count - 1);
            float t = 0f;
            while (t < total)
            {
                t += Time.deltaTime;
                for (int i = 0; i < moving.Count; i++)
                {
                    if (moving[i] == null) continue;
                    float u = (t - i * gap) / duration;
                    if (u < 0f) continue;
                    float e = Ease(u);
                    Vector2 pos = Vector2.Lerp(starts[i], goals[i], e);
                    pos.y += Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI) * 72f;
                    moving[i].rectTransform.anchoredPosition = pos;
                }
                yield return null;
            }
            for (int i = 0; i < moving.Count; i++)
            {
                if (moving[i] == null) continue;
                moving[i].rectTransform.anchoredPosition = goals[i];
            }
            yield return Squash(moving);
            Sfx.Play(SfxId.Place);
            Refresh();
            yield return PlayFlips(snap, source, column, pour);
            Busy = false;
        }

        public IEnumerator PlayRefill()
        {
            if (board == null || Busy) yield break;
            Busy = true;
            var before = new int[PackBoard.ColumnCount];
            for (int c = 0; c < before.Length; c++)
                before[c] = board.IsOpen(c) ? board.Column(c).Count : 0;

            if (board.Refill() <= 0)
            {
                Busy = false;
                yield break;
            }

            var flyers = new List<Image>();
            var faces = new List<Sprite>();
            var showFace = new List<bool>();
            var starts = new List<Vector2>();
            var goals = new List<Vector2>();
            Vector3 origin = hud.PileOrigin();
            for (int c = 0; c < before.Length; c++)
            {
                int added = board.IsOpen(c) ? board.Column(c).Count - before[c] : 0;
                if (added <= 0) continue;
                int total = board.Column(c).Count;
                for (int i = 0; i < items[c].Count; i++)
                {
                    if (items[c][i] == null) continue;
                    Place(items[c][i].rectTransform, i + added, total);
                }
                // 先落底下那件，后落顶上那件。飞的时候先盖着问号。
                for (int row = added - 1; row >= 0; row--)
                {
                    PackBoard.Cell cell = board.Column(c)[row];
                    Image flyer = Spawn(c, row, cell);
                    items[c].Insert(0, flyer);
                    Sprite face = flyer.sprite;
                    if (mystery != null)
                        flyer.sprite = mystery;
                    RectTransform rect = flyer.rectTransform;
                    Vector2 goal = rect.anchoredPosition;
                    rect.position = origin;
                    flyers.Add(flyer);
                    faces.Add(face);
                    showFace.Add(cell.Revealed && mystery != null && face != mystery);
                    goals.Add(goal);
                    starts.Add(rect.anchoredPosition);
                }
            }

            const float duration = 0.22f;
            const float gap = 0.045f;
            float totalTime = duration + gap * Mathf.Max(0, flyers.Count - 1);
            float t = 0f;
            while (t < totalTime)
            {
                t += Time.deltaTime;
                for (int i = 0; i < flyers.Count; i++)
                {
                    if (flyers[i] == null) continue;
                    float u = (t - i * gap) / duration;
                    if (u < 0f) continue;
                    float e = Ease(Mathf.Clamp01(u));
                    Vector2 pos = Vector2.Lerp(starts[i], goals[i], e);
                    pos.y += Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI) * 100f;
                    flyers[i].rectTransform.anchoredPosition = pos;
                    flyers[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(0.62f, 1f, e);
                }
                yield return null;
            }
            for (int i = 0; i < flyers.Count; i++)
            {
                if (flyers[i] == null) continue;
                flyers[i].rectTransform.anchoredPosition = goals[i];
                flyers[i].rectTransform.localScale = Vector3.one;
            }
            Sfx.Play(SfxId.Place);
            bool turning = false;
            for (int i = 0; i < showFace.Count; i++)
            {
                if (showFace[i]) turning = true;
            }
            if (turning)
            {
                float spin = 0f;
                const float spinDuration = 0.16f;
                var swapped = new bool[flyers.Count];
                while (spin < spinDuration)
                {
                    spin += Time.deltaTime;
                    float k = Mathf.Clamp01(spin / spinDuration);
                    for (int i = 0; i < flyers.Count; i++)
                    {
                        if (!showFace[i] || flyers[i] == null) continue;
                        if (!swapped[i] && k >= 0.5f)
                        {
                            flyers[i].sprite = faces[i];
                            swapped[i] = true;
                        }
                        float x = Mathf.Abs(Mathf.Cos(k * Mathf.PI));
                        flyers[i].rectTransform.localScale = new Vector3(Mathf.Max(0.05f, x), 1f, 1f);
                    }
                    yield return null;
                }
                for (int i = 0; i < flyers.Count; i++)
                {
                    if (!showFace[i] || flyers[i] == null) continue;
                    flyers[i].sprite = faces[i];
                    flyers[i].rectTransform.localScale = Vector3.one;
                }
            }

            Refresh();
            Busy = false;
        }

        public IEnumerator PlayPack(PackBoard.PackResult packed, int slot, Sprite icon)
        {
            Busy = true;
            Sfx.Play(SfxId.Pack);
            if (hud.readyMarks != null && packed.Column >= 0 && packed.Column < hud.readyMarks.Length
                && hud.readyMarks[packed.Column] != null)
                hud.readyMarks[packed.Column].enabled = false;

            var flying = new List<Image>();
            if (packed.Column >= 0 && packed.Column < items.Length)
            {
                for (int i = 0; i < items[packed.Column].Count; i++)
                {
                    if (items[packed.Column][i] != null)
                        flying.Add(items[packed.Column][i]);
                }
            }

            Vector3 target = hud.BinAnchor(slot);
            var starts = new List<Vector2>();
            var goals = new List<Vector2>();
            for (int i = 0; i < flying.Count; i++)
            {
                RectTransform rect = flying[i].rectTransform;
                rect.SetParent(hud.transform, true);
                rect.SetAsLastSibling();
                starts.Add(rect.anchoredPosition);
                Vector3 saved = rect.position;
                rect.position = target;
                goals.Add(rect.anchoredPosition);
                rect.position = saved;
            }

            bool stamped = false;
            float flight = 0.2f;
            float gap = 0.05f;
            float total = flying.Count == 0 ? 0f : flight + gap * (flying.Count - 1);
            var arrived = new bool[flying.Count];
            float t = 0f;
            while (t < total)
            {
                t += Time.deltaTime;
                for (int i = 0; i < flying.Count; i++)
                {
                    if (flying[i] == null || arrived[i]) continue;
                    float u = (t - i * gap) / flight;
                    if (u < 0f) continue;
                    if (u >= 1f)
                    {
                        arrived[i] = true;
                        flying[i].rectTransform.localScale = Vector3.zero;
                        if (!stamped)
                        {
                            hud.SetBoxSlot(slot, icon);
                            ShowPackingCount(packed);
                            stamped = true;
                        }
                        hud.NudgeBin(slot);
                        Sfx.Play(SfxId.Place);
                        continue;
                    }
                    float e = Ease(u);
                    Vector2 pos = Vector2.Lerp(starts[i], goals[i], e);
                    pos.y += Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI) * 120f;
                    flying[i].rectTransform.anchoredPosition = pos;
                    float shrink = u < 0.55f ? 1f : Mathf.Lerp(1f, 0.35f, (u - 0.55f) / 0.45f);
                    flying[i].rectTransform.localScale = Vector3.one * shrink;
                }
                yield return null;
            }
            for (int i = 0; i < flying.Count; i++)
            {
                if (arrived[i] || flying[i] == null) continue;
                flying[i].rectTransform.localScale = Vector3.zero;
                if (!stamped)
                {
                    hud.SetBoxSlot(slot, icon);
                    ShowPackingCount(packed);
                    stamped = true;
                }
                hud.NudgeBin(slot);
                Sfx.Play(SfxId.Place);
            }

            for (int i = flying.Count - 1; i >= 0; i--)
                UiKit.Discard(flying[i]);
            if (!stamped)
            {
                hud.SetBoxSlot(slot, icon);
                ShowPackingCount(packed);
            }

            if (packed.Shipped)
            {
                hud.SetProgress(board.BoxesDone, PackBoard.BoxesToWin, null);
                StartCoroutine(hud.PopNode(board.BoxesDone - 1));
                Sfx.Play(SfxId.GiftPop);
                yield return new WaitForSeconds(0.12f);
                yield return hud.CelebrateBins();
                yield return new WaitForSeconds(0.18f);
                if (!packed.Won)
                {
                    hud.ClearBox();
                    hud.LayoutBins(board.CurrentSeats);
                    hud.SetBoxCount(0, board.CurrentSeats, board.BoxesDone, false);
                    yield return hud.IntroBins();
                }
            }
            Refresh();
            Busy = false;
        }

        void ShowPackingCount(PackBoard.PackResult packed)
        {
            int boxIndex = packed.Shipped ? board.BoxesDone - 1 : board.BoxesDone;
            if (boxIndex < 0) boxIndex = 0;
            int seats = PackBoard.SeatsOf(boxIndex);
            int filled = packed.Shipped ? seats : board.BoxFilled;
            hud.SetBoxCount(filled, seats, boxIndex, false);
        }

        void PaintLanes()
        {
            for (int c = 0; c < PackBoard.ColumnCount; c++)
            {
                bool open = board.IsOpen(c);
                bool ready = board.IsFullUniform(c);
                bool selected = open && c == board.Selected;
                Image face = hud.laneFaces[c];
                if (face != null)
                {
                    Sprite sprite = ready && hud.laneReadySprite != null ? hud.laneReadySprite
                        : selected && hud.laneOnSprite != null ? hud.laneOnSprite
                        : hud.laneSprite;
                    if (sprite != null)
                        face.sprite = sprite;
                    face.type = Image.Type.Simple;
                    if (!open && board.IsAdColumn(c))
                        face.color = new Color(0.58f, 0.9f, 0.62f, 1f);
                    else if (!open)
                        face.color = new Color(0.78f, 0.74f, 0.76f, 1f);
                    else
                        face.color = Color.white;
                }
                if (hud.lockIcons[c] != null)
                    hud.lockIcons[c].gameObject.SetActive(!open);
                if (hud.lockLabels[c] != null)
                {
                    hud.lockLabels[c].gameObject.SetActive(!open);
                    hud.lockLabels[c].text = board.LockHint(c);
                }
                if (hud.adBadges[c] != null)
                    hud.adBadges[c].gameObject.SetActive(!open && board.IsAdColumn(c));
                if (hud.readyMarks[c] != null)
                    hud.readyMarks[c].enabled = ready;

                bool justReady = ready && !laneWasReady[c];
                bool justOpened = open && paintedOnce && !laneWasOpen[c];
                laneWasReady[c] = ready;
                laneWasOpen[c] = open;
                if (justReady)
                    StartCoroutine(PopReady(c));
                if (justOpened)
                    StartCoroutine(PopUnlock(c));
            }
            paintedOnce = true;
        }

        void ApplyLift()
        {
            int column = board.Selected;
            if (column < 0 || column >= items.Length) return;
            int count = board.GroupSize(column);
            int total = board.Column(column).Count;
            float bob = Mathf.Sin(Time.unscaledTime * 5.2f) * 6f;
            List<Image> stack = items[column];
            for (int i = 0; i < count && i < stack.Count; i++)
            {
                if (stack[i] == null) continue;
                RectTransform rect = stack[i].rectTransform;
                Place(rect, i, total);
                Vector2 pos = rect.anchoredPosition;
                pos.y += 50f + bob;
                rect.anchoredPosition = pos;
                rect.localScale = Vector3.one * 1.06f;
            }
        }

        Image Spawn(int column, int row, PackBoard.Cell cell)
        {
            Sprite sprite = cell.Revealed ? SpriteFor(cell.Type) : mystery;
            Image image = UiKit.Icon(hud.stacks[column], "Dress", sprite, new Vector2(0.5f, 1f),
                Vector2.zero, new Vector2(Item, Item));
            Image shadow = UiKit.Icon(image.transform, "Shadow", UiKit.Circle, new Vector2(0.5f, 0f),
                new Vector2(0f, 8f), new Vector2(Item * 0.62f, 16f));
            shadow.preserveAspect = false;
            shadow.color = new Color(0.45f, 0.24f, 0.2f, 0.28f);
            shadow.raycastTarget = false;
            shadow.transform.SetAsFirstSibling();
            if (board.IsFullUniform(column))
            {
                Sprite check = hud.CheckSprite;
                if (check != null)
                {
                    Image mark = UiKit.Icon(image.transform, "Ok", check, new Vector2(1f, 1f),
                        new Vector2(-6f, -6f), new Vector2(40f, 40f));
                    mark.raycastTarget = false;
                }
            }
            Place(image.rectTransform, row, board.Column(column).Count);
            image.raycastTarget = false;
            return image;
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
            float t = 0f;
            const float duration = 0.18f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float sx = k < 0.72f
                    ? Mathf.Lerp(0.05f, 1.08f, k / 0.72f)
                    : Mathf.Lerp(1.08f, 1f, (k - 0.72f) / 0.28f);
                for (int i = 0; i < popping.Count; i++)
                {
                    if (popping[i] != null)
                        popping[i].localScale = new Vector3(sx, 1f, 1f);
                }
                yield return null;
            }
            for (int i = 0; i < popping.Count; i++)
            {
                if (popping[i] != null)
                    popping[i].localScale = Vector3.one;
            }
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
                if (items[column][i] == null) continue;
                popping.Add(items[column][i].rectTransform);
            }
        }

        IEnumerator PopReady(int column)
        {
            var marks = new List<Transform>();
            if (column >= 0 && column < hud.readyMarks.Length && hud.readyMarks[column] != null)
            {
                hud.readyMarks[column].transform.localScale = Vector3.zero;
                marks.Add(hud.readyMarks[column].transform);
            }
            if (column >= 0 && column < items.Length)
            {
                for (int i = 0; i < items[column].Count; i++)
                {
                    if (items[column][i] == null) continue;
                    Transform ok = items[column][i].transform.Find("Ok");
                    if (ok == null) continue;
                    ok.localScale = Vector3.zero;
                    marks.Add(ok);
                }
            }
            RectTransform lane = column >= 0 && column < hud.laneFaces.Length && hud.laneFaces[column] != null
                ? hud.laneFaces[column].rectTransform
                : null;
            float t = 0f;
            const float duration = 0.22f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float pop = k < 0.6f
                    ? Mathf.Lerp(0.15f, 1.22f, k / 0.6f)
                    : Mathf.Lerp(1.22f, 1f, (k - 0.6f) / 0.4f);
                for (int i = 0; i < marks.Count; i++)
                {
                    if (marks[i] != null)
                        marks[i].localScale = Vector3.one * pop;
                }
                if (lane != null)
                    lane.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.04f);
                yield return null;
            }
            for (int i = 0; i < marks.Count; i++)
            {
                if (marks[i] != null)
                    marks[i].localScale = Vector3.one;
            }
            if (lane != null)
                lane.localScale = Vector3.one;
        }

        IEnumerator PopUnlock(int column)
        {
            if (column < 0 || column >= hud.laneFaces.Length || hud.laneFaces[column] == null) yield break;
            RectTransform lane = hud.laneFaces[column].rectTransform;
            Sfx.Play(SfxId.Unlock);
            float t = 0f;
            const float duration = 0.24f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float s = k < 0.55f
                    ? Mathf.Lerp(0.82f, 1.06f, k / 0.55f)
                    : Mathf.Lerp(1.06f, 1f, (k - 0.55f) / 0.45f);
                lane.localScale = Vector3.one * s;
                yield return null;
            }
            lane.localScale = Vector3.one;
        }

        IEnumerator Shake(RectTransform rect)
        {
            if (rect == null) yield break;
            Vector2 origin = rect.anchoredPosition;
            float t = 0f;
            const float duration = 0.18f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = 1f - t / duration;
                rect.anchoredPosition = origin + new Vector2(Mathf.Sin(t * 48f) * 8f * k, 0f);
                yield return null;
            }
            rect.anchoredPosition = origin;
        }

        IEnumerator Squash(List<Image> images)
        {
            float t = 0f;
            const float duration = 0.12f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float y = k < 0.45f ? Mathf.Lerp(1f, 0.78f, k / 0.45f) : Mathf.Lerp(0.78f, 1f, (k - 0.45f) / 0.55f);
                float x = 1f + (1f - y) * 0.45f;
                for (int i = 0; i < images.Count; i++)
                {
                    if (images[i] != null)
                        images[i].rectTransform.localScale = new Vector3(x, y, 1f);
                }
                yield return null;
            }
            for (int i = 0; i < images.Count; i++)
            {
                if (images[i] != null)
                    images[i].rectTransform.localScale = Vector3.one;
            }
        }

        IEnumerator FlipCard(Image image, Sprite face)
        {
            if (image == null) yield break;
            RectTransform rect = image.rectTransform;
            float t = 0f;
            bool swapped = false;
            const float duration = 0.16f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                if (!swapped && k >= 0.5f)
                {
                    image.sprite = face;
                    swapped = true;
                }
                float x = Mathf.Abs(Mathf.Cos(k * Mathf.PI));
                rect.localScale = new Vector3(Mathf.Max(0.05f, x), 1f, 1f);
                yield return null;
            }
            image.sprite = face;
            rect.localScale = Vector3.one;
        }

        static float Ease(float k)
        {
            k = Mathf.Clamp01(k);
            return 1f - Mathf.Pow(1f - k, 3f);
        }

        void Place(RectTransform rect, int row, int count)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Item, Item);
            int fromBottom = Mathf.Max(0, count - 1 - row);
            rect.anchoredPosition = new Vector2(0f, Pad + Item * 0.5f + fromBottom * Step);
            rect.localScale = Vector3.one;
        }

        Sprite SpriteFor(int type)
        {
            if (palette == null || type < 0 || type >= palette.Length || palette[type] == null)
                return mystery;
            return palette[type].ResolveIcon();
        }
    }
}
