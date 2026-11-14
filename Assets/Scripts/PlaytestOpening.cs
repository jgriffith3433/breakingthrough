using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// opening-scene: Chapter 1's in-engine opening (CinematicDirector, ch1_open_world). Watches the directed scene for
// moving shots and voiced lines, then re-enters and skips mid-scene. On a 390x844 phone viewport it uses taps only
// (no mouse or keyboard). Acceptance criteria from the brief are logged as events and fail as bugs when missed.
public static class PlaytestOpening {
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

    static IEnumerator EnterChapter1() {
        CampaignSave.EnterSandbox();
        yield return Wait(1f);
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.CampaignButton == null) {
            run.Problem(PlaytestSeverity.Bug, "opening-scene: no Campaign button");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        run.Event("mode-select", "Campaign");
        if (Phone) {
            yield return PlaytestUiFinger.Tap(RectScreen(menu.CampaignButton.GetComponent<RectTransform>()), "PlaytestOpening");
        } else {
            Tap(menu.CampaignButton.gameObject);
        }
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files");
        if (run.Done) {
            yield break;
        }
        yield return Wait(1f);
        Button slot = CampaignPanel.Instance.SlotButtons[0];
        if (Phone) {
            yield return PlaytestUiFinger.Tap(RectScreen(slot.GetComponent<RectTransform>()), "PlaytestOpening");
        } else {
            Tap(slot.gameObject);
        }
        run.Event("tap", "case file 1");
        yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.CardUp && ChapterScene.Instance.BeginButton != null, 45f, "Chapter 1's card");
        if (run.Done) {
            yield break;
        }
        yield return Wait(2f);
        if (Phone) {
            yield return PlaytestUiFinger.Tap(RectScreen(ChapterScene.Instance.BeginButton.GetComponent<RectTransform>()), "PlaytestOpening");
        } else {
            Tap(ChapterScene.Instance.BeginButton.gameObject);
        }
        run.Event("tap", "Begin");
        yield return run.Until(() => LevelScene.Instance != null && LevelScene.Instance.Yard != null, 45f, "Level_1_Yard");
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

    static int CountPrefix(string prefix) {
        int n = 0;
        foreach (string e in CinematicDirector.Events) {
            if (e.StartsWith(prefix)) {
                n++;
            }
        }
        return n;
    }

    public static IEnumerator Run(PlaytestRun r) {
        run = r;
        CinematicDirector.SuppressOpening = false;
        CinematicDirector.Events.Clear();
        run.Event("setup", "opening-scene: Chapter 1 in-engine cinematic; SuppressOpening=false; touch=" + UiFit.Touch + " size=" + Screen.width + "x" + Screen.height);
        yield return EnterChapter1();
        if (run.Done) {
            yield break;
        }
        run.Scene(SceneManager.GetActiveScene().name);
        run.Event("level-scene", SceneManager.GetActiveScene().name);
        yield return run.Until(() => CinematicDirector.IsOpen || Seen("fallback"), 12f, "the opening scene to start");
        if (run.Done) {
            yield break;
        }
        if (Seen("fallback")) {
            run.Problem(PlaytestSeverity.Bug, "opening-scene: fell back to still reel (an actor was missing)");
        }
        run.Event("opening-start", (CinematicDirector.Instance != null ? CinematicDirector.Instance.ReelId : "") + " open=" + CinematicDirector.IsOpen);

        // Watch the whole scene: sample each shot for actor motion and captions.
        int movingShots = 0;
        int framedOk = 0;
        var lastPos = new Dictionary<string, Vector3>();
        float started = Time.realtimeSinceStartup;
        int lastShot = -1;
        while (!run.Done && CinematicDirector.IsOpen && Time.realtimeSinceStartup - started < 55f) {
            int idx = CinematicDirector.Instance != null ? CinematicDirector.Instance.ShotIndex : -1;
            if (idx != lastShot && idx >= 0) {
                lastShot = idx;
                run.Event("opening-shot", CinematicDirector.Instance.ShotId + " #" + idx + " caption=\"" + CinematicDirector.Instance.Subtitle + "\"");
                run.Progress(0.15f + 0.6f * (idx + 1) / Mathf.Max(1, CinematicDirector.Instance.ShotCount));
                lastPos.Clear();
            }
            SampleMotion(ref movingShots, lastPos);
            if (Phone) {
                CheckPhoneFrame(ref framedOk);
            }
            // Captions above the bottom 15%.
            if (CinematicDirector.CaptionBottom01 + 0.001f < 0.15f && !string.IsNullOrEmpty(CinematicDirector.Instance != null ? CinematicDirector.Instance.Subtitle : null)) {
                run.Problem(PlaytestSeverity.Bug, "opening-scene: caption bottom " + CinematicDirector.CaptionBottom01.ToString("0.00") + " is under the bottom 15%");
            }
            yield return null;
        }
        float watchSeconds = Time.realtimeSinceStartup - started;
        run.Event("opening-end", "shotsShown=" + CinematicDirector.ShotsShown + " voice=" + CinematicDirector.VoiceLines + " moving=" + movingShots + " handoff=" + CinematicDirector.LastHandoffSeconds.ToString("0.000") + "s watch=" + watchSeconds.ToString("0.0"));
        run.Metric("openingShots", CinematicDirector.ShotsShown);
        run.Metric("openingVoiceLines", CinematicDirector.VoiceLines);
        run.Metric("openingMovingShots", movingShots);
        run.Metric("openingHandoffMs", Mathf.RoundToInt(CinematicDirector.LastHandoffSeconds * 1000f));
        run.Metric("openingCaptionBottom01", CinematicDirector.CaptionBottom01);

        if (CinematicDirector.ShotsShown < 3) {
            run.Problem(PlaytestSeverity.Bug, "opening-scene: fewer than 3 shots played (" + CinematicDirector.ShotsShown + ")");
        }
        if (movingShots < 3) {
            run.Problem(PlaytestSeverity.Bug, "opening-scene: fewer than 3 shots with visible actor motion (" + movingShots + ")");
        }
        if (CinematicDirector.VoiceLines < 1) {
            run.Problem(PlaytestSeverity.Bug, "opening-scene: expected at least 1 voiced line, got " + CinematicDirector.VoiceLines);
        }
        var lines = new HashSet<string>();
        int overlaps = 0;
        foreach (string e in CinematicDirector.Events) {
            if (!e.StartsWith("voice|")) {
                continue;
            }
            string line = e.Substring(e.IndexOf('|', 6) + 1);
            if (!lines.Add(line)) {
                run.Problem(PlaytestSeverity.Bug, "opening-scene: voiced line repeated: " + line);
            }
        }
        if (WallitVoice.Overlaps > 0 || overlaps > 0) {
            run.Problem(PlaytestSeverity.Bug, "opening-scene: voices overlapped (" + WallitVoice.Overlaps + ")");
        }
        run.Metric("openingUniqueVoiceLines", lines.Count);
        if (CinematicDirector.LastHandoffSeconds > 1f) {
            run.Problem(PlaytestSeverity.Bug, "opening-scene: handoff to play took " + CinematicDirector.LastHandoffSeconds.ToString("0.00") + " s (> 1 s)");
        }
        // No long black: handoff itself is the blend; report if control was blocked more than 0.3 s after close.
        float after = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - after < 0.35f) {
            if (CinematicDirector.IsOpen) {
                run.Problem(PlaytestSeverity.Bug, "opening-scene: cinematic still open more than 0.3 s after end");
                break;
            }
            yield return null;
        }
        // The fix: a swing, a hit and a kill inside 10 s of the opening's start.
        if (LevelScene.Instance != null && LevelScene.Instance.Yard != null) {
            LevelYard yard = LevelScene.Instance.Yard;
            int hitsBefore = BatEquipment.Impacts;
            yield return run.Until(() => yard.FirstBotDown || yard.FirstBotDownSeconds > 0f, 16f, "the first Yo Bot to fall");
            float toKill = watchSeconds + yard.FirstBotDownSeconds;
            run.Event("first-kill", "down=" + yard.FirstBotDown + " opening=" + watchSeconds.ToString("0.0") + "s + fight=" + yard.FirstBotDownSeconds.ToString("0.0") + "s = " + toKill.ToString("0.0") + "s, bat impacts=" + (BatEquipment.Impacts - hitsBefore));
            run.Metric("openingToFirstKillSeconds", toKill);
            if (!yard.FirstBotDown) {
                run.Problem(PlaytestSeverity.Bug, "opening-scene: the first Yo Bot was not killed");
            } else if (toKill > 11f) {
                run.Problem(PlaytestSeverity.Bug, "opening-scene: first kill came at " + toKill.ToString("0.0") + " s (> 10 s)");
            }
        }
        run.Event("opening-control", "Cutscene.IsOpen=" + Cutscene.IsOpen + " player at " + (LevelScene.Instance != null ? LevelScene.Instance.Player.position.ToString("0.0") : "?"));

        // Pass 2: re-enter and skip mid-scene (Esc or tap Skip pill).
        run.Event("setup", "re-enter Chapter 1 and skip the opening mid-scene");
        if (LevelScene.Instance != null) {
            LevelScene.Instance.LeaveToHub();
        }
        yield return run.Until(() => ChapterScene.Instance != null || Object.FindAnyObjectByType<MainMenu>() != null, 20f, "back to hub or menu");
        yield return Wait(1.5f);
        // Fresh sandbox chapter begin.
        if (ChapterScene.Instance != null && ChapterScene.Instance.BeginButton != null) {
            Tap(ChapterScene.Instance.BeginButton.gameObject);
        } else {
            yield return EnterChapter1();
            if (run.Done) {
                yield break;
            }
        }
        yield return run.Until(() => CinematicDirector.IsOpen, 20f, "opening again");
        if (run.Done) {
            yield break;
        }
        yield return Wait(2.5f);
        float skipAt = Time.realtimeSinceStartup;
        if (Phone) {
            // The pill is up with no tap; one tap anywhere (after 1 s) skips.
            run.Event("skip-pill", CinematicDirector.Instance != null && CinematicDirector.Instance.SkipPillShown ? "shown" : "missing");
            if (CinematicDirector.Instance == null || !CinematicDirector.Instance.SkipPillShown) {
                run.Problem(PlaytestSeverity.Bug, "opening-scene: Skip pill not shown on phone without a tap");
            }
            TouchControls.BotFinger(9, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), true);
            yield return Wait(0.05f);
            TouchControls.BotFinger(9, Vector2.zero, false);
        } else {
            PlayInput.Tap(KeyCode.Escape);
        }
        yield return run.Until(() => !CinematicDirector.IsOpen, 3f, "skip to end the scene");
        float skipSeconds = Time.realtimeSinceStartup - skipAt;
        run.Event("opening-skip", "seconds=" + skipSeconds.ToString("0.000") + " handoff=" + CinematicDirector.LastHandoffSeconds.ToString("0.000"));
        run.Metric("openingSkipMs", Mathf.RoundToInt(skipSeconds * 1000f));
        if (skipSeconds > 1f) {
            run.Problem(PlaytestSeverity.Bug, "opening-scene: skip to play took " + skipSeconds.ToString("0.00") + " s (> 1 s)");
        }
        if (Phone && framedOk < 3) {
            run.Problem(PlaytestSeverity.Warning, "opening-scene: few phone frames kept both speakers in shot (" + framedOk + ")");
        }
        run.Metric("openingPhoneFramesOk", framedOk);
        run.Progress(1f);
        if (!run.Done) {
            run.Finish(PlaytestOutcome.Done);
        }
    }

    static void SampleMotion(ref int movingShots, Dictionary<string, Vector3> lastPos) {
        LevelScene level = LevelScene.Instance;
        if (level == null || CinematicDirector.Instance == null) {
            return;
        }
        string key = CinematicDirector.Instance.ShotId;
        if (string.IsNullOrEmpty(key)) {
            return;
        }
        Vector3 w = level.Player != null ? level.Player.position : Vector3.zero;
        Vector3 s = level.Yard != null && level.Yard.SliceBody != null ? level.Yard.SliceBody.position : Vector3.zero;
        Enemy courier = null;
        foreach (Enemy e in Enemy.Roster) {
            if (e != null && e.Scripted) {
                courier = e;
                break;
            }
        }
        Vector3 c = courier != null ? courier.transform.position : Vector3.zero;
        if (!lastPos.ContainsKey(key + "|w")) {
            lastPos[key + "|w"] = w;
            lastPos[key + "|s"] = s;
            lastPos[key + "|c"] = c;
            lastPos[key + "|moved"] = Vector3.zero;
            return;
        }
        float moved = 0f;
        moved += Flat(w - lastPos[key + "|w"]).magnitude;
        moved += Flat(s - lastPos[key + "|s"]).magnitude;
        moved += Flat(c - lastPos[key + "|c"]).magnitude;
        lastPos[key + "|w"] = w;
        lastPos[key + "|s"] = s;
        lastPos[key + "|c"] = c;
        if (moved > 0.35f && lastPos[key + "|moved"].x < 0.5f) {
            lastPos[key + "|moved"] = Vector3.one;
            movingShots++;
            run.Event("opening-move", key + " delta=" + moved.ToString("0.00"));
        }
    }

    static void CheckPhoneFrame(ref int framedOk) {
        LevelScene level = LevelScene.Instance;
        Camera cam = level != null ? level.Cam : Camera.main;
        if (cam == null || level == null || level.Yard == null || level.Yard.SliceBody == null || level.Player == null) {
            return;
        }
        Vector3 a = cam.WorldToViewportPoint(level.Player.position + Vector3.up * 1.2f);
        Vector3 b = cam.WorldToViewportPoint(level.Yard.SliceBody.position + Vector3.up * 1.4f);
        bool ok = a.z > 0.2f && b.z > 0.2f && a.x > 0.05f && a.x < 0.95f && b.x > 0.05f && b.x < 0.95f
            && a.y > 0.2f && a.y < 0.92f && b.y > 0.2f && b.y < 0.92f;
        if (ok) {
            framedOk++;
        }
    }

    static Vector3 Flat(Vector3 v) {
        v.y = 0f;
        return v;
    }
}
