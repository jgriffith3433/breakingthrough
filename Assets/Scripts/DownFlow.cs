using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// The down beat: when Slice falls (Arcade and chapters) or Wallit goes down (a campaign level), the game does not freeze. For
// 0.8 s unscaled it runs at a crawl, the camera holds on whoever fell (an intended cut: CameraFeel is told), one existing
// voiced line plays (Wallit calls for Slice, Slice calls for Wallit) and the music dips. Then `then` runs: the run-end sheet
// (WaveDirector.Lose) or Wallit's checkpoint return (LevelScene).
//
// Every down has a way out. The beat runs on unscaled time from a host that outlives scene loads, it restores Time.timeScale
// itself, a watchdog ends it after MaxSeconds whatever else is open, and a scene change cancels it. A down that was already
// under way ignores a second one (Musician asks every frame while her defeat clip plays). The playtest reads State, Frames
// and the counters; `Stalls` counts the ways a down could hang: a beat that overran, a timescale left off 1 with nothing open
// to explain it, or control that never came back.
public class DownFlow : MonoBehaviour {
    public enum Phase { None, Beat, Sheet, Leaving }

    public const float SlowScale = 0.2f;
    public const float BeatSeconds = 0.8f;      // 0.5 to 1 s, on unscaled time
    public const float MaxSeconds = 1.3f;       // the watchdog
    public const float StallSeconds = 3f;       // control back after Retry within this, or it is a stall

    public static Phase State { get; private set; }
    public static bool Active { get { return State == Phase.Beat; } }
    public static int Downs { get; private set; }
    public static int Stalls { get; private set; }
    public static int Cuts { get; private set; }
    public static int Voiced { get; private set; }
    public static string LastLine { get; private set; } = "";
    public static string LastWho { get; private set; } = "";
    public static float LastBeatSeconds { get; private set; }
    public static float LowestScale { get; private set; } = 1f;
    public static float LowestMusic { get; private set; } = 1f;
    public static float SheetAt { get; private set; } = -1f;
    public static float ControlBackAt { get; private set; } = -1f;
    public static int RetryTaps { get; private set; }
    public static readonly System.Collections.Generic.List<string> Events = new System.Collections.Generic.List<string>();

    // True while the player cannot act because of a down: the beat, or the run-end sheet.
    public static bool InputLocked { get { return State != Phase.None || WaveDirector.IsLost; } }

    static DownFlow host;
    float startedAt;
    float endsAt;
    Action then;
    bool camHeld;
    bool wasArmed;
    Vector3 holdPos;
    Quaternion holdRot;
    float sheetStall;

    static DownFlow Host() {
        if (host == null) {
            var go = new GameObject("Down Flow");
            DontDestroyOnLoad(go);
            host = go.AddComponent<DownFlow>();
            SceneManager.sceneLoaded += (scene, mode) => { if (host != null) host.OnSceneLoaded(); };
        }
        return host;
    }

    // Resets the counters for a playtest scenario.
    public static void ResetCounters() {
        Downs = 0;
        Stalls = 0;
        Cuts = 0;
        Voiced = 0;
        RetryTaps = 0;
        LowestScale = 1f;
        LowestMusic = 1f;
        SheetAt = -1f;
        ControlBackAt = -1f;
        Events.Clear();
    }

    // Slice fell (wallit false) or Wallit is down (wallit true). `then` runs once, when the beat ends. Returns false when a
    // down is already under way (the caller's `then` is dropped: the first one still runs).
    public static bool Begin(bool wallitDown, Action then) {
        if (State == Phase.Beat) {
            return false;
        }
        DownFlow flow = Host();
        flow.Run(wallitDown, then);
        return true;
    }

    // The run-end sheet is up (WaveDirector says so): the stall clock for control-back starts there.
    public static void NoteSheet() {
        State = Phase.Sheet;
        SheetAt = Time.unscaledTime;
        ControlBackAt = -1f;
        Note("sheet", "run-end sheet up");
    }

    public static void NoteRetryTap() {
        RetryTaps++;
        State = Phase.Leaving;
        Note("retry-tap", "taps " + RetryTaps);
    }

    // The new scene has control of the player again (WaveDirector.Start or LevelScene after a resume).
    public static void NoteControlBack(string where) {
        if (State == Phase.Beat) {
            return;
        }
        ControlBackAt = Time.unscaledTime;
        if (State == Phase.Leaving || State == Phase.Sheet) {
            float took = SheetAt >= 0f ? ControlBackAt - SheetAt : 0f;
            Note("control-back", where + " after " + took.ToString("0.00") + " s");
            if (took > StallSeconds + 2f) {
                Stalls++;
            }
        }
        State = Phase.None;
    }

    public static void Note(string kind, string text) {
        Events.Add(kind + "|" + text);
    }

    void Run(bool wallitDown, Action after) {
        State = Phase.Beat;
        Downs++;
        then = after;
        startedAt = Time.unscaledTime;
        endsAt = startedAt + BeatSeconds;
        Time.timeScale = SlowScale;
        LowestScale = Mathf.Min(LowestScale, Time.timeScale);
        Note("slow-mo", "down " + Downs + (wallitDown ? " Wallit" : " Slice") + " scale " + SlowScale.ToString("0.00") + " for " + BeatSeconds.ToString("0.0") + " s");
        HoldCamera(wallitDown);
        Speak(wallitDown);
        AudioMix.Dip(BeatSeconds + 0.5f);
        LowestMusic = Mathf.Min(LowestMusic, AudioMix.MusicDuck);
    }

    // The camera frames the one who fell from the side and a little above: a held pose, one intended cut.
    void HoldCamera(bool wallitDown) {
        Camera cam = Camera.main;
        ZeldaStyleCamera zelda = cam != null ? cam.GetComponent<ZeldaStyleCamera>() : null;
        Transform who = null;
        if (wallitDown) {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            who = player != null ? player.transform : null;
        } else if (Musician.Instance != null) {
            who = Musician.Instance.transform;
        }
        if (cam == null || zelda == null || who == null) {
            Note("camera-hold", "none (no camera or subject)");
            return;
        }
        // Keep the side the camera is already on: the hold starts where it stands, pushes in and settles on the subject.
        Vector3 focus = who.position + Vector3.up * 1.2f;
        Vector3 away = cam.transform.position - focus;
        away.y = 0f;
        if (away.sqrMagnitude < 0.5f) {
            away = -who.forward * 4f;
        }
        away = away.normalized * 4.5f;
        holdPos = focus + away + Vector3.up * 1.6f;
        holdRot = Quaternion.LookRotation(focus - holdPos, Vector3.up);
        CameraFeel feel = CameraFeel.Instance;
        if (feel != null) {
            feel.Arm(false);   // an intended cut: the feel sensor does not count it as a snap
        }
        wasArmed = feel != null;
        zelda.CinematicHold(holdPos, holdRot);
        camHeld = true;
        Cuts++;
        Note("camera-cut", "hold on " + (wallitDown ? "Wallit" : "Slice"));
    }

    // One existing voiced line: Wallit calls for Slice when she falls, Slice calls for Wallit when he does.
    void Speak(bool wallitDown) {
        string line = wallitDown ? "Wallit. Now." : "Slice! Hold on!";
        WallitVoice.Silence();
        SliceVoice.Silence();
        float len = wallitDown ? SliceVoice.Cutscene(line) : WallitVoice.Cutscene(line);
        LastLine = line;
        LastWho = wallitDown ? "slice" : "wallit";
        Voiced++;
        GameAudio.NoteOther(GameAudio.Bus.Voice, LastWho + " \"" + line + "\" (" + len.ToString("0.0") + " s)");
        Note("voice", LastWho + ": " + line);
    }

    void Update() {
        if (State == Phase.Beat) {
            if (camHeld) {
                Camera cam = Camera.main;
                ZeldaStyleCamera zelda = cam != null ? cam.GetComponent<ZeldaStyleCamera>() : null;
                if (zelda != null) {
                    // A slow push toward the one who fell, on unscaled time.
                    float u = Mathf.Clamp01((Time.unscaledTime - startedAt) / BeatSeconds);
                    Vector3 pos = Vector3.Lerp(holdPos, holdPos + holdRot * Vector3.forward * 0.9f, u);
                    zelda.CinematicHold(pos, holdRot);
                }
            }
            Time.timeScale = SlowScale;
            LowestMusic = Mathf.Min(LowestMusic, AudioMix.MusicDuck);
            float now = Time.unscaledTime;
            if (now >= endsAt || now - startedAt > MaxSeconds) {
                if (now - startedAt > MaxSeconds) {
                    Stalls++;
                    Note("stall", "beat overran " + (now - startedAt).ToString("0.00") + " s");
                }
                Finish();
            }
            return;
        }
        // A timescale left off 1 with nothing to explain it is the freeze the bot used to sit in.
        if (State == Phase.None && Time.timeScale < 0.99f && !Explained()) {
            sheetStall += Time.unscaledDeltaTime;
            if (sheetStall > 1.5f) {
                sheetStall = 0f;
                Stalls++;
                Note("stall", "timescale " + Time.timeScale.ToString("0.00") + " with nothing open: put back to 1");
                Time.timeScale = 1f;
            }
        } else {
            sheetStall = 0f;
        }
    }

    // A pause, card, scene or the run-end sheet may hold the clock; the slow-motion of a boss entrance or a card's ease too.
    static bool Explained() {
        return PauseMenu.IsOpen || LevelUpScreen.IsOpen || StoryScreen.IsOpen || Cutscene.IsOpen || WaveDirector.IsLost
            || CinematicDirector.BossOpen || ScreenTransition.Busy;
    }

    void Finish() {
        LastBeatSeconds = Time.unscaledTime - startedAt;
        Time.timeScale = 1f;
        if (camHeld) {
            camHeld = false;
            Camera cam = Camera.main;
            ZeldaStyleCamera zelda = cam != null ? cam.GetComponent<ZeldaStyleCamera>() : null;
            if (zelda != null) {
                zelda.SnapBehind();   // let go of the hold; a lost run's sheet covers it, a checkpoint return starts behind him
            }
            if (wasArmed && CameraFeel.Instance != null) {
                CameraFeel.Instance.Arm(true);
            }
        }
        Action run = then;
        then = null;
        State = Phase.None;
        Note("beat-end", LastBeatSeconds.ToString("0.00") + " s, timescale " + Time.timeScale.ToString("0.00"));
        if (run != null) {
            try {
                run();
            } catch (Exception e) {
                Stalls++;
                Debug.LogException(e);
            }
        }
    }

    void OnSceneLoaded() {
        if ((State == Phase.Sheet || State == Phase.Leaving) && SceneManager.GetActiveScene().name == MetaProgress.MenuScene) {
            State = Phase.None;   // left for the menu: nothing of the down is still open
        }
        if (State == Phase.Beat) {
            then = null;
            camHeld = false;
            Time.timeScale = 1f;
            State = Phase.None;
            Note("cancelled", "scene changed during the beat");
        }
    }
}
