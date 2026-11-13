using System.Collections.Generic;
using UnityEngine;

// The Moonlit Hollow lit like a film set, one look per Arcade act (docs/world-bible.md, A). ArcadeActs owns the colours (sun, fog, sky,
// grade) and the 1.5 s blend; this adds what a film's lighting crew adds on top, each driven by ArcadeActs.Weight(act) so it
// blends over the very same 1.5 s:
//   Act I   dusk: a low warm key from the side, long shadows, the lamp posts stutter on one after another.
//   Act II  deep night: a cold moon key from behind the post office, a rim on Wallit, Slice and the bots, warm lamp pools on the
//           ground (the readable safe zones), ground fog cards that drift.
//   Act III the red alert: a red key, a beacon on the ruin sweeping the lawn, big soft ash glows (no tiny sparks), more contrast.
// Every act also has a colour-grading LUT (a 256x16 strip drawn in code, blended in the post pass), a reflection probe, soft key
// shadows on desktop (the phone tier turns them off: MobileQuality), and a rim that goes into the character shader as _FilmRim so
// Wallit, Slice and every bot keep an edge against the background. Light probes and lightmaps need an editor bake and are not here.
// Arcade only (it is added by ArcadeActs). Nothing here shakes the camera.
[DefaultExecutionOrder(100)]
public class FilmLight : MonoBehaviour {
    public static FilmLight Instance { get; private set; }

    // Per act: rim colour and gain (the shader rim), rim light intensity, lamp multiplier, lamp pool alpha, fog card alpha,
    // lamp colour, key shadow strength.
    struct Set {
        public Color Rim, Lamp, Fog;
        public float RimGain, RimLight, LampMul, Pool, FogAlpha, Shadow, Stutter;
    }

    static readonly Set[] Sets = {
        new Set { Rim = new Color(1f, 0.62f, 0.34f), RimGain = 0.7f, RimLight = 0.25f, Lamp = new Color(1f, 0.64f, 0.3f), LampMul = 1.1f,
            Pool = 0.28f, Fog = new Color(0.62f, 0.45f, 0.5f), FogAlpha = 0.05f, Shadow = 0.85f, Stutter = 1f },
        new Set { Rim = new Color(0.4f, 0.72f, 1f), RimGain = 2.1f, RimLight = 0.85f, Lamp = new Color(1f, 0.72f, 0.4f), LampMul = 1.8f,
            Pool = 0.75f, Fog = new Color(0.32f, 0.45f, 0.75f), FogAlpha = 0.16f, Shadow = 0.8f, Stutter = 0.1f },
        new Set { Rim = new Color(1f, 0.22f, 0.2f), RimGain = 1.5f, RimLight = 0.6f, Lamp = new Color(1f, 0.42f, 0.26f), LampMul = 1f,
            Pool = 0.4f, Fog = new Color(0.6f, 0.15f, 0.18f), FogAlpha = 0.1f, Shadow = 0.9f, Stutter = 0.35f }
    };

    // Seen by the playtest.
    public static Color KeyNow { get; private set; }
    public static Color RimNow { get; private set; }
    public static float RimGainNow { get; private set; }
    public static float BeaconNow { get; private set; }
    public static float PoolNow { get; private set; }
    public static float FogNow { get; private set; }
    public static int ShadowMode { get; private set; }
    public static float LastBlendSeconds { get; private set; }
    public static int Blends { get; private set; }
    public static float MinKeyDuringBlend { get; private set; } = 9f;
    public static bool Phone { get; private set; }
    public static int ReflectionRenders { get; private set; }
    public static bool LutOn { get { return Instance != null && Instance.grade != null && Instance.grade.LutAmount > 0f; } }
    static readonly List<float> frameMs = new List<float>(4096);
    static readonly int FilmRimId = Shader.PropertyToID("_FilmRim");

    Light sun, rimLight, beacon;
    GameObject fogPlayer;
    Color lastLamp = new Color(-1f, -1f, -1f, -1f), lastFog = new Color(-1f, -1f, -1f, -1f);
    float lastLampPool = -1f, lastAshRate = -1f;
    NightGrade grade;
    Camera cam;
    Texture2D[] luts;
    ParticleSystem fogCards, ash;
    Transform pools;
    readonly List<Transform> poolQuads = new List<Transform>();
    Mesh poolMesh;
    readonly Color[] poolColors = new Color[4];
    ReflectionProbe probe;
    GameObject beaconHalo;
    Quaternion q0, q1, q2;
    bool blending;
    float blendStart;
    int settledAct = -1;
    ParticleSystem.Particle[] buffer;

    void Awake() {
        Instance = this;
        Phone = UiFit.Touch;
        Blends = 0;
        LastBlendSeconds = 0f;
        MinKeyDuringBlend = 9f;
        frameMs.Clear();
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
        Shader.SetGlobalVector(FilmRimId, Vector4.zero);
        if (NightAtmosphere.Instance != null) {
            NightAtmosphere.LampMul = 1f;
            NightAtmosphere.LampStutter = 0f;
        }
    }

    void Start() {
        cam = Camera.main;
        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None)) {
            if (l.type == LightType.Directional) {
                sun = l;
                break;
            }
        }
        luts = new[] { MakeLut(0), MakeLut(1), MakeLut(2) };
        BuildRim();
        BuildFog();
        BuildAsh();
        BuildBeacon();
        if (!Phone) {
            BuildProbe();
        }
        ApplyQuality();
    }

    // Desktop: soft shadows from the key, two cascades, 70 m. Phone: none (MobileQuality has already turned them off).
    void ApplyQuality() {
        Phone = UiFit.Touch;
        if (sun == null) {
            return;
        }
        if (Phone) {
            sun.shadows = LightShadows.None;
            ShadowMode = 0;
        } else {
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowDistance = 70f;
            QualitySettings.shadowCascades = 2;
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = 0.05f;
            sun.shadowNormalBias = 0.4f;
            ShadowMode = 2;
        }
    }

    // ---- the lights ----

    void BuildRim() {
        var go = new GameObject("Film Rim");
        go.transform.SetParent(transform, false);
        rimLight = go.AddComponent<Light>();
        rimLight.type = LightType.Directional;
        rimLight.shadows = LightShadows.None;
        rimLight.intensity = 0f;
        rimLight.cullingMask = ~0;
    }

    void BuildBeacon() {
        var go = new GameObject("Alert Beacon");
        go.transform.SetParent(transform, false);
        beacon = go.AddComponent<Light>();
        beacon.type = LightType.Spot;
        beacon.spotAngle = 24f;
        beacon.range = 300f;
        beacon.color = new Color(1f, 0.12f, 0.1f);
        beacon.intensity = 0f;
        beacon.shadows = LightShadows.None;
        beacon.renderMode = LightRenderMode.ForcePixel;
        beaconHalo = new GameObject("Alert Beacon Glow");
        beaconHalo.transform.SetParent(transform, false);
        beaconHalo.AddComponent<Halo>().Build(16f, new Color(1f, 0.15f, 0.1f, 0.8f));
        beaconHalo.SetActive(false);
    }

    void BuildProbe() {
        var go = new GameObject("Film Reflection Probe");
        go.transform.SetParent(transform, false);
        probe = go.AddComponent<ReflectionProbe>();
        probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
        probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
        probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
        probe.resolution = 64;
        probe.size = new Vector3(420f, 60f, 420f);
        probe.center = new Vector3(0f, 8f, 0f);
        probe.intensity = 0.8f;
        probe.boxProjection = false;
        probe.hdr = true;
        Musician m = Musician.Instance;
        go.transform.position = (m != null ? m.transform.position : Vector3.zero) + Vector3.up * 3f;
    }

    // Fog cards: big flat soft cards sliding along the ground, tinted by the act (heaviest in act II).
    void BuildFog() {
        var go = new GameObject("Fog Cards");
        go.transform.SetParent(transform, false);
        fogCards = go.AddComponent<ParticleSystem>();
        fogCards.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = fogCards.main;
        main.loop = true;
        main.maxParticles = Phone ? 18 : 44;
        main.startLifetime = new ParticleSystem.MinMaxCurve(16f, 24f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(14f, 26f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(1f, 1f, 1f, 0f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = fogCards.emission;
        emission.rateOverTime = Phone ? 1.1f : 2.4f;
        var shape = fogCards.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(120f, 0.2f, 120f);
        shape.position = new Vector3(0f, 0.5f, 0f);
        var vel = fogCards.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.5f, 1.3f);   // they drift across the lawn
        vel.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);   // same mode as x and z: Unity rejects mixed modes
        var col = fogCards.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        NightAtmosphere.Particles(go, "smoke", false, 1f);
        fogCards.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        fogCards.Play();
    }

    // Act III's air: a few big slow soft glows (0.6-1 m), orange-red, rising off the lawn. Never small sparks.
    void BuildAsh() {
        var go = new GameObject("Alert Ash");
        go.transform.SetParent(transform, false);
        ash = go.AddComponent<ParticleSystem>();
        ash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ash.main;
        main.loop = true;
        main.maxParticles = Phone ? 14 : 34;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1f);
        main.startColor = new Color(1f, 0.38f, 0.2f, 0.5f);
        main.gravityModifier = -0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ash.emission;
        emission.rateOverTime = 0f;
        var shape = ash.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(46f, 1f, 46f);
        shape.position = new Vector3(0f, 0.8f, 0f);
        var vel = ash.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.8f);
        vel.y = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        var col = ash.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        NightAtmosphere.Particles(go, "glow", true, 1.3f);
        ash.Play();
    }

    // Warm pools on the ground under each lantern: where Slice's ring and the lamps say it is safe to stand.
    void BuildPools() {
        IList<Light> lamps = NightAtmosphere.Lamps;
        if (lamps == null || lamps.Count == 0) {
            return;
        }
        pools = new GameObject("Lamp Pools").transform;
        pools.SetParent(transform, false);
        poolMesh = new Mesh { name = "Lamp Pool" };
        poolMesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
        poolMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        poolMesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };   // both faces: the shader's culling is not ours to guess
        poolMesh.colors = new Color[4];
        poolMesh.bounds = new Bounds(Vector3.zero, new Vector3(2f, 2f, 1f));
        Shader shader = Shader.Find("BT/Particle");
        var mat = new Material(shader != null ? shader : SafeShader.Find("Sprites/Default"));
        mat.mainTexture = Fx.Texture("glow");
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_Boost", 1.2f);
        foreach (Light lamp in lamps) {
            if (lamp == null) {
                continue;
            }
            var go = new GameObject("Lamp Pool");
            go.transform.SetParent(pools, false);
            Vector3 at = lamp.transform.position;
            float y = at.y - 1.9f;
            if (Physics.Raycast(new Vector3(at.x, at.y + 5f, at.z), Vector3.down, out RaycastHit hit, 30f, 1 << 3, QueryTriggerInteraction.Ignore)) {
                y = hit.point.y;
            }
            go.transform.position = new Vector3(at.x, y + 0.05f, at.z);   // 5 cm above the lawn: additive, no depth write, no z-fight
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one * 11f;
            go.AddComponent<MeshFilter>().sharedMesh = poolMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            poolQuads.Add(go.transform);
        }
    }

    // ---- LUTs: one 256x16 strip per act (16 slices of 16x16, red across, green up, slices step blue) ----

    static Texture2D MakeLut(int act) {
        var tex = new Texture2D(256, 16, TextureFormat.RGB24, false, true) { name = "Film LUT " + (act + 1) };
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color[256 * 16];
        for (int b = 0; b < 16; b++) {
            for (int g = 0; g < 16; g++) {
                for (int r = 0; r < 16; r++) {
                    var c = new Color(r / 15f, g / 15f, b / 15f);
                    px[g * 256 + b * 16 + r] = Grade(c, act);
                }
            }
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return tex;
    }

    // Dusk: teal-violet shadows, amber lights. Deep night: ink-blue shadows, pale cold lights with the warm kept for lamps.
    // Alert: crushed shadows pushed red, hot lights, hard curve.
    static Color Grade(Color c, int act) {
        float l = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
        Color shadow, light;
        float curve, sat;
        if (act == 0) {
            shadow = new Color(0.9f, 0.92f, 1.12f); light = new Color(1.12f, 0.98f, 0.8f); curve = 0.25f; sat = 1.1f;
        } else if (act == 1) {
            shadow = new Color(0.74f, 0.9f, 1.25f); light = new Color(0.98f, 1.02f, 1.08f); curve = 0.35f; sat = 0.92f;
        } else {
            shadow = new Color(1.18f, 0.78f, 0.86f); light = new Color(1.2f, 0.9f, 0.8f); curve = 0.55f; sat = 1.05f;
        }
        Color t = Color.Lerp(shadow, light, Mathf.SmoothStep(0f, 1f, l));
        var o = new Color(c.r * t.r, c.g * t.g, c.b * t.b);
        float ol = 0.299f * o.r + 0.587f * o.g + 0.114f * o.b;
        o = new Color(ol + (o.r - ol) * sat, ol + (o.g - ol) * sat, ol + (o.b - ol) * sat);
        o.r = Curve(o.r, curve);
        o.g = Curve(o.g, curve);
        o.b = Curve(o.b, curve);
        return o;
    }

    static float Curve(float x, float amount) {
        x = Mathf.Clamp01(x);
        return Mathf.Lerp(x, x * x * (3f - 2f * x), amount);
    }

    // ---- per frame ----

    void LateUpdate() {
        if (ArcadeActs.Act < 0) {
            return;
        }
        float dt = Time.unscaledDeltaTime;
        if (dt > 0f && Time.timeScale > 0f) {
            frameMs.Add(dt * 1000f);
            if (frameMs.Count > 4000) {
                frameMs.RemoveRange(0, 1000);
            }
        }
        if (cam == null) {
            cam = Camera.main;
        }
        if (grade == null && cam != null) {
            grade = cam.GetComponent<NightGrade>();
        }
        if (Phone != UiFit.Touch) {
            ApplyQuality();
        }
        float w0 = ArcadeActs.Weight(0), w1 = ArcadeActs.Weight(1), w2 = ArcadeActs.Weight(2);
        float total = Mathf.Max(0.0001f, w0 + w1 + w2);
        w0 /= total;
        w1 /= total;
        w2 /= total;

        Set s = Mix(w0, w1, w2);
        TrackBlend(w0, w1, w2);
        Key(w0, w1, w2, s);
        Rim(s);
        Lamps(s, w0, w2);
        Fog(s, w2);
        Beacon(w2);
        Lut(w0, w1, w2);
        if (probe != null && ArcadeActs.Settled && settledAct != ArcadeActs.Act) {
            settledAct = ArcadeActs.Act;
            probe.RenderProbe();
            ReflectionRenders++;
        }
    }

    static Set Mix(float w0, float w1, float w2) {
        var o = new Set();
        for (int i = 0; i < 3; i++) {
            float w = i == 0 ? w0 : (i == 1 ? w1 : w2);
            Set a = Sets[i];
            o.Rim += a.Rim * w;
            o.Lamp += a.Lamp * w;
            o.Fog += a.Fog * w;
            o.RimGain += a.RimGain * w;
            o.RimLight += a.RimLight * w;
            o.LampMul += a.LampMul * w;
            o.Pool += a.Pool * w;
            o.FogAlpha += a.FogAlpha * w;
            o.Shadow += a.Shadow * w;
            o.Stutter += a.Stutter * w;
        }
        return o;
    }

    // Times a blend (from the first frame an act's weight leaves 0 or 1 until it is whole) and the dimmest key in it.
    void TrackBlend(float w0, float w1, float w2) {
        float top = Mathf.Max(w0, Mathf.Max(w1, w2));
        bool moving = top < 0.999f;
        if (moving && !blending) {
            blending = true;
            blendStart = Time.unscaledTime;
            MinKeyDuringBlend = 9f;
        } else if (!moving && blending) {
            blending = false;
            LastBlendSeconds = Time.unscaledTime - blendStart;
            Blends++;
        }
        if (blending && sun != null) {
            MinKeyDuringBlend = Mathf.Min(MinKeyDuringBlend, sun.intensity);
        }
    }

    // Key direction: act I a low sun from the side, act II the moon from behind the post office, act III low from the beacon's side.
    void Key(float w0, float w1, float w2, Set s) {
        if (sun == null) {
            return;
        }
        Vector3 circle = HollowPostOffice.Built ? HollowPostOffice.Circle : (Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero);
        Vector3 toCircle = HollowPostOffice.Built ? circle - HollowPostOffice.LandmarkBase : Vector3.back;
        toCircle.y = 0f;
        float yaw = toCircle.sqrMagnitude > 0.01f ? Mathf.Atan2(toCircle.x, toCircle.z) * Mathf.Rad2Deg : 180f;
        q0 = Quaternion.Euler(13f, yaw + 105f, 0f);
        q1 = Quaternion.Euler(34f, yaw, 0f);
        q2 = Quaternion.Euler(18f, yaw - 75f, 0f);
        Quaternion q = Quaternion.Slerp(q0, q1, w1 / Mathf.Max(0.0001f, w0 + w1));
        q = Quaternion.Slerp(q, q2, w2);
        sun.transform.rotation = q;
        sun.shadowStrength = s.Shadow;
        KeyNow = sun.color;
    }

    void Rim(Set s) {
        RimNow = s.Rim;
        RimGainNow = s.RimGain;
        Shader.SetGlobalVector(FilmRimId, new Vector4(s.Rim.r, s.Rim.g, s.Rim.b, s.RimGain));
        if (rimLight == null) {
            return;
        }
        rimLight.enabled = !Phone;   // a phone has the shader rim only: no extra pixel light
        rimLight.color = s.Rim;
        rimLight.intensity = s.RimLight;
        if (cam != null) {
            // From behind and to the side of the subjects, shining toward the camera: an edge on one flank of every character.
            Vector3 f = cam.transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 0.01f) {
                f = Vector3.forward;
            }
            Quaternion toward = Quaternion.LookRotation(-f.normalized, Vector3.up);
            rimLight.transform.rotation = toward * Quaternion.Euler(16f, 38f, 0f);
        }
    }

    void Lamps(Set s, float w0, float w2) {
        NightAtmosphere.LampMul = s.LampMul;
        NightAtmosphere.LampStutter = s.Stutter;
        IList<Light> lamps = NightAtmosphere.Lamps;
        // Lamp colour and range only move during a blend: skip the loop (and the light-state writes) once nothing changed.
        bool lampChanged = s.Lamp != lastLamp || !Mathf.Approximately(s.Pool, lastLampPool);
        if (lamps != null && lampChanged) {
            lastLamp = s.Lamp;
            lastLampPool = s.Pool;
            foreach (Light l in lamps) {
                if (l != null) {
                    l.color = s.Lamp;
                    l.range = Mathf.Lerp(11f, 15f, Mathf.Clamp01(s.Pool));
                }
            }
        }
        if (pools == null) {
            BuildPools();
        }
        PoolNow = s.Pool;
        if (poolMesh != null && lampChanged) {
            Color c = new Color(s.Lamp.r, s.Lamp.g, s.Lamp.b, s.Pool);
            poolColors[0] = poolColors[1] = poolColors[2] = poolColors[3] = c;
            poolMesh.colors = poolColors;
        }
    }

    void Fog(Set s, float w2) {
        FogNow = s.FogAlpha;
        if (fogCards == null) {
            return;
        }
        Transform follow = cam != null ? cam.transform : null;
        if (fogPlayer == null) {
            fogPlayer = GameObject.FindGameObjectWithTag("Player");
        }
        GameObject player = fogPlayer;
        if (player != null) {
            Vector3 p = player.transform.position;
            fogCards.transform.position = new Vector3(p.x, p.y - 0.4f, p.z);
            ash.transform.position = new Vector3(p.x, p.y - 0.4f, p.z);
        } else if (follow != null) {
            fogCards.transform.position = new Vector3(follow.position.x, 0f, follow.position.z);
        }
        // Retint every live card each frame, so a blend moves the fog already in the air, not only new cards.
        // Only while the look moves (a blend): a settled act has the same colour on every card, new ones included.
        Color c = new Color(s.Fog.r, s.Fog.g, s.Fog.b, s.FogAlpha);
        if (c != lastFog) {
            lastFog = c;
            int max = fogCards.main.maxParticles;
            if (buffer == null || buffer.Length < max) {
                buffer = new ParticleSystem.Particle[max];
            }
            int n = fogCards.GetParticles(buffer);
            for (int i = 0; i < n; i++) {
                buffer[i].startColor = c;
            }
            fogCards.SetParticles(buffer, n);
            var fogMain = fogCards.main;
            fogMain.startColor = c;   // cards born after the blend ends start in the settled colour
            var emission = fogCards.emission;
            emission.rateOverTime = (Phone ? 0.6f : 1.3f) + s.FogAlpha * (Phone ? 6f : 12f);
        }
        float ash5 = w2 * (Phone ? 1.6f : 4.2f);
        if (!Mathf.Approximately(ash5, lastAshRate)) {
            lastAshRate = ash5;
            var ae = ash.emission;
            ae.rateOverTime = ash5;
        }
    }

    // The beacon sweeps a ring of lawn round the circle; its glow sits on the ruin.
    void Beacon(float w2) {
        BeaconNow = w2;
        if (beacon == null) {
            return;
        }
        bool up = w2 > 0.02f && HollowPostOffice.Built;
        beacon.enabled = up;
        beaconHalo.SetActive(up);
        if (!up) {
            return;
        }
        Vector3 head = HollowPostOffice.LandmarkBase + Vector3.up * 17f;
        float a = Time.time * 0.85f;
        Vector3 target = HollowPostOffice.Circle + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 34f;
        beacon.transform.position = head;
        beacon.transform.rotation = Quaternion.LookRotation(target - head, Vector3.up);
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 6f);
        beacon.intensity = 6f * w2 * pulse;
        beaconHalo.transform.position = head;
        beaconHalo.transform.localScale = Vector3.one * (0.4f + 0.6f * w2);
    }

    void Lut(float w0, float w1, float w2) {
        if (grade == null || luts == null) {
            return;
        }
        // Blend the two nearest looks: A -> B by their shares, so the grade is never off or a third look in between.
        if (w2 > 0.0001f && w1 > 0.0001f) {
            grade.LutA = luts[1];
            grade.LutB = luts[2];
            grade.LutBlend = w2 / (w1 + w2);
        } else if (w2 > 0.0001f) {
            grade.LutA = luts[0];
            grade.LutB = luts[2];
            grade.LutBlend = w2 / (w0 + w2);
        } else {
            grade.LutA = luts[0];
            grade.LutB = luts[1];
            grade.LutBlend = w1 / Mathf.Max(0.0001f, w0 + w1);
        }
        grade.LutAmount = 0.55f;
    }

    // ---- for the playtest ----

    // Frame time in ms at the given percentile (0.95 = the slowest 5% start here) since the last reset.
    public static float FrameMsPercentile(float p) {
        if (frameMs.Count < 10) {
            return 0f;
        }
        var copy = new List<float>(frameMs);
        copy.Sort();
        return copy[Mathf.Clamp(Mathf.FloorToInt(p * (copy.Count - 1)), 0, copy.Count - 1)];
    }

    public static void ResetFrames() {
        frameMs.Clear();
    }

    public static string Describe() {
        return "key=" + KeyNow + " rim=" + RimNow + "x" + RimGainNow.ToString("0.0") + " pools=" + PoolNow.ToString("0.00")
            + " fog=" + FogNow.ToString("0.00") + " beacon=" + BeaconNow.ToString("0.00") + " shadows=" + (ShadowMode == 2 ? "soft" : "off")
            + " lut=" + LutOn + " phone=" + Phone;
    }
}
