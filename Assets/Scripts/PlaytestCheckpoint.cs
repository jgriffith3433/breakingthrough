using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The "checkpoint-resume" scenario: Chapter 1's level saves its checkpoint into the case file (LevelCheckpoint) and a reload picks
// it up. The bot plays to the Ring (hears Slice out) and reads two notices, then the page is "reloaded" (the save text is read back
// as a new page would read it and the scene is dropped to the menu), Campaign and the file are tapped, the card must name the
// checkpoint with a Continue button, and one tap resumes in the level with the same beats done. Last, a version 10 file with no
// checkpoint fields must load into its chapter. Taps go through pointer events (the same path a finger takes); the walk is the stick.
public static class PlaytestCheckpoint {
    static PlaytestRun run;

    static void Tap(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    static Vector3 Flat(Vector3 v) {
        v.y = 0f;
        return v;
    }

    static void Steer(LevelScene level, Vector3 target) {
        Vector3 to = Flat(target - level.Player.position);
        if (to.magnitude < 0.05f) {
            PlayInput.Axis("Horizontal", 0f);
            PlayInput.Axis("Vertical", 0f);
            return;
        }
        Vector3 fwd = Flat(level.Cam.transform.forward).normalized;
        Vector3 right = Flat(level.Cam.transform.right).normalized;
        Vector3 d = to.normalized;
        PlayInput.Axis("Horizontal", Mathf.Clamp(Vector3.Dot(d, right) * 1.4f, -1f, 1f));
        PlayInput.Axis("Vertical", Mathf.Clamp(Vector3.Dot(d, fwd) * 1.4f, -1f, 1f));
    }

    static IEnumerator WalkTo(LevelScene level, Vector3 target, float near, float seconds, System.Func<bool> abort = null) {
        float end = Time.realtimeSinceStartup + seconds;
        while (!run.Done && Time.realtimeSinceStartup < end && (abort == null || !abort()) && Flat(target - level.Player.position).magnitude > near) {
            Steer(level, target);
            yield return null;
        }
        PlayInput.Axis("Horizontal", 0f);
        PlayInput.Axis("Vertical", 0f);
    }

    // Presses the use key once its prompt names the verb (E on a keyboard, the Tap pad on a phone presses the same key).
    static IEnumerator Use(LevelScene level, string verb) {
        float end = Time.realtimeSinceStartup + 6f;
        while (!run.Done && Time.realtimeSinceStartup < end) {
            string p = level.Yard.Prompt;
            int c = p.IndexOf(": ");
            if (c >= 0 && p.Substring(c + 2).StartsWith(verb)) {
                break;
            }
            yield return null;
        }
        PlayInput.Tap(KeyCode.E);
        yield return new WaitForSecondsRealtime(0.6f);
    }

    static void Drain(LevelScene level, ref int drained) {
        while (level != null && level.Yard != null && drained < level.Yard.Timeline.Count) {
            string raw = level.Yard.Timeline[drained++];
            int cut = raw.IndexOf('|');
            run.Event(cut > 0 ? raw.Substring(0, cut) : raw, cut > 0 ? raw.Substring(cut + 1) : "");
        }
    }

    static IEnumerator ToCard() {
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.CampaignButton == null) {
            run.Problem(PlaytestSeverity.Bug, "checkpoint-resume: no Campaign button on the main menu");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        Tap(menu.CampaignButton.gameObject);
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);
        Tap(CampaignPanel.Instance.SlotButtons[0].gameObject);
        run.Event("tap", "case file 1 (sandbox)");
        yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.CardUp && ChapterScene.Instance.BeginButton != null && SceneManager.GetActiveScene().name == CampaignSave.Scene, 45f, "the chapter card");
        yield return new WaitForSecondsRealtime(2f);
    }

    public static IEnumerator Run(PlaytestRun r) {
        run = r;
        CampaignSave.EnterSandbox();
        LevelBarks.Reset();
        yield return new WaitForSecondsRealtime(1f);
        yield return ToCard();
        if (run.Done) {
            yield break;
        }
        run.Event("continue-card-fresh", "Begin reads \"" + ChapterScene.Instance.BeginButton.GetComponentInChildren<Text>().text + "\" with no checkpoint (continue line \"" + ChapterScene.ContinueLine + "\")");
        Tap(ChapterScene.Instance.BeginButton.gameObject);
        run.Event("tap", "Begin");
        yield return run.Until(() => LevelScene.Instance != null && LevelScene.Instance.Yard != null && SceneManager.GetActiveScene().name.StartsWith("Level_"), 45f, "the level scene");
        if (run.Done) {
            yield break;
        }
        LevelScene level = LevelScene.Instance;
        int drained = 0;
        run.Scene(SceneManager.GetActiveScene().name);
        yield return new WaitForSecondsRealtime(2f);

        // Hear Slice out, read two notices.
        yield return WalkTo(level, LevelYard.SlicePos + new Vector3(0f, 0f, -1.5f), 0.6f, 60f, () => level.Yard.Prompt.Contains("talk to Slice"));
        yield return Use(level, "talk");
        yield return run.Until(() => level.Yard.Talked, 70f, "the talk with Slice to end");
        Drain(level, ref drained);
        run.Event("talk-done", "Slice joined: " + level.Yard.SliceJoined);
        for (int i = 0; i < 2 && !run.Done; i++) {
            yield return WalkTo(level, LevelYard.NoticePos[i] + new Vector3(2f, 0f, -1.5f), 1.2f, 40f);
            yield return Use(level, "read");
        }
        yield return new WaitForSecondsRealtime(1f);
        Drain(level, ref drained);
        CampaignSave.Slot slot = CampaignSave.Get(0);
        run.Event("checkpoint-reached", "id " + slot.cpId + ", notices " + slot.cpNotices + ", flags " + slot.cpFlags + ", health " + slot.cpHealth + ", chapter " + (slot.cpChapter + 1));
        if (slot.cpId != LevelCheckpoint.Trail || slot.cpNotices != 3 || (slot.cpFlags & LevelCheckpoint.Talked) == 0) {
            run.Problem(PlaytestSeverity.Bug, "checkpoint-resume: after the talk and two notices the file holds checkpoint \"" + slot.cpId + "\" notices " + slot.cpNotices + " flags " + slot.cpFlags);
        }
        Vector3 savedAt = LevelCheckpoint.PositionOf(slot);

        // The reload: the text is read back as a new page would read it, and the scene goes to the menu.
        run.Event("setup", "page reload: the save text is read back from storage and the scene is dropped to the menu");
        string text = CampaignSave.ToText();
        PlayInput.Release();
        Time.timeScale = 1f;
        CampaignSave.EnterSandbox(text);
        SceneManager.LoadScene(MetaProgress.MenuScene);
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the menu after the reload");
        if (run.Done) {
            yield break;
        }
        run.Scene("Main Menu");
        yield return new WaitForSecondsRealtime(1f);
        yield return ToCard();
        if (run.Done) {
            yield break;
        }

        // The card names the checkpoint and Begin reads Continue.
        ChapterScene card = ChapterScene.Instance;
        string begin = card.BeginButton.GetComponentInChildren<Text>().text;
        float unitPx = Screen.height / Mathf.Max(1f, UiFit.Size.y) / Mathf.Max(0.5f, UiFit.PixelRatio);
        float cardScale = card.BeginButton.transform.parent.localScale.x;
        float beginPx = card.BeginButton.GetComponent<RectTransform>().rect.height * cardScale * unitPx;
        run.Event("continue-card", "line \"" + ChapterScene.ContinueLine + "\", button \"" + begin + "\" " + beginPx.ToString("0") + " px tall, screen " + Screen.width + "x" + Screen.height);
        bool cardOk = ChapterScene.ContinueLine.Contains(LevelCheckpoint.NameOf(LevelCheckpoint.Trail)) && begin == "Continue" && beginPx >= 44f;
        run.Metric("continueCardChecks", cardOk ? 1 : 0);
        if (!cardOk) {
            run.Problem(PlaytestSeverity.Bug, "checkpoint-resume: the card says \"" + ChapterScene.ContinueLine + "\" with a \"" + begin + "\" button " + beginPx.ToString("0") + " px tall");
        }
        Tap(card.BeginButton.gameObject);
        run.Event("tap", "Continue (one tap)");
        yield return run.Until(() => LevelScene.Instance != null && LevelScene.Instance.Yard != null && SceneManager.GetActiveScene().name.StartsWith("Level_"), 45f, "the level scene after Continue");
        if (run.Done) {
            yield break;
        }
        level = LevelScene.Instance;
        drained = 0;
        run.Scene(SceneManager.GetActiveScene().name);
        yield return new WaitForSecondsRealtime(2f);
        Drain(level, ref drained);
        float off = Flat(level.Player.position - savedAt).magnitude;
        bool resumed = level.Resumed && level.Yard.Restored && level.Yard.Talked && level.Yard.NoticesRead == 2 && level.Yard.SliceJoined && !level.Yard.FlagTaken && off < 4f;
        run.Event("checkpoint-resumed", "resumed " + level.Resumed + ", talked " + level.Yard.Talked + ", notices " + level.Yard.NoticesRead + ", Slice with him " + level.Yard.SliceJoined + ", " + off.ToString("0.0") + " m from the checkpoint, health " + level.Health);
        run.Metric("checkpointResumed", resumed ? 1 : 0);
        run.Metric("goalsKept", level.Yard.NoticesRead + (level.Yard.Talked ? 1 : 0));
        if (!resumed) {
            run.Problem(PlaytestSeverity.Bug, "checkpoint-resume: Continue did not put the level back as the checkpoint left it");
        }
        // The same notice is not offered again; the next one is.
        yield return WalkTo(level, LevelYard.NoticePos[2] + new Vector3(2f, 0f, -1.5f), 1.2f, 40f);
        yield return Use(level, "read");
        run.Event("after-resume", "third notice read: " + level.Yard.NoticesRead + " of 3");
        if (level.Yard.NoticesRead != 3) {
            run.Problem(PlaytestSeverity.Bug, "checkpoint-resume: after resuming, the third notice could not be read (" + level.Yard.NoticesRead + " read)");
        }

        // A file saved before this change: version 10, none of the checkpoint fields.
        string old = "{\"version\":10,\"last\":0,\"slots\":[{\"used\":true,\"chapter\":0,\"reached\":0,\"cleared\":0,\"xp\":40,\"seconds\":120.0},{},{}]}";
        PlayInput.Release();
        CampaignSave.EnterSandbox(old);
        CampaignSave.Slot oldSlot = CampaignSave.Get(0);
        bool oldOk = oldSlot.used && oldSlot.chapter == 0 && oldSlot.xp == 40 && !LevelCheckpoint.Has(oldSlot, 0) && CampaignSave.LoadResult == "migrated";
        run.Event("old-save-loads", "version " + CampaignSave.LoadedVersion + " file: used " + oldSlot.used + ", chapter " + (oldSlot.chapter + 1) + ", xp " + oldSlot.xp + ", checkpoint \"" + oldSlot.cpId + "\", result " + CampaignSave.LoadResult);
        run.Metric("oldSaveLoads", oldOk ? 1 : 0);
        if (!oldOk) {
            run.Problem(PlaytestSeverity.Bug, "checkpoint-resume: a version 10 file did not load into its chapter");
        }
    }
}
