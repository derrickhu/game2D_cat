using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>材料图标、材料数量行和过关后飘出的材料获得条。</summary>
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

        /// <summary>过关后在页面上方飘一条材料获得，停一会儿再淡出。</summary>
        public static void ShowGain(MonoBehaviour host, RectTransform parent, GameDatabase db, int[] mats)
        {
            if (host == null || parent == null || !CraftCatalog.Any(mats)) return;
            Image plate = UiKit.Slice(parent, "MaterialGain", UiKit.SpriteOf(Chip.White), new Vector2(0.5f, 1f),
                new Vector2(0f, -330f), new Vector2(900f, 132f), Color.white);
            plate.transform.SetAsLastSibling();
            Text title = UiKit.Label(plate.transform, "Title", "获得材料", new Vector2(0.5f, 1f),
                new Vector2(0f, -4f), new Vector2(400f, 44f), 28, Cocoa);
            title.fontStyle = FontStyle.Bold;
            RectTransform row = UiKit.Rect(plate.transform, "Row", new Vector2(0.5f, 0.5f),
                new Vector2(0f, -16f), new Vector2(880f, 72f));
            float width = BuildRow(row, db, mats, 64f, 30);
            plate.rectTransform.sizeDelta = new Vector2(Mathf.Clamp(width + 60f, 420f, 1000f), 132f);
            host.StartCoroutine(FadeGain(plate));
        }

        static IEnumerator FadeGain(Image plate)
        {
            var group = plate.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            RectTransform rect = plate.rectTransform;
            Vector2 rest = rect.anchoredPosition;
            float t = 0f;
            while (t < 0.25f && plate != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.25f);
                group.alpha = k;
                rect.anchoredPosition = rest + new Vector2(0f, (1f - k) * 40f);
                yield return null;
            }
            yield return new WaitForSeconds(1.8f);
            t = 0f;
            while (t < 0.35f && plate != null)
            {
                t += Time.deltaTime;
                group.alpha = 1f - Mathf.Clamp01(t / 0.35f);
                yield return null;
            }
            if (plate != null)
                UiKit.Discard(plate);
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
