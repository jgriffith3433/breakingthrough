using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

// The Gen-painted PBR sets (Docs/Materials.md): eight tiling sets, each an albedo with roughness in its alpha (<set>_a.png) and a
// tangent-space normal (<set>_n.png), 512 px, painted by Hawkes Gen (image.paint seamless, then texture.maps).
//   Hollow (first download, Resources/Pbr/hollow): grass, dirt, cobble. HollowGround blends them on the arena ground with a splat mask.
//   Hub (on demand, group core, Resources/Pbr/hub): brick, wood, linoleum, carpet, rust. KitTextures.Get swaps them into the office
//   palette's wall, wood, metal and cloth materials, and OwnMapOffice splats wood, linoleum and carpet on the floor.
// Everything reads through OnDemand.Texture, so a phone that has not fetched the hub set simply keeps the code-made kit textures.
// Import is PbrImportRules (ASTC 6x6 albedo, ASTC 5x5 normal on web and phones, 4x4 mip limit applied by MobileQuality).
public static class PbrKit {
    public const string HollowDir = "Pbr/hollow/";
    public const string HubDir = "Pbr/hub/";
    public static readonly string[] HollowSets = { "grass", "dirt", "cobble" };
    public static readonly string[] HubSets = { "brick", "wood", "linoleum", "carpet", "rust" };

    public static int SplatMaterials { get; private set; }
    public static string Last { get; private set; } = "";
    static readonly List<Texture2D> used = new List<Texture2D>();
    static Texture2D noise;

    public static Texture2D Load(string dir, string set, string suffix) {
        Texture2D t = OnDemand.Texture(dir + set + suffix);
        if (t != null) {
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;
            if (!used.Contains(t)) {
                used.Add(t);
            }
        }
        return t;
    }

    // True when every set of a folder is there (the hub's are fetched on demand).
    public static bool Have(string dir, string[] sets) {
        foreach (string s in sets) {
            if (OnDemand.Texture(dir + s + "_a") == null || OnDemand.Texture(dir + s + "_n") == null) {
                return false;
            }
        }
        return true;
    }

    // Runtime bytes of every PBR texture in use (as the GPU holds them: compressed where the import said so), the mask and the noise.
    public static long Bytes() {
        long total = 0;
        foreach (Texture2D t in used) {
            if (t != null) {
                total += Profiler.GetRuntimeMemorySizeLong(t);
            }
        }
        if (noise != null) {
            total += Profiler.GetRuntimeMemorySizeLong(noise);
        }
        foreach (Texture2D m in masks) {
            if (m != null) {
                total += Profiler.GetRuntimeMemorySizeLong(m);
            }
        }
        return total;
    }

    static readonly List<Texture2D> masks = new List<Texture2D>();

    // A fetched PBR texture arrives as one flat RGBA32 level. Rebuild it with a mip chain (the ground shimmers without one), the albedo at
    // 512 and the normal at 256 (a box filter), flagged sRGB or linear as the import rule would: about 8.75 MB for the five hub sets.
    public static Texture2D WithMips(Texture2D src, bool normal) {
        int cap = normal ? 256 : 512;
        int f = Mathf.Max(1, src.width / cap);
        int w = src.width / f, h = src.height / f;
        Color32[] px = src.GetPixels32();
        var outPx = new Color32[w * h];
        for (int y = 0; y < h; y++) {
            for (int x = 0; x < w; x++) {
                int r = 0, g = 0, b = 0, a = 0;
                for (int j = 0; j < f; j++) {
                    for (int i = 0; i < f; i++) {
                        Color32 c = px[(y * f + j) * src.width + x * f + i];
                        r += c.r; g += c.g; b += c.b; a += c.a;
                    }
                }
                int n = f * f;
                outPx[y * w + x] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), (byte)(a / n));
            }
        }
        var t = new Texture2D(w, h, TextureFormat.RGBA32, true, normal);
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Trilinear;
        t.anisoLevel = 4;
        t.SetPixels32(outPx);
        t.Apply(true, true);
        Object.Destroy(src);
        return t;
    }

    // ---- the noise texture: R,G a detail normal (0.6 m grain), B a smooth macro field (75 m), A another macro field -----------------

    static float Hash(int x, int y, int seed) {
        unchecked {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    static int Wrap(int i, int n) { int m = i % n; return m < 0 ? m + n : m; }

    static float Value(float u, float v, int period, int seed) {
        float x = u * period, y = v * period;
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float fx = x - ix, fy = y - iy;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash(Wrap(ix, period), Wrap(iy, period), seed), b = Hash(Wrap(ix + 1, period), Wrap(iy, period), seed);
        float c = Hash(Wrap(ix, period), Wrap(iy + 1, period), seed), d = Hash(Wrap(ix + 1, period), Wrap(iy + 1, period), seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Fbm(float u, float v, int period, int octaves, int seed) {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int o = 0; o < octaves; o++) {
            sum += amp * Value(u, v, period, seed + o * 17);
            norm += amp;
            amp *= 0.5f;
            period *= 2;
        }
        return sum / norm;
    }

    public static Texture2D Noise() {
        if (noise != null) {
            return noise;
        }
        const int size = 256;
        var height = new float[size * size];
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                height[y * size + x] = Fbm(u, v, 16, 4, 5);
            }
        }
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float l = height[y * size + Wrap(x - 1, size)], r = height[y * size + Wrap(x + 1, size)];
                float d = height[Wrap(y - 1, size) * size + x], up = height[Wrap(y + 1, size) * size + x];
                float nx = (l - r) * 6f, ny = (d - up) * 6f;
                float macro = Mathf.Clamp01((Fbm(u, v, 3, 4, 41) - 0.5f) * 1.7f + 0.5f);
                float macro2 = Mathf.Clamp01((Fbm(u, v, 4, 3, 77) - 0.5f) * 1.7f + 0.5f);
                px[y * size + x] = new Color32((byte)(Mathf.Clamp01(nx * 0.5f + 0.5f) * 255f), (byte)(Mathf.Clamp01(ny * 0.5f + 0.5f) * 255f), (byte)(macro * 255f), (byte)(macro2 * 255f));
            }
        }
        noise = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
        noise.name = "Pbr_Noise";
        noise.wrapMode = TextureWrapMode.Repeat;
        noise.filterMode = FilterMode.Trilinear;
        noise.SetPixels32(px);
        noise.Apply(true, true);
        return noise;
    }

    // A splat mask from a function of the world position: R, G are layers 1 and 2.
    public static Texture2D Mask(string name, int size, System.Func<int, int, Color32> pixel) {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        tex.name = name;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                px[y * size + x] = pixel(x, y);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);   // stays readable: the playtest counts the path in it
        masks.Add(tex);
        return tex;
    }

    // The three-layer ground material. dir is HollowDir or HubDir, sets the three layer names (layer 0 first).
    public static Material Splat(string dir, string[] sets, Texture2D mask, Vector4 rect, float tile, float macro, float vcol, Color tint) {
        Shader sh = Shader.Find("Knoll/PbrSplat");
        if (sh == null || sets.Length < 3) {
            return null;
        }
        var m = new Material(sh);
        m.name = "PbrSplat_" + sets[0] + "_" + sets[1] + "_" + sets[2];
        for (int i = 0; i < 3; i++) {
            Texture2D a = Load(dir, sets[i], "_a"), n = Load(dir, sets[i], "_n");
            if (a == null || n == null) {
                Object.Destroy(m);
                return null;
            }
            m.SetTexture("_A" + i, a);
            m.SetTexture("_N" + i, n);
        }
        m.SetTexture("_Mask", mask);
        m.SetTexture("_Noise", Noise());
        m.SetVector("_MaskRect", rect);
        m.SetVector("_Tile", new Vector4(tile, tile, tile, tile));
        m.SetFloat("_Macro", macro);
        m.SetFloat("_VCol", vcol);
        m.SetFloat("_Lite", UiFit.Touch ? 1f : 0f);
        m.SetColor("_Tint", tint);
        SplatMaterials++;
        Last = m.name + " (tile " + tile.ToString("0.0") + " m, macro " + macro.ToString("0.00") + ", " + (UiFit.Touch ? "phone tier" : "full") + ")";
        return m;
    }

    // ---- the hub's sets inside the kit's own materials ---------------------------------------------------------------------------------

    // Which hub set stands in for a kit set in the office palette (walls brick, boards, rusted metal, carpet), or null to keep the kit's.
    public static string HubSetFor(string kitSet) {
        switch (kitSet) {
            case "plaster": return "brick";
            case "wood": return "wood";
            case "metal": case "iron": return "rust";
            case "cloth": return "carpet";
        }
        return null;
    }

    public static float HubSmoothness(string hubSet) {
        switch (hubSet) {
            case "rust": return 0.32f;
            case "wood": return 0.22f;
            case "brick": return 0.1f;
            case "carpet": return 0.03f;
        }
        return 0.15f;
    }

    // Albedo and normal of a hub set if fetched, else false (the caller keeps the code-made texture).
    public static bool Hub(string kitSet, out Texture2D albedo, out Texture2D normal, out float smooth) {
        albedo = null;
        normal = null;
        smooth = 0.12f;
        string set = HubSetFor(kitSet);
        if (set == null) {
            return false;
        }
        albedo = Load(HubDir, set, "_a");
        normal = Load(HubDir, set, "_n");
        if (albedo == null || normal == null) {
            albedo = null;
            normal = null;
            return false;
        }
        smooth = HubSmoothness(set);
        return true;
    }

    // The office floor: boards down the middle, linoleum along both walls, carpet in front of the six doors. Null when the hub set
    // is not here (the caller keeps its plain kit floor).
    public static Material HubFloor(float hw, float z0, float z1) {
        if (!Have(HubDir, new[] { "wood", "linoleum", "carpet" })) {
            return null;
        }
        float w = hw * 2f, d = z1 - z0;
        Texture2D mask = Mask("Office Floor Mask", 128, (px, py) => {
            float x = -hw + (px + 0.5f) / 128f * w, z = z0 + (py + 0.5f) / 128f * d;
            float lino = Mathf.SmoothStep(0f, 1f, (Mathf.Abs(x) - hw * 0.55f) / (hw * 0.3f));
            float carpet = Mathf.SmoothStep(0f, 1f, (z - 9.2f) / 2.4f);
            return new Color32((byte)(lino * 255f), (byte)(carpet * 255f), 0, 255);
        });
        return Splat(HubDir, new[] { "wood", "linoleum", "carpet" }, mask, new Vector4(-hw, z0, 1f / w, 1f / d), 3f, 0.45f, 0f, new Color(1f, 0.93f, 0.82f));
    }
}
