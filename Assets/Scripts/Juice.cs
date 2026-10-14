using UnityEngine;
using UnityEngine.SceneManagement;

// Hit-stop: a blink of slow motion on big kills. There is deliberately no camera shake.
public class Juice : MonoBehaviour {
    static ZeldaStyleCamera cam;
    static float stopUntil;
    static bool stopping;

    public static ZeldaStyleCamera Cam {
        get {
            if (cam == null) {
                cam = FindAnyObjectByType<ZeldaStyleCamera>();
            }
            return cam;
        }
    }

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
        cam = null;
        stopping = false;
        if (SceneManager.GetActiveScene().name != MetaProgress.GameScene) {
            return;
        }
        if (FindAnyObjectByType<Juice>() != null) {
            return;
        }
        new GameObject("Juice").AddComponent<Juice>();
    }

    // Freeze the world for a blink. Never fights a real pause.
    public static void HitStop(float seconds) {
        if (Time.timeScale < 0.99f && !stopping) {
            return;
        }
        stopping = true;
        stopUntil = Mathf.Max(stopUntil, Time.unscaledTime + seconds);
        Time.timeScale = 0.05f;
    }

    void Update() {
        if (stopping && Time.unscaledTime >= stopUntil) {
            stopping = false;
            if (Time.timeScale > 0.01f) {
                Time.timeScale = 1f;
            }
        }
        if (stopping && Time.timeScale <= 0.01f) {
            stopping = false;
        }
        Combo.Tick(Time.unscaledDeltaTime);
    }
}

// Kill streak. Keeps going while kills land inside the window.
public static class Combo {
    public static int Count { get; private set; }
    public static int Best { get; private set; }
    public static float Left { get; private set; }
    public static float Pop { get; private set; }

    public static void BeginRun() {
        Count = 0;
        Best = 0;
        Left = 0f;
    }

    public static void Kill() {
        Count++;
        Left = Tuning.ComboWindow;
        Pop = 1f;
        if (Count > Best) {
            Best = Count;
        }
        if (Count == 25 || Count == 50 || Count == 100 || Count == 200 || (Count > 200 && Count % 100 == 0)) {
            UpgradeLook.Say(Count + " streak!" + (Count >= 100 ? "  UNSTOPPABLE" : Count >= 50 ? "  FRENZY" : ""), 1.4f, -60f);
            GameAudio.Play("coin", 0.5f, 1.4f);
        }
    }

    public static void Tick(float dt) {
        if (Pop > 0f) {
            Pop = Mathf.MoveTowards(Pop, 0f, dt * 4f);
        }
        if (Count <= 0 || Time.timeScale <= 0.01f) {
            return;
        }
        Left -= dt;
        if (Left <= 0f) {
            Count = 0;
        }
    }

    // More feathers the longer the streak, up to double.
    public static float FeatherBonus {
        get { return Mathf.Min(Tuning.ComboFeatherMax, Count * Tuning.ComboFeatherPerKill); }
    }

    // A long streak draws a little faster.
    public static float DrawBonus {
        get { return Mathf.Min(0.3f, Count * 0.004f); }
    }
}
