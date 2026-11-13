using UnityEngine;

// The mix, kept in code (an AudioMixer asset can only be authored in the editor): buses (Music, Voice, SFX-Combat, SFX-World,
// Ambience, UI) are the gains each system applies, snapshots (Menu, Explore, Combat, Boss, Cutscene, Pause) set them, a
// sidechain-style duck lowers Music 6 dB and Ambience 4 dB under any voice (150 ms attack, 600 ms release), a home run dips
// everything else for its crack, reverb follows the place, and a soft limiter sits on the listener.
public class AudioMix : MonoBehaviour {
    public enum Snap { Menu, Explore, Combat, Boss, Cutscene, Pause }

    public const float MusicDuckDb = -6f;
    public const float AmbienceDuckDb = -4f;
    const float Attack = 0.15f;
    const float Release = 0.6f;
    const float CeilingDb = -1.5f;

    static AudioMix instance;
    public static Snap Current { get; private set; } = Snap.Menu;
    public static string Place { get; private set; } = "menu";
    public static float ReverbDecay { get; private set; }
    public static int SnapChanges { get; private set; }

    float voiceAmt, crackUntil, crackAmt;

    // Linear gains the other systems multiply by.
    public static float MusicDuck { get { return instance != null ? Mathf.Lerp(1f, Db(MusicDuckDb), instance.voiceAmt) : 1f; } }
    public static float AmbienceDuck { get { return instance != null ? Mathf.Lerp(1f, Db(AmbienceDuckDb), instance.voiceAmt) : 1f; } }
    public static float CrackDuck { get { return instance != null ? instance.crackAmt : 1f; } }
    // Snapshot gain on a bus: pause drops effects and low-passes the rest, the fight leans on effects, cutscenes on voice.
    public static float SfxGain { get { return Current == Snap.Pause ? 0f : Current == Snap.Cutscene ? 0.7f : 1f; } }
    public static float MusicGain { get { return Current == Snap.Combat ? 0.85f : Current == Snap.Boss ? 1f : Current == Snap.Pause ? 0.6f : 1f; } }
    static float Db(float db) { return Mathf.Pow(10f, db / 20f); }

    public static void HomeRunCrack() {
        if (instance != null) {
            instance.crackUntil = Time.unscaledTime + 0.35f;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() {
        if (instance == null) {
            instance = new GameObject("Audio Mix").AddComponent<AudioMix>();
            DontDestroyOnLoad(instance.gameObject);
        }
    }

    void Update() {
        float dt = Time.unscaledDeltaTime;
        bool voice = !WallitVoice.FloorFree || CastVoice.Speaking;
        voiceAmt = Mathf.MoveTowards(voiceAmt, voice ? 1f : 0f, dt / (voice ? Attack : Release));
        crackAmt = Mathf.MoveTowards(crackAmt, Time.unscaledTime < crackUntil ? 0.45f : 1f, dt / (Time.unscaledTime < crackUntil ? 0.03f : 0.25f));
        Snap want = Pick();
        if (want != Current) {
            Current = want;
            SnapChanges++;
            GameAudio.NoteOther(GameAudio.Bus.UI, "snapshot " + want);
        }
        string place = PlaceNow();
        if (place != Place) {
            Place = place;
            GameAudio.NoteOther(GameAudio.Bus.Ambience, "place " + place);
        }
        ReverbDecay = DecayOf(Place);
        EnsureLimiter();
    }

    static Snap Pick() {
        if (PauseMenu.IsOpen) return Snap.Pause;
        if (Cutscene.IsOpen) return Snap.Cutscene;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Campaign" && ChapterHub.Instance != null) return Snap.Explore;
        bool any = Enemy.Roster != null && Enemy.Roster.Count > 0;
        if (!any) return Object.FindAnyObjectByType<MainMenu>() != null ? Snap.Menu : Snap.Explore;
        foreach (Enemy e in Enemy.Roster) {
            if (e != null && e.Boss) return Snap.Boss;
        }
        return Snap.Combat;
    }

    static string PlaceNow() {
        if (ChapterHub.Instance != null) return "hub";
        if (ChapterRun.Instance != null && ChapterRun.Instance.IsFloor) return "floor";
        return "hollow";
    }

    // Reverb tail by place: a small dry office, a tiled hall, the open night field.
    public static float DecayOf(string place) {
        return place == "hub" ? 0.35f : place == "floor" ? 2.2f : place == "menu" ? 0.6f : 3.6f;
    }

    public static void Dress(AudioSource source) {
        if (source != null && source.GetComponent<AudioReverbFilter>() == null) {
            source.gameObject.AddComponent<AudioReverbFilter>();
        }
    }

    // Called by GameAudio when a sound starts: the place's reverb goes on that source.
    public static void Reverb(AudioSource source) {
        if (source == null) return;
        Dress(source);
        var f = source.GetComponent<AudioReverbFilter>();
        float decay = DecayOf(Place);
        f.reverbPreset = AudioReverbPreset.User;
        f.decayTime = decay;
        f.dryLevel = 0f;
        f.room = Place == "hub" ? -2200f : Place == "floor" ? -900f : -1100f;
        f.reflectionsDelay = Place == "floor" ? 0.03f : 0.01f;
        f.reverbDelay = Place == "hub" ? 0.01f : 0.04f;
        f.diffusion = Place == "floor" ? 60f : 100f;
        f.density = 100f;
        f.hfReference = Place == "floor" ? 8000f : 5000f;
    }

    // A component with OnAudioFilterRead makes the engine attach a DSP to the FX channel group (AudioListener.cpp). While a scene
    // is loading or just swapped in, that group can be invalid ("An invalid object handle was used"), so the limiter goes on
    // only once audio has been running and the new scene has settled for a second.
    float settleUntil;

    void Awake() {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneSettled;
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneGone;
    }

    void OnDestroy() {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneSettled;
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneGone;
    }

    void OnSceneSettled(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) {
        settleUntil = Time.unscaledTime + 1f;
    }

    void OnSceneGone(UnityEngine.SceneManagement.Scene s) {
        settleUntil = Time.unscaledTime + 1f;
    }

    void EnsureLimiter() {
        if (Time.unscaledTime < settleUntil || Time.unscaledTime < 1f) return;
        if (AudioSettings.outputSampleRate <= 0) return;
        AudioListener l = Object.FindAnyObjectByType<AudioListener>();
        if (l != null && l.isActiveAndEnabled && l.GetComponent<AudioLimiter>() == null) {
            l.gameObject.AddComponent<AudioLimiter>();
        }
    }

    // A soft-knee limiter on the master (audio thread): nothing leaves above the ceiling.
    public class AudioLimiter : MonoBehaviour {
        float gain = 1f;
        void OnAudioFilterRead(float[] data, int channels) {
            float ceiling = Mathf.Pow(10f, CeilingDb / 20f);
            for (int i = 0; i < data.Length; i += channels) {
                float peak = 0f;
                for (int c = 0; c < channels; c++) peak = Mathf.Max(peak, Mathf.Abs(data[i + c]));
                float need = peak > ceiling ? ceiling / peak : 1f;
                gain = need < gain ? need : Mathf.Min(1f, gain + 0.0004f);
                for (int c = 0; c < channels; c++) data[i + c] *= gain;
            }
        }
    }
}
