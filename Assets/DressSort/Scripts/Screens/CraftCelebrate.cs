using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 工坊做成一件衣服后，就在本页弹出的庆祝。绣绷、针脚和彩珠，不走关卡结算那套礼盒红幕。
    /// </summary>
    public class CraftCelebrate : MonoBehaviour
    {
        const int Stitches = 16;
        const int Beads = 22;

        static readonly Color Cocoa = new Color(0.42f, 0.24f, 0.2f, 1f);
        static readonly Color Cream = new Color(1f, 0.96f, 0.88f, 1f);
        static readonly Color Mint = new Color(0.62f, 0.88f, 0.74f, 1f);
        static readonly Color[] BeadsColors =
        {
            new Color(0.62f, 0.88f, 0.74f),
            new Color(1f, 0.72f, 0.64f),
            new Color(0.93f, 0.45f, 0.58f),
            new Color(0.76f, 0.66f, 0.9f),
            new Color(1f, 0.86f, 0.45f),
            new Color(1f, 0.97f, 0.9f),
        };

        struct Stitch
        {
            public RectTransform rect;
            public float angle;
            public float spin;
        }

        struct Bead
        {
            public RectTransform rect;
            public Image image;
            public Vector2 vel;
            public float life;
        }

        RectTransform hoop;
        RectTransform picture;
        RectTransform orbit;
        CanvasGroup caption;
        CanvasGroup nameGroup;
        RectTransform wearRect;
        RectTransform keepRect;
        Button wearButton;
        Button keepButton;
        Image dim;
        Image shock;
        readonly Stitch[] stitches = new Stitch[Stitches];
        readonly List<Bead> beads = new List<Bead>();
        float age;
        bool popped;
        bool closed;

        public static CraftCelebrate Show(RectTransform parent, ItemDef item, GameDatabase db,
            Action onWear, Action onKeep)
        {
            var go = new GameObject("CraftCelebrate", typeof(RectTransform), typeof(CraftCelebrate));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();
            var view = go.GetComponent<CraftCelebrate>();
            view.Build(item, db, onWear, onKeep);
            return view;
        }

        public void Close()
        {
            if (closed) return;
            closed = true;
            Destroy(gameObject);
        }

        void Build(ItemDef item, GameDatabase db, Action onWear, Action onKeep)
        {
            Image shade = UiKit.Icon(transform, "Dim", UiKit.SoftRect, Vector2.zero, Vector2.zero, Vector2.one * 4f);
            var shadeRect = shade.rectTransform;
            shadeRect.anchorMin = Vector2.zero;
            shadeRect.anchorMax = Vector2.one;
            shadeRect.offsetMin = Vector2.zero;
            shadeRect.offsetMax = Vector2.zero;
            shade.type = Image.Type.Sliced;
            shade.color = new Color(0.34f, 0.18f, 0.2f, 0f);
            shade.raycastTarget = true;
            dim = shade;

            RectTransform stage = UiKit.Rect(transform, "Stage", new Vector2(0.5f, 0.58f), Vector2.zero, Vector2.zero);

            hoop = UiKit.Rect(stage, "Hoop", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Sprite frameSprite = HoopSprite();
            if (frameSprite != null)
            {
                float frameW = 720f;
                float frameH = frameW * frameSprite.rect.height / Mathf.Max(1f, frameSprite.rect.width);
                UiKit.Icon(hoop, "Frame", frameSprite, new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(frameW, frameH));
            }
            else
            {
                Disc(hoop, "Rim", 740f, Cocoa);
                Disc(hoop, "Cloth", 676f, Cream);
                Disc(hoop, "Wash", 620f, new Color(Mint.r, Mint.g, Mint.b, 0.95f));
            }
            shock = Disc(hoop, "Shock", 680f, new Color(1f, 0.98f, 0.93f, 0f));
            shock.transform.SetAsFirstSibling();

            Sprite pictureSprite = item.ResolveIcon() != null ? item.ResolveIcon() : item.worn;
            Vector2 pictureSize = item.slot == ItemSlot.Dress ? new Vector2(400f, 480f) : new Vector2(360f, 360f);
            Image icon = UiKit.Icon(hoop, "Picture", pictureSprite, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -24f), pictureSize);
            picture = icon.rectTransform;
            picture.localScale = Vector3.zero;
            hoop.localScale = Vector3.zero;

            orbit = UiKit.Rect(hoop, "Orbit", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            for (int i = 0; i < Stitches; i++)
            {
                float ang = i * Mathf.PI * 2f / Stitches;
                Color ink = BeadsColors[i % BeadsColors.Length];
                Image dot = UiKit.Icon(orbit, "Stitch", UiKit.Circle, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.one * (i % 2 == 0 ? 28f : 18f));
                dot.color = ink;
                stitches[i] = new Stitch { rect = dot.rectTransform, angle = ang, spin = i % 2 == 0 ? 140f : -90f };
            }

            var words = new GameObject("Words", typeof(RectTransform), typeof(CanvasGroup));
            words.transform.SetParent(stage, false);
            var wordRect = (RectTransform)words.transform;
            wordRect.anchorMin = new Vector2(0.5f, 0.5f);
            wordRect.anchorMax = new Vector2(0.5f, 0.5f);
            wordRect.sizeDelta = Vector2.zero;
            caption = words.GetComponent<CanvasGroup>();
            caption.alpha = 0f;
            Text title = UiKit.Label(wordRect, "Title", "做好啦", new Vector2(0.5f, 0.5f),
                new Vector2(0f, 520f), new Vector2(640f, 90f), 68, CraftView.Cocoa);
            title.fontStyle = FontStyle.Bold;
            CraftView.AddRim(title, Cream, 3f);
            Text kind = UiKit.Label(wordRect, "Kind", KindOf(item), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 432f), new Vector2(640f, 48f), 32, new Color(0.72f, 0.28f, 0.38f));
            kind.fontStyle = FontStyle.Bold;
            CraftView.AddRim(kind);
            Text name = UiKit.Label(stage, "Name", item.displayName, new Vector2(0.5f, 0.5f),
                new Vector2(0f, -460f), new Vector2(760f, 64f), 44, CraftView.Cocoa);
            name.fontStyle = FontStyle.Bold;
            CraftView.AddRim(name, Cream, 2.5f);
            nameGroup = name.gameObject.AddComponent<CanvasGroup>();
            nameGroup.alpha = 0f;

            wearButton = Pill(transform, "现在换上", new Vector2(0f, -560f), new Vector2(560f, 116f),
                db != null ? db.craftBtnMint : null, Color.white, 44, () => onWear?.Invoke());
            wearRect = (RectTransform)wearButton.transform;
            wearRect.SetParent(stage, false);
            wearRect.anchoredPosition = new Vector2(0f, -590f);
            Text wearLabel = wearButton.GetComponentInChildren<Text>();
            wearLabel.fontStyle = FontStyle.Bold;
            wearLabel.rectTransform.anchoredPosition = new Vector2(0f, 4f);
            CraftView.AddRim(wearLabel, new Color(0.18f, 0.42f, 0.28f));
            wearRect.localScale = Vector3.zero;
            wearButton.interactable = false;
            KeepColor(wearButton);

            keepButton = Pill(transform, "先放衣柜", new Vector2(0f, -680f), new Vector2(420f, 92f),
                db != null ? db.dressupBtnCream : null, CraftView.Cocoa, 36, () => onKeep?.Invoke());
            keepRect = (RectTransform)keepButton.transform;
            keepRect.SetParent(stage, false);
            keepRect.anchoredPosition = new Vector2(0f, -720f);
            Text keepLabel = keepButton.GetComponentInChildren<Text>();
            keepLabel.fontStyle = FontStyle.Bold;
            keepLabel.rectTransform.anchoredPosition = new Vector2(0f, 4f);
            keepRect.localScale = Vector3.zero;
            keepButton.interactable = false;
            KeepColor(keepButton);
        }

        void Update()
        {
            if (closed) return;
            float dt = Time.deltaTime;
            age += dt;

            float shade = Smooth(age / 0.22f);
            dim.color = new Color(0.34f, 0.18f, 0.2f, shade * 0.58f);

            float hoopK = OutBack(Mathf.Clamp01(age / 0.46f));
            hoop.localScale = Vector3.one * hoopK;

            float picK = OutBack(Mathf.Clamp01((age - 0.1f) / 0.4f));
            picture.localScale = Vector3.one * picK;
            float breathe = age > 0.7f ? 1f + Mathf.Sin(age * 2.4f) * 0.028f : 1f;
            picture.localScale *= breathe;
            picture.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-10f, 0f, picK));

            float wave = Mathf.Clamp01((age - 0.12f) / 0.55f);
            shock.color = new Color(1f, 0.98f, 0.93f, (1f - wave) * 0.85f);
            shock.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.35f, wave);

            float radius = Mathf.Lerp(30f, 338f, OutBack(Mathf.Clamp01(age / 0.5f)));
            orbit.localRotation = Quaternion.Euler(0f, 0f, age * 16f);
            for (int i = 0; i < stitches.Length; i++)
            {
                float ang = stitches[i].angle;
                stitches[i].rect.anchoredPosition = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
                stitches[i].rect.localRotation = Quaternion.Euler(0f, 0f, age * stitches[i].spin);
            }

            if (caption != null)
                caption.alpha = Smooth(Mathf.Clamp01((age - 0.18f) / 0.28f));
            if (nameGroup != null)
                nameGroup.alpha = Smooth(Mathf.Clamp01((age - 0.28f) / 0.28f));

            float wearK = OutBack(Mathf.Clamp01((age - 0.42f) / 0.34f));
            float keepK = OutBack(Mathf.Clamp01((age - 0.52f) / 0.34f));
            wearRect.localScale = Vector3.one * wearK;
            keepRect.localScale = Vector3.one * keepK;
            if (age > 0.72f)
            {
                wearButton.interactable = true;
                keepButton.interactable = true;
            }

            if (!popped && age >= 0.16f)
            {
                popped = true;
                Sfx.Play(SfxId.Confetti);
                Burst();
            }
            StepBeads(dt);
        }

        void Burst()
        {
            for (int i = 0; i < Beads; i++)
            {
                Color ink = BeadsColors[i % BeadsColors.Length];
                float size = UnityEngine.Random.Range(16f, 34f);
                Image dot = UiKit.Icon(hoop, "Bead", UiKit.Circle, new Vector2(0.5f, 0.5f),
                    UnityEngine.Random.insideUnitCircle * 40f, Vector2.one * size);
                dot.color = ink;
                float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float speed = UnityEngine.Random.Range(420f, 980f);
                beads.Add(new Bead
                {
                    rect = dot.rectTransform,
                    image = dot,
                    vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed + new Vector2(0f, 280f),
                    life = UnityEngine.Random.Range(0.7f, 1.15f),
                });
            }
        }

        void StepBeads(float dt)
        {
            for (int i = beads.Count - 1; i >= 0; i--)
            {
                Bead bead = beads[i];
                if (bead.rect == null) { beads.RemoveAt(i); continue; }
                bead.life -= dt;
                bead.vel.y -= 1600f * dt;
                bead.rect.anchoredPosition += bead.vel * dt;
                float a = Mathf.Clamp01(bead.life / 0.35f);
                Color c = bead.image.color;
                c.a = a;
                bead.image.color = c;
                beads[i] = bead;
                if (bead.life <= 0f)
                {
                    Destroy(bead.rect.gameObject);
                    beads.RemoveAt(i);
                }
            }
        }

        static Sprite HoopSprite()
        {
            Sprite sprite = Resources.Load<Sprite>("Ui/craft_hoop");
#if UNITY_EDITOR
            if (sprite == null)
                sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/DressSort/Art/Ui/Workshop/craft_hoop.png");
#endif
            return sprite;
        }

        static string KindOf(ItemDef item)
        {
            if (item.slot == ItemSlot.Hair) return "新发型";
            if (item.slot == ItemSlot.Wings) return "新翅膀";
            return "新裙子";
        }

        static Image Disc(Transform parent, string name, float size, Color color)
        {
            Image image = UiKit.Icon(parent, name, UiKit.Circle, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(size, size));
            image.color = color;
            return image;
        }

        static Button Pill(Transform parent, string caption, Vector2 offset, Vector2 size, Sprite sprite,
            Color ink, int fontSize, Action onClick)
        {
            Button button = UiKit.Button(parent, caption, new Vector2(0.5f, 0.5f), offset, size,
                sprite, ink, fontSize, () => onClick?.Invoke());
            UiKit.FitPill(button.targetGraphic as Image, sprite, size.y);
            return button;
        }

        static void KeepColor(Button button)
        {
            ColorBlock colors = button.colors;
            colors.disabledColor = Color.white;
            button.colors = colors;
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static float OutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
