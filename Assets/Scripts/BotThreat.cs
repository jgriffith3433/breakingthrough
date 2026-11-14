using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Every bot is easy to find and no blow comes from nowhere (the critic's "bots are small and sit in a distant lit circle").
//   - A flat ring on the ground under every live bot, in its kind's colour (a Yo Bot cyan, a Spider Bot teal, the Titan gold...).
//     It is a pooled quad drawn from code, no new art. It brightens, swells and pulses while the bot winds up a blow.
//     On a phone it is never under RingMinCssPx across, measured against the camera, so it reads at that distance.
//   - A small arrow at the screen edge for any bot outside the view that is within StrikeSeconds of striking Wallit or Slice,
//     at most MaxArrows (the nearest to striking first). On a phone it stays inside UiRects.SafeArea() and off the stick and pads.
// Metrics (PlaytestThreats): threats.offscreenMarked (distinct bots marked while off screen) and threats.unmarkedHits (a blow
// from a bot that was never on screen and never marked). AttackTell.NoteBlow calls Hit for every blow on Slice or Wallit.
public class BotThreat : MonoBehaviour {
    public const float StrikeSeconds = 2f;
    public const int MaxArrows = 4;
    public const float RingMinCssPx = 28f;
    const float RingY = 0.025f;
    const byte Seen = 1, Marked = 2;

    public static int OffscreenMarked, UnmarkedHits, Hits, ArrowFrames, ArrowPadOverlaps, ArrowOutsideSafe, WindFrames, PeakRings, PeakArrows;
    public static float RingMinPx = float.MaxValue, RingAlphaIdle, RingAlphaWind, ArrowMinPx = float.MaxValue;
    public static readonly List<string> Log = new List<string>();

    static BotThreat instance;
    static Mesh quad;
    static Texture2D ringTex, arrowTex;
    static readonly Dictionary<Enemy, byte> flags = new Dictionary<Enemy, byte>();
    static readonly int ColorId = Shader.PropertyToID("_Color");

    class Ring {
        public GameObject go;
        public MeshRenderer renderer;
        public float groundY, groundAt;
    }

    readonly Dictionary<Enemy, Ring> rings = new Dictionary<Enemy, Ring>();
    readonly Stack<Ring> spare = new Stack<Ring>();
    readonly List<Enemy> gone = new List<Enemy>();
    readonly Enemy[] pick = new Enemy[MaxArrows];
    readonly float[] pickEta = new float[MaxArrows];
    readonly List<Image> arrows = new List<Image>();
    MaterialPropertyBlock block;
    Material mat;
    Canvas canvas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() {
        instance = null;
        quad = null;
        ringTex = arrowTex = null;
        flags.Clear();
        Log.Clear();
        OffscreenMarked = UnmarkedHits = Hits = ArrowFrames = ArrowPadOverlaps = ArrowOutsideSafe = WindFrames = PeakRings = PeakArrows = 0;
        RingMinPx = ArrowMinPx = float.MaxValue;
        RingAlphaIdle = RingAlphaWind = 0f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() {
        if (instance == null) {
            instance = new GameObject("Bot Threat").AddComponent<BotThreat>();
            DontDestroyOnLoad(instance.gameObject);
        }
    }

    // A finished scene's bots are keys here: drop them so they can be collected (SceneMemory).
    public static void Prune() {
        List<Enemy> dead = null;
        foreach (Enemy e in flags.Keys) {
            if (e == null) {
                if (dead == null) dead = new List<Enemy>();
                dead.Add(e);
            }
        }
        if (dead != null) {
            foreach (Enemy e in dead) flags.Remove(e);
        }
    }

    // ---- the callers

    // A blow from a bot landed on Slice or Wallit (AttackTell.NoteBlow, before the damage).
    public static void Hit(Enemy by) {
        if (by == null) {
            return;
        }
        Hits++;
        byte f;
        flags.TryGetValue(by, out f);
        Camera cam = Camera.main;
        if (cam != null && InView(cam, by.transform.position + Vector3.up)) {
            f |= Seen;
            flags[by] = f;
        }
        if (f == 0) {
            UnmarkedHits++;
            if (Log.Count < 100) {
                Log.Add(Time.unscaledTime.ToString("0.00") + "|unmarked|" + by.Title);
            }
        }
    }

    // Seconds until this bot lands its next blow on Slice or Wallit, from where it is now (optimistic, so a mark comes early).
    public static float EtaOf(Enemy e) {
        if (e.Winding) {
            return e.WindRemaining;
        }
        float best = float.MaxValue;
        Musician slice = Musician.Instance;
        if (slice != null && slice.Alive && !slice.Away) {
            best = Mathf.Min(best, Distance(e, slice.transform.position));
        }
        if (PlayerGrowth.Instance != null) {
            best = Mathf.Min(best, Distance(e, PlayerGrowth.Instance.transform.position));
        }
        if (best == float.MaxValue) {
            return float.MaxValue;
        }
        float reach = 1.15f + e.BodyScale * 0.22f;
        float speed = Mathf.Max(e.walkSpeed, e.chaseSpeed) * Mathf.Max(0.5f, e.SpeedFactor) * (e.IsCharger ? 3.3f : 1f);
        float travel = Mathf.Max(0f, best - reach) / Mathf.Max(0.5f, speed);
        return travel + e.NextBlowIn + 0.45f;
    }

    static float Distance(Enemy e, Vector3 to) {
        Vector3 d = to - e.transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    static bool InView(Camera cam, Vector3 world) {
        Vector3 vp = cam.WorldToViewportPoint(world);
        return vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
    }

    // Eleven kinds, eleven colours that stay apart at a glance (Yo Bots cool or hot, Spider Bots greens, golds and violets), and a
    // ring size per kind on top of the bot's own size, so the ring alone names the kind (the arcade-acts sweep reads both).
    public static Color KindColour(BotKind kind) {
        switch (kind) {
            case BotKind.YoBot: return new Color(0.30f, 0.86f, 1.00f);
            case BotKind.PlatedYoBot: return new Color(0.93f, 0.95f, 0.98f);
            case BotKind.MegaYoBot: return new Color(1.00f, 0.22f, 0.25f);
            case BotKind.SpiderBot: return new Color(0.20f, 0.92f, 0.32f);
            case BotKind.StalkerSpiderBot: return new Color(1.00f, 0.50f, 0.88f);
            case BotKind.RunnerSpiderBot: return new Color(0.82f, 1.00f, 0.18f);
            case BotKind.RammerSpiderBot: return new Color(0.66f, 0.42f, 0.24f);
            case BotKind.ShadowSpiderBot: return new Color(0.52f, 0.28f, 1.00f);
            case BotKind.CrusherSpiderBot: return new Color(1.00f, 0.55f, 0.08f);
            case BotKind.ColossusSpiderBot: return new Color(0.22f, 0.42f, 1.00f);
            case BotKind.TitanSpiderBot: return new Color(1.00f, 0.84f, 0.30f);
        }
        return new Color(0.9f, 0.9f, 0.9f);
    }

    public static float KindSize(BotKind kind) {
        switch (kind) {
            case BotKind.YoBot: return 0.9f;
            case BotKind.PlatedYoBot: return 1.0f;
            case BotKind.MegaYoBot: return 1.12f;
            case BotKind.SpiderBot: return 0.95f;
            case BotKind.StalkerSpiderBot: return 1.0f;
            case BotKind.RunnerSpiderBot: return 0.88f;
            case BotKind.RammerSpiderBot: return 1.05f;
            case BotKind.ShadowSpiderBot: return 1.1f;
            case BotKind.CrusherSpiderBot: return 1.18f;
            case BotKind.ColossusSpiderBot: return 1.28f;
            case BotKind.TitanSpiderBot: return 1.4f;
        }
        return 1f;
    }

    // ---- the frame

    void Update() {
        Camera cam = Camera.main;
        var roster = Enemy.Roster;
        if (cam == null) {
            return;
        }
        bool hidden = LevelUpScreen.IsOpen || PauseMenu.IsOpen || Cutscene.IsOpen || roster.Count == 0;
        if (hidden) {
            ReleaseAll();
            HideArrows(0);
        } else {
            EnsureArt();
            if (block == null) {
                block = new MaterialPropertyBlock();
            }
            if (mat == null) {
                mat = new Material(SafeShader.Find("Sprites/Default")) { renderQueue = 2990 };
            }
        }

        int used = 0;
        for (int i = 0; i < roster.Count; i++) {
            Enemy e = roster[i];
            if (e == null || !e.Alive) {
                continue;
            }
            Vector3 body = e.transform.position + Vector3.up;
            bool inView = InView(cam, body);
            byte f;
            flags.TryGetValue(e, out f);
            if (inView && (f & Seen) == 0) {
                flags[e] = (byte)(f | Seen);
            }
            if (hidden) {
                continue;
            }
            if (!e.Scripted) {
                used++;
                TickRing(cam, e);
            }
        }
        PeakRings = Mathf.Max(PeakRings, used);
        if (!hidden) {
            DropGone(roster);
            TickArrows(cam, roster);
        }
    }

    void ReleaseAll() {
        if (rings.Count == 0) {
            return;
        }
        foreach (var kv in rings) {
            kv.Value.go.SetActive(false);
            spare.Push(kv.Value);
        }
        rings.Clear();
    }

    void DropGone(List<Enemy> roster) {
        gone.Clear();
        foreach (var kv in rings) {
            Enemy e = kv.Key;
            if (e == null || !e.Alive || e.Scripted || !roster.Contains(e)) {
                gone.Add(e);
            }
        }
        for (int i = 0; i < gone.Count; i++) {
            Ring r;
            if (rings.TryGetValue(gone[i], out r)) {
                r.go.SetActive(false);
                spare.Push(r);
            }
            rings.Remove(gone[i]);
        }
    }

    // ---- the ground ring

    void TickRing(Camera cam, Enemy e) {
        Ring r;
        if (!rings.TryGetValue(e, out r)) {
            r = spare.Count > 0 ? spare.Pop() : MakeRing();
            rings[e] = r;
            r.groundAt = -10f;
        }
        Vector3 p = e.transform.position;
        if (Time.time - r.groundAt > 0.25f) {
            r.groundAt = Time.time;
            r.groundY = Physics.Raycast(new Vector3(p.x, p.y + 4f, p.z), Vector3.down, out RaycastHit hit, 40f, 1 << 3, QueryTriggerInteraction.Ignore) ? hit.point.y : p.y;
        }
        float baseD = Mathf.Clamp(0.45f + e.BodyScale * 0.16f, 0.9f, 4.5f) * 2f * KindSize(e.Kind);
        float dist = Mathf.Max(1f, Vector3.Distance(cam.transform.position, new Vector3(p.x, r.groundY, p.z)));
        float pxPerUnit = Screen.height / (2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
        float d = baseD;
        bool touch = UiFit.Touch;
        float minPx = RingMinCssPx * (touch ? UiFit.PixelRatio : 1f) * 1.15f;
        if (d * pxPerUnit < minPx) {
            d = Mathf.Min(minPx / Mathf.Max(0.01f, pxPerUnit), baseD * 3f);
        }
        float k = 0f;
        bool wind = e.Winding;
        if (wind) {
            k = 1f - Mathf.Clamp01(e.WindRemaining / 0.5f);
        }
        float pulse = wind ? 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(16f, 40f, k)) : 0f;
        float shown = d * (wind ? Mathf.Lerp(1.05f, 1.3f, k) : 1f);
        float alpha = wind ? Mathf.Lerp(0.9f, 1f, pulse) : 0.5f;
        Color c = KindColour(e.Kind);
        if (wind) {
            c = Color.Lerp(c, new Color(1f, 0.95f, 0.85f), 0.45f + 0.3f * pulse);
        }
        r.go.SetActive(true);
        r.go.transform.position = new Vector3(p.x, r.groundY + RingY, p.z);
        r.go.transform.localScale = new Vector3(shown, shown, 1f);
        block.SetColor(ColorId, new Color(c.r, c.g, c.b, alpha));
        r.renderer.SetPropertyBlock(block);

        float px = d * pxPerUnit;
        if (touch) {
            RingMinPx = Mathf.Min(RingMinPx, px / UiFit.PixelRatio);
        } else {
            RingMinPx = Mathf.Min(RingMinPx, px);
        }
        if (wind) {
            WindFrames++;
            RingAlphaWind = Mathf.Max(RingAlphaWind, alpha);
        } else {
            RingAlphaIdle = Mathf.Max(RingAlphaIdle, alpha);
        }
    }

    Ring MakeRing() {
        var go = new GameObject("Threat Ring");
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        var rd = go.AddComponent<MeshRenderer>();
        rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rd.receiveShadows = false;
        var m = new Material(mat) { mainTexture = ringTex };
        m.renderQueue = 2990;
        rd.sharedMaterial = m;
        go.SetActive(false);
        return new Ring { go = go, renderer = rd };
    }

    // ---- the edge arrows

    void TickArrows(Camera cam, List<Enemy> roster) {
        int n = 0;
        for (int i = 0; i < roster.Count; i++) {
            Enemy e = roster[i];
            if (e == null || !e.Alive || e.Dormant || e.Scripted || e.IsCourier) {
                continue;
            }
            if (InView(cam, e.transform.position + Vector3.up)) {
                continue;
            }
            float eta = EtaOf(e);
            if (eta > StrikeSeconds) {
                continue;
            }
            // Keep the MaxArrows soonest to strike, sorted.
            int at = n;
            if (n < MaxArrows) {
                n++;
            } else if (eta >= pickEta[MaxArrows - 1]) {
                continue;
            } else {
                at = MaxArrows - 1;
            }
            while (at > 0 && pickEta[at - 1] > eta) {
                pick[at] = pick[at - 1];
                pickEta[at] = pickEta[at - 1];
                at--;
            }
            pick[at] = e;
            pickEta[at] = eta;
        }
        PeakArrows = Mathf.Max(PeakArrows, n);
        if (n == 0) {
            HideArrows(0);
            return;
        }
        EnsureCanvas();
        float scale = Mathf.Max(0.01f, canvas.scaleFactor);
        float sizePx = Mathf.Max(64f * scale, 48f * UiFit.PixelRatio);
        Rect safe = UiRects.SafeArea();
        float m = sizePx * 0.5f + 8f * UiFit.PixelRatio;
        Rect bounds = Rect.MinMaxRect(safe.xMin + m, safe.yMin + m, safe.xMax - m, safe.yMax - m);
        bool touch = UiFit.Touch;
        for (int i = 0; i < n; i++) {
            Enemy e = pick[i];
            byte f;
            flags.TryGetValue(e, out f);
            if ((f & Marked) == 0) {
                flags[e] = (byte)(f | Marked);
                OffscreenMarked++;
                if (Log.Count < 100) {
                    Log.Add(Time.unscaledTime.ToString("0.00") + "|marked|" + e.Title + "|eta " + pickEta[i].ToString("0.0"));
                }
            }
            Vector3 vp = cam.WorldToViewportPoint(e.transform.position + Vector3.up);
            Vector2 v = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
            if (vp.z <= 0f) {
                v = -v;
            }
            if (v.sqrMagnitude < 0.0001f) {
                v = Vector2.down;
            }
            Vector2 centre = bounds.center;
            Vector2 dir = new Vector2(v.x * Screen.width, v.y * Screen.height);
            float tx = Mathf.Abs(dir.x) > 0.01f ? bounds.width * 0.5f / Mathf.Abs(dir.x) : float.MaxValue;
            float ty = Mathf.Abs(dir.y) > 0.01f ? bounds.height * 0.5f / Mathf.Abs(dir.y) : float.MaxValue;
            Vector2 pos = centre + dir * Mathf.Min(tx, ty);
            if (touch) {
                float floor = Screen.height * 0.52f;
                if (pos.y < floor) {
                    // The stick (bottom left) and Swing (bottom right) own the low corners: slide up the side edge.
                    pos.x = dir.x < 0f ? bounds.xMin : bounds.xMax;
                    pos.y = Mathf.Max(floor, bounds.yMin);
                }
                int tries = 0;
                while (tries < 4 && OverlapsPads(pos, sizePx)) {
                    pos.y = Mathf.Min(bounds.yMax, pos.y + Screen.height * 0.08f);
                    tries++;
                }
            }
            pos.x = Mathf.Clamp(pos.x, bounds.xMin, bounds.xMax);
            pos.y = Mathf.Clamp(pos.y, bounds.yMin, bounds.yMax);

            Image img = ArrowAt(i);
            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(sizePx / scale, sizePx / scale);
            rt.anchoredPosition = pos / scale;
            float urgent = Mathf.Clamp01(1f - pickEta[i] / StrikeSeconds);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            float beat = 0.8f + 0.2f * Mathf.Sin(Time.time * Mathf.Lerp(6f, 18f, urgent));
            Color c = KindColour(e.Kind);
            c = Color.Lerp(c, new Color(1f, 0.95f, 0.85f), urgent * 0.5f);
            c.a = Mathf.Lerp(0.7f, 1f, urgent) * beat;
            img.color = c;
            img.gameObject.SetActive(true);

            ArrowFrames++;
            ArrowMinPx = Mathf.Min(ArrowMinPx, sizePx / UiFit.PixelRatio);
            Rect box = Rect.MinMaxRect(pos.x - sizePx * 0.5f, pos.y - sizePx * 0.5f, pos.x + sizePx * 0.5f, pos.y + sizePx * 0.5f);
            if (box.xMin < safe.xMin - 0.5f || box.xMax > safe.xMax + 0.5f || box.yMin < safe.yMin - 0.5f || box.yMax > safe.yMax + 0.5f) {
                ArrowOutsideSafe++;
            }
            if (touch && OverlapsPads(pos, sizePx)) {
                ArrowPadOverlaps++;
            }
        }
        HideArrows(n);
    }

    static readonly string[] PadNames = { "Swing", "Jump", "Shake", "Next", "Ability", "Dodge", "Interact", "Look" };

    static bool OverlapsPads(Vector2 pos, float size) {
        if (!TouchControls.PadsShown) {
            return false;
        }
        float h = size * 0.5f;
        Rect box = Rect.MinMaxRect(pos.x - h, pos.y - h, pos.x + h, pos.y + h);
        for (int i = 0; i < PadNames.Length; i++) {
            Rect pad;
            if (TouchControls.PadBox(PadNames[i], out pad) && pad.width > 1f && pad.Overlaps(box)) {
                return true;
            }
        }
        Vector2 stick;
        if (TouchControls.Spot("Stick", out stick)) {
            float r = TouchControls.StickReach;
            if (new Rect(stick.x - r, stick.y - r, r * 2f, r * 2f).Overlaps(box)) {
                return true;
            }
        }
        return false;
    }

    void HideArrows(int from) {
        for (int i = from; i < arrows.Count; i++) {
            if (arrows[i] != null && arrows[i].gameObject.activeSelf) {
                arrows[i].gameObject.SetActive(false);
            }
        }
    }

    Image ArrowAt(int i) {
        while (arrows.Count <= i) {
            var go = new GameObject("Threat Arrow " + arrows.Count);
            go.transform.SetParent(canvas.transform, false);
            var img = go.AddComponent<Image>();
            img.sprite = Sprite.Create(arrowTex, new Rect(0, 0, arrowTex.width, arrowTex.height), new Vector2(0.5f, 0.5f), 100f);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            go.SetActive(false);
            arrows.Add(img);
        }
        return arrows[i];
    }

    void EnsureCanvas() {
        if (canvas != null) {
            return;
        }
        var go = new GameObject("Threat Arrows");
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 117;   // under the edge glow, the pause menu, the cards and the touch pads
        var scaler = go.AddComponent<CanvasScaler>();
        UiFit.Apply(scaler);
    }

    // ---- art, drawn in code once

    static void EnsureArt() {
        if (quad != null) {
            return;
        }
        quad = new Mesh { name = "Threat Quad" };
        quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        quad.RecalculateBounds();
        // A solid band at the rim, a faint inside so the bot reads as standing in it.
        ringTex = Disc(128, r => r > 1f ? 0f : (r > 0.8f ? 1f - Mathf.Clamp01((r - 0.95f) / 0.05f) : 0.14f));
        arrowTex = Arrow(64);
    }

    static Texture2D Disc(int size, System.Func<float, float> alphaAt) {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "threat_ring" };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(alphaAt(Mathf.Sqrt(dx * dx + dy * dy)));
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    // A chunky arrowhead pointing up, with a soft edge and a notch cut from its base.
    static Texture2D Arrow(int size) {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "threat_arrow" };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float u = Mathf.Abs((x + 0.5f) / size * 2f - 1f);   // 0 centre .. 1 side
                float v = (y + 0.5f) / size;                          // 0 bottom .. 1 top
                float half = Mathf.Lerp(0f, 0.85f, Mathf.Clamp01((0.95f - v) / 0.8f));
                float edge = half - u;
                float notch = 0.12f + (0.3f - u * 0.35f);             // the base is a V: lower at the sides
                float a = v < notch * 0.6f || v > 0.97f ? 0f : Mathf.Clamp01(edge * 12f);
                float rim = Mathf.Clamp01(edge * 5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * (0.75f + 0.25f * rim) * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }
}
