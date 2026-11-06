using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// The score. One track at a time (StreamingAssets/Music/<name>.mp3, looping), crossfaded when the scene changes:
//   theme_wallit   main menu          theme_department  case files and chapter cards       theme_slice  Chapter Complete card
//   ch1_score..ch6_score  each chapter in play        boss_<name>  while a named boss is up      hub  the yard between chapters
//   ending         ADDRESS FILED      credits            the credits
// It ducks under any spoken line (the shared voice floor, CastVoice, the Wallit and Slice voices) and gives way to a shot
// reel's own bed (Cutscene.BedPlaying). Volume is the Settings music slider. A missing file is a quiet no-op: the log
// still says what was asked for, so the playtest can see the change.
// The tracks are not in Resources: 18 of them made the WebGL data file 50 MB bigger and the page took ~50 s to start. Each is
// fetched by URL the first time it is asked for (Fetch; the old track plays on until the new one is in) and the last few
// stay cached. Shot reels take their bed from here too (Cutscene.StartBed).
public class MusicDirector : MonoBehaviour {
    const float DuckLevel = 0.3f;
    const float FadeSeconds = 1.6f;
    const int CacheSize = 4;
    const float DecodeSeconds = 15f;

    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    static readonly List<string> recent = new List<string>();
    static readonly HashSet<string> missing = new HashSet<string>();
    static readonly Dictionary<string, List<Action<AudioClip>>> waiting = new Dictionary<string, List<Action<AudioClip>>>();

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
        clips.Clear();
        recent.Clear();
        missing.Clear();
        waiting.Clear();
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
        if (track.Length == 0) {
            return;
        }
        Fetch(track, clip => {
            // Still the track wanted, on the source it was meant for (a later Play may have moved on).
            if (clip == null || Current != track || instance == null || instance.sources[instance.active] != next) {
                return;
            }
            if (next.clip == clip && next.isPlaying) {
                return;
            }
            next.clip = clip;
            next.Play();
        });
    }

    // ---------------------------------------------------------------- fetching

    // A track already fetched, else null.
    public static AudioClip Cached(string track) {
        AudioClip clip;
        return !string.IsNullOrEmpty(track) && clips.TryGetValue(track, out clip) && clip != null ? clip : null;
    }

    // True once a fetch of the track has failed (no such file in StreamingAssets/Music).
    public static bool Missing(string track) {
        return !string.IsNullOrEmpty(track) && missing.Contains(track);
    }

    // Start fetching a track so it is ready when it is played.
    public static void Prefetch(string track) {
        Fetch(track, null);
    }

    // Hands the track's clip to done (null if there is no such track): at once when cached, else when the download is in.
    public static void Fetch(string track, Action<AudioClip> done) {
        if (string.IsNullOrEmpty(track) || missing.Contains(track)) {
            if (done != null) {
                done(null);
            }
            return;
        }
        AudioClip have = Cached(track);
        if (have != null) {
            Touch(track);
            if (done != null) {
                done(have);
            }
            return;
        }
        List<Action<AudioClip>> list;
        if (waiting.TryGetValue(track, out list)) {
            if (done != null) {
                list.Add(done);
            }
            return;
        }
        list = new List<Action<AudioClip>>();
        if (done != null) {
            list.Add(done);
        }
        waiting[track] = list;
        Ensure().StartCoroutine(Download(track));
    }

    static string Url(string track) {
        string path = Application.streamingAssetsPath + "/Music/" + track + ".mp3";
        return path.Contains("://") ? path : new Uri(path).AbsoluteUri;
    }

    static IEnumerator Download(string track) {
        AudioClip clip = null;
        bool absent = true;   // false when the network failed: the next Play tries again
        using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(Url(track), AudioType.MPEG)) {
            var handler = req.downloadHandler as DownloadHandlerAudioClip;
            if (handler != null) {
                handler.streamAudio = false;
                handler.compressed = true;
            }
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) {
                try {
                    clip = DownloadHandlerAudioClip.GetContent(req);
                } catch (Exception) {
                    clip = null;
                }
            } else {
                absent = req.result != UnityWebRequest.Result.ConnectionError;
                Debug.Log("[Music] " + track + " not fetched: " + req.error);
            }
        }
        if (clip != null) {
            clip.name = track;
            if (clip.loadState == AudioDataLoadState.Unloaded) {
                clip.LoadAudioData();
            }
            float until = Time.realtimeSinceStartup + DecodeSeconds;
            while (clip != null && clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartup < until) {
                yield return null;
            }
            if (clip != null && clip.loadState == AudioDataLoadState.Failed) {
                Debug.Log("[Music] " + track + " did not decode");
                Destroy(clip);
                clip = null;
            }
        }
        if (clip != null) {
            clips[track] = clip;
            Touch(track);
            Trim();
        } else if (absent) {
            missing.Add(track);
        }
        List<Action<AudioClip>> list;
        if (waiting.TryGetValue(track, out list)) {
            waiting.Remove(track);
            foreach (Action<AudioClip> done in list) {
                done(clip);
            }
        }
    }

    static void Touch(string track) {
        recent.Remove(track);
        recent.Add(track);
    }

    // Keeps the last few tracks; never drops one a music source or the open reel's bed is playing.
    static void Trim() {
        int i = 0;
        while (recent.Count > CacheSize && i < recent.Count) {
            string old = recent[i];
            AudioClip clip = Cached(old);
            if (clip != null && InUse(clip)) {
                i++;
                continue;
            }
            recent.RemoveAt(i);
            clips.Remove(old);
            if (clip != null) {
                Destroy(clip);
            }
        }
    }

    static bool InUse(AudioClip clip) {
        if (instance != null) {
            foreach (AudioSource s in instance.sources) {
                if (s != null && s.clip == clip) {
                    return true;
                }
            }
        }
        return Cutscene.Instance != null && Cutscene.Instance.BedClip == clip;
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
