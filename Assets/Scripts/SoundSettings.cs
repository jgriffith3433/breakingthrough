using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

// Sound on/off, music and effects volume: the studio's standard settings. Saved as JSON { on, music, sfx } (0-1)
// under "hawkes.sound": in the browser's localStorage on the web (where every Hawkes game keeps it), PlayerPrefs
// too. Music is every looping AudioSource in the scene (the piano); effects are GameAudio's voices.
public static class SoundSettings {
    const string Key = "hawkes.sound";

    [System.Serializable]
    public class Saved {
        public bool on = true;
        public float music = 1f;
        public float sfx = 1f;
    }

    static Saved now = new Saved();
    static bool loaded;
    static bool dirty;
    // Each music source's volume as the scene set it, so the slider scales it instead of replacing it.
    static readonly Dictionary<int, float> musicBase = new Dictionary<int, float>();

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern string HawkesSoundLoad();

    [DllImport("__Internal")]
    static extern void HawkesSoundSave(string json);

    [DllImport("__Internal")]
    static extern void HawkesSoundAttach();
#else
    static string HawkesSoundLoad() {
        return "";
    }

    static void HawkesSoundSave(string json) {
    }

    static void HawkesSoundAttach() {
    }
#endif

    public static bool On {
        get { Load(); return now.on; }
    }

    public static float Music {
        get { Load(); return now.music; }
    }

    public static float Sfx {
        get { Load(); return now.sfx; }
    }

    // What an effect's volume is multiplied by.
    public static float EffectsLevel {
        get { Load(); return now.on ? now.sfx : 0f; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Load();
        // Hand Unity's AudioContext to the page's "Tap to play" prompt, so it can resume it later.
        HawkesSoundAttach();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        musicBase.Clear();
        HawkesSoundAttach();
        Apply();
    }

    public static void Set(bool on, float music, float sfx) {
        Load();
        now.on = on;
        now.music = Mathf.Clamp01(music);
        now.sfx = Mathf.Clamp01(sfx);
        Apply();
        // Tooling and tests never write the player's save (MetaProgress's sandbox rule).
        if (!MetaProgress.Sandbox) {
            string json = JsonUtility.ToJson(now);
            HawkesSoundSave(json);
            PlayerPrefs.SetString(Key, json);
            dirty = true;
        }
    }

    // Writes PlayerPrefs out (on the web that is a disk sync, so not on every slider step). Settings calls it on close.
    public static void Flush() {
        if (dirty) {
            dirty = false;
            PlayerPrefs.Save();
        }
    }

    // Music volumes follow the settings right away; effects read EffectsLevel as they play.
    public static void Apply() {
        Load();
        float level = now.on ? now.music : 0f;
        foreach (AudioSource source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) {
            if (source == null || !source.loop || source.GetComponent<GameAudio>() != null) {
                continue;
            }
            int id = source.GetInstanceID();
            if (!musicBase.TryGetValue(id, out float scene)) {
                scene = source.volume;
                musicBase[id] = scene;
            }
            source.volume = scene * level;
        }
    }

    static void Load() {
        if (loaded) {
            return;
        }
        loaded = true;
        string json = HawkesSoundLoad();
        if (string.IsNullOrEmpty(json)) {
            json = PlayerPrefs.GetString(Key, "");
        }
        if (string.IsNullOrEmpty(json)) {
            return;
        }
        try {
            Saved read = JsonUtility.FromJson<Saved>(json);
            if (read != null) {
                now.on = read.on;
                now.music = Mathf.Clamp01(read.music);
                now.sfx = Mathf.Clamp01(read.sfx);
            }
        } catch (System.Exception) {
            now = new Saved();
        }
    }
}
