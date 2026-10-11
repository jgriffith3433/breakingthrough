using UnityEngine;
using UnityEngine.SceneManagement;

// Plays the short effects in Resources/Sfx.
public class GameAudio : MonoBehaviour {
    static GameAudio instance;
    AudioSource source;

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
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
    }

    public static void Play(string name, float volume = 1f) {
        if (instance == null || instance.source == null || string.IsNullOrEmpty(name)) {
            return;
        }
        AudioClip clip = Resources.Load<AudioClip>("Sfx/" + name);
        if (clip == null) {
            return;
        }
        instance.source.PlayOneShot(clip, volume);
    }
}
