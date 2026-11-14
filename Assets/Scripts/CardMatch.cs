using System.Collections.Generic;
using UnityEngine;

// Stepping from a chapter's painted loading card into its 3D scene should feel like stepping into the same picture (J: the critic
// said the 3D scenes "don't match the strong 2D chapter cards", and called the Chapter 1 yard flat blue with bare walls).
//
// Each card (Resources/LoadCards/card_<id>.png, the six chapters and the hub) has a dominant palette: its mean sRGB colour, measured
// once from the art (Tools/Cards/card_mean.py reads the PNGs; the numbers are below). A place's scene is graded toward it in two steps:
//   1. Fog and ambient are pulled a share of the way to the card's colour when the scene starts (the light the world is made of).
//   2. A short calibration at the camera: for the first seconds the camera's own frame is read (a tiny render through the real grade),
//      and a per-channel gain on NightGrade's split-tone tints is nudged until the frame's mean colour sits near the card's. The gain is
//      capped, so a warm key light and cool shadows still read inside the mean (the yard keeps its lamp-lit warm patches).
// Delta is CIE76 in Lab between the card's mean and the frame's mean; `grade.cardDeltaE` in the playtests is this number.
// Campaign only: Arcade never attaches one.
public class CardMatch : MonoBehaviour {
    struct Entry { public string Id; public Color Mean; public float Pull; }

    // Mean colour of each card (sRGB 0..1, 8 px grid over the 1024 px art), and how far fog and ambient move toward it (0..1).
    static readonly Entry[] Cards = {
        new Entry { Id = "ch1", Mean = new Color(0.218f, 0.378f, 0.544f, 1f), Pull = 0.35f },   // the yard: teal-blue night, lamp amber in the gain's reach
        new Entry { Id = "ch2", Mean = new Color(0.744f, 0.516f, 0.354f, 1f), Pull = 0.45f },   // the gate: postmark orange dusk
        new Entry { Id = "ch3", Mean = new Color(0.750f, 0.694f, 0.617f, 1f), Pull = 0.40f },   // the factory floor: pale warm concrete
        new Entry { Id = "ch4", Mean = new Color(0.452f, 0.519f, 0.589f, 1f), Pull = 0.40f },   // the long walk: grey-blue fog
        new Entry { Id = "ch5", Mean = new Color(0.576f, 0.483f, 0.269f, 1f), Pull = 0.45f },   // the reading room: paper and lamp gold
        new Entry { Id = "ch6", Mean = new Color(0.238f, 0.340f, 0.432f, 1f), Pull = 0.35f },   // the filed hollow: deep blue
        new Entry { Id = "hub", Mean = new Color(0.546f, 0.492f, 0.402f, 1f), Pull = 0.40f },   // the office: lamplight on paper
    };

    public const float Budget = 15f;                 // the criterion: card and first play frame under this far apart
    public const float MaxGain = 2.2f, MinGain = 0.45f;
    public const int Passes = 8;
    const float PassSeconds = 0.5f, FirstPass = 0.8f;

    public static CardMatch Instance { get; private set; }
    public static readonly List<string> Events = new List<string>();

    public string CardId { get; private set; }
    public Color Target { get; private set; }
    public Color Gain { get; private set; } = Color.white;
    public float LastDelta { get; private set; } = -1f;   // the last frame read through this component
    public int PassesDone { get; private set; }

    NightGrade grade;
    Camera cam;
    float pull;
    float nextPass;
    RenderTexture rt;
    Texture2D tex;

    public static bool Known(string id) { return Find(id, out _); }

    static bool Find(string id, out Entry entry) {
        foreach (Entry e in Cards) {
            if (e.Id == id) {
                entry = e;
                return true;
            }
        }
        entry = default;
        return false;
    }

    public static Color TargetOf(string id) {
        return Find(id, out Entry e) ? e.Mean : Color.gray;
    }

    public static string IdOfChapter(int chapter) { return "ch" + (chapter + 1); }

    // Put the match on a camera for a place (a chapter's card id "ch1".."ch6", or "hub"). A second call restarts it.
    public static CardMatch Attach(Camera camera, string id) {
        if (camera == null || !Find(id, out Entry entry)) {
            return null;
        }
        var match = camera.GetComponent<CardMatch>();
        if (match == null) {
            match = camera.gameObject.AddComponent<CardMatch>();
        }
        match.Begin(camera, id, entry);
        return match;
    }

    void Begin(Camera camera, string id, Entry entry) {
        cam = camera;
        CardId = id;
        Target = entry.Mean;
        pull = entry.Pull;
        grade = cam.GetComponent<NightGrade>();
        Gain = Color.white;
        if (grade != null) {
            grade.CardGain = Color.white;
        }
        PassesDone = 0;
        nextPass = Time.unscaledTime + FirstPass;
        Instance = this;
        Events.Add("attach|" + id);
    }

    void Update() {
        if (cam == null || Time.unscaledTime < nextPass) {
            return;
        }
        if (grade == null) {
            grade = cam.GetComponent<NightGrade>();
            if (grade == null) {
                nextPass = Time.unscaledTime + PassSeconds;
                return;
            }
        }
        if (PassesDone >= Passes) {
            return;
        }
        if (PassesDone == 1 || PassesDone == 5) {
            // The map's own lighting lands some frames after the scene starts (ChapterMap.Apply): pull the light after it, twice.
            PullLight();
        }
        nextPass = Time.unscaledTime + PassSeconds;
        Calibrate();
    }

    // Fog and ambient move toward the card's palette: the air and the fill light are what a scene's mean colour is mostly made of.
    void PullLight() {
        Color t = Target;
        if (RenderSettings.fog) {
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, t * 0.55f, pull * 0.6f);
        }
        if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Trilight) {
            RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, t * 0.6f, pull);
            RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor, t * 0.75f, pull);
            RenderSettings.ambientGroundColor = Color.Lerp(RenderSettings.ambientGroundColor, t * 0.4f, pull * 0.6f);
        } else if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Flat) {
            RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, t * 0.7f, pull);
        }
        Events.Add("light|" + CardId + "|pull " + pull.ToString("0.00"));
    }

    // The camera's frame through the real grade, as a mean colour (and the share of it that is a lit pixel, for the playtest).
    public static Color MeanOfFrame(Camera camera, ref RenderTexture target, ref Texture2D readback) {
        const int W = 64, H = 36;
        if (target == null) {
            target = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { hideFlags = HideFlags.HideAndDontSave };
            readback = new Texture2D(W, H, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
        }
        RenderTexture old = camera.targetTexture, active = RenderTexture.active;
        camera.targetTexture = target;
        camera.Render();
        camera.targetTexture = old;
        RenderTexture.active = target;
        readback.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        readback.Apply();
        RenderTexture.active = active;
        Color sum = Color.black;
        foreach (Color32 c in readback.GetPixels32()) {
            sum += new Color(c.r / 255f, c.g / 255f, c.b / 255f, 0f);
        }
        sum /= W * H;
        sum.a = 1f;
        return sum;
    }

    void Calibrate() {
        Color mean = MeanOfFrame(cam, ref rt, ref tex);
        float delta = DeltaE(mean, Target);
        LastDelta = delta;
        PassesDone++;
        // Move the gain by the ratio of target to measured, per channel, tamed so the loop settles instead of ringing.
        Color g = Gain;
        g.r = Mathf.Clamp(g.r * Mathf.Pow(Target.r / Mathf.Max(0.02f, mean.r), 0.75f), MinGain, MaxGain);
        g.g = Mathf.Clamp(g.g * Mathf.Pow(Target.g / Mathf.Max(0.02f, mean.g), 0.75f), MinGain, MaxGain);
        g.b = Mathf.Clamp(g.b * Mathf.Pow(Target.b / Mathf.Max(0.02f, mean.b), 0.75f), MinGain, MaxGain);
        Gain = g;
        grade.CardGain = g;
        Events.Add("pass|" + CardId + "|" + PassesDone + "|dE " + delta.ToString("0.0"));
    }

    // Restart the calibration (a cutscene ended, the camera moved to the play view).
    public void Recalibrate() {
        PassesDone = 0;
        nextPass = Time.unscaledTime + 0.1f;
    }

    void OnDestroy() {
        if (rt != null) {
            rt.Release();
            Destroy(rt);
        }
        if (tex != null) {
            Destroy(tex);
        }
        if (Instance == this) {
            Instance = null;
        }
    }

    // ------------------------------------------------------------------ Colour distance

    static Vector3 Lab(Color c) {
        float r = Lin(c.r), g = Lin(c.g), b = Lin(c.b);
        float x = (0.4124f * r + 0.3576f * g + 0.1805f * b) / 0.95047f;
        float y = 0.2126f * r + 0.7152f * g + 0.0722f * b;
        float z = (0.0193f * r + 0.1192f * g + 0.9505f * b) / 1.08883f;
        float fx = F(x), fy = F(y), fz = F(z);
        return new Vector3(116f * fy - 16f, 500f * (fx - fy), 200f * (fy - fz));
    }

    static float Lin(float v) { return v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f); }
    static float F(float t) { return t > 0.008856f ? Mathf.Pow(t, 1f / 3f) : 7.787f * t + 16f / 116f; }

    // CIE76 between two sRGB colours (what `grade.cardDeltaE` reports).
    public static float DeltaE(Color a, Color b) {
        return Vector3.Distance(Lab(a), Lab(b));
    }
}
