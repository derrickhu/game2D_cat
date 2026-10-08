using System.Collections.Generic;
using UnityEngine;

namespace DressSort
{
    /// <summary>
    /// 存档：已解锁的物品、当前装扮、关卡进度、星星数。
    /// 现在走 PlayerPrefs，接微信小游戏时把 Read/Write 换成云存档即可。
    /// </summary>
    public class WardrobeService
    {
        const string Key = "dresssort.save.v2";

        [System.Serializable]
        class SaveData
        {
            public List<string> unlocked = new List<string>();
            public string equippedDress;
            public string equippedWings;
            public string equippedHair;
            public int levelsCleared;
            public int stars;
            public int energy;
            public long energyUtc;
            public int checkRun;
            public int checkDay;
            public int clubDay;
            public int packDay;
            public List<string> blueprints = new List<string>();
            public int[] materials = new int[CraftCatalog.MatCount];
            public List<int> taskClaimed = new List<int>();
            public bool tasksReady;
            public bool gmWorkshop;
        }

        readonly GameDatabase database;
        SaveData data;

        public WardrobeService(GameDatabase database)
        {
            this.database = database;
            Load();
        }

        public const int MaxEnergy = 5;
        public const int EnergyPerLevel = 1;
        const long RecoverTicks = 5L * 60L * 10000000L;

        public int LevelsCleared => data.levelsCleared;
        public int Stars => data.stars;
        public int Energy => data.energy;
        public int UnlockedCount => data.unlocked.Count;
        public int TotalCount => database.items.Count;

        public bool IsUnlocked(ItemDef item)
        {
            if (item == null) return false;
            if (data.unlocked.Contains(item.id)) return true;
            // 放进任务里的衣服和头发，要领了才算拥有。
            return item.unlockedFromStart && !TaskCatalog.ContainsItem(item.id);
        }

        public ItemDef EquippedDress => database.Find(data.equippedDress) ?? database.defaultDress;
        public ItemDef EquippedWings => database.Find(data.equippedWings);
        public ItemDef EquippedHair => database.Find(data.equippedHair) ?? database.defaultHair;

        public ItemDef Equipped(ItemSlot slot)
        {
            if (slot == ItemSlot.Dress) return EquippedDress;
            if (slot == ItemSlot.Hair) return EquippedHair;
            return EquippedWings;
        }

        public bool Unlock(ItemDef item)
        {
            if (item == null || IsUnlocked(item)) return false;
            data.unlocked.Add(item.id);
            Save();
            return true;
        }

        public void Equip(ItemDef item)
        {
            if (item == null || !IsUnlocked(item)) return;
            if (item.slot == ItemSlot.Dress)
                data.equippedDress = item.id;
            else if (item.slot == ItemSlot.Hair)
                data.equippedHair = item.id;
            else
                data.equippedWings = item.id;
            Save();
        }

        /// <summary>翅膀允许脱下，裙子必须一直穿着一件。</summary>
        public void Unequip(ItemSlot slot)
        {
            if (slot == ItemSlot.Wings)
            {
                data.equippedWings = null;
                Save();
            }
        }

        public void ReportCleared(int levelIndex, int starsEarned)
        {
            if (levelIndex > data.levelsCleared)
                data.levelsCleared = levelIndex;
            data.stars += starsEarned;
            Save();
        }

        public int RecoverEnergy()
        {
            long now = System.DateTime.UtcNow.Ticks;
            if (data.energyUtc <= 0)
            {
                data.energy = MaxEnergy;
                data.energyUtc = now;
                Save();
                return data.energy;
            }

            if (data.energy >= MaxEnergy)
            {
                data.energyUtc = now;
                return data.energy;
            }

            long gained = (now - data.energyUtc) / RecoverTicks;
            if (gained > 0)
            {
                data.energy = Mathf.Min(MaxEnergy, data.energy + (int)gained);
                data.energyUtc += gained * RecoverTicks;
                if (data.energy >= MaxEnergy)
                    data.energyUtc = now;
                Save();
            }
            return data.energy;
        }

        public bool SpendEnergy(int cost = EnergyPerLevel)
        {
            RecoverEnergy();
            if (data.energy < cost) return false;
            data.energy -= cost;
            Save();
            return true;
        }

        /// <summary>签到、游戏圈这类奖励可以把体力加到上限以上，自然恢复只补到上限。</summary>
        public void GainEnergy(int amount)
        {
            if (amount <= 0) return;
            RecoverEnergy();
            data.energy += amount;
            Save();
        }

        // ------------------------------------------------------------ 签到与游戏圈

        /// <summary>七日签到每天的体力，第 7 天是大份。</summary>
        public static readonly int[] CheckInRewards = { 2, 2, 3, 2, 2, 3, 5 };
        public const int CheckInDays = 7;
        public const int ClubReward = 3;

        static int DayKey(System.DateTime d) => d.Year * 10000 + d.Month * 100 + d.Day;
        static int Today => DayKey(System.DateTime.Now);
        static int Yesterday => DayKey(System.DateTime.Now.AddDays(-1));

        public bool CheckedInToday => data.checkDay == Today;

        /// <summary>这一轮已经签了几天。断签一天或七天签满后，从第 1 天重新开始。</summary>
        public int CheckInRun
        {
            get
            {
                if (CheckedInToday) return data.checkRun;
                if (data.checkDay == Yesterday && data.checkRun < CheckInDays) return data.checkRun;
                return 0;
            }
        }

        /// <summary>签到成功返回当天是第几天（1~7），今天已签返回 0。</summary>
        public int CheckIn()
        {
            if (CheckedInToday) return 0;
            int run = CheckInRun + 1;
            data.checkRun = run;
            data.checkDay = Today;
            Save();
            GainEnergy(CheckInRewards[run - 1]);
            return run;
        }

        public bool ClubClaimedToday => data.clubDay == Today;

        public bool ClaimClub()
        {
            if (ClubClaimedToday) return false;
            data.clubDay = Today;
            Save();
            GainEnergy(ClubReward);
            return true;
        }

        public const int PackReward = 2;

        public bool PackRewardedToday => data.packDay == Today;

        /// <summary>活动装箱当天第一次完成给体力。已经领过返回 false。</summary>
        public bool ClaimPack()
        {
            if (PackRewardedToday) return false;
            data.packDay = Today;
            Save();
            GainEnergy(PackReward);
            return true;
        }

        // ------------------------------------------------------------ 工坊

        /// <summary>第 21 关通关送第一张图纸，工坊从那时开放。GM 也可以单独放开。</summary>
        public bool WorkshopOpen => data.levelsCleared >= CraftCatalog.WorkshopLevel || data.gmWorkshop;

        /// <summary>GM 放开工坊。不改关卡进度，重置存档后恢复上锁。</summary>
        public void UnlockWorkshop()
        {
            if (data.gmWorkshop) return;
            data.gmWorkshop = true;
            Save();
        }

        public bool TaskClaimed(int index) =>
            data.taskClaimed != null && data.taskClaimed.Contains(index);

        public bool CanClaimTask(int index)
        {
            if (index < 0 || index >= TaskCatalog.Count || TaskClaimed(index)) return false;
            return data.levelsCleared >= TaskCatalog.Track[index].level;
        }

        public bool ClaimTask(int index)
        {
            if (!CanClaimTask(index)) return false;
            TaskCatalog.Reward reward = TaskCatalog.Track[index];
            if (reward.items != null)
            {
                for (int i = 0; i < reward.items.Length; i++)
                {
                    ItemDef item = database.Find(reward.items[i]);
                    if (item != null && !data.unlocked.Contains(item.id))
                        data.unlocked.Add(item.id);
                }
            }
            if (!string.IsNullOrEmpty(reward.blueprint))
            {
                ItemDef print = database.Find(reward.blueprint);
                if (print != null && !data.blueprints.Contains(print.id))
                    data.blueprints.Add(print.id);
            }
            if (reward.materials != null)
            {
                for (int m = 0; m < CraftCatalog.MatCount && m < reward.materials.Length; m++)
                    data.materials[m] += Mathf.Max(0, reward.materials[m]);
            }
            data.taskClaimed.Add(index);
            Save();
            return true;
        }

        /// <summary>下一份还差几关。0 表示有可以领的，-1 表示都领完了。</summary>
        public int NextTaskGap()
        {
            int gap = int.MaxValue;
            bool any = false;
            for (int i = 0; i < TaskCatalog.Count; i++)
            {
                if (TaskClaimed(i)) continue;
                any = true;
                int left = TaskCatalog.Track[i].level - data.levelsCleared;
                if (left <= 0) return 0;
                if (left < gap) gap = left;
            }
            return any ? gap : -1;
        }

        public int MaterialCount(CraftMat mat) => data.materials[(int)mat];

        public void AddMaterials(int[] gained)
        {
            if (gained == null) return;
            for (int m = 0; m < CraftCatalog.MatCount && m < gained.Length; m++)
                data.materials[m] += Mathf.Max(0, gained[m]);
            Save();
        }

        /// <summary>GM 给一种材料。数量只增不减。</summary>
        public void AddMaterial(CraftMat mat, int amount)
        {
            if (amount <= 0) return;
            data.materials[(int)mat] += amount;
            Save();
        }

        public bool HasBlueprint(ItemDef item) => item != null && data.blueprints.Contains(item.id);

        public bool AddBlueprint(ItemDef item)
        {
            if (item == null || HasBlueprint(item)) return false;
            data.blueprints.Add(item.id);
            Save();
            return true;
        }

        /// <summary>GM 一次发多张有配方的图纸。已经有的跳过，只存一次档。</summary>
        public int GrantBlueprints(List<ItemDef> items)
        {
            if (items == null) return 0;
            int added = 0;
            for (int i = 0; i < items.Count; i++)
            {
                ItemDef item = items[i];
                if (item == null || string.IsNullOrEmpty(item.id) || HasBlueprint(item)) continue;
                if (CraftCatalog.RecipeOf(item.id) == null) continue;
                data.blueprints.Add(item.id);
                added++;
            }
            if (added > 0) Save();
            return added;
        }

        /// <summary>拿到的全部图纸，没做过的排在前面。</summary>
        public List<ItemDef> Blueprints(ItemSlot slot)
        {
            var open = new List<ItemDef>();
            var done = new List<ItemDef>();
            foreach (string id in data.blueprints)
            {
                ItemDef item = database.Find(id);
                if (item == null || item.slot != slot) continue;
                (IsUnlocked(item) ? done : open).Add(item);
            }
            open.AddRange(done);
            return open;
        }

        public bool CanCraft(ItemDef item)
        {
            if (item == null || !HasBlueprint(item) || IsUnlocked(item)) return false;
            int[] need = CraftCatalog.RecipeOf(item.id);
            if (need == null) return false;
            for (int m = 0; m < CraftCatalog.MatCount; m++)
            {
                if (data.materials[m] < need[m]) return false;
            }
            return true;
        }

        public bool Craft(ItemDef item)
        {
            if (!CanCraft(item)) return false;
            int[] need = CraftCatalog.RecipeOf(item.id);
            for (int m = 0; m < CraftCatalog.MatCount; m++)
                data.materials[m] -= need[m];
            data.unlocked.Add(item.id);
            Save();
            return true;
        }

        /// <summary>手上还没做的图纸一共还差多少材料，掉落时据此优先补缺的染料。</summary>
        public int[] Shortage()
        {
            var want = new int[CraftCatalog.MatCount];
            foreach (string id in data.blueprints)
            {
                ItemDef item = database.Find(id);
                if (item == null || IsUnlocked(item)) continue;
                int[] need = CraftCatalog.RecipeOf(id);
                if (need == null) continue;
                for (int m = 0; m < CraftCatalog.MatCount; m++)
                    want[m] += need[m];
            }
            for (int m = 0; m < CraftCatalog.MatCount; m++)
                want[m] = Mathf.Max(0, want[m] - data.materials[m]);
            return want;
        }

        // ------------------------------------------------------------ 读写

        void Load()
        {
            string json = PlayerPrefs.GetString(Key, string.Empty);
            data = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<SaveData>(json);
            if (data == null)
                data = new SaveData();

            data.equippedDress = GameDatabase.CanonicalId(data.equippedDress);
            data.equippedWings = GameDatabase.CanonicalId(data.equippedWings);
            data.equippedHair = GameDatabase.CanonicalId(data.equippedHair);
            if (data.unlocked == null)
                data.unlocked = new List<string>();
            for (int i = 0; i < data.unlocked.Count; i++)
                data.unlocked[i] = GameDatabase.CanonicalId(data.unlocked[i]);

            for (int i = 0; i < database.items.Count; i++)
            {
                ItemDef item = database.items[i];
                if (item == null || !item.unlockedFromStart || TaskCatalog.ContainsItem(item.id)) continue;
                if (!data.unlocked.Contains(item.id))
                    data.unlocked.Add(item.id);
            }

            if (data.taskClaimed == null)
                data.taskClaimed = new List<int>();
            if (!data.tasksReady)
            {
                data.unlocked.RemoveAll(TaskCatalog.ContainsItem);
                if (TaskCatalog.ContainsItem(data.equippedDress) && database.defaultDress != null)
                    data.equippedDress = database.defaultDress.id;
                if (TaskCatalog.ContainsItem(data.equippedHair) && database.defaultHair != null)
                    data.equippedHair = database.defaultHair.id;
                data.tasksReady = true;
                Save();
            }

            if (data.blueprints == null)
                data.blueprints = new List<string>();
            if (data.materials == null || data.materials.Length != CraftCatalog.MatCount)
            {
                var resized = new int[CraftCatalog.MatCount];
                if (data.materials != null)
                    System.Array.Copy(data.materials, resized, Mathf.Min(data.materials.Length, resized.Length));
                data.materials = resized;
            }

            if (string.IsNullOrEmpty(data.equippedDress) && database.defaultDress != null)
                data.equippedDress = database.defaultDress.id;
            if (string.IsNullOrEmpty(data.equippedHair) && database.defaultHair != null)
                data.equippedHair = database.defaultHair.id;

            RecoverEnergy();
        }

        void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public void Wipe()
        {
            PlayerPrefs.DeleteKey(Key);
            data = null;
            Load();
            Save();
        }
    }
}
