using UnityEngine;
using UnityEngine.SceneManagement;

// Hit-stop: a blink of slow motion on big kills and bat contact. The only camera shake is the bat's small
// contact jolt (ZeldaStyleCamera.Shake), which J asked for with the bat.
public class Juice : MonoBehaviour {
    static ZeldaStyleCamera cam;
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
        if ((SceneManager.GetActiveScene().name != MetaProgress.GameScene && !SceneManager.GetActiveScene().name.StartsWith("Level_"))) {
            return;
        }
        if (FindAnyObjectByType<Juice>() != null) {
            return;
        }
        new GameObject("Juice").AddComponent<Juice>();
    }

    // Hit-stop is counted in frames, not seconds, so it reads the same at any frame rate: 2 to 4 frames.
    public const int MinStopFrames = 2;
    public const int MaxStopFrames = 4;
    public static int Stops { get; private set; }
    public static int ShortestStop { get; private set; }
    public static int LongestStop { get; private set; }
    public static int HomeRunBeats { get; private set; }
    static int stopStartFrame;
    static int stopEndFrame;
    static float slowUntil;
    static float slowScale = 1f;
    static bool slowing;

    public static void ResetStats() {
        Stops = 0;
        ShortestStop = 0;
        LongestStop = 0;
        HomeRunBeats = 0;
    }

    // Freeze the world for a blink. Never fights a real pause. Seconds are turned into 2 to 4 frames at 60 fps.
    public static void HitStop(float seconds) {
        HitStopFrames(Mathf.RoundToInt(seconds * 60f));
    }

    public static void HitStopFrames(int frames) {
        frames = Mathf.Clamp(frames, MinStopFrames, MaxStopFrames);
        if (Time.timeScale < 0.99f && !stopping) {
            return;
        }
        if (!stopping) {
            stopStartFrame = Time.frameCount;
            stopEndFrame = stopStartFrame;
            Stops++;
        }
        stopping = true;
        stopEndFrame = Mathf.Min(Mathf.Max(stopEndFrame, Time.frameCount + frames), stopStartFrame + MaxStopFrames);
        Time.timeScale = 0.05f;
    }

    // After a home run's hit-stop the world eases through a short slow beat so the launch can be seen.
    public static void SlowBeat(float seconds, float scale) {
        HomeRunBeats++;
        slowUntil = Time.unscaledTime + seconds;
        slowScale = scale;
        slowing = true;
    }

    void Update() {
        if (stopping && Time.frameCount >= stopEndFrame) {
            stopping = false;
            int held = Time.frameCount - stopStartFrame;
            ShortestStop = ShortestStop == 0 ? held : Mathf.Min(ShortestStop, held);
            LongestStop = Mathf.Max(LongestStop, held);
            if (Time.timeScale > 0.01f) {
                Time.timeScale = 1f;
            }
        }
        if (stopping && Time.timeScale <= 0.01f) {
            stopping = false;
        }
        if (slowing && !stopping) {
            if (Time.timeScale <= 0.01f) {
                slowing = false;
            } else if (Time.unscaledTime >= slowUntil) {
                slowing = false;
                Time.timeScale = 1f;
            } else {
                Time.timeScale = slowScale;
            }
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
