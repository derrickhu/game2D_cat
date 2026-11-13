using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>材料图标、材料数量行。过关后的材料走奖励飞入。</summary>
    public static class CraftView
    {
        public static readonly Color Cocoa = new Color32(0x4A, 0x26, 0x2C, 0xFF);
        public static readonly Color Short = new Color32(0xE0, 0x3A, 0x48, 0xFF);
        static readonly Color Rim = new Color(1f, 0.98f, 0.93f, 0.95f);

        public static Sprite MatIcon(GameDatabase db, int mat)
        {
            if (db == null || db.craftMats == null || mat < 0 || mat >= db.craftMats.Count) return null;
            return db.craftMats[mat];
        }

        /// <summary>一排「图标 ×数量」，居中排在 parent 里，返回整排宽度。</summary>
        public static float BuildRow(RectTransform parent, GameDatabase db, int[] mats, float iconSize, int fontSize)
        {
            if (mats == null) return 0f;
            float cell = iconSize + fontSize * 1.9f;
            int count = 0;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] > 0) count++;
            }
            float x = -(count - 1) * cell * 0.5f;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] <= 0) continue;
                Image icon = UiKit.Icon(parent, "Mat_" + m, MatIcon(db, m), new Vector2(0.5f, 0.5f),
                    new Vector2(x - fontSize * 0.7f, 0f), new Vector2(iconSize, iconSize));
                if (icon.sprite == null)
                {
                    icon.sprite = UiKit.Circle;
                    icon.color = Palette.Lemon;
                }
                Text label = UiKit.Label(parent, "Count_" + m, "×" + mats[m], new Vector2(0.5f, 0.5f),
                    new Vector2(x + iconSize * 0.5f + 2f, -4f), new Vector2(fontSize * 2.2f, fontSize * 1.4f),
                    fontSize, Cocoa, TextAnchor.MiddleLeft);
                AddRim(label);
                x += cell;
            }
            return count * cell;
        }

        /// <summary>过关后材料从画面中间飞入，不再出「获得材料」字条。</summary>
        public static void ShowGain(MonoBehaviour host, RectTransform parent, GameDatabase db, int[] mats)
        {
            if (host == null || parent == null || mats == null || !CraftCatalog.Any(mats)) return;
            int n = 0;
            int limit = Mathf.Min(mats.Length, CraftCatalog.MatCount);
            for (int m = 0; m < limit; m++)
                if (mats[m] > 0) n++;
            if (n == 0) return;
            var bits = new RewardFly.Bit[n];
            int i = 0;
            for (int m = 0; m < limit; m++)
            {
                if (mats[m] <= 0) continue;
                bits[i] = new RewardFly.Bit
                {
                    sprite = MatIcon(db, m),
                    count = mats[m],
                    world = parent.position,
                    nudge = (i - (n - 1) * 0.5f) * 96f,
                };
                i++;
            }
            RewardFly.Play(host, bits, db, false);
        }

        public static void AddRim(Text label) => AddRim(label, Rim, 1.5f);

        public static void AddRim(Text label, Color color, float width = 2f)
        {
            var rim = label.gameObject.AddComponent<Outline>();
            rim.effectColor = color;
            rim.effectDistance = new Vector2(width, -width);
        }
    }
}
