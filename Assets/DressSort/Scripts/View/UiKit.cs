using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>界面用的颜色与尺寸基准，跟原型图一致。</summary>
    public static class Palette
    {
        public static readonly Color Sky = new Color32(0xF7, 0xFB, 0xFC, 0xFF);
        public static readonly Color Cream = new Color32(0xFF, 0xF6, 0xEC, 0xFF);
        public static readonly Color Ink = new Color32(0x2C, 0x24, 0x33, 0xFF);
        public static readonly Color Rose = new Color32(0xF3, 0xA7, 0xB8, 0xFF);
        public static readonly Color RoseDark = new Color32(0xD1, 0x27, 0x6C, 0xFF);
        public static readonly Color Aqua = new Color32(0x6F, 0xCF, 0xC8, 0xFF);
        public static readonly Color Teal = new Color32(0x2A, 0x9B, 0x96, 0xFF);
        public static readonly Color Lemon = new Color32(0xF0, 0xC8, 0x4A, 0xFF);
        public static readonly Color Lime = new Color32(0xA8, 0xE8, 0x5C, 0xFF);
        public static readonly Color Lane = new Color32(0xFF, 0xF6, 0xEC, 0xFF);
        public static readonly Color Locked = new Color32(0xC3, 0xC7, 0xCE, 0xFF);
        public static readonly Color Caption = new Color32(0x1B, 0x72, 0x6E, 0xFF);
        public static readonly Color Wood = new Color32(0xC8, 0x96, 0x5A, 0xFF);
    }

    public enum Chip
    {
        Teal,
        Pink,
        White,
    }

    /// <summary>
    /// 界面控件。可见的底板、按钮、列框一律用 GameDatabase 里的切图；
    /// 切图还没接上时才退回一张运行时圆角，避免编辑器空引用。
    /// </summary>
    public static class UiKit
    {
        public const float RefWidth = 1080f;
        public const float RefHeight = 1920f;

        public static GameDatabase Skin;

        static Sprite roundedSprite;
        static Sprite rectSprite;
        static Sprite circleSprite;
        static Sprite octagonSprite;
        static Font font;

        public static Font Font
        {
            get
            {
                if (font != null) return font;
                // 优先用工程里自带的中文字体，没有就退回内置字体
                Font[] all = Resources.LoadAll<Font>("Fonts");
                if (all != null && all.Length > 0)
                    font = all[0];
                if (font == null)
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static Sprite Rounded
        {
            get
            {
                if (roundedSprite == null)
                    roundedSprite = BuildRounded(48, 20, 100f);
                return roundedSprite;
            }
        }

        /// <summary>圆角较小的长方形底板，用在衣柜格子和操作按钮上。</summary>
        public static Sprite SoftRect
        {
            get
            {
                if (rectSprite == null)
                    rectSprite = BuildRounded(48, 6, 100f);
                return rectSprite;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circleSprite == null)
                    circleSprite = BuildRounded(64, 32, 100f);
                return circleSprite;
            }
        }

        /// <summary>衣架格用的正八边形，白底细描边，跟秀台那版装扮页一致。</summary>
        public static Sprite Octagon
        {
            get
            {
                if (octagonSprite == null)
                    octagonSprite = BuildOctagon(128);
                return octagonSprite;
            }
        }

        static Sprite BuildRounded(int size, int radius, float ppu)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 到圆角中心的距离，用半像素过渡做抗锯齿
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            int b = radius;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        static Sprite BuildOctagon(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color[size * size];
            float pad = 3f;
            float inner = size - pad;
            float cut = inner * (Mathf.Sqrt(2f) - 1f) * 0.5f;
            float ox = pad * 0.5f;
            const float border = 3.4f;
            var fill = Color.white;
            var ink = new Color(0.17f, 0.14f, 0.2f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = OctagonDist(x + 0.5f - ox, y + 0.5f - ox, inner, cut);
                    float a = Mathf.Clamp01(0.55f - d);
                    if (a <= 0.001f)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    float t = Mathf.Clamp01((-d - 0.4f) / border);
                    Color col = Color.Lerp(ink, fill, t);
                    col.a = a;
                    pixels[y * size + x] = col;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
        }

        static float OctagonDist(float x, float y, float s, float c)
        {
            float d = Mathf.Max(-x, x - s, -y, y - s);
            d = Mathf.Max(d, c - (x + y));
            d = Mathf.Max(d, c - (s - x + y));
            d = Mathf.Max(d, c - (s - x + s - y));
            d = Mathf.Max(d, c - (x + s - y));
            return d;
        }

        // ------------------------------------------------------------ 布局

        public static RectTransform Rect(Transform parent, string name, Vector2 anchor,
            Vector2 offset, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>铺满父级的空容器。子节点按 (0.5, 1) 锚点定位时，偏移量就是相对页面顶边的距离。</summary>
        public static RectTransform Stretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static Sprite SpriteOf(Chip chip)
        {
            if (Skin != null)
            {
                Sprite art = chip == Chip.Teal ? Skin.uiBtnTeal
                    : chip == Chip.Pink ? Skin.uiBtnPink
                    : Skin.uiBtnWhite;
                if (art != null) return art;
            }
            return Rounded;
        }

        public static Sprite CardSprite(bool selected = false)
        {
            if (Skin != null)
            {
                Sprite art = selected && Skin.uiCardOn != null ? Skin.uiCardOn : Skin.uiCard;
                if (art != null) return art;
            }
            return Rounded;
        }

        public static Color TextOn(Chip chip) =>
            chip == Chip.White ? Palette.Ink : Color.white;

        public static Image Slice(Transform parent, string name, Sprite sprite, Vector2 anchor,
            Vector2 offset, Vector2 size, Color color)
        {
            RectTransform rect = Rect(parent, name, anchor, offset, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : Rounded;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Image Backdrop(Transform parent, Sprite sprite)
        {
            RectTransform rect = Stretch(parent, "Backdrop");
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
            image.raycastTarget = false;
            if (sprite == null)
                image.color = Palette.Sky;
            return image;
        }

        public static Image ChipPlate(Transform parent, string name, Chip chip, Vector2 anchor,
            Vector2 offset, Vector2 size)
        {
            return Slice(parent, name, SpriteOf(chip), anchor, offset, size, Color.white);
        }

        public static Image Panel(Transform parent, string name, Vector2 anchor, Vector2 offset,
            Vector2 size, Color color)
        {
            // 深色底板改成白胶囊，避免把切图染成一团墨
            bool dark = color.r + color.g + color.b < 1.2f;
            Sprite sprite = dark ? SpriteOf(Chip.White) : CardSprite();
            Color tint = dark ? Color.white : (color.a < 0.99f ? color : Color.white);
            if (!dark && color != Color.white && color != Palette.Cream && color != Palette.Lane)
                tint = color;
            return Slice(parent, name, sprite, anchor, offset, size, tint);
        }

        public static Image Icon(Transform parent, string name, Sprite sprite,
            Vector2 anchor, Vector2 offset, Vector2 size)
        {
            RectTransform rect = Rect(parent, name, anchor, offset, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        public static Text Label(Transform parent, string name, string content, Vector2 anchor,
            Vector2 offset, Vector2 size, int fontSize, Color color,
            TextAnchor align = TextAnchor.MiddleCenter)
        {
            RectTransform rect = Rect(parent, name, anchor, offset, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Font;
            text.fontSize = fontSize;
            text.text = content;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static Button Button(Transform parent, string caption, Vector2 anchor, Vector2 offset,
            Vector2 size, Chip chip, int fontSize, UnityAction onClick)
        {
            return Button(parent, caption, anchor, offset, size, SpriteOf(chip), TextOn(chip),
                fontSize, onClick);
        }

        public static Button Button(Transform parent, string caption, Vector2 anchor, Vector2 offset,
            Vector2 size, Color face, Color textColor, int fontSize, UnityAction onClick)
        {
            return Button(parent, caption, anchor, offset, size, ChipFrom(face), fontSize, onClick);
        }

        static Chip ChipFrom(Color face)
        {
            if (face.r > 0.85f && face.g > 0.85f && face.b > 0.8f) return Chip.White;
            if (face.r > 0.7f && face.r > face.b + 0.08f && face.g > 0.55f) return Chip.Pink;
            if (face.r > 0.75f && face.g < 0.45f) return Chip.Pink;
            return Chip.Teal;
        }

        public static Button Button(Transform parent, string caption, Vector2 anchor, Vector2 offset,
            Vector2 size, Sprite sprite, Color textColor, int fontSize, UnityAction onClick)
        {
            RectTransform rect = Rect(parent, "Btn_" + caption, anchor, offset, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : Rounded;
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            Label(rect, "Text", caption, new Vector2(0.5f, 0.5f), Vector2.zero,
                size, fontSize, textColor);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var colors = button.colors;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.88f, 0.88f, 0.88f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            return button;
        }

        /// <summary>只负责接点击的透明按钮，用在整列热区、格子这些地方。</summary>
        public static Button HitArea(Transform parent, string name, Vector2 anchor, Vector2 offset,
            Vector2 size, UnityAction onClick)
        {
            RectTransform rect = Rect(parent, name, anchor, offset, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);
            return button;
        }

        /// <summary>
        /// 销毁节点。编辑器非播放模式下 Destroy 是延迟的、实际不会执行，
        /// 截图自检时旧节点会一层层堆在画面上，所以这里按模式分发。
        /// </summary>
        public static void Discard(Component component)
        {
            if (component == null) return;
            if (Application.isPlaying)
                Object.Destroy(component.gameObject);
            else
                Object.DestroyImmediate(component.gameObject);
        }

        public static Color Multiply(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        public static Color Gray(Color c)
        {
            float v = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            return new Color(Mathf.Lerp(v, 0.78f, 0.5f), Mathf.Lerp(v, 0.79f, 0.5f),
                Mathf.Lerp(v, 0.82f, 0.5f), c.a);
        }
    }
}
