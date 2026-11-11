using System.Collections.Generic;
using UnityEngine;

// The stakes, seen and heard without a word (Docs/Story.md, "The stakes"). Slice holds the circle in Arcade and in the chapters
// that keep it; when a bot reaches her the circle's ring dims and flickers, the screen's edges drain toward grey (NightGrade.Stakes,
// one multiply-add in the final post pass, so it costs nothing on a phone and, being post, never covers a HUD or touch control),
// under 30% a low heartbeat layer rises under the music, and the first time a bot reaches her in a run Wallit says one short key line
// about her (WallitVoice.OnStakes, "stakes-line"). Everything is a function of her health, so it recovers as she heals.
// Added to the Musician by Musician.Start; Hurt calls OnSliceHit.
public class StakesFeel : MonoBehaviour {
    public const float LowFraction = 0.3f;
    const float RingRadius = 5.5f;
    const float RingWidth = 0.8f;

    public static readonly List<string> Events = new List<string>();
    public static StakesFeel Instance { get; private set; }
    public static int Hits { get; private set; }
    public static bool LowNow { get; private set; }
    public static bool LowLayerPlaying { get { return Instance != null && Instance.low != null && Instance.low.isPlaying && Instance.low.volume > 0.01f; } }
    public static float Drain { get { return Instance != null ? Instance.drain : 0f; } }
    public static float RingAlpha { get { return Instance != null ? Instance.ringAlpha : 0f; } }

    Musician slice;
    Material ringMat;
    Transform ring;
    AudioSource low;
    NightGrade grade;
    float drain;
    float ringAlpha = 0.5f;
    float hitPulse;
    float lowLevel;
    float nextCam;
    bool stakesSaid;
    bool wasLow;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
        Events.Clear();
        Instance = null;
        Hits = 0;
        LowNow = false;
    }

    public static void Ensure(Musician musician) {
        if (musician == null || musician.GetComponent<StakesFeel>() != null) {
            return;
        }
        musician.gameObject.AddComponent<StakesFeel>().slice = musician;
    }

    public static void Note(string what, string detail) {
        Events.Add(what + "|" + Time.unscaledTime.ToString("0.0") + "|" + detail);
    }

    void Awake() {
        Instance = this;
        Events.Clear();
        Hits = 0;
        LowNow = false;
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
            LowNow = false;
        }
        if (grade != null) {
            grade.Stakes = 0f;
        }
        if (ring != null) {
            Destroy(ring.gameObject);
        }
    }

    // A bot's blow landed on Slice (Musician.Hurt, after her health changed).
    public static void OnSliceHit(Musician m, int amount, Enemy by) {
        if (Instance == null || m == null) {
            return;
        }
        Instance.Hit(m, amount, by);
    }

    void Hit(Musician m, int amount, Enemy by) {
        Hits++;
        hitPulse = 1f;
        int pct = Mathf.RoundToInt(100f * m.Health / Mathf.Max(1, m.MaxHealth));
        if (Hits <= 60) {
            Note("slice-hit", "-" + amount + " -> " + m.Health + "/" + m.MaxHealth + " (" + pct + "%)" + (by != null ? " by " + by.name : ""));
        }
        if (!stakesSaid && m.Alive) {
            stakesSaid = true;
            WallitVoice.OnStakes();
        }
    }

    void Update() {
        if (slice == null) {
            return;
        }
        float max = Mathf.Max(1, slice.MaxHealth);
        float lost = Mathf.Clamp01(1f - slice.Health / max);
        bool isLow = slice.Alive && slice.Health <= max * LowFraction;
        if (isLow && !wasLow) {
            Note("slice-low", slice.Health + "/" + slice.MaxHealth + ": the warning layer starts");
        }
        wasLow = isLow;
        LowNow = isLow;

        hitPulse = Mathf.MoveTowards(hitPulse, 0f, Time.deltaTime / 0.9f);
        float target = Mathf.Clamp01(lost * 0.9f + hitPulse * 0.2f);
        drain = Mathf.MoveTowards(drain, target, Time.deltaTime * (target > drain ? 1.6f : 0.35f));
        if (grade == null && Time.unscaledTime >= nextCam) {
            nextCam = Time.unscaledTime + 1f;
            Camera cam = Camera.main;
            grade = cam != null ? cam.GetComponent<NightGrade>() : null;
        }
        if (grade != null) {
            grade.Stakes = drain;
        }

        TickRing(lost);
        TickLow(isLow);
    }

    // ---------------------------------------------------------------- the ring

    void TickRing(float lost) {
        if (ring == null) {
            BuildRing();
        }
        if (ring == null) {
            return;
        }
        Vector3 at = slice.transform.position;
        ring.position = new Vector3(at.x, GroundY(at) + 0.06f, at.z);
        float alpha = 0.55f * (1f - 0.65f * lost);
        float t = Time.time;
        if (hitPulse > 0f) {
            // a hard stutter while the blow is fresh
            alpha *= Mathf.Lerp(1f, Mathf.Floor(t * 16f) % 2f < 1f ? 0.15f : 0.9f, hitPulse);
        }
        if (LowNow) {
            alpha *= 0.55f + 0.45f * Mathf.PerlinNoise(t * 6f, 0.7f);
        }
        ringAlpha = alpha;
        Color gold = new Color(1f, 0.85f, 0.45f);
        Color grey = new Color(0.55f, 0.55f, 0.6f);
        Color c = Color.Lerp(gold, grey, Mathf.Clamp01(lost * 1.2f));
        c.a = alpha;
        ringMat.color = c;
    }

    void BuildRing() {
        Shader shader = SafeShader.Find("Sprites/Default");
        if (shader == null) {
            return;
        }
        const int seg = 56;
        var verts = new List<Vector3>();
        var cols = new List<Color>();
        var tris = new List<int>();
        float[] radii = { RingRadius - RingWidth, RingRadius, RingRadius + RingWidth };
        float[] alphas = { 0f, 1f, 0f };
        for (int i = 0; i <= seg; i++) {
            float a = i / (float)seg * Mathf.PI * 2f;
            for (int r = 0; r < 3; r++) {
                verts.Add(new Vector3(Mathf.Cos(a) * radii[r], 0f, Mathf.Sin(a) * radii[r]));
                cols.Add(new Color(1f, 1f, 1f, alphas[r]));
            }
        }
        for (int i = 0; i < seg; i++) {
            for (int r = 0; r < 2; r++) {
                int a = i * 3 + r, b = (i + 1) * 3 + r;
                // wound to face up
                tris.AddRange(new[] { a, b + 1, a + 1, a, b, b + 1 });
            }
        }
        var mesh = new Mesh { name = "Circle Ring" };
        mesh.SetVertices(verts);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        var go = new GameObject("Circle Ring");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        ringMat = new Material(shader);
        ringMat.renderQueue = 2995;
        mr.sharedMaterial = ringMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        ring = go.transform;
    }

    static float GroundY(Vector3 at) {
        if (Physics.Raycast(new Vector3(at.x, at.y + 20f, at.z), Vector3.down, out RaycastHit hit, 60f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            return hit.point.y;
        }
        return at.y;
    }

    // ---------------------------------------------------------------- the low layer

    void TickLow(bool isLow) {
        if (low == null) {
            if (!isLow) {
                return;
            }
            low = BuildLayer(transform);
        }
        lowLevel = Mathf.MoveTowards(lowLevel, isLow ? 1f : 0f, Time.unscaledDeltaTime / 1.2f);
        float master = SoundSettings.On ? SoundSettings.Music : 0f;
        low.volume = lowLevel * 0.6f * master;
        if (lowLevel <= 0f && low.isPlaying) {
            low.Pause();
        } else if (lowLevel > 0f && !low.isPlaying) {
            low.UnPause();
        }
    }

    const int Rate = 16000;

    // A heartbeat at 75 beats a minute (two soft thumps) over two close low notes that beat against each other: a dread under the
    // music. Eight seconds, whole cycles, so the loop joins clean.
    static AudioSource BuildLayer(Transform parent) {
        const float seconds = 8f;
        int count = Mathf.RoundToInt(seconds * Rate);
        var data = new float[count];
        float f1 = Mathf.Round(73f * seconds) / seconds, f2 = Mathf.Round(77f * seconds) / seconds;
        for (int i = 0; i < count; i++) {
            float t = i / (float)Rate;
            float into = t % 0.8f;
            float lub = Thump(into, 0f), dub = Thump(into, 0.2f) * 0.7f;
            float drone = (Mathf.Sin(2f * Mathf.PI * f1 * t) + Mathf.Sin(2f * Mathf.PI * f2 * t)) * 0.18f;
            data[i] = (lub + dub) * 0.7f + drone;
        }
        float peak = 0.0001f;
        for (int i = 0; i < count; i++) {
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }
        for (int i = 0; i < count; i++) {
            data[i] = data[i] / peak * 0.85f;
        }
        AudioClip clip = AudioClip.Create("Slice Low Layer", count, 1, Rate, false);
        clip.SetData(data, 0);
        var go = new GameObject("Slice Low Layer");
        go.transform.SetParent(parent, false);
        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.spatialBlend = 0f;
        src.playOnAwake = false;
        src.volume = 0f;
        src.Play();
        return src;
    }

    static float Thump(float t, float at) {
        float x = t - at;
        return x < 0f || x > 0.3f ? 0f : Mathf.Sin(2f * Mathf.PI * 52f * x) * Mathf.Exp(-x * 16f);
    }
}
