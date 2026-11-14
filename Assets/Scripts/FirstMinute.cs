using UnityEngine;

// The first-minute milestones of an Arcade run (J, Nov 13 runs: first success reported "never"): gameplay (the run's
// scene is live), first-input-accepted (the first key, tap, stick push or click the game takes) and first-success (the
// first kill). Each is reported once per page, as seconds from the page opening, to the playtest bridge
// (window.hawkesPlaytest.milestone on the page, and Playtest.Milestone for a scenario's event and metric).
public static class FirstMinute {
    public const string Gameplay = "gameplay";
    public const string FirstInput = "first-input-accepted";
    public const string FirstSuccess = "first-success";

    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void HawkesMilestone(string name, string note);

    static bool gameplay, input, success;
    static float gameplayAt = -1f;

    // Seconds since the page opened (the player's start).
    public static float Now { get { return Time.realtimeSinceStartup; } }
    public static bool SuccessSeen { get { return success; } }

    // Each playtest run starts a fresh page or a fresh scenario; Arcade Start calls this so a second run reports again.
    public static void Reset() {
        gameplay = false;
        input = false;
        success = false;
        gameplayAt = -1f;
    }

    // Called by WaveDirector.Start on an Arcade run.
    public static void BeginGameplay() {
        Reset();
        gameplay = true;
        gameplayAt = Now;
        Report(Gameplay, "arcade wave " + WaveDirector.Wave);
    }

    // Called every frame by WaveDirector while Arcade plays; reads the same PlayInput the bat and the stick read.
    public static void Tick() {
        if (!gameplay || input) {
            return;
        }
        bool touched = false;
        for (int i = 0; i < Input.touchCount; i++) {
            if (Input.GetTouch(i).phase == TouchPhase.Began) {
                touched = true;
            }
        }
        if (PlayInput.AnyKeyDown || PlayInput.GetMouseButtonDown(0) || touched
            || Mathf.Abs(PlayInput.GetAxis("Horizontal")) > 0.15f || Mathf.Abs(PlayInput.GetAxis("Vertical")) > 0.15f) {
            input = true;
            Report(FirstInput, (Now - gameplayAt).ToString("0.0") + " s after gameplay");
        }
    }

    // Called by Combat.OnKill. A kill with no earlier input still counts the input (a bat swing is input).
    public static void NoteKill() {
        if (!gameplay || success) {
            return;
        }
        if (!input) {
            input = true;
            Report(FirstInput, "a kill came first");
        }
        success = true;
        Report(FirstSuccess, "first kill, " + (Now - gameplayAt).ToString("0.0") + " s after gameplay");
    }

    static void Report(string name, string note) {
#if UNITY_WEBGL && !UNITY_EDITOR
        try { HawkesMilestone(name, note); } catch (System.Exception) { }
#endif
        Playtest.Milestone(name, Now, note);
    }
}
