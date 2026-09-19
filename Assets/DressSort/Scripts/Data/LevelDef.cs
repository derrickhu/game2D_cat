using System.Collections.Generic;
using UnityEngine;

namespace DressSort
{
    /// <summary>一关的配置。棋盘尺寸和题材都从这里读，不写死在玩法里。</summary>
    [CreateAssetMenu(menuName = "叠叠裙/关卡", fileName = "Level")]
    public class LevelDef : ScriptableObject
    {
        public int index = 1;

        [Tooltip("显示在顶栏的题材名，比如「裙子」「翅膀」")]
        public string themeName = "裙子";

        public int columns = 5;
        public int columnHeight = 6;
        public int moveLimit = 60;

        [Tooltip("从已解状态逆推的步数，越大越难")]
        public int scrambleMoves = 26;

        public int shuffles = 3;

        [Tooltip("参与这一关的款式，长度要等于列数")]
        public List<ItemDef> palette = new List<ItemDef>();

        [Tooltip("过关时手里剩下的那件，也就是棋盘上的问号")]
        public ItemDef mystery;

        [Tooltip("通关解锁的物品，留空表示不给奖励")]
        public ItemDef reward;

        public bool IsValid => palette != null && palette.Count >= columns && mystery != null;
    }
}
