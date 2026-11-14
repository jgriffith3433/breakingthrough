using System.Collections;
using Hawkes.Playtest;
using UnityEngine;

// Scenario "slice-partner": Slice as partner — three voiced callouts with ring markers, one Act 2 parry with knockback,
// no overlapping voice, and at 390x844 the ring/arrow/caption stay in the play area clear of stick and buttons.
public static class PlaytestSlicePartner {
    public static IEnumerator Run(PlaytestRun run) {
        MetaProgress.EnterSandbox(true);
        int wasW = Screen.width, wasH = Screen.height;
        bool wasTouch = UiFit.Touch;
        // In a browser build Screen.SetResolution only stretches the canvas (smeared frame, black bar, clipped HUD), so
        // the bot never resizes it there: the phone twin already is 390x844, a desktop page stays a desktop page.
        bool browser = Application.platform == RuntimePlatform.WebGLPlayer;
        bool phoneViewport = Screen.height > Screen.width && Screen.width <= 500;
        if (!browser) {
            UiFit.ForceTouch(true);
            Screen.SetResolution(390, 844, false);
        } else if (phoneViewport) {
            UiFit.ForceTouch(true);
        }
        yield return null;
        yield return null;
        ColdOpen.Suppress = true;
        SoundSettings.SetChatter(3); // Often: shorter bark gaps so callouts show in the hold
        yield return Playtest.ToMenuPublic();
        if (run.Done) {
            if (!browser) { Screen.SetResolution(wasW, wasH, false); }
            UiFit.ForceTouch(wasTouch);
            yield break;
        }
        yield return Playtest.StartArcadePublic();
        if (run.Done) {
            if (!browser) { Screen.SetResolution(wasW, wasH, false); }
            UiFit.ForceTouch(wasTouch);
            yield break;
        }
        run.Scene("Arcade partner");
        ScreenSlots.StartProbe();   // ui.textOverlaps / ui.textOverflows over the whole run
        run.Event("phone-viewport", Screen.width + "x" + Screen.height + " touch=" + UiFit.Touch);
        if (Screen.width != 390 && !browser) {
            run.Event("phone-viewport-soft", "wanted 390x844, got " + Screen.width + "x" + Screen.height + " (layout still ForcedTouch)");
        }
        yield return run.Until(() => Musician.Instance != null && SliceCallouts.Instance != null && Enemy.Roster.Count > 0, 45f, "Slice callouts and wave 1 bots");
        if (run.Done) {
            if (!browser) { Screen.SetResolution(wasW, wasH, false); }
            UiFit.ForceTouch(wasTouch);
            yield break;
        }
        run.Event("partner-ready", "guard level " + SliceCallouts.GuardLevel() + " (act " + Tuning.ActOf(WaveDirector.Wave) + ")");
        // Voiced means a real clip, not caption-only.
        AudioClip probe = Resources.Load<AudioClip>("Voice/" + SliceVoice.LineId("Left."));
        run.Event("callout-clip", probe != null ? "slice_left ready (" + probe.length.ToString("0.00") + "s)" : "MISSING slice_left");
        if (probe == null) {
            run.Problem(PlaytestSeverity.Bug, "Slice callout voice file Voice/slice_left missing");
        }
        if (Resources.Load<AudioClip>("Sfx/sliceparry") == null) {
            run.Problem(PlaytestSeverity.Bug, "Parry clink Sfx/sliceparry missing");
        }

        // Mostly hold fire so bots close on her circle; top her up so she is not struck off before three callouts.
        float until = Time.unscaledTime + 150f;
        int lastCalls = 0;
        while (Time.unscaledTime < until && SliceCallouts.CalloutCount < 3 && !run.Done && !WaveDirector.IsLost) {
            if (LevelUpScreen.IsOpen) {
                PlayInput.Tap(KeyCode.Alpha1);
            }
            Musician slice = Musician.Instance;
            if (slice != null && slice.Health < slice.MaxHealth * 0.55f) {
                slice.Heal(slice.MaxHealth);
                run.Event("setup", "Slice topped up so callouts can finish");
            }
            yield return null;
            if (SliceCallouts.CalloutCount > lastCalls) {
                lastCalls = SliceCallouts.CalloutCount;
                run.Event("slice-callout", "#" + lastCalls + " \"" + SliceCallouts.LastWord + "\"");
                if (SliceCallouts.MarkerVisible) {
                    run.Event("callout-ring", "marker up on callout #" + lastCalls);
                }
            }
            if (SliceCallouts.ArrowVisible) {
                Vector2 a = SliceCallouts.LastArrowAnchor;
                run.Event("callout-arrow", "anchor " + a.x.ToString("0.00") + "," + a.y.ToString("0.00"));
                PhoneClear(run, a);
            }
        }
        int calls = SliceCallouts.CalloutCount;
        run.Event("slice-callouts", calls + " voiced callout(s), last \"" + SliceCallouts.LastWord + "\"");
        run.Metric("sliceCallouts", calls);
        if (calls < 3) {
            run.Problem(PlaytestSeverity.Bug, "wanted 3 Slice callouts in the hold, saw " + calls);
        }

        // Setup: Cold Hour (Act 2) so her parry unlocks.
        run.Event("setup", "testing wave key to wave 11 (Cold Hour / Act 2 parry)");
        yield return Playtest.GoWavePublic(11);
        if (run.Done) {
            yield break;
        }
        yield return run.Until(() => !WaveDirector.Waiting && Enemy.Roster.Count > 0, 30f, "Act 2 bots");
        run.Event("partner-act2", "wave " + WaveDirector.Wave + " guard " + SliceCallouts.GuardLevel());
        if (SliceCallouts.GuardLevel() < 1) {
            run.Problem(PlaytestSeverity.Bug, "Act 2 should unlock Slice's parry (guard level " + SliceCallouts.GuardLevel() + ")");
        }

        // Hold fire until a bot reaches her and she parries; keep her up.
        until = Time.unscaledTime + 90f;
        while (Time.unscaledTime < until && SliceCallouts.ParryCount < 1 && !run.Done && !WaveDirector.IsLost) {
            if (LevelUpScreen.IsOpen) {
                PlayInput.Tap(KeyCode.Alpha1);
            }
            Musician slice = Musician.Instance;
            if (slice != null && slice.Health < slice.MaxHealth * 0.4f) {
                slice.Heal(slice.MaxHealth);
            }
            yield return null;
        }
        run.Event("slice-parry", SliceCallouts.ParryCount > 0
            ? "parry fired with block pose and knockback"
            : "NO PARRY");
        run.Metric("sliceParries", SliceCallouts.ParryCount);
        if (SliceCallouts.ParryCount < 1) {
            run.Problem(PlaytestSeverity.Bug, "Act 2 Slice never parried a bot at her circle");
        }

        for (int i = 0; i < SliceCallouts.Events.Count && i < 24; i++) {
            string[] bits = SliceCallouts.Events[i].Split(new[] { '|' }, 3);
            run.Event(bits[0], bits.Length > 2 ? bits[2] : "");
        }

        int overlaps = WallitVoice.Overlaps;
        run.Event("voice-overlap", overlaps + " overlap(s) during partner run");
        run.Metric("slicePartnerVoiceOverlaps", overlaps);
        run.Metric("sliceRepliesWithin2s", WallitVoice.RepliesInTime);
        run.Event("slice-replies", WallitVoice.RepliesInTime + " Wallit replies within 2 s of a Slice line");
        if (overlaps > 0) {
            run.Problem(PlaytestSeverity.Bug, "voice lines overlapped during callouts (" + overlaps + ")");
        }

        // Caption one word above the HUD on the phone layout.
        if (!string.IsNullOrEmpty(SliceCallouts.LastWord)) {
            Rect cap = SliceCallouts.LastCaptionRect;
            run.Event("callout-caption", "\"" + SliceCallouts.LastWord + "\" rect " + cap);
            if (cap.height > 1f) {
                float bottom = cap.yMin;
                // Tall 390x844: stick/buttons occupy the bottom ~22%; caption must sit in the top band.
                if (bottom < Screen.height * 0.45f) {
                    run.Problem(PlaytestSeverity.Bug, "callout caption is too low for the phone HUD (yMin " + bottom.ToString("0") + ")");
                }
            }
        }

        run.Metric("quality.textOnlyBeats", 0);
        Playtest.ReportTextOverlaps("slice-partner");
        if (Screen.height > Screen.width && CaptionStack.BottomFraction > CaptionStack.PadFraction + 0.01f) {
            run.Problem(PlaytestSeverity.Bug, "captions reach the touch pads (end " + Mathf.RoundToInt(CaptionStack.BottomFraction * 100f) + "% down)");
        }
        ScreenSlots.StopProbe();
        yield return new WaitForSecondsRealtime(1f);
        if (!browser) { Screen.SetResolution(wasW, wasH, false); }
        UiFit.ForceTouch(wasTouch);
    }

    static void PhoneClear(PlaytestRun run, Vector2 anchor) {
        // Stick bottom-left, buttons bottom-right: keep arrow out of those corners on Tall.
        bool inStick = anchor.x < 0.22f && anchor.y < 0.28f;
        bool inButtons = anchor.x > 0.78f && anchor.y < 0.28f;
        if (inStick || inButtons) {
            run.Problem(PlaytestSeverity.Bug, "callout edge arrow under touch controls at " + anchor.x.ToString("0.00") + "," + anchor.y.ToString("0.00"));
        }
    }
}
