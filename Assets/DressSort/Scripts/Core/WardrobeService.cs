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
        }

        readonly GameDatabase database;
        SaveData data;

        public WardrobeService(GameDatabase database)
        {
            this.database = database;
            Load();
        }

        public int LevelsCleared => data.levelsCleared;
        public int Stars => data.stars;
        public int UnlockedCount => data.unlocked.Count;
        public int TotalCount => database.items.Count;

        public bool IsUnlocked(ItemDef item) =>
            item != null && (item.unlockedFromStart || data.unlocked.Contains(item.id));

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

        public bool IsLevelPlayable(int levelIndex) => levelIndex <= data.levelsCleared + 1;

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
            if (data.unlocked != null)
            {
                for (int i = 0; i < data.unlocked.Count; i++)
                    data.unlocked[i] = GameDatabase.CanonicalId(data.unlocked[i]);
            }

            for (int i = 0; i < database.items.Count; i++)
            {
                ItemDef item = database.items[i];
                if (item != null && item.unlockedFromStart && !data.unlocked.Contains(item.id))
                    data.unlocked.Add(item.id);
            }

            if (string.IsNullOrEmpty(data.equippedDress) && database.defaultDress != null)
                data.equippedDress = database.defaultDress.id;
            if (string.IsNullOrEmpty(data.equippedHair) && database.defaultHair != null)
                data.equippedHair = database.defaultHair.id;
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
