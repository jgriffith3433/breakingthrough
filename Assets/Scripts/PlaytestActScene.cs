using System.Collections;
using Hawkes.Playtest;
using UnityEngine;

// The "act-change" scenario: Arcade's act II and act III arrive as 5 to 7 s staged events (CinematicAct.cs). Waves 11 and 21 are
// reached with the testing wave key (setup). Per scene: its length, the pulled-back shot with Wallit and Slice in frame, the
// world change (lamps flared, beacon sweeping), the act card in the upper third, and the touch pads back within 0.3 s on a
// phone. Then act II is set up again and a tap ends it early.
public static partial class Playtest {
    static float actCardTopFrac, actTouchBack;

    static IEnumerator ActChangeRun(PlaytestOptions options) {
        bool phone = Screen.height > Screen.width || Screen.height < 500;
        UiFit.ForceTouch(phone);
        if (phone) {
            MobileQuality.Apply();
        }
        CinematicDirector.ActScenesOn = true;
        run.Log("act-change at " + Screen.width + "x" + Screen.height + ", " + (phone ? "touch" : "keys and mouse"));
        yield return run.Until(() => WaveDirector.Instance != null && Enemy.Roster.Count > 0 && ArcadeActs.Instance != null, 30f, "wave 1's bots");
        if (run.Done) {
            yield break;
        }
        int[] waves = { 11, 21 };
        for (int i = 0; i < waves.Length && !run.Done; i++) {
            run.Event("setup", "testing wave key to wave " + waves[i]);
            int before = CinematicDirector.ActScenes;
            yield return GoWave(waves[i]);
            yield return run.Until(() => CinematicDirector.ActScenes > before, 10f, "the act " + (i + 2) + " scene");
            if (run.Done) {
                yield break;
            }
            yield return ActWatch("Act " + (i + 2), i + 1, phone, false);
        }
        // Skip: act II again by setup, a tap ends it.
        ArcadeActs.ForgetStaged();
        yield return GoWave(1);
        int b2 = CinematicDirector.ActScenes;
        yield return GoWave(11);
        yield return run.Until(() => CinematicDirector.ActScenes > b2, 10f, "the act II scene again");
        if (!run.Done) {
            yield return ActWatch("Act II (skipped)", 1, phone, true);
        }
        CinematicDirector.ActScenesOn = false;
        run.Metric("actCardTopFrac", Mathf.Round(actCardTopFrac * 100f) / 100f);
        if (phone) {
            run.Metric("actTouchBackSeconds", Mathf.Round(actTouchBack * 100f) / 100f);
        }
    }

    static IEnumerator ActWatch(string label, int act, bool phone, bool skip) {
        run.Scene("Act scene: " + label);
        float t0 = Time.unscaledTime;
        bool cardSeen = false, padsDuring = false, skipped = false, shot = false;
        float cardY = -1f;
        int lamps = 0;
        bool beacon = false, frame = false;
        while (CinematicDirector.ActOpen && !run.Done && Time.unscaledTime - t0 < 12f) {
            float t = Time.unscaledTime - t0;
            if (CinematicDirector.ActCardUp && !cardSeen) {
                cardSeen = true;
                cardY = CinematicDirector.ActCardCenterFromTop;
            }
            lamps = Mathf.Max(lamps, CinematicDirector.ActLampsFlared);
            beacon |= CinematicDirector.ActBeaconSweeping;
            frame |= CinematicDirector.ActPairInFrame;
            if (!shot && t > 2.8f) {
                shot = true;
                run.Event("act-shot", label + ": pulled back at " + t.ToString("0.0") + " s, lamps flared " + lamps + ", beacon " + beacon + ", look " + ArcadeActs.Describe());
            }
            if (phone && TouchControls.Instance != null && TouchControls.Instance.IsShown) {
                padsDuring = true;
            }
            if (skip && !skipped && t > 1.4f) {
                skipped = true;
                PlayInput.Tap(KeyCode.Mouse0);
            }
            yield return null;
        }
        float seconds = CinematicDirector.LastActSeconds;
        float endedAt = Time.unscaledTime;
        float back = -1f;
        if (phone) {
            while (Time.unscaledTime - endedAt < 1.5f && !(TouchControls.Instance != null && TouchControls.Instance.IsShown)) {
                yield return null;
            }
            back = Time.unscaledTime - endedAt;
            actTouchBack = Mathf.Max(actTouchBack, back);
        }
        run.Event("act-scene", label + ": " + seconds.ToString("0.0") + " s" + (skip ? " (skipped by a tap)" : "") + ", card " + cardSeen
            + (cardY >= 0f ? " at " + cardY.ToString("0.00") + " from the top" : "") + ", wallit and slice in frame " + frame
            + ", lamps " + lamps + ", beacon " + beacon + ", look " + ArcadeActs.Describe()
            + (phone ? ", pads hidden " + !padsDuring + ", back in " + back.ToString("0.00") + " s" : ""));
        if (skip) {
            run.Event("act-skip", label + ": a tap ended it after " + seconds.ToString("0.0") + " s");
            if (seconds > 3f) {
                run.Problem(PlaytestSeverity.Bug, "act-change: a tap did not skip the act scene (" + seconds.ToString("0.0") + " s)");
            }
        } else {
            run.Metric("actSceneSeconds", Mathf.Round(seconds * 10f) / 10f);
            if (seconds < 5f || seconds > 7f) {
                run.Problem(PlaytestSeverity.Bug, "act-change: " + label + " lasted " + seconds.ToString("0.0") + " s (want 5 to 7)");
            }
            if (!cardSeen) {
                run.Problem(PlaytestSeverity.Bug, "act-change: " + label + " never stamped its act card");
            }
            if (!frame) {
                run.Problem(PlaytestSeverity.Bug, "act-change: Wallit and Slice were not both in frame in the pulled-back shot of " + label);
            }
            if (act == 1) {
                run.Metric("actLampsFlared", lamps);
                if (lamps < 3) {
                    run.Problem(PlaytestSeverity.Bug, "act-change: only " + lamps + " lamp posts flared in " + label);
                }
            } else if (!beacon) {
                run.Problem(PlaytestSeverity.Bug, "act-change: the ruin's beacon never began to sweep in " + label);
            }
        }
        if (cardSeen) {
            actCardTopFrac = Mathf.Max(actCardTopFrac, 1f - cardY);
            if (cardY > 0.34f) {
                run.Problem(PlaytestSeverity.Bug, "act-change: " + label + " card is not in the upper third (centre " + cardY.ToString("0.00") + " from the top)");
            }
        }
        if (phone && padsDuring) {
            run.Problem(PlaytestSeverity.Bug, "act-change: " + label + " touch pads stayed up during the scene");
        }
        if (phone && back > 0.3f) {
            run.Problem(PlaytestSeverity.Bug, "act-change: " + label + " touch pads took " + back.ToString("0.00") + " s to return (want 0.3)");
        }
        yield return new WaitForSecondsRealtime(0.4f);
    }
}
