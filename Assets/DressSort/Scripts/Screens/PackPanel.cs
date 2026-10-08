using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
        bool stuckNoted;

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
            ApplyPackArt();
            hud.FitOnCarpet();
            hud.FitChrome();
            hud.Wire(() => app.Show(ScreenId.Home), Restart, OnPack, OnRefill, OnLane);
        }

        void ApplyPackArt()
        {
            if (hud == null) return;
            if (hud.backdrop == null)
            {
                Transform found = hud.transform.Find("Backdrop");
                if (found != null)
                    hud.backdrop = found.GetComponent<Image>();
            }

            Sprite carpet = Resources.Load<Sprite>("Pack/bg_carpet");
            if (carpet == null && app.Database != null)
                carpet = app.Database.packCarpet;
            Sprite lane = hud.laneSprite;
            Sprite laneOn = hud.laneOnSprite;
            Sprite laneReady = hud.laneReadySprite;
#if UNITY_EDITOR
            // 编辑器里直接读磁盘上的新图。导入没跟上时，预制还指着旧房间。
            Sprite looseCarpet = LoadLoose("DressSort/Resources/Pack/bg_carpet.jpg");
            if (looseCarpet != null) carpet = looseCarpet;
            Sprite looseLane = LoadLoose("DressSort/Art/Ui/Pack/lane.png");
            Sprite looseOn = LoadLoose("DressSort/Art/Ui/Pack/lane_on.png");
            Sprite looseReady = LoadLoose("DressSort/Art/Ui/Pack/lane_ready.png");
            if (looseLane != null) lane = looseLane;
            if (looseOn != null) laneOn = looseOn;
            if (looseReady != null) laneReady = looseReady;
#endif
            hud.laneSprite = lane;
            hud.laneOnSprite = laneOn;
            hud.laneReadySprite = laneReady;
            Sprite heap = Resources.Load<Sprite>("Pack/heap");
            Sprite openBin = Resources.Load<Sprite>("Pack/bin_open");
            Sprite shutBin = Resources.Load<Sprite>("Pack/bin_shut");
#if UNITY_EDITOR
            Sprite looseHeap = LoadLoose("DressSort/Resources/Pack/heap.png");
            Sprite looseOpen = LoadLoose("DressSort/Resources/Pack/bin_open.png");
            Sprite looseShut = LoadLoose("DressSort/Resources/Pack/bin_shut.png");
            if (looseHeap != null) heap = looseHeap;
            if (looseOpen != null) openBin = looseOpen;
            if (looseShut != null) shutBin = looseShut;
#endif
            hud.heapSprite = heap;
            hud.binOpen = openBin;
            hud.binShut = shutBin;
            if (hud.backdrop != null && carpet != null)
            {
                hud.backdrop.sprite = carpet;
                hud.backdrop.color = Color.white;
                hud.backdrop.type = Image.Type.Simple;
                hud.backdrop.preserveAspect = false;
            }
            for (int i = 0; i < hud.laneFaces.Length; i++)
            {
                if (hud.laneFaces[i] == null || lane == null) continue;
                hud.laneFaces[i].sprite = lane;
                hud.laneFaces[i].type = Image.Type.Simple;
                hud.laneFaces[i].color = Color.white;
            }
        }

#if UNITY_EDITOR
        static Sprite LoadLoose(string underAssets)
        {
            string full = System.IO.Path.Combine(Application.dataPath, underAssets);
            if (!System.IO.File.Exists(full)) return null;
            byte[] bytes = System.IO.File.ReadAllBytes(full);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes)) return null;
            tex.name = System.IO.Path.GetFileNameWithoutExtension(full);
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
#endif

        public override void OnShow()
        {
            if (hud == null) return;
            ApplyPackArt();
            hud.FitOnCarpet();
            hud.FitChrome();
            Restart();
        }

        void Restart()
        {
            if (view != null && view.Busy) return;
            palette = PickPalette();
            mystery = app.Database.mysteryItem != null ? app.Database.mysteryItem.ResolveIcon() : null;
            if (palette.Length == 0)
            {
                Sfx.Play(SfxId.Deny);
                hud.ShowToast("还没有可以装箱的裙子");
                return;
            }
            stuckNoted = false;
            Sfx.Play(SfxId.Shuffle);
            board = new PackBoard(palette.Length, Random.Range(1, 999999));
            hud.ClearBox();
            hud.LayoutBins(board.CurrentSeats);
            view.Bind(board, palette, mystery);
            RefreshChrome();
            hud.ShowToast("先把顶上那件挪开，问号会翻成同色。排满一列就能装箱");
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
                    }, () =>
                    {
                        Sfx.Play(SfxId.Deny);
                        hud.ShowToast("广告没看完");
                    });
                    return;
                }
                Sfx.Play(SfxId.Deny);
                hud.ShowToast(column == PackBoard.SecondColumn ? "装 2 箱后解锁" : "装 1 箱后解锁");
                return;
            }

            StartCoroutine(MoveRoutine(column));
        }

        IEnumerator MoveRoutine(int column)
        {
            yield return view.PlayMove(column);
            if (!string.IsNullOrEmpty(view.Blocked))
                hud.ShowToast(view.Blocked);
            RefreshChrome();
            Notice();
        }

        void OnPack()
        {
            if (board == null || view.Busy || board.Won) return;
            if (!board.CanPack)
            {
                Sfx.Play(SfxId.Deny);
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
            if (packed.Shipped && !packed.Won)
            {
                if (board.BoxesDone == 1)
                    hud.ShowToast("第 1 箱装满了，新的一列开了");
                else if (board.BoxesDone == 2)
                    hud.ShowToast("第 2 箱装满了，又开了一列");
            }
            if (packed.Won)
            {
                bool granted = app.Wardrobe.ClaimPack();
                Sfx.Play(SfxId.Win);
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
                Sfx.Play(SfxId.Deny);
                if (board.ReserveCount == 0)
                    hud.ShowToast("衣服已经发完了");
                else
                    hud.ShowToast("先把能接上的倒开，再补充");
                return;
            }
            StartCoroutine(RefillRoutine());
        }

        IEnumerator RefillRoutine()
        {
            yield return view.PlayRefill();
            RefreshChrome();
            Notice();
        }

        void Notice()
        {
            if (board == null || board.Won) return;
            if (!board.IsStuck)
            {
                stuckNoted = false;
                return;
            }
            if (stuckNoted) return;
            stuckNoted = true;
            Sfx.Play(SfxId.Stuck);
            hud.ShowToast("这局堵住了，点重开再来");
        }

        void RefreshChrome()
        {
            if (hud == null || board == null) return;
            hud.SetProgress(board.BoxesDone, PackBoard.BoxesToWin, mystery);
            hud.SetPile(board.ReserveCount);
            hud.SetActions(board.CanPack, board.CanRefill);
            hud.SetBoxCount(board.BoxFilled, board.CurrentSeats, board.BoxesDone, board.Won);
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
