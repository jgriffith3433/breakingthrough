using System.Collections.Generic;
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

    // Hit-stop is counted in frames, not seconds, so it reads the same at any frame rate. By weight: a light combo
    // hit 3 frames (50 ms), a finisher 5, a home run 6. Other big moments ask in seconds and stay at 4 or under.
    public const int MinStopFrames = 2;
    public const int MaxStopFrames = 6;
    public const int OtherMaxFrames = 4;
    public enum Weight { Light, Finisher, HomeRun }
    public static int FramesFor(Weight w) {
        return w == Weight.HomeRun ? 6 : w == Weight.Finisher ? 5 : 3;
    }
    // One line per weighted contact: the frames asked and held, and the camera kick with its measured decay.
    public static readonly List<string> HitLog = new List<string>();
    public static int LightHeld { get; private set; }
    public static int FinisherHeld { get; private set; }
    public static int HomeRunHeld { get; private set; }
    static int stopCap = OtherMaxFrames;
    static int stopWeight = -1;

    // Bat contact scaled by weight: hit-stop frames, the camera kick toward the hit (off with Reduce Motion; the
    // freeze stays) and, for a home run, a short FOV punch. `toward` is the world direction of the blow.
    public static void Contact(Weight w, Vector3 toward) {
        int frames = FramesFor(w);
        bool still = ComfortOptions.ReducedMotion;
        float kick = w == Weight.HomeRun ? 0.32f : w == Weight.Finisher ? 0.12f : 0.04f;
        float fov = w == Weight.HomeRun ? 4f : w == Weight.Finisher ? 1f : 0f;
        float seconds = w == Weight.HomeRun ? 0.26f : w == Weight.Finisher ? 0.18f : 0.1f;
        if (UiFit.Touch) {
            kick *= 0.6f;   // a phone's frame is small: the play area must stay on screen
            fov *= 0.75f;
        }
        if (!still) {
            ZeldaStyleCamera.Kick(toward, kick, fov, seconds);
            // The bat's small contact jolt (J's 2026-10-19 exception): a few centimetres, gone in a tenth of a second.
            ZeldaStyleCamera.Shake(w == Weight.HomeRun ? 0.09f : w == Weight.Finisher ? 0.06f : 0.035f * (UiFit.Touch ? 0.7f : 1f), 0.12f);
        }
        stopWeight = Mathf.Max(stopWeight, (int)w);
        HitStopFrames(frames, true);
        HitLog.Add(w + " hit: hit-stop " + frames + " frames, kick " + (still ? 0f : kick).ToString("0.00") + " m"
            + (still ? " (off: Reduce Motion)" : ", decays in " + seconds.ToString("0.00") + " s, fov punch " + fov.ToString("0.0") + " deg"));
        if (HitLog.Count > 40) {
            HitLog.RemoveAt(0);
        }
    }
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
        LightHeld = 0;
        FinisherHeld = 0;
        HomeRunHeld = 0;
        HitLog.Clear();
    }

    // Freeze the world for a blink. Never fights a real pause. Seconds are turned into 2 to 4 frames at 60 fps.
    public static void HitStop(float seconds) {
        HitStopFrames(Mathf.RoundToInt(seconds * 60f));
    }

    public static void HitStopFrames(int frames, bool weighted = false) {
        frames = Mathf.Clamp(frames, MinStopFrames, weighted ? MaxStopFrames : OtherMaxFrames);
        if (Time.timeScale < 0.99f && !stopping) {
            return;
        }
        if (!stopping) {
            stopStartFrame = Time.frameCount;
            stopEndFrame = stopStartFrame;
            stopCap = OtherMaxFrames;
            Stops++;
        }
        stopping = true;
        stopCap = Mathf.Max(stopCap, frames);
        stopEndFrame = Mathf.Min(Mathf.Max(stopEndFrame, Time.frameCount + frames), stopStartFrame + stopCap);
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
            if (stopWeight == (int)Weight.HomeRun) {
                HomeRunHeld = Mathf.Max(HomeRunHeld, held);
            } else if (stopWeight == (int)Weight.Finisher) {
                FinisherHeld = Mathf.Max(FinisherHeld, held);
            } else if (stopWeight == (int)Weight.Light) {
                LightHeld = LightHeld == 0 ? held : Mathf.Max(LightHeld, held);
            }
            if (stopWeight >= 0) {
                HitLog.Add("hit-stop held " + held + " frames (" + (Weight)stopWeight + "), kick decayed in "
                    + ZeldaStyleCamera.KickDecaySeconds.ToString("0.00") + " s");
            }
            stopWeight = -1;
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
