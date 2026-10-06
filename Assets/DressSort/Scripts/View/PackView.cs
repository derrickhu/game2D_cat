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
        const float Item = 112f;
        const float Step = 90f;
        const float Pad = 108f;

        PackHud hud;
        PackBoard board;
        ItemDef[] palette;
        Sprite mystery;
        readonly List<Image>[] items = new List<Image>[PackBoard.ColumnCount];

        public bool Busy { get; private set; }

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
                for (int i = items[c].Count - 1; i >= 0; i--)
                    UiKit.Discard(items[c][i]);
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
            if (board == null || Busy) yield break;
            int source = board.Selected;
            int pour = source >= 0 ? board.PourCount(source, column) : 0;
            if (pour <= 0 || source >= items.Length || items[source].Count < pour)
            {
                board.Tap(column);
                Refresh();
                yield break;
            }

            Busy = true;
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

            Refresh();
            Busy = false;
        }

        public IEnumerator PlayPack(PackBoard.PackResult packed, int slot, Sprite icon)
        {
            Busy = true;
            List<Image> flying = packed.Column >= 0 && packed.Column < items.Length
                ? items[packed.Column]
                : null;
            Vector3 target = hud.boxSlots[slot] != null
                ? hud.boxSlots[slot].transform.position
                : hud.box.transform.position;
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
                    // 选中列的红边比列条本身宽，九宫格拉满会只剩红框。选中时整张拉伸，红边按比例收细。
                    bool selectedArt = selected && !ready && hud.laneOnSprite != null;
                    face.type = selectedArt ? Image.Type.Simple : Image.Type.Sliced;
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
