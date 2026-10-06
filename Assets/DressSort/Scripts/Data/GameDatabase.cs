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

        [Tooltip("棋盘上的问号，过关时手里剩下的那件，不进衣柜")]
        public ItemDef mysteryItem;
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
        public Sprite iconHomeEnergy;
        public Sprite labelHomeCircle;
        public Sprite labelHomeRank;
        public Sprite labelHomeCheckin;
        public Sprite labelHomeWorkshop;
        public Sprite labelHomeQuest;
        public Sprite labelHomeEvent;

        [Header("对局")]
        public Sprite btnGameUndo;
        public Sprite btnGameShuffle;
        public Sprite btnGameBack;
        public Sprite btnGameSteps;
        public Sprite btnGameGear;
        public Sprite btnGameSwap;
        public Sprite gameBadge;
        public Sprite gameRod;
        public Sprite gamePauseBoard;
        public Sprite gameParcel;
        public Sprite gameDustCover;
        public Sprite gameCoverTag;
        public Sprite gameLock;
        public Sprite gameKey;
        public Sprite gameAlarm;
        public Sprite gameBubble;
        public Sprite packLane;
        public Sprite packLaneOn;
        public Sprite packLaneReady;
        public Sprite packBox;
        public Sprite packBtn;
        public Sprite packRefill;
        public Sprite packLock;
        public Sprite packAd;
        public Sprite packCheck;
        public List<Sprite> boardBgs = new List<Sprite>();
        public List<Sprite> boardHangers = new List<Sprite>();
        public List<Sprite> boardRods = new List<Sprite>();
        [Tooltip("工坊材料图标，顺序同 CraftMat")]
        public List<Sprite> craftMats = new List<Sprite>();
        public Sprite craftCard;
        public Sprite craftBg;
        public Sprite craftBtnMint;
        [Header("过关领奖页")]
        public Sprite rewardBg;
        public Sprite rewardGiftClosed;
        public Sprite rewardGiftBase;
        public Sprite rewardGiftLid;
        public Sprite rewardRays;
        public Sprite rewardBtn;
        public Sprite rewardRibbon;
        public Sprite rewardPlate;
        public Sprite rewardPopper;
        [Tooltip("彩纸碎片，随机挑着撒")]
        public List<Sprite> rewardConfetti = new List<Sprite>();
        public Sprite rewardSparkle;
        public Sprite boardLaneSolved;
        public Sprite boardCheckSolved;
        public int boardBgBand = 20;

        /// <summary>棋盘主题按这个顺序每 boardBgBand 关换一次，走完一轮从头再来。</summary>
        public static readonly string[] BoardThemes = { "shop", "seaside", "garden", "night", "autumn", "winter" };

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
        public Sprite dressupPanel;
        public Sprite dressupTabTrack;
        public Sprite dressupTabOn;
        public Sprite dressupStarChip;

        /// <summary>第 1–10 关第一张，11–20 第二张，以后每 10 关加一张。</summary>
        public Sprite BoardBgFor(int levelIndex) => PickByBand(boardBgs, levelIndex, ActiveBg);

        public Sprite BoardHangerFor(int levelIndex)
        {
            Sprite hanger = PickByBand(boardHangers, levelIndex, null);
            return hanger != null ? hanger : iconHanger;
        }

        public Sprite BoardRodFor(int levelIndex) => PickByBand(boardRods, levelIndex, gameRod);

        Sprite PickByBand(List<Sprite> list, int levelIndex, Sprite fallback)
        {
            if (list == null || list.Count == 0)
                return fallback;
            int band = (Mathf.Max(1, levelIndex) - 1) / Mathf.Max(1, boardBgBand);
            Sprite picked = list[band % list.Count];
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
