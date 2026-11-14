using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.SceneManagement;

// Reports feel.worstHitchMs for every playtest scenario: the longest unscaled frame while play was under way.
// Frames inside a scene's first seconds (a load, the paper sheet, the warm-up behind the load card) are load
// stutter and are counted apart (feel.loadHitchMs). Every frame over 250 ms in play is logged with where it
// happened, so a long frame can be traced to the first use of a bot kind, boss or effect.
public class HitchMeter : MonoBehaviour {
    const float LoadGraceSeconds = 3f;
    const float LimitMs = 250f;

    static HitchMeter instance;
    static PlaytestRun run;
    static float worstMs;
    static float loadMs;
    static float graceUntil;
    static int told;

    public static float WorstMs { get { return worstMs; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook() {
        SceneManager.sceneLoaded -= Loaded;
        SceneManager.sceneLoaded += Loaded;
    }

    static void Loaded(Scene scene, LoadSceneMode mode) {
        if (mode == LoadSceneMode.Single) {
            graceUntil = Time.realtimeSinceStartup + LoadGraceSeconds;
        }
    }

    public static void Begin(PlaytestRun current) {
        run = current;
        worstMs = 0f;
        loadMs = 0f;
        told = 0;
        graceUntil = Time.realtimeSinceStartup + LoadGraceSeconds;
        if (instance == null) {
            var go = new GameObject("Hitch Meter");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<HitchMeter>();
        }
        run.Metric("feel.worstHitchMs", 0f);
    }

    public static void End() {
        run = null;
    }

    void Update() {
        if (run == null || run.Done) {
            return;
        }
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) {
            return;
        }
        float ms = dt * 1000f;
        float now = Time.realtimeSinceStartup;
        if (now < graceUntil || SceneWarmup.Active) {
            if (ms > loadMs) {
                loadMs = ms;
                run.Metric("feel.loadHitchMs", Mathf.Round(loadMs));
            }
            return;
        }
        if (ms > worstMs) {
            worstMs = ms;
            run.Metric("feel.worstHitchMs", Mathf.Round(worstMs));
        }
        if (ms > LimitMs && told < 12) {
            told++;
            run.Event("hitch", Mathf.Round(ms) + " ms in " + SceneManager.GetActiveScene().name + ", wave " + WaveDirector.Wave
                + ", bots " + Enemy.Roster.Count + ", run " + Mathf.Round(now - run.StartTime) + " s");
        }
    }
}
