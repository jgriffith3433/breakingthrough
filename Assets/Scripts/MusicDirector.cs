using System.Collections.Generic;
using UnityEngine;

// The score. One track at a time (Resources/Music/<name>.mp3, looping, compressed), crossfaded when the scene changes:
//   theme_wallit   main menu          theme_department  case files and chapter cards       theme_slice  Chapter Complete card
//   ch1_score..ch6_score  each chapter in play        boss_<name>  while a named boss is up      hub  the yard between chapters
//   ending         ADDRESS FILED      credits            the credits
// It ducks under any spoken line (the shared voice floor, CastVoice, the Wallit and Slice voices) and gives way to a shot
// reel's own bed (Cutscene.BedPlaying). Volume is the Settings music slider. A missing file is a quiet no-op: the log
// still says what was asked for, so the playtest can see the change.
public class MusicDirector : MonoBehaviour {
    const float DuckLevel = 0.3f;
    const float FadeSeconds = 1.6f;

    static MusicDirector instance;
    public static string Current { get; private set; } = "";
    public static int Changes { get; private set; }
    public static float LowestDuck { get; private set; } = 1f;
    public static readonly List<string> Events = new List<string>();
    // The loudest the bed gets (before the Settings slider); kept low so voices sit on top without a hard cut.
    public static float BedLevel = 0.55f;

    AudioSource[] sources = new AudioSource[2];
    int active;
    float duck = 1f;
    float level;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() {
        instance = null;
        Current = "";
        Changes = 0;
        LowestDuck = 1f;
        Events.Clear();
    }

    static MusicDirector Ensure() {
        if (instance == null) {
            var go = new GameObject("Music Director");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MusicDirector>();
            for (int i = 0; i < 2; i++) {
                AudioSource s = go.AddComponent<AudioSource>();
                s.loop = true;
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.volume = 0f;
                s.ignoreListenerPause = true;
                instance.sources[i] = s;
            }
        }
        return instance;
    }

    // Change the track. The same track again does nothing; "" fades the music out.
    public static void Play(string track) {
        if (track == null) {
            track = "";
        }
        if (track == Current) {
            return;
        }
        MusicDirector d = Ensure();
        Current = track;
        Changes++;
        Events.Add("track|" + track);
        d.active = 1 - d.active;
        AudioSource next = d.sources[d.active];
        next.Stop();
        next.volume = 0f;
        AudioClip clip = track.Length > 0 ? Resources.Load<AudioClip>("Music/" + track) : null;
        if (clip != null) {
            next.clip = clip;
            next.Play();
        }
    }

    public static string ChapterTrack(int chapterIndex) {
        return "ch" + (chapterIndex + 1) + "_score";
    }

    // A boss's own theme by its name, else "" (the chapter music carries on).
    public static string BossTrack(string bossName) {
        if (string.IsNullOrEmpty(bossName)) {
            return "";
        }
        string n = bossName.ToLowerInvariant();
        if (n.Contains("courier")) return "boss_courier";
        if (n.Contains("plated")) return "boss_platedalpha";
        if (n.Contains("foreman")) return "boss_foreman";
        if (n.Contains("server")) return "boss_server";
        if (n.Contains("registrar")) return "boss_registrar";
        if (n.Contains("order")) return "boss_orderbearer";
        return "";
    }

    void Update() {
        float dt = Time.unscaledDeltaTime;
        bool voice = !WallitVoice.FloorFree || CastVoice.Speaking;
        duck = Mathf.MoveTowards(duck, voice ? DuckLevel : 1f, dt * (voice ? 4f : 0.8f));
        LowestDuck = Mathf.Min(LowestDuck, duck);
        bool reelBed = Cutscene.IsOpen && Cutscene.Instance != null && Cutscene.Instance.BedPlaying;
        float want = SoundSettings.On && !reelBed ? SoundSettings.Music * BedLevel * duck : 0f;
        for (int i = 0; i < 2; i++) {
            AudioSource s = sources[i];
            if (s == null || !s.isPlaying) {
                continue;
            }
            float target = i == active ? want : 0f;
            s.volume = Mathf.MoveTowards(s.volume, target, dt / FadeSeconds);
            if (i != active && s.volume <= 0.001f) {
                s.Stop();
            }
        }
        level = sources[active] != null ? sources[active].volume : 0f;
    }

    public static float Volume { get { return instance != null ? instance.level : 0f; } }
    public static bool Playing { get { return instance != null && instance.sources[instance.active].isPlaying; } }
    public static float Duck { get { return instance != null ? instance.duck : 1f; } }
}
