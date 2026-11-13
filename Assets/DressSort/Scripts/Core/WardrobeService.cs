using System.Collections.Generic;
using UnityEngine;

namespace DressSort
{
    /// <summary>
    /// 存档：已解锁的物品、当前装扮、关卡进度、星星数。
    /// 本地仍写 PlayerPrefs；播放中每次 Save 会标脏，由 CloudSync 整份上行。
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
            /// <summary>第一次七日签到领到粉红马尾图纸的日期。0 表示还没领过。</summary>
            public int checkPrizeDay;
            /// <summary>今天的签到材料已经看广告翻过倍。0 表示还没翻。</summary>
            public int checkDoubleDay;
            public int clubDay;
            public int clubOfferDay;
            public int clubMat0;
            public int clubMat1;
            public int clubCount0;
            public int clubCount1;
            public bool clubRefreshed;
            public int packDay;
            public int toolDay;
            public int swapsToday;
            public int shufflesToday;
            public int shareDay;
            public int packPlays;
            public int packStageMask;
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
        /// <summary>看一条体力广告加的点数。可以超过自然恢复上限。</summary>
        public const int EnergyAdReward = 5;
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

        /// <summary>GM 一次解锁多件装饰。已经有的跳过，只存一次档。</summary>
        public int GrantUnlocked(List<ItemDef> items)
        {
            if (items == null) return 0;
            int added = 0;
            for (int i = 0; i < items.Count; i++)
            {
                ItemDef item = items[i];
                if (item == null || string.IsNullOrEmpty(item.id) || IsUnlocked(item)) continue;
                data.unlocked.Add(item.id);
                added++;
            }
            if (added > 0) Save();
            return added;
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

        /// <summary>七日签到每天一种材料，各 2 份。第 7 天平时是蕾丝；第一次签满改送粉红马尾图纸。</summary>
        public static readonly int[] CheckInMats =
        {
            (int)CraftMat.Cloth, (int)CraftMat.Hair, (int)CraftMat.DyeRed, (int)CraftMat.DyeYellow,
            (int)CraftMat.DyeBlue, (int)CraftMat.DyeBlack, (int)CraftMat.Lace,
        };
        public static readonly int[] CheckInCounts = { 2, 2, 2, 2, 2, 2, 2 };
        public const int CheckInDays = 7;
        public const string CheckInPrizeId = "hair_pink";

        public struct CheckInGrant
        {
            public int mat;
            public int count;
            public bool blueprint;
        }

        public bool CheckInPrizePending => data.checkPrizeDay == 0;

        /// <summary>这一天卡片上该显示什么。第一次的第 7 天是粉红马尾图纸。</summary>
        public CheckInGrant PreviewCheckIn(int day)
        {
            bool prize = day == CheckInDays && (CheckInPrizePending || data.checkPrizeDay == Today);
            if (prize) return new CheckInGrant { blueprint = true };
            int i = Mathf.Clamp(day, 1, CheckInDays) - 1;
            return new CheckInGrant { mat = CheckInMats[i], count = CheckInCounts[i] };
        }

        public CheckInGrant LastCheckIn { get; private set; }

        static int DayKey(System.DateTime d) => d.Year * 10000 + d.Month * 100 + d.Day;
        static int Today => DayKey(System.DateTime.Now);
        static int Yesterday => DayKey(System.DateTime.Now.AddDays(-1));

        public bool CheckedInToday => data.checkDay == Today;

        public bool CheckInDoubledToday => data.checkDoubleDay == Today;

        /// <summary>把今天签到的材料再发一份。图纸不翻倍。今天没签、已经翻过、或今天是图纸，返回 false。</summary>
        public bool DoubleCheckIn()
        {
            if (!CheckedInToday || CheckInDoubledToday) return false;
            CheckInGrant grant = PreviewCheckIn(data.checkRun);
            if (grant.blueprint || grant.count <= 0) return false;
            data.materials[grant.mat] += grant.count;
            data.checkDoubleDay = Today;
            LastCheckIn = grant;
            Save();
            return true;
        }

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

        /// <summary>签到成功返回当天是第几天（1~7），今天已签返回 0。发的东西在 LastCheckIn。</summary>
        public int CheckIn()
        {
            if (CheckedInToday) return 0;
            int run = CheckInRun + 1;
            data.checkRun = run;
            data.checkDay = Today;
            bool prize = run == CheckInDays && data.checkPrizeDay == 0;
            if (prize)
            {
                data.checkPrizeDay = Today;
                ItemDef item = database.Find(CheckInPrizeId);
                if (AddBlueprint(item))
                {
                    LastCheckIn = new CheckInGrant { blueprint = true };
                    return run;
                }
                data.materials[(int)CraftMat.Lace] += CheckInCounts[CheckInDays - 1];
                LastCheckIn = new CheckInGrant { mat = (int)CraftMat.Lace, count = CheckInCounts[CheckInDays - 1] };
            }
            else
            {
                CheckInGrant grant = PreviewCheckIn(run);
                data.materials[grant.mat] += grant.count;
                LastCheckIn = grant;
            }
            Save();
            return run;
        }

        public bool ClubClaimedToday => data.clubDay == Today;

        public struct ClubOffer
        {
            public int mat0;
            public int count0;
            public int mat1;
            public int count1;
            public bool ready;
            public bool refreshed;
        }

        public bool ClubRefreshUsed => data.clubOfferDay == Today && data.clubRefreshed;

        /// <summary>今天的游戏圈奖励。两格各抽一次，可能是 1 份材料，也可能是 1 或 2 点体力。</summary>
        public ClubOffer EnsureClubOffer()
        {
            if (data.clubOfferDay != Today || data.clubCount0 < 1 || data.clubCount1 < 1)
            {
                if (!ClubClaimedToday)
                {
                    RollClubOffer();
                    data.clubOfferDay = Today;
                    data.clubRefreshed = false;
                    Save();
                }
            }
            return ReadClubOffer();
        }

        /// <summary>看广告后重抽一次。今天已经换过或已经领过就不动。</summary>
        public bool RefreshClubOffer()
        {
            if (ClubClaimedToday || ClubRefreshUsed) return false;
            EnsureClubOffer();
            RollClubOffer();
            data.clubOfferDay = Today;
            data.clubRefreshed = true;
            Save();
            return true;
        }

        /// <summary>领走今天已经抽好的两格奖励。</summary>
        public bool ClaimClub()
        {
            if (ClubClaimedToday) return false;
            ClubOffer offer = EnsureClubOffer();
            if (!offer.ready) return false;
            data.clubDay = Today;
            int energy = 0;
            energy += GrantClubSlot(offer.mat0, offer.count0);
            energy += GrantClubSlot(offer.mat1, offer.count1);
            if (energy > 0)
                GainEnergy(energy);
            else
                Save();
            return true;
        }

        public string DescribeClubOffer(ClubOffer offer)
        {
            return DescribeClubSlot(offer.mat0, offer.count0) + "、" + DescribeClubSlot(offer.mat1, offer.count1);
        }

        static string DescribeClubSlot(int mat, int count)
        {
            if (mat < 0) return "体力 +" + count;
            return CraftCatalog.MatNames[mat] + " ×" + count;
        }

        /// <summary>材料返回 0。体力返回点数，由调用方一次加上。</summary>
        int GrantClubSlot(int mat, int count)
        {
            if (mat < 0) return count;
            data.materials[mat] += count;
            return 0;
        }

        void RollClubOffer()
        {
            RollClubSlot(out data.clubMat0, out data.clubCount0);
            RollClubSlot(out data.clubMat1, out data.clubCount1);
        }

        static void RollClubSlot(out int mat, out int count)
        {
            if (Random.Range(0, 2) == 0)
            {
                mat = -1;
                count = Random.Range(1, 3);
                return;
            }
            mat = Random.Range(0, CraftCatalog.MatCount);
            count = 1;
        }

        ClubOffer ReadClubOffer()
        {
            bool ready = data.clubOfferDay == Today && data.clubCount0 >= 1 && data.clubCount1 >= 1;
            return new ClubOffer
            {
                mat0 = data.clubMat0,
                count0 = data.clubCount0,
                mat1 = data.clubMat1,
                count1 = data.clubCount1,
                ready = ready,
                refreshed = ready && data.clubRefreshed,
            };
        }

        /// <summary>交换和随机记在今天。过了零点清掉，开局不送。</summary>
        void RollTools()
        {
            if (data.toolDay == Today) return;
            data.toolDay = Today;
            data.swapsToday = 0;
            data.shufflesToday = 0;
            Save();
        }

        public int SwapsToday
        {
            get
            {
                RollTools();
                return data.swapsToday;
            }
        }

        public int ShufflesToday
        {
            get
            {
                RollTools();
                return data.shufflesToday;
            }
        }

        public bool ShareReady
        {
            get
            {
                RollTools();
                return data.shareDay != Today;
            }
        }

        public bool TrySpendSwap()
        {
            RollTools();
            if (data.swapsToday <= 0) return false;
            data.swapsToday--;
            Save();
            return true;
        }

        public bool TrySpendShuffle()
        {
            RollTools();
            if (data.shufflesToday <= 0) return false;
            data.shufflesToday--;
            Save();
            return true;
        }

        public void GrantSwap(int count = 1)
        {
            if (count <= 0) return;
            RollTools();
            data.swapsToday += count;
            Save();
        }

        public void GrantShuffle(int count = 1)
        {
            if (count <= 0) return;
            RollTools();
            data.shufflesToday += count;
            Save();
        }

        /// <summary>今天第一次分享。0 是交换，1 是随机，-1 表示今天领过了。</summary>
        public int ClaimShareTool()
        {
            RollTools();
            if (data.shareDay == Today) return -1;
            data.shareDay = Today;
            int which = Random.Range(0, 2);
            if (which == 0)
                data.swapsToday++;
            else
                data.shufflesToday++;
            Save();
            return which;
        }

        /// <summary>活动每天能开的局数。不扣体力。</summary>
        public const int PackPlaysPerDay = 2;

        /// <summary>活动每辆车送走的材料，顺序同 CraftMat。一局里每辆领一次，新的一局重新领。</summary>
        public static readonly int[][] PackStageMats =
        {
            new[] { 2, 0, 0, 0, 0, 0, 0, 0 },
            new[] { 0, 2, 1, 0, 0, 0, 0, 0 },
            new[] { 0, 0, 0, 1, 1, 0, 1, 0 },
            new[] { 1, 0, 0, 0, 0, 1, 0, 1 },
        };

        int PlaysToday => data.packDay == Today ? data.packPlays : 0;

        public int PackPlaysLeft => Mathf.Max(0, PackPlaysPerDay - PlaysToday);

        /// <summary>开一局。今天已经两局就返回 false，不扣体力。</summary>
        public bool TryBeginPack()
        {
            int played = PlaysToday;
            if (played >= PackPlaysPerDay) return false;
            data.packDay = Today;
            data.packPlays = played + 1;
            data.packStageMask = 0;
            Save();
            return true;
        }

        int PackMaskToday => data.packDay == Today ? data.packStageMask : 0;

        public bool PackStageClaimed(int stage) => (PackMaskToday & (1 << stage)) != 0;

        /// <summary>领第 stage 辆车的材料，times 是倍数（看广告翻倍）。这一局领过返回 false。</summary>
        public bool ClaimPackStage(int stage, int times)
        {
            if (stage < 0 || stage >= PackStageMats.Length || PackStageClaimed(stage)) return false;
            data.packDay = Today;
            data.packStageMask = PackMaskToday | (1 << stage);
            int[] mats = PackStageMats[stage];
            for (int m = 0; m < CraftCatalog.MatCount && m < mats.Length; m++)
                data.materials[m] += mats[m] * Mathf.Max(1, times);
            Save();
            return true;
        }

        // ------------------------------------------------------------ 工坊

        /// <summary>第 11 关通关送第一张图纸，工坊从那时开放。GM 也可以单独放开。</summary>
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
            if (Application.isPlaying) CloudSync.Touch();
            PlayerPrefs.Save();
        }

        public static bool HasLocal => PlayerPrefs.HasKey(Key);

        public static string RawLocal => PlayerPrefs.GetString(Key, "");

        /// <summary>云端下行用，不标脏。内存里的衣柜要再 Reload。</summary>
        public static void WriteRaw(string json)
        {
            PlayerPrefs.SetString(Key, json ?? "");
        }

        public void Reload() => Load();

        public void Wipe()
        {
            PlayerPrefs.DeleteKey(Key);
            data = null;
            Load();
            Save();
            if (Application.isPlaying) CloudSync.FlushNow("wipe");
        }
    }
}
