using System;
using System.Collections.Generic;

namespace DressSort
{
    public enum CraftMat
    {
        Cloth,
        Hair,
        DyeRed,
        DyeYellow,
        DyeBlue,
        DyeBlack,
        Lace,
        Stardust,
    }

    /// <summary>
    /// 关卡奖励与工坊配方。前 20 关每关直送一件；第 21 关起改发图纸和材料，
    /// 图纸在工坊用材料做成衣服。所有数值都在这里，调节奏只改这一个文件。
    /// </summary>
    public static class CraftCatalog
    {
        public const int MatCount = 8;
        public const int GiftLevels = 20;
        public const int WorkshopLevel = 21;

        public static readonly string[] MatNames = { "布匹", "发丝", "莓红染料", "柠黄染料", "湖蓝染料", "墨黑染料", "蕾丝", "星砂" };
        public static readonly string[] MatKeys = { "cloth", "hair", "dye_red", "dye_yellow", "dye_blue", "dye_black", "lace", "stardust" };

        /// <summary>第 1–20 关的直送。第 10、20 关送发型，其余送裙子。</summary>
        static readonly string[] Gifts =
        {
            "lemon_print", "mint_bow", "orange_slice", "sky_dot", "strawberry",
            "cherry_red", "ivory_lace", "peach_puff", "grape_school", "hair_caramel",
            "navy_stripe", "lilac_lace", "matcha_pinafore", "coral_sun", "cocoa_check",
            "lavender_star", "cream_cloud", "blueberry", "rose_velvet", "hair_baguette",
        };

        // 图纸按顺序发，发完的那类改送材料大礼包。画了新衣服接到对应队尾即可。
        static readonly string[] DressBlueprints = { "honey_bow" };
        static readonly string[] HairBlueprints = { "hair_milktea_long" };
        static readonly string[] WingBlueprints = { "wing_aqua", "wing_rose" };

        static int[] Need(int cloth = 0, int hair = 0, int red = 0, int yellow = 0, int blue = 0,
            int black = 0, int lace = 0, int star = 0)
        {
            return new[] { cloth, hair, red, yellow, blue, black, lace, star };
        }

        // 裙子都要布匹 8；染料按配色拆成红黄蓝黑四种基础色，花边多的加蕾丝。
        // 发型短发 5 份发丝、长发 9 份；翅膀要只在难关掉的星砂。
        static readonly Dictionary<string, int[]> Recipes = new Dictionary<string, int[]>
        {
            { "teal_sailor", Need(cloth: 8, blue: 3, yellow: 1) },
            { "black_ribbon", Need(cloth: 8, black: 4) },
            { "pink_gingham", Need(cloth: 8, red: 2, lace: 1) },
            { "lemon_print", Need(cloth: 8, yellow: 4, black: 1) },
            { "orange_slice", Need(cloth: 8, red: 2, yellow: 3) },
            { "ivory_lace", Need(cloth: 8, lace: 3) },
            { "strawberry", Need(cloth: 8, red: 4, lace: 1) },
            { "grape_school", Need(cloth: 8, red: 2, blue: 2, lace: 1) },
            { "mint_bow", Need(cloth: 8, yellow: 2, blue: 2) },
            { "sky_dot", Need(cloth: 8, blue: 3) },
            { "cherry_red", Need(cloth: 8, red: 5) },
            { "navy_stripe", Need(cloth: 8, blue: 4, black: 1) },
            { "peach_puff", Need(cloth: 8, red: 2, yellow: 2) },
            { "lilac_lace", Need(cloth: 8, red: 1, blue: 1, lace: 3) },
            { "matcha_pinafore", Need(cloth: 8, yellow: 3, blue: 1, black: 1) },
            { "coral_sun", Need(cloth: 8, red: 3, yellow: 3) },
            { "cocoa_check", Need(cloth: 8, red: 2, yellow: 2, black: 2) },
            { "lavender_star", Need(cloth: 8, red: 2, blue: 2, yellow: 1) },
            { "cream_cloud", Need(cloth: 8, blue: 2, lace: 1) },
            { "blueberry", Need(cloth: 8, blue: 4, red: 1) },
            { "rose_velvet", Need(cloth: 8, red: 4, blue: 1, lace: 2) },
            { "honey_bow", Need(cloth: 8, yellow: 4, red: 1, black: 1) },
            { "hair_milktea", Need(hair: 5, red: 1, yellow: 1, black: 1) },
            { "hair_wisteria", Need(hair: 5, red: 1, blue: 2) },
            { "hair_caramel", Need(hair: 5, red: 1, yellow: 1, black: 1) },
            { "hair_baguette", Need(hair: 5, yellow: 3) },
            { "hair_denim", Need(hair: 5, blue: 2, black: 1) },
            { "hair_apricot", Need(hair: 5, red: 2, yellow: 2) },
            { "hair_milktea_long", Need(hair: 9, red: 1, yellow: 2, black: 2) },
            { "wing_aqua", Need(cloth: 4, blue: 3, yellow: 1, lace: 4, star: 5) },
            { "wing_rose", Need(cloth: 4, red: 3, yellow: 1, lace: 4, star: 5) },
        };

        public enum Kind
        {
            Materials,
            Gift,
            DressBlueprint,
            HairBlueprint,
            WingBlueprint,
        }

        /// <summary>这一关结算时给什么。材料另由 MaterialsFor 算，因为染料要看玩家缺什么。</summary>
        public struct LevelReward
        {
            public Kind kind;
            public string itemId;
            public bool hard;
            /// <summary>该发图纸但这类图纸已经发完，改送一份难关的材料。</summary>
            public bool bigPack;
        }

        public static int[] RecipeOf(string id) =>
            id != null && Recipes.TryGetValue(id, out int[] need) ? need : null;

        public static bool IsGiftLevel(int index) => index >= 1 && index <= GiftLevels;

        public static LevelReward RewardFor(int index)
        {
            var reward = new LevelReward { kind = Kind.Materials, hard = index % 10 == 0 };
            if (IsGiftLevel(index))
            {
                reward.kind = Kind.Gift;
                reward.itemId = Gifts[index - 1];
                return reward;
            }
            if (index == WorkshopLevel)
            {
                return Blueprint(reward, Kind.DressBlueprint, DressBlueprints, 0);
            }
            if (index % 50 == 0)
                return Blueprint(reward, Kind.WingBlueprint, WingBlueprints, index / 50 - 1);
            if (index % 10 == 0)
            {
                int slot = (index - 30) / 10 - (index - 1) / 50;
                return Blueprint(reward, Kind.HairBlueprint, HairBlueprints, slot);
            }
            if (index % 10 == 5)
                return Blueprint(reward, Kind.DressBlueprint, DressBlueprints, 1 + (index - 25) / 10);
            return reward;
        }

        static LevelReward Blueprint(LevelReward reward, Kind kind, string[] pool, int slot)
        {
            reward.kind = kind;
            reward.itemId = Pick(pool, slot);
            reward.bigPack = reward.itemId == null;
            return reward;
        }

        /// <summary>某类图纸的发放顺序，模拟「内容补齐后」的节奏时拿来当代表款。</summary>
        public static string[] PoolOf(Kind kind) =>
            kind == Kind.DressBlueprint ? DressBlueprints
            : kind == Kind.HairBlueprint ? HairBlueprints
            : kind == Kind.WingBlueprint ? WingBlueprints
            : new string[0];

        static string Pick(string[] pool, int slot) =>
            slot >= 0 && slot < pool.Length ? pool[slot] : null;

        /// <summary>
        /// 这一关掉的材料。种子固定在关卡号上；染料优先掉 shortage 里最缺的颜色，
        /// 拿到图纸后不会一直卡在一种染料上。shortage 可以传 null。
        /// asIfStocked 让图纸发完的关卡也按「有图纸」掉材料，只给模拟用。
        /// </summary>
        public static int[] MaterialsFor(int index, int[] shortage, bool asIfStocked = false)
        {
            var drop = new int[MatCount];
            if (IsGiftLevel(index)) return drop;
            LevelReward reward = RewardFor(index);
            if (asIfStocked) reward.bigPack = false;
            var rng = new Random(index * 48271 + 7);

            if (index == WorkshopLevel && reward.itemId != null)
            {
                int[] need = RecipeOf(reward.itemId);
                if (need != null) Array.Copy(need, drop, MatCount);
                return drop;
            }

            if (reward.kind == Kind.DressBlueprint && !reward.bigPack)
            {
                drop[PickDye(rng, shortage, -1)] = 1;
                return drop;
            }

            // 一组 10 关大约产出布匹 10、发丝 5、染料 10、蕾丝 2、星砂 1，
            // 比这组图纸要用的多两成：图纸拿到后攒几关就能做，又不会堆成山。
            bool rich = reward.hard || reward.bigPack;
            drop[(int)CraftMat.Cloth] = rich ? 2 : 1;
            drop[(int)CraftMat.Hair] = rich || index % 2 == 0 ? 1 : 0;
            int dyes = rich ? 2 : 1;
            int lastDye = -1;
            for (int i = 0; i < dyes; i++)
            {
                int dye = PickDye(rng, shortage, lastDye);
                drop[dye] += 1;
                lastDye = dye;
            }
            if (rich)
            {
                drop[(int)CraftMat.Lace] += 1;
                drop[(int)CraftMat.Stardust] += 1;
            }
            else if (rng.NextDouble() < 0.15)
            {
                drop[(int)CraftMat.Lace] += 1;
            }
            return drop;
        }

        static int PickDye(Random rng, int[] shortage, int skip)
        {
            int first = (int)CraftMat.DyeRed;
            int last = (int)CraftMat.DyeBlack;
            if (shortage != null && rng.NextDouble() < 0.7)
            {
                int best = -1;
                for (int m = first; m <= last; m++)
                {
                    if (m == skip || shortage[m] <= 0) continue;
                    if (best < 0 || shortage[m] > shortage[best]) best = m;
                }
                if (best >= 0) return best;
            }
            int pick;
            do pick = rng.Next(first, last + 1);
            while (pick == skip);
            return pick;
        }

        /// <summary>「布匹×2 发丝×1」这种一行文字，界面提示用。</summary>
        public static string Describe(int[] mats)
        {
            if (mats == null) return "";
            var parts = new List<string>();
            for (int m = 0; m < MatCount && m < mats.Length; m++)
            {
                if (mats[m] > 0) parts.Add(MatNames[m] + "×" + mats[m]);
            }
            return string.Join("  ", parts);
        }

        public static bool Any(int[] mats)
        {
            if (mats == null) return false;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] > 0) return true;
            }
            return false;
        }
    }
}
