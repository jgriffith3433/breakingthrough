using System.Collections;
using Hawkes.Playtest;
using UnityEngine;

// The "tells" scenario: every blow is seen coming (CombatTells.cs). Arcade waves 1 to 3: the bot holds fire for a few seconds
// each wave so the Yo Bots reach Slice and wind up on her (red glow on the claw, a flat red mark under her, the circle pulsing,
// the screen edge glowing when she is off screen), then fights. Then wave 10 (setup: the wave key) for the Alpha's slam, held
// off until its red mark has filled and landed once, then 15 s of fighting. Desktop by keys; on a phone (mobile, 390x844)
// by fingers only. Metrics combat.tellLeadMsMin / Max, combat.untelegraphedHits, combat.windups, boss.telegraphLeadMs
// (shortest), combat.circlePulses, combat.edgeTints; on a phone the edge tint is checked against the stick and the Swing pad.
public static partial class Playtest {
    static IEnumerator TellsRun(PlaytestOptions options) {
        animTouch = Screen.height > Screen.width || Screen.height < 500;
        UiFit.ForceTouch(animTouch);
        if (animTouch) {
            MobileQuality.Apply();
        }
        run.Event("tells-setup", (animTouch ? "touch 390x844, fingers only" : "keys and mouse"));
        yield return run.Until(() => WaveDirector.Instance != null && Enemy.Roster.Count > 0, 30f, "wave 1's bots");
        if (run.Done) {
            yield break;
        }
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        bool padOverlap = false;
        string padWhat = "";

        // Waves 1 to 3.
        for (int wave = 1; wave <= 3 && !run.Done && !WaveDirector.IsLost; wave++) {
            run.Scene("Tells: wave " + wave);
            if (WaveDirector.Wave < wave) {
                run.Event("setup", "testing wave key to wave " + wave);
                yield return GoWave(wave);
                yield return new WaitForSecondsRealtime(1f);
            }
            int before = CombatTells.Windups;
            float hold = Time.realtimeSinceStartup;
            // Hold fire until three wind-ups have shown on the ground (or 25 s), watching the edge tint.
            while (!run.Done && !WaveDirector.IsLost && CombatTells.Windups - before < 3 && Time.realtimeSinceStartup - hold < 25f) {
                TouchControls.LiftBotFingers();
                PlayInput.Release();
                TellsEdgeCheck(ref padOverlap, ref padWhat);
                yield return null;
            }
            yield return AnimFight(player, 12f, () => false);
            TellsLog("wave-" + wave);
        }

        // The wave 10 boss.
        if (!run.Done && !WaveDirector.IsLost) {
            run.Scene("Tells: the wave 10 boss");
            run.Event("setup", "testing wave key to wave 10 for the boss");
            yield return GoWave(10);
            float start = Time.realtimeSinceStartup;
            while (!run.Done && !WaveDirector.IsLost && CombatTells.BossLeadsMs.Count < 1 && Time.realtimeSinceStartup - start < 60f) {
                if (LevelUpScreen.IsOpen) {
                    PlayInput.Tap(KeyCode.Alpha1);
                }
                TouchControls.LiftBotFingers();
                PlayInput.Release();
                TellsEdgeCheck(ref padOverlap, ref padWhat);
                run.Note("waiting on the boss's slam: " + BossSetPiece.Events.Count + " boss events");
                yield return null;
            }
            yield return AnimFight(player, 15f, () => false);
            TellsLog("boss");
        }
        TouchControls.LiftBotFingers();
        PlayInput.Release();
        if (run.Done) {
            yield break;
        }

        run.Metric("combat.tellLeadMsMin", Mathf.Round(CombatTells.MinLeadMs));
        run.Metric("combat.tellLeadMsMax", Mathf.Round(CombatTells.MaxLeadMs));
        run.Metric("combat.untelegraphedHits", CombatTells.Untelegraphed);
        run.Metric("combat.windups", CombatTells.Windups);
        run.Metric("combat.circlePulses", CombatTells.Pulses);
        run.Metric("combat.edgeTints", CombatTells.EdgeShows);
        run.Metric("boss.telegraphLeadMs", Mathf.Round(CombatTells.MinBossLeadMs));
        TellsLog("end");

        if (CombatTells.LeadsMs.Count == 0) {
            run.Problem(PlaytestSeverity.Bug, "no bot wind-up landed in waves 1 to 3, so no tell lead was measured");
        } else if (CombatTells.MinLeadMs < 400f || CombatTells.MaxLeadMs > 800f) {
            run.Problem(PlaytestSeverity.Bug, "wind-up tells led by " + CombatTells.MinLeadMs.ToString("0") + " to " + CombatTells.MaxLeadMs.ToString("0") + " ms (need 400 to 800)");
        }
        if (CombatTells.Untelegraphed > 0) {
            run.Problem(PlaytestSeverity.Bug, CombatTells.Untelegraphed + " bot hits landed with no tell: " + string.Join("; ", CombatTells.Events.FindAll(e => e.StartsWith("untelegraphed"))));
        }
        if (CombatTells.BossLeadsMs.Count == 0) {
            run.Problem(PlaytestSeverity.Bug, "the wave 10 boss never landed a heavy blow, so its telegraph lead was not measured");
        } else if (CombatTells.MinBossLeadMs < 600f) {
            run.Problem(PlaytestSeverity.Bug, "the boss's heavy blow was marked only " + CombatTells.MinBossLeadMs.ToString("0") + " ms ahead (need 600)");
        }
        if (CombatTells.Pulses == 0) {
            run.Problem(PlaytestSeverity.Bug, "Slice's circle never pulsed when a bot reached her");
        }
        if (padOverlap) {
            run.Problem(PlaytestSeverity.Bug, "the edge tint overlapped " + padWhat);
        }
    }

    // On a phone the edge tint must stay clear of the stick and the Swing pad.
    static void TellsEdgeCheck(ref bool overlap, ref string what) {
        if (!animTouch || !CombatTells.EdgeVisible) {
            return;
        }
        Rect r = CombatTells.EdgeRect;
        foreach (string pad in new[] { "Stick", "Swing" }) {
            Vector2 at;
            if (!TouchControls.Spot(pad, out at)) {
                continue;
            }
            float reach = pad == "Stick" ? TouchControls.StickReach : Screen.height * 0.09f;
            float cx = Mathf.Clamp(at.x, r.xMin, r.xMax), cy = Mathf.Clamp(at.y, r.yMin, r.yMax);
            if ((new Vector2(cx, cy) - at).magnitude < reach) {
                overlap = true;
                what = "the " + pad + " pad at " + r;
            }
        }
    }

    static void TellsLog(string when) {
        run.Event("tells-" + when, "wind-ups " + CombatTells.Windups + ", lead " + CombatTells.MinLeadMs.ToString("0") + ".." + CombatTells.MaxLeadMs.ToString("0")
            + " ms, untelegraphed " + CombatTells.Untelegraphed + ", boss leads " + CombatTells.BossLeadsMs.Count + " (min " + CombatTells.MinBossLeadMs.ToString("0")
            + " ms), circle pulses " + CombatTells.Pulses + ", reaches " + CombatTells.Reaches + ", edge tints " + CombatTells.EdgeShows);
    }
}
