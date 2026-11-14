using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Scenario "hub-two-shot": hub conversations play as acted two-shots (ConversationStage) on a 390x844 phone with
// fingers only. Walks to a lodger with a pending chat, taps Interact, and logs each acceptance criterion.
public static class PlaytestTwoShot {
    static PlaytestRun run;

    public static IEnumerator Run(PlaytestRun current) {
        run = current;
        MetaProgress.EnterSandbox(true);
        CampaignSave.EnterSandbox();
        int wasW = Screen.width, wasH = Screen.height;
        bool wasTouch = UiFit.Touch;
        UiFit.ForceTouch(true);
        Screen.SetResolution(390, 844, false);
        yield return null;
        yield return null;
        ColdOpen.Suppress = true;
        CinematicDirector.SuppressOpening = true;
        ChapterScene.HubNext = true;
        CampaignSave.Slot slot = CampaignSave.Get(0);
        slot.used = true;
        slot.chapter = 1;
        slot.reached = 1;
        slot.cleared = 1;
        slot.chats = 0;
        yield return Playtest.ToMenuPublic();
        if (run.Done) {
            Restore(wasW, wasH, wasTouch);
            yield break;
        }
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.CampaignButton == null) {
            run.Problem(PlaytestSeverity.Bug, "hub-two-shot: no Campaign button");
            run.Finish(PlaytestOutcome.Stuck);
            Restore(wasW, wasH, wasTouch);
            yield break;
        }
        Tap(menu.CampaignButton.gameObject);
        run.Event("tap", "Campaign");
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "case files");
        if (run.Done) {
            Restore(wasW, wasH, wasTouch);
            yield break;
        }
        Tap(CampaignPanel.Instance.SlotButtons[0].gameObject);
        run.Event("tap", "case file 1");
        run.Event("case-file-press", "label=\"" + UiRects.LastPressLabel + "\" answered=" + UiRects.LastPressAnswered
            + " ms=" + UiRects.LastPressMs.ToString("0.0")
            + " scale=" + CampaignPanel.Instance.SlotButtons[0].transform.localScale.x.ToString("0.000")
            + " sub=\"" + CampaignPanel.Instance.SubtitleShown + "\"");
        run.Metric("caseFilePressMs", UiRects.LastPressMs);
        if (!UiRects.LastPressAnswered || UiRects.LastPressMs > HawkesUiRects.VisualMs) {
            run.Problem(PlaytestSeverity.Bug, "hub-two-shot: Case file 1 press was not visually answered within "
                + HawkesUiRects.VisualMs + " ms (answered=" + UiRects.LastPressAnswered + ", ms=" + UiRects.LastPressMs.ToString("0.0") + ")");
        }
        float limit = Time.realtimeSinceStartup + 45f;
        while (Time.realtimeSinceStartup < limit && !(ChapterScene.Instance != null && SceneManager.GetActiveScene().name == CampaignSave.Scene)) {
            yield return null;
        }
        if (ChapterScene.Instance == null) {
            run.Problem(PlaytestSeverity.Bug, "hub-two-shot: Campaign scene did not load");
            run.Finish(PlaytestOutcome.Stuck);
            Restore(wasW, wasH, wasTouch);
            yield break;
        }
        yield return run.Until(() => ChapterScene.Instance.Hub != null && ChapterScene.Instance.Hub.Shown, 25f, "the hub");
        if (run.Done) {
            Restore(wasW, wasH, wasTouch);
            yield break;
        }
        ChapterHub hub = ChapterScene.Instance.Hub;
        run.Scene("Hub two-shot");
        run.Event("phone-viewport", Screen.width + "x" + Screen.height + " touch=" + UiFit.Touch);
        if (Screen.width != 390) {
            run.Event("phone-viewport-soft", "wanted 390x844, got " + Screen.width + "x" + Screen.height);
        }
        float waitArrival = Time.realtimeSinceStartup + 12f;
        while (Time.realtimeSinceStartup < waitArrival && hub.ArrivalShown < 2 && !run.Done) {
            yield return null;
        }
        const int tick = 0;
        if (hub.PendingChatOf(tick) < 0) {
            CampaignSave.Get(0).chats = 0;
            hub.Refresh();
        }
        run.Event("hub-chat-ready", "Tick pending chat index " + hub.PendingChatOf(tick));
        yield return Walk(hub, hub.LodgerAt(tick) + new Vector3(0f, 0f, -1.5f), 25f, () => hub.NearLodger == tick);
        if (run.Done) {
            Restore(wasW, wasH, wasTouch);
            yield break;
        }
        yield return TapTalk(hub);
        float openUntil = Time.realtimeSinceStartup + 8f;
        while (!ConversationStage.IsOpen && Time.realtimeSinceStartup < openUntil && !run.Done) {
            yield return null;
        }
        if (!ConversationStage.IsOpen) {
            run.Problem(PlaytestSeverity.Bug, "hub-two-shot: ConversationStage did not open after talking to Tick");
            run.Finish(PlaytestOutcome.Stuck);
            Restore(wasW, wasH, wasTouch);
            yield break;
        }
        run.Event("two-shot-open", "speakers facing=" + ConversationStage.SpeakersFacing
            + " gestures A=\"" + ConversationStage.GestureA + "\" B=\"" + ConversationStage.GestureB + "\"");
        float sampleUntil = Time.realtimeSinceStartup + 4f;
        float amp = 0f;
        int faceOk = 0;
        int moveOk = 0;
        while (Time.realtimeSinceStartup < sampleUntil && ConversationStage.IsOpen && !run.Done) {
            if (ConversationStage.BothFacesInFrame) {
                faceOk++;
            }
            if (ConversationStage.GestureTravel > 0.01f || ConversationStage.CoinShown) {
                moveOk++;
            }
            amp = Mathf.Max(amp, ConversationStage.AmplitudePeak);
            yield return null;
        }
        run.Event("two-shot-speakers", "facing " + ConversationStage.SpeakersFacing
            + ", both faces in frame samples " + faceOk
            + ", gesture travel " + ConversationStage.GestureTravel.ToString("0.000")
            + " coin=" + ConversationStage.CoinShown
            + ", gestures=\"" + ConversationStage.GestureA + "\" / \"" + ConversationStage.GestureB + "\"");
        run.Metric("twoShotFacing", ConversationStage.SpeakersFacing);
        run.Metric("twoShotFaceFrames", faceOk);
        run.Metric("twoShotGestureTravel", ConversationStage.GestureTravel);
        if (ConversationStage.SpeakersFacing < 2 || faceOk < 1) {
            run.Problem(PlaytestSeverity.Bug, "hub-two-shot: speakers were not both facing/on camera (facing="
                + ConversationStage.SpeakersFacing + ", faceFrames=" + faceOk + ")");
        }
        if (ConversationStage.GestureTravel < 0.01f && !ConversationStage.CoinShown) {
            run.Problem(PlaytestSeverity.Bug, "hub-two-shot: no talk gesture travel while a line played (travel="
                + ConversationStage.GestureTravel.ToString("0.000") + ")");
        }
        run.Event("two-shot-amplitude", "peak " + amp.ToString("0.00") + ", gesture travel "
            + ConversationStage.GestureTravel.ToString("0.000") + ", coin=" + ConversationStage.CoinShown);
        run.Metric("twoShotAmplitudePeak", amp);
        if (amp < 0.04f) {
            run.Problem(PlaytestSeverity.Bug, "hub-two-shot: speaking amplitude stayed near 0 (peak " + amp.ToString("0.00") + ")");
        }
        run.Event("two-shot-portrait", "portraitShown=" + ConversationStage.PortraitShown
            + " portraitBodies=" + ConversationStage.PortraitBodies + " (want 0)");
        run.Metric("twoShotPortraitBodies", ConversationStage.PortraitBodies);
        if (ConversationStage.PortraitBodies > 0 || ConversationStage.PortraitShown) {
            run.Problem(PlaytestSeverity.Bug, "hub-two-shot: portrait shown for a speaker with a 3D body");
        }
        ConversationStage.ProbeChoices("Pay up.", "Noted.");
        yield return null;
        float captionFrac = ConversationStage.CaptionHeightFrac;
        float choicePx = ConversationStage.ChoiceMinHeightPx;
        bool faces = ConversationStage.BothFacesInFrame;
        run.Event("two-shot-phone", "390x844 captionFrac=" + captionFrac.ToString("0.00")
            + " (max 0.20) choicePx=" + choicePx.ToString("0.0")
            + " (min 44) bothFaces=" + faces);
        run.Metric("twoShotCaptionFrac", captionFrac);
        run.Metric("twoShotChoicePx", choicePx);
        run.Metric("twoShotBothFaces", faces ? 1 : 0);
        if (captionFrac > 0.20f + 0.001f) {
            run.Problem(PlaytestSeverity.Bug, "hub-two-shot: caption bar is " + (captionFrac * 100f).ToString("0.0") + "% of height (max 20%)");
        }
        if (choicePx < 44f) {
            run.Problem(PlaytestSeverity.Bug, "hub-two-shot: choice targets are " + choicePx.ToString("0.0") + " px tall (want ≥44)");
        }
        if (!faces) {
            run.Problem(PlaytestSeverity.Bug, "hub-two-shot: both faces not in frame at 390x844");
        }
        float chatUntil = Time.realtimeSinceStartup + 20f;
        while (ConversationStage.IsOpen && Time.realtimeSinceStartup < chatUntil && !run.Done) {
            PlayInput.Tap(KeyCode.E);
            yield return new WaitForSecondsRealtime(0.35f);
        }
        run.Event("two-shot-done", "lines=" + ConversationStage.LinesShown + " open=" + ConversationStage.IsOpen);
        run.Metric("twoShotLines", ConversationStage.LinesShown);
        // Every other lodger with a conversation still to hear: the first frame of each must show both speakers.
        for (int other = 1; other < Story.Lodgers.Length && !run.Done; other++) {
            CampaignSave.Get(0).chats = 0;
            hub.Refresh();
            if (hub.PendingChatOf(other) < 0) {
                run.Event("hub-chat-none", Story.Lodgers[other].Name + " has no conversation to play");
                continue;
            }
            yield return Walk(hub, hub.LodgerAt(other) + new Vector3(0f, 0f, -1.5f), 25f, () => hub.NearLodger == other);
            if (run.Done) {
                break;
            }
            int firstBefore = ConversationStage.FirstFramesFramed;
            int openedBefore = ConversationStage.ConversationsOpened;
            yield return TapTalk(hub);
            float until = Time.realtimeSinceStartup + 8f;
            while (!ConversationStage.IsOpen && Time.realtimeSinceStartup < until && !run.Done) {
                yield return null;
            }
            if (!ConversationStage.IsOpen) {
                run.Problem(PlaytestSeverity.Bug, "hub-two-shot: ConversationStage did not open for " + Story.Lodgers[other].Name);
                continue;
            }
            float end = Time.realtimeSinceStartup + 20f;
            while (ConversationStage.IsOpen && Time.realtimeSinceStartup < end && !run.Done) {
                PlayInput.Tap(KeyCode.E);
                yield return new WaitForSecondsRealtime(0.35f);
            }
            int opened = ConversationStage.ConversationsOpened - openedBefore;
            int framedFirst = ConversationStage.FirstFramesFramed - firstBefore;
            run.Event("two-shot-first-frame", Story.Lodgers[other].Name + ": " + framedFirst + " of " + opened + " opening frames held both speakers");
        }
        // Framing totals across every conversation played (Tick, then the others, Slice's cut-ins included).
        float framedPct = ConversationStage.FramedPct;
        float headPct = ConversationStage.HeadScreenPct;
        float headMin = ConversationStage.HeadScreenPctMin > 99f ? headPct : ConversationStage.HeadScreenPctMin;
        run.Event("talk-framing", "framedPct=" + framedPct.ToString("0.0") + " headScreenPct=" + headPct.ToString("0.0")
            + " (smallest settled " + headMin.ToString("0.0") + ") firstFramesFramed=" + ConversationStage.FirstFramesFramed
            + "/" + ConversationStage.ConversationsOpened + " shotDistance=" + ConversationStage.ShotDistance.ToString("0.0"));
        run.Metric("talk.framedPct", framedPct);
        run.Metric("talk.headScreenPct", headPct);
        run.Metric("talk.headScreenPctMin", headMin);
        run.Metric("talk.firstFramesFramed", ConversationStage.FirstFramesFramed);
        run.Metric("talk.conversations", ConversationStage.ConversationsOpened);
        if (framedPct < 95f) {
            run.Problem(PlaytestSeverity.Bug, "hub-two-shot: both heads on screen in only " + framedPct.ToString("0.0") + "% of conversation frames (want 95)");
        }
        if (ConversationStage.FirstFramesFramed < ConversationStage.ConversationsOpened) {
            run.Problem(PlaytestSeverity.Bug, "hub-two-shot: a conversation opened on a frame without both speakers ("
                + ConversationStage.FirstFramesFramed + "/" + ConversationStage.ConversationsOpened + ")");
        }
        if (headMin < 8f) {
            run.Problem(PlaytestSeverity.Bug, "hub-two-shot: a speaker's head is " + headMin.ToString("0.0") + "% of the screen height (want 8)");
        }
        Restore(wasW, wasH, wasTouch);
        if (!run.Done) {
            run.Finish(PlaytestOutcome.Done);
        }
    }

    static void Restore(int w, int h, bool touch) {
        Screen.SetResolution(w, h, false);
        UiFit.ForceTouch(touch);
        ChapterScene.HubNext = false;
        TouchControls.LiftBotFingers();
        PlayInput.Release();
    }

    static void Tap(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    static IEnumerator Walk(ChapterHub hub, Vector3 target, float seconds, System.Func<bool> stop) {
        float start = Time.realtimeSinceStartup;
        while (!stop() && Time.realtimeSinceStartup - start < seconds && !run.Done) {
            Vector3 to = target - hub.Player.position;
            Vector2 dir = new Vector2(Mathf.Clamp(to.x, -1f, 1f), Mathf.Clamp(to.z, -1f, 1f));
            Vector2 stick;
            if (TouchControls.Spot("Stick", out stick)) {
                TouchControls.BotFinger(0, stick + dir.normalized * TouchControls.StickReach * 0.85f, true);
            } else {
                PlayInput.Axis("Horizontal", dir.x);
                PlayInput.Axis("Vertical", dir.y);
            }
            yield return null;
        }
        TouchControls.LiftBotFingers();
        PlayInput.Release();
    }

    static IEnumerator TapTalk(ChapterHub hub) {
        Vector2 at;
        if (TouchControls.Spot("Interact", out at)) {
            TouchControls.BotFinger(2, at, true);
            yield return new WaitForSecondsRealtime(0.05f);
            TouchControls.BotFinger(2, Vector2.zero, false);
            run.Event("tap", "Interact pad");
        } else {
            PlayInput.Tap(KeyCode.E);
            run.Event("tap", "E (Interact channel)");
        }
        yield return new WaitForSecondsRealtime(0.5f);
        if (!ConversationStage.IsOpen && hub.Talks == 0) {
            PlayInput.Tap(KeyCode.E);
            yield return new WaitForSecondsRealtime(0.4f);
        }
    }
}
