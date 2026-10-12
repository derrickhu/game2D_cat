using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 关卡页最上面一层特效：烟尘、星爆、光环、彩纸、飘字、横幅、震屏。
    /// 位置一律传世界坐标，这里自己换算成本层坐标，所以棋盘怎么摆都不影响。
    /// 碎片用池子复用，一局下来不会越堆越多。
    /// </summary>
    public class BoardFx : MonoBehaviour
    {
        class Bit
        {
            public RectTransform rect;
            public Image image;
            public Vector2 velocity;
            public float gravity;
            public float drag;
            public float spin;
            public float life;
            public float age;
            public float sway;
            public float grow;
            public float startSize;
            public Color tint;
            public bool flip;
        }

        static readonly Color Cocoa = new Color32(0x4A, 0x26, 0x2C, 0xFF);
        static readonly Color Gold = new Color32(0xFF, 0xD3, 0x4E, 0xFF);

        static Sprite ringSprite;
        static Sprite glowSprite;

        RectTransform layer;
        GameDatabase db;
        readonly List<Bit> bits = new List<Bit>();
        readonly Stack<Bit> spare = new Stack<Bit>();
        readonly Dictionary<RectTransform, Coroutine> shakes = new Dictionary<RectTransform, Coroutine>();

        public static BoardFx Create(Transform parent, GameDatabase db)
        {
            RectTransform rect = UiKit.Stretch(parent, "Fx");
            var fx = rect.gameObject.AddComponent<BoardFx>();
            fx.layer = rect;
            fx.db = db;
            return fx;
        }

        public RectTransform Layer => layer;

        Vector2 Local(Vector3 world) => layer.InverseTransformPoint(world);

        // ------------------------------------------------------------ 程序贴图

        /// <summary>空心圆环，光环扩散用。</summary>
        public static Sprite Ring
        {
            get
            {
                if (ringSprite == null)
                    ringSprite = BuildRadial(128, d => Mathf.Clamp01(1f - Mathf.Abs(d - 0.84f) / 0.12f));
                return ringSprite;
            }
        }

        /// <summary>中心实、边缘柔的光斑。</summary>
        public static Sprite Glow
        {
            get
            {
                if (glowSprite == null)
                    glowSprite = BuildRadial(128, d => Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f));
                return glowSprite;
            }
        }

        static Sprite BuildRadial(int size, System.Func<float, float> alphaAt)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, d > 1f ? 0f : alphaAt(d));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ------------------------------------------------------------ 碎片

        Bit Spawn(Sprite sprite, Vector2 at, float size, Color tint)
        {
            if (sprite == null) return null;
            Bit bit = spare.Count > 0 ? spare.Pop() : null;
            if (bit == null || bit.rect == null)
            {
                Image image = UiKit.Icon(layer, "Bit", sprite, new Vector2(0.5f, 0.5f), at, Vector2.one * size);
                bit = new Bit { rect = image.rectTransform, image = image };
            }
            bit.rect.gameObject.SetActive(true);
            bit.rect.SetAsLastSibling();
            bit.image.sprite = sprite;
            bit.image.color = tint;
            bit.rect.anchorMin = bit.rect.anchorMax = new Vector2(0.5f, 0.5f);
            bit.rect.sizeDelta = Vector2.one * size;
            bit.rect.anchoredPosition = at;
            bit.rect.localRotation = Quaternion.identity;
            bit.rect.localScale = Vector3.one;
            bit.velocity = Vector2.zero;
            bit.gravity = 0f;
            bit.drag = 0f;
            bit.spin = 0f;
            bit.life = 0.6f;
            bit.age = 0f;
            bit.sway = 0f;
            bit.grow = 0f;
            bit.startSize = size;
            bit.tint = tint;
            bit.flip = false;
            bits.Add(bit);
            return bit;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = bits.Count - 1; i >= 0; i--)
            {
                Bit bit = bits[i];
                if (bit.rect == null)
                {
                    bits.RemoveAt(i);
                    continue;
                }
                bit.age += dt;
                bit.velocity.y -= bit.gravity * dt;
                bit.velocity *= Mathf.Exp(-bit.drag * dt);
                Vector2 pos = bit.rect.anchoredPosition + bit.velocity * dt;
                if (bit.sway > 0f)
                    pos.x += Mathf.Sin(bit.age * 3.4f + i) * bit.sway * dt;
                bit.rect.anchoredPosition = pos;
                bit.rect.Rotate(0f, 0f, bit.spin * dt);
                float k = Mathf.Clamp01(bit.age / bit.life);
                float scale = 1f + bit.grow * k;
                float sx = bit.flip ? Mathf.Max(0.2f, Mathf.Abs(Mathf.Cos(bit.age * 7f + i))) : 1f;
                bit.rect.localScale = new Vector3(scale * sx, scale, 1f);
                Color c = bit.tint;
                c.a *= Mathf.Clamp01((bit.life - bit.age) / Mathf.Min(0.35f, bit.life * 0.6f));
                bit.image.color = c;
                if (bit.age >= bit.life)
                {
                    bit.rect.gameObject.SetActive(false);
                    bits.RemoveAt(i);
                    spare.Push(bit);
                }
            }
        }

        public void Clear()
        {
            foreach (Bit bit in bits)
            {
                if (bit.rect == null) continue;
                bit.rect.gameObject.SetActive(false);
                spare.Push(bit);
            }
            bits.Clear();
        }

        // ------------------------------------------------------------ 组合效果

        /// <summary>落地扬起的小烟团，左右各散开。</summary>
        public void Puff(Vector3 world, float size)
        {
            Sprite sprite = db != null ? db.boardPuff : null;
            Vector2 at = Local(world);
            int count = 4;
            for (int i = 0; i < count; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float s = size * Random.Range(0.32f, 0.46f);
                Bit bit = sprite != null
                    ? Spawn(sprite, at + new Vector2(side * size * 0.18f, -size * 0.05f), s, Color.white)
                    : Spawn(UiKit.Circle, at, s * 0.6f, new Color(1f, 1f, 1f, 0.9f));
                if (bit == null) continue;
                bit.velocity = new Vector2(side * Random.Range(160f, 300f), Random.Range(40f, 140f));
                bit.drag = 5.5f;
                bit.life = Random.Range(0.32f, 0.42f);
                bit.grow = 0.35f;
                bit.spin = side * Random.Range(30f, 90f);
            }
        }

        /// <summary>卡通星爆：先胀开再淡出，垫在主体后面。</summary>
        public void Burst(Vector3 world, float size, float life = 0.32f)
        {
            Sprite sprite = db != null ? db.boardBurst : null;
            if (sprite == null) return;
            Bit bit = Spawn(sprite, Local(world), size * 0.45f, Color.white);
            if (bit == null) return;
            bit.grow = 1.4f;
            bit.life = life;
            bit.spin = Random.Range(-90f, 90f);
        }

        public void RingWave(Vector3 world, float size, Color color, float life = 0.42f)
        {
            Bit bit = Spawn(Ring, Local(world), size * 0.35f, color);
            if (bit == null) return;
            bit.grow = 2.6f;
            bit.life = life;
        }

        public void Flash(Vector3 world, float size, Color color, float life = 0.28f)
        {
            Bit bit = Spawn(Glow, Local(world), size, color);
            if (bit == null) return;
            bit.grow = 0.6f;
            bit.life = life;
        }

        public void Sparkles(Vector3 world, int count, float speed, float size = 56f)
        {
            Sprite sprite = db != null ? db.rewardSparkle : null;
            if (sprite == null) return;
            Vector2 at = Local(world);
            for (int i = 0; i < count; i++)
            {
                float rad = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                Bit bit = Spawn(sprite, at, Random.Range(size * 0.7f, size * 1.15f), Color.white);
                if (bit == null) continue;
                bit.velocity = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * speed * Random.Range(0.7f, 1.1f);
                bit.drag = 3.4f;
                bit.life = Random.Range(0.5f, 0.75f);
                bit.spin = Random.Range(-200f, 200f);
                bit.grow = -0.4f;
            }
        }

        /// <summary>彩纸从一点往上喷，落下时翻面飘。</summary>
        public void Confetti(Vector3 world, int count, float power, float spread = 0.9f, float angle = 90f)
        {
            float aim = angle * Mathf.Deg2Rad;
            List<Sprite> pool = db != null ? db.rewardConfetti : null;
            if (pool == null || pool.Count == 0) return;
            Vector2 at = Local(world);
            for (int i = 0; i < count; i++)
            {
                Sprite sprite = pool[Random.Range(0, pool.Count)];
                Bit bit = Spawn(sprite, at, Random.Range(30f, 48f), Color.white);
                if (bit == null) continue;
                float rad = aim + Random.Range(-spread, spread);
                bit.velocity = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * power * Random.Range(0.55f, 1.05f);
                bit.gravity = 1500f;
                bit.drag = 1.6f;
                bit.life = Random.Range(1.1f, 1.6f);
                bit.spin = Random.Range(-540f, 540f);
                bit.sway = Random.Range(30f, 80f);
                bit.flip = true;
                bit.rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            }
        }

        /// <summary>整屏从上往下飘彩纸。</summary>
        public IEnumerator ConfettiRain(int count, float span)
        {
            List<Sprite> pool = db != null ? db.rewardConfetti : null;
            if (pool == null || pool.Count == 0) yield break;
            Rect area = layer.rect;
            float step = span / Mathf.Max(1, count);
            for (int i = 0; i < count; i++)
            {
                Vector2 from = new Vector2(Random.Range(-0.5f, 0.5f) * area.width, area.height * 0.5f + 40f);
                Bit bit = Spawn(pool[Random.Range(0, pool.Count)], from, Random.Range(34f, 54f), Color.white);
                if (bit != null)
                {
                    bit.velocity = new Vector2(Random.Range(-80f, 80f), -Random.Range(420f, 680f));
                    bit.gravity = 120f;
                    bit.life = 3.2f;
                    bit.spin = Random.Range(-400f, 400f);
                    bit.sway = Random.Range(60f, 120f);
                    bit.flip = true;
                    bit.rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                }
                if (step > 0f)
                    yield return new WaitForSeconds(step);
            }
        }

        // ------------------------------------------------------------ 字

        Text Word(string text, Vector2 at, int fontSize, Color fill, Color rim, float stroke = 5f)
        {
            Text label = UiKit.Label(layer, "Word", text, new Vector2(0.5f, 0.5f), at,
                new Vector2(900f, fontSize * 1.6f), fontSize, fill);
            label.fontStyle = FontStyle.Bold;
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = rim;
            outline.effectDistance = new Vector2(stroke, -stroke);
            var outline2 = label.gameObject.AddComponent<Outline>();
            outline2.effectColor = rim;
            outline2.effectDistance = new Vector2(-stroke * 0.8f, stroke * 0.8f);
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.29f, 0.08f, 0.14f, 0.55f);
            shadow.effectDistance = new Vector2(0f, -stroke * 2f);
            return label;
        }

        /// <summary>夸奖字：弹出、上浮、淡出。位置收进屏幕，避免贴在边列时被裁掉。</summary>
        public void FloatWord(Vector3 world, string text, Color fill, int fontSize = 48)
        {
            Vector2 at = Local(world);
            Rect area = layer.rect;
            float halfW = text.Length * fontSize * 0.55f;
            float limitX = Mathf.Max(0f, area.width * 0.5f - halfW - 28f);
            at.x = Mathf.Clamp(at.x, -limitX, limitX);
            float ceiling = area.height * 0.5f - fontSize - 150f;
            at.y = Mathf.Min(at.y, ceiling);
            Text label = Word(text, at, fontSize, fill, Color.white, 3f);
            StartCoroutine(FloatRoutine(label));
        }

        IEnumerator FloatRoutine(Text label)
        {
            RectTransform rect = label.rectTransform;
            Vector2 start = rect.anchoredPosition;
            float t = 0f;
            const float life = 0.95f;
            while (t < life && rect != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / life);
                float pop = k < 0.18f ? Ease.OutBack(k / 0.18f) * 1.0f : 1f;
                rect.localScale = Vector3.one * Mathf.Max(0.01f, pop);
                rect.anchoredPosition = start + new Vector2(0f, Ease.OutCubic(k) * 96f);
                Color c = label.color;
                c.a = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
                label.color = c;
                yield return null;
            }
            UiKit.Discard(label);
        }

        /// <summary>过关横幅：飘带从小弹大，停一会儿再缩回去。</summary>
        public IEnumerator Banner(string text, float hold)
        {
            Sprite ribbon = db != null ? db.rewardRibbon : null;
            RectTransform root = UiKit.Rect(layer, "Banner", new Vector2(0.5f, 0.5f), new Vector2(0f, 120f),
                new Vector2(900f, 267f));
            Image glow = UiKit.Icon(root, "Glow", Glow, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 700f));
            glow.color = new Color(1f, 0.95f, 0.75f, 0.85f);
            if (ribbon != null)
                UiKit.Icon(root, "Ribbon", ribbon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 267f));
            Text label = Word(text, new Vector2(0f, 26f), 84, Color.white, new Color32(0x8A, 0x1E, 0x3A, 0xFF));
            label.rectTransform.SetParent(root, false);
            label.rectTransform.anchoredPosition = new Vector2(0f, 26f);

            float t = 0f;
            const float inTime = 0.38f;
            while (t < inTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / inTime);
                root.localScale = Vector3.one * Ease.OutBack(k, 2.2f);
                root.localRotation = Quaternion.Euler(0f, 0f, (1f - k) * -8f);
                yield return null;
            }
            root.localScale = Vector3.one;
            root.localRotation = Quaternion.identity;

            t = 0f;
            while (t < hold)
            {
                t += Time.deltaTime;
                float breathe = 1f + Mathf.Sin(t * 6f) * 0.025f;
                root.localScale = Vector3.one * breathe;
                glow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, t * 20f);
                yield return null;
            }
        }

        public void ClearBanners()
        {
            for (int i = layer.childCount - 1; i >= 0; i--)
            {
                Transform child = layer.GetChild(i);
                if (child.name == "Banner" || child.name == "Splash" || child.name == "Word")
                    UiKit.Discard(child);
            }
        }

        /// <summary>难关开场：压暗、大红字砸下来、小字补一句，再收掉。</summary>
        public IEnumerator Splash(string title, string sub)
        {
            RectTransform root = UiKit.Stretch(layer, "Splash");
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0.12f, 0.04f, 0.08f, 0f);
            dim.raycastTarget = true;
            Text big = Word(title, new Vector2(0f, 140f), 120, new Color32(0xFF, 0x4D, 0x5E, 0xFF), Color.white);
            big.rectTransform.SetParent(root, false);
            big.rectTransform.anchoredPosition = new Vector2(0f, 140f);
            Text small = UiKit.Label(root, "Sub", sub, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f),
                new Vector2(900f, 70f), 44, Color.white);
            var rim = small.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color(0.29f, 0.08f, 0.14f, 0.8f);
            rim.effectDistance = new Vector2(2f, -2f);

            float t = 0f;
            const float total = 1.5f;
            while (t < total)
            {
                t += Time.deltaTime;
                float k = t / total;
                float fadeIn = Mathf.Clamp01(t / 0.18f);
                float fadeOut = Mathf.Clamp01((total - t) / 0.3f);
                dim.color = new Color(0.12f, 0.04f, 0.08f, 0.62f * fadeIn * fadeOut);
                float slam = t < 0.26f ? Mathf.Lerp(2.6f, 1f, Ease.OutCubic(t / 0.26f)) : 1f;
                big.rectTransform.localScale = Vector3.one * slam;
                Color c = big.color;
                c.a = Mathf.Clamp01(t / 0.12f) * fadeOut;
                big.color = c;
                Color s = small.color;
                s.a = Mathf.Clamp01((t - 0.25f) / 0.2f) * fadeOut;
                small.color = s;
                if (t > 0.26f && t - Time.deltaTime <= 0.26f)
                    Shake(layer.parent as RectTransform, 16f, 0.25f);
                yield return null;
            }
            UiKit.Discard(root);
        }

        // ------------------------------------------------------------ 震

        public void Shake(RectTransform target, float amplitude, float duration)
        {
            if (target == null) return;
            if (shakes.TryGetValue(target, out Coroutine running) && running != null)
                return;
            shakes[target] = StartCoroutine(ShakeRoutine(target, amplitude, duration));
        }

        IEnumerator ShakeRoutine(RectTransform target, float amplitude, float duration)
        {
            Vector2 home = target.anchoredPosition;
            float t = 0f;
            while (t < duration && target != null)
            {
                t += Time.deltaTime;
                float fall = 1f - Mathf.Clamp01(t / duration);
                target.anchoredPosition = home + new Vector2(
                    Mathf.Sin(t * 95f) * amplitude * fall,
                    Mathf.Cos(t * 77f) * amplitude * 0.6f * fall);
                yield return null;
            }
            if (target != null)
                target.anchoredPosition = home;
            shakes.Remove(target);
        }
    }

    public static class Ease
    {
        public static float OutCubic(float k) => 1f - Mathf.Pow(1f - Mathf.Clamp01(k), 3f);
        public static float OutQuad(float k) => 1f - (1f - k) * (1f - k);

        public static float OutBack(float k, float s = 1.7f)
        {
            k = Mathf.Clamp01(k) - 1f;
            return k * k * ((s + 1f) * k + s) + 1f;
        }

        /// <summary>阻尼弹簧：0 处位移 1，越往后晃得越小。</summary>
        public static float Spring(float k, float freq = 3.2f, float damp = 5f)
        {
            k = Mathf.Clamp01(k);
            return Mathf.Exp(-damp * k) * Mathf.Cos(freq * Mathf.PI * 2f * k);
        }
    }

    /// <summary>按下缩一点、松手弹回来。挂在任意按钮上。</summary>
    public class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float pressed = 0.9f;
        float rest = 1f;
        float target = 1f;
        float current = 1f;
        float velocity;
        bool down;

        /// <summary>松手后回到的大小，选中态的按钮会比 1 大一点。</summary>
        public float Rest
        {
            get => rest;
            set
            {
                rest = value;
                if (!down) target = rest;
            }
        }

        public static void Attach(Component host, float pressed = 0.9f)
        {
            if (host == null) return;
            var press = host.GetComponent<PressScale>();
            if (press == null) press = host.gameObject.AddComponent<PressScale>();
            press.pressed = pressed;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            down = true;
            target = rest * pressed;
        }

        public void OnPointerUp(PointerEventData eventData) => Lift();
        public void OnPointerExit(PointerEventData eventData) => Lift();

        void Lift()
        {
            down = false;
            target = rest;
        }

        void Update()
        {
            if (Mathf.Abs(current - target) < 0.0005f && Mathf.Abs(velocity) < 0.0005f) return;
            // 欠阻尼弹簧，松手时会冲过 1 一点再回来
            float force = (target - current) * 520f - velocity * 22f;
            velocity += force * Time.unscaledDeltaTime;
            current += velocity * Time.unscaledDeltaTime;
            transform.localScale = Vector3.one * current;
        }

        void OnDisable()
        {
            down = false;
            target = current = rest;
            velocity = 0f;
            transform.localScale = Vector3.one * rest;
        }
    }
}
