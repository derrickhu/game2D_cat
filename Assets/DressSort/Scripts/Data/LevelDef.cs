using System.Collections.Generic;
using UnityEngine;

namespace DressSort
{
    /// <summary>一关的配置。由 LevelCatalog 按关卡号生成，同一关永远是同一盘。</summary>
    public class LevelDef : ScriptableObject
    {
        public int index = 1;

        public int columns = 5;
        public int columnHeight = 6;
        public int moveLimit = 60;

        [Tooltip("从已解状态逆推的步数，越大越难")]
        public int scrambleMoves = 26;

        [Tooltip("发牌种子，固定后这一关每次都是同一个布局")]
        public int seed = 1;

        public int shuffles = 3;

        [Tooltip("「交换」道具次数：手里那件和架上任意一件对调")]
        public int swaps = 2;

        [Tooltip("包裹数：盖住的格子落到列底才拆开")]
        public int parcels;

        [Tooltip("每列的防尘罩：叠好几列后拉开，0 表示这列没有罩。留空表示整关没有")]
        public List<int> dustCovers = new List<int>();

        [Tooltip("每列是否上锁，1 表示锁住。钥匙数等于锁数")]
        public List<int> locks = new List<int>();

        [Tooltip("每列是否是专属列，1 表示只认一款")]
        public List<int> targets = new List<int>();

        [Tooltip("限时闹钟数")]
        public int alarms;

        [Tooltip("参与这一关的款式，长度要等于列数")]
        public List<ItemDef> palette = new List<ItemDef>();

        [Tooltip("过关时手里剩下的那件，也就是棋盘上的问号")]
        public ItemDef mystery;

        [Tooltip("通关解锁的物品，留空表示不给奖励")]
        public ItemDef reward;

        public bool IsValid => palette != null && palette.Count >= columns && mystery != null;

        public bool HasCovers => Any(dustCovers);
        public bool HasLocks => Any(locks);
        public bool HasTargets => Any(targets);

        static bool Any(List<int> list)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] > 0) return true;
            }
            return false;
        }
    }
}
