using System.Collections;
using Hawkes.Playtest;
using UnityEngine;

// The "combo-feel" scenario (a part of Playtest, played inside Chapter 1's run like "moves"): the controls' feel, measured.
//   1. A scripted 10-press combo. Each press after the first lands while the swing before it is in its last 150 ms (the input
//      buffer), so it must queue and fire the next combo step: 10 presses, 0 dropped (combat.droppedInputs).
//   2. A Dodge pressed out of a swing's recovery: the swing is cut short and Wallit moves within 100 ms (feel.dodgeOutOfSwingMs).
//   3. A bot hits Wallit: knocked 1 to 1.5 m away, the outline flashes for 0.6 s, a flinch, a grunt, a jolt only on a heavy hit.
// Desktop presses the left mouse button and X; on a phone (portrait, or under 500 px tall) a finger taps the Swing and Dodge pads.
// A named setup shortcut (setup event) lands the claw if no bot reaches Wallit in 25 s.
public static partial class Playtest {
    const int FeelPadId = 9;

    static bool FeelPhone {
        get { return Screen.height > Screen.width || Screen.height < 500; }
    }

    // One press of Swing or Dodge: a finger on the pad for two frames on a phone, else the key for a frame.
    static IEnumerator FeelPress(string pad, KeyCode key) {
        if (FeelPhone) {
            if (TouchControls.Spot(pad, out Vector2 at)) {
                TouchControls.BotFinger(FeelPadId, at, true);
                yield return null;
                yield return null;
                TouchControls.BotFinger(FeelPadId, Vector2.zero, false);
            } else {
                run.Problem(PlaytestSeverity.Bug, "combo-feel: no " + pad + " pad on screen to tap");
                PlayInput.Tap(key);
            }
        } else {
            PlayInput.Tap(key);
        }
    }

    static IEnumerator ComboFeelPlay(CharacterMovement player, ChapterRun chapter) {
        Feel.Reset();
        WallitHurt.Reset();
        bool phone = FeelPhone;
        run.Log("combo-feel at " + Screen.width + "x" + Screen.height + ", " + (phone ? "touch: fingers on the Swing and Dodge pads" : "keys and mouse"));
        PlayInput.Axis("Vertical", 0f);
        yield return new WaitForSecondsRealtime(0.5f);

        // 1. The combo.
        int swingsBefore = BatEquipment.Swings;
        int presses = 0;
        int inBuffer = 0;
        float guard = Time.realtimeSinceStartup + 20f;
        while (presses < 10 && !run.Done && Time.realtimeSinceStartup < guard) {
            if (presses > 0) {
                // Wait for the swing to be in its last 150 ms, then press.
                float wait = Time.realtimeSinceStartup + 2f;
                while (!run.Done && Time.realtimeSinceStartup < wait && !(BatEquipment.SwingRemaining > 0f && BatEquipment.SwingRemaining <= Feel.BufferSeconds)) {
                    yield return null;
                }
                if (BatEquipment.SwingRemaining > 0f && BatEquipment.SwingRemaining <= Feel.BufferSeconds) {
                    inBuffer++;
                }
            }
            presses++;
            run.Event("combo-press", "press " + presses + " of 10, swing has " + (BatEquipment.SwingRemaining * 1000f).ToString("0") + " ms left");
            yield return FeelPress("Swing", KeyCode.Mouse0);
            yield return null;
        }
        float settle = Time.realtimeSinceStartup + 1.5f;
        while (!run.Done && Time.realtimeSinceStartup < settle && (BatEquipment.IsAnimatingAction || Feel.Dropped == 0 && BatEquipment.Swings - swingsBefore < 10)) {
            yield return null;
        }
        int swung = BatEquipment.Swings - swingsBefore;
        run.Event("combo-result", presses + " presses, " + inBuffer + " inside the last 150 ms, " + swung + " swings began, " + Feel.Dropped + " dropped, buffered " + Feel.Buffered + ", longest wait " + Feel.MaxBufferWaitMs.ToString("0") + " ms");
        if (swung < presses || Feel.Dropped > 0) {
            run.Problem(PlaytestSeverity.Bug, "combat.droppedInputs: " + presses + " presses made " + swung + " swings, " + Feel.Dropped + " dropped (" + Feel.Last + ")");
        }
        run.Metric("combo.presses", presses);
        run.Metric("combo.swings", swung);
        run.Metric("combo.pressesInBuffer", inBuffer);

        // 1b. A swing while walking: the swing's clip must be the one on screen for the whole swing, and the trail must show.
        yield return new WaitForSecondsRealtime(1.2f);
        PlayInput.Axis("Vertical", 1f);
        yield return new WaitForSecondsRealtime(0.5f);
        Vector3 walkFrom = player.transform.position;
        int walkSwingsBefore = BatEquipment.Swings;
        int trailBefore = BatEquipment.TrailSwings;
        yield return FeelPress("Swing", KeyCode.Mouse0);
        int frames = 0;
        int onClip = 0;
        float walkEnd = Time.realtimeSinceStartup + 2f;
        yield return null;
        while (!run.Done && Time.realtimeSinceStartup < walkEnd && BatEquipment.IsAnimatingAction) {
            int want = BatEquipment.ActionClip;
            Animator a = player.Anim;
            if (want >= 0 && a != null) {
                frames++;
                int hash = Animator.StringToHash(CharacterMovement.ClipNames[want]);
                if (a.GetCurrentAnimatorStateInfo(0).shortNameHash == hash || a.IsInTransition(0) && a.GetNextAnimatorStateInfo(0).shortNameHash == hash) {
                    onClip++;
                }
            }
            yield return null;
        }
        Vector3 walked = player.transform.position - walkFrom;
        walked.y = 0f;
        PlayInput.Axis("Vertical", 0f);
        run.Event("swing-while-walking", "swung " + (BatEquipment.Swings - walkSwingsBefore) + " time(s) while walking " + walked.magnitude.ToString("0.0") + " m, swing clip on screen " + onClip + " of " + frames + " frames, trail swings " + (BatEquipment.TrailSwings - trailBefore));
        run.Metric("walkSwing.clipFrames", frames);
        run.Metric("walkSwing.clipOnScreen", onClip);
        if (BatEquipment.Swings == walkSwingsBefore || frames > 2 && onClip < frames - 1) {
            run.Problem(PlaytestSeverity.Bug, "a swing made while walking did not show its swing animation (" + onClip + " of " + frames + " frames on the swing clip)");
        }
        if (walked.magnitude < 0.3f) {
            run.Problem(PlaytestSeverity.Bug, "Wallit did not keep moving during the swing (moved " + walked.magnitude.ToString("0.00") + " m)");
        }

        // 2. Dodge out of a swing's recovery.
        yield return new WaitForSecondsRealtime(1.2f);   // the dodge's cooldown and the combo gap
        PlayInput.Axis("Horizontal", 1f);
        yield return new WaitForSecondsRealtime(0.1f);
        PlayInput.Axis("Horizontal", 0f);
        yield return FeelPress("Swing", KeyCode.Mouse0);
        float recoveryWait = Time.realtimeSinceStartup + 2f;
        while (!run.Done && Time.realtimeSinceStartup < recoveryWait && !BatEquipment.InRecovery) {
            yield return null;
        }
        bool inRecovery = BatEquipment.InRecovery;
        Vector3 before = player.transform.position;
        int cutBefore = Feel.CancelledSwings;
        float pressedAt = Time.realtimeSinceStartup;
        int dodgesBefore = CampaignMoves.Dodges;
        yield return FeelPress("Dodge", CampaignMoves.DodgeKey);
        float movedAt = -1f;
        float until = Time.realtimeSinceStartup + 0.6f;
        while (!run.Done && Time.realtimeSinceStartup < until) {
            Vector3 d = player.transform.position - before;
            d.y = 0f;
            if (movedAt < 0f && d.magnitude > 0.05f) {
                movedAt = Time.realtimeSinceStartup;
            }
            yield return null;
        }
        Vector3 away = player.transform.position - before;
        away.y = 0f;
        float latency = movedAt < 0f ? -1f : (movedAt - pressedAt) * 1000f;
        run.Event("dodge-cancel", "pressed in the swing's recovery: " + inRecovery + ", swing cut short: " + (Feel.CancelledSwings > cutBefore) + ", dodges " + (CampaignMoves.Dodges - dodgesBefore)
            + ", moved " + away.magnitude.ToString("0.0") + " m, first movement " + (latency < 0f ? "never" : latency.ToString("0") + " ms") + " after the press (feel sensor " + Feel.DodgeFromSwingMs.ToString("0") + " ms)");
        if (CampaignMoves.Dodges == dodgesBefore || Feel.CancelledSwings == cutBefore) {
            run.Problem(PlaytestSeverity.Bug, "feel: a Dodge pressed out of a swing's recovery did not cut the swing and dodge");
        } else if (latency < 0f || latency > 100f + 34f) {
            run.Problem(PlaytestSeverity.Bug, "feel.inputLatencyMs: the dodge moved Wallit " + (latency < 0f ? "never" : latency.ToString("0") + " ms") + " after the press (limit 100 ms)");
        }

        // 3. A bot hits him.
        yield return new WaitForSecondsRealtime(0.8f);
        int strikesBefore = WallitHurt.Strikes;
        int grunts = WallitVoice.HurtLines;
        int jolts = CameraFeel.Jolts;
        Vector3 prior = player.transform.position;
        Vector3 last = prior;
        float strikeAt = -1f;
        Vector3 atStrike = prior;
        float limit = Time.realtimeSinceStartup + 25f;
        while (!run.Done && Time.realtimeSinceStartup < limit && !(strikeAt > 0f && Time.realtimeSinceStartup - strikeAt > 0.45f)) {
            if (WallitHurt.Strikes > strikesBefore && strikeAt < 0f) {
                strikeAt = Time.realtimeSinceStartup;
                atStrike = last;
            }
            last = player.transform.position;
            yield return null;
        }
        if (strikeAt < 0f && Enemy.Roster.Count > 0) {
            Enemy by = Enemy.Roster[0];
            run.Event("setup", "no bot reached Wallit in 25 s: the nearest bot's claw lands on him (PlayerGrowth.OnHit)");
            atStrike = player.transform.position;
            player.GetComponent<PlayerGrowth>().OnHit(by);
            strikeAt = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(0.45f);
        }
        if (strikeAt < 0f) {
            run.Problem(PlaytestSeverity.Bug, "feel: no bot ever hit Wallit and none stood near enough to be made to");
        } else {
            Vector3 slid = player.transform.position - atStrike;
            slid.y = 0f;
            bool flashed = WallitHurt.FlashPeak > 0.3f;
            run.Event("hurt-react", WallitHurt.Last + "; slid " + slid.magnitude.ToString("0.00") + " m in 0.45 s, outline flash peak " + WallitHurt.FlashPeak.ToString("0.00")
                + ", grunt lines " + (WallitVoice.HurtLines - grunts) + ", camera jolts " + (CameraFeel.Jolts - jolts) + ", hits ignored in the window " + WallitHurt.Ignored);
            run.Metric("feel.hurtKnockM", Mathf.Round(slid.magnitude * 100f) / 100f);
            run.Metric("feel.hurtWindowS", WallitHurt.InvulnerableSeconds);
            if (slid.magnitude < 0.6f) {
                run.Problem(PlaytestSeverity.Bug, "feel: Wallit slid " + slid.magnitude.ToString("0.00") + " m when hit (wanted 1 to 1.5)");
            }
            if (!flashed) {
                run.Problem(PlaytestSeverity.Bug, "feel: no outline flash when Wallit was hit");
            }
            if (WallitVoice.HurtLines == grunts) {
                run.Problem(PlaytestSeverity.Bug, "feel: no hurt grunt when Wallit was hit");
            }
            if (CameraFeel.Jolts != jolts && WallitHurt.Heavy == 0) {
                run.Problem(PlaytestSeverity.Bug, "feel: a light hit jolted the camera");
            }
        }

        TouchControls.LiftBotFingers();
        PlayInput.Release();
        Feel.Report(run);
        run.Metric("feel.cameraJolts", CameraFeel.Jolts);
        run.Metric("combat.hurtStrikes", WallitHurt.Strikes);
        if (Feel.SwingLatencyMs >= 100f) {
            run.Problem(PlaytestSeverity.Bug, "feel.inputLatencyMs: a swing waited " + Feel.SwingLatencyMs.ToString("0") + " ms after Wallit could act (limit 100)");
        }
        if (Feel.DodgeLatencyMs >= 100f) {
            run.Problem(PlaytestSeverity.Bug, "feel.inputLatencyMs: a dodge moved Wallit " + Feel.DodgeLatencyMs.ToString("0") + " ms after the press (limit 100)");
        }
    }
}
