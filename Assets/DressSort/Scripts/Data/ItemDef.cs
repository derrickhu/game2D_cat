using UnityEngine;

namespace DressSort
{
    public enum ItemSlot
    {
        Dress,
        Wings,
        Hair,
    }

    /// <summary>一件可收集的装扮。</summary>
    [CreateAssetMenu(menuName = "叠叠裙/物品", fileName = "Item")]
    public class ItemDef : ScriptableObject
    {
        public string id;
        public string displayName;
        public ItemSlot slot = ItemSlot.Dress;

        [Tooltip("棋盘和衣柜格子里用的小图，带白描边")]
        public Sprite icon;

        /// <summary>先用 Inspector 上的 icon，丢了再从 Resources/DressIcons 补。</summary>
        public Sprite ResolveIcon()
        {
            if (icon != null)
                return icon;
            if (string.IsNullOrEmpty(id))
                return null;
            return Resources.Load<Sprite>("DressIcons/" + id);
        }

        [Tooltip("裙子用身体层，发型用头和前发，翅膀用背后那层")]
        public Sprite worn;

        [Tooltip("长发后片，夹在翅膀和身体之间。短发留空")]
        public Sprite wornBack;

        [Tooltip("关掉之后这件只能在衣柜收藏，不会出现在棋盘上")]
        public bool boardEligible = true;

        [Tooltip("归类完成时列高亮用的颜色")]
        public Color accent = new Color(1f, 0.24f, 0.55f);

        public bool unlockedFromStart;

        [Tooltip("立绘是无头身体，换装时再叠发型层")]
        public bool layeredWithHair;
    }
}
