using System.Collections.Generic;
using UnityEngine;
using TexMap = System.Collections.Generic.Dictionary<string, UnityEngine.Texture2D>;

// The Model Kit's procedural textures and materials (Docs/ModelKit.md section 3). Everything is generated in code and
// tileable (periodic noise, wrapped Voronoi). Each set has an albedo and a normal map made from its height field; palettes
// differ by tint, so twelve texture sets serve every material. No GPU, no imported art.
public static class KitTextures {
    public static readonly string[] Sets = { "brick", "plaster", "wood", "metal", "iron", "brass", "stone", "concrete", "slate", "cloth", "paper", "glass", "grass" };

    static readonly TexMap albedo = new TexMap();
    static readonly TexMap normals = new TexMap();
    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

    public static int SizeOf(string set) { return set == "glass" ? 64 : (set == "brass" || set == "cloth" || set == "paper") ? 128 : 256; }

    public static Texture2D Albedo(string set) { Ensure(set); return albedo[set]; }
    public static Texture2D Normal(string set) { Ensure(set); return normals[set]; }

    public static IEnumerable<Texture2D> All() {
        foreach (string s in Sets) {
            Ensure(s);
            yield return albedo[s];
            yield return normals[s];
        }
    }

    // RGBA32 with a full mip chain: 4 bytes a pixel plus a third.
    public static long Bytes(Texture2D t) {
        return t == null ? 0L : (long)(t.width * t.height * 4L * 4L / 3L);
    }

    // ---- noise ------------------------------------------------------------------------------------------------------

    static float Hash(int x, int y, int seed) {
        unchecked {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }

    static int Wrap(int i, int n) { int m = i % n; return m < 0 ? m + n : m; }

    // Value noise that tiles at (px, py) lattice cells over u, v in 0..1.
    static float Noise(float u, float v, int px, int py, int seed) {
        float x = u * px, y = v * py;
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float fx = x - ix, fy = y - iy;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash(Wrap(ix, px), Wrap(iy, py), seed), b = Hash(Wrap(ix + 1, px), Wrap(iy, py), seed);
        float c = Hash(Wrap(ix, px), Wrap(iy + 1, py), seed), d = Hash(Wrap(ix + 1, px), Wrap(iy + 1, py), seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Fbm(float u, float v, int period, int octaves, int seed) {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int o = 0; o < octaves; o++) {
            sum += amp * Noise(u, v, period, period, seed + o * 17);
            norm += amp;
            amp *= 0.5f;
            period *= 2;
        }
        return sum / norm;
    }

    // Wrapped Voronoi: distance to the nearest (f1) and second nearest (f2) feature point, in cell units; id of the nearest cell.
    static void Voronoi(float u, float v, int n, int seed, out float f1, out float f2, out float id) {
        float x = u * n, y = v * n;
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        f1 = 9f;
        f2 = 9f;
        id = 0f;
        for (int j = -1; j <= 1; j++) {
            for (int i = -1; i <= 1; i++) {
                int cx = ix + i, cy = iy + j;
                int wx = Wrap(cx, n), wy = Wrap(cy, n);
                float fx = cx + Hash(wx, wy, seed) * 0.8f + 0.1f;
                float fy = cy + Hash(wx, wy, seed + 5) * 0.8f + 0.1f;
                float d = Mathf.Sqrt((fx - x) * (fx - x) + (fy - y) * (fy - y));
                if (d < f1) {
                    f2 = f1;
                    f1 = d;
                    id = Hash(wx, wy, seed + 9);
                } else if (d < f2) {
                    f2 = d;
                }
            }
        }
    }

    static Color Mix(Color a, Color b, float t) { return Color.Lerp(a, b, Mathf.Clamp01(t)); }
    static float Step(float edge, float x, float soft) { return Mathf.Clamp01((x - edge) / Mathf.Max(soft, 1e-4f)); }

    // ---- sets: colour and height at (u, v) --------------------------------------------------------------------------

    static void Sample(string set, float u, float v, out Color col, out float h) {
        float grime = Fbm(u, v, 3, 4, 3);
        switch (set) {
            case "brick": {
                const float rows = 8f, per = 4f;
                float rv = v * rows;
                int row = Mathf.FloorToInt(rv);
                float uu = u * per + (row % 2 == 0 ? 0f : 0.5f);
                float cell = Mathf.Floor(uu);
                float fu = uu - cell, fv = rv - row;
                float mortar = (fv < 0.12f || fu < 0.07f) ? 1f : 0f;
                float tone = 0.75f + 0.5f * Hash(Wrap((int)cell, 4), Wrap(row, 8), 1);
                Color brick = new Color(0.55f, 0.29f, 0.22f) * tone * (0.85f + 0.3f * Noise(u, v, 32, 32, 4));
                Color mort = new Color(0.62f, 0.6f, 0.54f) * (0.8f + 0.2f * Noise(u, v, 48, 48, 5));
                col = Mix(brick, mort, mortar);
                col = Mix(col, col * 0.55f, Step(0.55f, grime, 0.3f) * 0.7f);
                h = mortar > 0.5f ? 0.15f : 0.65f + 0.25f * Noise(u, v, 24, 24, 6) - Mathf.Max(0f, 0.12f - fv) ;
                col.a = 1f;
                break;
            }
            case "plaster": {
                float stain = Fbm(u, v, 2, 5, 11);
                float f1, f2, id;
                Voronoi(u, v, 3, 12, out f1, out f2, out id);
                float crack = 1f - Step(0.0f, (f2 - f1) - 0.025f, 0.02f);
                col = new Color(0.84f, 0.81f, 0.74f) * (0.82f + 0.3f * stain) * (0.94f + 0.06f * Noise(u, v, 96, 96, 13));
                col = Mix(col, col * 0.45f, crack * 0.5f);
                col = Mix(col, new Color(0.55f, 0.5f, 0.4f), Step(0.62f, stain, 0.25f) * 0.35f);
                h = 0.55f + 0.25f * Noise(u, v, 64, 64, 14) - crack * 0.3f;
                col.a = 1f;
                break;
            }
            case "wood": {
                float planks = 4f;
                float pu = u * planks;
                int pl = Mathf.FloorToInt(pu);
                float fu = pu - pl;
                float seg = Mathf.Floor(v * 2f + Hash(Wrap(pl, 4), 0, 21) * 2f);
                float grain = Noise(u + Hash(Wrap(pl, 4), 3, 22), v, 24, 3, 23) * 0.6f + Noise(u, v, 96, 6, 24) * 0.4f;
                float ring = 0.5f + 0.5f * Mathf.Sin((grain * 9f + fu * 2f) * 6.2831f);
                float tone = 0.7f + 0.5f * Hash(Wrap(pl, 4), Wrap((int)seg, 4), 25);
                Color dark = new Color(0.30f, 0.18f, 0.10f), light = new Color(0.56f, 0.36f, 0.20f);
                col = Mix(dark, light, ring * 0.6f + grain * 0.4f) * tone;
                float gap = (fu < 0.03f || fu > 0.97f) ? 1f : 0f;
                float joint = Mathf.Abs(Mathf.Repeat(v * 2f + Hash(Wrap(pl, 4), 0, 21) * 2f, 1f) - 0.0f) < 0.02f ? 1f : 0f;
                col = Mix(col, col * 0.4f, Mathf.Max(gap, joint));
                h = 0.6f + 0.15f * ring - 0.45f * Mathf.Max(gap, joint);
                col.a = 1f;
                break;
            }
            case "metal": {
                float brush = Noise(u, v, 2, 160, 31);
                float scratch = Step(0.86f, Noise(u, v, 40, 3, 32), 0.05f) * 0.5f + Step(0.88f, Noise(u, v, 3, 52, 33), 0.05f) * 0.4f;
                float rivetX = Mathf.Abs(Mathf.Repeat(u * 4f, 1f) - 0.5f), rivetY = Mathf.Abs(Mathf.Repeat(v * 4f, 1f) - 0.5f);
                float rivet = rivetX < 0.4f && rivetY < 0.4f ? 0f : 1f;
                float rd = Mathf.Sqrt(Mathf.Pow(Mathf.Repeat(u * 4f, 1f) - 0.08f, 2f) + Mathf.Pow(Mathf.Repeat(v * 4f, 1f) - 0.08f, 2f));
                float dome = rd < 0.045f ? 1f : 0f;
                float rust = Step(0.58f, Fbm(u, v, 3, 5, 34), 0.18f);
                col = new Color(0.58f, 0.6f, 0.64f) * (0.8f + 0.3f * brush) * (1f - scratch * 0.3f);
                col = Mix(col, new Color(0.46f, 0.25f, 0.12f) * (0.8f + 0.4f * Noise(u, v, 40, 40, 35)), rust * 0.75f);
                col = Mix(col, col * 1.25f, dome);
                h = 0.5f + 0.1f * brush + 0.3f * dome - 0.08f * scratch - 0.0f * rivet;
                col.a = 1f;
                break;
            }
            case "iron": {
                float f1, f2, id;
                Voronoi(u, v, 14, 41, out f1, out f2, out id);
                float pit = 1f - Step(0.12f, f1, 0.08f);
                float flake = Step(0.6f, Fbm(u, v, 4, 4, 42), 0.1f);
                col = new Color(0.27f, 0.27f, 0.3f) * (0.8f + 0.3f * Noise(u, v, 64, 64, 43));
                col = Mix(col, new Color(0.46f, 0.43f, 0.4f), flake * 0.45f);
                col = Mix(col, col * 0.4f, pit * 0.7f * id);
                h = 0.6f - 0.3f * pit * id + 0.1f * flake;
                col.a = 1f;
                break;
            }
            case "brass": {
                float streak = Noise(u, v, 3, 48, 51);
                float tarnish = Step(0.55f, Fbm(u, v, 3, 4, 52), 0.2f);
                col = new Color(0.8f, 0.6f, 0.25f) * (0.85f + 0.25f * streak);
                col = Mix(col, new Color(0.32f, 0.3f, 0.16f), tarnish * 0.6f);
                h = 0.5f + 0.15f * streak;
                col.a = 1f;
                break;
            }
            case "stone": {
                float f1, f2, id;
                Voronoi(u, v, 4, 61, out f1, out f2, out id);
                float edge = 1f - Step(0.0f, (f2 - f1) - 0.05f, 0.05f);
                float surface = Fbm(u, v, 8, 4, 62);
                float lichen = Step(0.66f, Fbm(u, v, 3, 4, 63), 0.15f);
                col = new Color(0.52f, 0.52f, 0.54f) * (0.72f + 0.45f * id) * (0.85f + 0.3f * surface);
                col = Mix(col, new Color(0.35f, 0.45f, 0.28f), lichen * 0.4f);
                col = Mix(col, col * 0.4f, edge * 0.8f);
                h = 0.65f + 0.2f * surface - 0.5f * edge + 0.1f * id;
                col.a = 1f;
                break;
            }
            case "concrete": {
                float pitCell = Noise(u, v, 80, 80, 71);
                float pit = Step(0.86f, pitCell, 0.05f);
                float stain = Fbm(u, v, 2, 5, 72);
                col = new Color(0.6f, 0.6f, 0.58f) * (0.8f + 0.35f * Fbm(u, v, 12, 4, 73)) * (0.85f + 0.2f * stain);
                col = Mix(col, col * 0.4f, pit * 0.8f);
                h = 0.6f + 0.2f * Noise(u, v, 48, 48, 74) - 0.35f * pit;
                col.a = 1f;
                break;
            }
            case "slate": {
                const float rows = 6f, per = 4f;
                float rv = v * rows;
                int row = Mathf.FloorToInt(rv);
                float uu = u * per + (row % 2 == 0 ? 0f : 0.5f);
                float cell = Mathf.Floor(uu);
                float fu = uu - cell, fv = rv - row;
                float edgeU = (fu < 0.04f) ? 1f : 0f;
                float tone = 0.7f + 0.5f * Hash(Wrap((int)cell, 4), Wrap(row, 6), 81);
                col = new Color(0.30f, 0.33f, 0.40f) * tone * (0.85f + 0.3f * Noise(u, v, 40, 40, 82));
                col = Mix(col, col * 0.4f, Mathf.Max(edgeU, Step(0.82f, fv, 0.15f) * 0.8f));
                h = 0.3f + 0.6f * fv * (1f - edgeU * 0.5f);
                col.a = 1f;
                break;
            }
            case "cloth": {
                float wu = 0.5f + 0.5f * Mathf.Sin(u * 6.2831f * 24f), wv = 0.5f + 0.5f * Mathf.Sin(v * 6.2831f * 24f);
                bool over = (Mathf.FloorToInt(u * 24f) + Mathf.FloorToInt(v * 24f)) % 2 == 0;
                float thread = over ? wu : wv;
                float fold = Fbm(u, v, 2, 4, 91);
                col = new Color(0.72f, 0.72f, 0.72f) * (0.7f + 0.3f * thread) * (0.8f + 0.35f * fold);
                h = 0.4f + 0.5f * thread;
                col.a = 1f;
                break;
            }
            case "paper": {
                float fibre = Noise(u, v, 140, 12, 101) * 0.5f + Noise(u, v, 20, 100, 102) * 0.5f;
                float line = Mathf.Abs(Mathf.Repeat(v * 8f, 1f) - 0.9f) < 0.025f ? 1f : 0f;
                float margin = Mathf.Abs(u - 0.14f) < 0.008f ? 1f : 0f;
                col = new Color(0.93f, 0.9f, 0.8f) * (0.88f + 0.18f * fibre) * (0.9f + 0.15f * Fbm(u, v, 3, 3, 103));
                col = Mix(col, new Color(0.45f, 0.55f, 0.75f), line * 0.45f);
                col = Mix(col, new Color(0.7f, 0.3f, 0.3f), margin * 0.5f);
                h = 0.5f + 0.1f * fibre;
                col.a = 1f;
                break;
            }
            case "grass": {
                float blade = Noise(u, v, 120, 120, 121);
                float tuft = Fbm(u, v, 6, 4, 122);
                float patch = Fbm(u, v, 2, 4, 123);
                col = Mix(new Color(0.2f, 0.36f, 0.16f), new Color(0.42f, 0.55f, 0.24f), tuft) * (0.78f + 0.34f * blade) * (0.86f + 0.26f * patch);
                col = Mix(col, new Color(0.5f, 0.46f, 0.26f), Step(0.7f, patch, 0.2f) * 0.35f);
                h = 0.4f + 0.45f * blade + 0.15f * tuft;
                col.a = 1f;
                break;
            }
            default: {   // glass
                float streak = Mathf.Abs(Mathf.Repeat(u + v, 1f) - 0.5f);
                float shine = 1f - Step(0.0f, streak - 0.05f, 0.08f);
                col = new Color(0.1f, 0.16f, 0.22f) * (0.8f + 0.5f * Fbm(u, v, 3, 3, 111));
                col = Mix(col, new Color(0.6f, 0.75f, 0.85f), shine * 0.35f);
                h = 0.5f;
                col.a = 1f;
                break;
            }
        }
    }

    static float NormalStrength(string set) {
        switch (set) {
            case "brick": return 5f;
            case "stone": return 4f;
            case "slate": return 4f;
            case "wood": return 3f;
            case "cloth": return 2.5f;
            case "glass": return 0.2f;
            default: return 2.5f;
        }
    }

    static void Ensure(string set) {
        if (albedo.ContainsKey(set)) {
            return;
        }
        int size = SizeOf(set);
        var colors = new Color32[size * size];
        var height = new float[size * size];
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                Color c;
                float h;
                Sample(set, (x + 0.5f) / size, (y + 0.5f) / size, out c, out h);
                colors[y * size + x] = c;
                height[y * size + x] = h;
            }
        }
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
        tex.name = "Kit_" + set + "_albedo";
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 4;
        tex.SetPixels32(colors);
        tex.Apply(true, false);
        albedo[set] = tex;

        float strength = NormalStrength(set);
        var nc = new Color32[size * size];
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float l = height[y * size + Wrap(x - 1, size)], r = height[y * size + Wrap(x + 1, size)];
                float d = height[Wrap(y - 1, size) * size + x], up = height[Wrap(y + 1, size) * size + x];
                Vector3 n = new Vector3((l - r) * strength, (d - up) * strength, 1f).normalized;
                nc[y * size + x] = new Color32((byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f), 255);
            }
        }
        var ntex = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
        ntex.name = "Kit_" + set + "_normal";
        ntex.wrapMode = TextureWrapMode.Repeat;
        ntex.filterMode = FilterMode.Trilinear;
        ntex.anisoLevel = 4;
        ntex.SetPixels32(nc);
        ntex.Apply(true, false);
        normals[set] = ntex;
    }

    // ---- materials --------------------------------------------------------------------------------------------------

    // The tint a palette gives a texture set: palettes look different through colour as well as shape and texture choice.
    public static Color Tint(string palette, string set) {
        Color wall, wood, metal, cloth;
        switch (palette) {
            case "street": wall = new Color(0.98f, 0.92f, 0.86f); wood = new Color(0.95f, 0.82f, 0.7f); metal = new Color(0.8f, 0.86f, 0.95f); cloth = new Color(0.8f, 0.3f, 0.26f); break;
            case "office": wall = new Color(1f, 0.93f, 0.8f); wood = new Color(1f, 0.82f, 0.62f); metal = new Color(0.7f, 0.8f, 0.72f); cloth = new Color(0.66f, 0.26f, 0.24f); break;
            case "yard": wall = new Color(0.62f, 0.7f, 0.98f); wood = new Color(0.66f, 0.7f, 0.86f); metal = new Color(0.56f, 0.62f, 0.85f); cloth = new Color(0.55f, 0.56f, 0.66f); break;
            case "gate": wall = new Color(0.86f, 0.93f, 1.05f); wood = new Color(0.8f, 0.84f, 0.95f); metal = new Color(0.5f, 0.64f, 0.92f); cloth = new Color(0.86f, 0.86f, 0.94f); break;
            case "floor": wall = new Color(1.05f, 0.8f, 0.58f); wood = new Color(0.95f, 0.68f, 0.42f); metal = new Color(1.1f, 0.78f, 0.52f); cloth = new Color(0.62f, 0.42f, 0.26f); break;
            case "walk": wall = new Color(0.64f, 0.7f, 0.88f); wood = new Color(0.7f, 0.7f, 0.82f); metal = new Color(0.58f, 0.58f, 0.86f); cloth = new Color(0.6f, 0.5f, 0.85f); break;
            case "reading": wall = new Color(1.05f, 0.9f, 0.66f); wood = new Color(0.72f, 0.5f, 0.34f); metal = new Color(0.95f, 0.82f, 0.55f); cloth = new Color(0.55f, 0.16f, 0.15f); break;
            case "filed": wall = new Color(1.08f, 1.08f, 1.12f); wood = new Color(1f, 0.96f, 0.88f); metal = new Color(1.05f, 0.92f, 0.62f); cloth = new Color(0.96f, 0.96f, 1.05f); break;
            default: wall = new Color(0.78f, 0.78f, 0.9f); wood = new Color(0.8f, 0.72f, 0.7f); metal = new Color(0.72f, 0.72f, 0.82f); cloth = new Color(0.7f, 0.5f, 0.5f); break;
        }
        switch (set) {
            case "wood": return wood;
            case "metal": case "iron": return metal;
            case "cloth": return cloth;
            case "brass": return new Color(1f, 0.95f, 0.88f);
            case "glass": case "lamp": return new Color(0.9f, 1f, 1f);
            default: return wall;
        }
    }

    public static Color Lamp(string palette) {
        switch (palette) {
            case "floor": return new Color(1f, 0.62f, 0.18f);
            case "walk": return new Color(0.62f, 0.38f, 1f);
            case "yard": case "gate": return new Color(0.82f, 0.9f, 1f);
            case "filed": return new Color(1f, 0.9f, 0.55f);
            default: return new Color(1f, 0.78f, 0.4f);
        }
    }

    // A shared material for a texture set in a palette. "lamp" is the glass set glowing the palette's lamp colour.
    public static Material Get(string palette, string set) {
        string key = palette + "|" + set;
        Material m;
        if (materials.TryGetValue(key, out m) && m != null) {
            return m;
        }
        bool lamp = set == "lamp";
        string tex = lamp ? "glass" : set;
        Shader shader = SafeShader.Find("Knoll/KitLit");
        m = new Material(shader);
        m.name = "Kit_" + palette + "_" + set;
        m.mainTexture = Albedo(tex);
        m.SetTexture("_BumpMap", Normal(tex));
        m.SetColor("_Color", Tint(palette, set));
        float smooth = 0.12f, metal = 0f;
        switch (tex) {
            case "metal": smooth = 0.4f; metal = 0.3f; break;
            case "iron": smooth = 0.3f; metal = 0.3f; break;
            case "brass": smooth = 0.5f; metal = 0.4f; break;
            case "glass": smooth = 0.85f; break;
            case "stone": smooth = 0.08f; break;
            case "cloth": smooth = 0.02f; break;
        }
        m.SetFloat("_Glossiness", smooth);
        m.SetFloat("_Metallic", metal);
        if (lamp) {
            m.SetColor("_EmissionColor", Lamp(palette));
            m.SetColor("_Color", Lamp(palette) * 0.8f);
        }
        m.enableInstancing = true;
        materials[key] = m;
        return m;
    }
}
