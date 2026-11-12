using System.Collections.Generic;
using UnityEngine;

// Each bot family's own sound: a positional movement loop (Yo Bot servo whirr, Spider Bot leg skitter, Mega Yo Bot hydraulic
// tread), a wind-up tell 0.4-0.8 s before the blow (whine, hiss, ratchet) and a death (pop and fizz, crumple, long collapse).
// Clips are synthesised (SfxSynth "bot_*", three variants each, never the same twice) until Hawkes Gen files named
// Resources/Sfx/bot_<name>[_2..] arrive. At most MaxLoops movement loops play at once, nearest bots first.
public class BotSound : MonoBehaviour {
    public const int MaxLoops = 6;
    public static int LoopsNow { get; private set; }
    public static int PeakLoops { get; private set; }
    public static int Tells { get; private set; }
    public static int Deaths { get; private set; }
    public static readonly HashSet<string> Families = new HashSet<string>();
    public static readonly List<string> Log = new List<string>();

    static BotSound instance;
    readonly AudioSource[] sources = new AudioSource[MaxLoops];
    readonly Enemy[] owner = new Enemy[MaxLoops];
    readonly List<Enemy> near = new List<Enemy>();
    float nextPoll;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() {
        instance = null;
        LoopsNow = PeakLoops = Tells = Deaths = 0;
        Families.Clear();
        Log.Clear();
    }

    public static string Family(Enemy e) {
        if (e.Boss) return "boss";
        if (e.Kind == BotKind.MegaYoBot) return "mega";
        return e.IsSpiderFamily ? "spider" : "yo";
    }

    public static void Tell(Enemy e) {
        string f = Family(e);
        Tells++;
        Families.Add(f);
        GameAudio.PlayAt("bot_tell_" + f, e.transform.position, 0.7f, 1f);
        if (Log.Count < 300) Log.Add(Time.unscaledTime.ToString("0.00") + "|tell|" + f);
    }

    public static void Death(Enemy e) {
        string f = Family(e);
        Deaths++;
        GameAudio.PlayAt("bot_death_" + f, e.transform.position, f == "boss" ? 0.9f : 0.6f, 1f);
        if (Log.Count < 300) Log.Add(Time.unscaledTime.ToString("0.00") + "|death|" + f);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() {
        if (instance == null) {
            instance = new GameObject("Bot Sound").AddComponent<BotSound>();
            DontDestroyOnLoad(instance.gameObject);
        }
    }

    void Update() {
        if (Time.unscaledTime < nextPoll) return;
        nextPoll = Time.unscaledTime + 0.2f;
        var cam = Camera.main;
        float level = SoundSettings.On ? SoundSettings.EffectsLevel : 0f;
        near.Clear();
        if (cam != null && level > 0f && Time.timeScale > 0.1f) {
            Vector3 at = cam.transform.position;
            foreach (Enemy e in Enemy.Roster) {
                if (e != null && e.isActiveAndEnabled && (e.transform.position - at).sqrMagnitude < 45f * 45f) near.Add(e);
            }
            near.Sort((a, b) => (a.transform.position - at).sqrMagnitude.CompareTo((b.transform.position - at).sqrMagnitude));
        }
        int active = 0;
        for (int i = 0; i < MaxLoops; i++) {
            Enemy e = i < near.Count ? near[i] : null;
            AudioSource s = sources[i];
            if (e == null) {
                if (s != null && s.isPlaying) s.Stop();
                owner[i] = null;
                continue;
            }
            if (s == null) {
                s = new GameObject("Bot Loop " + i).AddComponent<AudioSource>();
                s.transform.SetParent(transform, false);
                s.loop = true;
                s.playOnAwake = false;
                s.spatialBlend = 1f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 3f;
                s.maxDistance = 40f;
                s.dopplerLevel = 0f;
                sources[i] = s;
            }
            string f = Family(e);
            if (owner[i] != e || !s.isPlaying) {
                s.clip = GameAudio.ClipOf("bot_move_" + (f == "boss" ? "mega" : f));
                owner[i] = e;
                if (s.clip == null) continue;
                s.time = Random.value * s.clip.length * 0.9f;
                s.Play();
            }
            s.transform.position = e.transform.position;
            s.volume = (f == "mega" || f == "boss" ? 0.32f : 0.2f) * level;
            active++;
            Families.Add(f);
        }
        LoopsNow = active;
        PeakLoops = Mathf.Max(PeakLoops, active);
    }

    public static AudioClip Make(string name, int i) {
        var rng = new System.Random(101 + i * 13 + name.Length * 7);
        float v = 1f + i * 0.17f;
        float lp = 0f;
        const float TwoPi = 2f * Mathf.PI;
        switch (name) {
            case "bot_move_yo":
                return SfxSynth.Build(name + i, 1f, (s, t) => (Mathf.Sin(TwoPi * 520f * v * t + 2f * Mathf.Sin(TwoPi * 8f * t)) * 0.5f + Mathf.Sin(TwoPi * 1040f * v * t) * 0.15f) * (0.7f + 0.3f * Mathf.Sin(TwoPi * 6f * t)));
            case "bot_move_spider":
                return SfxSynth.Build(name + i, 0.8f, (s, t) => {
                    float ph = (t * (10f + i * 2f)) % 1f;
                    float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                    lp += (n - lp) * 0.5f;
                    return (lp * Mathf.Exp(-ph * 40f) + Mathf.Sin(TwoPi * 1900f * t) * Mathf.Exp(-ph * 60f) * 0.4f) * 0.8f;
                });
            case "bot_move_mega":
                return SfxSynth.Build(name + i, 1.25f, (s, t) => {
                    float ph = (t * (0.8f + 0.0f)) % 1f;
                    float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                    lp += (n - lp) * 0.04f;
                    return (Mathf.Sin(TwoPi * (52f + 20f * Mathf.Exp(-ph * 12f) + i * 4f) * t) * Mathf.Exp(-ph * 7f) + lp * 2f * Mathf.Exp(-ph * 3f)) * 0.9f;
                });
            case "bot_tell_yo":
                return SfxSynth.Build(name + i, 0.45f, (s, t) => Mathf.Sin(TwoPi * (500f * v + 1400f * t) * t) * Mathf.Min(1f, t * 12f) * (0.3f + t * 1.2f) * 0.6f);
            case "bot_tell_spider":
                return SfxSynth.Build(name + i, 0.5f, (s, t) => {
                    float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                    lp += (n - lp) * (0.35f + 0.1f * i);
                    return (n - lp) * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t / 0.5f)) * 0.9f;
                });
            case "bot_tell_mega":
            case "bot_tell_boss":
                return SfxSynth.Build(name + i, 0.7f, (s, t) => {
                    float ph = (t * (14f + 30f * t * v)) % 1f;
                    float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                    return (n * 0.6f + Mathf.Sin(TwoPi * 240f * t) * 0.4f) * Mathf.Exp(-ph * 18f) * (0.5f + t);
                });
            case "bot_death_yo":
                return SfxSynth.Build(name + i, 0.6f, (s, t) => {
                    float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                    lp += (n - lp) * 0.5f;
                    return Mathf.Sin(TwoPi * (700f - 4000f * t * v * 0.5f) * t) * Mathf.Exp(-t * 30f) * 0.8f + (n - lp) * Mathf.Exp(-t * 5f) * 0.5f * (0.6f + 0.4f * Mathf.Sin(TwoPi * 40f * t));
                });
            case "bot_death_spider":
                return SfxSynth.Build(name + i, 0.9f, (s, t) => {
                    float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                    lp += (n - lp) * 0.3f;
                    float ph = (t * (18f + i * 3f)) % 1f;
                    return lp * Mathf.Exp(-t * 4f) * 0.8f + Mathf.Sin(TwoPi * 1700f * t) * Mathf.Exp(-ph * 50f) * Mathf.Exp(-t * 2f) * 0.5f;
                });
            default:   // bot_death_mega, bot_death_boss: a long mechanical collapse
                return SfxSynth.Build(name + i, 2.2f, (s, t) => {
                    float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                    lp += (n - lp) * 0.06f;
                    float ph = (t * 5f) % 1f;
                    return Mathf.Sin(TwoPi * (70f - 30f * t / 2.2f) * t) * Mathf.Exp(-t * 1.4f) * 0.8f + lp * 1.6f * Mathf.Exp(-t * 1.8f) + n * Mathf.Exp(-ph * 25f) * Mathf.Exp(-t) * 0.3f;
                });
        }
    }
}
