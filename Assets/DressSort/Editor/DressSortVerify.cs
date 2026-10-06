using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DressSort.EditorTools
{
    /// <summary>
    /// batchmode 下的自检：跑一遍玩法逻辑，再把每个页面渲到 RenderTexture 截图。
    /// 不进播放模式，靠 Canvas 走 ScreenSpaceCamera + Camera.Render 直接出图。
    /// </summary>
    public static class DressSortVerify
    {
        const int ShotWidth = 540;
        const int ShotHeight = 960;
        const string ShotDir = "Screenshots";
        const string DatabasePath = "Assets/DressSort/Data/GameDatabase.asset";

        [MenuItem("叠叠裙/跑自检并截图", priority = 2)]
        public static void Run()
        {
            var log = new StringBuilder();
            Directory.CreateDirectory(ShotDir);

            try
            {
                DressSortBuilder.RebuildAll();

                var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
                log.AppendLine(LogicReport(database));
                CaptureScreens(database, log);
            }
            catch (System.Exception error)
            {
                log.AppendLine("!! 自检中断：" + error);
            }
            finally
            {
                string text = log.ToString();
                Debug.Log("[自检]\n" + text);
                File.WriteAllText(Path.Combine(ShotDir, "verify.txt"), text);
            }
        }

        // ----------------------------------------------------------- 玩法自检

        static string LogicReport(GameDatabase database)
        {
            var sb = new StringBuilder();
            sb.AppendLine("== 玩法自检 ==");

            sb.AppendLine(LevelReport(database, LevelCatalog.Count, 30));
            sb.AppendLine(MechanicsReport());

            // shift 机制与撤回的对称性
            var probe = new SortBoard(5, 6, 5, 99);
            probe.Deal(20, 777);
            var before = Snapshot(probe);
            probe.Play(2);
            probe.Undo();
            sb.AppendLine("撤回还原局面：" + (Snapshot(probe) == before ? "通过" : "失败"));

            // 已解状态判胜
            var solved = new SortBoard(5, 6, 5, 99);
            solved.Deal(0, 1);
            sb.AppendLine("已解状态判胜：" + (solved.IsWin() ? "通过" : "失败"));

            // 手里落到中间后自动沉底；再点一次就能顶出异款。撤回要连沉底一起还原。
            var rotate = new SortBoard(5, 5, 5, 99);
            rotate.Load(new[]
            {
                new[] { 1, 0, 0, 0, 0 },
                new[] { 1, 1, 1, 1, 1 },
                new[] { 2, 2, 2, 2, 2 },
                new[] { 3, 3, 3, 3, 3 },
                new[] { 4, 4, 4, 4, 4 },
            }, 0);
            string rotateBefore = Snapshot(rotate);
            bool willRotate = rotate.TryAutoRotate(0, out int oddIndex);
            SortBoard.Settle[] settled = rotate.SettleReady();
            bool sank = settled.Length == 1 && settled[0].Column == 0 && settled[0].FromIndex == 0
                && rotate.Column(0)[4] == 1 && rotate.Held == 0;
            rotate.Play(0);
            bool ejectedOdd = rotate.Held == 1 && rotate.IsColumnSolved(0);
            rotate.Undo();
            bool undoAfterSettlePlay = Snapshot(rotate) == SnapshotAfterSettle();
            rotate.Load(new[]
            {
                new[] { 1, 0, 0, 0, 0 },
                new[] { 1, 1, 1, 1, 1 },
                new[] { 2, 2, 2, 2, 2 },
                new[] { 3, 3, 3, 3, 3 },
                new[] { 4, 4, 4, 4, 4 },
            }, 0);
            rotate.Play(0);
            rotate.Undo();
            sb.AppendLine("落到中间自动换位：" + (willRotate && oddIndex == 0 && sank ? "通过" : "失败"));
            sb.AppendLine("换位后一击顶出：" + (ejectedOdd ? "通过" : "失败"));
            sb.AppendLine("沉底后再交换撤回：" + (undoAfterSettlePlay ? "通过" : "失败"));
            sb.AppendLine("交换连同沉底撤回：" + (Snapshot(rotate) == rotateBefore ? "通过" : "失败"));
            sb.AppendLine("已叠好的列不可点：" + (!rotate.CanPlay(1) && rotate.IsColumnSolved(1) ? "通过" : "失败"));

            string SnapshotAfterSettle()
            {
                var ready = new SortBoard(5, 5, 5, 99);
                ready.Load(new[]
                {
                    new[] { 1, 0, 0, 0, 0 },
                    new[] { 1, 1, 1, 1, 1 },
                    new[] { 2, 2, 2, 2, 2 },
                    new[] { 3, 3, 3, 3, 3 },
                    new[] { 4, 4, 4, 4, 4 },
                }, 0);
                ready.SettleReady();
                return Snapshot(ready);
            }
            return sb.ToString();
        }

        static string Snapshot(SortBoard board)
        {
            var sb = new StringBuilder();
            sb.Append(board.Held).Append('|');
            for (int c = 0; c < board.ColumnCount; c++)
            {
                IReadOnlyList<int> column = board.Column(c);
                for (int r = 0; r < column.Count; r++)
                    sb.Append(column[r]).Append(',');
                sb.Append(';');
            }
            return sb.ToString();
        }

        static SortBoard DealFor(LevelDef level, int seed)
        {
            var board = new SortBoard(level.columns, level.columnHeight, level.columns, level.moveLimit);
            board.SetCovers(level.dustCovers);
            board.SetLocks(level.locks);
            board.SetTargets(level.targets);
            board.Deal(level.scrambleMoves, seed, level.parcels, level.alarms);
            return board;
        }

        /// <summary>
        /// 每关只发它固定种子那一盘，按倒推走法正着走一遍确认能解。
        /// 前 detail 关逐关列出，后面按每 100 关汇总。
        /// </summary>
        public static string LevelReport(GameDatabase database, int count, int detail)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"关卡数 {count}，物品数 {database.items.Count}");
            int failed = 0;
            for (int i = 1; i <= count; i++)
            {
                LevelDef level = LevelCatalog.Get(database, i);
                if (level == null || !level.IsValid)
                {
                    sb.AppendLine($"第{i}关 配置不完整");
                    failed++;
                    continue;
                }
                bool verified = DealFor(level, level.seed).Verified;
                bool solved = ReplaySolve(level, level.seed, out int steps);
                bool ok = verified && solved && steps <= level.moveLimit;
                if (!ok)
                {
                    failed++;
                    sb.AppendLine($"!! 第{i}关 固定盘解不开");
                }

                if (i <= detail || !ok)
                {
                    sb.AppendLine(
                        $"第{i}关 {level.columns}列x{level.columnHeight} 打乱{level.scrambleMoves} 上限{level.moveLimit} " +
                        $"包裹{level.parcels} 罩[{string.Join(",", level.dustCovers)}] 锁[{string.Join(",", level.locks)}] " +
                        $"专属[{string.Join(",", level.targets)}] 闹钟{level.alarms} 正解{steps}步 " +
                        $"奖励{(level.reward != null ? level.reward.displayName : "无")}");
                }
                if (i % 100 == 0)
                    sb.AppendLine(SummaryFor(database, i - 99, i));
            }
            sb.AppendLine($"固定盘全部可解：{(failed == 0 ? "通过" : "失败 " + failed + " 关")}");
            return sb.ToString();
        }

        static string SummaryFor(GameDatabase database, int from, int to)
        {
            int cells = 0, parcels = 0, covers = 0, locks = 0, targets = 0, alarms = 0, eight = 0;
            for (int i = from; i <= to; i++)
            {
                LevelDef l = LevelCatalog.Get(database, i);
                cells += l.columns * l.columnHeight;
                if (l.columns == 8) eight++;
                if (l.parcels > 0) parcels++;
                if (l.HasCovers) covers++;
                if (l.HasLocks) locks++;
                if (l.HasTargets) targets++;
                if (l.alarms > 0) alarms++;
            }
            int n = to - from + 1;
            return $"-- 第{from}-{to}关：平均{cells / n}件 8列{eight}关 包裹{parcels} 防尘罩{covers} 锁{locks} 专属{targets} 闹钟{alarms}";
        }

        /// <summary>按发牌时倒推的走法正着走一遍，闹钟响了也算失败。</summary>
        static bool ReplaySolve(LevelDef level, int seed, out int steps)
        {
            SortBoard board = DealFor(level, seed);
            var recipe = new List<int>(board.Recipe);
            foreach (int column in recipe)
            {
                if (board.IsWin()) break;
                if (!board.CanPlay(column))
                {
                    steps = 0;
                    return false;
                }
                board.Play(column);
                if (board.AlarmRang)
                {
                    steps = board.Steps;
                    return false;
                }
            }
            steps = board.Steps;
            return board.IsWin();
        }

        /// <summary>锁、钥匙、闹钟、专属列的规则单测。</summary>
        public static string MechanicsReport()
        {
            var sb = new StringBuilder();

            var locked = new SortBoard(3, 3, 3, 99);
            locked.SetLocks(new[] { 0, 0, 1 });
            locked.Load(new[]
            {
                new[] { 0, 0, 1 },
                new[] { 1, 1, 0 },
                new[] { 2, 2, 2 },
            }, 3);
            locked.PutKey(0, 2);
            bool blocked = !locked.CanPlay(2) && locked.IsLocked(2);
            locked.Play(0);
            bool opened = !locked.IsLocked(2) && locked.LastUnlocked.Length == 1 && locked.LastUnlocked[0] == 2;
            locked.Undo();
            sb.AppendLine("锁住的列不能点，顶出钥匙开锁，撤回不再上锁：" +
                (blocked && opened && !locked.IsLocked(2) ? "通过" : "失败"));

            var alarm = new SortBoard(3, 3, 3, 99);
            alarm.Load(new[]
            {
                new[] { 0, 1, 0 },
                new[] { 1, 0, 1 },
                new[] { 2, 2, 2 },
            }, 3);
            alarm.PutAlarm(1, 2, 2);
            bool counting = alarm.AlarmLeft(1, 2) == 2;
            alarm.Play(0);
            bool tick = alarm.AlarmLeft(1, 2) == 1 && !alarm.AlarmRang;
            alarm.Play(0);
            bool rang = alarm.AlarmRang && !alarm.CanPlay(1);
            alarm.Undo();
            alarm.Play(1);
            bool alarmOff = !alarm.AlarmRang && alarm.LastAlarmOff;
            sb.AppendLine("闹钟倒数、响铃、撤回后顶出关掉：" + (counting && tick && rang && alarmOff ? "通过" : "失败"));

            var target = new SortBoard(2, 2, 2, 99);
            target.SetTargets(new[] { 1, 0 });
            target.Load(new[]
            {
                new[] { 1, 1 },
                new[] { 0, 0 },
            }, 2);
            bool wrongNotSolved = !target.IsColumnSolved(0) && target.IsColumnSolved(1) && !target.IsWin();
            target.Load(new[]
            {
                new[] { 0, 0 },
                new[] { 1, 1 },
            }, 2);
            sb.AppendLine("专属列只认指定款：" + (wrongNotSolved && target.IsWin() ? "通过" : "失败"));

            var same1 = new SortBoard(6, 6, 6, 120);
            same1.SetLocks(new[] { 0, 1, 0, 0, 0, 0 });
            same1.Deal(40, 4242, 4, 2);
            var same2 = new SortBoard(6, 6, 6, 120);
            same2.SetLocks(new[] { 0, 1, 0, 0, 0, 0 });
            same2.Deal(40, 4242, 4, 2);
            sb.AppendLine("同一种子发同一盘：" + (Snapshot(same1) == Snapshot(same2) && same1.Verified ? "通过" : "失败"));
            return sb.ToString();
        }

        // ------------------------------------------------------------- 截图

        static void CaptureScreens(GameDatabase database, StringBuilder log)
        {
            log.AppendLine("== 截图 ==");

            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 建新场景会顺手卸掉没人引用的资产，之前拿到的引用会变成已销毁的空壳，
            // 所以这里必须重新按路径取一次
            database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            log.AppendLine($"重载数据库：物品 {database.items.Count}，关卡 {LevelCatalog.Count}");

            var rt = new RenderTexture(ShotWidth, ShotHeight, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 2,
            };

            var cameraGo = new GameObject("Capture", typeof(Camera));
            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Sky;
            camera.targetTexture = rt;
            cameraGo.transform.position = new Vector3(0f, 0f, -100f);

            var appGo = new GameObject("App");
            var app = appGo.AddComponent<App>();
            app.Boot(camera, database);
            app.Wardrobe.Wipe();
            app.Show(ScreenId.Home);

            Shoot(app, camera, rt, ScreenId.Home, "home", log);

            app.CurrentLevel = app.LevelAt(1);
            app.Show(ScreenId.Game);
            var game = app.PanelOf<GamePanel>(ScreenId.Game);
            game.DealWithSeed(20260916);
            Shoot(app, camera, rt, ScreenId.Game, "game-start", log);

            // 走几步，确认插入与顶出的叠放关系正确
            game.StepImmediate(0);
            game.StepImmediate(0);
            game.StepImmediate(3);
            Shoot(app, camera, rt, ScreenId.Game, "game-shift", log);

            // 通关演出：先看未开封的礼盒，再看翻开后的结果
            app.PendingLevel = app.LevelAt(4);
            app.Show(ScreenId.Reward);
            Shoot(app, camera, rt, ScreenId.Reward, "reward", log);

            app.PanelOf<RewardPanel>(ScreenId.Reward).ApplyReveal();
            Shoot(app, camera, rt, ScreenId.Reward, "reward-open", log);

            // 全解锁一遍，好让衣柜页有东西可看
            foreach (ItemDef item in database.items)
                app.Wardrobe.Unlock(item);
            app.Wardrobe.Equip(database.Find("strawberry"));
            app.Wardrobe.Equip(database.Find("wing_aqua"));

            app.Show(ScreenId.DressUp);
            Shoot(app, camera, rt, ScreenId.DressUp, "dressup", log);

            app.Show(ScreenId.Home);
            Shoot(app, camera, rt, ScreenId.Home, "home-equipped", log);

            camera.targetTexture = null;
            Object.DestroyImmediate(rt);
        }

        static void Shoot(App app, Camera camera, RenderTexture rt, ScreenId id, string name,
            StringBuilder log)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;

            string path = Path.Combine(ShotDir, "dresssort-" + name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            log.AppendLine($"{id} -> {path}");
        }
    }
}
