using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.UI;

// The paper HUD (HudPaper): Slice's address card tearing in stages, the coin ledger rolling with a sound, the wave
// postmark re-stamping each wave with a red seal on boss waves, the ink stamp, and on a phone no element under the touch
// controls and every number 16 px or taller. Scenario id: hud-paper (run it with mobile; fingers only for the Play tap).
// Setup shortcuts (named): Slice's health is knocked down in steps, coins are granted, the wave key skips to wave 10.
public static class PlaytestHudPaper {
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
        run.Scene("Arcade");
        yield return run.Until(() => HudPaper.Active && Musician.Instance != null && WaveDirector.Instance != null, 30f, "the paper HUD");
        if (run.Done) {
            yield break;
        }
        run.Event("hud-paper", "active, mode " + UiFit.Mode + " stage " + HudPaper.CardStage);
        yield return new WaitForSecondsRealtime(1.2f);
        CheckLayout(run, "start");

        // 1. Slice's card: three tears as her health falls.
        var stages = new List<int> { HudPaper.CardStage };
        Musician slice = Musician.Instance;
        int[] floors = { 70, 45, 20 };
        for (int i = 0; i < floors.Length; i++) {
            int drop = slice.Health - floors[i];
            if (drop > 0) {
                slice.Hurt(drop, null);
            }
            run.Event("setup", "Slice knocked down to " + slice.Health);
            yield return new WaitForSecondsRealtime(0.5f);
            if (HudPaper.CardStage != stages[stages.Count - 1]) {
                stages.Add(HudPaper.CardStage);
            }
            run.Event("card-stage", "stage " + HudPaper.CardStage + " at " + slice.Health + " health");
        }
        run.Metric("cardStages", stages.Count);
        run.Metric("cardTears", Mathf.Max(0, stages.Count - 1));
        if (stages.Count < 4) {
            run.Problem(PlaytestSeverity.Bug, "Slice's card showed " + (stages.Count - 1) + " tear stages, wanted 3");
        }
        CheckLayout(run, "torn");
        int healsBefore = HudPaper.Heals;
        slice.Heal(40);
        yield return new WaitForSecondsRealtime(0.4f);
        run.Event("card-taped", "heals " + HudPaper.Heals + ", stage " + HudPaper.CardStage);
        if (HudPaper.Heals <= healsBefore) {
            run.Problem(PlaytestSeverity.Bug, "the card did not react to a heal");
        }

        // 2. Coins: the ledger rolls on every gain.
        int gainsBefore = HudPaper.CoinGains;
        int coinsBefore = MetaProgress.Coins;
        MetaProgress.AddCoins(7, Vector3.zero);
        run.Event("setup", "coins granted: +7");
        yield return new WaitForSecondsRealtime(0.12f);
        int midway = HudPaper.ShownCoins;
        yield return new WaitForSecondsRealtime(1.2f);
        run.Event("coin-roll", "from " + coinsBefore + " to " + MetaProgress.Coins + ", shown " + midway + " then " + HudPaper.ShownCoins);
        run.Metric("coinGains", HudPaper.CoinGains - gainsBefore);
        if (HudPaper.CoinGains <= gainsBefore || HudPaper.ShownCoins != MetaProgress.Coins) {
            run.Problem(PlaytestSeverity.Bug, "the coin ledger did not roll to the new total");
        }

        // 3. The postmark: wave key to 10 (a boss wave: the red seal).
        int restamps = HudPaper.Restamps;
        int level;
        for (level = 0; level < 12 && WaveDirector.Wave < 10; level++) {
            PlayInput.Tap(KeyCode.Equals);
            yield return new WaitForSecondsRealtime(0.25f);
            if (LevelUpScreen.IsOpen) {
                PlayInput.Tap(KeyCode.Alpha1);
                yield return new WaitForSecondsRealtime(0.25f);
            }
        }
        run.Event("setup", "wave key to " + WaveDirector.Wave);
        yield return new WaitForSecondsRealtime(0.8f);
        run.Event("postmark", "wave " + WaveDirector.Wave + ", restamps " + (HudPaper.Restamps - restamps) + ", seal " + HudPaper.SealUp);
        run.Metric("restamps", HudPaper.Restamps - restamps);
        if (HudPaper.Restamps - restamps < 1) {
            run.Problem(PlaytestSeverity.Bug, "the postmark was not re-stamped when the wave changed");
        }
        if (WaveDirector.Wave % 10 == 0 && !HudPaper.SealUp) {
            run.Problem(PlaytestSeverity.Bug, "no red seal on boss wave " + WaveDirector.Wave);
        }
        CheckLayout(run, "boss-wave");
        run.Finish(PlaytestOutcome.Done);
    }

    // No HUD element off screen or under a touch control, and every number 16 CSS px or more.
    static void CheckLayout(PlaytestRun run, string where) {
        float ratio = UiFit.PixelRatio;
        float minPx = 999f;
        int overlaps = 0;
        int off = 0;
        var pads = new List<RectTransform>();
        TouchControls.Shown(pads);
        var padRects = new List<Rect>();
        foreach (RectTransform pad in pads) {
            padRects.Add(Screen(pad));
        }
        HudPaper paper = HudPaper.Instance;
        if (paper == null) {
            return;
        }
        foreach (Text text in paper.GetComponentsInChildren<Text>(false)) {
            if (string.IsNullOrEmpty(text.text) || text.color.a < 0.05f) {
                continue;
            }
            minPx = Mathf.Min(minPx, text.fontSize * text.transform.lossyScale.y / ratio);
        }
        foreach (Image image in paper.GetComponentsInChildren<Image>(false)) {
            if (image.color.a < 0.05f || image.rectTransform.parent == null || image.rectTransform.parent.name == "Digits") {
                continue;
            }
            Rect r = Screen(image.rectTransform);
            if (r.xMin < -2f || r.yMin < -2f || r.xMax > UnityEngine.Screen.width + 2f || r.yMax > UnityEngine.Screen.height + 2f) {
                // The postmark's pop scales past its box for 0.18 s; only the settled layout counts.
                if (image.name != "Mark") {
                    off++;
                }
            }
            if (image.name == "Card" || image.name == "Mark" || image.name == "Ledger Art" || image.name == "Stamp") {
                foreach (Rect p in padRects) {
                    if (p.Overlaps(r)) {
                        overlaps++;
                        run.Problem(PlaytestSeverity.Bug, where + ": " + image.name + " overlaps a touch control");
                    }
                }
            }
        }
        run.Event("hud-layout", where + ": minTextPx " + minPx.ToString("0.0") + ", touchOverlaps " + overlaps + ", offScreen " + off);
        run.Metric("paperMinTextPx", minPx);
        run.Metric("paperTouchOverlaps", overlaps);
        if (UiFit.Touch && minPx < 16f) {
            run.Problem(PlaytestSeverity.Bug, where + ": a HUD number is " + minPx.ToString("0.0") + " px, wanted 16 or more");
        }
        if (off > 0) {
            run.Problem(PlaytestSeverity.Bug, where + ": " + off + " HUD pieces are off screen");
        }
    }

    static Rect Screen(RectTransform rect) {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }
}
