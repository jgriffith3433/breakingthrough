using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Arcade's three acts (waves 1-10 Night Shift, 11-20 Cold Hour, 21-30 Last Light; after 30 the loop starts again under an
// overtime tint). Each act has its own light, sky, grade and a synthesised score layer; the change crossfades over
// Tuning.ActFadeSeconds and shows a banner with a sting. Arcade only: a chapter (ChapterRun) never gets one.
public class ArcadeActs : MonoBehaviour {
    struct Look {
        public Color Sun, Fog, SkyTop, SkyHorizon, SkyGround, AmbSky, AmbEq, AmbGround, Disc, Shadows, Highlights, Lift;
        public float SunIntensity, FogDensity, DiscSize, Haze, Exposure, Contrast, Saturation, Vignette;
        public Vector3 DiscDir;

        public static Look Lerp(Look a, Look b, float t) {
            return new Look {
                Sun = Color.Lerp(a.Sun, b.Sun, t), Fog = Color.Lerp(a.Fog, b.Fog, t), SkyTop = Color.Lerp(a.SkyTop, b.SkyTop, t),
                SkyHorizon = Color.Lerp(a.SkyHorizon, b.SkyHorizon, t), SkyGround = Color.Lerp(a.SkyGround, b.SkyGround, t),
                AmbSky = Color.Lerp(a.AmbSky, b.AmbSky, t), AmbEq = Color.Lerp(a.AmbEq, b.AmbEq, t),
                AmbGround = Color.Lerp(a.AmbGround, b.AmbGround, t), Disc = Color.Lerp(a.Disc, b.Disc, t),
                Shadows = Color.Lerp(a.Shadows, b.Shadows, t), Highlights = Color.Lerp(a.Highlights, b.Highlights, t),
                Lift = Color.Lerp(a.Lift, b.Lift, t),
                SunIntensity = Mathf.Lerp(a.SunIntensity, b.SunIntensity, t), FogDensity = Mathf.Lerp(a.FogDensity, b.FogDensity, t),
                DiscSize = Mathf.Lerp(a.DiscSize, b.DiscSize, t), Haze = Mathf.Lerp(a.Haze, b.Haze, t),
                Exposure = Mathf.Lerp(a.Exposure, b.Exposure, t), Contrast = Mathf.Lerp(a.Contrast, b.Contrast, t),
                Saturation = Mathf.Lerp(a.Saturation, b.Saturation, t), Vignette = Mathf.Lerp(a.Vignette, b.Vignette, t),
                DiscDir = Vector3.Lerp(a.DiscDir, b.DiscDir, t)
            };
        }
    }

    static readonly string[] Titles = { "ACT I  NIGHT SHIFT", "ACT II  COLD HOUR", "ACT III  LAST LIGHT" };
    static readonly string[] Lines = {
        "Yo Bots on the hill. Slice has the circle.",
        "Spider Bots come down the wall.",
        "Elites in every crew. Nothing is plain."
    };
    const string OvertimeTitle = "OVERTIME";
    const string OvertimeLine = "The clock won't clock out. Pay up.";

    static readonly Look[] Looks = {
        // Night Shift: the Moonlit Hollow as NightAtmosphere has always lit it.
        new Look { Sun = new Color(0.62f, 0.72f, 1f), SunIntensity = 0.9f, Fog = new Color(0.1f, 0.13f, 0.22f), FogDensity = 0.0062f,
            SkyTop = new Color(0.03f, 0.05f, 0.14f), SkyHorizon = new Color(0.16f, 0.22f, 0.42f), SkyGround = new Color(0.04f, 0.05f, 0.09f),
            AmbSky = new Color(0.27f, 0.33f, 0.55f), AmbEq = new Color(0.16f, 0.2f, 0.3f), AmbGround = new Color(0.07f, 0.08f, 0.11f),
            Disc = new Color(0.8f, 0.88f, 1f), DiscDir = new Vector3(-0.3f, 0.55f, 0.8f), DiscSize = 0.05f, Haze = 0.5f,
            Exposure = 1.08f, Contrast = 1.12f, Saturation = 0.9f, Vignette = 0.85f,
            Shadows = new Color(0.8f, 0.9f, 1.16f), Highlights = new Color(1.08f, 1f, 0.9f), Lift = new Color(0f, 0.012f, 0.03f) },
        // Cold Hour: a teal storm light, the moon gone behind it.
        new Look { Sun = new Color(0.5f, 0.85f, 0.95f), SunIntensity = 0.72f, Fog = new Color(0.06f, 0.17f, 0.19f), FogDensity = 0.0078f,
            SkyTop = new Color(0.02f, 0.07f, 0.1f), SkyHorizon = new Color(0.12f, 0.32f, 0.35f), SkyGround = new Color(0.03f, 0.06f, 0.08f),
            AmbSky = new Color(0.16f, 0.38f, 0.42f), AmbEq = new Color(0.1f, 0.24f, 0.27f), AmbGround = new Color(0.04f, 0.08f, 0.09f),
            Disc = new Color(0.5f, 0.9f, 0.95f), DiscDir = new Vector3(0.45f, 0.3f, -0.8f), DiscSize = 0.03f, Haze = 0.85f,
            Exposure = 1.04f, Contrast = 1.2f, Saturation = 1.02f, Vignette = 1.05f,
            Shadows = new Color(0.7f, 1.02f, 1.1f), Highlights = new Color(0.95f, 1.05f, 1f), Lift = new Color(0f, 0.025f, 0.03f) },
        // Last Light: a bruised dusk, orange at the rim, the Hollow's last hour before the Department opens.
        new Look { Sun = new Color(1f, 0.66f, 0.5f), SunIntensity = 1.05f, Fog = new Color(0.28f, 0.13f, 0.15f), FogDensity = 0.0052f,
            SkyTop = new Color(0.12f, 0.05f, 0.16f), SkyHorizon = new Color(0.85f, 0.38f, 0.3f), SkyGround = new Color(0.1f, 0.05f, 0.07f),
            AmbSky = new Color(0.5f, 0.35f, 0.5f), AmbEq = new Color(0.32f, 0.2f, 0.24f), AmbGround = new Color(0.12f, 0.07f, 0.08f),
            Disc = new Color(1f, 0.7f, 0.5f), DiscDir = new Vector3(0.2f, 0.08f, 0.95f), DiscSize = 0.12f, Haze = 0.7f,
            Exposure = 1.15f, Contrast = 1.1f, Saturation = 1.12f, Vignette = 0.9f,
            Shadows = new Color(1.05f, 0.85f, 0.95f), Highlights = new Color(1.15f, 0.95f, 0.8f), Lift = new Color(0.03f, 0.006f, 0.012f) }
    };

    static readonly Look Overtime = new Look {
        Sun = new Color(1f, 0.25f, 0.22f), SunIntensity = 0.8f, Fog = new Color(0.2f, 0.02f, 0.04f), FogDensity = 0.0078f,
        SkyTop = new Color(0.12f, 0f, 0.03f), SkyHorizon = new Color(0.55f, 0.04f, 0.08f), SkyGround = new Color(0.06f, 0.01f, 0.02f),
        AmbSky = new Color(0.45f, 0.15f, 0.2f), AmbEq = new Color(0.25f, 0.08f, 0.1f), AmbGround = new Color(0.08f, 0.02f, 0.03f),
        Disc = new Color(1f, 0.2f, 0.2f), DiscDir = new Vector3(0f, 0.4f, 0.9f), DiscSize = 0.09f, Haze = 0.8f,
        Exposure = 1f, Contrast = 1.25f, Saturation = 0.95f, Vignette = 1.2f,
        Shadows = new Color(1.1f, 0.7f, 0.8f), Highlights = new Color(1.1f, 0.85f, 0.8f), Lift = new Color(0.03f, 0f, 0.005f)
    };

    public static ArcadeActs Instance { get; private set; }
    public static int Act { get; private set; } = -1;
    public static bool InOvertime { get; private set; }
    // A boss theme (MusicDirector) is playing: the act's score layers step back under it (CinematicDirector boss entrances).
    public static bool BossTheme;
    public static int Changes { get; private set; }
    public static string BannerTitle { get; private set; } = "";
    public static string Stings { get; private set; } = "";
    // Lowest combined layer level seen once the score was up: near 1 means no gap and no hard cut in a crossfade.
    public static float MixFloor { get; private set; } = 9f;
    public static bool Settled { get { return Instance != null && Instance.fade >= 1f; } }

    Look from, cur, target;
    float fade = 1f;
    Material skyMat;
    Light sun;
    NightGrade grade;
    Camera cam;
    AudioSource[] layers;
    AudioSource stingSource;
    AudioClip[] stings;
    float[] weights = new float[3];
    float[] weightGoal = new float[3];
    bool musicUp;
    int lastWave = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        SceneManager.sceneLoaded += (s, m) => Ensure();
        Ensure();
    }

    static void Ensure() {
        if (SceneManager.GetActiveScene().name != MetaProgress.GameScene || ChapterRun.Instance != null || FindAnyObjectByType<ArcadeActs>() != null) {
            return;
        }
        new GameObject("Arcade Acts").AddComponent<ArcadeActs>();
    }

    public static int ActOf(int wave) {
        return Tuning.ActOf(wave);
    }

    public static string TitleOf(int wave) {
        return Tuning.IsOvertime(wave) ? OvertimeTitle : Titles[Tuning.ActOf(wave)];
    }

    void Awake() {
        Instance = this;
        Act = -1;
        InOvertime = false;
        Changes = 0;
        BannerTitle = "";
        Stings = "";
        MixFloor = 9f;
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
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
        Shader shader = SafeShader.Find("Knoll/ChapterSky");
        if (shader != null) {
            skyMat = new Material(shader);
        }
        BuildAudio();
    }

    void Update() {
        if (ChapterRun.Instance != null) {
            Destroy(gameObject);
            return;
        }
        int wave = WaveDirector.Wave;
        if (wave >= 1 && wave != lastWave) {
            lastWave = wave;
            int act = Tuning.ActOf(wave);
            bool over = Tuning.IsOvertime(wave);
            if (act != Act || over != InOvertime) {
                Change(act, over);
            }
        }
        if (Act < 0) {
            return;
        }
        float dt = Time.unscaledDeltaTime;
        if (fade < 1f) {
            fade = Mathf.Min(1f, fade + dt / Tuning.ActFadeSeconds);
            cur = Look.Lerp(from, target, Mathf.SmoothStep(0f, 1f, fade));
        }
        Apply(cur);
        TickMusic(dt);
    }

    void Change(int act, bool over) {
        bool firstChange = Act < 0;
        Act = act;
        InOvertime = over;
        Changes++;
        target = Preset(act, over);
        from = firstChange ? target : cur;
        if (firstChange) {
            cur = target;
            fade = 1f;
            for (int i = 0; i < weights.Length; i++) {
                weights[i] = i == act ? 1f : 0f;
            }
        } else {
            fade = 0f;
        }
        for (int i = 0; i < weightGoal.Length; i++) {
            weightGoal[i] = i == act ? 1f : 0f;
        }
        BannerTitle = TitleOf(lastWave);
        Banner(BannerTitle, over ? OvertimeLine : Lines[act]);
        Sting(act, over);
        RunAnalytics.Screen("act");
    }

    static Look Preset(int act, bool over) {
        Look look = Looks[Mathf.Clamp(act, 0, 2)];
        return over ? Look.Lerp(look, Overtime, Tuning.OvertimeTint) : look;
    }

    void Apply(Look look) {
        if (sun == null) {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None)) {
                if (l.type == LightType.Directional) {
                    sun = l;
                    break;
                }
            }
        }
        if (sun != null) {
            sun.color = look.Sun;
            sun.intensity = look.SunIntensity;
        }
        RenderSettings.fog = true;
        RenderSettings.fogColor = look.Fog;
        RenderSettings.fogDensity = look.FogDensity;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = look.AmbSky;
        RenderSettings.ambientEquatorColor = look.AmbEq;
        RenderSettings.ambientGroundColor = look.AmbGround;
        if (cam == null) {
            cam = Camera.main;
        }
        if (cam != null) {
            if (skyMat != null) {
                skyMat.SetColor("_Top", look.SkyTop);
                skyMat.SetColor("_Horizon", look.SkyHorizon);
                skyMat.SetColor("_Ground", look.SkyGround);
                skyMat.SetColor("_Disc", look.Disc);
                skyMat.SetVector("_DiscDir", new Vector4(look.DiscDir.x, look.DiscDir.y, look.DiscDir.z, 0f));
                skyMat.SetFloat("_DiscSize", look.DiscSize);
                skyMat.SetFloat("_Haze", look.Haze);
                RenderSettings.skybox = skyMat;
                cam.clearFlags = CameraClearFlags.Skybox;
            } else {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = look.SkyHorizon;
            }
            if (grade == null) {
                grade = cam.GetComponent<NightGrade>();
            }
        }
        if (grade != null) {
            grade.Exposure = look.Exposure;
            grade.Contrast = look.Contrast;
            grade.Saturation = look.Saturation;
            grade.Vignette = look.Vignette;
            grade.Shadows = look.Shadows;
            grade.Highlights = look.Highlights;
            grade.Lift = look.Lift;
        }
    }

    // One line of what the playtest can compare between acts.
    public static string Describe() {
        Color h = Color.black;
        if (Instance != null && Instance.skyMat != null) {
            h = Instance.skyMat.GetColor("_Horizon");
        }
        Color fog = RenderSettings.fogColor;
        Color sunColor = Instance != null && Instance.sun != null ? Instance.sun.color : Color.black;
        return "act=" + Act + " overtime=" + InOvertime + " sun=" + sunColor + " fog=" + fog + " horizon=" + h;
    }

    // Distance between two looks' sky horizon, sun and fog colours, so a playtest can say waves 1, 11 and 21 differ.
    public static Vector3 Signature() {
        Color h = Instance != null && Instance.skyMat != null ? Instance.skyMat.GetColor("_Horizon") : RenderSettings.fogColor;
        Color s = Instance != null && Instance.sun != null ? Instance.sun.color : Color.black;
        return new Vector3(h.r + h.g + h.b, s.r + s.g + s.b, RenderSettings.fogColor.r + RenderSettings.fogColor.g + RenderSettings.fogColor.b);
    }

    public static float SignatureDistance(Color a, Color b) {
        return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
    }

    public static Color HorizonNow {
        get { return Instance != null && Instance.skyMat != null ? Instance.skyMat.GetColor("_Horizon") : RenderSettings.fogColor; }
    }

    public static Color SunNow {
        get { return Instance != null && Instance.sun != null ? Instance.sun.color : Color.black; }
    }

    // ---- banner ----

    Canvas bannerCanvas;
    Text bannerTitle;
    Text bannerLine;
    float bannerLeft;

    public static List<Rect> BannerRects() {
        var rects = new List<Rect>();
        if (Instance == null || Instance.bannerCanvas == null) {
            return rects;
        }
        foreach (Text t in Instance.bannerCanvas.GetComponentsInChildren<Text>()) {
            var corners = new Vector3[4];
            t.rectTransform.GetWorldCorners(corners);
            rects.Add(new Rect(corners[0].x, corners[0].y, corners[2].x - corners[0].x, corners[2].y - corners[0].y));
        }
        return rects;
    }

    public static bool BannerUp {
        get { return Instance != null && Instance.bannerCanvas != null; }
    }

    void Banner(string title, string line) {
        if (bannerCanvas != null) {
            Destroy(bannerCanvas.gameObject);
        }
        var go = new GameObject("Act Banner");
        bannerCanvas = go.AddComponent<Canvas>();
        bannerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        bannerCanvas.sortingOrder = 190;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        bool compact = UiFit.Compact;
        if (compact) {
            UiFit.Apply(scaler);
        }
        float width = compact ? UiFit.Size.x - 48f : 1400f;
        float top = compact ? (UiFit.Mode == UiMode.Tall ? 330f : 140f) : 330f;
        bannerTitle = MakeText(go.transform, "Title", title, compact ? 36 : 60, FontStyle.Bold, new Color(1f, 0.92f, 0.72f), width, top, compact ? 56f : 90f);
        bannerLine = MakeText(go.transform, "Line", line, compact ? 22 : 32, FontStyle.Normal, new Color(0.85f, 0.9f, 1f), width, top - (compact ? 52f : 80f), compact ? 36f : 50f);
        bannerLeft = Tuning.ActBannerSeconds;
    }

    static Text MakeText(Transform parent, string name, string text, int size, FontStyle style, Color color, float width, float y, float height) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = UiSkin.Font(UiSkin.FontRole.Label);
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.color = color;
        t.text = text;
        t.raycastTarget = false;
        go.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
        var rect = t.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(width, height);
        return t;
    }

    void LateUpdate() {
        if (bannerCanvas == null) {
            return;
        }
        bannerLeft -= Time.unscaledDeltaTime;
        float a = Mathf.Clamp01(Mathf.Min(bannerLeft / 0.6f, (Tuning.ActBannerSeconds - bannerLeft) / 0.4f));
        SetAlpha(bannerTitle, a);
        SetAlpha(bannerLine, a);
        if (bannerLeft <= 0f) {
            Destroy(bannerCanvas.gameObject);
            bannerCanvas = null;
        }
    }

    static void SetAlpha(Text t, float a) {
        if (t != null) {
            Color c = t.color;
            c.a = a;
            t.color = c;
        }
    }

    // ---- sound: three looping layers and a sting per act, all made in code (no files, no cost) ----

    const int Rate = 22050;

    void BuildAudio() {
        layers = new AudioSource[3];
        float[][] notes = {
            new[] { 110f, 164.8f, 220f, 261.6f },
            new[] { 73.4f, 110f, 146.8f, 174.6f },
            new[] { 55f, 82.4f, 123.5f, 185f }
        };
        float[] pulse = { 0f, 1.5f, 3f };
        float[] strikeEvery = { 4f, 2f, 1f };
        float[] strikeHz = { 659.3f, 392f, 220f };
        stings = new AudioClip[3];
        for (int i = 0; i < 3; i++) {
            var go = new GameObject("Act Layer " + (i + 1));
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = Pad("Act Score " + (i + 1), 8f, notes[i], pulse[i], pulse[i] > 0f ? 0.35f : 0f, strikeEvery[i], strikeHz[i]);
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.Play();
            layers[i] = source;
            stings[i] = Chime("Act Sting " + (i + 1), notes[i][3] * 2f, i == 1 ? 1.335f : 1.5f);
        }
        var sg = new GameObject("Act Sting");
        sg.transform.SetParent(transform, false);
        stingSource = sg.AddComponent<AudioSource>();
        stingSource.spatialBlend = 0f;
        stingSource.playOnAwake = false;
        musicUp = true;
    }

    void TickMusic(float dt) {
        if (!musicUp) {
            return;
        }
        float level = SoundSettings.On ? SoundSettings.Music * Tuning.ActMusicLevel * (BossTheme ? 0.2f : 1f) : 0f;
        float step = dt / Tuning.ActFadeSeconds;
        float mix = 0f;
        for (int i = 0; i < layers.Length; i++) {
            weights[i] = Mathf.MoveTowards(weights[i], weightGoal[i], step);
            // Equal-power: the two layers of a crossfade never dip below one layer's level.
            float gain = Mathf.Sqrt(weights[i]);
            mix += gain;
            layers[i].volume = gain * level;
            layers[i].pitch = InOvertime ? 0.94f : 1f;
        }
        MixFloor = Mathf.Min(MixFloor, mix);
    }

    void Sting(int act, bool over) {
        if (stingSource == null || stings == null) {
            return;
        }
        stingSource.pitch = over ? 0.8f : 1f;
        stingSource.PlayOneShot(stings[act], 0.7f * SoundSettings.EffectsLevel);
        Stings += (Stings.Length > 0 ? "," : "") + (over ? "o" : "") + (act + 1);
    }

    // Held tones tuned to whole cycles per loop, so the loop is seamless, with a slow swell, a pulse and soft strikes.
    static AudioClip Pad(string name, float seconds, float[] freqs, float pulseHz, float pulseDepth, float strikeEvery, float strikeHz) {
        int count = Mathf.RoundToInt(seconds * Rate);
        var data = new float[count];
        float peak = 0.0001f;
        float pulseFixed = pulseHz > 0f ? Mathf.Round(pulseHz * seconds) / seconds : 0f;
        float strikeFixed = Mathf.Round(strikeHz * strikeEvery) / strikeEvery;
        for (int n = 0; n < count; n++) {
            float t = n / (float)Rate;
            float v = 0f;
            for (int k = 0; k < freqs.Length; k++) {
                float f = Mathf.Round(freqs[k] * seconds) / seconds;
                float swell = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * (t / seconds * (1 + k % 2)) + k);
                v += Mathf.Sin(2f * Mathf.PI * f * t) * swell / (1f + k * 0.5f);
            }
            if (pulseFixed > 0f) {
                v *= 1f - pulseDepth + pulseDepth * 0.5f * (1f + Mathf.Sin(2f * Mathf.PI * pulseFixed * t));
            }
            float into = t % strikeEvery;
            v += Mathf.Sin(2f * Mathf.PI * strikeFixed * t) * Mathf.Exp(-into * 3f) * 0.7f * Mathf.Min(1f, into * 40f);
            data[n] = v;
            peak = Mathf.Max(peak, Mathf.Abs(v));
        }
        for (int n = 0; n < count; n++) {
            data[n] = data[n] / peak * 0.8f;
        }
        var clip = AudioClip.Create(name, count, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // A two-note rising chime that rings out: the act's sting.
    static AudioClip Chime(string name, float baseHz, float ratio) {
        int count = Mathf.RoundToInt(1.6f * Rate);
        var data = new float[count];
        for (int n = 0; n < count; n++) {
            float t = n / (float)Rate;
            float env = Mathf.Min(1f, t * 60f);
            float a = Mathf.Sin(2f * Mathf.PI * baseHz * t) * Mathf.Exp(-t * 2.4f);
            float tb = Mathf.Max(0f, t - 0.18f);
            float b = Mathf.Sin(2f * Mathf.PI * baseHz * ratio * tb) * Mathf.Exp(-tb * 2.2f) * (t > 0.18f ? 1f : 0f);
            data[n] = (a * 0.6f + b * 0.6f) * env * 0.8f;
        }
        var clip = AudioClip.Create(name, count, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
