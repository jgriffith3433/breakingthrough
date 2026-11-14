using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The visual tell of every blow (J's feel brief: the player sees every hit coming in time to swing or dodge).
//
// A regular bot's wind-up (Enemy: CharacterRig.BeginWindup, the same moment BotSound.Tell plays) now also shows:
//   - a soft red glow on the claw or leg that is about to strike (a flat-shaded billboard on the limb, brighter and flickering
//     faster as the blow nears; never a spark or a floating word), and
//   - a ground marker under the target (Slice's feet, Wallit's, the wall): a red ink rim with a fill that grows to the rim as
//     the blow lands, flat on the ground a few centimetres above it, drawn without depth writes so nothing z-fights.
// A boss's heavy blows already draw their ground marks (BossPhases, Courier, Server); they tell this class when the blow lands
// so the lead is measured, and the boss's striking arm glows too.
// When a bot reaches Slice's circle, the circle's ring pulses red and, if Slice is off screen, a soft red glow lights the screen
// edge toward her (clear of the stick and Swing pad on a phone).
//
// Metrics for the playtest: LeadMs (regular bots), BossLeadMs, Untelegraphed (a blow from a bot that landed with no tell),
// Pulses, EdgeTints. Everything is in the Department palette (DeptPalette.Red).
public class AttackTell : MonoBehaviour {
    public const float MinLead = 0.4f;
    public const float MaxLead = 0.8f;
    public const float CircleRadius = 3.6f;
    const int Pool = 24;

    public static readonly List<float> LeadMs = new List<float>();
    public static readonly List<float> BossLeadMs = new List<float>();
    public static readonly List<string> Log = new List<string>();
    public static int Begun, Landed, Cancelled, Untelegraphed, Climbs, Glows, Markers, CircleReached, Pulses, EdgeTints, EdgeTintFrames;
    public static int EdgeTintOverlaps, MarkerPadOverlaps, EdgeTintPhoneFrames;
    public static float EdgeTintLowest = 1f;   // the lowest viewport y the tint reached on a phone (pads sit under 0.4)

    static AttackTell instance;
    static Mesh quad;
    static Texture2D glowTex, rimTex, fillTex, ringTex, edgeTex;
    static readonly Dictionary<Enemy, float> toldAt = new Dictionary<Enemy, float>();

    class Tell {
        public Enemy enemy;
        public Renderer limbRenderer;
        public Transform limb;
        public float start, duration;
        public Vector3 at;
        public float radius;
        public bool ground;
        public GameObject glow, rim, fill;
        public Material glowMat, rimMat, fillMat;
    }

    readonly List<Tell> live = new List<Tell>();
    readonly Stack<Tell> spare = new Stack<Tell>();
    GameObject circle;
    Material circleMat;
    float circleAlpha, nextPulse, edgeAlpha;
    bool tintUp;
    Canvas canvas;
    Image edge;
    RectTransform edgeRect;

    // A finished scene's bots are keys here: drop them so they can be collected (SceneMemory).
    public static void Prune() {
        PruneDead(toldAt);
    }

    static void PruneDead(Dictionary<Enemy, float> map) {
        List<Enemy> dead = null;
        foreach (Enemy e in map.Keys) {
            if (e == null) {
                if (dead == null) dead = new List<Enemy>();
                dead.Add(e);
            }
        }
        if (dead != null) {
            foreach (Enemy e in dead) map.Remove(e);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() {
        instance = null;
        quad = null;
        glowTex = rimTex = fillTex = ringTex = edgeTex = null;
        LeadMs.Clear();
        BossLeadMs.Clear();
        Log.Clear();
        toldAt.Clear();
        Begun = Landed = Cancelled = Untelegraphed = Climbs = Glows = Markers = CircleReached = Pulses = EdgeTints = EdgeTintFrames = 0;
        EdgeTintOverlaps = MarkerPadOverlaps = EdgeTintPhoneFrames = 0;
        EdgeTintLowest = 1f;
    }

    static AttackTell Instance {
        get {
            if (instance == null) {
                instance = new GameObject("Attack Tells").AddComponent<AttackTell>();
            }
            return instance;
        }
    }

    // ---- the callers

    // A regular bot's wind-up starts (Enemy.Update). limb: the claw or leg about to strike. at: where the blow lands.
    public static void Begin(Enemy e, Transform limb, Vector3 at, float radius, float seconds) {
        if (e == null || e.Scripted) {
            return;
        }
        Begun++;
        Instance.Add(e, limb, at, radius * (UiFit.Touch ? 1.35f : 1f), seconds, true);
    }

    // A boss's ground-mark wind-up starts: its marks are already drawn (BossPhases); the arm glows and the lead is timed.
    public static void BossBegin(Enemy e, Transform limb, float seconds) {
        if (e == null) {
            return;
        }
        Begun++;
        Instance.Add(e, limb, e.transform.position, 0f, seconds, false);
    }

    // The blow lands (before it hurts anyone). A regular bot's lead must be 0.4-0.8 s; a boss's at least 0.6 s.
    public static void Land(Enemy e, bool boss = false) {
        if (e == null) {
            return;
        }
        toldAt[e] = Time.time;
        if (instance == null) {
            return;
        }
        Tell found = null;
        for (int i = 0; i < instance.live.Count; i++) {
            if (instance.live[i].enemy == e) {
                found = instance.live[i];
                break;
            }
        }
        if (found == null) {
            return;
        }
        float ms = (Time.time - found.start) * 1000f;
        Landed++;
        (boss ? BossLeadMs : LeadMs).Add(ms);
        if (Log.Count < 200) {
            Log.Add(Time.unscaledTime.ToString("0.00") + "|land|" + (boss ? "boss" : "bot") + "|" + e.Title + "|lead " + ms.ToString("0") + " ms");
        }
        instance.Release(found);
    }

    public static void Cancel(Enemy e) {
        if (instance == null || e == null) {
            return;
        }
        for (int i = 0; i < instance.live.Count; i++) {
            if (instance.live[i].enemy == e) {
                Cancelled++;
                instance.Release(instance.live[i]);
                return;
            }
        }
    }

    // A scripted blow that has its own tell (the Courier's and the Server's marks, the plating's first crack, a climb's grab).
    public static void Told(Enemy e) {
        if (e != null) {
            toldAt[e] = Time.time;
        }
    }

    public static void Climb(Enemy e) {
        Climbs++;
        Told(e);
    }

    // Every damage path calls this first (Musician.Hurt, PlayerGrowth.OnHit): a bot's blow with no tell is counted.
    public static void NoteBlow(Enemy by) {
        if (by == null) {
            return;
        }
        float at;
        if (toldAt.TryGetValue(by, out at) && Time.time - at <= 0.25f) {
            return;
        }
        Untelegraphed++;
        if (Log.Count < 200) {
            Log.Add(Time.unscaledTime.ToString("0.00") + "|untelegraphed|" + by.Title);
        }
    }

    // A bot has come within reach of Slice (Enemy.Update, once per arrival).
    public static void ReachedCircle(Enemy e) {
        CircleReached++;
        SliceVoice.Reached(e);
        if (Log.Count < 200) {
            Log.Add(Time.unscaledTime.ToString("0.00") + "|circle|" + (e != null ? e.Title : "?"));
        }
    }

    // ---- the visuals

    void Add(Enemy e, Transform limb, Vector3 at, float radius, float seconds, bool ground) {
        for (int i = 0; i < live.Count; i++) {
            if (live[i].enemy == e) {
                Release(live[i]);
                break;
            }
        }
        if (live.Count >= Pool) {
            Release(live[0]);
        }
        Tell t = spare.Count > 0 ? spare.Pop() : Make();
        t.enemy = e;
        t.limb = limb;
        t.limbRenderer = limb != null ? limb.GetComponentInChildren<Renderer>() : null;
        t.start = Time.time;
        t.duration = Mathf.Max(0.1f, seconds);
        t.ground = ground && radius > 0f;
        t.radius = radius;
        t.at = new Vector3(at.x, GroundY(at), at.z);
        t.glow.SetActive(true);
        t.rim.SetActive(t.ground);
        t.fill.SetActive(t.ground);
        live.Add(t);
        Glows++;
        if (t.ground) {
            Markers++;
            Vector3 vp = ViewportOf(at);
            if (UiFit.Touch && vp.z > 0f && TouchOverlap(vp)) {
                MarkerPadOverlaps++;
            }
        }
        Paint(t, 0f);
    }

    Tell Make() {
        EnsureArt();
        var t = new Tell();
        t.glow = Quad("Tell Glow", glowTex, 3000, out t.glowMat);
        t.rim = Quad("Tell Marker", rimTex, 3001, out t.rimMat);
        t.fill = Quad("Tell Marker Fill", fillTex, 3000, out t.fillMat);
        t.rim.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        t.fill.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        return t;
    }

    GameObject Quad(string name, Texture2D tex, int queue, out Material mat) {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        var r = go.AddComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        Shader shader = SafeShader.Find("Sprites/Default");
        mat = new Material(shader) { mainTexture = tex, color = Color.clear };
        mat.renderQueue = queue;
        r.sharedMaterial = mat;
        go.SetActive(false);
        return go;
    }

    void Release(Tell t) {
        live.Remove(t);
        t.glow.SetActive(false);
        t.rim.SetActive(false);
        t.fill.SetActive(false);
        t.enemy = null;
        spare.Push(t);
    }

    void Update() {
        Camera cam = Camera.main;
        for (int i = live.Count - 1; i >= 0; i--) {
            Tell t = live[i];
            if (t.enemy == null || !t.enemy.Alive || Time.time - t.start > t.duration + 0.5f) {
                Release(t);
                continue;
            }
            Paint(t, Mathf.Clamp01((Time.time - t.start) / t.duration));
            if (cam != null) {
                t.glow.transform.rotation = cam.transform.rotation;
            }
        }
        TickCircle(cam);
    }

    static float GroundY(Vector3 p) {
        if (Physics.Raycast(new Vector3(p.x, p.y + 4f, p.z), Vector3.down, out RaycastHit hit, 30f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            return hit.point.y;
        }
        return p.y;
    }

    static void Paint(Tell t, float k) {
        // The glow sits on the striking limb and flares as the blow nears; its flicker quickens with k.
        Vector3 pos = t.limbRenderer != null ? t.limbRenderer.bounds.center : (t.limb != null ? t.limb.position : t.enemy.transform.position + Vector3.up);
        float size = t.limbRenderer != null ? Mathf.Clamp(t.limbRenderer.bounds.size.magnitude * 1.7f, 0.9f, 3.6f) : 1.4f;
        if (UiFit.Touch) {
            size *= 1.4f;
        }
        float flick = 0.82f + 0.18f * Mathf.Sin(Time.time * Mathf.Lerp(18f, 46f, k));
        t.glow.transform.position = pos;
        t.glow.transform.localScale = Vector3.one * size * Mathf.Lerp(0.8f, 1.15f, k);
        t.glowMat.color = new Color(1f, 0.32f, 0.22f, Mathf.Lerp(0.35f, 0.95f, k) * flick);
        if (!t.ground) {
            return;
        }
        float d = t.radius * 2f;
        t.rim.transform.position = t.at + Vector3.up * 0.045f;
        t.rim.transform.localScale = new Vector3(d, d, 1f);
        Color red = DeptPalette.Red;
        t.rimMat.color = new Color(red.r + 0.15f, red.g + 0.1f, red.b + 0.1f, Mathf.Lerp(0.5f, 0.95f, k));
        float f = d * Mathf.Lerp(0.12f, 0.96f, k);
        t.fill.transform.position = t.at + Vector3.up * 0.035f;
        t.fill.transform.localScale = new Vector3(f, f, 1f);
        t.fillMat.color = new Color(red.r, red.g, red.b, Mathf.Lerp(0.3f, 0.65f, k));
    }

    // ---- Slice's circle: it pulses while a bot is at it, and the screen edge lights toward her when she is off screen

    void TickCircle(Camera cam) {
        Musician slice = Musician.Instance;
        bool any = false;
        if (slice != null && slice.Alive && !WaveDirector.IsLost) {
            var roster = Enemy.Roster;
            for (int i = 0; i < roster.Count; i++) {
                if (roster[i] != null && roster[i].AtCircle) {
                    any = true;
                    break;
                }
            }
        }
        float before = circleAlpha;
        circleAlpha = Mathf.MoveTowards(circleAlpha, any ? 1f : 0f, Time.unscaledDeltaTime * (any ? 6f : 2.5f));
        if (circleAlpha <= 0.001f) {
            if (circle != null && circle.activeSelf) {
                circle.SetActive(false);
            }
            SetEdge(0f, Vector2.zero);
            nextPulse = 0f;
            return;
        }
        EnsureCircle();
        circle.SetActive(true);
        Vector3 at = slice.transform.position;
        float y = GroundY(at);
        circle.transform.position = new Vector3(at.x, y + 0.04f, at.z);
        float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * (Mathf.PI * 2f / 0.9f));
        float d = CircleRadius * 2f * (0.94f + 0.1f * beat);
        circle.transform.localScale = new Vector3(d, d, 1f);
        Color red = DeptPalette.Red;
        circleMat.color = new Color(red.r + 0.12f, red.g + 0.08f, red.b + 0.08f, circleAlpha * (0.35f + 0.5f * beat));
        if (Time.time >= nextPulse && any) {
            nextPulse = Time.time + 0.9f;
            Pulses++;
            Fx.Ring(at, CircleRadius * 2.6f, new Color(DeptPalette.Red.r + 0.14f, 0.3f, 0.24f, 0.7f), 0.7f);
            if (Log.Count < 200) {
                Log.Add(Time.unscaledTime.ToString("0.00") + "|pulse");
            }
        }

        // Off screen: a glow on the edge toward her.
        Vector2 where = Vector2.zero;
        float want = 0f;
        if (cam != null) {
            Vector3 vp = cam.WorldToViewportPoint(at + Vector3.up * 1f);
            bool off = vp.z <= 0f || vp.x < 0.02f || vp.x > 0.98f || vp.y < 0.02f || vp.y > 0.98f;
            if (off) {
                Vector2 v = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                if (vp.z <= 0f) {
                    v = -v;
                }
                if (v.sqrMagnitude < 0.0001f) {
                    v = Vector2.down;
                }
                float m = Mathf.Max(Mathf.Abs(v.x), Mathf.Abs(v.y));
                where = new Vector2(0.5f, 0.5f) + v * (0.5f / m);
                if (UiFit.Touch) {
                    // The stick (bottom left) and Swing (bottom right) own the low corners: slide up the side edge.
                    float side = where.x < 0.5f ? 0f : 1f;
                    if (Mathf.Abs(v.x) > 0.02f) {
                        side = v.x < 0f ? 0f : 1f;
                    }
                    if (where.y < 0.52f) {
                        where = new Vector2(side, 0.52f);
                    }
                    where.x = Mathf.Clamp(where.x, 0f, 1f);
                }
                where.x = Mathf.Clamp(where.x, 0f, 1f);
                where.y = Mathf.Clamp(where.y, 0f, 1f);
                want = circleAlpha;
            }
        }
        SetEdge(want, where);
        if (circleAlpha > before && before <= 0.001f) {
            nextPulse = 0f;
        }
    }

    void SetEdge(float alpha, Vector2 viewport) {
        edgeAlpha = Mathf.MoveTowards(edgeAlpha, alpha, Time.unscaledDeltaTime * 5f);
        if (edgeAlpha <= 0.001f) {
            if (edge != null && edge.gameObject.activeSelf) {
                edge.gameObject.SetActive(false);
            }
            tintUp = false;
            return;
        }
        EnsureEdge();
        edge.gameObject.SetActive(true);
        float size = UiFit.Touch ? 260f : 420f;
        edgeRect.sizeDelta = new Vector2(size, size);
        edgeRect.anchorMin = edgeRect.anchorMax = viewport;
        edgeRect.anchoredPosition = Vector2.zero;
        float beat = 0.7f + 0.3f * Mathf.Sin(Time.time * 7f);
        Color red = DeptPalette.Red;
        edge.color = new Color(red.r + 0.1f, red.g + 0.05f, red.b + 0.05f, 0.6f * edgeAlpha * beat);
        if (!tintUp) {
            tintUp = true;
            EdgeTints++;
        }
        EdgeTintFrames++;
        if (UiFit.Touch) {
            EdgeTintPhoneFrames++;
            EdgeTintLowest = Mathf.Min(EdgeTintLowest, viewport.y);
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            float half = size * scale * 0.5f * 0.6f;   // the visible core of the soft glow
            var box = Rect.MinMaxRect(viewport.x * Screen.width - half, viewport.y * Screen.height - half, viewport.x * Screen.width + half, viewport.y * Screen.height + half);
            if (OverlapsPads(box)) {
                EdgeTintOverlaps++;
            }
        }
    }

    static Vector3 ViewportOf(Vector3 world) {
        Camera cam = Camera.main;
        return cam != null ? cam.WorldToViewportPoint(world) : new Vector3(0.5f, 0.5f, 1f);
    }

    static readonly string[] PadNames = { "Swing", "Jump", "Shake", "Next", "Ability", "Dodge", "Interact", "Look" };

    static bool TouchOverlap(Vector3 viewport) {
        var point = new Vector2(viewport.x * Screen.width, viewport.y * Screen.height);
        return OverlapsPads(new Rect(point.x - 1f, point.y - 1f, 2f, 2f));
    }

    static bool OverlapsPads(Rect box) {
        if (!TouchControls.PadsShown) {
            return false;
        }
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

    void EnsureCircle() {
        if (circle != null) {
            return;
        }
        EnsureArt();
        circle = Quad("Circle Pulse", ringTex, 3001, out circleMat);
        circle.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }

    void EnsureEdge() {
        if (canvas != null) {
            return;
        }
        EnsureArt();
        var go = new GameObject("Edge Tell");
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 118;   // under the pause menu, the cards and the touch pads (they draw over it)
        var scaler = go.AddComponent<CanvasScaler>();
        UiFit.Apply(scaler);
        var img = new GameObject("Glow");
        img.transform.SetParent(go.transform, false);
        edge = img.AddComponent<Image>();
        edge.sprite = Sprite.Create(edgeTex, new Rect(0, 0, edgeTex.width, edgeTex.height), new Vector2(0.5f, 0.5f), 100f);
        edge.raycastTarget = false;
        edgeRect = edge.rectTransform;
    }

    // ---- art, drawn in code once

    static void EnsureArt() {
        if (quad != null) {
            return;
        }
        quad = new Mesh { name = "Tell Quad" };
        quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quad.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        quad.RecalculateBounds();
        glowTex = Art(64, (r) => Mathf.Pow(Mathf.Clamp01(1f - r), 2f) * 1f);
        // A rim ring (bright band at the edge, a faint inside) for the ground marker; a soft disc for its fill.
        rimTex = Art(128, (r) => r > 1f ? 0f : (r > 0.88f ? 1f - Mathf.Clamp01((r - 0.97f) / 0.03f) * 1f : 0.16f));
        fillTex = Art(128, (r) => r > 1f ? 0f : Mathf.Lerp(1f, 0.55f, r));
        ringTex = Art(128, (r) => r > 1f ? 0f : (r > 0.9f ? 1f : (r > 0.8f ? (r - 0.8f) / 0.1f * 0.9f : 0f)));
        edgeTex = Art(64, (r) => Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f));
    }

    static Texture2D Art(int size, System.Func<float, float> alphaAt) {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "tell_art" };
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
}
