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

            List<LevelDef> levels = database.AllLevels();
            sb.AppendLine($"关卡数 {levels.Count}，物品数 {database.items.Count}");

            foreach (LevelDef level in levels)
            {
                if (!level.IsValid)
                {
                    sb.AppendLine($"第{level.index}关 配置不完整");
                    continue;
                }

                // 逆向打乱生成的局面，把打乱步骤正着走回去一定能解开
                var board = new SortBoard(level.columns, level.columnHeight, level.columns,
                    level.moveLimit);
                board.Deal(level.scrambleMoves, 12345 + level.index);

                int total = 0;
                for (int c = 0; c < board.ColumnCount; c++)
                    total += board.CountIn(c);

                bool solvable = BruteSolve(level, 12345 + level.index, out int usedSteps);

                sb.AppendLine(
                    $"第{level.index}关 {level.columns}列x{level.columnHeight} " +
                    $"牌数{total} 手里{(board.Held == board.MysteryIndex ? "问号" : board.Held.ToString())} " +
                    $"步数上限{level.moveLimit} 求解{(solvable ? $"成功({usedSteps}步)" : "失败")} " +
                    $"奖励{(level.reward != null ? level.reward.displayName : "无")}");
            }

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

        /// <summary>
        /// 贪心加随机重启的求解器，只用来确认局面在步数上限内确实能解开，
        /// 不是给玩家用的提示功能。
        /// </summary>
        static bool BruteSolve(LevelDef level, int seed, out int steps)
        {
            var random = new System.Random(seed);
            for (int attempt = 0; attempt < 400; attempt++)
            {
                var board = new SortBoard(level.columns, level.columnHeight, level.columns,
                    level.moveLimit);
                board.Deal(level.scrambleMoves, seed);

                for (int move = 0; move < level.moveLimit; move++)
                {
                    if (board.IsWin())
                    {
                        steps = board.Steps;
                        return true;
                    }

                    int best = -1;
                    int bestScore = int.MinValue;
                    for (int c = 0; c < board.ColumnCount; c++)
                    {
                        IReadOnlyList<int> column = board.Column(c);
                        int score = 0;
                        // 优先放进已经堆着同款的列，并且别把整理好的列打散
                        for (int r = 0; r < column.Count; r++)
                        {
                            if (column[r] == board.Held) score += 3;
                        }
                        if (column.Count > 0 && column[0] == board.Held) score += 6;
                        if (board.IsColumnSolved(c)) score -= 40;
                        score += random.Next(0, 5);

                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = c;
                        }
                    }

                    if (best < 0 || !board.CanPlay(best)) break;
                    board.Play(best);
                }

                if (board.IsWin())
                {
                    steps = board.Steps;
                    return true;
                }
            }

            steps = 0;
            return false;
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
            log.AppendLine($"重载数据库：物品 {database.items.Count}，章节 {database.chapters.Count}，" +
                $"关卡 {database.AllLevels().Count}");

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

            app.Show(ScreenId.LevelMap);
            Shoot(app, camera, rt, ScreenId.LevelMap, "levelmap", log);

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
