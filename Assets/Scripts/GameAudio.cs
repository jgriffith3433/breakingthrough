using System.Collections.Generic;
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
        for (int i = 0; i < voices.Length; i++) {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            voices[i] = source;
        }
    }

    public static void Play(string name, float volume = 1f) {
        Play(name, volume, 1f);
    }

    public static void Play(string name, float volume, float pitch) {
        float level = SoundSettings.EffectsLevel;
        if (instance == null || string.IsNullOrEmpty(name) || level <= 0f) {
            return;
        }
        AudioClip clip;
        if (!clips.TryGetValue(name, out clip)) {
            clip = Resources.Load<AudioClip>("Sfx/" + name);
            clips[name] = clip;
        }
        if (clip == null) {
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
            mix *= VoiceDuck;
            Ducked++;
        }
        Played++;
        AudioSource source = instance.FreeVoice();
        source.Stop();
        source.clip = clip;
        source.pitch = Mathf.Clamp(pitch, 0.3f, 3f);
        source.volume = Mathf.Clamp01(volume * level * mix);
        source.Play();
    }

    // A quiet voice if there is one, else the one that started longest ago.
    AudioSource FreeVoice() {
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
        return voices[pick];
    }
}
