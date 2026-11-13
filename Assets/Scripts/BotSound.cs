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
    public static int Layers { get; private set; }
    // For the playtest: the movement clip each family loops and how far left and right of the camera its sources sat (-1..1).
    public static readonly Dictionary<string, string> MoveClip = new Dictionary<string, string>();
    public static readonly Dictionary<string, Vector2> PanRange = new Dictionary<string, Vector2>();
    public static readonly HashSet<string> LayerNames = new HashSet<string>();
    public static readonly HashSet<string> Families = new HashSet<string>();
    public static readonly List<string> Log = new List<string>();

    static BotSound instance;
    readonly AudioSource[] sources = new AudioSource[MaxLoops];
    readonly Enemy[] owner = new Enemy[MaxLoops];
    readonly List<Enemy> near = new List<Enemy>();
    readonly Vector3[] lastPos = new Vector3[MaxLoops];
    readonly float[] nextLayer = new float[MaxLoops];
    float nextPoll;
    float lastPollAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() {
        instance = null;
        LoopsNow = PeakLoops = Tells = Deaths = Layers = Hits = InWindow = 0;
        LayerNames.Clear();
        MoveClip.Clear();
        PanRange.Clear();
        DeathVariants.Clear();
        tellAt.Clear();
        Families.Clear();
        Log.Clear();
    }

    public static string Family(Enemy e) {
        if (e.Boss) return "boss";
        if (e.Kind == BotKind.MegaYoBot) return "mega";
        return e.IsSpiderFamily ? "spider" : "yo";
    }

    // Mega Yo Bots share the bosses' ratchet and long collapse (their own Gen files: bot_tell_boss, bot_death_boss).
    static string Heavy(string f) { return f == "mega" ? "boss" : f; }

    static readonly Dictionary<Enemy, float> tellAt = new Dictionary<Enemy, float>();
    public static int Hits { get; private set; }
    public static int InWindow { get; private set; }   // blows that landed 0.4-0.8 s after their tell
    public static float LastLead { get; private set; }
    public static readonly Dictionary<string, int> DeathVariants = new Dictionary<string, int>();

    // A wind-up starts (a regular bot's claw or leg going up, a boss's ground-mark tell): its audible tell plays now.
    public static void Tell(Enemy e) {
        if (e == null || e.Scripted) return;
        string f = Family(e);
        Tells++;
        Families.Add(f);
        tellAt[e] = Time.time;
        GameAudio.PlayAt("bot_tell_" + Heavy(f), e.transform.position, f == "yo" ? 0.7f : 0.85f, 1f);
        if (Log.Count < 300) Log.Add(Time.unscaledTime.ToString("0.00") + "|tell|" + f);
    }

    // A boss's long ground-mark wind-up: its ratchet plays for the last 0.6 s, so it still tells the blow 0.4-0.8 s ahead.
    public static void TellIn(Enemy e, float windSeconds) {
        if (e == null || e.Scripted) return;
        float delay = Mathf.Max(0f, windSeconds - 0.6f);
        if (instance != null && delay > 0.05f) instance.StartCoroutine(instance.TellLater(e, delay));
        else Tell(e);
    }

    System.Collections.IEnumerator TellLater(Enemy e, float delay) {
        yield return new WaitForSeconds(delay);
        if (e != null && !e.Scripted) Tell(e);
    }

    // The blow lands: how long after its tell was that? The criteria want 0.4-0.8 s so a player can react by ear.
    public static void Hit(Enemy e) {
        float at;
        if (e == null || !tellAt.TryGetValue(e, out at)) return;
        tellAt.Remove(e);
        Hits++;
        LastLead = Time.time - at;
        if (LastLead >= 0.4f && LastLead <= 0.8f) InWindow++;
        if (Log.Count < 300) Log.Add(Time.unscaledTime.ToString("0.00") + "|hit|" + Family(e) + "|lead " + LastLead.ToString("0.00"));
    }

    public static void Death(Enemy e) {
        string f = Family(e);
        Deaths++;
        tellAt.Remove(e);
        // GameAudio picks the take (three or more, never the one just played) and counts a repeat in GameAudio.Repeats.
        GameAudio.PlayAt("bot_death_" + Heavy(f), e.transform.position, f == "boss" ? 0.9f : 0.6f, 1f);
        int count;
        DeathVariants.TryGetValue(f, out count);
        DeathVariants[f] = count + 1;
        if (Log.Count < 300) Log.Add(Time.unscaledTime.ToString("0.00") + "|death|" + f);
    }

    // A footfall in its family's own voice: tiny metal ticks, spider clicks, heavy hydraulic steps that shake the low end.
    public static void Step(Enemy e, float volume) {
        string f = Family(e);
        string name = "bot_step_" + (f == "boss" ? "mega" : f);
        float v = f == "yo" ? 0.2f : f == "spider" ? 0.22f : 0.55f;
        GameAudio.PlayAt(name, e.transform.position, Mathf.Clamp(v * volume, 0.08f, 0.7f), 1f);
    }

    // A chapter's variant gets a small layer over its family sound: paper rustle, stamp clack, ticket printer.
    public static string LayerOf(Enemy e) {
        string t = e.Title;
        if (string.IsNullOrEmpty(t)) return null;
        if (t.StartsWith("Dispatch") || t.StartsWith("Clerk")) return "bot_layer_paper";
        if (t.StartsWith("Returned") || t.StartsWith("Sealed")) return "bot_layer_stamp";
        if (t.StartsWith("Reissue") || t.StartsWith("Queue-Skip")) return "bot_layer_ticket";
        return null;
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
        float span = Mathf.Max(0.05f, Time.unscaledTime - lastPollAt);
        lastPollAt = Time.unscaledTime;
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
                lastPos[i] = e.transform.position;
                nextLayer[i] = Time.unscaledTime + Random.Range(0.3f, 1.2f);
            }
            // The faster it goes the faster and louder it sounds: a Spider Bot's clicks scale with its speed, a standing bot goes quiet.
            Vector3 moved = e.transform.position - lastPos[i];
            moved.y = 0f;
            float speed = Mathf.Clamp(moved.magnitude / span, 0f, 12f);
            lastPos[i] = e.transform.position;
            float go = Mathf.Clamp01(speed / 1.2f);
            bool heavy = f == "mega" || f == "boss";
            s.pitch = Mathf.Lerp(0.8f, f == "spider" ? 1.5f : 1.25f, Mathf.Clamp01(speed / 6f)) * (heavy ? 0.8f : 1f);
            s.transform.position = e.transform.position;
            if (cam != null) {
                Vector3 toBot = e.transform.position - cam.transform.position;
                float pan = toBot.sqrMagnitude > 1f ? Vector3.Dot(cam.transform.right, toBot.normalized) : 0f;
                Vector2 r;
                if (!PanRange.TryGetValue(f, out r)) r = new Vector2(pan, pan);
                PanRange[f] = new Vector2(Mathf.Min(r.x, pan), Mathf.Max(r.y, pan));
                MoveClip[f] = s.clip != null ? s.clip.name : "";
            }
            s.volume = (heavy ? 0.32f : 0.2f) * Mathf.Lerp(0.25f, 1f, go) * level;
            if (Time.unscaledTime >= nextLayer[i]) {
                string layer = LayerOf(e);
                nextLayer[i] = Time.unscaledTime + Random.Range(1.4f, 2.6f);
                if (layer != null && go > 0.2f) {
                    GameAudio.PlayAt(layer, e.transform.position, 0.3f, 1f);
                    Layers++;
                    LayerNames.Add(layer);
                }
            }
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
            case "bot_step_yo":   // a tiny metal tick
                return SfxSynth.Build(name + i, 0.12f, (s, t) => Mathf.Sin(TwoPi * (2600f + i * 500f) * t) * Mathf.Exp(-t * 75f) * 0.6f + (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 120f) * 0.3f);
            case "bot_step_spider":   // a pair of thin leg clicks
                return SfxSynth.Build(name + i, 0.16f, (s, t) => (Mathf.Sin(TwoPi * (3400f + i * 350f) * t) * Mathf.Exp(-t * 110f) + Mathf.Sin(TwoPi * 2300f * (t - 0.05f)) * (t > 0.05f ? Mathf.Exp(-(t - 0.05f) * 110f) : 0f)) * 0.55f);
            case "bot_step_mega":   // a heavy hydraulic step: sub thump and a piston hiss
                return SfxSynth.Build(name + i, 0.6f, (s, t) => {
                    float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                    lp += (n - lp) * 0.2f;
                    return Mathf.Sin(TwoPi * (46f + i * 5f - 14f * t) * t) * Mathf.Exp(-t * 7f) * 0.95f + (n - lp) * Mathf.Exp(-t * 9f) * (t > 0.05f ? 0.25f : 0f);
                });
            case "bot_layer_paper":   // a paper rustle: soft band-passed noise bursts
                return SfxSynth.Build(name + i, 0.5f, (s, t) => {
                    float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                    lp += (n - lp) * 0.25f;
                    return (n - lp) * (0.5f + 0.5f * Mathf.Sin(TwoPi * (14f + i * 4f) * t)) * Mathf.Exp(-t * 5f) * 0.6f;
                });
            case "bot_layer_stamp":   // a stamp clack: wood thunk and a dry tick
                return SfxSynth.Build(name + i, 0.25f, (s, t) => Mathf.Sin(TwoPi * (170f + i * 25f) * t) * Mathf.Exp(-t * 30f) * 0.7f + Mathf.Sin(TwoPi * 1800f * t) * Mathf.Exp(-t * 90f) * 0.4f);
            case "bot_layer_ticket":   // a ticket printer: a quick row of ticks
                return SfxSynth.Build(name + i, 0.5f, (s, t) => {
                    float ph = (t * (26f + i * 4f)) % 1f;
                    return Mathf.Sin(TwoPi * 2100f * t) * Mathf.Exp(-ph * 30f) * (t < 0.4f ? 0.5f : 0.5f * (0.5f - t) * 10f);
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
