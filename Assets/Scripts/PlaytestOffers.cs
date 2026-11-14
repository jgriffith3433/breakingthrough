using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// The "boon-offers" scenario: a 20-wave Arcade run in which every pick-of-three is read off the screen. Per offer it logs the
// boons and their families (event offer) and the run's own audit (RunBoons.Audit: picks.families is the fewest families any
// offer spanned, picks.deadOffers the offers that held a maxed boon, a repeat or an evolution before its parents). The first
// offer is rerolled by finger: the three are replaced once, then the button greys out and a second tap does nothing. Every
// tap is a touch on the card or button (run it with mobile for 390x844). Named setup shortcuts: the bot strikes off each wave's
// bots, a level of bolts is granted once a wave so a card is owed, and Enter skips the breaks.
public static class PlaytestOffers {
    const int Waves = 20;

    static void Tap(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    static Rect Box(RectTransform rect) {
        var c = new Vector3[4];
        rect.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    static string Describe(List<BoonId> offer) {
        var names = new List<string>();
        for (int i = 0; i < offer.Count; i++) {
            names.Add(RunBoons.FamilyName(RunBoons.FamilyOf(offer[i])) + ": " + RunBoons.Info(offer[i]).Name);
        }
        return string.Join(" | ", names.ToArray());
    }

    static bool Same(List<BoonId> a, List<BoonId> b) {
        if (a.Count != b.Count) {
            return false;
        }
        for (int i = 0; i < a.Count; i++) {
            if (!b.Contains(a[i])) {
                return false;
            }
        }
        return true;
    }

    // Is a control fully inside the safe area and at least 44 CSS px each way?
    static bool Fits(GameObject go, out float minPx) {
        Rect safe = Screen.safeArea;
        float px = Mathf.Max(1f, UiFit.PixelRatio);
        Rect r = Box((RectTransform)go.transform);
        minPx = Mathf.Min(r.width, r.height) / px;
        return r.xMin >= safe.xMin - 1.5f && r.xMax <= safe.xMax + 1.5f && r.yMin >= safe.yMin - 1.5f && r.yMax <= safe.yMax + 1.5f;
    }

    public static IEnumerator Run(PlaytestRun run) {
        UiFit.ForceTouch(true);
        CampaignSave.EnterSandbox();
        MetaProgress.EnterSandbox(true);
        yield return new WaitForSecondsRealtime(1f);
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.PlayButton == null) {
            run.Problem(PlaytestSeverity.Bug, "boon-offers: no Play button on the menu");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        Tap(menu.PlayButton.gameObject);
        yield return run.Until(() => WaveDirector.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the game scene");
        if (run.Done) {
            yield break;
        }
        run.Scene("Arcade, 20 waves of picks");
        run.Event("setup", "each wave's bots are struck off, a level of bolts is granted once a wave, Enter skips the breaks");

        bool rerollDone = false;
        int grantedWave = 0;
        int cardsTapped = 0;
        int cardMask = 0;
        int offersSeen = 0;
        int minFamilies = 99;
        int unusable = 0;
        float minTap = 9999f;
        int outside = 0;
        float stuckSince = Time.realtimeSinceStartup;
        int lastWave = 0;

        while (!run.Done && WaveDirector.Wave <= Waves) {
            if (WaveDirector.IsLost) {
                run.Problem(PlaytestSeverity.Bug, "boon-offers: Slice fell during the setup run at wave " + WaveDirector.Wave);
                break;
            }
            if (Musician.Instance != null) {
                Musician.Instance.Heal(50);
            }
            if (WaveDirector.Wave != lastWave) {
                lastWave = WaveDirector.Wave;
                stuckSince = Time.realtimeSinceStartup;
            }
            if (Time.realtimeSinceStartup - stuckSince > 90f) {
                run.Problem(PlaytestSeverity.Bug, "boon-offers: wave " + WaveDirector.Wave + " did not end in 90 s");
                break;
            }
            if (LevelUpScreen.IsOpen) {
                yield return new WaitForSecondsRealtime(1.6f);   // the cards deal face down, then flip
                if (!LevelUpScreen.IsOpen) {
                    continue;
                }
                List<BoonId> offer = new List<BoonId>(LevelUpScreen.Offered);
                int families = 0;
                var seen = new HashSet<BoonFamily>();
                for (int i = 0; i < offer.Count; i++) {
                    seen.Add(RunBoons.FamilyOf(offer[i]));
                }
                families = seen.Count;
                offersSeen++;
                minFamilies = Mathf.Min(minFamilies, families);
                run.Event("offer", "wave " + WaveDirector.Wave + ", " + LevelUpScreen.Heading + ": " + Describe(offer) + " (families " + families + ")");
                if (offer.Count == 3) {
                    for (int i = 0; i < 3; i++) {
                        float px;
                        GameObject card = LevelUpScreen.CardObject(i);
                        if (card == null || !Fits(card, out px)) {
                            outside++;
                        } else {
                            minTap = Mathf.Min(minTap, px);
                        }
                    }
                }

                if (!rerollDone) {
                    rerollDone = true;
                    GameObject button = LevelUpScreen.RerollObject;
                    float rpx;
                    bool inside = button != null && Fits(button, out rpx);
                    rpx = 0f;
                    if (button != null) {
                        Fits(button, out rpx);
                    }
                    run.Event("reroll-button", UiFit.Mode + " " + Screen.width + "x" + Screen.height + ": " + (inside ? "inside the safe area" : "OUTSIDE the safe area") + ", " + Mathf.Round(rpx) + " px, usable " + LevelUpScreen.RerollUsable + ", reads \"" + LevelUpScreen.RerollText + "\"");
                    run.Metric("picks.rerollPx", Mathf.Round(rpx));
                    if (!inside || rpx < 44f || !LevelUpScreen.RerollUsable) {
                        run.Problem(PlaytestSeverity.Bug, "boon-offers: the Reroll button is off the safe area, under 44 px or not usable (" + rpx + " px)");
                    }
                    if (button != null) {
                        Tap(button);
                    }
                    yield return new WaitForSecondsRealtime(1.6f);
                    List<BoonId> again = new List<BoonId>(LevelUpScreen.Offered);
                    bool replaced = !Same(offer, again);
                    seen.Clear();
                    for (int i = 0; i < again.Count; i++) {
                        seen.Add(RunBoons.FamilyOf(again[i]));
                    }
                    offersSeen++;
                    minFamilies = Mathf.Min(minFamilies, seen.Count);
                    run.Event("reroll", "the three were replaced: " + replaced + ". Now " + Describe(again) + " (families " + seen.Count + "); button usable " + LevelUpScreen.RerollUsable + ", reads \"" + LevelUpScreen.RerollText + "\"");
                    if (!replaced) {
                        run.Problem(PlaytestSeverity.Bug, "boon-offers: the reroll dealt the same three boons");
                    }
                    if (LevelUpScreen.RerollUsable) {
                        run.Problem(PlaytestSeverity.Bug, "boon-offers: the Reroll button did not grey out after its one use");
                    }
                    int takenBefore = RunBoons.RerollsTaken;
                    if (button != null) {
                        Tap(button);
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                    bool held = RunBoons.RerollsTaken == takenBefore && Same(again, LevelUpScreen.Offered);
                    run.Event("reroll-spent", "a second tap on the greyed button changed nothing: " + held);
                    run.Metric("picks.rerolls", RunBoons.RerollsTaken);
                    if (!held) {
                        run.Problem(PlaytestSeverity.Bug, "boon-offers: a second reroll went through");
                    }
                    offer = again;
                } else if (LevelUpScreen.RerollUsable) {
                    unusable++;
                    run.Problem(PlaytestSeverity.Bug, "boon-offers: Reroll was usable again after being spent");
                }

                // Tap a card, a different slot each time so all three are pressed over the run.
                int slot = Mathf.Min(offersSeen % 3, Mathf.Max(0, offer.Count - 1));
                int picks = RunBoons.Picks;
                float until = Time.realtimeSinceStartup + 10f;
                while (LevelUpScreen.IsOpen && Time.realtimeSinceStartup < until) {
                    GameObject card = LevelUpScreen.CardObject(slot);
                    if (card != null) {
                        Tap(card);
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                if (RunBoons.Picks > picks) {
                    cardsTapped++;
                    cardMask |= 1 << slot;
                } else {
                    run.Problem(PlaytestSeverity.Bug, "boon-offers: tapping card " + (slot + 1) + " did not take the boon (wave " + WaveDirector.Wave + ")");
                }
                yield return new WaitForSecondsRealtime(0.3f);
                continue;
            }

            if (WaveDirector.Waiting) {
                PlayInput.Tap(KeyCode.Return);
            } else {
                if (grantedWave != WaveDirector.Wave) {
                    grantedWave = WaveDirector.Wave;
                    RunBoons.AddFeathers(RunBoons.NextAt);
                }
                var copy = new List<Enemy>(Enemy.Roster);
                foreach (Enemy e in copy) {
                    if (e != null && e.Alive) {
                        e.Damage(9999f, DamageKind.Bat, false);
                    }
                }
            }
            yield return new WaitForSecondsRealtime(0.4f);
        }

        if (offersSeen == 0) {
            minFamilies = 0;
        }
        run.Metric("picks.offers", RunBoons.Offers);
        run.Metric("picks.families", RunBoons.MinFamilies == 99 ? 0 : RunBoons.MinFamilies);
        run.Metric("picks.familiesAvg", RunBoons.Offers > 0 ? RunBoons.FamilySum / (float)RunBoons.Offers : 0f);
        run.Metric("picks.deadOffers", RunBoons.DeadOffers);
        run.Metric("picks.screenMinFamilies", minFamilies);
        run.Metric("picks.cardsTapped", cardsTapped);
        run.Metric("picks.cardSlotsTapped", ((cardMask & 1) + ((cardMask >> 1) & 1) + ((cardMask >> 2) & 1)));
        run.Metric("picks.cardMinPx", minTap > 9000f ? 0f : Mathf.Round(minTap));
        run.Event("offers-summary", "wave " + WaveDirector.Wave + ": " + RunBoons.Offers + " offers, fewest families " + RunBoons.MinFamilies + ", dead offers " + RunBoons.DeadOffers + ", rerolls taken " + RunBoons.RerollsTaken + ", card slots tapped " + cardMask + ", " + outside + " cards outside the safe area, build " + RunBoons.Summary());
        if (RunBoons.MinFamilies < 2) {
            run.Problem(PlaytestSeverity.Bug, "boon-offers: an offer spanned only " + RunBoons.MinFamilies + " family");
        }
        if (RunBoons.DeadOffers > 0) {
            run.Problem(PlaytestSeverity.Bug, "boon-offers: " + RunBoons.DeadOffers + " offers held a boon that could not help");
        }
        if (outside > 0 || (minTap < 44f && minTap < 9000f)) {
            run.Problem(PlaytestSeverity.Bug, "boon-offers: a card is outside the safe area or under 44 px (" + outside + " outside, " + minTap + " px)");
        }
        if (WaveDirector.Wave <= Waves && !run.Done) {
            run.Problem(PlaytestSeverity.Bug, "boon-offers: the run stopped at wave " + WaveDirector.Wave + " of " + Waves);
        }
    }
}
