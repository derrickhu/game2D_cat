using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DressSort
{
    /// <summary>
    /// 活动页。外观在 Resources/Prefabs/PackScreen，这里负责发牌、点选、装箱和广告解锁。
    /// </summary>
    public class PackPanel : Panel
    {
        const int PaletteSize = 4;

        PackHud hud;
        PackView view;
        PackBoard board;
        ItemDef[] palette = new ItemDef[0];
        Sprite mystery;

        protected override void Build()
        {
            Transform frameBg = transform.Find("Backdrop");
            if (frameBg != null)
                frameBg.gameObject.SetActive(false);

            GameObject prefab = Resources.Load<GameObject>("Prefabs/PackScreen");
            hud = prefab != null
                ? Instantiate(prefab, transform).GetComponent<PackHud>()
                : PackHud.Assemble((RectTransform)transform, app.Database);
            if (hud == null)
            {
                Debug.LogError("[叠叠裙] 装箱预制没有 PackHud");
                return;
            }

            var rect = (RectTransform)hud.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();
            if (hud.topBar != null)
                hud.topBar.SetParent(root, false);

            view = hud.GetComponent<PackView>();
            if (view == null)
                view = PackView.Attach(hud);
            hud.Wire(() => app.Show(ScreenId.Home), Restart, OnPack, OnRefill, OnLane);
        }

        public override void OnShow()
        {
            if (hud == null) return;
            Restart();
        }

        void Restart()
        {
            if (view != null && view.Busy) return;
            palette = PickPalette();
            mystery = Resources.Load<Sprite>("DressIcons/mystery");
            if (palette.Length == 0)
            {
                hud.ShowToast("还没有可以装箱的裙子");
                return;
            }
            board = new PackBoard(palette.Length, Random.Range(1, 999999));
            hud.ClearBox();
            view.Bind(board, palette, mystery);
            RefreshChrome();
            hud.ShowToast("先点一列，再点另一列，把最上面的同款倒过去");
        }

        void OnLane(int column)
        {
            if (board == null || view.Busy || board.Won) return;
            if (!board.IsOpen(column))
            {
                if (board.IsAdColumn(column))
                {
                    WxBridge.ShowRewarded(() =>
                    {
                        if (board == null || board.IsOpen(column)) return;
                        board.UnlockAd(column);
                        view.Refresh();
                        hud.ShowToast("这一列临时解锁了");
                    }, () => hud.ShowToast("广告没看完"));
                    return;
                }
                hud.ShowToast("装 1 箱后解锁");
                return;
            }

            StartCoroutine(MoveRoutine(column));
        }

        IEnumerator MoveRoutine(int column)
        {
            yield return view.PlayMove(column);
            RefreshChrome();
            Notice();
        }

        void OnPack()
        {
            if (board == null || view.Busy || board.Won) return;
            if (!board.CanPack)
            {
                hud.ShowToast("还没有排满的一列");
                return;
            }
            int slot = board.BoxFilled;
            PackBoard.PackResult packed = board.Pack();
            Sprite icon = packed.Type >= 0 && packed.Type < palette.Length
                ? palette[packed.Type].ResolveIcon()
                : null;
            StartCoroutine(PackRoutine(packed, slot, icon));
        }

        IEnumerator PackRoutine(PackBoard.PackResult packed, int slot, Sprite icon)
        {
            yield return view.PlayPack(packed, slot, icon);
            RefreshChrome();
            if (packed.Shipped && board.IsOpen(PackBoard.ProgressColumn) && board.BoxesDone == 1)
                hud.ShowToast("新的一列开了");
            if (packed.Won)
            {
                bool granted = app.Wardrobe.ClaimPack();
                hud.ShowToast(granted ? "整理完成，体力 +2" : "整理完成，今天的体力已经领过");
                yield break;
            }
            Notice();
        }

        void OnRefill()
        {
            if (board == null || view.Busy || board.Won) return;
            if (!board.CanRefill)
            {
                hud.ShowToast(board.ReserveCount == 0 ? "衣服已经发完了" : "列上没有空位");
                return;
            }
            int placed = board.Refill();
            view.Refresh();
            RefreshChrome();
            hud.ShowToast(placed > 0 ? "补了 " + placed + " 件" : "没有补上");
            Notice();
        }

        void Notice()
        {
            if (board == null || board.Won) return;
            if (board.IsStuck)
                hud.ShowToast("这局堵住了，点重开再来");
        }

        void RefreshChrome()
        {
            if (hud == null || board == null) return;
            hud.SetProgress(board.BoxesDone, PackBoard.BoxesToWin);
            hud.SetQueue(board.ReserveCount, mystery);
        }

        ItemDef[] PickPalette()
        {
            var pool = new List<ItemDef>();
            List<ItemDef> dresses = app.Database.ItemsInSlot(ItemSlot.Dress);
            for (int i = 0; i < dresses.Count; i++)
            {
                ItemDef item = dresses[i];
                if (item == null || !item.boardEligible) continue;
                if (item.id == "mystery") continue;
                pool.Add(item);
            }
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                ItemDef tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }
            int count = Mathf.Min(PaletteSize, pool.Count);
            var picked = new ItemDef[count];
            for (int i = 0; i < count; i++)
                picked[i] = pool[i];
            return picked;
        }
    }
}
