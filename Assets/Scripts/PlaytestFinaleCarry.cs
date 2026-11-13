using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// finale-carry: Chapter 6 ADDRESS FILED interactive beat — hold swing to carry the stamp, bat 3 notices,
// release to print ADDRESS and light ≥4 lamps. Phone: hold prompt in the lower third, ≥88 CSS px.
public static class PlaytestFinaleCarry {
    static PlaytestRun run;

    static void Tap(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    static IEnumerator Wait(float seconds) {
        yield return new WaitForSecondsRealtime(seconds);
    }

    static bool Phone {
        get { return UiFit.Touch && Screen.height >= Screen.width; }
    }

    static Vector2 RectScreen(RectTransform rt) {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return new Vector2((corners[0].x + corners[2].x) * 0.5f, (corners[0].y + corners[2].y) * 0.5f);
    }

    static bool Seen(string prefix) {
        foreach (string e in CinematicDirector.Events) {
            if (e.StartsWith(prefix)) {
                return true;
            }
        }
        return false;
    }

    public static IEnumerator Run(PlaytestRun r) {
        run = r;
        CinematicDirector.Events.Clear();
        run.Event("setup", "finale-carry: Chapter 6 filed Hollow + ending_filed_world hold beat; touch=" + UiFit.Touch + " size=" + Screen.width + "x" + Screen.height);

        CampaignSave.EnterSandbox();
        CampaignSave.Slot file = CampaignSave.Get(0);
        file.used = true;
        file.chapter = 5;
        file.reached = 5;
        file.cleared = 5;
        yield return Wait(0.5f);

        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.CampaignButton == null) {
            run.Problem(PlaytestSeverity.Bug, "finale-carry: no Campaign button");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        run.Event("mode-select", "Campaign");
        if (Phone) {
            yield return PlaytestUiFinger.Tap(RectScreen(menu.CampaignButton.GetComponent<RectTransform>()), "PlaytestFinaleCarry");
        } else {
            Tap(menu.CampaignButton.gameObject);
        }
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files");
        if (run.Done) {
            yield break;
        }
        yield return Wait(0.6f);
        Button slot = CampaignPanel.Instance.SlotButtons[0];
        if (Phone) {
            yield return PlaytestUiFinger.Tap(RectScreen(slot.GetComponent<RectTransform>()), "PlaytestFinaleCarry");
        } else {
            Tap(slot.gameObject);
        }
        yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.CardUp && ChapterScene.Instance.BeginButton != null, 45f, "Chapter 6 card");
        if (run.Done) {
            yield break;
        }
        ChapterScene.Instance.ShowCard(5);
        yield return Wait(0.8f);
        if (Phone) {
            yield return PlaytestUiFinger.Tap(RectScreen(ChapterScene.Instance.BeginButton.GetComponent<RectTransform>()), "PlaytestFinaleCarry");
        } else {
            Tap(ChapterScene.Instance.BeginButton.gameObject);
        }
        yield return run.Until(() => ChapterRun.Instance != null && ChapterRun.Instance.Chapter == 5, 45f, "ChapterRun Chapter 6");
        if (run.Done) {
            yield break;
        }
        run.Scene(SceneManager.GetActiveScene().name);

        yield return run.Until(() => CinematicDirector.IsOpen || ChapterRun.Instance.PlaySeconds > 2f, 20f, "opening or play");
        if (CinematicDirector.IsOpen) {
            CinematicDirector.Instance.Skip();
            yield return run.Until(() => !CinematicDirector.IsOpen, 8f, "opening closed");
        }
        yield return Wait(0.6f);

        run.Event("setup", "ending_filed_world with finaleCarry");
        CinematicDirector.Events.Clear();
        bool done = false;
        CinematicDirector.PlayChapter("ending_filed_world", () => done = true);
        yield return run.Until(() => CinematicDirector.IsOpen || done || Seen("fallback"), 8f, "ending_filed_world start");
        if (Seen("fallback")) {
            run.Problem(PlaytestSeverity.Bug, "finale-carry: ending_filed_world fell back to still reel");
        }
        run.Event("finale-start", "reel=" + (CinematicDirector.Instance != null ? CinematicDirector.Instance.ReelId : "?"));

        yield return run.Until(() => CinematicDirector.FinaleCarryActive || done || !CinematicDirector.IsOpen, 50f, "finale carry");
        if (!CinematicDirector.FinaleCarryActive) {
            run.Problem(PlaytestSeverity.Bug, "finale-carry: finaleCarry never armed");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        run.Event("finale-hold", "active progress=" + CinematicDirector.FinaleCarryProgress.ToString("0.00"));
        run.Progress(0.2f);

        yield return Wait(0.4f);
        float holdW = CinematicDirector.FinaleHoldPromptWidthPx;
        float holdY = CinematicDirector.FinaleHoldPromptCenterY01;
        run.Metric("finaleHoldPromptPx", Mathf.RoundToInt(holdW));
        run.Metric("finaleHoldPromptY01", Mathf.RoundToInt(holdY * 100f));
        run.Event("finale-hold-prompt", "w=" + holdW.ToString("0") + "px y01=" + holdY.ToString("0.00") + " phone=" + Phone);
        if (Phone) {
            if (holdW < 88f) {
                run.Problem(PlaytestSeverity.Bug, "finale-carry: hold prompt " + holdW.ToString("0") + " px wide (need ≥88)");
            }
            if (holdY > 0.34f) {
                run.Problem(PlaytestSeverity.Bug, "finale-carry: hold prompt center y01=" + holdY.ToString("0.00") + " is above the lower third");
            }
        }

        // Drive hold through PlayInput (touch pads are hidden while Cutscene.IsOpen).
        PlayInput.PadHold(KeyCode.Mouse0, true);
        float started = Time.realtimeSinceStartup;
        int lastBats = 0;
        bool missedOnce = false;
        bool waitingMiss = false;
        while (!run.Done && CinematicDirector.IsOpen && CinematicDirector.FinaleCarryActive && Time.realtimeSinceStartup - started < 55f) {
            if (CinematicDirector.FinaleTimingOpen) {
                if (!missedOnce && !waitingMiss) {
                    // First notice: deliberately miss to prove the beat stays alive.
                    waitingMiss = true;
                    float missUntil = Time.unscaledTime + 2.5f;
                    while (CinematicDirector.FinaleTimingOpen && Time.unscaledTime < missUntil && CinematicDirector.FinaleCarryActive) {
                        yield return null;
                    }
                    if (CinematicDirector.FinaleCarryMisses > 0 || Seen("finale-miss|")) {
                        missedOnce = true;
                        run.Event("finale-miss", "deliberate; still active=" + CinematicDirector.FinaleCarryActive);
                        if (!CinematicDirector.FinaleCarryActive) {
                            run.Problem(PlaytestSeverity.Bug, "finale-carry: miss ended the beat");
                        }
                    }
                    waitingMiss = false;
                    continue;
                }
                // Bat remaining notices with Space (works while Mouse0 is held).
                PlayInput.PadTap(KeyCode.Space);
                yield return Wait(0.12f);
            }
            if (CinematicDirector.FinaleCarryBats > lastBats) {
                lastBats = CinematicDirector.FinaleCarryBats;
                run.Event("finale-bat", "bats=" + lastBats);
                run.Progress(0.2f + 0.15f * lastBats);
            }
            if (CinematicDirector.FinaleCarryReady) {
                run.Event("finale-ready", "seconds=" + CinematicDirector.FinaleCarrySeconds.ToString("0.0") + " progress=" + CinematicDirector.FinaleCarryProgress.ToString("0.00"));
                PlayInput.PadHold(KeyCode.Mouse0, false);
                break;
            }
            yield return null;
        }
        PlayInput.PadHold(KeyCode.Mouse0, false);

        if (!missedOnce) {
            run.Problem(PlaytestSeverity.Warning, "finale-carry: deliberate miss never logged");
        }

        yield return run.Until(() => Seen("finale-stamp|") || Seen("address-stamp|") || done || !CinematicDirector.IsOpen, 20f, "ADDRESS stamp");
        if (Seen("finale-stamp|") || CinematicDirector.AddressStampSeen) {
            run.Event("finale-stamp", "ADDRESS printed");
            run.Metric("endingStamp", 1);
        } else {
            run.Problem(PlaytestSeverity.Bug, "finale-carry: ADDRESS never printed on the ring");
        }

        yield return run.Until(() => CinematicDirector.FinaleLampsLit >= 4 || done || !CinematicDirector.IsOpen, 12f, "lamps");
        int lamps = CinematicDirector.FinaleLampsLit;
        run.Event("finale-lamps", lamps + " lit");
        run.Metric("finaleLamps", lamps);
        if (lamps < 4) {
            run.Problem(PlaytestSeverity.Bug, "finale-carry: only " + lamps + " lamps lit (need ≥4)");
        }

        float seconds = CinematicDirector.FinaleCarrySeconds;
        run.Metric("finaleCarrySeconds", Mathf.RoundToInt(seconds));
        run.Metric("finaleCarryBats", CinematicDirector.FinaleCarryBats);
        run.Metric("finaleCarryMisses", CinematicDirector.FinaleCarryMisses);
        run.Event("finale-carry-done", "seconds=" + seconds.ToString("0.0") + " bats=" + CinematicDirector.FinaleCarryBats + " misses=" + CinematicDirector.FinaleCarryMisses);
        if (seconds < 18f || seconds > 45f) {
            run.Problem(PlaytestSeverity.Bug, "finale-carry: beat lasted " + seconds.ToString("0.0") + " s (want ~20–40)");
        }
        if (CinematicDirector.FinaleCarryBats < 3) {
            run.Problem(PlaytestSeverity.Bug, "finale-carry: only " + CinematicDirector.FinaleCarryBats + " bats (need 3)");
        }
        if (!Seen("finale-bat|")) {
            run.Problem(PlaytestSeverity.Bug, "finale-carry: no real finale-bat events (failsafe must not invent bats)");
        }

        yield return run.Until(() => done || !CinematicDirector.IsOpen, 20f, "filed reel end");
        run.Progress(0.85f);

        CampaignSave.Slot slotFile = CampaignSave.Get(0);
        slotFile.finds = (1 << 6) - 1;
        CinematicDirector.Events.Clear();
        bool receivedDone = false;
        CinematicDirector.PlayChapter("ending_received_world", () => receivedDone = true);
        yield return run.Until(() => CinematicDirector.IsOpen || receivedDone || Seen("fallback"), 8f, "RECEIVED start");
        yield return run.Until(() => receivedDone || !CinematicDirector.IsOpen, 60f, "RECEIVED end");
        if (CinematicDirector.DoorsShown >= 6 || Seen("doors|")) {
            run.Event("received-intact", "doors=" + CinematicDirector.DoorsShown);
            run.Metric("endingDoors", CinematicDirector.DoorsShown);
        } else {
            run.Problem(PlaytestSeverity.Bug, "finale-carry: RECEIVED path broken after the carry beat (doors=" + CinematicDirector.DoorsShown + ")");
        }

        run.Progress(1f);
        if (!run.Done) {
            run.Finish(PlaytestOutcome.Done);
        }
    }
}
