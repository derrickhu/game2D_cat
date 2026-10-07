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
            Refresh();
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
            LiftSelection();
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
                    yield break;
                }
                board.Tap(column);
                Sfx.Play(SfxId.Lift);
                Refresh();
                yield break;
            }

            Busy = true;
            Sfx.Play(SfxId.Lift);
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

            float t = 0f;
            const float duration = 0.28f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / duration), 3f);
                for (int i = 0; i < moving.Count; i++)
                {
                    if (moving[i] == null) continue;
                    moving[i].rectTransform.anchoredPosition = Vector2.Lerp(starts[i], goals[i], k);
                }
                yield return null;
            }

            Sfx.Play(SfxId.Place);
            Refresh();
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

            var drops = new List<int>();
            for (int c = 0; c < before.Length; c++)
            {
                if (!board.IsOpen(c) || board.Column(c).Count <= before[c]) continue;
                drops.Add(c);
                int count = board.Column(c).Count;
                for (int i = 0; i < items[c].Count; i++)
                {
                    if (items[c][i] == null) continue;
                    Place(items[c][i].rectTransform, i + 1, count);
                }
            }

            Vector3 origin = hud.PileOrigin();
            for (int d = 0; d < drops.Count; d++)
            {
                int column = drops[d];
                Image flyer = Spawn(column, 0, board.Column(column)[0]);
                items[column].Insert(0, flyer);
                RectTransform rect = flyer.rectTransform;
                Vector2 goal = rect.anchoredPosition;
                rect.position = origin;
                Vector2 start = rect.anchoredPosition;
                float t = 0f;
                const float duration = 0.26f;
                while (t < duration)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / duration);
                    float e = 1f - Mathf.Pow(1f - k, 3f);
                    Vector2 pos = Vector2.Lerp(start, goal, e);
                    pos.y += Mathf.Sin(k * Mathf.PI) * 56f;
                    rect.anchoredPosition = pos;
                    rect.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, e);
                    yield return null;
                }
                rect.anchoredPosition = goal;
                rect.localScale = Vector3.one;
                Sfx.Play(SfxId.Place);
                if (d < drops.Count - 1)
                    yield return new WaitForSeconds(0.06f);
            }

            Refresh();
            Busy = false;
        }

        public IEnumerator PlayPack(PackBoard.PackResult packed, int slot, Sprite icon)
        {
            Busy = true;
            Sfx.Play(SfxId.Pack);
            List<Image> flying = packed.Column >= 0 && packed.Column < items.Length
                ? items[packed.Column]
                : null;
            Vector3 target = hud.BinAnchor(slot);
            var starts = new List<Vector3>();
            if (flying != null)
            {
                for (int i = 0; i < flying.Count; i++)
                    starts.Add(flying[i] != null ? flying[i].rectTransform.position : target);
            }
            float t = 0f;
            const float duration = 0.32f;
            while (flying != null && t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float e = 1f - Mathf.Pow(1f - k, 3f);
                for (int i = 0; i < flying.Count; i++)
                {
                    if (flying[i] == null) continue;
                    flying[i].rectTransform.position = Vector3.Lerp(starts[i], target, e);
                    flying[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, e);
                }
                yield return null;
            }

            if (flying != null)
            {
                for (int i = flying.Count - 1; i >= 0; i--)
                    UiKit.Discard(flying[i]);
                flying.Clear();
            }
            hud.SetBoxSlot(slot, icon);
            PaintLanes();
            if (packed.Shipped)
            {
                yield return new WaitForSeconds(0.45f);
                if (!packed.Won)
                    hud.ClearBox();
            }
            Refresh();
            Busy = false;
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
                    face.color = open ? Color.white : new Color(0.78f, 0.74f, 0.76f, 1f);
                }
                if (hud.lockIcons[c] != null)
                    hud.lockIcons[c].gameObject.SetActive(!open);
                if (hud.lockLabels[c] != null)
                {
                    hud.lockLabels[c].gameObject.SetActive(!open);
                    hud.lockLabels[c].text = board.IsAdColumn(c) ? "临时解锁" : "装1箱后开";
                }
                if (hud.adBadges[c] != null)
                    hud.adBadges[c].gameObject.SetActive(!open && board.IsAdColumn(c));
                if (hud.readyMarks[c] != null)
                    hud.readyMarks[c].enabled = ready;
            }
        }

        void LiftSelection()
        {
            int column = board.Selected;
            if (column < 0 || column >= items.Length) return;
            int count = board.GroupSize(column);
            List<Image> stack = items[column];
            for (int i = 0; i < count && i < stack.Count; i++)
            {
                if (stack[i] == null) continue;
                Vector2 pos = stack[i].rectTransform.anchoredPosition;
                pos.y += 36f;
                stack[i].rectTransform.anchoredPosition = pos;
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
            Place(image.rectTransform, row, board.Column(column).Count);
            image.raycastTarget = false;
            return image;
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
