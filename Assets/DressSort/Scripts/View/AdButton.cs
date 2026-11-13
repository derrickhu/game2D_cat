using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 看广告的按钮：薄荷绿胶囊，文字左侧固定一枚播放标。
    /// 图标用 Pack/ad，和活动页锁列上的是同一张。
    /// </summary>
    public static class AdButton
    {
        static readonly Color Ink = new Color32(0x4A, 0x26, 0x2C, 0xFF);

        public static Sprite Face(GameDatabase db)
        {
            if (db == null) return null;
            if (db.adBtn != null) return db.adBtn;
            return db.craftBtnMint;
        }

        public static Sprite Mark(GameDatabase db) => db != null ? db.packAd : null;

        /// <summary>换成广告按钮的底，并在文字左边放上播放标。</summary>
        public static void Apply(Button button, GameDatabase db)
        {
            if (button == null) return;
            RectTransform root = button.transform as RectTransform;
            Image face = button.targetGraphic as Image;
            Sprite pill = Face(db);
            float height = root != null ? root.sizeDelta.y : 0f;
            if (height < 8f) height = 100f;
            if (face != null && pill != null)
            {
                face.preserveAspect = false;
                UiKit.FitPill(face, pill, height);
            }
            PlaceIcon(button, db, height);
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
                label.color = Ink;
        }

        public static void ShowIcon(Button button, bool show)
        {
            if (button == null) return;
            Transform icon = button.transform.Find("Ad");
            if (icon != null) icon.gameObject.SetActive(show);
            if (show) return;
            Text label = button.GetComponentInChildren<Text>();
            RectTransform root = button.transform as RectTransform;
            if (label == null || root == null) return;
            Vector2 pos = label.rectTransform.anchoredPosition;
            pos.x = 0f;
            label.rectTransform.anchoredPosition = pos;
            label.rectTransform.sizeDelta = new Vector2(Mathf.Max(80f, root.sizeDelta.x - 36f), label.rectTransform.sizeDelta.y);
        }

        static void PlaceIcon(Button button, GameDatabase db, float height)
        {
            RectTransform root = button.transform as RectTransform;
            if (root == null) return;
            float iconSize = Mathf.Clamp(height * 0.62f, 42f, 84f);
            float pad = Mathf.Clamp(height * 0.2f, 16f, 40f);
            Transform existing = button.transform.Find("Ad");
            Image icon = existing != null ? existing.GetComponent<Image>() : null;
            if (icon == null)
                icon = UiKit.Icon(button.transform, "Ad", Mark(db), new Vector2(0f, 0.5f), Vector2.zero,
                    new Vector2(iconSize, iconSize));
            icon.sprite = Mark(db);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.gameObject.SetActive(icon.sprite != null);
            if (icon.sprite == null) return;
            RectTransform rect = icon.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(iconSize, iconSize);
            rect.anchoredPosition = new Vector2(pad, height * 0.03f);

            Text label = button.GetComponentInChildren<Text>();
            if (label == null || label.transform == icon.transform) return;
            float shift = (pad + iconSize) * 0.46f;
            RectTransform text = label.rectTransform;
            float y = text.anchoredPosition.y;
            text.anchoredPosition = new Vector2(shift, Mathf.Abs(y) < 0.5f ? height * 0.04f : y);
            text.sizeDelta = new Vector2(Mathf.Max(80f, root.sizeDelta.x - pad - iconSize - 28f),
                Mathf.Max(text.sizeDelta.y, height * 0.62f));
        }
    }
}
