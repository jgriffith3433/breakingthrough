using System.Collections.Generic;
using UnityEngine;

// Every trait a bot carries has its own visible marker, so a player knows what it will do before it acts (Hades' elite marker).
// All of it is drawn from code (no new art or audio, Sprites/Default quads with soft edges, no sparks):
//   - a badge over the head per trait, each its own shape and colour (a Sprinter's double chevron, a Hopper's up arrow, a Bruiser's
//     frame, a Juker's diamond, a Jump-Jet's triangle over a flame, a Splitter's cracked disc, a Blinker's eye, a Heavy's hexagon,
//     a Charger's down arrowhead, a Swarmer's three dots, a Juggernaut's star). It copies the camera's rotation, bobs, and on a
//     phone is never under BadgeMinCssPx across, measured against the camera, so it reads at that distance.
//   - a Sprinter's speed streaks trailing behind it, long when it runs;
//   - a Splitter's crack line across its body;
//   - a Blinker's body flickers for BlinkWarn seconds before it jumps (Enemy.BlinkSoon).
// Slice names the first bot with a trait that she has not met in a run (SliceVoice.OnNewTrait, her existing new-bot lines).
// Metrics for the playtest: Seen (traits whose badge was drawn), Flickers (a Blinker flicker started), StreakFrames, CrackFrames,
// BadgeMinPx. Docs/Story.md, "Reading a bot".
public class TraitMark : MonoBehaviour {
    public const float BlinkWarn = 0.7f;
    public const float BadgeMinCssPx = 34f;
    const int GlyphCount = 12;

    public static readonly HashSet<BotTrait> Seen = new HashSet<BotTrait>();
    public static int Flickers, StreakFrames, CrackFrames, BadgeFrames;
    public static float BadgeMinPx = float.MaxValue;

    static Mesh quad;
    static readonly Texture2D[] glyphs = new Texture2D[GlyphCount];
    static readonly Material[] mats = new Material[GlyphCount];
    static Texture2D streakTex, crackTex;
    static Material streakMat, crackMat;
    static readonly int ColorId = Shader.PropertyToID("_Color");

    Enemy enemy;
    readonly List<Transform> badges = new List<Transform>();
    readonly List<BotTrait> badgeTraits = new List<BotTrait>();
    readonly List<Transform> streaks = new List<Transform>();
    Transform crack;
    Renderer[] bodyParts;
    Vector3 lastPos;
    float speed;
    float height = 1.5f, heightAt = -9f, seenAt;
    bool flickering;
    int built;
    MaterialPropertyBlock block;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() {
        Seen.Clear();
        Flickers = StreakFrames = CrackFrames = BadgeFrames = 0;
        BadgeMinPx = float.MaxValue;
        quad = null;
        streakTex = crackTex = null;
        streakMat = crackMat = null;
        for (int i = 0; i < GlyphCount; i++) {
            glyphs[i] = null;
            mats[i] = null;
        }
    }

    public static Color ColourOf(BotTrait t) {
        switch (t) {
            case BotTrait.Sprinter: return new Color(1f, 0.92f, 0.2f);
            case BotTrait.Hopper: return new Color(0.5f, 1f, 0.35f);
            case BotTrait.Bruiser: return new Color(1f, 0.3f, 0.25f);
            case BotTrait.Juker: return new Color(0.25f, 0.95f, 0.85f);
            case BotTrait.JumpJets: return new Color(0.4f, 0.7f, 1f);
            case BotTrait.Splitter: return new Color(1f, 0.6f, 0.15f);
            case BotTrait.Blinker: return new Color(0.85f, 0.4f, 1f);
            case BotTrait.Heavy: return new Color(0.8f, 0.15f, 0.2f);
            case BotTrait.Charger: return new Color(1f, 0.45f, 0.1f);
            case BotTrait.Swarmer: return new Color(1f, 0.85f, 0.45f);
            case BotTrait.Juggernaut: return new Color(0.6f, 0.35f, 0.85f);
        }
        return Color.white;
    }

    void Awake() {
        enemy = GetComponent<Enemy>();
        block = new MaterialPropertyBlock();
        lastPos = transform.position;
    }

    void OnDestroy() {
        StopFlicker();
        for (int i = 0; i < badges.Count; i++) {
            if (badges[i] != null) Destroy(badges[i].gameObject);
        }
        for (int i = 0; i < streaks.Count; i++) {
            if (streaks[i] != null) Destroy(streaks[i].gameObject);
        }
        if (crack != null) Destroy(crack.gameObject);
    }

    void LateUpdate() {
        Camera cam = Camera.main;
        if (enemy == null || cam == null) {
            return;
        }
        bool hidden = !enemy.Alive || enemy.Dormant || enemy.Scripted || LevelUpScreen.IsOpen || Cutscene.IsOpen;
        if (built != enemy.TraitMask) {
            Build();
        }
        if (hidden) {
            Show(false);
            StopFlicker();
            return;
        }
        Vector3 p = transform.position;
        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        Vector3 d = p - lastPos;
        d.y = 0f;
        speed = Mathf.Lerp(speed, d.magnitude / dt, 0.2f);
        if (d.sqrMagnitude > 0.0004f) {
            heading = Vector3.Lerp(heading, d.normalized, 0.25f);
        }
        lastPos = p;
        if (Time.time - heightAt > 0.5f) {
            heightAt = Time.time;
            RefreshHeight();
        }
        Show(true);
        PlaceBadges(cam, p);
        PlaceStreaks(cam, p);
        PlaceCrack(cam, p);
        TickFlicker();
        NameNew();
    }

    Vector3 heading = Vector3.forward;

    void RefreshHeight() {
        float top = transform.position.y + 1f;
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) {
            if (r != null && (r.enabled || flickering)) {
                top = Mathf.Max(top, r.bounds.max.y);
            }
        }
        height = Mathf.Max(0.8f, top - transform.position.y);
    }

    void Show(bool on) {
        for (int i = 0; i < badges.Count; i++) if (badges[i] != null) badges[i].gameObject.SetActive(on);
        if (!on) {
            for (int i = 0; i < streaks.Count; i++) if (streaks[i] != null) streaks[i].gameObject.SetActive(false);
            if (crack != null) crack.gameObject.SetActive(false);
        }
    }

    // ---- building

    void Build() {
        built = enemy.TraitMask;
        for (int i = 0; i < badges.Count; i++) if (badges[i] != null) Destroy(badges[i].gameObject);
        badges.Clear();
        badgeTraits.Clear();
        EnsureArt();
        for (int t = 1; t < GlyphCount; t++) {
            if (!enemy.HasTrait((BotTrait)t)) continue;
            badges.Add(MakeQuad("Trait Badge " + (BotTrait)t, mats[t]));
            badgeTraits.Add((BotTrait)t);
        }
        if (enemy.HasTrait(BotTrait.Sprinter) && streaks.Count == 0) {
            for (int i = 0; i < 3; i++) streaks.Add(MakeQuad("Sprinter Streak", streakMat));
        }
        if (enemy.HasTrait(BotTrait.Splitter) && crack == null) {
            crack = MakeQuad("Splitter Crack", crackMat);
        }
    }

    static Transform MakeQuad(string name, Material m) {
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        var rd = go.AddComponent<MeshRenderer>();
        rd.sharedMaterial = m;
        rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rd.receiveShadows = false;
        return go.transform;
    }

    // ---- placing

    void PlaceBadges(Camera cam, Vector3 p) {
        int n = badges.Count;
        float scale = Mathf.Max(0.8f, enemy.BodyScale);
        float baseSize = 0.5f + 0.06f * scale;
        float dist = Mathf.Max(1f, Vector3.Distance(cam.transform.position, p));
        float pxPerUnit = Screen.height / (2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
        bool touch = UiFit.Touch;
        float css = touch ? UiFit.PixelRatio : 1f;
        float minPx = BadgeMinCssPx * css;
        float size = baseSize;
        if (size * pxPerUnit < minPx) {
            size = Mathf.Min(minPx / Mathf.Max(0.01f, pxPerUnit), baseSize * 3.5f);
        }
        BadgeMinPx = Mathf.Min(BadgeMinPx, size * pxPerUnit / css);
        BadgeFrames++;
        Vector3 right = cam.transform.right;
        float bob = Mathf.Sin(Time.time * 3f + p.x) * 0.06f;
        float lift = height + size * 0.9f + 0.25f;
        for (int i = 0; i < n; i++) {
            Transform b = badges[i];
            if (b == null) continue;
            float off = (i - (n - 1) * 0.5f) * size * 1.05f;
            b.position = p + Vector3.up * (lift + bob) + right * off;
            b.rotation = cam.transform.rotation;
            float pulse = 1f;
            if (badgeTraits[i] == BotTrait.Blinker && enemy.BlinkSoon) {
                pulse = 1f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.time * 40f));
            }
            b.localScale = new Vector3(size * pulse, size * pulse, 1f);
            Seen.Add(badgeTraits[i]);
        }
    }

    void PlaceStreaks(Camera cam, Vector3 p) {
        if (streaks.Count == 0) {
            return;
        }
        float k = Mathf.Clamp01((speed - 0.8f) / 2.2f);
        bool show = k > 0.02f;
        float scale = Mathf.Max(0.8f, enemy.BodyScale);
        float len = (1.1f + 0.9f * k) * scale * 0.9f;
        Vector3 back = -heading;
        Vector3 right = cam.transform.right;
        for (int i = 0; i < streaks.Count; i++) {
            Transform s = streaks[i];
            if (s == null) continue;
            s.gameObject.SetActive(show);
            if (!show) continue;
            float h = height * (0.25f + 0.28f * i);
            float side = (i - 1) * 0.28f * scale;
            Vector3 start = p + Vector3.up * h + right * side + back * (0.35f * scale + len * 0.5f);
            s.position = start;
            Vector3 fwd = cam.transform.forward;
            Vector3 up = back;
            if (Mathf.Abs(Vector3.Dot(up, fwd)) > 0.97f) up = Vector3.up;
            s.rotation = Quaternion.LookRotation(fwd, up);
            s.localScale = new Vector3(0.11f * scale + 0.04f, len, 1f);
            block.SetColor(ColorId, new Color(1f, 0.95f, 0.45f, 0.85f * k));
            s.GetComponent<Renderer>().SetPropertyBlock(block);
        }
        if (show) StreakFrames++;
    }

    void PlaceCrack(Camera cam, Vector3 p) {
        if (crack == null) {
            return;
        }
        float scale = Mathf.Max(0.8f, enemy.BodyScale);
        crack.gameObject.SetActive(true);
        crack.position = p + Vector3.up * (height * 0.5f) - cam.transform.forward * (0.45f * scale);
        crack.rotation = cam.transform.rotation;
        float s = height * 0.95f;
        crack.localScale = new Vector3(s, s, 1f);
        float glow = 0.75f + 0.25f * Mathf.Sin(Time.time * 5f + p.z);
        block.SetColor(ColorId, new Color(1f, 0.75f * glow + 0.1f, 0.2f, 1f));
        crack.GetComponent<Renderer>().SetPropertyBlock(block);
        CrackFrames++;
    }

    // ---- the Blinker's flicker before it jumps

    void TickFlicker() {
        if (!enemy.BlinkSoon) {
            StopFlicker();
            return;
        }
        if (!flickering) {
            flickering = true;
            Flickers++;
            var list = new List<Renderer>();
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) {
                if (r != null && r.enabled && r.gameObject.name != "Crest") list.Add(r);
            }
            bodyParts = list.ToArray();
        }
        bool on = Mathf.Repeat(Time.time * 18f, 2f) < 1f;
        for (int i = 0; i < bodyParts.Length; i++) {
            if (bodyParts[i] != null) bodyParts[i].enabled = on;
        }
    }

    void StopFlicker() {
        if (!flickering) {
            return;
        }
        flickering = false;
        if (bodyParts != null) {
            for (int i = 0; i < bodyParts.Length; i++) {
                if (bodyParts[i] != null) bodyParts[i].enabled = true;
            }
        }
        bodyParts = null;
    }

    // ---- Slice names the first of each trait in a run (retried until her floor is free)

    void NameNew() {
        if (Time.time - seenAt < 1f) {
            return;
        }
        seenAt = Time.time;
        Musician m = Musician.Instance;
        if (m == null || Vector3.Distance(transform.position, m.transform.position) > 40f) {
            return;
        }
        for (int i = 0; i < badgeTraits.Count; i++) {
            if (SliceVoice.OnNewTrait(badgeTraits[i])) {
                return;
            }
        }
    }

    // ---- art, all drawn in code

    static void EnsureArt() {
        if (quad == null) {
            quad = new Mesh { name = "Trait Quad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        }
        for (int t = 1; t < GlyphCount; t++) {
            if (mats[t] != null) continue;
            glyphs[t] = DrawGlyph((BotTrait)t);
            mats[t] = new Material(SafeShader.Find("Sprites/Default")) { mainTexture = glyphs[t], renderQueue = 3000 };
            mats[t].color = Color.white;
        }
        if (streakMat == null) {
            streakTex = DrawStreak();
            streakMat = new Material(SafeShader.Find("Sprites/Default")) { mainTexture = streakTex, renderQueue = 2995 };
        }
        if (crackMat == null) {
            crackTex = DrawCrack();
            crackMat = new Material(SafeShader.Find("Sprites/Default")) { mainTexture = crackTex, renderQueue = 2996 };
        }
    }

    static bool Inside(BotTrait t, float x, float y) {
        float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
        switch (t) {
            case BotTrait.Sprinter:
                return ay < 0.75f && (Mathf.Abs(x - (0.6f - 0.9f * ay)) < 0.16f || Mathf.Abs(x - (-0.1f - 0.9f * ay)) < 0.16f);
            case BotTrait.Hopper:
                return (ax < 0.2f && y > -0.8f && y < 0.1f) || (y >= 0.1f && y < 0.85f && ax < 0.8f * (0.85f - y) / 0.75f);
            case BotTrait.Bruiser:
                return ax < 0.75f && ay < 0.75f && (ax > 0.4f || ay > 0.4f);
            case BotTrait.Juker:
                return ax + ay < 0.85f;
            case BotTrait.JumpJets:
                return (y > -0.15f && y < 0.85f && ax < 0.7f * (0.85f - y)) || (x * x + (y + 0.55f) * (y + 0.55f) < 0.07f);
            case BotTrait.Splitter:
                return x * x + y * y < 0.64f && Mathf.Abs(x - 0.14f * Mathf.Sin(y * 9f)) > 0.08f;
            case BotTrait.Blinker: {
                float r2 = x * x + y * y;
                return (r2 < 0.64f && r2 > 0.3f) || r2 < 0.05f;
            }
            case BotTrait.Heavy:
                return ay < 0.75f && ax * 0.866f + ay * 0.5f < 0.75f;
            case BotTrait.Charger:
                return y > -0.8f && y < 0.8f && ax < 0.9f * (y + 0.8f) / 1.6f;
            case BotTrait.Swarmer:
                return Dot(x, y, -0.5f, -0.3f) || Dot(x, y, 0.5f, -0.3f) || Dot(x, y, 0f, 0.45f);
            case BotTrait.Juggernaut:
                return Mathf.Pow(ax, 0.8f) + Mathf.Pow(ay, 0.8f) < 1f;
        }
        return false;
    }

    static bool Dot(float x, float y, float cx, float cy) {
        return (x - cx) * (x - cx) + (y - cy) * (y - cy) < 0.09f;
    }

    // A glyph in its colour with a dark rim, so it reads against the grass, the night and the bots.
    static Texture2D DrawGlyph(BotTrait t) {
        const int N = 64;
        var inside = new bool[N, N];
        for (int j = 0; j < N; j++) {
            for (int i = 0; i < N; i++) {
                inside[i, j] = Inside(t, (i + 0.5f) / N * 2f - 1f, (j + 0.5f) / N * 2f - 1f);
            }
        }
        Color c = ColourOf(t);
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Trait " + t };
        var px = new Color[N * N];
        for (int j = 0; j < N; j++) {
            for (int i = 0; i < N; i++) {
                if (inside[i, j]) {
                    px[j * N + i] = c;
                    continue;
                }
                bool near = false;
                for (int dj = -3; dj <= 3 && !near; dj++) {
                    for (int di = -3; di <= 3; di++) {
                        int a = i + di, b = j + dj;
                        if (a >= 0 && b >= 0 && a < N && b < N && inside[a, b] && di * di + dj * dj <= 10) {
                            near = true;
                            break;
                        }
                    }
                }
                px[j * N + i] = near ? new Color(0.05f, 0.04f, 0.07f, 0.92f) : Color.clear;
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return tex;
    }

    // A soft streak: bright at the head (top, the bot's end), fading to nothing at the tail.
    static Texture2D DrawStreak() {
        const int W = 16, H = 64;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Sprinter Streak" };
        var px = new Color[W * H];
        for (int j = 0; j < H; j++) {
            float along = 1f - j / (float)(H - 1);   // 1 at the top (the bot), 0 at the tail
            for (int i = 0; i < W; i++) {
                float across = 1f - Mathf.Abs((i + 0.5f) / W * 2f - 1f);
                float a = Mathf.Clamp01(across * 1.4f) * along * along;
                px[j * W + i] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return tex;
    }

    // A jagged crack from the top to the bottom of the quad: a bright core with a soft dark rim.
    static Texture2D DrawCrack() {
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Splitter Crack" };
        var pts = new[] { new Vector2(0.52f, 1f), new Vector2(0.42f, 0.82f), new Vector2(0.58f, 0.66f), new Vector2(0.4f, 0.5f), new Vector2(0.6f, 0.34f), new Vector2(0.44f, 0.18f), new Vector2(0.5f, 0f) };
        var px = new Color[N * N];
        for (int j = 0; j < N; j++) {
            for (int i = 0; i < N; i++) {
                var q = new Vector2((i + 0.5f) / N, (j + 0.5f) / N);
                float best = 9f;
                for (int s = 0; s + 1 < pts.Length; s++) {
                    best = Mathf.Min(best, SegDist(q, pts[s], pts[s + 1]));
                }
                Color col = Color.clear;
                if (best < 0.022f) {
                    col = Color.white;
                } else if (best < 0.045f) {
                    col = new Color(0.08f, 0.03f, 0.02f, 0.85f);
                }
                px[j * N + i] = col;
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return tex;
    }

    static float SegDist(Vector2 p, Vector2 a, Vector2 b) {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }
}
