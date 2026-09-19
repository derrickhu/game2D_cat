using System.Collections.Generic;
using UnityEngine;

namespace DressSort
{
    /// <summary>
    /// 全部内容的索引。所有资源引用集中在这一个资产上，
    /// 以后换成 Addressables 只要改这里的取图方式。
    /// </summary>
    [CreateAssetMenu(menuName = "叠叠裙/数据库", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        public List<ItemDef> items = new List<ItemDef>();
        public List<ChapterDef> chapters = new List<ChapterDef>();
        public ItemDef defaultDress;
        public ItemDef defaultHair;

        public static readonly string[] UiBgNames =
        {
            "更衣室", "精品店", "云台", "花园", "秀台",
        };

        [Header("界面切图")]
        public Sprite uiBg;
        public List<Sprite> uiBgs = new List<Sprite>();
        public int uiBgIndex = 4;
        public Sprite uiBtnTeal;
        public Sprite uiBtnPink;
        public Sprite uiBtnWhite;
        public Sprite uiCard;
        public Sprite uiCardOn;
        public Sprite uiLane;
        public Sprite pageLoading;

        [Header("UI 图标")]
        public Sprite iconBack;
        public Sprite iconGear;
        public Sprite iconLock;
        public Sprite iconCheck;
        public Sprite iconUndo;
        public Sprite iconShuffle;
        public Sprite iconEject;
        public Sprite iconHanger;
        public Sprite iconStar;
        public Sprite iconMystery;

        [Header("首页按钮")]
        public Sprite btnHomeStart;
        public Sprite btnHomeDress;
        public Sprite iconHomeCircle;
        public Sprite iconHomeRank;
        public Sprite iconHomeCheckin;
        public Sprite iconHomeWorkshop;
        public Sprite iconHomeQuest;
        public Sprite iconHomeEvent;

        [Header("对局")]
        public Sprite btnGameUndo;
        public Sprite btnGameShuffle;
        public Sprite btnGameBack;
        public Sprite btnGameSteps;
        public Sprite btnGameGear;
        public List<Sprite> boardBgs = new List<Sprite>();
        public List<Sprite> boardHangers = new List<Sprite>();
        public Sprite boardLaneSolved;
        public Sprite boardCheckSolved;
        public int boardBgBand = 10;

        [Header("装扮")]
        public Sprite dressupBg;
        public Sprite dressupTitle;
        public Sprite dressupBack;
        public Sprite dressupSave;
        public Sprite dressupPodium;
        public Sprite dressupShelf;
        public Sprite dressupTray;
        public Sprite dressupBtnCream;
        public Sprite dressupBtnPeach;
        public Sprite dressupCard;
        public Sprite dressupCardOn;
        public Sprite dressupCardLock;
        public Sprite dressupTabDressOn;
        public Sprite dressupTabDressOff;
        public Sprite dressupTabHairOn;
        public Sprite dressupTabHairOff;
        public Sprite dressupTabWingsOn;
        public Sprite dressupTabWingsOff;

        /// <summary>第 1–10 关第一张，11–20 第二张，以后每 10 关加一张。</summary>
        public Sprite BoardBgFor(int levelIndex) => PickByBand(boardBgs, levelIndex, ActiveBg);

        public Sprite BoardHangerFor(int levelIndex)
        {
            Sprite hanger = PickByBand(boardHangers, levelIndex, null);
            return hanger != null ? hanger : iconHanger;
        }

        Sprite PickByBand(List<Sprite> list, int levelIndex, Sprite fallback)
        {
            if (list == null || list.Count == 0)
                return fallback;
            int band = (Mathf.Max(1, levelIndex) - 1) / Mathf.Max(1, boardBgBand);
            Sprite picked = list[Mathf.Clamp(band, 0, list.Count - 1)];
            return picked != null ? picked : fallback;
        }

        static readonly Dictionary<string, string> IdAliases = new Dictionary<string, string>
        {
            { "rose_puff", "teal_sailor" },
            { "lemon_overall", "black_ribbon" },
            { "aqua_sailor", "pink_gingham" },
            { "lime_cami", "lemon_print" },
            { "cream_offshoulder", "orange_slice" },
            { "coral_square", "ivory_lace" },
            { "lilac_dot", "strawberry" },
            { "mint_high", "grape_school" },
        };

        Dictionary<string, ItemDef> lookup;

        public static string CanonicalId(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            return IdAliases.TryGetValue(id, out string mapped) ? mapped : id;
        }

        public ItemDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (lookup == null)
            {
                lookup = new Dictionary<string, ItemDef>();
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] != null && !string.IsNullOrEmpty(items[i].id))
                        lookup[items[i].id] = items[i];
                }
            }
            if (lookup.TryGetValue(id, out ItemDef def))
                return def;
            return lookup.TryGetValue(CanonicalId(id), out def) ? def : null;
        }

        public Sprite ActiveBg
        {
            get
            {
                if (uiBgs != null && uiBgIndex >= 0 && uiBgIndex < uiBgs.Count && uiBgs[uiBgIndex] != null)
                    return uiBgs[uiBgIndex];
                return uiBg;
            }
        }

        public void SelectBg(int index)
        {
            if (uiBgs == null || uiBgs.Count == 0) return;
            uiBgIndex = Mathf.Clamp(index, 0, uiBgs.Count - 1);
            if (uiBgs[uiBgIndex] != null)
                uiBg = uiBgs[uiBgIndex];
        }

        public List<LevelDef> AllLevels()
        {
            var all = new List<LevelDef>();
            for (int c = 0; c < chapters.Count; c++)
            {
                if (chapters[c] == null) continue;
                for (int l = 0; l < chapters[c].levels.Count; l++)
                {
                    if (chapters[c].levels[l] != null)
                        all.Add(chapters[c].levels[l]);
                }
            }
            return all;
        }

        public List<ItemDef> ItemsInSlot(ItemSlot slot)
        {
            var list = new List<ItemDef>();
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].slot == slot)
                    list.Add(items[i]);
            }
            return list;
        }
    }
}
