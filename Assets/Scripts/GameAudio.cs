using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

// Plays the short effects in Resources/Sfx. Each voice plays one sound, so a new pitch never bends one that is
// still ringing. The mix: the same sound landing many times at once (a wide swing through a crowd) is stacked
// softer and capped, and effects dip a little while Wallit or Slice is talking so the line is heard.
public class GameAudio : MonoBehaviour {
    const int MaxStack = 3;
    const float StackWindow = 0.05f;
    const float StackFalloff = 0.6f;
    const float VoiceDuck = 0.7f;

    static GameAudio instance;
    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    static readonly Dictionary<string, float> stackAt = new Dictionary<string, float>();
    static readonly Dictionary<string, int> stackCount = new Dictionary<string, int>();
    // For the polish playtest: sounds played, played softer because they stacked, dropped at the cap, and dipped under a line.
    public static int Played { get; private set; }
    public static int Softened { get; private set; }
    public static int Capped { get; private set; }
    public static int Ducked { get; private set; }

    readonly AudioSource[] voices = new AudioSource[12];
    readonly float[] startedAt = new float[12];
    int next;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Ensure();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        Ensure();
    }

    static void Ensure() {
        if (FindAnyObjectByType<GameAudio>() != null) {
            return;
        }
        var go = new GameObject("Game Audio");
        go.AddComponent<GameAudio>();
    }

    void Awake() {
        instance = this;
        ExposeToProbe();
        for (int i = 0; i < voices.Length; i++) {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            voices[i] = source;
        }
        for (int i = 0; i < spatial.Length; i++) {
            var child = new GameObject("World Sound " + i);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0.8f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = NearRange;
            source.maxDistance = FarRange;
            source.dopplerLevel = 0f;
            source.spread = 40f;
            spatial[i] = source;
        }
    }

    public enum Bus { Music, Ambience, Voice, Effects, UI }

    // One sound's variants: a file (name, name_2, name_3 ...) or the same file at its own rate, or a synthesised clip.
    class Variant { public AudioClip clip; public float rate = 1f; }
    static readonly Dictionary<string, List<Variant>> variants = new Dictionary<string, List<Variant>>();
    static readonly Dictionary<string, int> lastVariant = new Dictionary<string, int>();
    static readonly float[] RateSteps = { 1f, 0.9f, 1.12f, 0.8f, 1.25f };
    const int WantedVariants = 3;

    // The play log: "time|bus|name|variant|2D" or "...|3D x,y,z". Capped so a long run cannot grow it.
    public static readonly List<string> Log = new List<string>();
    public static int Repeats { get; private set; }
    public static int Snapshots { get; private set; }
    public static string Snapshot { get; private set; } = "Normal";
    public static int Spatial { get; private set; }

    // The voice snapshot: effects and UI sink under a line, then come back. Phones get a deeper dip.
    const float DuckNormal = 1f;
    const float DuckVoice = 0.45f;
    const float DuckVoicePhone = 0.38f;
    float duck = 1f;

    // 3D sources for sounds that come from the world.
    const int SpatialVoices = 10;
    const float NearRange = 6f;
    const float FarRange = 95f;
    readonly AudioSource[] spatial = new AudioSource[SpatialVoices];
    readonly float[] spatialBase = new float[SpatialVoices];
    readonly float[] voiceBase = new float[12];
    int nextSpatial;
    readonly bool[] spatialIsVoice = new bool[SpatialVoices];

    static List<Variant> VariantsOf(string name) {
        List<Variant> list;
        if (variants.TryGetValue(name, out list)) {
            return list;
        }
        list = new List<Variant>();
        AudioClip first = Resources.Load<AudioClip>("Sfx/" + name);
        if (first != null) {
            list.Add(new Variant { clip = first });
            for (int i = 2; i <= 8; i++) {
                AudioClip more = Resources.Load<AudioClip>("Sfx/" + name + "_" + i);
                if (more == null) {
                    break;
                }
                list.Add(new Variant { clip = more });
            }
            // Fewer than three files: the same take at its own rate, so no sample plays twice in a row.
            for (int i = 1; list.Count < WantedVariants && i < RateSteps.Length; i++) {
                list.Add(new Variant { clip = first, rate = RateSteps[i] });
            }
        } else if (SfxSynth.Has(name)) {
            for (int i = 0; i < WantedVariants; i++) {
                AudioClip made = SfxSynth.Make(name, i);
                if (made != null) {
                    list.Add(new Variant { clip = made });
                }
                if (name.StartsWith("homerun") || name.StartsWith("spot_")) {
                    break;
                }
            }
        }
        variants[name] = list;
        return list;
    }

    // One clip of a sound (a spot emitter plays it on its own source): a random variant, never the one just before.
    public static AudioClip ClipOf(string name) {
        int index;
        Variant v = Pick(name, out index);
        return v != null ? v.clip : null;
    }

    static Variant Pick(string name, out int index) {
        List<Variant> list = VariantsOf(name);
        index = -1;
        if (list.Count == 0) {
            return null;
        }
        int last;
        if (!lastVariant.TryGetValue(name, out last)) {
            last = -1;
        }
        index = list.Count == 1 ? 0 : Random.Range(0, list.Count - 1);
        if (list.Count > 1 && index >= last && last >= 0) {
            index++;   // skip the one that just played
        }
        index = Mathf.Min(index, list.Count - 1);
        if (list.Count > 1 && index == last) {
            Repeats++;
        }
        lastVariant[name] = index;
        return list[index];
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void HawkesSoundProbeEmit(string json);
    [DllImport("__Internal")]
    static extern void HawkesAudioExpose();
#endif

    // Announces Unity's own AudioContext (and starts copying its output) to the playtest audio probe: the page's probe cannot
    // see FMOD's context by itself. Safe to call often; a no-op outside a WebGL player.
    public static void ExposeToProbe() {
#if UNITY_WEBGL && !UNITY_EDITOR
        try {
            HawkesAudioExpose();
        } catch (System.Exception) {
        }
#endif
    }

    // Tells the playtest audio probe a sound started (the page's hawkes-audio.js reads it as a sound-start), so
    // audio.sounds, the silent-action check and the loudness window all see what the game plays. Not per frame.
    static void Report(Bus bus, string name, int variant, float rate, float volume, float dur) {
#if UNITY_WEBGL && !UNITY_EDITOR
        string id = name.Replace("\\", "/").Replace("\"", "'");
        string bus2 = bus == Bus.Effects ? "sfx" : bus.ToString().ToLowerInvariant();
        try {
            HawkesSoundProbeEmit("{\"type\":\"sound-start\",\"sample\":\"" + id + "/" + (variant + 1) + "\",\"bus\":\"" + bus2
                + "\",\"rate\":" + rate.ToString("0.###", CultureInfo.InvariantCulture)
                + ",\"gain\":" + volume.ToString("0.###", CultureInfo.InvariantCulture)
                + ",\"dur\":" + dur.ToString("0.###", CultureInfo.InvariantCulture) + "}");
        } catch (System.Exception) {
        }
#endif
    }

    static void Note(Bus bus, string name, int variant, bool has3d, Vector3 at, float volume, float rate = 1f, float dur = 0f) {
        Report(bus, name, variant, rate, volume, dur);
        if (Log.Count >= 6000) {
            return;
        }
        Log.Add(Time.unscaledTime.ToString("0.00") + "|" + bus + "|" + name + "|" + (variant + 1) + "|"
            + (has3d ? "3D " + at.x.ToString("0.0") + "," + at.y.ToString("0.0") + "," + at.z.ToString("0.0") : "2D") + "|" + volume.ToString("0.00"));
    }

    // Voice and music are 2D and not played here: they note themselves so the log shows every bus.
    public static void NoteOther(Bus bus, string name) {
        Note(bus, name, 0, false, Vector3.zero, 1f);
    }

    public static void Play(string name, float volume = 1f) {
        Play(name, volume, 1f);
    }

    public static void Play(string name, float volume, float pitch) {
        Start(name, volume, pitch, Bus.Effects, false, Vector3.zero);
    }

    // A menu or card sound: 2D, on the UI bus.
    public static void PlayUI(string name, float volume = 1f, float pitch = 1f) {
        Start(name, volume, pitch, Bus.UI, false, Vector3.zero);
    }

    // A sound that comes from a place in the world: bat hits, bot steps, attacks and deaths, pickups, boss tells.
    public static void PlayAt(string name, Vector3 position, float volume = 1f, float pitch = 1f) {
        Start(name, volume, pitch, Bus.Effects, true, position);
    }

    // A spoken clip (a bot's bark) from a place in the world: 3D, at its own pitch, on the Voice bus. The caller owns the floor.
    public static void PlayVoiceAt(AudioClip clip, string name, Vector3 position, float volume = 1f) {
        float level = SoundSettings.EffectsLevel;
        if (instance == null || clip == null || level <= 0f) {
            return;
        }
        int slot = instance.FreeSpatial();
        AudioSource source = instance.spatial[slot];
        instance.spatialBase[slot] = Mathf.Clamp01(volume);
        instance.spatialIsVoice[slot] = true;
        source.Stop();
        source.clip = clip;
        source.pitch = 1f;
        source.transform.position = position;
        // Its own line holds the floor, which ducks effects: keep this one out of that dip.
        source.volume = Mathf.Clamp01(volume) * level;
        source.Play();
        Played++;
        Spatial++;
        Note(Bus.Voice, name, 0, true, position, volume, 1f, clip.length);
    }

    static void Start(string name, float volume, float pitch, Bus bus, bool world, Vector3 at) {
        float level = SoundSettings.EffectsLevel;
        if (instance == null || string.IsNullOrEmpty(name) || level <= 0f) {
            return;
        }
        int index;
        Variant variant = Pick(name, out index);
        if (variant == null || variant.clip == null) {
            return;
        }
        float now = Time.unscaledTime;
        float firstAt;
        int stacked = 0;
        if (stackAt.TryGetValue(name, out firstAt) && now - firstAt < StackWindow) {
            stacked = stackCount[name];
        } else {
            stackAt[name] = now;
        }
        if (stacked >= MaxStack) {
            Capped++;
            return;
        }
        stackCount[name] = stacked + 1;
        float mix = Mathf.Pow(StackFalloff, stacked);
        if (stacked > 0) {
            Softened++;
        }
        if (!WallitVoice.FloorFree) {
            Ducked++;
        }
        Played++;
        float rate = Mathf.Clamp(pitch * variant.rate * Random.Range(0.97f, 1.03f), 0.3f, 3f);
        float baseVolume = Mathf.Clamp01(volume * mix);
        AudioSource source;
        if (world) {
            Spatial++;
            int slot = instance.FreeSpatial();
            source = instance.spatial[slot];
            instance.spatialBase[slot] = baseVolume;
            instance.spatialIsVoice[slot] = false;
        } else {
            int slot = instance.FreeVoice();
            source = instance.voices[slot];
            instance.voiceBase[slot] = baseVolume;
        }
        source.Stop();
        source.clip = variant.clip;
        source.pitch = rate;
        if (world) {
            source.transform.position = at;
        }
        source.volume = baseVolume * level * instance.duck * AudioMix.SfxGain * AudioMix.CrackDuck;
        AudioMix.Reverb(source);
        source.Play();
        if (name.StartsWith("homerun") || name.StartsWith("batcrack")) {
            AudioMix.HomeRunCrack();
        }
        Note(bus, name, index, world, at, baseVolume, rate, variant.clip.length / rate);
    }

    float nextExpose;

    void Update() {
        if (Time.unscaledTime >= nextExpose) {
            nextExpose = Time.unscaledTime + 2f;
            ExposeToProbe();
        }
        bool held = !WallitVoice.FloorFree;
        string state = held ? "Voice" : "Normal";
        if (state != Snapshot) {
            Snapshot = state;
            Snapshots++;
            Log.Add(Time.unscaledTime.ToString("0.00") + "|Mixer|snapshot " + state + "|0|2D|" + (held ? (UiFit.Touch ? DuckVoicePhone : DuckVoice) : DuckNormal).ToString("0.00"));
        }
        float target = held ? (UiFit.Touch ? DuckVoicePhone : DuckVoice) : DuckNormal;
        duck = Mathf.MoveTowards(duck, target, Time.unscaledDeltaTime * 6f);
        float level = SoundSettings.EffectsLevel;
        for (int i = 0; i < voices.Length; i++) {
            if (voices[i].isPlaying) {
                voices[i].volume = Mathf.Clamp01(voiceBase[i] * level * duck);
            }
        }
        for (int i = 0; i < spatial.Length; i++) {
            if (spatial[i].isPlaying) {
                spatial[i].volume = Mathf.Clamp01(spatialBase[i] * level * (spatialIsVoice[i] ? 1f : duck));
            }
        }
    }

    int FreeSpatial() {
        int pick = -1;
        for (int i = 0; i < spatial.Length; i++) {
            int index = (nextSpatial + i) % spatial.Length;
            if (!spatial[index].isPlaying) {
                pick = index;
                break;
            }
        }
        if (pick < 0) {
            pick = nextSpatial;
        }
        nextSpatial = (pick + 1) % spatial.Length;
        return pick;
    }

    // A quiet voice if there is one, else the one that started longest ago.
    int FreeVoice() {
        int pick = -1;
        for (int i = 0; i < voices.Length; i++) {
            int index = (next + i) % voices.Length;
            if (!voices[index].isPlaying) {
                pick = index;
                break;
            }
        }
        if (pick < 0) {
            pick = 0;
            for (int i = 1; i < voices.Length; i++) {
                if (startedAt[i] < startedAt[pick]) {
                    pick = i;
                }
            }
        }
        next = (pick + 1) % voices.Length;
        startedAt[pick] = Time.unscaledTime;
        return pick;
    }
}
