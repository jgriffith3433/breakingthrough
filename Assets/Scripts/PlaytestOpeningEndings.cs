using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// opening-endings: Chapters 4-6 in-engine openings plus ADDRESS FILED / RECEIVED endings and live credits.
// Acceptance: ≥5 moving shots per opening; stamp in engine; ≥6 doors on RECEIVED; phone credits ≥14 px and tap-to-card ≤1 s.
public static class PlaytestOpeningEndings {
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

    static IEnumerator EnterChapter(int chapter) {
        CampaignSave.EnterSandbox();
        CampaignSave.Slot file = CampaignSave.Get(0);
        file.used = true;
        file.chapter = chapter;
        file.reached = chapter;
        file.cleared = chapter;
        yield return Wait(0.6f);
        if (ChapterScene.Instance != null && ChapterScene.Instance.CardUp && ChapterScene.Instance.BeginButton != null) {
            ChapterScene.Instance.ShowCard(chapter);
            yield return Wait(0.8f);
            yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.BeginButton != null, 10f, "Chapter " + (chapter + 1) + "'s card");
            yield break;
        }
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.CampaignButton == null) {
            run.Problem(PlaytestSeverity.Bug, "opening-endings: no Campaign button");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        run.Event("mode-select", "Campaign chapter " + (chapter + 1));
        if (Phone) {
            TouchControls.BotFinger(8, RectScreen(menu.CampaignButton.GetComponent<RectTransform>()), true);
            yield return Wait(0.05f);
            TouchControls.BotFinger(8, Vector2.zero, false);
        } else {
            Tap(menu.CampaignButton.gameObject);
        }
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files");
        if (run.Done) {
            yield break;
        }
        yield return Wait(0.8f);
        Button slot = CampaignPanel.Instance.SlotButtons[0];
        if (Phone) {
            TouchControls.BotFinger(8, RectScreen(slot.GetComponent<RectTransform>()), true);
            yield return Wait(0.05f);
            TouchControls.BotFinger(8, Vector2.zero, false);
        } else {
            Tap(slot.gameObject);
        }
        run.Event("tap", "case file 1");
        yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.CardUp && ChapterScene.Instance.BeginButton != null, 45f, "Chapter " + (chapter + 1) + "'s card");
    }

    static IEnumerator PressBegin(int chapter) {
        yield return Wait(1.2f);
        if (ChapterScene.Instance == null || ChapterScene.Instance.BeginButton == null) {
            run.Problem(PlaytestSeverity.Bug, "opening-endings: no Begin on Chapter " + (chapter + 1));
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        if (Phone) {
            TouchControls.BotFinger(8, RectScreen(ChapterScene.Instance.BeginButton.GetComponent<RectTransform>()), true);
            yield return Wait(0.05f);
            TouchControls.BotFinger(8, Vector2.zero, false);
        } else {
            Tap(ChapterScene.Instance.BeginButton.gameObject);
        }
        run.Event("tap", "Begin chapter " + (chapter + 1));
        yield return run.Until(() => ChapterRun.Instance != null && ChapterRun.Instance.Chapter == chapter, 45f, "ChapterRun for chapter " + (chapter + 1));
    }

    public static IEnumerator Run(PlaytestRun r) {
        run = r;
        CinematicDirector.Events.Clear();
        run.Event("setup", "opening-endings: ch4-6 world opens + filed/received + live credits; touch=" + UiFit.Touch + " size=" + Screen.width + "x" + Screen.height);

        yield return WatchOpen(3, "ch4_open_world", "paper");
        if (run.Done) {
            yield break;
        }
        if (ChapterRun.Instance != null) {
            ChapterRun.Instance.ToMenu();
        }
        yield return run.Until(() => ChapterRun.Instance == null, 20f, "leave Chapter 4");
        yield return Wait(1f);

        CinematicDirector.Events.Clear();
        yield return WatchOpen(4, "ch5_open_world", null);
        if (run.Done) {
            yield break;
        }
        if (ChapterRun.Instance != null) {
            ChapterRun.Instance.ToMenu();
        }
        yield return run.Until(() => ChapterRun.Instance == null, 20f, "leave Chapter 5");
        yield return Wait(1f);

        CinematicDirector.Events.Clear();
        yield return WatchOpen(5, "ch6_open_world", null);
        if (run.Done) {
            yield break;
        }

        // Stay on the filed Hollow and play the endings in engine (setup shortcut: no full boss fight).
        run.Event("setup", "ending_filed_world + ending_received_world + credits-live on Chapter 6's map");
        yield return Wait(0.8f);
        yield return PlayEnding("ending_filed_world", "address-stamp|", "filed");
        if (run.Done) {
            yield break;
        }
        // Grant all six finds so the secret path is the one under test, then play RECEIVED.
        CampaignSave.Slot file = CampaignSave.Get(0);
        file.finds = (1 << 6) - 1;
        CinematicDirector.Events.Clear();
        yield return PlayEnding("ending_received_world", "doors|", "received");
        if (run.Done) {
            yield break;
        }
        run.Metric("endingDoors", CinematicDirector.DoorsShown);
        if (CinematicDirector.DoorsShown < 6) {
            run.Problem(PlaytestSeverity.Bug, "opening-endings: RECEIVED showed " + CinematicDirector.DoorsShown + " doors (need ≥6)");
        } else {
            run.Event("ending-doors", CinematicDirector.DoorsShown + " doors around the ring");
        }
        if (!CinematicDirector.AddressStampSeen) {
            run.Problem(PlaytestSeverity.Bug, "opening-endings: ADDRESS stamp never came down in engine");
        } else {
            run.Event("ending-stamp", "ADDRESS stamp seen in engine");
            run.Metric("endingStamp", 1);
        }

        yield return WatchCreditsLive();
        if (run.Done) {
            yield break;
        }

        run.Progress(1f);
        if (!run.Done) {
            run.Finish(PlaytestOutcome.Done);
        }
    }

    static IEnumerator WatchOpen(int chapter, string expectId, string beat) {
        yield return EnterChapter(chapter);
        if (run.Done) {
            yield break;
        }
        yield return PressBegin(chapter);
        if (run.Done) {
            yield break;
        }
        run.Scene(SceneManager.GetActiveScene().name);
        yield return run.Until(() => CinematicDirector.IsOpen || Seen("fallback"), 20f, expectId + " to start");
        if (run.Done) {
            yield break;
        }
        if (Seen("fallback")) {
            run.Problem(PlaytestSeverity.Bug, "opening-endings: " + expectId + " fell back to still reel");
        }
        string reel = CinematicDirector.Instance != null ? CinematicDirector.Instance.ReelId : "";
        run.Event("opening-start", reel + " chapter=" + (chapter + 1));
        if (reel != expectId) {
            run.Problem(PlaytestSeverity.Bug, "opening-endings: expected " + expectId + ", got " + reel);
        }

        int movingShots = 0;
        var lastPos = new Dictionary<string, Vector3>();
        float started = Time.realtimeSinceStartup;
        int lastShot = -1;
        while (!run.Done && CinematicDirector.IsOpen && Time.realtimeSinceStartup - started < 70f) {
            int idx = CinematicDirector.Instance != null ? CinematicDirector.Instance.ShotIndex : -1;
            if (idx != lastShot && idx >= 0) {
                lastShot = idx;
                run.Event("opening-shot", CinematicDirector.Instance.ShotId + " #" + idx + " caption=\"" + CinematicDirector.Instance.Subtitle + "\"");
                run.Progress(0.05f + 0.2f * (chapter - 3) + 0.2f * (idx + 1) / Mathf.Max(1, CinematicDirector.Instance.ShotCount));
                lastPos.Clear();
            }
            SampleMotion(ref movingShots, lastPos);
            yield return null;
        }
        float seconds = Time.realtimeSinceStartup - started;
        run.Event("opening-end", "chapter=" + (chapter + 1) + " shots=" + CinematicDirector.ShotsShown + " moving=" + movingShots + " seconds=" + seconds.ToString("0.0"));
        run.Metric("openingCh" + (chapter + 1) + "Shots", CinematicDirector.ShotsShown);
        run.Metric("openingCh" + (chapter + 1) + "Moving", movingShots);
        run.Metric("openingCh" + (chapter + 1) + "Seconds", Mathf.RoundToInt(seconds));
        if (beat == "paper") {
            bool paper = Seen("paper|");
            run.Event("opening-paper", paper ? "seen" : "missing");
            run.Metric("openingPaper", paper ? 1 : 0);
            if (!paper) {
                run.Problem(PlaytestSeverity.Bug, "opening-endings: Chapter 4 paper offer never fired");
            }
        }
        if (CinematicDirector.ShotsShown < 5) {
            run.Problem(PlaytestSeverity.Bug, "opening-endings: Chapter " + (chapter + 1) + " fewer than 5 shots (" + CinematicDirector.ShotsShown + ")");
        }
        if (movingShots < 5) {
            run.Problem(PlaytestSeverity.Bug, "opening-endings: Chapter " + (chapter + 1) + " fewer than 5 moving shots (" + movingShots + ")");
        }
    }

    static IEnumerator PlayEnding(string id, string beatPrefix, string tag) {
        if (ChapterRun.Instance == null) {
            run.Problem(PlaytestSeverity.Bug, "opening-endings: no ChapterRun for " + id);
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        bool done = false;
        CinematicDirector.PlayChapter(id, () => done = true);
        yield return run.Until(() => CinematicDirector.IsOpen || done || Seen("fallback"), 8f, id + " start");
        if (Seen("fallback")) {
            run.Problem(PlaytestSeverity.Bug, "opening-endings: " + id + " fell back to still reel");
        }
        string reel = CinematicDirector.Instance != null ? CinematicDirector.Instance.ReelId : "";
        run.Event("ending-start", tag + " reel=" + reel);
        float started = Time.realtimeSinceStartup;
        int movingShots = 0;
        var lastPos = new Dictionary<string, Vector3>();
        int lastShot = -1;
        bool holding = false;
        while (!run.Done && CinematicDirector.IsOpen && Time.realtimeSinceStartup - started < 90f) {
            int idx = CinematicDirector.Instance != null ? CinematicDirector.Instance.ShotIndex : -1;
            if (idx != lastShot && idx >= 0) {
                lastShot = idx;
                run.Event("ending-shot", tag + " " + CinematicDirector.Instance.ShotId + " #" + idx);
                lastPos.Clear();
            }
            // Drive the finale carry hold so ending_filed_world can print ADDRESS.
            if (CinematicDirector.FinaleCarryActive) {
                if (!holding) {
                    PlayInput.PadHold(KeyCode.Mouse0, true);
                    holding = true;
                }
                if (CinematicDirector.FinaleTimingOpen) {
                    PlayInput.PadTap(KeyCode.Space);
                }
                if (CinematicDirector.FinaleCarryReady) {
                    PlayInput.PadHold(KeyCode.Mouse0, false);
                    holding = false;
                }
            } else if (holding) {
                PlayInput.PadHold(KeyCode.Mouse0, false);
                holding = false;
            } else {
                SampleMotion(ref movingShots, lastPos);
            }
            yield return null;
        }
        if (holding) {
            PlayInput.PadHold(KeyCode.Mouse0, false);
        }
        yield return run.Until(() => done || !CinematicDirector.IsOpen, 5f, id + " end");
        bool beat = string.IsNullOrEmpty(beatPrefix) || Seen(beatPrefix);
        run.Event("ending-end", tag + " shots=" + CinematicDirector.ShotsShown + " moving=" + movingShots + " beat=" + beat);
        if (!beat) {
            run.Problem(PlaytestSeverity.Bug, "opening-endings: " + tag + " missing beat " + beatPrefix);
        }
    }

    static IEnumerator WatchCreditsLive() {
        bool done = false;
        CinematicDirector.PlayCreditsLive(() => done = true, true);
        yield return run.Until(() => CinematicDirector.CreditsLive || done, 5f, "live credits");
        run.Event("credits-live", "start");
        yield return Wait(1.2f);
        float px = CinematicDirector.CreditsTextPx;
        run.Metric("creditsTextPx", Mathf.RoundToInt(px));
        run.Event("credits-px", px.ToString("0.0"));
        if (Phone && px + 0.01f < 14f) {
            run.Problem(PlaytestSeverity.Bug, "opening-endings: credits text " + px.ToString("0.0") + " px (< 14) at " + Screen.width + "x" + Screen.height);
        }
        float skipAt = Time.realtimeSinceStartup;
        if (Phone) {
            // First tap reveals Skip; second tap jumps to the end card.
            TouchControls.BotFinger(9, new Vector2(Screen.width * 0.5f, Screen.height * 0.45f), true);
            yield return Wait(0.05f);
            TouchControls.BotFinger(9, Vector2.zero, false);
            yield return Wait(0.25f);
            TouchControls.BotFinger(9, new Vector2(Screen.width * 0.5f, Screen.height * 0.45f), true);
            yield return Wait(0.05f);
            TouchControls.BotFinger(9, Vector2.zero, false);
        } else {
            PlayInput.Tap(KeyCode.Escape);
        }
        yield return run.Until(() => done || !CinematicDirector.CreditsLive, 3f, "credits skip to end card");
        float skipSeconds = Time.realtimeSinceStartup - skipAt;
        run.Metric("creditsSkipMs", Mathf.RoundToInt(skipSeconds * 1000f));
        run.Event("credits-skip", "seconds=" + skipSeconds.ToString("0.000") + " directorMs=" + CinematicDirector.CreditsSkipMs.ToString("0"));
        if (skipSeconds > 1f) {
            run.Problem(PlaytestSeverity.Bug, "opening-endings: credits tap to end took " + skipSeconds.ToString("0.00") + " s (> 1 s)");
        }
        // The end card is what PlayCreditsLive hands to; here the callback just marks done.
        run.Event("credits-card", done ? "callback" : "missing");
    }

    static void SampleMotion(ref int movingShots, Dictionary<string, Vector3> lastPos) {
        if (CinematicDirector.Instance == null) {
            return;
        }
        string key = CinematicDirector.Instance.ShotId;
        if (string.IsNullOrEmpty(key)) {
            return;
        }
        Transform player = PlayerGrowth.Instance != null ? PlayerGrowth.Instance.transform : null;
        Transform slice = Companion.Instance != null ? Companion.Instance.transform
            : (Musician.Instance != null ? Musician.Instance.transform : null);
        Vector3 w = player != null ? player.position : Vector3.zero;
        Vector3 s = slice != null ? slice.position : Vector3.zero;
        // Also count scripted bots (Server / Mega / Yo Bot) so paper and reading shots register as moving.
        Vector3 b = Vector3.zero;
        foreach (Enemy e in Enemy.Roster) {
            if (e != null && e.Scripted) {
                b = e.transform.position;
                break;
            }
        }
        if (!lastPos.ContainsKey(key + "|w")) {
            lastPos[key + "|w"] = w;
            lastPos[key + "|s"] = s;
            lastPos[key + "|b"] = b;
            lastPos[key + "|moved"] = Vector3.zero;
            return;
        }
        float moved = Flat(w - lastPos[key + "|w"]).magnitude
            + Flat(s - lastPos[key + "|s"]).magnitude
            + Flat(b - lastPos[key + "|b"]).magnitude;
        lastPos[key + "|w"] = w;
        lastPos[key + "|s"] = s;
        lastPos[key + "|b"] = b;
        if (moved > 0.35f && lastPos[key + "|moved"].x < 0.5f) {
            lastPos[key + "|moved"] = Vector3.one;
            movingShots++;
            run.Event("opening-move", key + " delta=" + moved.ToString("0.00"));
        }
    }

    static Vector3 Flat(Vector3 v) {
        v.y = 0f;
        return v;
    }
}
