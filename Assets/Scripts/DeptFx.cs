using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The Department's hit effects, layered on top of Fx: torn paper, stamp-ink flecks, soft spark streaks, speed lines,
// the STRUCK OFF stamp on a dying bot, a Spider Bot's scattered legs, the coin glint and its arc to the HUD, and the
// ripple through Slice's circle. Everything is pooled: one ParticleSystem per look (all sharing one atlas texture, a
// row of it each through the material's tiling), a fixed set of stamp objects and a fixed set of HUD coin flyers.
// Counts are scaled by Tier (half on a phone). It never touches hit-stop, shake or the damage flash.
// The atlas is drawn in code (Atlas()); a Gen-made sheet can replace it by dropping Resources/Fx/DeptAtlas.png in.
public class DeptFx : MonoBehaviour {
    static DeptFx instance;
    static Texture2D atlas;
    static Texture2D smear;

    // What the playtest reads.
    public static int Bursts { get; private set; }
    public static int PaperEmitted { get; private set; }
    public static int InkEmitted { get; private set; }
    public static int Stamps { get; private set; }
    public static int StampsBeforeBreak { get; private set; }
    public static int SpeedLineBursts { get; private set; }
    public static int Landings { get; private set; }
    public static int LegScatters { get; private set; }
    public static int Ripples { get; private set; }
    public static int CoinGlints { get; private set; }
    public static int CoinArcs { get; private set; }
    public static int Peak { get; private set; }

    // Phone tier: half the particles.
    public static float Tier { get { return UiFit.Touch ? 0.5f : 1f; } }

    // Atlas rows (bottom to top): 0 paper scraps, 1 ink splats, 2 spark streaks, 3 stamp frame / speed line / glint / leg.
    enum Look { Paper, Ink, Spark, Speed, Glint, Leg }

    readonly Dictionary<Look, ParticleSystem> systems = new Dictionary<Look, ParticleSystem>();

    class Stamp {
        public Transform root;
        public TextMesh text;
        public MeshRenderer textRenderer;
        public MeshRenderer frame;
        public Material frameMaterial;
        public Mesh frameMesh;
        public bool busy;
        public float baseWidth;
    }
    readonly List<Stamp> stamps = new List<Stamp>();
    const int StampPool = 8;

    class Flyer {
        public RectTransform rect;
        public RawImage image;
        public Vector2 from, to;
        public float age, life;
        public bool busy;
    }
    readonly List<Flyer> flyers = new List<Flyer>();
    Canvas flyCanvas;
    float nextFlyAt;

    public static bool Ready { get { return instance != null; } }

    // Added by Fx.Awake, so it lives and dies with the effects object.
    public static void Attach(GameObject host) {
        if (host.GetComponent<DeptFx>() == null) {
            host.AddComponent<DeptFx>();
        }
    }

    void Awake() {
        instance = this;
        Make(Look.Paper, 0, 0, 3, false, 260, 1.5f, 0.8f, false);
        Make(Look.Ink, 1, 0, 3, false, 200, 1.1f, 1.1f, false);
        Make(Look.Spark, 2, 0, 3, true, 160, 0.3f, 0.2f, true);
        Make(Look.Speed, 3, 1, 1, true, 120, 0.3f, 0f, true);
        Make(Look.Glint, 3, 2, 2, true, 60, 0.4f, 0f, false);
        Make(Look.Leg, 3, 3, 3, false, 60, 1.4f, 1.2f, false);
        for (int i = 0; i < StampPool; i++) {
            stamps.Add(MakeStamp());
        }
    }

    void OnDestroy() {
        if (instance == this) {
            instance = null;
        }
    }

    void Make(Look look, int row, int colLo, int colHi, bool additive, int max, float life, float gravity, bool stretched) {
        var go = new GameObject("DeptFx " + look);
        go.transform.SetParent(transform, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = life;
        main.gravityModifier = gravity;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            additive
                ? new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.4f), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = fade;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, additive
            ? new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f))
            : new AnimationCurve(new Keyframe(0f, 0.9f), new Keyframe(1f, 0.6f)));

        if (!additive) {
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-5f, 5f);
        }
        if (look == Look.Paper) {
            // Paper drifts: it flutters down slowly instead of falling like a plate.
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.1f;
            limit.limit = 2.5f;
        }

        // The sheet: four columns picked at random per particle; the row is chosen by the material's tiling.
        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = 4;
        sheet.numTilesY = 1;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
        sheet.startFrame = new ParticleSystem.MinMaxCurve(colLo / 4f, (colHi + 1) / 4f - 0.01f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("BT/Particle");
        var material = new Material(shader != null ? shader : SafeShader.Find("Sprites/Default"));
        material.mainTexture = Atlas();
        material.mainTextureScale = new Vector2(1f, 0.25f);
        material.mainTextureOffset = new Vector2(0f, row * 0.25f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_Boost", additive ? 1.5f : 1f);
        material.renderQueue = additive ? 3110 : 3060;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortMode = ParticleSystemSortMode.None;
        if (stretched) {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = look == Look.Speed ? 0.12f : 0.07f;
            renderer.lengthScale = look == Look.Speed ? 3f : 1.4f;
        } else {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }
        renderer.maxParticleSize = 3f;
        ps.Play();
        systems[look] = ps;
    }

    void Update() {
        int live = 0;
        foreach (KeyValuePair<Look, ParticleSystem> pair in systems) {
            if (pair.Value != null) {
                live += pair.Value.particleCount;
            }
        }
        if (live > Peak) {
            Peak = live;
        }
        TickFlyers(Time.unscaledDeltaTime);
    }

    // ---------- emit ----------

    static void Emit(Look look, Vector3 at, int count, Color color, float size, float speed, Vector3 bias, float spread = 0.2f, float life = -1f) {
        if (instance == null || count <= 0) {
            return;
        }
        ParticleSystem ps;
        if (!instance.systems.TryGetValue(look, out ps) || ps == null) {
            return;
        }
        var p = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++) {
            Vector3 dir = Random.insideUnitSphere;
            dir.y = Mathf.Abs(dir.y) * 0.8f + 0.15f;
            p.position = at + Random.insideUnitSphere * spread;
            p.velocity = (dir.normalized + bias).normalized * speed * Random.Range(0.45f, 1.1f);
            p.startSize = size * Random.Range(0.75f, 1.25f);
            p.startColor = color;
            p.rotation = Random.Range(0f, 360f);
            if (life > 0f) {
                p.startLifetime = life * Random.Range(0.8f, 1.2f);
            } else {
                p.ResetStartLifetime();
            }
            ps.Emit(p, 1);
        }
    }

    static int Scaled(float n) {
        return Mathf.Max(1, Mathf.RoundToInt(n * Tier));
    }

    static readonly Color Paper = new Color(0.96f, 0.93f, 0.82f, 1f);
    static readonly Color Ink = new Color(0.72f, 0.1f, 0.1f, 1f);
    static readonly Color Navy = new Color(0.1f, 0.12f, 0.3f, 1f);

    // A bat hit: spark streaks, torn paper and stamp-ink flecks. weight 0 light, 1 finisher or sweet spot, 2 home run.
    public static void Impact(Vector3 at, float botScale, int weight, Vector3 away) {
        if (instance == null) {
            return;
        }
        Bursts++;
        float root = Mathf.Clamp(botScale, 0.8f, 2.5f);
        Vector3 bias = (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.zero) * 0.7f;
        int w = Mathf.Clamp(weight, 0, 2);
        int paper = Scaled(4 + w * 4);
        int ink = Scaled(3 + w * 3);
        PaperEmitted += paper;
        InkEmitted += ink;
        Emit(Look.Spark, at, Scaled(4 + w * 3), new Color(1f, 0.92f, 0.65f, 1f), (0.45f + 0.15f * w) * root, 5f + 2f * w, bias, 0.1f);
        Emit(Look.Paper, at, paper, Paper, (0.34f + 0.08f * w) * root, 3.4f + 1.4f * w, bias, 0.15f);
        Emit(Look.Ink, at, ink, w == 0 ? Ink : Color.Lerp(Ink, Navy, 0.35f * w), (0.22f + 0.06f * w) * root, 3f + 1.2f * w, bias, 0.1f);
    }

    // The launch: a heavy paper burst and a shockwave ring at the contact point.
    public static void HomeRunLaunch(Vector3 at, float botScale, Vector3 away) {
        if (instance == null) {
            return;
        }
        Impact(at, botScale, 2, away);
        Emit(Look.Paper, at, Scaled(10), Paper, 0.5f * Mathf.Clamp(botScale, 0.8f, 2.5f), 7f, away.normalized * 0.5f, 0.3f);
        PaperEmitted += Scaled(10);
        Fx.Ring(at - Vector3.up * 0.6f, 9f * Mathf.Clamp(botScale, 1f, 2.5f), new Color(1f, 1f, 0.92f, 0.9f), 0.5f);
    }

    // Speed lines behind a launched bot, called each frame while it flies. heading is its direction of travel.
    public static void SpeedLines(Vector3 at, Vector3 heading, float botScale) {
        if (instance == null || heading.sqrMagnitude < 0.01f) {
            return;
        }
        SpeedLineBursts++;
        Vector3 back = -heading.normalized;
        float root = Mathf.Clamp(botScale, 0.8f, 2.5f);
        ParticleSystem ps;
        if (!instance.systems.TryGetValue(Look.Speed, out ps) || ps == null) {
            return;
        }
        var p = new ParticleSystem.EmitParams();
        int n = Scaled(3);
        for (int i = 0; i < n; i++) {
            p.position = at + Random.insideUnitSphere * 0.5f * root;
            p.velocity = back * Random.Range(7f, 12f);
            p.startSize = Random.Range(0.5f, 0.8f) * root;
            p.startColor = new Color(1f, 0.97f, 0.85f, 0.9f);
            p.rotation = 0f;
            p.startLifetime = Random.Range(0.22f, 0.34f);
            ps.Emit(p, 1);
        }
    }

    // Where a home run bot comes down: a big distant-looking flash, a dust puff and a few scraps.
    public static void Landing(Vector3 at, float botScale) {
        Landings++;
        float root = Mathf.Clamp(botScale, 0.8f, 2.5f);
        Fx.Emit(FxKind.Flash, at + Vector3.up * 0.8f, 2, new Color(1f, 0.95f, 0.7f, 1f), 5.5f * root, 0f);
        Fx.Emit(FxKind.Dust, at + Vector3.up * 0.2f, Scaled(10), new Color(0.85f, 0.78f, 0.64f, 0.8f), 1.8f * root, 3.2f, 1.4f);
        Fx.Ring(at + Vector3.up * 0.1f, 8f * root, new Color(1f, 0.95f, 0.75f, 0.8f), 0.6f);
        Emit(Look.Paper, at + Vector3.up * 0.3f, Scaled(8), Paper, 0.4f * root, 4.5f, Vector3.zero, 0.3f);
        PaperEmitted += Scaled(8);
    }

    // A bot coming apart, after its stamp: scraps of paper fold out of it; a Spider Bot also scatters its legs.
    static void Scraps(Vector3 at, float botScale, bool big, bool spider, Color paint) {
        float root = Mathf.Clamp(botScale, 0.8f, 3f);
        int paper = Scaled(big ? 16 : 9);
        PaperEmitted += paper;
        InkEmitted += Scaled(4);
        Emit(Look.Paper, at, paper, Paper, 0.42f * root, 3.6f, Vector3.zero, 0.3f * root);
        Emit(Look.Ink, at, Scaled(4), Ink, 0.28f * root, 2.8f, Vector3.zero, 0.2f * root);
        if (spider) {
            LegScatters++;
            Emit(Look.Leg, at + Vector3.up * 0.3f * root, Scaled(big ? 8 : 6), Color.Lerp(paint, Color.white, 0.25f), 0.85f * root, 5.5f, Vector3.zero, 0.3f * root, 1.3f);
        }
    }

    // The death: a red STRUCK OFF stamp slams on, holds a beat, then the bot folds into paper scraps and the usual
    // death burst. Falls back to the plain burst when there is nowhere to run it.
    public static void Death(Enemy enemy, Vector3 at, float scale, Color paint, bool big, bool spider) {
        if (instance == null || enemy == null) {
            Fx.Death(at, scale, paint, big);
            return;
        }
        instance.StartCoroutine(instance.DeathRoutine(enemy, at, scale, paint, big, spider));
    }

    IEnumerator DeathRoutine(Enemy enemy, Vector3 at, float scale, Color paint, bool big, bool spider) {
        Stamp stamp = null;
        int limit = UiFit.Touch ? 3 : StampPool;
        int busy = 0;
        for (int i = 0; i < stamps.Count; i++) {
            if (stamps[i].busy) {
                busy++;
            }
        }
        if (busy < limit) {
            for (int i = 0; i < stamps.Count && stamp == null; i++) {
                if (!stamps[i].busy) {
                    stamp = stamps[i];
                }
            }
        }
        float root = Mathf.Sqrt(Mathf.Max(1f, scale));
        Vector3 spot = at;
        const float Hold = 0.34f;
        if (stamp != null) {
            stamp.busy = true;
            Stamps++;
            float roll = Random.Range(-9f, 9f);
            float far = Camera.main != null ? Mathf.Clamp(Vector3.Distance(Camera.main.transform.position, at) / 16f, 1f, 3f) : 1f;
            float size = root * far;
            stamp.root.gameObject.SetActive(true);
            float t = 0f;
            while (t < Hold + 0.3f) {
                float dt = Time.deltaTime;
                t += dt;
                if (enemy != null) {
                    spot = enemy.transform.position;
                }
                Camera cam = Camera.main;
                Quaternion face = cam != null ? cam.transform.rotation : Quaternion.identity;
                Vector3 toCam = cam != null ? -cam.transform.forward : Vector3.back;
                stamp.root.position = spot + Vector3.up * (1.0f + 0.9f * root) + toCam * 1.2f;
                // Camera rotation, never LookAt: the text reads the right way round.
                stamp.root.rotation = face * Quaternion.Euler(0f, 0f, roll);
                // Slam: big and thin to full size in 0.09 s with a small overshoot, then hold, then fade.
                float slam = Mathf.Clamp01(t / 0.09f);
                float s = slam < 1f ? Mathf.Lerp(2.4f, 1f, slam * slam) : 1f + Mathf.Max(0f, 0.12f * (1f - (t - 0.09f) / 0.12f));
                stamp.root.localScale = Vector3.one * s * size;
                float alpha = t < Hold ? Mathf.Clamp01(t / 0.05f) : Mathf.Clamp01(1f - (t - Hold) / 0.3f);
                stamp.text.color = new Color(0.86f, 0.1f, 0.08f, alpha);
                SetFrameColor(stamp, new Color(0.86f, 0.1f, 0.08f, alpha * 0.9f));
                if (t >= Hold && !broken(stamp)) {
                    Break(stamp, spot, scale, paint, big, spider);
                }
                yield return null;
            }
            stamp.root.gameObject.SetActive(false);
            stamp.busy = false;
            if (!broken(stamp)) {
                Break(stamp, spot, scale, paint, big, spider);
            }
            brokenSet.Remove(stamp);
        } else {
            // No stamp free (a crowd going down at once on a phone): break at once.
            Fx.Death(spot, scale, paint, big);
            Scraps(spot + Vector3.up * 0.4f * scale, root, big, spider, paint);
        }
    }

    readonly HashSet<Stamp> brokenSet = new HashSet<Stamp>();

    bool broken(Stamp s) {
        return brokenSet.Contains(s);
    }

    void Break(Stamp s, Vector3 spot, float scale, Color paint, bool big, bool spider) {
        brokenSet.Add(s);
        StampsBeforeBreak++;
        Fx.Death(spot, scale, paint, big);
        Scraps(spot + Vector3.up * 0.4f * Mathf.Max(1f, scale), Mathf.Sqrt(Mathf.Max(1f, scale)), big, spider, paint);
    }

    Stamp MakeStamp() {
        var s = new Stamp();
        var go = new GameObject("StruckOff Stamp");
        go.transform.SetParent(transform, false);
        s.root = go.transform;
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(s.root, false);
        s.text = textGo.AddComponent<TextMesh>();
        s.text.text = "STRUCK OFF";
        s.text.font = UiSkin.Font(UiSkin.FontRole.Label);
        s.text.fontSize = 64;
        s.text.fontStyle = FontStyle.Bold;
        s.text.characterSize = 0.05f;
        s.text.anchor = TextAnchor.MiddleCenter;
        s.text.alignment = TextAlignment.Center;
        s.text.color = new Color(0.86f, 0.1f, 0.08f, 0f);
        s.textRenderer = textGo.GetComponent<MeshRenderer>();
        if (s.textRenderer != null && s.text.font != null && s.text.font.material != null) {
            s.textRenderer.material = s.text.font.material;
        }
        // Measured once at identity, so the frame fits the words.
        float width = 1.9f;
        float height = 0.5f;
        if (s.textRenderer != null) {
            Bounds b = s.textRenderer.bounds;
            if (b.size.x > 0.1f) {
                width = b.size.x;
                height = Mathf.Max(0.3f, b.size.y);
            }
        }
        s.baseWidth = width;
        // The stamp's rough frame: a quad with the atlas's frame cell, a hair behind the words.
        var frameGo = new GameObject("Frame");
        frameGo.transform.SetParent(s.root, false);
        frameGo.transform.localPosition = new Vector3(0f, 0f, 0.02f);
        frameGo.transform.localScale = new Vector3(width * 1.22f, height * 1.9f, 1f);
        var filter = frameGo.AddComponent<MeshFilter>();
        s.frameMesh = FrameMesh();
        filter.sharedMesh = s.frameMesh;
        s.frame = frameGo.AddComponent<MeshRenderer>();
        Shader shader = Shader.Find("BT/Particle");
        s.frameMaterial = new Material(shader != null ? shader : SafeShader.Find("Sprites/Default"));
        s.frameMaterial.mainTexture = Atlas();
        s.frameMaterial.mainTextureScale = new Vector2(0.25f, 0.25f);
        s.frameMaterial.mainTextureOffset = new Vector2(0f, 0.75f);
        s.frameMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        s.frameMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        s.frameMaterial.renderQueue = 3070;
        s.frame.sharedMaterial = s.frameMaterial;
        s.frame.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        s.frame.receiveShadows = false;
        go.SetActive(false);
        return s;
    }

    // The shader tints by vertex colour, so each stamp owns a quad whose colours carry its fade.
    static void SetFrameColor(Stamp s, Color c) {
        Color32 c32 = c;
        s.frameMesh.colors32 = new[] { c32, c32, c32, c32 };
    }

    static Mesh FrameMesh() {
        var frameMesh = new Mesh { name = "dept_stamp_quad" };
        frameMesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        frameMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        // Wound to face -Z, where the camera-aligned root's text is read from.
        frameMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        frameMesh.RecalculateBounds();
        return frameMesh;
    }

    // ---------- Slice's circle ----------

    // Her circle ripples when a bot lands a blow: two rings out from her feet and a few torn scraps.
    public static void CircleRipple(Vector3 at, float diameter) {
        if (instance == null) {
            return;
        }
        Ripples++;
        instance.StartCoroutine(instance.Ripple(at, diameter));
    }

    IEnumerator Ripple(Vector3 at, float diameter) {
        Color c = new Color(0.95f, 0.3f, 0.25f, 0.85f);
        Fx.Ring(at + Vector3.up * 0.15f, diameter * 0.6f, c, 0.5f);
        Emit(Look.Paper, at + Vector3.up * 1f, Scaled(5), Paper, 0.34f, 2.8f, Vector3.zero, 0.5f);
        PaperEmitted += Scaled(5);
        Emit(Look.Ink, at + Vector3.up * 1f, Scaled(3), Ink, 0.24f, 2.4f, Vector3.zero, 0.4f);
        yield return new WaitForSeconds(0.12f);
        Fx.Ring(at + Vector3.up * 0.15f, diameter, new Color(1f, 0.55f, 0.4f, 0.6f), 0.7f);
        if (!UiFit.Touch) {
            yield return new WaitForSeconds(0.12f);
            Fx.Ring(at + Vector3.up * 0.15f, diameter * 1.35f, new Color(1f, 0.8f, 0.6f, 0.35f), 0.9f);
        }
    }

    // ---------- coins ----------

    // A bolt picked up: a gold glint where it was and a coin that arcs to the HUD counter at the bottom of the screen.
    public static void Coin(Vector3 at) {
        if (instance == null) {
            return;
        }
        CoinGlints++;
        Emit(Look.Glint, at, 1, new Color(1f, 0.88f, 0.4f, 1f), 0.9f, 0.4f, Vector3.up, 0.05f, 0.35f);
        instance.LaunchFlyer(at);
    }

    void LaunchFlyer(Vector3 at) {
        Camera cam = Camera.main;
        if (cam == null || Time.unscaledTime < nextFlyAt) {
            return;
        }
        nextFlyAt = Time.unscaledTime + (UiFit.Touch ? 0.09f : 0.04f);
        Vector3 sp = cam.WorldToScreenPoint(at);
        if (sp.z <= 0f) {
            return;
        }
        EnsureCanvas();
        Flyer f = null;
        int limit = UiFit.Touch ? 5 : 10;
        int busy = 0;
        for (int i = 0; i < flyers.Count; i++) {
            if (flyers[i].busy) {
                busy++;
            } else if (f == null) {
                f = flyers[i];
            }
        }
        if (f == null || busy >= limit) {
            return;
        }
        CoinArcs++;
        f.busy = true;
        f.age = 0f;
        f.life = 0.5f;
        f.from = new Vector2(sp.x, sp.y);
        f.to = new Vector2(Screen.width * 0.5f, Screen.height * 0.04f);
        f.rect.gameObject.SetActive(true);
    }

    void EnsureCanvas() {
        if (flyCanvas != null) {
            return;
        }
        var go = new GameObject("Coin Flyers");
        go.transform.SetParent(transform, false);
        flyCanvas = go.AddComponent<Canvas>();
        flyCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        flyCanvas.sortingOrder = 124;
        for (int i = 0; i < 10; i++) {
            var coin = new GameObject("Coin", typeof(RectTransform), typeof(RawImage));
            coin.transform.SetParent(go.transform, false);
            var f = new Flyer();
            f.rect = (RectTransform)coin.transform;
            f.rect.anchorMin = f.rect.anchorMax = Vector2.zero;
            f.rect.pivot = new Vector2(0.5f, 0.5f);
            f.image = coin.GetComponent<RawImage>();
            f.image.texture = Atlas();
            f.image.uvRect = new Rect(0.5f, 0f, 0.25f, 0.25f);
            f.image.color = new Color(1f, 0.86f, 0.35f, 1f);
            f.image.raycastTarget = false;
            coin.SetActive(false);
            flyers.Add(f);
        }
    }

    void TickFlyers(float dt) {
        for (int i = 0; i < flyers.Count; i++) {
            Flyer f = flyers[i];
            if (!f.busy) {
                continue;
            }
            f.age += dt;
            float u = Mathf.Clamp01(f.age / f.life);
            float e = u * u * (3f - 2f * u);
            Vector2 mid = (f.from + f.to) * 0.5f + Vector2.up * Screen.height * 0.12f;
            Vector2 a = Vector2.Lerp(f.from, mid, e);
            Vector2 b = Vector2.Lerp(mid, f.to, e);
            f.rect.anchoredPosition = Vector2.Lerp(a, b, e);
            float size = Mathf.Max(36f, Screen.height * 0.045f) * Mathf.Lerp(1.2f, 0.6f, u);
            f.rect.sizeDelta = new Vector2(size, size);
            f.rect.localRotation = Quaternion.Euler(0f, 0f, u * 360f);
            if (u >= 1f) {
                f.busy = false;
                f.rect.gameObject.SetActive(false);
            }
        }
    }

    // ---------- textures ----------

    // The swing's smear: soft and white, bright down the middle of the ribbon and fading out along it.
    public static Texture2D Smear() {
        if (smear != null) {
            return smear;
        }
        const int w = 64;
        const int h = 32;
        smear = new Texture2D(w, h, TextureFormat.RGBA32, true);
        smear.wrapMode = TextureWrapMode.Clamp;
        smear.name = "dept_smear";
        var px = new Color[w * h];
        for (int y = 0; y < h; y++) {
            float v = Mathf.Abs((y + 0.5f) / h * 2f - 1f);
            for (int x = 0; x < w; x++) {
                float u = (x + 0.5f) / w;
                float across = Mathf.Pow(1f - v, 1.6f);
                float along = Mathf.Lerp(0.35f, 1f, u);
                float grain = 0.88f + 0.12f * Mathf.PerlinNoise(x * 0.35f, y * 0.5f);
                px[y * w + x] = new Color(1f, 1f, 1f, across * along * grain);
            }
        }
        smear.SetPixels(px);
        smear.Apply(true, true);
        return smear;
    }

    // One shared 256x256 sheet, 4 x 4 cells of 64 px. Bottom row 0: paper scraps; row 1: ink splats; row 2: spark
    // streaks; row 3: stamp frame, speed line, glint, a Spider Bot leg. A Gen-made sheet at Resources/Fx/DeptAtlas wins.
    public static Texture2D Atlas() {
        if (atlas != null) {
            return atlas;
        }
        var made = Resources.Load<Texture2D>("Fx/DeptAtlas");
        if (made != null) {
            atlas = made;
            return atlas;
        }
        const int n = 256;
        const int cell = 64;
        atlas = new Texture2D(n, n, TextureFormat.RGBA32, true);
        atlas.wrapMode = TextureWrapMode.Clamp;
        atlas.name = "dept_atlas";
        var px = new Color[n * n];
        for (int row = 0; row < 4; row++) {
            for (int col = 0; col < 4; col++) {
                for (int y = 0; y < cell; y++) {
                    for (int x = 0; x < cell; x++) {
                        float u = (x + 0.5f) / cell * 2f - 1f;
                        float v = (y + 0.5f) / cell * 2f - 1f;
                        px[(row * cell + y) * n + col * cell + x] = Cell(row, col, u, v, x, y);
                    }
                }
            }
        }
        atlas.SetPixels(px);
        atlas.Apply(true, true);
        return atlas;
    }

    // A blobby outline: radius by angle, wobbled by noise seeded per cell.
    static float Blob(float u, float v, float seed, float baseR, float wobble, float sx, float sy) {
        float uu = u / sx;
        float vv = v / sy;
        float r = Mathf.Sqrt(uu * uu + vv * vv);
        float ang = Mathf.Atan2(vv, uu);
        float edge = baseR + wobble * (Mathf.PerlinNoise(seed + Mathf.Cos(ang) * 1.6f + 3f, seed * 0.7f + Mathf.Sin(ang) * 1.6f + 3f) - 0.5f) * 2f;
        return Mathf.Clamp01((edge - r) * 14f);
    }

    static Color Cell(int row, int col, float u, float v, int x, int y) {
        float seed = row * 7.3f + col * 2.1f;
        float r = Mathf.Sqrt(u * u + v * v);
        switch (row) {
            case 0: {
                // torn paper: a skewed, ragged scrap with faint ruled lines
                float tilt = (col - 1.5f) * 0.35f;
                float ru = u * Mathf.Cos(tilt) - v * Mathf.Sin(tilt);
                float rv = u * Mathf.Sin(tilt) + v * Mathf.Cos(tilt);
                float a = Blob(ru, rv, seed, 0.78f, 0.22f, 0.9f - 0.1f * (col % 2), 0.55f + 0.12f * col);
                float line = (y % 9 == 0) ? 0.82f : 1f;
                float shade = Mathf.Lerp(0.88f, 1f, Mathf.PerlinNoise(x * 0.2f + seed, y * 0.2f));
                return new Color(shade * line, shade * line, shade * line * 0.96f, a);
            }
            case 1: {
                // ink: a splat with a spiky edge and two satellite drops
                float a = Blob(u, v, seed, 0.5f, 0.28f, 1f, 1f);
                float dropA = Mathf.Clamp01((0.13f - Vector2.Distance(new Vector2(u, v), new Vector2(0.62f - col * 0.1f, 0.55f))) * 20f);
                float dropB = Mathf.Clamp01((0.09f - Vector2.Distance(new Vector2(u, v), new Vector2(-0.6f, -0.5f + col * 0.1f))) * 20f);
                a = Mathf.Max(a, Mathf.Max(dropA, dropB));
                return new Color(1f, 1f, 1f, a);
            }
            case 2: {
                // a soft streak: tapering tail, hot head
                float skew = (col - 1.5f) * 0.12f;
                float along = (u + 1f) * 0.5f;
                float width = Mathf.Lerp(0.05f, 0.34f, along) * (1f - 0.2f * col * 0.3f);
                float a = Mathf.Clamp01(1f - Mathf.Abs(v - skew * (1f - along)) / width) * Mathf.Clamp01((0.96f - along) * 12f + 0.2f);
                a = a * a * (3f - 2f * a);
                return new Color(1f, 1f, 1f, a);
            }
            default: {
                if (col == 0) {
                    // the stamp's frame: a rough rectangle outline with a gap-prone inner line
                    float edge = Mathf.Max(Mathf.Abs(u) / 0.96f, Mathf.Abs(v) / 0.94f);
                    float noise = Mathf.PerlinNoise(x * 0.35f, y * 0.35f);
                    float outer = Mathf.Clamp01((0.985f - edge) * 30f);
                    float inner = Mathf.Clamp01((edge - 0.8f) * 30f);
                    float a = outer * inner * Mathf.Lerp(0.55f, 1f, noise);
                    return new Color(1f, 1f, 1f, a);
                }
                if (col == 1) {
                    // a speed line: thin, bright in the middle, tapering to both ends
                    float a = Mathf.Clamp01(1f - Mathf.Abs(v) / (0.12f * (1f - Mathf.Abs(u) * 0.8f) + 0.02f)) * Mathf.Clamp01((1f - Mathf.Abs(u)) * 4f);
                    return new Color(1f, 1f, 1f, a * a);
                }
                if (col == 2) {
                    // a four-point glint
                    float arm = Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(u) * 7f) * Mathf.Clamp01(1f - Mathf.Abs(v)), Mathf.Clamp01(1f - Mathf.Abs(v) * 7f) * Mathf.Clamp01(1f - Mathf.Abs(u)));
                    float core = Mathf.Clamp01(1f - r * 2.2f);
                    return new Color(1f, 1f, 1f, Mathf.Clamp01(arm + core * core));
                }
                // a Spider Bot leg: two jointed segments, a knee and a foot spike
                float d1 = SegDist(u, v, -0.85f, -0.7f, -0.1f, 0.5f);
                float d2 = SegDist(u, v, -0.1f, 0.5f, 0.85f, 0.75f);
                float d = Mathf.Min(d1, d2);
                float knee = Vector2.Distance(new Vector2(u, v), new Vector2(-0.1f, 0.5f));
                float body = Mathf.Max(Mathf.Clamp01((0.15f - d) * 30f), Mathf.Clamp01((0.2f - knee) * 30f));
                float shade = 0.65f + 0.35f * Mathf.Clamp01(0.5f + (0.1f - d) * 4f);
                return new Color(shade, shade, shade, body);
            }
        }
    }

    static float SegDist(float px, float py, float ax, float ay, float bx, float by) {
        float dx = bx - ax;
        float dy = by - ay;
        float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy));
        float cx = ax + dx * t - px;
        float cy = ay + dy * t - py;
        return Mathf.Sqrt(cx * cx + cy * cy);
    }
}
