using UnityEngine;
using UnityEngine.UI;

// Rounded, softly bordered panels for every UI in the game, drawn in code and nine-sliced.
public static class UiSkin {
    static Sprite rounded;
    static Sprite pill;
    static Sprite glow;

    // A rounded rectangle with a faint lighter rim, for panels, cards and buttons.
    public static Sprite Rounded {
        get {
            if (rounded == null) {
                rounded = Make(64, 18, 0.12f, false);
            }
            return rounded;
        }
    }

    // Fully rounded ends, for bars.
    public static Sprite Pill {
        get {
            if (pill == null) {
                pill = Make(32, 15, 0f, false);
            }
            return pill;
        }
    }

    // A soft outer glow, for card halos.
    public static Sprite Glow {
        get {
            if (glow == null) {
                glow = Make(64, 30, 0f, true);
            }
            return glow;
        }
    }

    public static void Round(Image image, float scale = 1f) {
        if (image == null) {
            return;
        }
        image.sprite = Rounded;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f / Mathf.Max(0.1f, scale);
    }

    public static void Bar(Image image) {
        if (image == null) {
            return;
        }
        image.sprite = Pill;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 2f;
    }

    static Sprite Make(int size, int radius, float rim, bool soft) {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float a;
                if (soft) {
                    a = Mathf.Clamp01(1f - d / radius);
                    a = a * a;
                } else {
                    a = Mathf.Clamp01(radius - d + 0.5f);
                }
                float edge = Mathf.Clamp01(1f - Mathf.Abs(radius - 1.5f - d) / 1.5f) * rim;
                float top = Mathf.Lerp(0.92f, 1f, y / (float)size);
                float v = Mathf.Clamp01(top + edge * 3f);
                px[y * size + x] = new Color(v, v, v, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        int border = radius + 1;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }
}
