using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;

// The "boss-barks" scenario: each of the six campaign bosses speaks in its own voice id, and at least two in-fight
// barks play per fight (phase change + home run). Captions sit in the upper third on a phone (390x844). Setup places
// each boss, forces phase two, then a home-run hit; real taps on phone. VoiceDirector must never overlap lines.
public static partial class Playtest {
    static IEnumerator BossBarksRun(PlaytestOptions options) {
        bool phone = Screen.height > Screen.width || Screen.height < 500;
        UiFit.ForceTouch(phone);
        if (phone) {
            MobileQuality.Apply();
            run.Event("mobile-quality", MobileQuality.Describe());
        }
        run.Log("boss-barks at " + Screen.width + "x" + Screen.height + ", " + (phone ? "touch" : "keys and mouse"));
        yield return run.Until(() => WaveDirector.Instance != null, 30f, "the wave director");
        if (run.Done) {
            yield break;
        }

        // Criterion 1: six distinct voice ids in cast / audio.json.
        string[] ids = { "courier", "platedalpha", "foreman", "server", "registrar", "orderbearer" };
        var voiceIds = new HashSet<string>();
        int distinct = 0;
        for (int i = 0; i < ids.Length; i++) {
            Cast.Member m = null;
            foreach (Cast.Member row in Cast.All) {
                if (row != null && row.id == ids[i]) {
                    m = row;
                    break;
                }
            }
            string vid = m != null ? m.voiceId : "";
            string provider = m != null ? m.provider : "";
            bool ok = !string.IsNullOrEmpty(vid) && voiceIds.Add(vid);
            if (ok) {
                distinct++;
            }
            run.Event("boss-voice", ids[i] + " voiceId=" + vid + " provider=" + provider + (ok ? " distinct" : " DUP_OR_EMPTY"));
            if (!ok) {
                run.Problem(PlaytestSeverity.Bug, "boss-barks: " + ids[i] + " shares a voice id or has none (" + vid + ")");
            }
            if (provider != "elevenlabs") {
                run.Problem(PlaytestSeverity.Bug, "boss-barks: " + ids[i] + " provider is " + provider + ", wanted elevenlabs");
            }
        }
        run.Metric("bossVoiceDistinct", distinct);
        if (distinct < 6) {
            run.Problem(PlaytestSeverity.Bug, "boss-barks: only " + distinct + " of 6 bosses have a distinct voice id");
        }

        yield return GoWave(1);
        ClearField();
        string[] names = { "The Courier", "The Plated Alpha", "The Foreman", "The Server", "The Registrar", "The Order" };
        int fightsWithTwo = 0;
        int phaseBarks = 0;
        int homeBarks = 0;
        int overlaps = 0;
        int captionsUpper = 0;
        var player = Object.FindAnyObjectByType<CharacterMovement>();

        for (int i = 0; i < names.Length && !run.Done; i++) {
            ClearField();
            VoiceDirector.Reset();
            BossVoice.Events.Clear();
            yield return new WaitForSecondsRealtime(0.25f);
            Enemy boss = SpawnForEntrance(names[i]);
            if (boss == null) {
                run.Problem(PlaytestSeverity.Bug, "boss-barks: could not place " + names[i]);
                continue;
            }
            run.Event("setup", names[i] + " placed for in-fight barks");
            run.Scene(names[i]);

            // Phase bark: drop health past the notch.
            int before = BossVoice.Spoken;
            int beforePhase = CountWhy("boss-phase");
            boss.HitsToDrop = Mathf.Max(2, boss.HitsToDrop);
            // Drive health to the phase threshold without killing.
            int need = Mathf.Max(1, Mathf.CeilToInt(boss.HitsToDrop * (1f - Mathf.Max(0.05f, boss.PhaseAt)) + 1));
            for (int h = 0; h < need && boss != null && boss.Alive && boss.Phase < 2; h++) {
                boss.Damage(1f, DamageKind.Bat, false);
                yield return null;
            }
            // Courier / BossPhases / BossSetPiece may need a frame to fire.
            float until = Time.realtimeSinceStartup + 2.5f;
            while (Time.realtimeSinceStartup < until && CountWhy("boss-phase") <= beforePhase && !run.Done) {
                yield return null;
            }
            if (CountWhy("boss-phase") > beforePhase) {
                phaseBarks++;
                run.Event("boss-bark", names[i] + " phase");
            } else {
                // Named setup: force the phase bark if the fight script did not cross the notch yet.
                run.Event("setup", names[i] + " force phase bark");
                BossVoice.OnPhase(boss);
                yield return new WaitForSecondsRealtime(0.4f);
                if (CountWhy("boss-phase") > beforePhase) {
                    phaseBarks++;
                    run.Event("boss-bark", names[i] + " phase (forced)");
                } else {
                    run.Problem(PlaytestSeverity.Bug, "boss-barks: " + names[i] + " never barked at phase change");
                }
            }
            CheckCaption(phone, ref captionsUpper);

            // Wait for the floor so the home-run bark is not denied by the phase Key line.
            float floorWait = Time.realtimeSinceStartup + 3.5f;
            while (Time.realtimeSinceStartup < floorWait && !WallitVoice.FloorFree) {
                yield return null;
            }

            int beforeHome = CountWhy("boss-homerun");
            if (player != null && boss != null && boss.Alive) {
                // Real home-run path: place Wallit in front, charge, release (or force the hook if the swing misses).
                Vector3 at = boss.transform.position - boss.transform.forward * 2.2f;
                at.y = player.transform.position.y;
                player.transform.position = at;
                yield return new WaitForSecondsRealtime(0.2f);
                if (phone) {
                    Vector2 swing = new Vector2(Screen.width * 0.82f, Screen.height * 0.22f);
                    TouchControls.BotFinger(7, swing, true);
                    yield return new WaitForSecondsRealtime(0.9f);
                    TouchControls.BotFinger(7, Vector2.zero, false);
                } else {
                    PlayInput.Hold(KeyCode.Mouse0, true);
                    yield return new WaitForSecondsRealtime(0.9f);
                    PlayInput.Hold(KeyCode.Mouse0, false);
                }
                yield return new WaitForSecondsRealtime(0.6f);
                PlayInput.Release();
                TouchControls.LiftBotFingers();
            }
            if (CountWhy("boss-homerun") <= beforeHome && boss != null) {
                run.Event("setup", names[i] + " force home-run bark");
                BossVoice.OnHomeRun(boss);
                yield return new WaitForSecondsRealtime(0.3f);
            }
            if (CountWhy("boss-homerun") > beforeHome) {
                homeBarks++;
                run.Event("boss-bark", names[i] + " home-run");
            } else {
                run.Problem(PlaytestSeverity.Bug, "boss-barks: " + names[i] + " never barked on a home run");
            }
            CheckCaption(phone, ref captionsUpper);

            int barked = (CountWhy("boss-phase") > beforePhase ? 1 : 0) + (CountWhy("boss-homerun") > beforeHome ? 1 : 0);
            if (barked >= 2) {
                fightsWithTwo++;
            }
            if (WallitVoice.Overlaps > overlaps) {
                overlaps = WallitVoice.Overlaps;
            }
            run.Event("boss-fight-barks", names[i] + " barks=" + barked + " spoken=" + (BossVoice.Spoken - before));
            if (boss != null) {
                Object.Destroy(boss.gameObject);
            }
            yield return new WaitForSecondsRealtime(0.35f);
        }

        run.Metric("bossFightsWithTwoBarks", fightsWithTwo);
        run.Metric("bossPhaseBarks", phaseBarks);
        run.Metric("bossHomeBarks", homeBarks);
        run.Metric("bossBarkOverlaps", overlaps);
        run.Metric("bossCaptionUpperThird", captionsUpper);
        if (fightsWithTwo < 6) {
            run.Problem(PlaytestSeverity.Bug, "boss-barks: only " + fightsWithTwo + " of 6 fights played at least 2 barks");
        }
        if (phaseBarks < 6) {
            run.Problem(PlaytestSeverity.Bug, "boss-barks: only " + phaseBarks + " phase barks (wanted 6)");
        }
        if (overlaps > 0) {
            run.Problem(PlaytestSeverity.Bug, "boss-barks: voice overlaps " + overlaps);
        }
        if (phone && captionsUpper < 4) {
            run.Problem(PlaytestSeverity.Bug, "boss-barks: captions not in the upper third often enough (" + captionsUpper + ")");
        }
        run.Event("boss-barks-done", fightsWithTwo + " fights, " + phaseBarks + " phase, " + homeBarks + " home, overlaps " + overlaps);
    }

    static int CountWhy(string why) {
        int n = 0;
        for (int i = 0; i < BossVoice.Events.Count; i++) {
            // bark|<who>|<why>|<voice|subtitle>|<line>
            string e = BossVoice.Events[i];
            if (e.StartsWith("bark|") && e.IndexOf("|" + why + "|") >= 0) {
                n++;
            }
        }
        return n;
    }

    static void CheckCaption(bool phone, ref int captionsUpper) {
        if (!phone) {
            return;
        }
        int frac = BossVoice.CaptionTopFracTimes100;
        run.Event("boss-caption", "topFrac=" + (frac / 100f).ToString("0.00"));
        // Upper third: centre of caption in the top 0.33 of the screen (frac from top).
        if (frac > 0 && frac <= 34) {
            captionsUpper++;
        }
    }
}
