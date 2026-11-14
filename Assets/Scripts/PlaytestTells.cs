using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;

// The "tells" scenario (AttackTell.cs): every bot blow is seen coming. Arcade waves 1 to 3, then the wave 10 boss (setup: the
// testing wave key). Desktop by keys and mouse, phone (390x844) by fingers only: stick, Swing, cards, Next wave.
//   - waves 1-3: each regular blow's glow on the striking limb and ground marker lead it by 0.4-0.8 s (combat.tellLeadMs, min and
//     max), and no blow lands untold (combat.untelegraphedHits). Wallit walks away from the circle for a while (the stick or
//     WASD, no teleport) so bots reach Slice: her circle pulses and, with her off screen, the screen edge lights toward her.
//   - wave 10: the Plated Alpha's heavy attacks draw their ground marks at least 0.6 s before they land (boss.telegraphLeadMs).
public static partial class Playtest {
    static IEnumerator TellsRun(PlaytestOptions options) {
        animTouch = Screen.height > Screen.width || Screen.height < 500;
        UiFit.ForceTouch(animTouch);
        if (animTouch) {
            MobileQuality.Apply();
            run.Event("mobile-quality", MobileQuality.Describe());
        }
        run.Event("tells-setup", (animTouch ? "touch 390x844 phone tier, fingers only" : "keys and mouse") + ", bot wind-ups " + CharacterRig.YoWind.ToString("0.00") + " s (Yo Bot) and "
            + CharacterRig.SpiderWind.ToString("0.00") + " s (Spider Bot)");
        yield return run.Until(() => WaveDirector.Instance != null && Enemy.Roster.Count > 0, 40f, "wave 1's bots");
        if (run.Done) {
            yield break;
        }
        var player = Object.FindAnyObjectByType<CharacterMovement>();

        run.Scene("Tells: wave 1, the glow and the ground marker");
        yield return AnimFight(player, 40f, () => AttackTell.LeadMs.Count >= 3);
        TellsNote("wave-1");
        if (run.Done) {
            yield break;
        }

        run.Scene("Tells: Slice's circle with Wallit away");
        run.Event("setup", "Wallit walks away from the circle (real input) so the bots reach Slice off screen; her health is topped up");
        yield return TellsWalkAway(player, 26f, () => AttackTell.EdgeTints >= 1 && AttackTell.Pulses >= 3);
        TellsNote("walk-away");
        if (run.Done) {
            yield break;
        }

        run.Scene("Tells: waves 2 and 3");
        float start = Time.realtimeSinceStartup;
        while (!run.Done && !WaveDirector.IsLost && WaveDirector.Wave <= 3 && Time.realtimeSinceStartup - start < 150f) {
            yield return AnimFight(player, 3f, null);
        }
        TouchControls.LiftBotFingers();
        animStickDown = false;
        PlayInput.Release();
        if (run.Done) {
            yield break;
        }
        TellsNote("wave-3");

        // The verdict for waves 1 to 3 comes before the boss wave adds its own blows.
        float min = 0f, max = 0f, mean = 0f;
        TellsStats(AttackTell.LeadMs, out min, out max, out mean);
        int untold = AttackTell.Untelegraphed;
        run.Metric("combat.tellLeadMs", Mathf.Round(mean));
        run.Metric("combat.tellLeadMs.min", Mathf.Round(min));
        run.Metric("combat.tellLeadMs.max", Mathf.Round(max));
        run.Metric("combat.tellCount", AttackTell.LeadMs.Count);
        run.Metric("combat.untelegraphedHits", untold);
        run.Metric("combat.circlePulses", AttackTell.Pulses);
        run.Metric("combat.circleReached", AttackTell.CircleReached);
        run.Metric("combat.edgeTints", AttackTell.EdgeTints);
        run.Event("tells-waves-1-3", AttackTell.LeadMs.Count + " blows told, lead " + min.ToString("0") + "-" + max.ToString("0") + " ms (mean " + mean.ToString("0") + "), "
            + untold + " untelegraphed, " + AttackTell.Pulses + " circle pulses, " + AttackTell.EdgeTints + " edge tints");
        if (AttackTell.LeadMs.Count == 0) {
            run.Problem(PlaytestSeverity.Bug, "no bot blow was timed against its tell in waves 1-3");
        } else if (min < AttackTell.MinLead * 1000f - 1f || max > AttackTell.MaxLead * 1000f + 1f) {
            run.Problem(PlaytestSeverity.Bug, "bot tells led their blows by " + min.ToString("0") + "-" + max.ToString("0") + " ms (want 400-800)");
        }
        if (untold > 0) {
            run.Problem(PlaytestSeverity.Bug, untold + " bot blows landed with no tell (combat.untelegraphedHits)");
        }
        if (AttackTell.Pulses == 0) {
            run.Problem(PlaytestSeverity.Bug, "Slice's circle never pulsed when a bot reached her");
        }
        if (AttackTell.EdgeTints == 0) {
            run.Problem(PlaytestSeverity.Bug, "the screen edge never tinted toward Slice while she was off screen with a bot at her circle");
        }
        if (animTouch) {
            run.Metric("combat.edgeTintLowestY", Mathf.Round(AttackTell.EdgeTintLowest * 100f) / 100f);
            run.Metric("combat.edgeTintPadOverlaps", AttackTell.EdgeTintOverlaps);
            run.Metric("combat.markerPadOverlaps", AttackTell.MarkerPadOverlaps);
            if (AttackTell.EdgeTintOverlaps > 0) {
                run.Problem(PlaytestSeverity.Bug, "the edge tint overlapped the stick or a pad on " + AttackTell.EdgeTintOverlaps + " frames at 390x844");
            }
            if (AttackTell.MarkerPadOverlaps > 0) {
                run.Problem(PlaytestSeverity.Warning, AttackTell.MarkerPadOverlaps + " ground markers began under the stick or a pad at 390x844");
            }
        }

        run.Scene("Tells: the wave 10 boss's heavy attacks");
        run.Event("setup", "testing wave key to wave 10 (the Plated Alpha)");
        yield return GoWave(10);
        if (run.Done || WaveDirector.IsLost) {
            yield break;
        }
        WaveDirector.Instance.TrimForPlaytest();
        int intros = BossSetPiece.Intros;
        yield return run.Until(() => BossSetPiece.Intros > intros, 60f, "the wave 10 boss intro");
        if (run.Done) {
            yield break;
        }
        yield return run.Until(() => !BossSetPiece.Active, 10f, "the intro to end");
        float bossStart = Time.realtimeSinceStartup;
        float nextHeal = 0f;
        bool sawMark = false;
        while (!run.Done && !WaveDirector.IsLost && Time.realtimeSinceStartup - bossStart < 110f && AttackTell.BossLeadMs.Count < 3) {
            if (Time.realtimeSinceStartup >= nextHeal && Musician.Instance != null && Musician.Instance.Alive) {
                nextHeal = Time.realtimeSinceStartup + 4f;
                Musician.Instance.Heal(100);
            }
            if (BossPhases.MarkUp && !sawMark) {
                sawMark = true;
                run.Event("boss-mark", "the boss's ground mark is up (" + BossPhases.Tells + " tells so far)");
            } else if (!BossPhases.MarkUp) {
                sawMark = false;
            }
            yield return AnimFight(player, 0.5f, null);
        }
        TouchControls.LiftBotFingers();
        PlayInput.Release();
        if (run.Done) {
            yield break;
        }
        float bmin, bmax, bmean;
        TellsStats(AttackTell.BossLeadMs, out bmin, out bmax, out bmean);
        run.Metric("boss.telegraphLeadMs", Mathf.Round(bmin));
        run.Metric("boss.telegraphLeadMs.max", Mathf.Round(bmax));
        run.Metric("boss.telegraphCount", AttackTell.BossLeadMs.Count);
        run.Metric("combat.untelegraphedHits.total", AttackTell.Untelegraphed);
        run.Event("tells-boss", AttackTell.BossLeadMs.Count + " heavy attacks marked, lead " + bmin.ToString("0") + "-" + bmax.ToString("0") + " ms, " + AttackTell.Untelegraphed + " untelegraphed in all");
        if (AttackTell.BossLeadMs.Count == 0) {
            run.Problem(PlaytestSeverity.Bug, "the wave 10 boss never landed a marked heavy attack");
        } else if (bmin < 600f) {
            run.Problem(PlaytestSeverity.Bug, "a boss heavy attack was marked only " + bmin.ToString("0") + " ms before it landed (want 600+)");
        }
        if (AttackTell.Untelegraphed > untold) {
            run.Problem(PlaytestSeverity.Bug, (AttackTell.Untelegraphed - untold) + " bot blows landed with no tell in the boss wave");
        }
        foreach (string line in AttackTell.Log) {
            run.Event("tell-log", line);
        }
        run.Finish(PlaytestOutcome.Done);
    }

    static void TellsStats(List<float> values, out float min, out float max, out float mean) {
        min = 0f;
        max = 0f;
        mean = 0f;
        if (values.Count == 0) {
            return;
        }
        min = float.MaxValue;
        for (int i = 0; i < values.Count; i++) {
            min = Mathf.Min(min, values[i]);
            max = Mathf.Max(max, values[i]);
            mean += values[i];
        }
        mean /= values.Count;
    }

    static void TellsNote(string where) {
        run.Event("tells", where + ": glows " + AttackTell.Glows + ", markers " + AttackTell.Markers + ", blows told " + AttackTell.LeadMs.Count + ", untelegraphed "
            + AttackTell.Untelegraphed + ", at circle " + AttackTell.CircleReached + ", pulses " + AttackTell.Pulses + ", edge tints " + AttackTell.EdgeTints);
    }

    // Wallit walks directly away from Slice (WASD, or the stick by finger) while bots close on her; no swings.
    static IEnumerator TellsWalkAway(CharacterMovement player, float seconds, System.Func<bool> stop) {
        float start = Time.realtimeSinceStartup;
        float nextHeal = 0f;
        TouchControls.LiftBotFingers();
        animStickDown = false;
        PlayInput.Hold(KeyCode.Mouse0, false);
        PlayInput.Hold(KeyCode.LeftShift, false);
        while (!run.Done && !WaveDirector.IsLost && Time.realtimeSinceStartup - start < seconds && (stop == null || !stop())) {
            Musician slice = Musician.Instance;
            if (slice == null || player == null) {
                break;
            }
            if (Time.realtimeSinceStartup >= nextHeal && slice.Alive) {
                nextHeal = Time.realtimeSinceStartup + 3f;
                slice.Heal(100);
            }
            if (LevelUpScreen.IsOpen) {
                PlayInput.Tap(KeyCode.Alpha1);
                yield return null;
                continue;
            }
            Vector3 away = player.transform.position - slice.transform.position;
            away.y = 0f;
            if (away.magnitude < 0.5f) {
                away = Vector3.forward;
            }
            bool far = away.magnitude > 22f;
            Vector2 dir = far ? Vector2.zero : StickToward(away);
            if (animTouch) {
                if (dir.magnitude > 0.05f) {
                    if (!animStickDown) {
                        if (TouchControls.Spot("Stick", out Vector2 at)) {
                            animStickAt = at;
                            TouchControls.BotFinger(0, at, true);
                            animStickDown = true;
                        }
                    } else {
                        TouchControls.BotFinger(0, animStickAt + Vector2.ClampMagnitude(dir, 1f) * TouchControls.StickReach * 0.9f, true);
                    }
                } else if (animStickDown) {
                    TouchControls.BotFinger(0, animStickAt, false);
                    animStickDown = false;
                }
            } else {
                PlayInput.Hold(KeyCode.W, dir.y > 0.35f);
                PlayInput.Hold(KeyCode.S, dir.y < -0.35f);
                PlayInput.Hold(KeyCode.D, dir.x > 0.35f);
                PlayInput.Hold(KeyCode.A, dir.x < -0.35f);
            }
            yield return null;
        }
        TouchControls.LiftBotFingers();
        animStickDown = false;
        PlayInput.Release();
    }
}
