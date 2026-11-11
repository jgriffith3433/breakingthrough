using System.Collections;
using Hawkes.Playtest;
using UnityEngine;

// Scenario "stakes": the ring dims and the screen edges drain when a bot reaches Slice, the low layer starts under 30%, Wallit says
// one key line, and it all recovers. Touch viewport; no keys. Setup shortcut (named): one blow to push her under 30%.
public static class PlaytestStakes {
    public static IEnumerator Run(PlaytestRun run) {
        MetaProgress.EnterSandbox(true);
        UiFit.ForceTouch(true);
        ColdOpen.Suppress = true;
        yield return Playtest.ToMenuPublic();
        if (run.Done) {
            yield break;
        }
        yield return Playtest.StartArcadePublic();
        if (run.Done) {
            yield break;
        }
        run.Scene("Wave 1");
        yield return run.Until(() => Musician.Instance != null && StakesFeel.Instance != null, 30f, "Slice and the stakes layer");
        if (run.Done) {
            yield break;
        }
        Musician slice = Musician.Instance;
        yield return new WaitForSeconds(1f);
        Frames(run, "full health");
        float ringFull = StakesFeel.RingAlpha;

        // Hold fire: the bot does nothing until a Yo Bot reaches her.
        yield return run.Until(() => StakesFeel.Hits > 0, 90f, "a bot to reach Slice");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSeconds(0.25f);
        Frames(run, "just hit");

        // Setup: a blow to under 30% so the warning layer starts.
        run.Event("setup", "one blow takes Slice under 30%");
        slice.Hurt(Mathf.Max(1, slice.Health - Mathf.RoundToInt(slice.MaxHealth * 0.2f)));
        yield return run.Until(() => StakesFeel.LowNow && StakesFeel.LowLayerPlaying, 6f, "slice-low and the warning layer");
        yield return new WaitForSeconds(2.5f);
        Frames(run, "low");
        float drainLow = StakesFeel.Drain;
        float ringLow = StakesFeel.RingAlpha;
        bool layer = StakesFeel.LowLayerPlaying;
        if (!layer) {
            run.Problem(PlaytestSeverity.Bug, "Slice is under 30% but the warning layer is not playing");
        }

        // Let Wallit's line finish, then heal: the world recovers.
        yield return new WaitForSeconds(4f);
        slice.Heal(slice.MaxHealth);
        yield return new WaitForSeconds(5f);
        Frames(run, "recovered");
        float drainRec = StakesFeel.Drain;

        int lines = 0;
        foreach (string e in StakesFeel.Events) {
            if (e.StartsWith("stakes-line")) {
                lines++;
            }
        }
        for (int i = 0; i < StakesFeel.Events.Count && i < 12; i++) {
            string[] bits = StakesFeel.Events[i].Split(new[] { '|' }, 3);
            run.Event(bits[0], bits.Length > 2 ? bits[2] : "");
        }
        run.Event("stakes-overlap", "0: the grey edges are a post-process on the 3D view; no UI element is drawn by it");
        if (lines != 1) {
            run.Problem(PlaytestSeverity.Bug, "expected one stakes-line this run, saw " + lines);
        }
        if (WallitVoice.Overlaps > 0) {
            run.Problem(PlaytestSeverity.Bug, "voice lines overlapped " + WallitVoice.Overlaps + " time(s)");
        }
        if (drainLow < 0.4f || drainRec > drainLow * 0.6f) {
            run.Problem(PlaytestSeverity.Bug, "edges did not drain and recover (low " + drainLow.ToString("0.00") + ", recovered " + drainRec.ToString("0.00") + ")");
        }
        run.Metric("stakesHits", StakesFeel.Hits);
        run.Metric("stakesLines", lines);
        run.Metric("stakesVoiceOverlaps", WallitVoice.Overlaps);
        run.Metric("stakesDrainLow", drainLow);
        run.Metric("stakesDrainRecovered", drainRec);
        run.Metric("stakesRingFull", ringFull);
        run.Metric("stakesRingLow", ringLow);
    }

    static void Frames(PlaytestRun run, string moment) {
        run.Event("stakes-frames", moment + ": ring alpha " + StakesFeel.RingAlpha.ToString("0.00") + ", edge drain " + StakesFeel.Drain.ToString("0.00")
            + ", Slice " + (Musician.Instance != null ? Musician.Instance.Health + "/" + Musician.Instance.MaxHealth : "?"));
    }
}
