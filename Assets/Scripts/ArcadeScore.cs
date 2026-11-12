using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Arcade's reactive score (Docs: world bible, A "three acts ... score"). Each act's looping pad (ArcadeActs) gets two stems made
// in the same key and on the same grid: a percussion layer and a brass lead. A danger value (bots within 6 m of Slice's circle,
// Slice's health, a boss on the field) brings layer 1 in at moderate danger and layer 2 at high danger. Every switch lands on a
// bar line of the pad, read off the pad's own sample position (so it holds if the pitch drops in Overtime). A wave clear sends both
// layers out over 2 s from the next bar line. A boss's phase change adds its own layer (a driving pulse and a siren) on the next
// bar; its death plays a 4 s cadence that resolves the act's chord.
// Grid: 150 bpm, four beats a bar (1.6 s), five bars to the 8 s pad loop (four bars of riff, one of turnaround).
// The stems are made in code once wave 1 is up, a clip a frame (no files, no download, so nothing streams before wave 1); a
// recorded set would arrive through MusicDirector.Fetch like the tracks. On the phone tier at most 3 sources play at once: the
// pad and two stems, so the boss layer takes the percussion's place.
public class ArcadeScore : MonoBehaviour {
    const int Rate = 22050;
    const int BarSamples = 35280;      // 1.6 s
    const int BeatSamples = 8820;      // 0.4 s
    const int LoopSamples = 176400;    // 8 s, five bars

    public const float NearMeters = 6f;
    public const float Layer1At = 0.3f, Layer1Off = 0.15f;
    public const float Layer2At = 0.6f, Layer2Off = 0.4f;
    const float HoldOffSeconds = 3f;
    const float FadeInSeconds = 0.4f;
    public const float FadeOutSeconds = 2f;
    public const float CadenceSeconds = 4f;
    const int Perc = 0, Lead = 1, BossStem = 2;

    // Same notes as the act pads (ArcadeActs.BuildAudio): A minor, D minor, A with a raised fifth.
    static readonly float[][] Keys = {
        new[] { 110f, 164.8f, 220f, 261.6f },
        new[] { 73.4f, 110f, 146.8f, 174.6f },
        new[] { 55f, 82.4f, 123.5f, 185f }
    };
    static readonly float[] StemGain = { 0.85f, 0.7f, 0.8f };

    public static ArcadeScore Instance { get; private set; }
    public static readonly List<string> Events = new List<string>();
    public static float Danger { get; private set; }
    public static int Near { get; private set; }
    public static int LayersOn { get; private set; }
    public static int MaxLayersOn { get; private set; }
    public static int LayerStarts { get; private set; }
    public static int BossLayerStarts { get; private set; }
    public static int Cadences { get; private set; }
    public static float LastCadenceSeconds { get; private set; }
    public static float LastClearFadeSeconds { get; private set; } = -1f;
    public static float BarLateMax { get; private set; }
    public static int StemFetches { get { return 0; } }
    public static bool Prepared { get; private set; }
    public static int PreparedAtWave { get; private set; } = -1;
    public static float Dip { get; private set; } = 1f;
    public static int MaxStreams { get; private set; }

    AudioSource master;
    readonly AudioSource[] src = new AudioSource[3];
    readonly float[] gain = new float[3];
    readonly float[] goal = new float[3];
    readonly bool[] gate = new bool[3];
    readonly float[] offTimer = new float[3];
    readonly AudioClip[] clips = new AudioClip[9];
    readonly AudioClip[] cadence = new AudioClip[3];
    AudioSource cadenceSource;
    int preparedAct = -1;
    int preparing = -1;
    int lastBar = -1;
    int stemAct = -1;
    bool wasClear = true;
    float clearAt = -1f;
    float cadenceLeft;
    float danger;
    Enemy trackedBoss;
    bool bossP2;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() {
        Instance = null;
        Events.Clear();
        Danger = 0f;
        Near = LayersOn = MaxLayersOn = LayerStarts = BossLayerStarts = Cadences = 0;
        LastCadenceSeconds = 0f;
        LastClearFadeSeconds = -1f;
        BarLateMax = 0f;
        Prepared = false;
        PreparedAtWave = -1;
        Dip = 1f;
        MaxStreams = 0;
    }

    void Awake() {
        Instance = this;
        for (int i = 0; i < 3; i++) {
            var go = new GameObject("Score Stem " + i);
            go.transform.SetParent(transform, false);
            AudioSource s = go.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;
            src[i] = s;
        }
        var cg = new GameObject("Score Cadence");
        cg.transform.SetParent(transform, false);
        cadenceSource = cg.AddComponent<AudioSource>();
        cadenceSource.playOnAwake = false;
        cadenceSource.spatialBlend = 0f;
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    public static int ExtraStemCap { get { return UiFit.Touch ? 2 : 3; } }

    void Update() {
        if (ChapterRun.Instance != null) {
            return;
        }
        if (master == null && ArcadeActs.Instance != null) {
            master = ArcadeActs.Instance.Master;
        }
        int act = Mathf.Clamp(ArcadeActs.Act, 0, 2);
        if (master == null || ArcadeActs.Act < 0 || WaveDirector.Wave < 1) {
            return;
        }
        if (preparing < 0 && preparedAct != act) {
            preparing = act;
            if (PreparedAtWave < 0) {
                PreparedAtWave = WaveDirector.Wave;
            }
            StartCoroutine(Prepare(act));
        }
        float dt = Time.unscaledDeltaTime;

        Sense(dt);
        TickBars();
        TickGains(dt);
        TickCadence(dt);
    }

    // ---- danger ----

    void Sense(float dt) {
        Musician slice = Musician.Instance;
        Vector3 centre = slice != null ? slice.transform.position : Vector3.zero;
        int near = 0, alive = 0;
        Enemy boss = null;
        var roster = Enemy.Roster;
        for (int i = 0; i < roster.Count; i++) {
            Enemy e = roster[i];
            if (e == null || !e.Alive) {
                continue;
            }
            alive++;
            Vector3 d = e.transform.position - centre;
            d.y = 0f;
            if (d.sqrMagnitude <= NearMeters * NearMeters) {
                near++;
            }
            if (e.Boss) {
                boss = e;
            }
        }
        Near = near;
        float hpLow = slice != null && slice.MaxHealth > 0 ? 1f - Mathf.Clamp01((float)slice.Health / slice.MaxHealth) : 0f;
        float raw = Mathf.Clamp01(near / 6f + hpLow * 0.4f + (boss != null ? 0.4f : 0f) + (boss != null && boss.Phase >= 2 ? 0.2f : 0f));
        danger = raw > danger ? raw : Mathf.MoveTowards(danger, raw, dt * 0.3f);
        Danger = danger;

        bool clear = WaveDirector.IsBuilding || alive == 0 || WaveDirector.IsLost;
        if (clear && !wasClear) {
            clearAt = Time.unscaledTime;
            Events.Add("clear|wave " + WaveDirector.Wave);
        }
        wasClear = clear;

        // Boss: its phase change adds a layer, its death resolves the act.
        if (boss != null) {
            if (trackedBoss != boss) {
                trackedBoss = boss;
                bossP2 = false;
            }
            if (boss.Phase >= 2 && !bossP2) {
                bossP2 = true;
                Events.Add("boss-phase2|" + boss.Title + "|wave " + WaveDirector.Wave);
            }
        } else if (trackedBoss != null || bossP2) {
            bool died = trackedBoss != null && !trackedBoss.Alive && !WaveDirector.IsLost;
            trackedBoss = null;
            bossP2 = false;
            if (died) {
                PlayCadence();
            }
        }

        bool boss2 = bossP2 && !clear;
        Hysteresis(Perc, !clear && danger >= Layer1At, danger < Layer1Off || clear, dt, clear);
        Hysteresis(Lead, !clear && danger >= Layer2At, danger < Layer2Off || clear, dt, clear);
        gate[BossStem] = boss2;
        if (boss2 && ExtraStemCap < 3) {
            gate[Perc] = false;   // the phone tier: pad + lead + boss layer, three sources
        }
        if (cadenceLeft > 0f) {
            gate[Perc] = gate[Lead] = gate[BossStem] = false;
        }
    }

    void Hysteresis(int i, bool up, bool belowOff, float dt, bool clear) {
        if (up) {
            gate[i] = true;
            offTimer[i] = 0f;
        } else if (gate[i]) {
            offTimer[i] = belowOff ? offTimer[i] + dt : 0f;
            if (clear || offTimer[i] >= HoldOffSeconds) {
                gate[i] = false;
            }
        }
    }

    // ---- bar lines ----

    void TickBars() {
        if (!master.isPlaying) {
            return;
        }
        int pos = master.timeSamples;
        int bar = pos / BarSamples;
        if (bar == lastBar) {
            return;
        }
        lastBar = bar;
        float late = (pos % BarSamples) / (float)Rate;
        BarLateMax = Mathf.Max(BarLateMax, late);
        int act = Mathf.Clamp(ArcadeActs.Act, 0, 2);
        for (int i = 0; i < 3; i++) {
            float want = gate[i] ? 1f : 0f;
            if (want == goal[i]) {
                continue;
            }
            if (want > 0f) {
                AudioClip clip = clips[act * 3 + i];
                if (clip == null) {
                    continue;   // not made yet: tried again next bar
                }
                AudioSource s = src[i];
                if (!s.isPlaying || stemAct != act || s.clip != clip) {
                    s.clip = clip;
                    s.volume = 0f;
                    s.Play();
                    s.timeSamples = Mathf.Clamp(pos % LoopSamples, 0, clip.samples - 1);
                }
                stemAct = act;
                LayerStarts++;
                if (i == BossStem) {
                    BossLayerStarts++;
                }
            }
            goal[i] = want;
            Events.Add("layer|" + (i == Perc ? "perc" : i == Lead ? "lead" : "boss") + "|" + (want > 0f ? "in" : "out") + "|bar " + bar + "|late " + late.ToString("0.000")
                + "|near " + Near + "|danger " + danger.ToString("0.00"));
        }
    }

    void TickGains(float dt) {
        float level = SoundSettings.On ? SoundSettings.Music * Tuning.ActMusicLevel * (ArcadeActs.BossTheme ? 0.2f : 1f) : 0f;
        int on = 0, playing = 0;
        bool wasAudible = false;
        for (int i = 0; i < 3; i++) {
            float rate = goal[i] > gain[i] ? 1f / FadeInSeconds : (cadenceLeft > 0f ? 1f / 0.6f : 1f / FadeOutSeconds);
            if (gain[i] > 0.001f) {
                wasAudible = true;
            }
            gain[i] = Mathf.MoveTowards(gain[i], goal[i], dt * rate);
            AudioSource s = src[i];
            s.volume = gain[i] * level * StemGain[i];
            s.pitch = ArcadeActs.InOvertime ? 0.94f : 1f;
            if (goal[i] > 0f) {
                on++;
            }
            if (gain[i] <= 0.001f && goal[i] <= 0f && s.isPlaying) {
                s.Stop();
            }
            if (s.isPlaying) {
                playing++;
            }
        }
        LayersOn = on;
        MaxLayersOn = Mathf.Max(MaxLayersOn, on);
        MaxStreams = Mathf.Max(MaxStreams, playing + 1);
        if (wasAudible && gain[Perc] <= 0.001f && gain[Lead] <= 0.001f && gain[BossStem] <= 0.001f && clearAt >= 0f) {
            LastClearFadeSeconds = Time.unscaledTime - clearAt;
            Events.Add("layers-silent|" + LastClearFadeSeconds.ToString("0.0") + " s after the clear");
            clearAt = -1f;
        }
    }

    // ---- boss cadence ----

    void PlayCadence() {
        int act = Mathf.Clamp(ArcadeActs.Act, 0, 2);
        AudioClip clip = cadence[act];
        Cadences++;
        Events.Add("cadence|act " + (act + 1) + "|wave " + WaveDirector.Wave + "|" + (clip != null ? clip.length.ToString("0.0") : "0") + " s");
        if (clip == null) {
            return;
        }
        LastCadenceSeconds = clip.length;
        cadenceLeft = clip.length;
        cadenceSource.clip = clip;
        cadenceSource.volume = SoundSettings.On ? Mathf.Clamp01(SoundSettings.Music * 0.9f) : 0f;
        cadenceSource.Play();
    }

    void TickCadence(float dt) {
        if (cadenceLeft > 0f) {
            cadenceLeft -= dt;
            Dip = Mathf.MoveTowards(Dip, 0.3f, dt * 3f);
        } else {
            Dip = Mathf.MoveTowards(Dip, 1f, dt / 1.5f);
        }
    }

    // ---- making the stems ----

    IEnumerator Prepare(int act) {
        for (int i = 0; i < 3; i++) {
            if (clips[act * 3 + i] == null) {
                clips[act * 3 + i] = MakeStem(act, i);
                yield return null;
            }
        }
        if (cadence[act] == null) {
            cadence[act] = MakeCadence(act);
            yield return null;
        }
        preparedAct = act;
        preparing = -1;
        Prepared = true;
        Events.Add("stems-ready|act " + (act + 1) + "|wave " + WaveDirector.Wave);
    }

    static AudioClip MakeStem(int act, int stem) {
        var d = new float[LoopSamples];
        float[] k = Keys[act];
        uint seed = 7919u + (uint)(act * 101 + stem * 13);
        if (stem == Perc) {
            for (int beat = 0; beat < 20; beat++) {
                int at = beat * BeatSamples;
                Kick(d, at, 1f);
                if (beat % 4 == 1 || beat % 4 == 3) {
                    Snare(d, at, ref seed, 0.8f);
                }
                for (int h = 0; h < (act == 0 ? 2 : 4); h++) {
                    Hat(d, at + h * BeatSamples / (act == 0 ? 2 : 4), ref seed, h == 0 ? 0.35f : 0.22f);
                }
            }
            for (int s16 = 0; s16 < 4; s16++) {   // the turnaround bar's last beat: a snare roll
                Snare(d, 19 * BeatSamples + s16 * BeatSamples / 4, ref seed, 0.55f + s16 * 0.12f);
            }
        } else if (stem == Lead) {
            int[][] riff = { new[] { 0, 3, 4, 6 }, new[] { 0, 2, 4, 7 }, new[] { 0, 3, 5, 6 }, new[] { 0, 2, 4, 5 } };
            int[] degree = { 1, 0, 2, 3, 2, 0, 1, 3 };
            for (int bar = 0; bar < 4; bar++) {
                for (int n = 0; n < 4; n++) {
                    int eighth = riff[bar][n];
                    float f = k[degree[(bar * 2 + n) % degree.Length]] * 2f;
                    float len = n % 2 == 0 ? 0.6f : 0.3f;
                    Brass(d, bar * BarSamples + eighth * (BeatSamples / 2), f, len, 0.8f);
                }
            }
            for (int q = 0; q < 4; q++) {   // the turnaround: four rising quarters
                Brass(d, 4 * BarSamples + q * BeatSamples, k[q] * 2f, 0.38f, 0.8f);
            }
        } else {
            // Boss layer: a sixteenth-note pulse under everything, an octave jump on the beat, and a siren sweep each bar.
            int sixteenth = BeatSamples / 4;
            for (int i = 0; i < 80; i++) {
                float f = k[0] * (i % 4 == 0 ? 1f : 0.5f);
                Pluck(d, i * sixteenth, f, 0.1f, i % 4 == 0 ? 0.9f : 0.5f);
            }
            for (int bar = 0; bar < 5; bar++) {
                Siren(d, bar * BarSamples, k[3] * 2f, BarSamples, 0.35f);
            }
            for (int beat = 0; beat < 20; beat++) {
                Kick(d, beat * BeatSamples + BeatSamples / 2, 0.7f);
            }
        }
        return Make("Score " + (stem == Perc ? "Perc " : stem == Lead ? "Lead " : "Boss ") + (act + 1), d, 0.8f);
    }

    // Four seconds: the act's chord struck with a kick and a crash, held, and left to ring out.
    static AudioClip MakeCadence(int act) {
        int n = Mathf.RoundToInt(CadenceSeconds * Rate);
        var d = new float[n];
        uint seed = 4242u + (uint)act;
        float[] k = Keys[act];
        Kick(d, 0, 1.1f, false);
        for (int i = 0; i < 4; i++) {
            Brass(d, i * 2200, k[i] * (i == 3 ? 2f : 1f), 3.6f, 0.9f, false);
        }
        Brass(d, 0, k[0] * 0.5f, 3.8f, 1.0f, false);
        for (int i = 0; i < n; i++) {
            float t = i / (float)Rate;
            seed = seed * 1664525u + 1013904223u;
            float noise = ((seed >> 8) / (float)(1 << 24)) * 2f - 1f;
            d[i] += noise * Mathf.Exp(-t * 3.2f) * 0.25f;
        }
        int fade = Mathf.RoundToInt(0.5f * Rate);
        for (int i = 0; i < fade; i++) {
            d[n - 1 - i] *= i / (float)fade;
        }
        return Make("Score Cadence " + (act + 1), d, 0.85f);
    }

    static AudioClip Make(string name, float[] d, float peakTo) {
        float peak = 0.0001f;
        for (int i = 0; i < d.Length; i++) {
            peak = Mathf.Max(peak, Mathf.Abs(d[i]));
        }
        float g = peakTo / peak;
        for (int i = 0; i < d.Length; i++) {
            d[i] *= g;
        }
        AudioClip clip = AudioClip.Create(name, d.Length, 1, Rate, false);
        clip.SetData(d, 0);
        return clip;
    }

    // Writes v at a position; a note that runs past the loop's end wraps to its start, so the seam is whole.
    static void Put(float[] d, int index, float v, bool wrap) {
        if (index >= d.Length) {
            if (!wrap) {
                return;
            }
            index %= d.Length;
        }
        d[index] += v;
    }

    static void Kick(float[] d, int at, float amount, bool wrap = true) {
        int n = Rate / 5;
        float phase = 0f;
        for (int i = 0; i < n; i++) {
            float t = i / (float)Rate;
            float f = 42f + 90f * Mathf.Exp(-t * 28f);
            phase += 2f * Mathf.PI * f / Rate;
            Put(d, at + i, Mathf.Sin(phase) * Mathf.Exp(-t * 14f) * amount * Mathf.Min(1f, t * 600f), wrap);
        }
    }

    static void Snare(float[] d, int at, ref uint seed, float amount) {
        int n = Rate / 6;
        for (int i = 0; i < n; i++) {
            float t = i / (float)Rate;
            seed = seed * 1664525u + 1013904223u;
            float noise = ((seed >> 8) / (float)(1 << 24)) * 2f - 1f;
            float v = noise * Mathf.Exp(-t * 22f) * 0.7f + Mathf.Sin(2f * Mathf.PI * 190f * t) * Mathf.Exp(-t * 30f) * 0.5f;
            Put(d, at + i, v * amount, true);
        }
    }

    static void Hat(float[] d, int at, ref uint seed, float amount) {
        int n = Rate / 25;
        float prev = 0f;
        for (int i = 0; i < n; i++) {
            float t = i / (float)Rate;
            seed = seed * 1664525u + 1013904223u;
            float noise = ((seed >> 8) / (float)(1 << 24)) * 2f - 1f;
            float hp = noise - prev;   // a one-pole high-pass: all hiss, no body
            prev = noise;
            Put(d, at + i, hp * Mathf.Exp(-t * 90f) * amount * 0.5f, true);
        }
    }

    static void Brass(float[] d, int at, float hz, float seconds, float amount, bool wrap = true) {
        int n = Mathf.RoundToInt(seconds * Rate);
        for (int i = 0; i < n; i++) {
            float t = i / (float)Rate;
            float vib = 1f + 0.004f * Mathf.Sin(2f * Mathf.PI * 5.5f * t) * Mathf.Clamp01(t * 4f);
            float v = 0f;
            for (int h = 1; h <= 6; h++) {
                v += Mathf.Sin(2f * Mathf.PI * hz * vib * h * t + 0.3f * h) / h;
            }
            // A brass swell: a little bite on the attack, then it settles and falls away.
            float env = Mathf.Min(1f, t / 0.035f) * Mathf.Exp(-t * (2.2f / seconds)) * (1f + 0.35f * Mathf.Exp(-t * 18f));
            Put(d, at + i, v * env * amount * 0.5f, wrap);
        }
    }

    static void Pluck(float[] d, int at, float hz, float seconds, float amount) {
        int n = Mathf.RoundToInt(seconds * Rate);
        for (int i = 0; i < n; i++) {
            float t = i / (float)Rate;
            float v = Mathf.Sin(2f * Mathf.PI * hz * t) + 0.5f * Mathf.Sin(4f * Mathf.PI * hz * t) + 0.25f * Mathf.Sin(6f * Mathf.PI * hz * t);
            Put(d, at + i, v * Mathf.Exp(-t * 26f) * Mathf.Min(1f, t * 400f) * amount * 0.5f, true);
        }
    }

    static void Siren(float[] d, int at, float hz, int samples, float amount) {
        float phase = 0f;
        for (int i = 0; i < samples; i++) {
            float x = i / (float)samples;
            float f = hz * (1f + 0.25f * Mathf.Sin(Mathf.PI * x));
            phase += 2f * Mathf.PI * f / Rate;
            float env = Mathf.Sin(Mathf.PI * x);
            Put(d, at + i, (Mathf.Sin(phase) + 0.3f * Mathf.Sin(phase * 2f)) * env * amount * 0.5f, true);
        }
    }
}
