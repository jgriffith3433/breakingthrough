using System.Collections;
using Hawkes.Playtest;
using UnityEngine;

// The "hit-fx" scenario: the Department's hit effects (DeptFx.cs). Plays Arcade by keys, or by fingers only on a phone
// viewport (390x844), until a swing trail, an impact burst with paper scraps, a STRUCK OFF stamp before a bot breaks, a home
// run (speed lines behind the bot, the shockwave ring, the flash where it lands) and a coin pickup have all been seen, then
// drops a crowd of 10+ bots (wave 10, setup: the wave key) and holds the frame rate against the phone budget with the effects on.
public static partial class Playtest {
    const float HitFxPhoneFps = 30f;

    static IEnumerator HitFxRun(PlaytestOptions options) {
        animTouch = Screen.height > Screen.width || Screen.height < 500;
        UiFit.ForceTouch(animTouch);
        if (animTouch) {
            MobileQuality.Apply();
            run.Event("mobile-quality", MobileQuality.Describe());
        }
        run.Event("hit-fx-setup", (animTouch ? "touch 390x844 phone tier" : "keys and mouse") + ", particle tier x" + DeptFx.Tier.ToString("0.0")
            + ", atlas " + DeptFx.Atlas().width + "x" + DeptFx.Atlas().height);
        yield return run.Until(() => WaveDirector.Instance != null && Enemy.Roster.Count > 0 && DeptFx.Ready, 30f, "wave 1's bots and the effects");
        if (run.Done) {
            yield break;
        }
        var player = Object.FindAnyObjectByType<CharacterMovement>();

        run.Scene("Hit VFX: swings, hits and deaths");
        yield return AnimFight(player, 30f, () => DeptFx.Stamps >= 2 && BatEquipment.TrailSwings >= 3 && DeptFx.Bursts >= 4);
        if (run.Done) {
            yield break;
        }
        HitFxTell("swings-and-hits");

        run.Scene("Hit VFX: a home run");
        int runsBefore = BatEquipment.HomeRuns;
        for (int attempt = 0; attempt < 8 && !run.Done && BatEquipment.HomeRuns == runsBefore && !WaveDirector.IsLost; attempt++) {
            yield return AnimFight(player, 4f, () => HitFxBotInReach(player));
            if (animTouch) {
                yield return HitFxTouchCharge(player);
            } else {
                yield return HitFxKeyCharge(player);
            }
        }
        if (run.Done) {
            yield break;
        }
        yield return AnimFight(player, 3f, null);   // let it fly and land
        HitFxTell("home-run");

        run.Scene("Hit VFX: a crowd");
        run.Event("setup", "testing wave key to wave 10 for a crowd of 10+ bots");
        yield return GoWave(10);
        float start = Time.realtimeSinceStartup;
        float sum = 0f;
        int frames = 0;
        float worst = 999f;
        int crowd = 0;
        while (!run.Done && !WaveDirector.IsLost && Time.realtimeSinceStartup - start < 25f) {
            yield return AnimFight(player, 0.25f, null);
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f && Enemy.Roster.Count >= 10) {
                float fps = 1f / dt;
                sum += fps;
                frames++;
                worst = Mathf.Min(worst, fps);
                crowd = Mathf.Max(crowd, Enemy.Roster.Count);
            }
        }
        TouchControls.LiftBotFingers();
        PlayInput.Release();
        if (run.Done) {
            yield break;
        }
        float avg = frames > 0 ? sum / frames : 0f;
        run.Event("hit-fx-fps", crowd + " bots on screen with the effects on: avg " + avg.ToString("0.0") + " fps, worst " + (worst < 999f ? worst.ToString("0.0") : "n/a")
            + " fps, peak " + DeptFx.Peak + " live particles");
        run.Metric("hitFxCrowdBots", crowd);
        run.Metric("hitFxCrowdFpsAvg", Mathf.Round(avg * 10f) / 10f);
        run.Metric("hitFxPeakParticles", DeptFx.Peak);
        run.Metric("hitFxPaper", DeptFx.PaperEmitted);
        run.Metric("hitFxStamps", DeptFx.Stamps);
        run.Metric("hitFxLandings", DeptFx.Landings);
        if (frames == 0) {
            run.Problem(PlaytestSeverity.Warning, "the crowd never reached 10 bots on screen, so the frame rate with effects was not measured");
        } else if (animTouch && avg < HitFxPhoneFps) {
            run.Problem(PlaytestSeverity.Bug, "phone tier held " + avg.ToString("0.0") + " fps in a wave of " + crowd + " bots with the effects on (need " + HitFxPhoneFps.ToString("0") + ")");
        } else if (avg < HitFxPhoneFps) {
            run.Problem(PlaytestSeverity.Warning, "desktop averaged " + avg.ToString("0.0") + " fps in a wave of " + crowd + " bots with the effects on");
        }

        if (BatEquipment.TrailSwings == 0) {
            run.Problem(PlaytestSeverity.Bug, "no swing showed a trail on the bat");
        }
        if (DeptFx.Bursts == 0 || DeptFx.PaperEmitted == 0 || DeptFx.InkEmitted == 0) {
            run.Problem(PlaytestSeverity.Bug, "bat hits showed no impact burst with torn paper and ink (bursts " + DeptFx.Bursts + ", paper " + DeptFx.PaperEmitted + ")");
        }
        if (DeptFx.Stamps == 0) {
            run.Problem(PlaytestSeverity.Bug, "no killed bot showed the STRUCK OFF stamp");
        } else if (DeptFx.StampsBeforeBreak < DeptFx.Stamps - 1) {
            run.Problem(PlaytestSeverity.Bug, "a bot broke apart before its STRUCK OFF stamp had shown (" + DeptFx.StampsBeforeBreak + " broke of " + DeptFx.Stamps + " stamped)");
        }
        if (BatEquipment.HomeRuns == runsBefore) {
            run.Problem(PlaytestSeverity.Bug, "no home run went off");
        } else {
            if (DeptFx.SpeedLineBursts == 0) {
                run.Problem(PlaytestSeverity.Bug, "the home run bot had no speed-line trail");
            }
            if (DeptFx.Landings == 0) {
                run.Problem(PlaytestSeverity.Bug, "no flash where the home run bot landed");
            }
        }
        if (Pickups.BoltsCollected > 0 && DeptFx.CoinGlints == 0) {
            run.Problem(PlaytestSeverity.Bug, "bolts were picked up with no coin glint");
        }
    }

    static bool HitFxBotInReach(CharacterMovement player) {
        Enemy near = GuardTarget(player, null);
        if (near == null || player == null) {
            return false;
        }
        Vector3 flat = near.transform.position - player.transform.position;
        flat.y = 0f;
        return flat.magnitude - near.BodyScale * 0.3f <= BatEquipment.Reach * 0.9f;
    }

    // Keys: hold the left button long enough to charge, release.
    static IEnumerator HitFxKeyCharge(CharacterMovement player) {
        if (!HitFxBotInReach(player)) {
            yield break;
        }
        PlayInput.Hold(KeyCode.Mouse0, true);
        float t = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t < 1.5f && !run.Done) {
            SampleFps();
            yield return null;
        }
        PlayInput.Hold(KeyCode.Mouse0, false);
        yield return null;
    }

    // Fingers: stop the stick, press and hold Swing for the charge, lift.
    static IEnumerator HitFxTouchCharge(CharacterMovement player) {
        if (!HitFxBotInReach(player)) {
            yield break;
        }
        TouchControls.LiftBotFingers();
        animStickDown = false;
        if (!TouchControls.Spot("Swing", out Vector2 swingAt)) {
            yield break;
        }
        TouchControls.BotFinger(1, swingAt, true);
        float t = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t < 1.5f && !run.Done) {
            SampleFps();
            yield return null;
        }
        TouchControls.BotFinger(1, Vector2.zero, false);
        yield return null;
    }

    static void HitFxTell(string what) {
        run.Event("hit-fx-" + what, "swing trails " + BatEquipment.TrailSwings + ", impact bursts " + DeptFx.Bursts + " (paper " + DeptFx.PaperEmitted + ", ink " + DeptFx.InkEmitted
            + "), stamps " + DeptFx.Stamps + " (" + DeptFx.StampsBeforeBreak + " then broke), leg scatters " + DeptFx.LegScatters + ", home runs " + BatEquipment.HomeRuns
            + ", speed-line frames " + DeptFx.SpeedLineBursts + ", landings " + DeptFx.Landings + ", circle ripples " + DeptFx.Ripples
            + ", coin glints " + DeptFx.CoinGlints + " arcs " + DeptFx.CoinArcs);
    }
}
