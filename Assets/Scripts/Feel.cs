using Hawkes.Playtest;
using UnityEngine;

// What the controls feel like, as numbers the playtest reads: the 150 ms press buffer for Swing and Dodge, the
// latency from a press (once Wallit could act) to the first visible response, presses that were lost, and the
// hit-stop in use. Gameplay calls one line of it at the real moment, never per frame.
public static class Feel {
    public const float BufferSeconds = 0.15f;

    public static int SwingPresses { get; private set; }
    public static int DodgePresses { get; private set; }
    public static int Dropped { get; private set; }
    public static int Buffered { get; private set; }
    public static int CancelledSwings { get; private set; }
    public static float SwingLatencyMs { get; private set; }
    public static float DodgeLatencyMs { get; private set; }
    public static float DodgeFromSwingMs { get; private set; }
    public static float MaxBufferWaitMs { get; private set; }
    public static string Last = "";

    public static void Reset() {
        SwingPresses = 0;
        DodgePresses = 0;
        Dropped = 0;
        Buffered = 0;
        CancelledSwings = 0;
        SwingLatencyMs = 0f;
        DodgeLatencyMs = 0f;
        DodgeFromSwingMs = 0f;
        MaxBufferWaitMs = 0f;
        Last = "";
    }

    public static float Now {
        get { return Time.realtimeSinceStartup; }
    }

    public static void PressSwing() {
        SwingPresses++;
    }

    public static void PressDodge() {
        DodgePresses++;
    }

    // A press that could not act at once and waited in the buffer.
    public static void Waited(float seconds) {
        Buffered++;
        MaxBufferWaitMs = Mathf.Max(MaxBufferWaitMs, seconds * 1000f);
    }

    // A press that got no answer: overwritten while it waited, or out of time.
    public static void Drop(string why) {
        Dropped++;
        Last = "dropped " + why;
    }

    // The swing began; pressAt and readyAt are Feel.Now stamps. Latency counts from when Wallit could first act.
    public static void SwingStarted(float pressAt, float readyAt) {
        float ms = Mathf.Max(0f, (Now - Mathf.Max(pressAt, readyAt)) * 1000f);
        SwingLatencyMs = Mathf.Max(SwingLatencyMs, ms);
    }

    public static void DodgeMoved(float pressAt, bool outOfSwing) {
        float ms = Mathf.Max(0f, (Now - pressAt) * 1000f);
        DodgeLatencyMs = Mathf.Max(DodgeLatencyMs, ms);
        if (outOfSwing) {
            DodgeFromSwingMs = Mathf.Max(DodgeFromSwingMs, ms);
        }
    }

    public static void Report(PlaytestRun run) {
        if (run == null) {
            return;
        }
        float latency = Mathf.Max(SwingLatencyMs, DodgeLatencyMs);
        run.Metric("feel.inputLatencyMs", Mathf.Round(latency));
        run.Metric("feel.swingLatencyMs", Mathf.Round(SwingLatencyMs));
        run.Metric("feel.dodgeLatencyMs", Mathf.Round(DodgeLatencyMs));
        run.Metric("feel.dodgeOutOfSwingMs", Mathf.Round(DodgeFromSwingMs));
        run.Metric("feel.bufferMs", Mathf.Round(BufferSeconds * 1000f));
        run.Metric("feel.bufferedPresses", Buffered);
        run.Metric("feel.maxBufferWaitMs", Mathf.Round(MaxBufferWaitMs));
        run.Metric("combat.droppedInputs", Dropped);
        run.Metric("combat.swingPresses", SwingPresses);
        run.Metric("combat.dodgePresses", DodgePresses);
        run.Metric("feel.hitStopMs", Mathf.Round(Juice.HeaviestStopMs));
        run.Event("feel", "swing presses " + SwingPresses + ", dodge presses " + DodgePresses + ", buffered " + Buffered + ", dropped " + Dropped
            + ", swing latency " + SwingLatencyMs.ToString("0") + " ms, dodge latency " + DodgeLatencyMs.ToString("0") + " ms (out of a swing " + DodgeFromSwingMs.ToString("0") + " ms)");
    }
}
