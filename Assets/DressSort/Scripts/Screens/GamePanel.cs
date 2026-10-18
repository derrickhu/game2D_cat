using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    public class GamePanel : Panel
    {
        GameHud hud;
        BoardView board;
        BoardFx fx;
        SortBoard logic;
        LevelDef level;

        int shufflesLeft;
        int swapsLeft;
        int shuffleSerial;
        int dealSerial;
        bool stepsAdUsed;
        bool toolAdBusy;
        bool failOpen;
        bool failShown;
        bool leaving;
        bool failWired;
        bool finished;
        bool clearing;

        protected override void Build()
        {
            Image frameBg = transform.Find("Backdrop") != null
                ? transform.Find("Backdrop").GetComponent<Image>()
                : null;
            if (frameBg != null)
                frameBg.enabled = false;

            GameObject prefab = Resources.Load<GameObject>("Prefabs/GameScreen");
            if (prefab != null)
                hud = Instantiate(prefab, transform).GetComponent<GameHud>();
            else
                hud = GameHud.Assemble((RectTransform)transform, app.Database);

            if (hud == null)
            {
                Debug.LogError("[一裙又一裙] 关卡预制没有 GameHud");
                return;
            }

            Stretch(hud.transform as RectTransform);
            hud.transform.SetAsFirstSibling();
            if (hud.topBar != null)
                hud.topBar.SetParent(root, false);
            if (hud.pauseLayer != null)
                hud.pauseLayer.SetParent(transform, false);
            if (app.Database != null)
            {
                hud.laneSolved = app.Database.boardLaneSolved;
                hud.checkSolved = app.Database.boardCheckSolved;
                hud.sparkleSprite = app.Database.rewardSparkle;
                hud.BindMechanics(app.Database);
            }
            hud.EnsureClear(app.Database);
            hud.EnsureStepsAd(app.Database);
            EnsureFailReady();
            Sfx.BindClick(hud.stepsAdButton, OnStepsAd);
            hud.Wire(OnSwap, OnUndo, OnShuffle,
                () => { CancelSwap(); CloseFail(); hud.ShowPause(true); },
                () => hud.ShowPause(false),
                OnRestart,
                OnFailHome,
                OnClearNow);

            board = BoardView.Create(hud.boardRoot != null ? hud.boardRoot : root,
                new BoardView.Layout { overlap = 0.70f });
            board.AttachHud(hud);
            fx = BoardFx.Create(hud.transform, app.Database);
            board.AttachFx(fx);
            board.OnColumnClicked = OnColumnClicked;
            board.OnItemClicked = OnSwapPick;
        }

        static void Stretch(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public override void OnShow()
        {
            level = app.CurrentLevel;
            if (level == null || !level.IsValid)
            {
                if (hud != null)
                    hud.ShowToast("这一关的配置不完整");
                return;
            }
            TrackLevelStart();
            stepsAdUsed = false;
            shuffleSerial = 0;
            Deal(level.seed, true);
        }

        public SortBoard Logic => logic;

        public void DealWithSeed(int seed)
        {
            level = app.CurrentLevel;
            Deal(seed);
        }

        public void StepImmediate(int column)
        {
            if (!logic.CanPlay(column)) return;
            logic.Play(column);
            board.Refresh();
            UpdateHud();
        }

        /// <summary>intro：新进一关才放难关开场，洗牌和重开只重新发牌。</summary>
        void Deal(int seed, bool intro = false)
        {
            finished = false;
            clearing = false;
            dealSerial++;
            failShown = false;
            leaving = false;
            CloseFail();
            if (fx != null)
            {
                fx.Clear();
                fx.ClearBanners();
            }
            SyncTools();
            if (board != null)
                board.PickMode = false;
            if (hud != null)
            {
                hud.SetSwapArmed(false);
                hud.ShowPause(false);
            }

            logic = new SortBoard(level.columns, level.columnHeight, level.columns, level.moveLimit);
            logic.SetCovers(level.dustCovers);
            logic.SetLocks(level.locks);
            logic.SetTargets(level.targets);
            logic.Deal(level.scrambleMoves, seed, level.parcels, level.alarms);

            if (hud != null)
            {
                hud.ApplyTheme(
                    app.Database.BoardBgFor(level.index),
                    app.Database.BoardHangerFor(level.index),
                    app.Database.BoardRodFor(level.index),
                    level.columns);
            }

            board.Bind(logic, level.palette, level.mystery);
            UpdateHud(false);
            if (hud != null)
                hud.ShowToast(LevelCatalog.IntroFor(level), true);

            bool hard = intro && LevelCatalog.IsHard(level) && fx != null && isActiveAndEnabled;
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            board.PlayDealIn(hard ? 1.35f : 0f);
            if (hard)
            {
                Sfx.Play(SfxId.HardLevel);
                WxBridge.Vibrate(WxBridge.Buzz.Heavy);
                fx.StartCoroutine(fx.Splash("高能预警", "这关步数要省着用"));
            }
        }

        void OnColumnClicked(int column)
        {
            if (finished || board.Busy) return;
            if (!logic.CanPlay(column))
            {
                Sfx.Play(SfxId.Deny);
                board.Deny(column);
                if (hud != null)
                {
                    if (logic.IsCovered(column))
                        hud.ShowToast("再叠好 " + logic.CoverLeft(column) + " 列，防尘罩就拉开");
                    else if (logic.IsLocked(column))
                        hud.ShowToast("锁着呢，先把带钥匙的那件顶出来");
                    else if (logic.IsColumnSolved(column))
                        hud.ShowToast("这列已经叠好了");
                    else if (logic.AlarmRang)
                        hud.ShowToast("闹钟响了，撤回一步或者重新开始");
                    else if (logic.OutOfMoves)
                        hud.ShowToast(StuckText());
                    else
                        hud.ShowToast("手里没有可放的");
                }
                return;
            }
            if (logic.Steps == 0 && hud != null)
                hud.ShowToast("");
            StartCoroutine(PlayRoutine(column));
        }

        IEnumerator PlayRoutine(int column)
        {
            yield return board.PlayColumn(column);
            UpdateHud();
            if (logic.LastUnlocked.Length > 0)
                hud.ShowToast("钥匙打开了一把锁");
            else if (logic.LastAlarmOff)
            {
                Sfx.Play(SfxId.AlarmOff);
                hud.ShowToast("闹钟关掉了");
            }
            else if (logic.LastOpened.Length > 0)
                hud.ShowToast("防尘罩拉开了");

            if (logic.IsWin())
            {
                finished = true;
                hud.ShowToast("");
                yield return board.Finale("整理完成！");
                Complete();
                yield break;
            }

            if (logic.AlarmRang)
            {
                finished = true;
                PresentFail();
                yield break;
            }

            if (logic.OutOfMoves)
            {
                finished = true;
                PresentFail();
            }
        }

        void Update()
        {
            if (leaving || failOpen || clearing || logic == null || !finished || logic.IsWin()) return;
            if (hud != null && hud.pauseLayer != null && hud.pauseLayer.gameObject.activeSelf) return;
            if (logic.OutOfMoves || logic.AlarmRang)
                PresentFail();
        }

        void EnsureFailReady()
        {
            if (hud == null) return;
            hud.EnsureFail(app != null ? app.Database : null);
            if (hud.failLayer != null && hud.failLayer.parent != transform)
            {
                hud.failLayer.SetParent(transform, false);
                Stretch(hud.failLayer);
            }
            if (failWired) return;
            failWired = true;
            Sfx.BindClick(hud.failAdButton, OnStepsAd);
            Sfx.BindClick(hud.failRestartButton, OnRestart);
            Sfx.BindClick(hud.failHomeButton, OnFailHome);
        }

        void PresentFail()
        {
            if (leaving || failOpen || hud == null || logic == null || clearing || logic.IsWin()) return;
            EnsureFailReady();
            failOpen = true;
            CancelSwap();
            hud.ShowToast("");
            bool first = !failShown;
            failShown = true;
            if (first)
                WxBridge.Vibrate(WxBridge.Buzz.Heavy);
            if (logic.AlarmRang)
            {
                if (first)
                    Sfx.PlaySting(SfxId.Alarm);
                hud.ShowFail("闹钟响了", "重新开始，或者回到首页", false);
                return;
            }
            if (first)
                Sfx.PlaySting(SfxId.Stuck);
            bool ad = !stepsAdUsed;
            hud.ShowFail("步数用完了", ad ? "看广告，再加 5 步" : "这关不能再加步了", ad);
        }

        void CloseFail()
        {
            failOpen = false;
            Sfx.StopSting();
            if (hud != null)
                hud.HideFail();
        }

        void OnFailHome()
        {
            leaving = true;
            if (hud != null)
                hud.ShowPause(false);
            bool failed = finished && logic != null && !logic.IsWin();
            string reason = logic != null && logic.AlarmRang ? "alarm" : "out_of_moves";
            CloseFail();
            if (level != null && !finished)
                Analytics.LevelFail("quit_to_home", LevelProgress());
            else if (level != null && failed)
                Analytics.LevelFail(reason, LevelProgress());
            app.Show(ScreenId.Home);
        }

        void OnClearNow()
        {
            if (clearing || level == null || logic == null) return;
            clearing = true;
            finished = true;
            CloseFail();
            CancelSwap();
            if (hud != null)
                hud.ShowToast("");
            StartCoroutine(ClearNowRoutine());
        }

        IEnumerator ClearNowRoutine()
        {
            if (board != null)
            {
                board.StopAllCoroutines();
                yield return board.Finale("整理完成！");
            }
            Complete();
        }

        void Complete()
        {
            int spare = Mathf.Max(0, level.moveLimit - logic.Steps);
            int stars = 1 + Mathf.Clamp(spare / Mathf.Max(1, level.moveLimit / 3), 0, 2);
            Analytics.LevelClear(stars);
            app.PendingLevel = level;
            // 图纸要进工坊，GM 也入库。关卡进度仍然不写。
            bool freshPrint = false;
            if (level.blueprint != null)
            {
                freshPrint = app.Wardrobe.AddBlueprint(level.blueprint);
                app.PendingPrintSlot = level.blueprint.slot;
                app.OpenPrintTab = true;
            }
            int[] mats = CraftCatalog.MaterialsFor(level.index, app.Wardrobe.Shortage());
            if (!app.GmPlay)
            {
                app.Wardrobe.ReportCleared(level.index, stars);
                RankService.Submit(app.Wardrobe.LevelsCleared);
                app.Wardrobe.AddMaterials(mats);
            }
            app.PendingMaterials = mats;
            app.PendingNewPrint = freshPrint;
            if (level.reward != null || freshPrint || CraftCatalog.Any(mats))
            {
                app.Show(ScreenId.Reward);
                return;
            }
            if (app.GmPlay)
            {
                LevelDef gmNext = app.LevelAt(level.index + 1);
                if (gmNext == null)
                {
                    app.Notice = "GM 第 " + level.index + " 关过了";
                    app.Show(ScreenId.Home);
                    return;
                }
                app.CurrentLevel = gmNext;
                level = gmNext;
                TrackLevelStart();
                Deal(level.seed, true);
                return;
            }
            GoNext(mats);
        }

        /// <summary>过关直接接下一关，不回选关页。体力不够或全部通关就回首页。</summary>
        void GoNext(int[] gained)
        {
            string got = CraftCatalog.Any(gained) ? "获得 " + CraftCatalog.Describe(gained) + "\n" : "";
            LevelDef next = app.NextLevel;
            if (next == null)
            {
                app.Notice = got + "全部 " + app.LevelCount + " 关都通关啦";
                app.Show(ScreenId.Home);
                return;
            }
            if (!app.Wardrobe.SpendEnergy())
            {
                app.Notice = got + "体力不够了";
                app.OfferEnergyAd = true;
                app.Show(ScreenId.Home);
                return;
            }
            app.CurrentLevel = next;
            level = next;
            TrackLevelStart();
            Deal(level.seed, true);
            CraftView.ShowGain(this, root, app.Database, gained);
        }

        void OnUndo()
        {
            if (board.Busy) return;
            if (!logic.CanUndo)
            {
                Sfx.Play(SfxId.Deny);
                hud.ShowToast("没有可撤回的步骤");
                return;
            }
            CancelSwap();
            logic.Undo();
            board.Refresh();
            finished = logic.OutOfMoves || logic.AlarmRang;
            CloseFail();
            if (!finished)
                failShown = false;
            UpdateHud();
            Sfx.Play(SfxId.Undo);
            hud.ShowToast("已撤回一步");
        }

        void OnShuffle()
        {
            if (board.Busy || !ToolsOpen) return;
            if (logic == null || !logic.CanShuffleUnsolved())
            {
                Sfx.Play(SfxId.Deny);
                hud.ShowToast("没有还能打乱的列");
                return;
            }
            if (shufflesLeft <= 0)
            {
                OfferToolAd(false);
                return;
            }
            DoShuffle();
        }

        void DoShuffle()
        {
            if (logic == null || !logic.CanShuffleUnsolved())
            {
                SyncTools();
                UpdateHud();
                hud.ShowToast("没有还能打乱的列");
                return;
            }
            if (app.Wardrobe == null || !app.Wardrobe.TrySpendShuffle())
            {
                SyncTools();
                UpdateHud();
                return;
            }
            shuffleSerial++;
            logic.ShuffleUnsolved(level.seed + shuffleSerial * 104729);
            board.Refresh();
            UpdateHud();
            Sfx.Play(SfxId.Shuffle);
            hud.ShowToast("没排好的列打乱了");
            if (logic.IsWin())
            {
                finished = true;
                hud.ShowToast("");
                StartCoroutine(ShuffleWin());
            }
        }

        IEnumerator ShuffleWin()
        {
            yield return board.Finale("整理完成！");
            Complete();
        }

        void OnSwap()
        {
            if (board.Busy || !ToolsOpen) return;
            if (board.PickMode)
            {
                CancelSwap();
                Sfx.Play(SfxId.Tap);
                hud.ShowToast("");
                return;
            }
            if (swapsLeft <= 0)
            {
                OfferToolAd(true);
                return;
            }
            board.PickMode = true;
            hud.SetSwapArmed(true);
            Sfx.Play(SfxId.Tap);
            hud.ShowToast("点架上任意一件，和手里这件交换");
        }

        void CancelSwap()
        {
            if (board != null)
                board.PickMode = false;
            if (hud != null)
                hud.SetSwapArmed(false);
        }

        void OnSwapPick(int column, int row)
        {
            if (board.Busy || !ToolsOpen) return;
            if (!logic.CanSwap(column, row))
            {
                Sfx.Play(SfxId.Deny);
                if (logic.IsCovered(column))
                    hud.ShowToast("防尘罩还没拉开");
                else if (logic.IsLocked(column))
                    hud.ShowToast("这列还锁着");
                else if (logic.HasKey(column, row) || logic.AlarmLeft(column, row) >= 0)
                    hud.ShowToast("挂钥匙和闹钟的要靠顶出来，换不了");
                else if (logic.IsWrapped(column, row))
                    hud.ShowToast("包裹还没拆开，换不了");
                else
                    hud.ShowToast(row < 0 ? "这列是空的" : "两件一样，换了没变化");
                return;
            }
            if (app.Wardrobe == null || !app.Wardrobe.TrySpendSwap())
            {
                CancelSwap();
                SyncTools();
                UpdateHud();
                hud.ShowToast("交换次数用完了");
                return;
            }
            CancelSwap();
            SyncTools();
            StartCoroutine(SwapRoutine(column, row));
        }

        void OnStepsAd()
        {
            if (stepsAdUsed || toolAdBusy || logic == null || clearing) return;
            if (logic.IsWin() || logic.AlarmRang) return;
            if (logic.MoveLimit - logic.Steps > 5) return;
            int ticket = dealSerial;
            toolAdBusy = true;
            if (hud != null && hud.failAdButton != null)
                hud.failAdButton.interactable = false;
            const string scene = "steps";
            Analytics.AdRequest(scene, WxBridge.ToolAdUnitId);
            WxBridge.ShowRewarded(WxBridge.ToolAdUnitId, () =>
            {
                toolAdBusy = false;
                if (ticket != dealSerial || logic == null || hud == null) return;
                stepsAdUsed = true;
                logic.GrantMoves(5);
                CloseFail();
                if (!logic.OutOfMoves && !logic.IsWin() && !logic.AlarmRang)
                    finished = false;
                Analytics.AdShow(scene, WxBridge.ToolAdUnitId);
                Analytics.AdClose(scene, WxBridge.ToolAdUnitId, true);
                UpdateHud();
                Sfx.Play(SfxId.Heart);
                hud.ShowToast("加了 5 步");
            }, () =>
            {
                toolAdBusy = false;
                if (ticket != dealSerial || hud == null) return;
                if (hud.failAdButton != null)
                    hud.failAdButton.interactable = true;
                Analytics.AdError(scene, WxBridge.ToolAdUnitId, "not_ended");
                Sfx.Play(SfxId.Deny);
                if (failOpen)
                    hud.ShowFail("步数用完了", "广告没看完", true);
                else
                    hud.ShowToast("广告没看完");
            });
        }

        void OfferToolAd(bool swap)
        {
            if (toolAdBusy || !ToolsOpen) return;
            toolAdBusy = true;
            string scene = swap ? "tool_swap" : "tool_shuffle";
            Analytics.AdRequest(scene, WxBridge.ToolAdUnitId);
            WxBridge.ShowRewarded(WxBridge.ToolAdUnitId, () =>
            {
                toolAdBusy = false;
                if (app.Wardrobe != null)
                {
                    if (swap)
                        app.Wardrobe.GrantSwap();
                    else
                        app.Wardrobe.GrantShuffle();
                }
                SyncTools();
                UpdateHud();
                Analytics.AdShow(scene, WxBridge.ToolAdUnitId);
                Analytics.AdClose(scene, WxBridge.ToolAdUnitId, true);
                if (!ToolsOpen || board == null)
                {
                    hud.ShowToast(swap ? "得到一次交换，今天有效" : "得到一次随机，今天有效");
                    return;
                }
                if (swap)
                {
                    board.PickMode = true;
                    hud.SetSwapArmed(true);
                    hud.ShowToast("得到一次交换，点一件和手里的换");
                    return;
                }
                if (board.Busy)
                    hud.ShowToast("得到一次随机，今天有效");
                else
                    DoShuffle();
            }, () =>
            {
                toolAdBusy = false;
                Analytics.AdError(scene, WxBridge.ToolAdUnitId, "not_ended");
                Sfx.Play(SfxId.Deny);
                hud.ShowToast("广告没看完");
            });
        }

        IEnumerator SwapRoutine(int column, int row)
        {
            yield return board.PlaySwap(column, row);
            UpdateHud();
            hud.ShowToast(logic.LastOpened.Length > 0 ? "交换成功，防尘罩拉开了" : "交换成功");
            if (logic.IsWin())
            {
                finished = true;
                hud.ShowToast("");
                yield return board.Finale("整理完成！");
                Complete();
            }
        }

        void OnRestart()
        {
            if (board.Busy) return;
            hud.ShowPause(false);
            if (level != null && logic != null && !logic.IsWin())
            {
                if (!finished)
                    Analytics.LevelFail("restart", LevelProgress());
                else if (logic.OutOfMoves || logic.AlarmRang)
                    Analytics.LevelFail(logic.AlarmRang ? "alarm" : "out_of_moves", LevelProgress());
            }
            TrackLevelStart();
            stepsAdUsed = false;
            shuffleSerial = 0;
            Deal(level.seed);
            Sfx.Play(SfxId.Shuffle);
            hud.ShowToast("重新开始");
        }

        void UpdateHud(bool animate = true)
        {
            if (hud == null || level == null) return;
            if (hud.levelLabel != null)
                hud.levelLabel.text = (app.GmPlay ? "GM 第 " : "第 ") + level.index + " 关";
            int left = Mathf.Max(0, logic.MoveLimit - logic.Steps);
            hud.SetSteps(left, animate && Application.isPlaying);
            hud.SetCounts(swapsLeft, shufflesLeft);
            bool offerSteps = !failOpen && !stepsAdUsed && left > 0 && left <= 5
                && !logic.IsWin() && !logic.AlarmRang;
            hud.SetStepsAd(offerSteps);
        }

        void SyncTools()
        {
            swapsLeft = app != null && app.Wardrobe != null ? app.Wardrobe.SwapsToday : 0;
            shufflesLeft = app != null && app.Wardrobe != null ? app.Wardrobe.ShufflesToday : 0;
        }

        bool ToolsOpen => logic != null && !clearing && !logic.IsWin() && !logic.AlarmRang;

        string StuckText()
        {
            return stepsAdUsed
                ? "步数用完了，撤回或者重新开始"
                : "步数用完了，点步数下的广告加 5 步";
        }

        void TrackLevelStart()
        {
            if (level == null) return;
            Analytics.LevelStart(level.index, "第" + level.index + "关");
        }

        float LevelProgress()
        {
            if (level == null || level.moveLimit <= 0 || logic == null) return 0f;
            return Mathf.Clamp01((float)logic.Steps / level.moveLimit);
        }
    }
}
