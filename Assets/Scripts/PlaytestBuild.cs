using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Scenario "build-panel": the build on Pause and on the run-end sheet, by finger on a phone (390x844 touch). Arcade, the wave
// key to wave 10 (real card picks on the way), then setup tops the build up to eight boons so the list has to scroll.
// Pause is tapped, Build opens: one row per boon held (name, level, effect), each row 44 px or more, the effect text 14 px or
// more, a finger drag scrolls it, Back returns to Pause, Resume plays on. Then Slice falls (setup: DevLose) and the run-end
// sheet shows the same build as an icon band, with the run-end layout check (endOverlaps) at 0.
// Events: build-setup, build-pause, build-panel, build-scroll, build-closed, build-run-end. Metrics buildBoonsHeld, buildRows,
// buildRowPx, buildTextPx, buildScrolled, buildRunEndShown, endOverlaps.
public static class PlaytestBuild {
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
        yield return run.Until(() => WaveDirector.Instance != null && !WaveDirector.IsLost, 30f, "Arcade run ready");
        if (run.Done) {
            yield break;
        }
        // A 10-wave run: the wave key, taking card 1 whenever a level-up opens.
        float until = Time.realtimeSinceStartup + 90f;
        while (WaveDirector.Wave < 10 && Time.realtimeSinceStartup < until && !run.Done) {
            PlayInput.Tap(KeyCode.Equals);
            yield return new WaitForSecondsRealtime(0.15f);
            if (LevelUpScreen.IsOpen) {
                PlayInput.Tap(KeyCode.Alpha1);
                yield return new WaitForSecondsRealtime(0.25f);
            }
        }
        run.Event("setup", "wave key to " + WaveDirector.Wave);
        // Setup: enough boons that the list must scroll (more than six).
        BoonId[] extra = { BoonId.Heavy, BoonId.QuickDraw, BoonId.Fleet, BoonId.Magnet, BoonId.Mark, BoonId.Ovation, BoonId.Bounty, BoonId.EagleEye };
        for (int i = 0; i < extra.Length && BuildList.Held().Count < 8; i++) {
            RunBoons.Take(extra[i]);
        }
        int held = BuildList.Held().Count;
        run.Event("build-setup", held + " boons held: " + RunBoons.Summary());
        run.Metric("buildBoonsHeld", held);
        yield return run.Until(() => !LevelUpScreen.IsOpen && PauseMenu.Instance != null && PauseMenu.Instance.PauseButton.isActiveAndEnabled, 20f, "Pause button");
        if (run.Done) {
            yield break;
        }

        PlaytestRunEnd.TapButton(PauseMenu.Instance.PauseButton.gameObject);
        yield return run.Until(() => PauseMenu.IsOpen, 5f, "Pause to open");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(0.7f);
        Button open = PauseMenu.Instance.BuildButton;
        run.Event("build-pause", "Build button " + (open != null && open.isActiveAndEnabled ? "shown" : "MISSING"));
        if (open == null || !open.isActiveAndEnabled) {
            run.Problem(PlaytestSeverity.Bug, "build-panel: Pause has no Build button with " + held + " boons held");
            yield break;
        }
        PlaytestRunEnd.TapButton(open.gameObject);
        yield return run.Until(() => BuildPanel.IsOpen, 5f, "Build panel to open");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(0.5f);
        BuildPanel panel = BuildPanel.Instance;
        int rows = panel.RowCount;
        float px = panel.RowPx;
        float textPx = 24f * px / 108f;
        bool scrolls = panel.CanScroll;
        run.Event("build-panel", rows + " rows (" + held + " held), row " + Mathf.Round(px) + " px, effect text " + Mathf.Round(textPx) + " px, scrolls " + scrolls
            + "; first: " + (panel.RowTexts.Count > 0 ? panel.RowTexts[0] : ""));
        run.Metric("buildRows", rows);
        run.Metric("buildRowPx", Mathf.Round(px));
        run.Metric("buildTextPx", Mathf.Round(textPx));
        if (rows != held) {
            run.Problem(PlaytestSeverity.Bug, "build-panel: " + rows + " rows for " + held + " boons held");
        }
        if (px < 44f) {
            run.Problem(PlaytestSeverity.Bug, "build-panel: rows are " + Mathf.Round(px) + " px tall (want 44 or more)");
        }
        if (textPx < 14f) {
            run.Problem(PlaytestSeverity.Bug, "build-panel: effect text is " + Mathf.Round(textPx) + " px (want 14 or more)");
        }
        if (held > 6 && !scrolls) {
            run.Problem(PlaytestSeverity.Bug, "build-panel: " + held + " boons but the list does not scroll");
        }
        // A finger drag up the list.
        float before = panel.Scroll.verticalNormalizedPosition;
        Rect view = ScreenBox(panel.Viewport);
        Vector2 from = new Vector2(view.center.x, view.yMin + view.height * 0.8f);
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = from, pressPosition = from };
        ExecuteEvents.Execute(panel.Scroll.gameObject, data, ExecuteEvents.initializePotentialDrag);
        ExecuteEvents.Execute(panel.Scroll.gameObject, data, ExecuteEvents.beginDragHandler);
        for (int i = 1; i <= 8; i++) {
            data.position = from + new Vector2(0f, view.height * 0.6f * i / 8f);
            data.delta = new Vector2(0f, view.height * 0.6f / 8f);
            ExecuteEvents.Execute(panel.Scroll.gameObject, data, ExecuteEvents.dragHandler);
            yield return null;
        }
        ExecuteEvents.Execute(panel.Scroll.gameObject, data, ExecuteEvents.endDragHandler);
        yield return new WaitForSecondsRealtime(0.4f);
        float after = panel.Scroll.verticalNormalizedPosition;
        run.Event("build-scroll", "scroll " + before.ToString("0.00") + " -> " + after.ToString("0.00"));
        run.Metric("buildScrolled", after < before - 0.05f ? 1 : 0);
        if (scrolls && after >= before - 0.05f) {
            run.Problem(PlaytestSeverity.Bug, "build-panel: a finger drag did not scroll the list");
        }
        PlaytestRunEnd.TapButton(panel.BackButton.gameObject);
        yield return run.Until(() => !BuildPanel.IsOpen, 5f, "Build panel to close");
        if (run.Done) {
            yield break;
        }
        if (!PauseMenu.IsOpen) {
            run.Problem(PlaytestSeverity.Bug, "build-panel: Back also closed Pause");
        }
        run.Event("build-closed", "back on Pause: " + PauseMenu.IsOpen);
        PlaytestRunEnd.TapButton(PauseMenu.Instance.ResumeButton.gameObject);
        yield return new WaitForSecondsRealtime(0.6f);

        // The run-end sheet.
        run.Event("setup", "Slice falls (WaveDirector.DevLose)");
        WaveDirector.Instance.DevLose();
        yield return run.Until(() => RunEndScreen.IsOpen, 8f, "run-end sheet");
        if (run.Done) {
            yield break;
        }
        yield return run.Until(() => RunEndScreen.CountingDone, 12f, "counting done");
        yield return run.Until(() => RunEndScreen.Instance != null && RunEndScreen.Instance.RetryButton != null
            && RunEndScreen.Instance.RetryButton.interactable, 16f, "sheet ready");
        if (run.Done) {
            yield break;
        }
        run.Event("build-run-end", RunEndScreen.BuildShown + " of " + RunEndScreen.BuildHeld + " boons shown in the band");
        run.Metric("buildRunEndShown", RunEndScreen.BuildShown);
        if (RunEndScreen.BuildShown < Mathf.Min(RunEndScreen.BuildHeld, 6)) {
            run.Problem(PlaytestSeverity.Bug, "build-panel: the run-end sheet shows only " + RunEndScreen.BuildShown + " of " + RunEndScreen.BuildHeld + " boons");
        }
        PlaytestRunEnd.CheckPhoneLayout(run);
        UiFit.ForceTouch(false);
        if (!run.Done) {
            run.Finish(PlaytestOutcome.Done);
        }
    }

    static Rect ScreenBox(RectTransform rt) {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }
}
