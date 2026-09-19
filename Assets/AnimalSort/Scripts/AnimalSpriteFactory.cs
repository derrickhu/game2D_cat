using UnityEngine;

public static class AnimalSpriteFactory
{
    public const int CanvasSize = 512;
    public const float PixelsPerUnit = 400f;

    public static Sprite Get(AnimalType type)
    {
        if (type == AnimalType.Rainbow)
            return Rainbow();

        var loaded = Resources.Load<Sprite>("Animals/" + type);
        if (loaded != null)
            return loaded;

        return Fallback(type);
    }

    public static Color BodyColor(AnimalType type)
    {
        switch (type)
        {
            case AnimalType.Cow: return new Color(0.98f, 0.98f, 0.96f);
            case AnimalType.Pig: return new Color(1f, 0.72f, 0.78f);
            case AnimalType.Bear: return new Color(0.45f, 0.28f, 0.18f);
            case AnimalType.Cat: return new Color(0.42f, 0.38f, 0.72f);
            case AnimalType.Wolf: return new Color(0.62f, 0.64f, 0.68f);
            default: return Color.white;
        }
    }

    static Sprite rainbow;

    static Sprite Rainbow()
    {
        if (rainbow != null) return rainbow;

        var pixels = NewCanvas(out Texture2D tex);
        float c = CanvasSize * 0.5f;
        float outer = 215f;
        Color[] bands =
        {
            new Color(0.95f, 0.24f, 0.28f),
            new Color(1f, 0.56f, 0.16f),
            new Color(1f, 0.86f, 0.22f),
            new Color(0.29f, 0.78f, 0.4f),
            new Color(0.26f, 0.56f, 0.95f),
            new Color(0.58f, 0.36f, 0.9f)
        };

        for (int y = 0; y < CanvasSize; y++)
        {
            for (int x = 0; x < CanvasSize; x++)
            {
                float dx = x + 0.5f - c;
                float dy = y + 0.5f - c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                if (r > outer) continue;

                if (r > outer - 14f)
                {
                    pixels[y * CanvasSize + x] = new Color(0.12f, 0.12f, 0.14f);
                    continue;
                }

                float t = Mathf.Repeat((Mathf.Atan2(dy, dx) + Mathf.PI) / (Mathf.PI * 2f), 1f);
                int band = Mathf.Clamp((int)(t * bands.Length), 0, bands.Length - 1);
                pixels[y * CanvasSize + x] = bands[band];
            }
        }

        rainbow = Finish(tex, pixels, "Proc_Rainbow");
        return rainbow;
    }

    static Sprite Fallback(AnimalType type)
    {
        var pixels = NewCanvas(out Texture2D tex);
        Color fill = BodyColor(type);
        Color line = new Color(0.12f, 0.12f, 0.14f);
        float c = CanvasSize * 0.5f;

        Disc(pixels, c, c, 215f, line);
        Disc(pixels, c, c, 200f, fill);
        Disc(pixels, c - 60f, c + 30f, 34f, line);
        Disc(pixels, c + 60f, c + 30f, 34f, line);
        Disc(pixels, c - 60f, c + 30f, 24f, Color.white);
        Disc(pixels, c + 60f, c + 30f, 24f, Color.white);
        Disc(pixels, c - 56f, c + 28f, 13f, line);
        Disc(pixels, c + 64f, c + 28f, 13f, line);

        return Finish(tex, pixels, "Proc_" + type);
    }

    static Color[] NewCanvas(out Texture2D tex)
    {
        tex = new Texture2D(CanvasSize, CanvasSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color[CanvasSize * CanvasSize];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.clear;
        return pixels;
    }

    static Sprite Finish(Texture2D tex, Color[] pixels, string name)
    {
        tex.SetPixels(pixels);
        tex.Apply();
        tex.name = name;
        return Sprite.Create(tex, new Rect(0, 0, CanvasSize, CanvasSize), new Vector2(0.5f, 0.5f), PixelsPerUnit);
    }

    static void Disc(Color[] pixels, float cx, float cy, float radius, Color color)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - radius));
        int maxX = Mathf.Min(CanvasSize - 1, Mathf.CeilToInt(cx + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(cy - radius));
        int maxY = Mathf.Min(CanvasSize - 1, Mathf.CeilToInt(cy + radius));
        float r2 = radius * radius;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x + 0.5f - cx;
                float dy = y + 0.5f - cy;
                if (dx * dx + dy * dy <= r2)
                    pixels[y * CanvasSize + x] = color;
            }
        }
    }
}
