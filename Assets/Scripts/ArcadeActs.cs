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
        // Act I dusk: a low warm sun-to-moon key, amber fog, a violet sky going to ink; the lamp posts flicker on.
        new Look { Sun = new Color(1f, 0.78f, 0.32f), SunIntensity = 1.1f, Fog = new Color(0.24f, 0.14f, 0.2f), FogDensity = 0.0058f,
            SkyTop = new Color(0.1f, 0.08f, 0.24f), SkyHorizon = new Color(0.95f, 0.5f, 0.3f), SkyGround = new Color(0.08f, 0.05f, 0.08f),
            AmbSky = new Color(0.44f, 0.36f, 0.52f), AmbEq = new Color(0.3f, 0.22f, 0.26f), AmbGround = new Color(0.1f, 0.07f, 0.08f),
            Disc = new Color(1f, 0.78f, 0.55f), DiscDir = new Vector3(-0.3f, 0.12f, 0.8f), DiscSize = 0.1f, Haze = 0.6f,
            Exposure = 1.1f, Contrast = 1.1f, Saturation = 1.08f, Vignette = 0.8f,
            Shadows = new Color(0.9f, 0.88f, 1.1f), Highlights = new Color(1.14f, 0.98f, 0.82f), Lift = new Color(0.015f, 0.006f, 0.02f) },
        // Act II deep night: a cold blue moon key from behind the post office, rim light on everyone, warm lamp pools, ground fog.
        new Look { Sun = new Color(0.4f, 0.58f, 1f), SunIntensity = 0.95f, Fog = new Color(0.04f, 0.08f, 0.2f), FogDensity = 0.0072f,
            SkyTop = new Color(0.01f, 0.02f, 0.09f), SkyHorizon = new Color(0.07f, 0.15f, 0.4f), SkyGround = new Color(0.02f, 0.03f, 0.07f),
            AmbSky = new Color(0.22f, 0.34f, 0.6f), AmbEq = new Color(0.13f, 0.19f, 0.34f), AmbGround = new Color(0.04f, 0.05f, 0.1f),
            Disc = new Color(0.7f, 0.85f, 1f), DiscDir = new Vector3(0.45f, 0.35f, -0.8f), DiscSize = 0.05f, Haze = 0.8f,
            Exposure = 1.18f, Contrast = 1.2f, Saturation = 0.95f, Vignette = 1f,
            Shadows = new Color(0.92f, 0.88f, 1.0f), Highlights = new Color(1.1f, 1.0f, 0.86f), Lift = new Color(0.012f, 0.008f, 0.02f) },
        // Act III the Department's red alert: a red key, a beacon sweeping the lawn, ash in the air, higher contrast.
        new Look { Sun = new Color(1f, 0.08f, 0.2f), SunIntensity = 0.95f, Fog = new Color(0.26f, 0.03f, 0.05f), FogDensity = 0.0062f,
            SkyTop = new Color(0.14f, 0f, 0.04f), SkyHorizon = new Color(0.7f, 0.07f, 0.1f), SkyGround = new Color(0.08f, 0.01f, 0.02f),
            AmbSky = new Color(0.5f, 0.14f, 0.18f), AmbEq = new Color(0.3f, 0.07f, 0.1f), AmbGround = new Color(0.1f, 0.02f, 0.03f),
            Disc = new Color(1f, 0.2f, 0.18f), DiscDir = new Vector3(0.2f, 0.08f, 0.95f), DiscSize = 0.12f, Haze = 0.75f,
            Exposure = 1.1f, Contrast = 1.32f, Saturation = 1.05f, Vignette = 1.1f,
            Shadows = new Color(1.1f, 0.74f, 0.82f), Highlights = new Color(1.18f, 0.9f, 0.82f), Lift = new Color(0.035f, 0f, 0.008f) }
    };

    static readonly Look Overtime = new Look {
        Sun = new Color(1f, 0.1f, 0.2f), SunIntensity = 0.8f, Fog = new Color(0.2f, 0.02f, 0.04f), FogDensity = 0.0078f,
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
    // Each act's share of the look right now (0..1, they sum to 1): FilmLight blends its key direction, rim, lamps, fog cards,
    // beacon, ash and LUT by it, so the whole set changes over the same Tuning.ActFadeSeconds as the colours.
    public static float Weight(int act) { return Instance != null && act >= 0 && act < 3 ? Instance.weights[act] : 0f; }
    public static bool Settled { get { return Instance != null && Instance.fade >= 1f; } }

    // The staged act change (CinematicAct.cs): the blend lasts as long as the scene; the sting and the card wait for it.
    public static float FadeSeconds = Tuning.ActFadeSeconds;
    public static int StagedChanges { get; private set; }
    int pendingAct = -1;
    bool pendingOver;
    readonly bool[] staged = new bool[3];

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
    public AudioSource Master { get { return layers != null && layers.Length > 0 ? layers[0] : null; } }
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
        FadeSeconds = Tuning.ActFadeSeconds;
        StagedChanges = 0;
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
        gameObject.AddComponent<FilmLight>();   // the film-set light layer, blended by this class's weights
        if (ChapterRun.Instance == null) {
            // The Hollow's own air: its bed (fetched by URL) and the spots on the ruin's sign, the lanterns and the post boxes. The
            // landmark builds in its own Start, so the spots look for their objects a moment later.
            MusicDirector.Ambience("amb_hollow");
            Invoke(nameof(EnterPlaceAudio), 1.5f);
        }
    }

    void EnterPlaceAudio() {
        if (ChapterRun.Instance == null) {
            PlaceAudio.Enter(PlaceAudio.Place.Hollow, transform);
        }
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
                // Acts II and III arrive as a staged scene once per run (after the wave 10 and 20 bosses); it waits for any card.
                if (Act >= 0 && !over && act > Act && !staged[act] && CinematicDirector.ActScenesAllowed) {
                    pendingAct = act;
                    pendingOver = over;
                } else {
                    Change(act, over, false);
                }
            }
        }
        if (pendingAct >= 0 && !LevelUpScreen.IsOpen && !PauseMenu.IsOpen && !Cutscene.IsOpen && !StoryScreen.IsOpen) {
            int a = pendingAct;
            pendingAct = -1;
            staged[a] = true;
            Change(a, pendingOver, true);
        }
        if (Act < 0) {
            return;
        }
        // A hitch (the first Spider Bots of act II load on this frame) must not eat the blend: one step is at most 0.1 s,
        // so the change always plays out over about FadeSeconds instead of cutting.
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        if (fade < 1f) {
            fade = Mathf.Min(1f, fade + dt / FadeSeconds);
            cur = Look.Lerp(from, target, Mathf.SmoothStep(0f, 1f, fade));
        }
        Apply(cur);
        // The film-light layer reads these weights, so they move whether or not the score is up (they used to step inside
        // TickMusic, which returns early without it, leaving the old act's lamps, rim and LUT stacked on the new look).
        float wStep = dt / FadeSeconds;
        for (int i = 0; i < weights.Length; i++) {
            weights[i] = Mathf.MoveTowards(weights[i], weightGoal[i], wStep);
        }
        TickMusic(dt);
    }

    void Change(int act, bool over, bool stage) {
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
        FadeSeconds = Tuning.ActFadeSeconds;
        if (stage && CinematicDirector.PlayAct(act, BannerTitle, null)) {
            StagedChanges++;
            FadeSeconds = CinematicDirector.ActSeconds * 0.8f;   // the sky, light and score finish just before the camera returns
        } else {
            Banner(BannerTitle, over ? OvertimeLine : Lines[act]);
            Sting(act, over);
        }
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

    // The banner owns the ActBanner slot (ScreenSlots): it waits while the boss card or the run-end sheet is up, and it
    // sits where ScreenSlots.FitTop finds no other text, re-checked a few times a second while it shows.
    string pendingTitle;
    string pendingLine;
    float pendingAge;
    float titleH;
    float lineH;
    float bannerWidth;
    float nextFit;

    void Banner(string title, string line) {
        if (ScreenSlots.Busy(Slot.BossCard) || ScreenSlots.Busy(Slot.RunEnd)) {
            pendingTitle = title;
            pendingLine = line;
            pendingAge = 0f;
            return;
        }
        pendingTitle = null;
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
        bannerWidth = compact ? UiFit.Size.x - 48f : 1400f;
        titleH = compact ? 56f : 90f;
        lineH = compact ? 36f : 50f;
        bannerTitle = MakeText(go.transform, "Title", title, compact ? 36 : 60, FontStyle.Bold, new Color(1f, 0.92f, 0.72f), bannerWidth, 0f, titleH);
        bannerLine = MakeText(go.transform, "Line", line, compact ? 22 : 32, FontStyle.Normal, DeptPalette.CreamSoft, bannerWidth, 0f, lineH);
        bannerLeft = Tuning.ActBannerSeconds;
        PlaceBanner();
    }

    void PlaceBanner() {
        if (bannerCanvas == null || bannerTitle == null) {
            return;
        }
        float s = ScreenSlots.UnitScale();
        bool clear;
        float top = ScreenSlots.FitTop(Slot.ActBanner, bannerWidth * s, (titleH + lineH) * s, bannerCanvas.transform, out clear);
        float cy = Screen.height * 0.5f;
        bannerTitle.rectTransform.anchoredPosition = new Vector2(0f, (top - titleH * s * 0.5f - cy) / s);
        bannerLine.rectTransform.anchoredPosition = new Vector2(0f, (top - titleH * s - lineH * s * 0.5f - cy) / s);
        BannerClear = clear;
    }

    // False only when no free spot was found at the last placement.
    public static bool BannerClear { get; private set; } = true;

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
        if (pendingTitle != null) {
            pendingAge += Time.unscaledDeltaTime;
            if (WaveDirector.IsLost || pendingAge > 8f) {
                pendingTitle = null;
            } else if (!ScreenSlots.Busy(Slot.BossCard)) {
                Banner(pendingTitle, pendingLine);
            }
        }
        if (bannerCanvas == null) {
            return;
        }
        if (Time.unscaledTime >= nextFit) {
            nextFit = Time.unscaledTime + 0.25f;
            PlaceBanner();
        }
        if (WaveDirector.IsLost) {
            bannerLeft = 0f;   // the run-end panel takes the screen
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
            GameAudio.NoteOther(GameAudio.Bus.Music, "act_score_" + (i + 1));   // the looping bed, told to the audio probe
            layers[i] = source;
            stings[i] = Chime("Act Sting " + (i + 1), notes[i][3] * 2f, i == 1 ? 1.335f : 1.5f);
        }
        var sg = new GameObject("Act Sting");
        sg.transform.SetParent(transform, false);
        stingSource = sg.AddComponent<AudioSource>();
        stingSource.spatialBlend = 0f;
        stingSource.playOnAwake = false;
        gameObject.AddComponent<ArcadeScore>();
        musicUp = true;
    }

    void TickMusic(float dt) {
        if (!musicUp) {
            return;
        }
        float level = SoundSettings.On ? SoundSettings.Music * Tuning.ActMusicLevel * (BossTheme ? 0.2f : 1f) * ArcadeScore.Dip : 0f;
        float mix = 0f;
        for (int i = 0; i < layers.Length; i++) {
            // Equal-power: the two layers of a crossfade never dip below one layer's level.
            float gain = Mathf.Sqrt(weights[i]);
            mix += gain;
            layers[i].volume = gain * level;
            layers[i].pitch = InOvertime ? 0.94f : 1f;
        }
        MixFloor = Mathf.Min(MixFloor, mix);
    }

    // A setup shortcut for the playtest: every act's scene may play again.
    public static void ForgetStaged() {
        if (Instance != null) {
            System.Array.Clear(Instance.staged, 0, Instance.staged.Length);
        }
    }

    // The scene stamps its card on the bar and calls this: the act's chime lands with it.
    public static void StingNow(int act) {
        if (Instance != null) {
            Instance.Sting(act, false);
        }
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
