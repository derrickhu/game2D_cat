using UnityEngine;
using UnityEngine.UI;

namespace DressSort
{
    /// <summary>
    /// 翅膀周围的小粒子。湖水翼往下滴，蜜桃花瓣往上飘，其余翅膀用星光。
    /// 画在人偶最上层，从翅膀两侧冒出来。
    /// </summary>
    public class WingMotes : MonoBehaviour
    {
        enum Kind
        {
            Drop,
            Heart,
            Spark,
        }

        const int Count = 14;

        Image[] motes;
        float[] age;
        float[] life;
        float[] vx;
        float[] vy;
        Vector2[] origin;
        Kind kind = Kind.Spark;
        Color tint = Color.white;
        Color hot = Color.white;
        string playingId;
        bool playing;
        bool placed;

        static Sprite heartSprite;
        static Sprite sparkSprite;

        public void Play(ItemDef wing)
        {
            string id = wing != null && wing.worn != null ? wing.id : null;
            if (id == playingId)
                return;
            playingId = id;
            if (id == null)
            {
                playing = false;
                placed = false;
                Hide();
                return;
            }

            if (id == "wing_aqua")
            {
                kind = Kind.Drop;
                tint = new Color(0.35f, 0.92f, 1f, 1f);
                hot = new Color(0.85f, 1f, 1f, 1f);
            }
            else if (id == "wing_rose")
            {
                kind = Kind.Heart;
                tint = new Color(1f, 0.42f, 0.62f, 1f);
                hot = new Color(1f, 0.78f, 0.86f, 1f);
            }
            else if (id == "wing_wisteria")
            {
                kind = Kind.Spark;
                tint = new Color(0.72f, 0.48f, 1f, 1f);
                hot = new Color(1f, 0.82f, 0.96f, 1f);
            }
            else
            {
                kind = Kind.Spark;
                tint = new Color(1f, 0.86f, 0.45f, 1f);
                hot = Color.white;
            }

            Ensure();
            ApplySprite();
            playing = true;
            placed = false;
        }

        void Ensure()
        {
            if (motes != null)
                return;
            motes = new Image[Count];
            age = new float[Count];
            life = new float[Count];
            vx = new float[Count];
            vy = new float[Count];
            origin = new Vector2[Count];
            for (int i = 0; i < Count; i++)
            {
                Image mote = UiKit.Icon(transform, "Mote", UiKit.Circle, new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(18f, 18f));
                mote.raycastTarget = false;
                mote.color = new Color(1f, 1f, 1f, 0f);
                motes[i] = mote;
            }
        }

        void ApplySprite()
        {
            Sprite sprite = kind == Kind.Heart ? Heart()
                : kind == Kind.Spark ? Spark()
                : UiKit.Circle;
            for (int i = 0; i < motes.Length; i++)
            {
                motes[i].sprite = sprite;
                motes[i].enabled = true;
            }
        }

        void Hide()
        {
            if (motes == null)
                return;
            for (int i = 0; i < motes.Length; i++)
            {
                motes[i].enabled = false;
                motes[i].color = new Color(1f, 1f, 1f, 0f);
            }
        }

        void Update()
        {
            if (!playing || motes == null)
                return;
            var area = (RectTransform)transform;
            float width = area.rect.width;
            float height = area.rect.height;
            if (width < 8f || height < 8f)
                return;
            if (!placed)
            {
                for (int i = 0; i < motes.Length; i++)
                    Respawn(i, width, height, true);
                placed = true;
            }

            float dt = Time.deltaTime;
            for (int i = 0; i < motes.Length; i++)
            {
                age[i] += dt;
                if (age[i] >= life[i])
                    Respawn(i, width, height, false);
                float k = Mathf.Clamp01(age[i] / life[i]);
                float fade = Mathf.Sin(k * Mathf.PI);
                Vector2 p = origin[i];
                p.x += vx[i] * age[i] + Mathf.Sin(age[i] * 3.2f + i) * 10f;
                p.y += vy[i] * age[i];
                RectTransform rect = motes[i].rectTransform;
                rect.anchoredPosition = p;
                Color color = Color.Lerp(tint, hot, (i & 1) == 0 ? 0.25f : 0.85f);
                color.a = fade;
                motes[i].color = color;
                float scale = Mathf.Lerp(0.4f, 1f, fade);
                rect.localScale = new Vector3(scale, scale, 1f);
                if (kind != Kind.Drop)
                    rect.localRotation = Quaternion.Euler(0f, 0f, age[i] * (kind == Kind.Heart ? 40f : 80f) + i * 30f);
            }
        }

        void Respawn(int i, float width, float height, bool scatter)
        {
            bool left = (i & 1) == 0;
            float x = (left ? -1f : 1f) * Random.Range(0.26f, 0.46f) * width;
            float y = Random.Range(-0.04f, 0.18f) * height;
            origin[i] = new Vector2(x, y);
            life[i] = Random.Range(0.9f, 1.7f);
            age[i] = scatter ? Random.Range(0f, life[i]) : 0f;
            vx[i] = (left ? -1f : 1f) * Random.Range(6f, 18f);
            if (kind == Kind.Drop)
                vy[i] = -Random.Range(36f, 78f);
            else if (kind == Kind.Heart)
                vy[i] = Random.Range(22f, 48f);
            else
                vy[i] = Random.Range(10f, 28f);
            float size = kind == Kind.Heart ? Random.Range(16f, 26f) : Random.Range(12f, 20f);
            motes[i].rectTransform.sizeDelta = new Vector2(size, size);
        }

        static Sprite Heart()
        {
            if (heartSprite != null)
                return heartSprite;
            const int n = 48;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float px = (x + 0.5f) / n * 2.6f - 1.3f;
                    float py = (y + 0.5f) / n * 2.6f - 1.15f;
                    float a = px * px + py * py - 1f;
                    float v = a * a * a - px * px * py * py * py;
                    float edge = Mathf.Clamp01(0.45f - v * 2.4f);
                    pixels[y * n + x] = new Color(1f, 1f, 1f, edge);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            heartSprite = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
            return heartSprite;
        }

        static Sprite Spark()
        {
            if (sparkSprite != null)
                return sparkSprite;
            const int n = 48;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f;
                    float dy = (y + 0.5f) / n * 2f - 1f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float spikes = Mathf.Abs(Mathf.Cos(ang * 2f));
                    float radius = Mathf.Lerp(0.16f, 0.92f, spikes * spikes);
                    float edge = Mathf.Clamp01((radius - dist) / 0.14f);
                    pixels[y * n + x] = new Color(1f, 1f, 1f, edge);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            sparkSprite = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
            return sparkSprite;
        }
    }
}
