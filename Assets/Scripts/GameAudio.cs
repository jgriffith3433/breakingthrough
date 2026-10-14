using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Plays the short effects in Resources/Sfx. A few voices so pitch can vary per sound.
public class GameAudio : MonoBehaviour {
    static GameAudio instance;
    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly AudioSource[] voices = new AudioSource[8];
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
        if (instance == null || string.IsNullOrEmpty(name)) {
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
        AudioSource source = instance.voices[instance.next];
        instance.next = (instance.next + 1) % instance.voices.Length;
        source.pitch = Mathf.Clamp(pitch, 0.3f, 3f);
        source.PlayOneShot(clip, volume);
    }
}
