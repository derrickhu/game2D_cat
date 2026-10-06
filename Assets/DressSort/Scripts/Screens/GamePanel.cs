using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    public class GamePanel : Panel
    {
        GameHud hud;
        BoardView board;
        SortBoard logic;
        LevelDef level;

        int shufflesLeft;
        int swapsLeft;
        bool finished;

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
                Debug.LogError("[叠叠裙] 关卡预制没有 GameHud");
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
                hud.BindMechanics(app.Database);
            }
            hud.Wire(OnSwap, OnUndo, OnShuffle,
                () => { CancelSwap(); hud.ShowPause(true); },
                () => hud.ShowPause(false),
                OnRestart,
                () => { hud.ShowPause(false); app.Show(ScreenId.Home); });

            board = BoardView.Create(hud.boardRoot != null ? hud.boardRoot : root,
                new BoardView.Layout { overlap = 0.70f });
            board.AttachHud(hud);
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
            Deal(level.seed);
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

        void Deal(int seed)
        {
            finished = false;
            shufflesLeft = level.shuffles;
            swapsLeft = level.swaps;
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
            UpdateHud();
            if (hud != null)
                hud.ShowToast(LevelCatalog.IntroFor(level));
        }

        void OnColumnClicked(int column)
        {
            if (finished || board.Busy) return;
            if (!logic.CanPlay(column))
            {
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
                        hud.ShowToast("步数用完了，撤回或者重新开始");
                    else
                        hud.ShowToast("手里没有可放的");
                }
                return;
            }
            StartCoroutine(PlayRoutine(column));
        }

        IEnumerator PlayRoutine(int column)
        {
            yield return board.PlayColumn(column);
            UpdateHud();
            if (logic.LastUnlocked.Length > 0)
                hud.ShowToast("钥匙打开了一把锁");
            else if (logic.LastAlarmOff)
                hud.ShowToast("闹钟关掉了");
            else if (logic.LastOpened.Length > 0)
                hud.ShowToast("防尘罩拉开了");

            if (logic.IsWin())
            {
                finished = true;
                hud.ShowToast("整理完成！");
                yield return new WaitForSeconds(0.45f);
                Complete();
                yield break;
            }

            if (logic.AlarmRang)
            {
                finished = true;
                hud.ShowToast("闹钟响了！撤回一步或者重新开始");
                yield break;
            }

            if (logic.OutOfMoves)
            {
                finished = true;
                hud.ShowToast("步数用完了，撤回或者重新开始");
            }
        }

        void Complete()
        {
            int spare = Mathf.Max(0, level.moveLimit - logic.Steps);
            int stars = 1 + Mathf.Clamp(spare / Mathf.Max(1, level.moveLimit / 3), 0, 2);

            app.Wardrobe.ReportCleared(level.index, stars);
            RankService.Submit(app.Wardrobe.LevelsCleared);
            app.PendingLevel = level;
            if (level.reward != null)
            {
                app.Show(ScreenId.Reward);
                return;
            }
            GoNext();
        }

        /// <summary>过关直接接下一关，不回选关页。体力不够或全部通关就回首页。</summary>
        void GoNext()
        {
            LevelDef next = app.NextLevel;
            if (next == null)
            {
                app.Notice = "全部 " + app.LevelCount + " 关都通关啦";
                app.Show(ScreenId.Home);
                return;
            }
            if (!app.Wardrobe.SpendEnergy())
            {
                app.Notice = "体力不足，休息一下再来第 " + next.index + " 关";
                app.Show(ScreenId.Home);
                return;
            }
            app.CurrentLevel = next;
            level = next;
            Deal(level.seed);
        }

        void OnUndo()
        {
            if (board.Busy) return;
            if (!logic.CanUndo)
            {
                hud.ShowToast("没有可撤回的步骤");
                return;
            }
            CancelSwap();
            logic.Undo();
            if (logic.LastUndoWasSwap)
                swapsLeft = Mathf.Min(level.swaps, swapsLeft + 1);
            board.Refresh();
            finished = false;
            UpdateHud();
            hud.ShowToast("已撤回一步");
        }

        void OnShuffle()
        {
            if (board.Busy) return;
            if (shufflesLeft <= 0 && !finished)
            {
                hud.ShowToast("随机次数用完了");
                return;
            }

            int keep = finished ? level.shuffles : shufflesLeft - 1;
            int swaps = swapsLeft;
            Deal(level.seed + (level.shuffles - keep) * 104729);
            shufflesLeft = keep;
            swapsLeft = swaps;
            UpdateHud();
            hud.ShowToast("重新洗牌");
        }

        void OnSwap()
        {
            if (board.Busy || finished) return;
            if (board.PickMode)
            {
                CancelSwap();
                hud.ShowToast("");
                return;
            }
            if (swapsLeft <= 0)
            {
                hud.ShowToast("交换次数用完了");
                return;
            }
            board.PickMode = true;
            hud.SetSwapArmed(true);
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
            if (board.Busy || finished) return;
            if (!logic.CanSwap(column, row))
            {
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
            CancelSwap();
            swapsLeft--;
            StartCoroutine(SwapRoutine(column, row));
        }

        IEnumerator SwapRoutine(int column, int row)
        {
            yield return board.PlaySwap(column, row);
            UpdateHud();
            hud.ShowToast(logic.LastOpened.Length > 0 ? "交换成功，防尘罩拉开了" : "交换成功");
            if (logic.IsWin())
            {
                finished = true;
                hud.ShowToast("整理完成！");
                yield return new WaitForSeconds(0.45f);
                Complete();
            }
        }

        void OnRestart()
        {
            if (board.Busy) return;
            hud.ShowPause(false);
            Deal(level.seed);
            hud.ShowToast("重新开始");
        }

        void UpdateHud()
        {
            if (hud == null || level == null) return;
            if (hud.levelLabel != null)
                hud.levelLabel.text = "第 " + level.index + " 关";
            if (hud.movesLabel != null)
                hud.movesLabel.text = "剩余 " + Mathf.Max(0, level.moveLimit - logic.Steps) + " 步";
            hud.SetCounts(swapsLeft, shufflesLeft);
        }
    }
}
