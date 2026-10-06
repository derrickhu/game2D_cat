using System.Collections.Generic;

namespace DressSort
{
    /// <summary>
    /// 过关任务。前 20 关隔几关就有一份，后面隔得更开。
    /// 礼物可以是衣服、发型、材料或图纸。
    /// </summary>
    public static class TaskCatalog
    {
        public struct Reward
        {
            public int level;
            public string[] items;
            public string blueprint;
            public int[] materials;
        }

        static int[] Mats(int cloth = 0, int hair = 0, int red = 0, int yellow = 0, int blue = 0,
            int black = 0, int lace = 0, int star = 0)
        {
            return new[] { cloth, hair, red, yellow, blue, black, lace, star };
        }

        /// <summary>
        /// 身上穿着的泳池水手裙和奶茶棕头发仍是开局就有。
        /// 其余原来白送的衣服和头发改到这里领。
        /// </summary>
        public static readonly Reward[] Track =
        {
            new Reward { level = 2, items = new[] { "hair_apricot" } },
            new Reward { level = 4, materials = Mats(cloth: 4, yellow: 2) },
            new Reward { level = 6, materials = Mats(hair: 3, red: 2) },
            new Reward { level = 8, materials = Mats(cloth: 4, lace: 1) },
            new Reward { level = 10, items = new[] { "black_ribbon" } },
            new Reward { level = 15, items = new[] { "hair_wisteria" } },
            new Reward { level = 20, items = new[] { "pink_gingham", "hair_denim" } },
            new Reward { level = 35, materials = Mats(cloth: 6, hair: 3, blue: 2, lace: 1) },
            new Reward { level = 60, materials = Mats(cloth: 8, hair: 4, black: 2, lace: 2, star: 1) },
            new Reward { level = 80, blueprint = "wing_rose" },
            new Reward { level = 120, materials = Mats(cloth: 12, hair: 6, red: 2, yellow: 2, blue: 2, lace: 3, star: 3) },
        };

        public static int Count => Track.Length;

        public static bool ContainsItem(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < Track.Length; i++)
            {
                string[] items = Track[i].items;
                if (items == null) continue;
                for (int n = 0; n < items.Length; n++)
                {
                    if (items[n] == id) return true;
                }
            }
            return false;
        }

        public static string Describe(Reward reward, GameDatabase db)
        {
            var parts = new List<string>();
            if (reward.items != null)
            {
                for (int i = 0; i < reward.items.Length; i++)
                    parts.Add(NameOf(db, reward.items[i]));
            }
            if (!string.IsNullOrEmpty(reward.blueprint))
                parts.Add(NameOf(db, reward.blueprint) + "图纸");
            string mats = CraftCatalog.Describe(reward.materials);
            if (!string.IsNullOrEmpty(mats))
                parts.Add(mats);
            return string.Join("、", parts);
        }

        public static void Icons(Reward reward, GameDatabase db, List<SpriteSlot> into)
        {
            into.Clear();
            if (reward.items != null)
            {
                for (int i = 0; i < reward.items.Length; i++)
                    into.Add(new SpriteSlot { sprite = IconOf(db, reward.items[i]) });
            }
            if (!string.IsNullOrEmpty(reward.blueprint))
                into.Add(new SpriteSlot { sprite = IconOf(db, reward.blueprint) });
            int[] mats = reward.materials;
            if (mats != null)
            {
                for (int m = 0; m < mats.Length && m < CraftCatalog.MatCount; m++)
                {
                    if (mats[m] <= 0) continue;
                    into.Add(new SpriteSlot { sprite = CraftView.MatIcon(db, m), count = mats[m] });
                    if (into.Count >= 3) break;
                }
            }
        }

        static string NameOf(GameDatabase db, string id)
        {
            ItemDef item = db != null ? db.Find(id) : null;
            return item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : id;
        }

        static UnityEngine.Sprite IconOf(GameDatabase db, string id)
        {
            ItemDef item = db != null ? db.Find(id) : null;
            return item != null ? item.ResolveIcon() : null;
        }

        public struct SpriteSlot
        {
            public UnityEngine.Sprite sprite;
            public int count;
        }
    }
}
