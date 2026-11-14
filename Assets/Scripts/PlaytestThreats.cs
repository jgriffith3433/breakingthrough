using System.Collections;
using Hawkes.Playtest;
using UnityEngine;

// The "threats" scenario (BotThreat.cs): every bot has a ground ring in its kind's colour, brighter in its wind-up, and a bot outside
// the view that is about to strike gets an arrow at the screen edge. Arcade waves 1 to 3 played by real input, with Wallit walking
// away from Slice for a spell (stick or WASD) so bots strike her off screen, then the testing wave key to wave 8 for the bigger kinds
// and 40 s more. Phone (mobile, 390x844): fingers only. Metrics threats.offscreenMarked, threats.unmarkedHits, threats.ringMinPx,
// threats.arrowPadOverlaps, threats.arrowOutsideSafe.
public static partial class Playtest {
    static IEnumerator ThreatsRun(PlaytestOptions options) {
        animTouch = Screen.height > Screen.width || Screen.height < 500;
        UiFit.ForceTouch(animTouch);
        if (animTouch) {
            MobileQuality.Apply();
            run.Event("mobile-quality", MobileQuality.Describe());
        }
        run.Event("threats-setup", (animTouch ? "touch 390x844 phone tier, fingers only" : "keys and mouse") + ", arrows for bots within " + BotThreat.StrikeSeconds.ToString("0") + " s of a blow, at most " + BotThreat.MaxArrows);
        yield return run.Until(() => WaveDirector.Instance != null && Enemy.Roster.Count > 0, 40f, "wave 1's bots");
        if (run.Done) {
            yield break;
        }
        var player = Object.FindAnyObjectByType<CharacterMovement>();

        run.Scene("Threats: wave 1, a ring under every bot");
        yield return AnimFight(player, 25f, () => BotThreat.WindFrames >= 10);
        ThreatsNote("rings");
        if (run.Done) {
            yield break;
        }

        run.Scene("Threats: Wallit walks away, bots strike Slice off screen");
        run.Event("setup", "Wallit walks away from the circle (real input) so bots strike Slice off screen; her health is topped up");
        yield return TellsWalkAway(player, 30f, () => BotThreat.OffscreenMarked >= 2);
        ThreatsNote("walk-away");
        if (run.Done) {
            yield break;
        }

        run.Scene("Threats: waves 2 and 3");
        float start = Time.realtimeSinceStartup;
        while (!run.Done && !WaveDirector.IsLost && WaveDirector.Wave <= 3 && Time.realtimeSinceStartup - start < 120f) {
            yield return AnimFight(player, 3f, null);
            if (Time.realtimeSinceStartup - start > 40f && Time.realtimeSinceStartup - start < 56f) {
                yield return TellsWalkAway(player, 14f, null);
            }
        }
        if (run.Done) {
            yield break;
        }
        run.Scene("Threats: wave 8, the bigger kinds");
        run.Event("setup", "testing wave key to wave 8 (Plated, Mega and Spider Bots)");
        yield return GoWave(8);
        if (run.Done || WaveDirector.IsLost) {
            yield break;
        }
        start = Time.realtimeSinceStartup;
        while (!run.Done && !WaveDirector.IsLost && Time.realtimeSinceStartup - start < 40f) {
            yield return AnimFight(player, 3f, null);
        }
        TouchControls.LiftBotFingers();
        animStickDown = false;
        PlayInput.Release();
        if (run.Done) {
            yield break;
        }
        ThreatsNote("wave-8");

        run.Metric("threats.offscreenMarked", BotThreat.OffscreenMarked);
        run.Metric("threats.unmarkedHits", BotThreat.UnmarkedHits);
        run.Metric("threats.hits", BotThreat.Hits);
        run.Metric("threats.peakRings", BotThreat.PeakRings);
        run.Metric("threats.peakArrows", BotThreat.PeakArrows);
        run.Metric("threats.ringMinPx", Mathf.Round(BotThreat.RingMinPx == float.MaxValue ? 0f : BotThreat.RingMinPx));
        run.Metric("threats.ringAlphaIdle", Mathf.Round(BotThreat.RingAlphaIdle * 100f) / 100f);
        run.Metric("threats.ringAlphaWind", Mathf.Round(BotThreat.RingAlphaWind * 100f) / 100f);
        run.Metric("threats.arrowFrames", BotThreat.ArrowFrames);
        run.Metric("threats.arrowOutsideSafe", BotThreat.ArrowOutsideSafe);
        run.Metric("threats.arrowPadOverlaps", BotThreat.ArrowPadOverlaps);
        foreach (string line in BotThreat.Log) {
            run.Event("threat-log", line);
        }
        if (BotThreat.PeakRings == 0) {
            run.Problem(PlaytestSeverity.Bug, "no bot had a ground ring");
        }
        if (BotThreat.WindFrames == 0) {
            run.Problem(PlaytestSeverity.Bug, "no bot was seen winding up with its ring brightened");
        }
        if (BotThreat.PeakArrows > BotThreat.MaxArrows) {
            run.Problem(PlaytestSeverity.Bug, BotThreat.PeakArrows + " arrows at once (cap " + BotThreat.MaxArrows + ")");
        }
        if (animTouch && BotThreat.RingMinPx < 24f && BotThreat.RingMinPx > 0f) {
            run.Problem(PlaytestSeverity.Bug, "a ground ring was only " + BotThreat.RingMinPx.ToString("0") + " px across at 390x844 (want 24+)");
        }
        if (BotThreat.OffscreenMarked == 0) {
            run.Problem(PlaytestSeverity.Warning, "no off-screen bot was marked with an edge arrow in this run");
        }
        if (BotThreat.ArrowOutsideSafe > 0) {
            run.Problem(PlaytestSeverity.Bug, "an edge arrow left the safe area on " + BotThreat.ArrowOutsideSafe + " frames");
        }
        if (BotThreat.ArrowPadOverlaps > 0) {
            run.Problem(PlaytestSeverity.Bug, "an edge arrow sat on the stick or a pad on " + BotThreat.ArrowPadOverlaps + " frames at 390x844");
        }
        if (BotThreat.UnmarkedHits > 0) {
            run.Problem(PlaytestSeverity.Bug, BotThreat.UnmarkedHits + " hits came from a bot that was never on screen or marked (threats.unmarkedHits)");
        }
        run.Finish(PlaytestOutcome.Done);
    }

    static void ThreatsNote(string where) {
        run.Event("threats", where + ": rings " + BotThreat.PeakRings + ", winding frames " + BotThreat.WindFrames + ", marked off screen " + BotThreat.OffscreenMarked
            + ", arrows up " + BotThreat.PeakArrows + ", hits " + BotThreat.Hits + ", unmarked " + BotThreat.UnmarkedHits);
    }
}
