using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Every blow is seen coming. A bot's wind-up (CharacterRig RigState.Windup, the one BotSound.Tell sounds) now shows on the
// body and the ground: a soft red glow on the claw or front leg that is about to strike, and a flat stamp-red ground mark under
// whoever it will hit, filling until the blow lands (0.45 s Yo Bot, 0.50 s Spider Bot). A boss's heavy blow (Enemy.BeginTell ..
// EndTell) is timed here too. When a bot reaches Slice's circle the circle pulses, and when she is off screen the screen edge
// glows toward her (kept off the stick and the Swing pad). No floating text: the tell is a glow and a mark.
// Metrics: LeadsMs (wind-up start to blow), BossLeadsMs (tell start to slam), Untelegraphed (a bot's blow on Slice or Wallit
// that landed with no tell running), Pulses, EdgeShows.
public class CombatTells : MonoBehaviour {
    public const float CircleRadius = 9f;       // a bot this close to Slice has reached her circle
    const float GlowEvery = 0.07f;
    const float MarkY = 0.11f;                  // above the boss marks (0.07/0.09) and Fx rings (0.08): no shared plane

    public static readonly List<float> LeadsMs = new List<float>();
    public static readonly List<float> BossLeadsMs = new List<float>();
    public static int Windups, Untelegraphed, Pulses, EdgeShows, Reaches;
    public static bool EdgeVisible { get; private set; }
    public static Rect EdgeRect { get; private set; }           // screen pixels (origin bottom-left) of the tint now showing
    public static bool Exempt;                                    // a scripted blow (the wave 10 crack) has no tell by design
    public static readonly List<string> Events = new List<string>();

    class Tell {
        public Enemy enemy;
        public Transform limb, target;
        public Vector3 point;
        public float start, seconds, landedAt = -1f, glowAt, diameter;
        public bool boss, onSlice, active = true;
        public Mark mark;
    }

    class Mark {
        public GameObject root;
        public Transform fill, rimRoot;
        public Material rimMat, fillMat;
    }

    static CombatTells inst;
    static readonly Dictionary<int, Tell> Tells = new Dictionary<int, Tell>();
    static readonly List<Mark> Pool = new List<Mark>();
    static readonly List<int> Drop = new List<int>();
    static readonly HashSet<int> Reached = new HashSet<int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
        inst = null;
        Tells.Clear();
        Pool.Clear();
        Reached.Clear();
        LeadsMs.Clear();
        BossLeadsMs.Clear();
        Events.Clear();
        Windups = Untelegraphed = Pulses = EdgeShows = Reaches = 0;
        EdgeVisible = false;
        Exempt = false;
    }

    static void Ensure() {
        if (inst == null) {
            inst = new GameObject("Combat Tells").AddComponent<CombatTells>();
        }
    }

    // ---- what the bots and bosses call

    // An ordinary bot's wind-up starts: limb is the claw or leg that will strike, target who it will hit.
    public static void Begin(Enemy e, float seconds, Transform limb, Transform target, bool onSlice) {
        if (e == null) {
            return;
        }
        Ensure();
        Windups++;
        Drop1(e);
        var t = new Tell { enemy = e, limb = limb, target = target, start = Time.time, seconds = seconds, onSlice = onSlice };
        t.diameter = (e.IsSpiderFamily ? 3.4f : 2.4f) * (UiFit.Touch ? 1.3f : 1f);
        t.mark = Take();
        Tells[e.GetInstanceID()] = t;
        if (onSlice) {
            Pulse(Musician.Instance != null ? Musician.Instance.transform.position : e.transform.position);
        }
    }

    // The blow lands: the lead is what the player had.
    public static void Landed(Enemy e) {
        Tell t;
        if (e == null || !Tells.TryGetValue(e.GetInstanceID(), out t) || !t.active) {
            return;
        }
        float lead = (Time.time - t.start) * 1000f;
        if (t.boss) {
            BossLeadsMs.Add(lead);
        } else {
            LeadsMs.Add(lead);
        }
        t.active = false;
        t.landedAt = Time.time;
        Release(t);
    }

    public static void Cancel(Enemy e) {
        Tell t;
        if (e != null && Tells.TryGetValue(e.GetInstanceID(), out t)) {
            t.active = false;
            t.landedAt = -10f;
            Release(t);
        }
    }

    // A boss's heavy blow: the tell starts (its own ground marks are drawn by BossPhases, Courier, Server or the slam).
    public static void BossBegin(Enemy e) {
        if (e == null) {
            return;
        }
        Ensure();
        Drop1(e);
        Tells[e.GetInstanceID()] = new Tell { enemy = e, start = Time.time, boss = true };
    }

    // A boss's own flat ground mark, for the heavy blows nothing else draws (the Arcade Alphas' slam). Call after BossBegin.
    public static void GroundMark(Enemy e, Vector3 at, float diameter, float seconds) {
        Tell t;
        if (e != null && Tells.TryGetValue(e.GetInstanceID(), out t) && t.boss) {
            t.point = at;
            t.diameter = diameter;
            t.seconds = seconds;
            if (t.mark == null) {
                t.mark = Take();
            }
        }
    }

    // Called where a bot's blow reaches Slice or Wallit: a blow with no tell running is counted.
    public static void NoteHit(Enemy by) {
        if (by == null || Exempt) {
            return;
        }
        Tell t;
        if (Tells.TryGetValue(by.GetInstanceID(), out t) && (t.active || Time.time - t.landedAt < 0.5f)) {
            return;
        }
        Untelegraphed++;
        Events.Add("untelegraphed|" + by.Title + "|" + Time.time.ToString("0.0"));
    }

    static void Drop1(Enemy e) {
        Tell old;
        if (Tells.TryGetValue(e.GetInstanceID(), out old)) {
            Release(old);
            Tells.Remove(e.GetInstanceID());
        }
    }

    static Mark Take() {
        for (int i = 0; i < Pool.Count; i++) {
            if (Pool[i].root != null && !Pool[i].root.activeSelf) {
                Pool[i].root.SetActive(true);
                return Pool[i];
            }
        }
        var m = new Mark();
        var shader = SafeShader.Find("Sprites/Default");
        m.root = new GameObject("Tell Mark");
        var rim = KitMesh.Disc("Tell Rim");
        rim.transform.SetParent(m.root.transform, false);
        rim.transform.localScale = new Vector3(1f, 0.02f, 1f);
        m.rimRoot = rim.transform;
        var fill = KitMesh.Disc("Tell Fill");
        m.fill = fill.transform;
        m.fill.SetParent(m.root.transform, false);
        m.fill.localPosition = new Vector3(0f, 0.02f, 0f);
        if (shader != null) {
            m.rimMat = new Material(shader);
            m.fillMat = new Material(shader);
            rim.GetComponent<Renderer>().sharedMaterial = m.rimMat;
            fill.GetComponent<Renderer>().sharedMaterial = m.fillMat;
        }
        foreach (Renderer r in m.root.GetComponentsInChildren<Renderer>()) {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        Pool.Add(m);
        return m;
    }

    static void Release(Tell t) {
        if (t.mark != null && t.mark.root != null) {
            t.mark.root.SetActive(false);
        }
        t.mark = null;
    }

    // ---- Slice's circle

    static float nextPulse, threatUntil;

    static void Pulse(Vector3 at) {
        threatUntil = Time.time + 1.3f;
        if (Time.time < nextPulse) {
            return;
        }
        nextPulse = Time.time + 0.7f;
        Pulses++;
        Fx.Ring(at, CircleRadius * 1.6f, new Color(DeptPalette.Red.r, DeptPalette.Red.g, DeptPalette.Red.b, 0.55f), 0.8f);
        Fx.Ring(at, CircleRadius, new Color(DeptPalette.RedLight.r, DeptPalette.RedLight.g, DeptPalette.RedLight.b, 0.4f), 0.55f);
        Events.Add("pulse|" + Time.time.ToString("0.0"));
    }

    float scanAt;

    // ---- the tint

    Canvas canvas;
    RectTransform edge;
    Image edgeImage;
    static Sprite blob;

    static Sprite Blob() {
        if (blob == null) {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < n; y++) {
                for (int x = 0; x < n; x++) {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a * (3f - 2f * a);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            blob = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }
        return blob;
    }

    void Awake() {
        var go = new GameObject("Slice Edge Tint");
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3;   // under every panel, pad and bar
        var img = new GameObject("Edge");
        img.transform.SetParent(go.transform, false);
        edgeImage = img.AddComponent<Image>();
        edgeImage.sprite = Blob();
        edgeImage.raycastTarget = false;
        edge = edgeImage.rectTransform;
        edge.anchorMin = edge.anchorMax = Vector2.zero;
        edgeImage.color = Color.clear;
    }

    void Update() {
        if (PauseMenu.IsOpen) {
            return;
        }
        Musician slice = Musician.Instance;
        // Bots that have just come inside Slice's circle: pulse once each.
        if (slice != null && slice.Alive && Time.time >= scanAt) {
            scanAt = Time.time + 0.2f;
            for (int i = 0; i < Enemy.Roster.Count; i++) {
                Enemy e = Enemy.Roster[i];
                if (e == null || !e.Alive) {
                    continue;
                }
                Vector3 d = e.transform.position - slice.transform.position;
                d.y = 0f;
                if (d.magnitude <= CircleRadius && Reached.Add(e.GetInstanceID())) {
                    Reaches++;
                    Pulse(slice.transform.position);
                }
            }
        }
        TickTells();
        TickEdge(slice);
    }

    void TickTells() {
        Drop.Clear();
        foreach (var kv in Tells) {
            Tell t = kv.Value;
            if (t.enemy == null) {
                Release(t);
                Drop.Add(kv.Key);
                continue;
            }
            if (!t.active) {
                if (Time.time - t.landedAt > 1f) {
                    Drop.Add(kv.Key);
                }
                continue;
            }
            float k = t.seconds > 0f ? Mathf.Clamp01((Time.time - t.start) / t.seconds) : 0f;
            if (!t.boss && Time.time - t.start > t.seconds + 0.6f) {
                t.active = false;   // the wind-up was broken (shoved, killed, stepped away) and nothing landed
                t.landedAt = -10f;
                Release(t);
                continue;
            }
            if (t.onSlice) {
                threatUntil = Mathf.Max(threatUntil, Time.time + 0.3f);
            }
            // The glow on the claw or leg.
            if (!t.boss && t.limb != null && Time.time >= t.glowAt) {
                t.glowAt = Time.time + GlowEvery;
                float s = (0.45f + 0.9f * k) * (UiFit.Touch ? 1.3f : 1f) * (t.enemy.IsSpiderFamily ? 1.3f : 1f);
                Fx.Emit(FxKind.Flash, t.limb.position, 1, new Color(DeptPalette.Red.r * 1.2f, DeptPalette.Red.g, DeptPalette.Red.b, 0.35f + 0.5f * k), s, 0f, 0.1f, 0.14f);
            }
            // The ground mark.
            if (t.mark != null && t.diameter > 0f) {
                Vector3 at = t.point;
                if (!t.boss) {
                    at = t.target != null ? t.target.position : t.enemy.transform.position + t.enemy.transform.forward * 1.2f;
                    at.y = t.enemy.transform.position.y;
                }
                PaintMark(t.mark, at, t.diameter, k);
            }
        }
        for (int i = 0; i < Drop.Count; i++) {
            Tells.Remove(Drop[i]);
        }
    }

    static void PaintMark(Mark m, Vector3 at, float diameter, float k) {
        m.root.transform.position = new Vector3(at.x, at.y + MarkY, at.z);
        m.rimRoot.localScale = new Vector3(diameter, 0.02f, diameter);
        float f = diameter * Mathf.Lerp(0.15f, 1f, k);
        m.fill.localScale = new Vector3(f, 0.025f, f);
        if (m.rimMat != null) {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 18f);
            m.rimMat.color = new Color(DeptPalette.Red.r, DeptPalette.Red.g, DeptPalette.Red.b, 0.3f + 0.25f * pulse);
        }
        if (m.fillMat != null) {
            m.fillMat.color = new Color(DeptPalette.Red.r, DeptPalette.Red.g * 0.8f, DeptPalette.Red.b * 0.8f, 0.3f + 0.4f * k);
        }
    }

    void TickEdge(Musician slice) {
        bool want = slice != null && slice.Alive && Time.time < threatUntil && Camera.main != null;
        Vector3 vp = Vector3.zero;
        bool off = false;
        if (want) {
            vp = Camera.main.WorldToViewportPoint(slice.transform.position + Vector3.up * 1f);
            off = vp.z < 0f || vp.x < 0.03f || vp.x > 0.97f || vp.y < 0.03f || vp.y > 0.97f;
        }
        if (!off) {
            EdgeVisible = false;
            edgeImage.color = Color.Lerp(edgeImage.color, Color.clear, Mathf.Clamp01(Time.unscaledDeltaTime * 12f));
            return;
        }
        if (!EdgeVisible) {
            EdgeVisible = true;
            EdgeShows++;
            Events.Add("edge|" + Time.time.ToString("0.0"));
        }
        float W = Screen.width, H = Screen.height;
        Vector2 d = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
        if (vp.z < 0f) {
            d = -d;   // behind the camera: she is the other way
        }
        if (d.sqrMagnitude < 0.0001f) {
            d = Vector2.down;
        }
        float depth = H * 0.12f;
        float len = Mathf.Min(H * 0.30f, W * 0.40f);
        float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y);
        Vector2 p;
        Vector2 size;
        float padTop = H * 0.38f;   // the stick and the Swing pad sit in the bottom corners: side tints stay above them
        if (ax >= ay) {
            float y = 0.5f + d.y * (0.5f / ax);
            float yp = Mathf.Clamp(y * H, padTop + len * 0.5f, H - len * 0.5f - H * 0.08f);
            p = new Vector2(d.x > 0f ? W - depth * 0.3f : depth * 0.3f, yp);
            size = new Vector2(depth, len);
        } else {
            float x = 0.5f + d.x * (0.5f / ay);
            float xp = x * W;
            if (d.y < 0f) {
                len = Mathf.Min(len, W * 0.22f);   // the bottom corners belong to the pads
                xp = Mathf.Clamp(xp, W * 0.42f, W * 0.58f);
                p = new Vector2(xp, depth * 0.3f);
            } else {
                xp = Mathf.Clamp(xp, len * 0.5f, W - len * 0.5f);
                p = new Vector2(xp, H - depth * 0.3f);
            }
            size = new Vector2(len, depth);
        }
        edge.sizeDelta = size;
        edge.anchoredPosition = p;
        EdgeRect = new Rect(Mathf.Max(0f, p.x - size.x * 0.5f), Mathf.Max(0f, p.y - size.y * 0.5f),
            Mathf.Min(W, size.x), Mathf.Min(H, size.y));
        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 9f);
        edgeImage.color = new Color(DeptPalette.Red.r, DeptPalette.Red.g, DeptPalette.Red.b, 0.8f * pulse);
    }

    // For the playtest.
    public static float MinLeadMs { get { return Min(LeadsMs); } }
    public static float MaxLeadMs { get { return Max(LeadsMs); } }
    public static float MinBossLeadMs { get { return Min(BossLeadsMs); } }
    static float Min(List<float> l) { float m = float.MaxValue; for (int i = 0; i < l.Count; i++) m = Mathf.Min(m, l[i]); return l.Count > 0 ? m : 0f; }
    static float Max(List<float> l) { float m = 0f; for (int i = 0; i < l.Count; i++) m = Mathf.Max(m, l[i]); return m; }
}
