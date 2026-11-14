using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// opening-ch23: Chapters 2 and 3 in-engine openings (ch2_open_world / ch3_open_world) on their own maps.
// Watches moving shots, the postmark slam (ch2), the belt carrying things (ch3), skip-to-play, and phone framing.
public static class PlaytestOpeningCh23 {
    static PlaytestRun run;
    static int framedOk;

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
            run.Problem(PlaytestSeverity.Bug, "opening-ch23: no Campaign button");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        run.Event("mode-select", "Campaign chapter " + (chapter + 1));
        if (Phone) {
            yield return PlaytestUiFinger.Tap(RectScreen(menu.CampaignButton.GetComponent<RectTransform>()), "PlaytestOpeningCh23");
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
            yield return PlaytestUiFinger.Tap(RectScreen(slot.GetComponent<RectTransform>()), "PlaytestOpeningCh23");
        } else {
            Tap(slot.gameObject);
        }
        run.Event("tap", "case file 1");
        yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.CardUp && ChapterScene.Instance.BeginButton != null, 45f, "Chapter " + (chapter + 1) + "'s card");
    }

    static IEnumerator PressBegin(int chapter) {
        yield return Wait(1.2f);
        if (ChapterScene.Instance == null || ChapterScene.Instance.BeginButton == null) {
            run.Problem(PlaytestSeverity.Bug, "opening-ch23: no Begin on Chapter " + (chapter + 1));
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        if (Phone) {
            yield return PlaytestUiFinger.Tap(RectScreen(ChapterScene.Instance.BeginButton.GetComponent<RectTransform>()), "PlaytestOpeningCh23");
        } else {
            Tap(ChapterScene.Instance.BeginButton.gameObject);
        }
        run.Event("tap", "Begin chapter " + (chapter + 1));
        yield return run.Until(() => ChapterRun.Instance != null && ChapterRun.Instance.Chapter == chapter, 45f, "ChapterRun for chapter " + (chapter + 1));
    }

    public static IEnumerator Run(PlaytestRun r) {
        run = r;
        CinematicDirector.Events.Clear();
        run.Event("setup", "opening-ch23: Chapters 2 and 3 in-engine openings; touch=" + UiFit.Touch + " size=" + Screen.width + "x" + Screen.height);
        framedOk = 0;

        yield return WatchChapter(1, "ch2_open_world", "postmark");
        if (run.Done) {
            yield break;
        }
        // Leave chapter 2 so chapter 3 can begin from the hub/card.
        if (ChapterRun.Instance != null) {
            ChapterRun.Instance.ToMenu();
        }
        yield return run.Until(() => ChapterRun.Instance == null, 20f, "leave Chapter 2");
        yield return Wait(1.2f);

        CinematicDirector.Events.Clear();
        yield return WatchChapter(2, "ch3_open_world", "belt");
        if (run.Done) {
            yield break;
        }

        // Skip pass on Chapter 3 while it is still open, or re-enter Chapter 2 for skip.
        yield return SkipPass(2);

        run.Metric("openingPhoneFramesOk", framedOk);
        run.Metric("openingCaptionBottom01", CinematicDirector.CaptionBottom01);
        if (Phone && framedOk < 8) {
            run.Problem(PlaytestSeverity.Bug, "opening-ch23: speaker left the phone frame too often (ok frames=" + framedOk + ")");
        }
        run.Progress(1f);
        if (!run.Done) {
            run.Finish(PlaytestOutcome.Done);
        }
    }

    static IEnumerator WatchChapter(int chapter, string expectId, string beat) {
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
            run.Problem(PlaytestSeverity.Bug, "opening-ch23: " + expectId + " fell back to still reel");
        }
        string reel = CinematicDirector.Instance != null ? CinematicDirector.Instance.ReelId : "";
        run.Event("opening-start", reel + " chapter=" + (chapter + 1));
        if (reel != expectId) {
            run.Problem(PlaytestSeverity.Bug, "opening-ch23: expected " + expectId + ", got " + reel);
        }

        int movingShots = 0;
        var lastPos = new Dictionary<string, Vector3>();
        float started = Time.realtimeSinceStartup;
        int lastShot = -1;
        while (!run.Done && CinematicDirector.IsOpen && Time.realtimeSinceStartup - started < 65f) {
            int idx = CinematicDirector.Instance != null ? CinematicDirector.Instance.ShotIndex : -1;
            if (idx != lastShot && idx >= 0) {
                lastShot = idx;
                run.Event("opening-shot", CinematicDirector.Instance.ShotId + " #" + idx + " caption=\"" + CinematicDirector.Instance.Subtitle + "\"");
                run.Progress(0.1f + 0.35f * (chapter == 1 ? 0f : 0.5f) + 0.35f * (idx + 1) / Mathf.Max(1, CinematicDirector.Instance.ShotCount));
                lastPos.Clear();
            }
            SampleMotion(ref movingShots, lastPos);
            if (Phone) {
                CheckPhoneFrame();
            }
            if (CinematicDirector.CaptionBottom01 + 0.001f < 0.15f && !string.IsNullOrEmpty(CinematicDirector.Instance != null ? CinematicDirector.Instance.Subtitle : null)) {
                run.Problem(PlaytestSeverity.Bug, "opening-ch23: caption bottom " + CinematicDirector.CaptionBottom01.ToString("0.00") + " is under the bottom 15%");
            }
            yield return null;
        }
        float seconds = Time.realtimeSinceStartup - started;
        bool postmark = Seen("postmark|");
        bool belt = Seen("belt|");
        run.Event("opening-end", "chapter=" + (chapter + 1) + " shots=" + CinematicDirector.ShotsShown + " moving=" + movingShots + " seconds=" + seconds.ToString("0.0") + " postmark=" + postmark + " belt=" + belt);
        if (beat == "postmark") {
            run.Event("opening-postmark", postmark ? "seen" : "missing");
            run.Metric("openingPostmark", postmark ? 1 : 0);
            run.Metric("openingCh2Shots", CinematicDirector.ShotsShown);
            run.Metric("openingCh2Moving", movingShots);
            run.Metric("openingCh2Seconds", Mathf.RoundToInt(seconds));
            if (!postmark) {
                run.Problem(PlaytestSeverity.Bug, "opening-ch23: Chapter 2 postmark slam never fired");
            }
        } else {
            run.Event("opening-belt", belt ? "seen" : "missing");
            run.Metric("openingBelt", belt ? 1 : 0);
            run.Metric("openingCh3Shots", CinematicDirector.ShotsShown);
            run.Metric("openingCh3Moving", movingShots);
            run.Metric("openingCh3Seconds", Mathf.RoundToInt(seconds));
            if (!belt) {
                run.Problem(PlaytestSeverity.Bug, "opening-ch23: Chapter 3 belt motion never fired");
            }
        }
        if (CinematicDirector.ShotsShown < 5) {
            run.Problem(PlaytestSeverity.Bug, "opening-ch23: Chapter " + (chapter + 1) + " fewer than 5 shots (" + CinematicDirector.ShotsShown + ")");
        }
        if (movingShots < 5) {
            run.Problem(PlaytestSeverity.Bug, "opening-ch23: Chapter " + (chapter + 1) + " fewer than 5 moving shots (" + movingShots + ")");
        }
        if (seconds > 60f) {
            run.Problem(PlaytestSeverity.Bug, "opening-ch23: Chapter " + (chapter + 1) + " opening lasted " + seconds.ToString("0.0") + " s (> 60)");
        }
        yield return run.Until(() => CinematicDirector.ClosedCount > 0 && !CinematicDirector.IsOpen, 3f, "cinematic closed");
        run.Event("cinematic.closed", "opening-ch23 chapter=" + (chapter + 1) + " reel=" + CinematicDirector.LastClosedReel + " closed=" + CinematicDirector.ClosedCount
            + " cap=" + CinematicDirector.LastClosedByCap + " seconds=" + CinematicDirector.LastClosedAfterSeconds.ToString("0.00") + " handoff=" + CinematicDirector.LastHandoffSeconds.ToString("0.00")
            + " touch=" + UiFit.Touch + " size=" + Screen.width + "x" + Screen.height);
        run.Metric("cinematic.closed.ch" + (chapter + 1), CinematicDirector.ClosedCount > 0 ? 1 : 0);
        if (CinematicDirector.ClosedCount != 1) {
            run.Problem(PlaytestSeverity.Bug, "opening-ch23: cinematic.closed fired " + CinematicDirector.ClosedCount + " times for Chapter " + (chapter + 1) + " (need exactly 1)");
        }
        if (CinematicDirector.LastClosedByCap) {
            run.Problem(PlaytestSeverity.Bug, "opening-ch23: Chapter " + (chapter + 1) + " opening only closed on its hard cap");
        }
        if (CinematicDirector.LastHandoffSeconds > 0.3f) {
            run.Problem(PlaytestSeverity.Bug, "opening-ch23: control returned " + CinematicDirector.LastHandoffSeconds.ToString("0.00") + " s after the last shot (> 0.3)");
        }
        if (CinematicDirector.LastHandoffSeconds > 1f) {
            run.Problem(PlaytestSeverity.Bug, "opening-ch23: handoff took " + CinematicDirector.LastHandoffSeconds.ToString("0.00") + " s");
        }
        if (chapter == 1) {
            yield return FirstBlowCheck();
        }
    }

    // The first 10 s of Chapter 2 once the stick is back: a Yo Bot runs in, the bat's one swing drops it (hit-stop, plates, ring),
    // and only then does the objective line (the 70-79 m gate counter) appear. The bat swings by itself: no input, so it holds on touch.
    static IEnumerator FirstBlowCheck() {
        ChapterRun chapter = ChapterRun.Instance;
        if (chapter == null) {
            run.Problem(PlaytestSeverity.Bug, "opening-ch23: no ChapterRun after the Chapter 2 opening");
            yield break;
        }
        float t0 = Time.realtimeSinceStartup;
        bool lineEarly = false;
        int impacts0 = BatEquipment.Impacts;
        while (!run.Done && Time.realtimeSinceStartup - t0 < 12f && !chapter.FirstBotDown) {
            lineEarly |= !string.IsNullOrEmpty(chapter.ObjectiveLine);
            yield return null;
        }
        float took = Time.realtimeSinceStartup - t0;
        run.Event("first-kill", "chapter 2: down=" + chapter.FirstBotDown + " " + took.ToString("0.0") + " s after the stick returned, bat impacts="
            + (BatEquipment.Impacts - impacts0) + ", objective line before the kill=" + lineEarly);
        run.Metric("ch2FirstKillSeconds", took);
        yield return Wait(0.3f);
        run.Event("objective", "after the first kill: \"" + chapter.ObjectiveLine + "\"");
        if (!chapter.FirstBotDown || took > 10f) {
            run.Problem(PlaytestSeverity.Bug, "opening-ch23: Chapter 2's first Yo Bot was not down within 10 s (" + took.ToString("0.0") + " s)");
        }
        if (lineEarly) {
            run.Problem(PlaytestSeverity.Bug, "opening-ch23: Chapter 2's objective line showed before the first kill");
        }
    }

    static IEnumerator SkipPass(int chapter) {
        run.Event("setup", "re-enter Chapter " + (chapter + 1) + " and skip mid-opening");
        if (ChapterRun.Instance != null) {
            ChapterRun.Instance.ToMenu();
        }
        yield return run.Until(() => ChapterRun.Instance == null, 20f, "leave for skip pass");
        yield return Wait(1f);
        yield return EnterChapter(chapter);
        if (run.Done) {
            yield break;
        }
        yield return PressBegin(chapter);
        if (run.Done) {
            yield break;
        }
        yield return run.Until(() => CinematicDirector.IsOpen, 20f, "opening again for skip");
        if (run.Done) {
            yield break;
        }
        yield return Wait(2.2f);
        float skipAt = Time.realtimeSinceStartup;
        if (Phone) {
            TouchControls.BotFinger(9, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), true);
            yield return Wait(0.05f);
            TouchControls.BotFinger(9, Vector2.zero, false);
            yield return Wait(0.3f);
            run.Event("skip-pill", CinematicDirector.Instance != null && CinematicDirector.Instance.SkipPillShown ? "shown" : "missing");
            TouchControls.BotFinger(9, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), true);
            yield return Wait(0.05f);
            TouchControls.BotFinger(9, Vector2.zero, false);
        } else {
            PlayInput.Tap(KeyCode.Escape);
        }
        yield return run.Until(() => !CinematicDirector.IsOpen, 3f, "skip to end the scene");
        float skipSeconds = Time.realtimeSinceStartup - skipAt;
        run.Event("opening-skip", "seconds=" + skipSeconds.ToString("0.000"));
        run.Metric("openingSkipMs", Mathf.RoundToInt(skipSeconds * 1000f));
        if (skipSeconds > 1f) {
            run.Problem(PlaytestSeverity.Bug, "opening-ch23: skip to play took " + skipSeconds.ToString("0.00") + " s (> 1 s)");
        }
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
        if (!lastPos.ContainsKey(key + "|w")) {
            lastPos[key + "|w"] = w;
            lastPos[key + "|s"] = s;
            lastPos[key + "|moved"] = Vector3.zero;
            return;
        }
        float moved = Flat(w - lastPos[key + "|w"]).magnitude + Flat(s - lastPos[key + "|s"]).magnitude;
        lastPos[key + "|w"] = w;
        lastPos[key + "|s"] = s;
        if (moved > 0.35f && lastPos[key + "|moved"].x < 0.5f) {
            lastPos[key + "|moved"] = Vector3.one;
            movingShots++;
            run.Event("opening-move", key + " delta=" + moved.ToString("0.00"));
        }
    }

    static void CheckPhoneFrame() {
        Camera cam = Camera.main;
        if (cam == null || CinematicDirector.Instance == null) {
            return;
        }
        string who = CinematicDirector.Instance.SubtitleSpeaker;
        Transform speaker = null;
        if (who == "S") {
            speaker = Companion.Instance != null ? Companion.Instance.transform
                : (Musician.Instance != null ? Musician.Instance.transform : null);
        } else if (who == "Y") {
            foreach (Enemy e in Enemy.Roster) {
                if (e != null && e.Scripted) {
                    speaker = e.transform;
                    break;
                }
            }
        }
        if (speaker == null && PlayerGrowth.Instance != null) {
            speaker = PlayerGrowth.Instance.transform;
        }
        if (speaker == null) {
            return;
        }
        Vector3 a = cam.WorldToViewportPoint(speaker.position + Vector3.up * 1.3f);
        bool ok = a.z > 0.2f && a.x > 0.05f && a.x < 0.95f && a.y > 0.2f && a.y < 0.92f;
        if (ok) {
            framedOk++;
        }
    }

    static Vector3 Flat(Vector3 v) {
        v.y = 0f;
        return v;
    }
}
