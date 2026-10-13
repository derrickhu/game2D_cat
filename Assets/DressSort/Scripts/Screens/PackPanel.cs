using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace DressSort
{
    /// <summary>
    /// 活动页「衣服排排上车」：发牌、点选、上车、补充、每辆车的奖励，以及三处激励视频——
    /// 第 7 列临时解锁、领奖翻倍、堵住时加一列或收起顶上的衣服。
    /// </summary>
    public class PackPanel : Panel
    {
        PackHud hud;
        PackView view;
        BoardFx fx;
        PackBoard board;
        ItemDef[] palette = new ItemDef[0];
        bool popupOpen;
        bool boardHinted;
        int runId;

        protected override void Build()
        {
            Transform frameBg = transform.Find("Backdrop");
            if (frameBg != null)
                frameBg.gameObject.SetActive(false);

            hud = PackHud.Assemble((RectTransform)transform, root, app.Database);
            hud.transform.SetAsFirstSibling();
            fx = BoardFx.Create(hud.fxLayer, app.Database);
            view = PackView.Attach(hud, fx);
            hud.Wire(() => app.Show(ScreenId.Home), Restart, OnBoard, OnRefill, OnLane);
        }

        public override void OnShow()
        {
            if (hud == null) return;
            hud.Fit();
            Restart();
        }

        public override void OnHide()
        {
            StopAllCoroutines();
            ClearPopups();
            if (fx != null) fx.Clear();
        }

        void ClearPopups()
        {
            popupOpen = false;
            if (hud == null || hud.popupLayer == null) return;
            for (int i = hud.popupLayer.childCount - 1; i >= 0; i--)
                UiKit.Discard(hud.popupLayer.GetChild(i));
        }

        bool Locked => board == null || view.Busy || popupOpen;

        void Restart()
        {
            if (view != null && view.Busy) return;
            palette = PickPalette();
            if (palette.Length == 0)
            {
                Sfx.Play(SfxId.Deny);
                hud.ShowToast("还没有可以整理的裙子");
                return;
            }
            StopAllCoroutines();
            runId++;
            ClearPopups();
            boardHinted = false;
            fx.Clear();
            fx.ClearBanners();
            board = new PackBoard(palette.Length, Random.Range(1, 999999));
            hud.ShowCar(0);
            hud.SetProgress(0, 0, board.CurrentSeats);
            view.Bind(board, palette);
            RefreshChrome();
            StartCoroutine(IntroRoutine());
        }

        IEnumerator IntroRoutine()
        {
            Sfx.Play(SfxId.DealIn);
            yield return view.PlayIntro();
            RefreshChrome();
            hud.ShowToast("同款排满一列，点「上车」送走", 3f);
        }

        // ------------------------------------------------------------ 点列

        void OnLane(int column)
        {
            if (Locked || board.Won) return;
            if (!board.IsOpen(column))
            {
                fx.Shake(hud.lanes[column].rectTransform, 8f, 0.2f);
                if (board.IsAdColumn(column))
                {
                    WatchAd("pack_column", () =>
                    {
                        if (board == null || !board.UnlockAd()) return;
                        view.Refresh();
                        RefreshChrome();
                        hud.ShowToast("多了一条衣架，可以随便放");
                    });
                    return;
                }
                Sfx.Play(SfxId.Deny);
                hud.ShowToast(column == PackBoard.SecondUnlock ? "送走 2 辆车后解锁" : "送走 1 辆车后解锁");
                return;
            }
            if (board.Selected < 0 && board.IsFullUniform(column))
            {
                Sfx.Play(SfxId.Tap);
                hud.ShowToast("这一列排满了，点「上车」");
                return;
            }
            StartCoroutine(MoveRoutine(column));
        }

        IEnumerator MoveRoutine(int column)
        {
            int run = runId;
            yield return view.PlayTap(column);
            if (run != runId) yield break;
            if (!string.IsNullOrEmpty(view.Blocked))
                hud.ShowToast(view.Blocked);
            RefreshChrome();
            if (board.CanBoard && !boardHinted)
            {
                boardHinted = true;
                hud.ShowToast("排满啦，点「上车」");
            }
            Notice();
        }

        // ------------------------------------------------------------ 上车

        void OnBoard()
        {
            if (Locked || board.Won) return;
            if (!board.CanBoard)
            {
                Sfx.Play(SfxId.Deny);
                fx.Shake((RectTransform)hud.boardButton.transform, 8f, 0.2f);
                hud.ShowToast("先把一列排满同款");
                return;
            }
            Sfx.Play(SfxId.Tap);
            List<PackBoard.Boarding> list = board.BoardAll();
            StartCoroutine(BoardRoutine(list));
        }

        IEnumerator BoardRoutine(List<PackBoard.Boarding> list)
        {
            int run = runId;
            RefreshChrome();
            yield return view.PlayBoard(list, CarReward);
            if (run != runId) yield break;
            RefreshChrome();
            if (board.Won)
            {
                yield return view.PlayFinale();
                yield return Ask("全部送达！", "四辆车都坐满了，明天还有新的奖励", null,
                    "再来一局", Restart, null, null, true);
                yield break;
            }
            if (board.Car > 0 && list.Count > 0 && list[list.Count - 1].CarDone)
            {
                string unlock = board.Car == 1 ? "第 5 条衣架开了" : board.Car == 2 ? "第 6 条衣架开了" : "";
                if (!string.IsNullOrEmpty(unlock)) hud.ShowToast(unlock);
            }
            Notice();
        }

        /// <summary>一辆车坐满开走后弹奖励。看广告翻倍，没看完按单倍。</summary>
        IEnumerator CarReward(int car)
        {
            bool last = car >= PackBoard.CarCount - 1;
            string title = last ? "满载而归！" : "第 " + (car + 1) + " 辆车出发！";
            var items = StageItems(car);
            if (app.Wardrobe.PackStageClaimed(car))
            {
                yield return Ask(title, "今天这辆的奖励已经领过", items, "继续", null, null, null, true);
                yield break;
            }
            Sfx.Play(SfxId.StageReward);
            fx.Confetti(hud.transform.position, 40, 1500f, 1f);
            bool settled = false;
            yield return Ask(title, "送达奖励", items, "领取", () =>
            {
                app.Wardrobe.ClaimPackStage(car, 1);
                settled = true;
            }, "双倍领取", () =>
            {
                WatchAd("pack_double", () =>
                {
                    app.Wardrobe.ClaimPackStage(car, 2);
                    Sfx.Play(SfxId.Claim);
                    hud.ShowToast("奖励翻倍了");
                    settled = true;
                }, () =>
                {
                    app.Wardrobe.ClaimPackStage(car, 1);
                    hud.ShowToast("广告没看完，先领了单倍");
                    settled = true;
                });
            }, true);
            while (!settled) yield return null;
            Sfx.Play(SfxId.Claim);
        }

        List<PackHud.PopupItem> StageItems(int car)
        {
            var list = new List<PackHud.PopupItem>();
            GameDatabase db = app.Database;
            int[] mats = WardrobeService.PackStageMats[Mathf.Clamp(car, 0, WardrobeService.PackStageMats.Length - 1)];
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] <= 0) continue;
                Sprite icon = db.craftMats != null && m < db.craftMats.Count ? db.craftMats[m] : null;
                list.Add(new PackHud.PopupItem { Icon = icon, Count = mats[m] });
            }
            if (car >= PackBoard.CarCount - 1)
                list.Add(new PackHud.PopupItem { Icon = db.iconHomeEnergy, Count = WardrobeService.PackReward });
            return list;
        }

        // ------------------------------------------------------------ 补充

        void OnRefill()
        {
            if (Locked || board.Won) return;
            if (!board.CanRefill)
            {
                Sfx.Play(SfxId.Deny);
                fx.Shake((RectTransform)hud.refillButton.transform, 8f, 0.2f);
                hud.ShowToast("衣架都满了，先倒一倒");
                return;
            }
            Sfx.Play(SfxId.Tap);
            List<PackBoard.Drop> drops = board.Refill();
            StartCoroutine(RefillRoutine(drops));
        }

        IEnumerator RefillRoutine(List<PackBoard.Drop> drops)
        {
            int run = runId;
            RefreshChrome();
            yield return view.PlayRefill(drops);
            if (run != runId) yield break;
            RefreshChrome();
            Notice();
        }

        // ------------------------------------------------------------ 堵住

        void Notice()
        {
            if (board == null || board.Won || popupOpen || !board.IsStuck) return;
            StartCoroutine(StuckRoutine());
        }

        IEnumerator StuckRoutine()
        {
            Sfx.Play(SfxId.Stuck);
            WxBridge.Vibrate(WxBridge.Buzz.Heavy);
            bool canAddColumn = !board.IsOpen(PackBoard.AdColumn);
            string sub = canAddColumn ? "看个视频，多开一条衣架接着放" : "看个视频，把每列顶上的衣服收回去";
            string adLabel = canAddColumn ? "加一列" : "收起衣服";
            bool watched = false;
            yield return Ask("衣架堵住了", sub, null, "重新开始", Restart, adLabel, () =>
            {
                WatchAd(canAddColumn ? "pack_revive_column" : "pack_revive_take", () => watched = true, () =>
                {
                    hud.ShowToast("广告没看完");
                    Notice();
                });
            }, false);
            if (!watched) yield break;
            while (view.Busy) yield return null;
            if (canAddColumn)
            {
                board.UnlockAd();
                view.Refresh();
                hud.ShowToast("多了一条衣架，接着整理");
            }
            else
            {
                List<PackBoard.Drop> taken = board.Revive();
                yield return view.PlayRevive(taken);
                hud.ShowToast("顶上的衣服收回去了，补充时会先落回同款");
            }
            RefreshChrome();
            Notice();
        }

        // ------------------------------------------------------------ 公共

        IEnumerator Ask(string title, string sub, List<PackHud.PopupItem> items, string primary, UnityAction onPrimary,
            string ad, UnityAction onAd, bool rays)
        {
            popupOpen = true;
            yield return hud.Popup(title, sub, items, primary, onPrimary, ad, onAd, rays);
            popupOpen = false;
        }

        void WatchAd(string scene, System.Action success, System.Action fail = null)
        {
            string unit = WxBridge.RewardedAdUnitId;
            bool tracked = !string.IsNullOrEmpty(unit);
            if (tracked) Analytics.AdRequest(scene, unit);
            WxBridge.ShowRewarded(() =>
            {
                if (tracked)
                {
                    Analytics.AdShow(scene, unit);
                    Analytics.AdClose(scene, unit, true);
                }
                success?.Invoke();
            }, () =>
            {
                if (tracked) Analytics.AdError(scene, unit, "not_ended");
                Sfx.Play(SfxId.Deny);
                if (fail != null) fail();
                else hud.ShowToast("广告没看完");
            });
        }

        void RefreshChrome()
        {
            if (hud == null || board == null) return;
            bool idle = !view.Busy;
            hud.SetActions(board.CanBoard && idle, board.CanRefill && idle);
            if (!view.Busy)
                hud.SetProgress(board.Car, board.SeatsFilled, board.CurrentSeats);
        }

        void Update()
        {
            if (board == null || hud == null) return;
            hud.SetActions(board.CanBoard && !view.Busy && !popupOpen, board.CanRefill && !view.Busy && !popupOpen);
        }

        ItemDef[] PickPalette()
        {
            var pool = new List<ItemDef>();
            List<ItemDef> dresses = app.Database.ItemsInSlot(ItemSlot.Dress);
            for (int i = 0; i < dresses.Count; i++)
            {
                ItemDef item = dresses[i];
                if (item == null || !item.boardEligible || item.id == "mystery") continue;
                pool.Add(item);
            }
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                ItemDef tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }
            int count = Mathf.Min(PackBoard.MaxColors, pool.Count);
            var picked = new ItemDef[count];
            for (int i = 0; i < count; i++)
                picked[i] = pool[i];
            return picked;
        }
    }
}
