using System;
using System.Collections.Generic;
using UnityEngine;

namespace DressSort
{
    /// <summary>
    /// 全部关卡。前 30 关手排，教会规则、逐个引入机制；之后按关卡号推出参数。
    /// 每关的种子固定，同一关永远是同一盘，不存关卡资产。
    /// </summary>
    public static class LevelCatalog
    {
        public const int Count = 1000;

        struct Plan
        {
            public int columns;
            public int height;
            public int scramble;
            public int limit;
            public int parcels;
            public int alarms;
            public int[] covers;
            public int[] locks;
            public int[] targets;
        }

        static Plan P(int columns, int height, int scramble, int limit,
            int parcels = 0, int[] covers = null, int[] locks = null, int[] targets = null, int alarms = 0)
        {
            return new Plan
            {
                columns = columns,
                height = height,
                scramble = scramble,
                limit = limit,
                parcels = parcels,
                covers = covers,
                locks = locks,
                targets = targets,
                alarms = alarms,
            };
        }

        // 第 6 关包裹，第 11 关防尘罩，第 14 关专属列，第 19 关锁和钥匙，第 23 关限时闹钟。
        // 新机制先单独出两关，再和大盘、别的机制混着来。奖励见 CraftCatalog。
        static readonly Plan[] Authored =
        {
            P(4, 4, 8, 24),
            P(5, 5, 14, 36),
            P(4, 5, 16, 36),
            P(5, 5, 20, 42),
            P(5, 6, 24, 50),
            P(5, 5, 20, 44, parcels: 4),
            P(5, 6, 26, 54, parcels: 6),
            P(6, 6, 30, 60),
            P(6, 6, 32, 62, parcels: 6),
            P(6, 6, 34, 66, parcels: 10),
            P(5, 5, 24, 52, covers: new[] { 0, 0, 1, 0, 0 }),
            P(5, 5, 28, 56, covers: new[] { 2, 0, 1, 0, 0 }),
            P(7, 6, 38, 72),
            P(5, 6, 26, 56, targets: new[] { 1, 0, 0, 0, 1 }),
            P(6, 6, 32, 64, parcels: 6, targets: new[] { 0, 1, 0, 0, 1, 0 }),
            P(7, 7, 42, 78),
            P(6, 6, 34, 66, covers: new[] { 0, 1, 0, 0, 2, 0 }),
            P(7, 7, 40, 78, parcels: 8, targets: new[] { 1, 0, 0, 1, 0, 0, 1 }),
            P(5, 6, 26, 58, locks: new[] { 0, 0, 1, 0, 0 }),
            P(6, 6, 32, 66, parcels: 6, locks: new[] { 0, 1, 0, 0, 0, 0 }),
            P(8, 7, 46, 84),
            P(6, 7, 38, 74, locks: new[] { 0, 0, 1, 0, 1, 0 }),
            P(5, 6, 28, 60, alarms: 1),
            P(6, 6, 32, 66, parcels: 4, alarms: 1),
            P(7, 7, 42, 80, covers: new[] { 0, 0, 0, 1, 0, 0, 0 }, targets: new[] { 0, 1, 0, 0, 0, 1, 0 }),
            P(6, 7, 38, 74, alarms: 2),
            P(7, 7, 44, 82, locks: new[] { 0, 0, 0, 1, 0, 0, 0 }, alarms: 1),
            P(8, 8, 52, 92, parcels: 12),
            P(7, 8, 50, 90, parcels: 8, locks: new[] { 0, 0, 1, 0, 0, 0, 0 }, targets: new[] { 1, 0, 0, 0, 0, 0, 1 }),
            P(8, 8, 56, 98, parcels: 8, covers: new[] { 0, 0, 1, 0, 0, 2, 0, 0 }, alarms: 1),
        };

        /// <summary>棋盘用的裙子，顺序固定，关卡按关号错开取连续几款。</summary>
        static readonly string[] DressOrder =
        {
            "teal_sailor", "black_ribbon", "pink_gingham", "lemon_print",
            "orange_slice", "ivory_lace", "strawberry", "grape_school",
        };

        public static void CopyBoardDressIds(List<string> dst)
        {
            for (int i = 0; i < DressOrder.Length; i++)
                dst.Add(DressOrder[i]);
        }

        static readonly Dictionary<int, LevelDef> cache = new Dictionary<int, LevelDef>();
        static GameDatabase cachedFor;

        public static LevelDef Get(GameDatabase db, int index)
        {
            if (db == null || index < 1 || index > Count) return null;
            if (cachedFor != db)
            {
                cache.Clear();
                cachedFor = db;
            }
            if (cache.TryGetValue(index, out LevelDef level) && level != null)
                return level;

            Plan plan = PlanFor(index);
            level = ScriptableObject.CreateInstance<LevelDef>();
            level.hideFlags = HideFlags.DontSave;
            level.name = "Level" + index;
            level.index = index;
            level.columns = plan.columns;
            level.columnHeight = plan.height;
            level.scrambleMoves = plan.scramble;
            level.moveLimit = plan.limit;
            level.seed = index * 7349 + 101;
            level.shuffles = 3;
            level.swaps = 2;
            level.parcels = plan.parcels;
            level.alarms = plan.alarms;
            level.dustCovers = plan.covers != null ? new List<int>(plan.covers) : new List<int>();
            level.locks = plan.locks != null ? new List<int>(plan.locks) : new List<int>();
            level.targets = plan.targets != null ? new List<int>(plan.targets) : new List<int>();
            level.palette = new List<ItemDef>();
            int start = (index * 3) % DressOrder.Length;
            for (int i = 0; i < plan.columns; i++)
                level.palette.Add(db.Find(DressOrder[(start + i) % DressOrder.Length]));
            level.mystery = db.mysteryItem;
            CraftCatalog.LevelReward reward = CraftCatalog.RewardFor(index);
            ItemDef item = reward.itemId != null ? db.Find(reward.itemId) : null;
            level.reward = reward.kind == CraftCatalog.Kind.Gift ? item : null;
            level.blueprint = reward.kind != CraftCatalog.Kind.Gift ? item : null;
            cache[index] = level;
            return level;
        }

        /// <summary>机制第一次出现的那关给一句说明，其余关卡给通用提示。</summary>
        public static string IntroFor(LevelDef def)
        {
            if (def == null) return "";
            int i = def.index;
            if (i == FirstWith(p => p.alarms > 0))
                return "新机制：挂闹钟的裙子，数字归零前要把它顶出来";
            if (i == FirstWith(p => Any(p.locks)))
                return "新机制：锁住的列先别急，顶出带钥匙的那件就能打开";
            if (i == FirstWith(p => Any(p.targets)))
                return "新机制：列顶气泡里是哪款，这列就只能叠那款";
            if (i == FirstWith(p => Any(p.covers)))
                return "新机制：防尘罩里的列，叠好上面数字那么多列就会拉开";
            if (i == FirstWith(p => p.parcels > 0))
                return "新机制：包裹落到最下面才会拆开";
            if (i % 10 == 0 && i > Authored.Length)
                return "难关来了，步数要省着用";
            return "点一列，手里这件插到最上";
        }

        static int FirstWith(Predicate<Plan> match)
        {
            for (int i = 0; i < Authored.Length; i++)
            {
                if (match(Authored[i])) return i + 1;
            }
            return -1;
        }

        static bool Any(int[] flags)
        {
            if (flags == null) return false;
            for (int i = 0; i < flags.Length; i++)
            {
                if (flags[i] > 0) return true;
            }
            return false;
        }

        static Plan PlanFor(int index)
        {
            if (index <= Authored.Length)
                return Authored[index - 1];
            return Procedural(index);
        }

        /// <summary>
        /// 第 31 关以后：每 100 关整体加难一档；每 10 关的第 10 关是难关，
        /// 紧接着的两关松一口气。机制从已经教过的五种里随机组合，越往后叠得越多。
        /// </summary>
        static Plan Procedural(int index)
        {
            var rng = new System.Random(index * 92821 + 17);
            int tier = Math.Min(9, (index - Authored.Length - 1) / 100);
            int beat = index % 10;
            bool hard = beat == 0;
            bool relax = beat == 1 || beat == 2;

            float d = 0.4f + tier * 0.058f;
            if (hard) d += 0.2f;
            else if (relax) d -= 0.15f;
            d += (float)(rng.NextDouble() - 0.5) * 0.12f;
            d = Mathf.Clamp(d, 0.1f, 1f);

            int columns = Mathf.Clamp(5 + Mathf.RoundToInt(d * 3f + (float)(rng.NextDouble() - 0.5)), 5, 8);
            int height = Mathf.Clamp(5 + Mathf.RoundToInt(d * 3f + (float)(rng.NextDouble() - 0.5)), 5, 8);
            int cells = columns * height;
            int scramble = Mathf.RoundToInt(cells * (0.8f + 0.4f * d));
            int limit = scramble + Mathf.RoundToInt(scramble * (0.9f - 0.35f * d)) + 6;

            int want = hard ? 2 : relax ? 0 : 1;
            if (rng.NextDouble() < 0.15 + tier * 0.06) want++;
            if (relax && rng.NextDouble() < 0.5) want++;
            want = Mathf.Clamp(want, 0, 3);

            var kinds = new List<int> { 0, 1, 2, 3, 4 };
            for (int i = kinds.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (kinds[i], kinds[j]) = (kinds[j], kinds[i]);
            }

            var plan = P(columns, height, scramble, limit);
            var taken = new bool[columns];
            for (int k = 0; k < want; k++)
            {
                switch (kinds[k])
                {
                    case 0:
                        plan.parcels = Mathf.RoundToInt(cells * (0.1f + 0.2f * d));
                        break;
                    case 1:
                    {
                        int count = 1 + (d > 0.55f ? 1 : 0) + (columns >= 8 && d > 0.8f ? 1 : 0);
                        plan.covers = new int[columns];
                        var needs = new List<int>();
                        for (int n = 1; n <= count; n++) needs.Add(n);
                        foreach (int need in needs)
                        {
                            int c = PickFree(rng, taken);
                            if (c < 0) break;
                            plan.covers[c] = need;
                        }
                        break;
                    }
                    case 2:
                    {
                        int count = 1 + Mathf.FloorToInt(d * 2.5f);
                        plan.targets = new int[columns];
                        for (int n = 0; n < count; n++)
                            plan.targets[rng.Next(columns)] = 1;
                        break;
                    }
                    case 3:
                    {
                        int count = 1 + (d > 0.6f && columns >= 6 ? 1 : 0);
                        plan.locks = new int[columns];
                        for (int n = 0; n < count; n++)
                        {
                            int c = PickFree(rng, taken);
                            if (c < 0) break;
                            plan.locks[c] = 1;
                        }
                        break;
                    }
                    default:
                        plan.alarms = 1 + (d > 0.55f ? 1 : 0) + (d > 0.85f ? 1 : 0);
                        break;
                }
            }
            // 气泡和锁、防尘罩都挂在衣架上，同一列只留一个。
            if (plan.targets != null)
            {
                for (int c = 0; c < columns; c++)
                {
                    if (taken[c]) plan.targets[c] = 0;
                }
            }
            return plan;
        }

        /// <summary>挑一列给防尘罩或锁，至少留三列空着，开局才有地方走。</summary>
        static int PickFree(System.Random rng, bool[] taken)
        {
            int free = 0;
            for (int i = 0; i < taken.Length; i++)
            {
                if (!taken[i]) free++;
            }
            if (free <= 3) return -1;
            int skip = rng.Next(free);
            for (int i = 0; i < taken.Length; i++)
            {
                if (taken[i]) continue;
                if (skip-- > 0) continue;
                taken[i] = true;
                return i;
            }
            return -1;
        }
    }
}
