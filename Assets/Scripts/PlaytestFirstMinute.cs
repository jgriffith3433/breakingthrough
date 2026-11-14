using System.Collections;
using Hawkes.Playtest;
using UnityEngine;

// Scenario "first-minute": Arcade's first success (J, Nov 13 runs). The game reports gameplay, first-input-accepted and
// first-success itself (FirstMinute.cs); this bot plays from the cold start and checks the numbers. On a phone viewport it
// plays with fingers only (the stick and Swing, through Batter).
public static partial class Playtest {
    const float FirstSuccessWithin = 10f;
    const float FirstBotReachWithin = 3f;

    static void PrepareFirstMinute() {
        bool touch = Screen.height > Screen.width || Screen.height < 500;
        UiFit.ForceTouch(touch);
        if (touch) {
            MobileQuality.Apply();
        }
    }

    static IEnumerator FirstMinuteRun(PlaytestOptions options) {
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        float start = Time.realtimeSinceStartup;
        float reachAt = -1f;
        float firstSwingAt = -1f;
        int swings0 = BatEquipment.Swings;
        float successAt = -1f;
        while (!run.Done && Time.realtimeSinceStartup - start < 25f) {
            if (LevelUpScreen.IsOpen) {
                PlayInput.Tap(KeyCode.Alpha1);
            } else {
                Batter(player, false, null, true);
            }
            float t = Time.realtimeSinceStartup - start;
            if (reachAt < 0f) {
                foreach (Enemy e in Enemy.Roster) {
                    if (e != null && player != null && Vector3.Distance(e.transform.position, player.transform.position) <= 4.5f) {
                        reachAt = t;
                        break;
                    }
                }
            }
            if (firstSwingAt < 0f && BatEquipment.Swings > swings0) {
                firstSwingAt = t;
            }
            if (successAt < 0f && FirstMinute.SuccessSeen) {
                successAt = t;
                break;
            }
            yield return null;
        }
        if (run.Done) {
            yield break;
        }
        float control = firstSwingAt >= 0f ? firstSwingAt : 0f;
        run.Event("first-bot-reach", reachAt >= 0f ? "a Yo Bot was within swing reach " + reachAt.ToString("0.0") + " s after Play" : "no Yo Bot within reach in 25 s");
        run.Metric("firstBotReachSeconds", reachAt >= 0f ? Mathf.Round(reachAt * 10f) / 10f : -1f);
        if (reachAt < 0f || reachAt > FirstBotReachWithin) {
            run.Problem(PlaytestSeverity.Bug, "the first Yo Bot was not in reach within " + FirstBotReachWithin.ToString("0") + " s (" + (reachAt < 0f ? "never" : reachAt.ToString("0.0") + " s") + ")");
        }
        if (successAt < 0f) {
            run.Metric("firstSuccessSeconds", -1f);
            run.Problem(PlaytestSeverity.Bug, "first-minute: no first success (kill) in 25 s");
        } else {
            float since = successAt - control;
            run.Event("first-success-seconds", since.ToString("0.0") + " s after the first control (first swing at " + control.ToString("0.0") + " s, kill at " + successAt.ToString("0.0") + " s after Play)");
            run.Metric("firstInputSeconds", Mathf.Round(control * 10f) / 10f);
            run.Metric("firstSuccessSeconds", Mathf.Round(since * 10f) / 10f);
            if (since > FirstSuccessWithin) {
                run.Problem(PlaytestSeverity.Bug, "first success came " + since.ToString("0.0") + " s after the first control (limit " + FirstSuccessWithin.ToString("0") + " s)");
            }
        }
        int hits = BatEquipment.Connects;
        run.Metric("firstKillHits", hits);
        run.Event("first-kill-hits", hits + " bat hits landed by the first kill");
        if (hits > 2 && successAt >= 0f && WaveDirector.Wave == 1) {
            run.Problem(PlaytestSeverity.Bug, "the first Yo Bot took " + hits + " bat hits (two wanted)");
        }
        yield return new WaitForSecondsRealtime(2f);   // let the payoff play
        run.Finish(PlaytestOutcome.Done);
    }
}
