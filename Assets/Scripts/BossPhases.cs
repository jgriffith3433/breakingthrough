using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Two phases for the chapter bosses that have no fight of their own (Docs/Story.md, "Boss phases"): the Plated Alpha, the
// Foreman, the Registrar and the Order, and the Server once its three refusals are spent. (The Courier and the Server's
// refusals are their own scripts.) Every attack is telegraphed: red marks appear on the ground, fill over the wind-up while the
// boss stands still and rings pulse, and the blow lands only when the fill reaches the rim. Move out of a mark and it misses;
// hit the boss for InterruptShare of its health before it fills and the wind-up breaks. At PhaseAt (half health; 0.4 for the
// Server) the phase changes on screen: a red flash, a banner, a ring and a new tint, the boss speeds up and the pattern changes.
public class BossPhases : MonoBehaviour {
    public enum Fight { Plated, Foreman, Registrar, Order, Server }
    enum Shape { Stamp, March, Page }     // a mark on Wallit; three marks marching at Slice; a mark under Slice

    public const float InterruptShare = 0.08f;
    const float StampRadius = 3f;
    const float PageRadius = 3.4f;
    const float MarchRadius = 2.6f;
    const int WallitDamage = 1;
    const int SliceDamage = 10;

    // What the fight did, for the playtest: "phase1|<title>", "tell|<title>|<phase>|<attack>|<seconds>", "land|<attack>|<who>",
    // "dodged|<attack>", "interrupt|<attack>", "phase2|<title>|<health>".
    public static readonly List<string> Events = new List<string>();
    public static int Tells, Lands, Dodges, Interrupts;
    public static bool MarkUp { get; private set; }

    class Mark {
        public Vector3 at;
        public float radius;
        public bool onSlice;
        public GameObject root;
        public GameObject fill;
        public Material rimMat, fillMat;
    }

    Enemy enemy;
    Fight fight;
    int phase = 1;
    float nextAttack = 3f;
    float clock, windTime, healthAtWind;
    bool winding;
    string attackName = "";
    float nextRing;
    readonly List<Mark> marks = new List<Mark>();
    bool toldTip;

    public void Setup(Fight which) {
        fight = which;
        enemy = GetComponent<Enemy>();
        if (enemy != null && enemy.Phase >= 2) {
            phase = 2;
        }
    }

    void Awake() {
        Events.Clear();
        Tells = Lands = Dodges = Interrupts = 0;
        MarkUp = false;
    }

    void Start() {
        if (enemy == null) {
            enemy = GetComponent<Enemy>();
        }
        Events.Add("phase1|" + (enemy != null ? enemy.Title : fight.ToString()));
    }

    void OnDestroy() {
        DropMarks();
    }

    string Who { get { return enemy != null ? enemy.Title.Replace("Alpha ", "") : "The boss"; } }

    void Update() {
        if (enemy == null || !enemy.Alive) {
            DropMarks();
            return;
        }
        Musician slice = Musician.Instance;
        if (slice == null || !slice.Alive || PauseMenu.IsOpen || Cutscene.IsOpen || LevelUpScreen.IsOpen) {
            return;
        }
        if (phase == 1 && enemy.Health01 <= enemy.PhaseAt) {
            ChangePhase();
        }
        float dt = Time.deltaTime;
        if (winding) {
            Wind(slice, dt);
            return;
        }
        nextAttack -= dt;
        Vector3 to = slice.transform.position - transform.position;
        to.y = 0f;
        if (nextAttack <= 0f && to.magnitude <= 14f) {
            BeginAttack(slice);
        }
    }

    // The phase change, shown: flash over the screen, a banner naming the boss and what is different, a ring, a new tint.
    void ChangePhase() {
        phase = 2;
        enemy.EnterPhase2(PhaseTint());
        Events.Add("phase2|" + enemy.Title + "|" + enemy.Health01.ToString("0.00"));
        CancelWind();
        enemy.Hold(1.4f);
        nextAttack = 1.8f;
        Announce(enemy, Who + ": " + PhaseLine());
        Fx.Ring(transform.position, enemy.BodyScale * 4.5f, new Color(1f, 0.2f, 0.12f, 0.9f), 0.8f);
        Fx.Emit(FxKind.Flash, transform.position + Vector3.up * 1.4f, 1, new Color(1f, 0.4f, 0.3f, 0.8f), 3.2f, 0f);
        GameAudio.Play("stomp", 1f, 0.5f);
    }

    // The on-screen change, shared with the Courier and the Server's own scripts: a red wash that fades, and the banner.
    public static void Announce(Enemy boss, string line) {
        UpgradeLook.Say("PHASE TWO - " + line, 3.4f, 240f);
        var go = new GameObject("Phase Flash");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 190;
        var img = new GameObject("Wash").AddComponent<Image>();
        img.transform.SetParent(go.transform, false);
        img.raycastTarget = false;
        img.rectTransform.anchorMin = Vector2.zero;
        img.rectTransform.anchorMax = Vector2.one;
        img.rectTransform.offsetMin = Vector2.zero;
        img.rectTransform.offsetMax = Vector2.zero;
        img.color = new Color(0.9f, 0.1f, 0.08f, 0.35f);
        go.AddComponent<PhaseWash>().Wash = img;
    }

    class PhaseWash : MonoBehaviour {
        public Image Wash;
        float t;
        void Update() {
            t += Time.unscaledDeltaTime;
            Wash.color = new Color(0.9f, 0.1f, 0.08f, Mathf.Max(0f, 0.35f * (1f - t / 0.8f)));
            if (t >= 0.8f) {
                Destroy(gameObject);
            }
        }
    }

    Color PhaseTint() {
        switch (fight) {
            case Fight.Plated: return new Color(1.4f, 0.6f, 0.55f);
            case Fight.Foreman: return new Color(1.4f, 0.8f, 0.4f);
            case Fight.Registrar: return new Color(1.4f, 1.1f, 0.4f);
            case Fight.Order: return new Color(1.3f, 0.5f, 0.5f);
            default: return new Color(0.7f, 1.2f, 1.4f);
        }
    }

    string PhaseLine() {
        switch (fight) {
            case Fight.Plated: return "plates off, stamps twice at once";
            case Fight.Foreman: return "overtime, the order comes down the line, then at Slice";
            case Fight.Registrar: return "pages turn faster, stamps beside the filing";
            case Fight.Order: return "the seal is broken, it marches and calls bots";
            default: return "the paper is spent, it serves in earnest";
        }
    }

    Shape[] Pattern() {
        switch (fight) {
            case Fight.Plated: return phase == 1 ? new[] { Shape.Stamp } : new[] { Shape.Stamp, Shape.Stamp };
            case Fight.Foreman: return phase == 1 ? new[] { Shape.March } : new[] { Shape.March, Shape.Page };
            case Fight.Registrar: return phase == 1 ? new[] { Shape.Page } : new[] { Shape.Page, Shape.Stamp };
            case Fight.Order: return phase == 1 ? new[] { Shape.Stamp } : new[] { Shape.March };
            default: return phase == 1 ? new[] { Shape.Stamp } : new[] { Shape.March };
        }
    }

    static string NameOf(Shape s) { return s == Shape.Stamp ? "stamp" : s == Shape.March ? "march" : "page"; }

    void BeginAttack(Musician slice) {
        Shape[] pattern = Pattern();
        windTime = phase == 1 ? 2.2f : 1.5f;
        winding = true;
        clock = 0f;
        healthAtWind = enemy.Health01;
        attackName = "";
        PlayerGrowth player = PlayerGrowth.Instance;
        Vector3 sliceAt = GroundAt(slice.transform.position);
        Vector3 wallitAt = player != null ? GroundAt(player.transform.position) : sliceAt;
        for (int i = 0; i < pattern.Length; i++) {
            attackName += (i > 0 ? "+" : "") + NameOf(pattern[i]);
            switch (pattern[i]) {
                case Shape.Stamp:
                    // One mark on Wallit; a second stamp lands a step to the side of him so standing still is wrong either way.
                    Vector3 stampAt = i == 0 ? wallitAt : wallitAt + Quaternion.Euler(0f, 90f, 0f) * (sliceAt - wallitAt).normalized * 4.5f;
                    AddMark(stampAt, StampRadius, false);
                    break;
                case Shape.March:
                    Vector3 from = GroundAt(transform.position);
                    for (int k = 1; k <= 3; k++) {
                        AddMark(Vector3.Lerp(from, sliceAt, k / 3.2f), MarchRadius, k == 3);
                    }
                    break;
                default:
                    AddMark(sliceAt, PageRadius, true);
                    break;
            }
        }
        Tells++;
        Events.Add("tell|" + enemy.Title + "|" + phase + "|" + attackName + "|" + windTime.ToString("0.0"));
        enemy.Hold(windTime + 0.3f);
        MarkUp = true;
        nextRing = 0f;
        GameAudio.Play("stomp", 0.7f, phase == 1 ? 1.2f : 0.9f);
        Fx.Emit(FxKind.Flash, transform.position + Vector3.up * 1.2f, 1, new Color(1f, 0.5f, 0.4f, 0.5f), 2f, 0f);
        if (!toldTip) {
            toldTip = true;
            UpgradeLook.Say("Slice: \"Red is where it lands. Move, or break it.\"", 3.2f, 230f);
        }
        if (phase == 2 && fight == Fight.Order && WaveDirector.Instance != null) {
            WaveDirector.Instance.BossCall(transform.position, 2);
        }
    }

    void Wind(Musician slice, float dt) {
        clock += dt;
        enemy.Hold(windTime - clock + 0.3f);
        Vector3 to = slice.transform.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f) {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), dt * 6f);
        }
        float t = Mathf.Clamp01(clock / windTime);
        for (int i = 0; i < marks.Count; i++) {
            Paint(marks[i], t);
        }
        if (Time.time >= nextRing) {
            nextRing = Time.time + 0.55f;
            for (int i = 0; i < marks.Count; i++) {
                Fx.Ring(marks[i].at, marks[i].radius * 2f * (0.5f + 0.5f * t), new Color(1f, 0.25f, 0.18f, 0.5f + 0.4f * t), 0.5f);
            }
        }
        if (enemy.Phase == phase && healthAtWind - enemy.Health01 >= InterruptShare) {
            Interrupts++;
            Events.Add("interrupt|" + attackName);
            CancelWind();
            enemy.Hold(2f);
            nextAttack = 3.2f;
            Fx.Ring(transform.position, 6f, new Color(1f, 1f, 1f, 0.8f), 0.5f);
            GameAudio.Play("batcrack", 0.8f, 0.8f);
            UpgradeLook.Say("Wind-up broken!", 1.8f, 160f);
            return;
        }
        if (clock >= windTime) {
            Land(slice);
        }
    }

    void Land(Musician slice) {
        PlayerGrowth player = PlayerGrowth.Instance;
        bool sliceHit = false, wallitHit = false;
        for (int i = 0; i < marks.Count; i++) {
            Mark m = marks[i];
            Fx.Ring(m.at, m.radius * 2.4f, new Color(1f, 0.35f, 0.2f, 0.9f), 0.5f);
            Fx.Emit(FxKind.Dust, m.at + Vector3.up * 0.3f, 10, new Color(0.6f, 0.55f, 0.48f, 0.6f), 1.2f, 2.2f, 1.8f);
            if (player != null) {
                Vector3 d = player.transform.position - m.at;
                d.y = 0f;
                if (d.magnitude <= m.radius) {
                    wallitHit = true;
                }
            }
            if (m.onSlice) {
                Vector3 d = slice.transform.position - m.at;
                d.y = 0f;
                if (d.magnitude <= m.radius + 0.5f) {
                    sliceHit = true;
                }
            }
        }
        GameAudio.Play("stamp", 0.9f, 0.9f);
        if (wallitHit && player != null) {
            Lands++;
            Events.Add("land|" + attackName + "|wallit");
            player.OnHit(enemy);
        }
        if (sliceHit && !wallitHit) {
            Lands++;
            Events.Add("land|" + attackName + "|slice");
            slice.Hurt(SliceDamage, enemy);
        } else if (!wallitHit) {
            Dodges++;
            Events.Add("dodged|" + attackName);
        }
        DropMarks();
        winding = false;
        enemy.Hold(phase == 1 ? 1.2f : 0.6f);
        nextAttack = phase == 1 ? 4.5f : 3f;
    }

    void CancelWind() {
        DropMarks();
        winding = false;
    }

    static Vector3 GroundAt(Vector3 p) {
        if (Physics.Raycast(new Vector3(p.x, p.y + 6f, p.z), Vector3.down, out RaycastHit hit, 20f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            p.y = hit.point.y;
        }
        return p;
    }

    void AddMark(Vector3 at, float radius, bool onSlice) {
        var shader = SafeShader.Find("Sprites/Default");
        var m = new Mark { at = at, radius = radius, onSlice = onSlice };
        m.root = new GameObject("Boss Mark");
        m.root.transform.position = at;
        var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rim.name = "Mark";
        Destroy(rim.GetComponent<Collider>());
        rim.transform.SetParent(m.root.transform, false);
        rim.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
        rim.transform.localPosition = new Vector3(0f, 0.07f, 0f);
        m.fill = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        m.fill.name = "Mark Fill";
        Destroy(m.fill.GetComponent<Collider>());
        m.fill.transform.SetParent(m.root.transform, false);
        m.fill.transform.localScale = new Vector3(0.1f, 0.025f, 0.1f);
        m.fill.transform.localPosition = new Vector3(0f, 0.09f, 0f);
        if (shader != null) {
            m.rimMat = new Material(shader) { color = new Color(1f, 0.2f, 0.15f, 0.22f) };
            m.fillMat = new Material(shader) { color = new Color(1f, 0.3f, 0.2f, 0.4f) };
            rim.GetComponent<Renderer>().material = m.rimMat;
            m.fill.GetComponent<Renderer>().material = m.fillMat;
        }
        foreach (Renderer r in m.root.GetComponentsInChildren<Renderer>()) {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        marks.Add(m);
    }

    static void Paint(Mark m, float t) {
        float r = m.radius * 2f * Mathf.Lerp(0.08f, 1f, t);
        m.fill.transform.localScale = new Vector3(r, 0.025f, r);
        if (m.rimMat != null) {
            m.rimMat.color = new Color(1f, 0.2f, 0.15f, 0.18f + 0.18f * Mathf.PingPong(Time.time * 3f, 1f));
        }
        if (m.fillMat != null) {
            m.fillMat.color = new Color(1f, 0.3f, 0.2f, 0.3f + 0.35f * t);
        }
    }

    void DropMarks() {
        MarkUp = false;
        for (int i = 0; i < marks.Count; i++) {
            if (marks[i].root != null) {
                Destroy(marks[i].root);
            }
        }
        marks.Clear();
    }
}
