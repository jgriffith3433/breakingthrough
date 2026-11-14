using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The "ui-walk" scenario: every in-game screen opened in turn on a phone-sized viewport (390x844 or 844x390, touch forced), each
// one's controls reported to the Hawkes UI probe by UiRects (ui-layout: small, inset, offscreen) and each control pressed once by
// a finger through the EventSystem (PlaytestUiFinger), so a hit area that is too small or covered is missed and logged as a dead
// press. A press shows its pressed state and plays the menu's click (MenuButtonFx), which the probe reads as answered.
// Screens: Arcade HUD and pads, level-up cards, Pause, Settings, the run-end sheet, the shop, Chapter 1's level HUD, the hub,
// the Journal and the Lore panel. The chapter dossier (ChapterCeremony) needs a finished chapter run and is walked by the
// `ceremony` scenario; UiRects reports it too whenever it is on screen.
// Events: ui-walk (screen, size, controls, issues, presses). Metrics: uiWalkScreens, uiWalkPresses, uiWalkControls.
public static class PlaytestUiWalk {
    static PlaytestRun run;
    static int screens;
    static int presses;
    static int controls;

    static Rect ScreenRect(RectTransform rt) {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    static bool Matches(string label, string pattern) {
        return pattern != null && pattern.Length > 0 && System.Text.RegularExpressions.Regex.IsMatch(label, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    static string LabelOf(Button b) {
        Text t = b.GetComponentInChildren<Text>(true);
        string s = t != null ? t.text.Replace("\n", " ").Trim() : "";
        return s.Length > 0 ? s : b.name;
    }

    // Let UiRects see the screen (it reports once a layout has held for two scans), then press every live button once: the ones
    // matching `skip` never, the ones matching `last` at the end (they close the screen). Stops when the screen closes.
    static IEnumerator Walk(string name, System.Func<bool> open, string skip, string last) {
        UiRects.Rearm();
        int before = UiRects.Reports;
        yield return new WaitForSecondsRealtime(0.6f);
        yield return run.Until(() => UiRects.Reports > before, 5f, "the UI probe's layout for " + name);
        int pressed = 0;
        var first = new List<Button>();
        var final = new List<Button>();
        foreach (Button b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None)) {
            if (!UiRects.IsLive(b) || !b.interactable) {
                continue;
            }
            string label = LabelOf(b);
            if (Matches(label, skip)) {
                continue;
            }
            (Matches(label, last) || b.name.StartsWith("Card") ? final : first).Add(b);
        }
        first.AddRange(final);
        string issues = UiRects.LastJson != null ? UiRects.LastIssues + "" : "?";
        foreach (Button b in first) {
            if (run.Done) {
                yield break;
            }
            if (open != null && !open()) {
                break;
            }
            if (b == null || !UiRects.IsLive(b)) {
                continue;
            }
            Rect r = ScreenRect(b.targetGraphic.rectTransform);
            bool hit = PlaytestUiFinger.Press(r.center);
            if (!hit) {
                run.Event("ui-walk-miss", name + ": a finger at " + r.center + " found nothing for \"" + LabelOf(b) + "\"");
            } else {
                pressed++;
            }
            yield return new WaitForSecondsRealtime(0.4f);
        }
        screens++;
        presses += pressed;
        controls += UiRects.LastControls;
        run.Event("ui-walk", name + " " + UiFit.Mode + " " + Screen.width + "x" + Screen.height + ": " + UiRects.LastControls + " controls reported, " + issues + " issues, " + pressed + " pressed");
        run.Metric("uiWalkScreens", screens);
        run.Metric("uiWalkPresses", presses);
        run.Metric("uiWalkControls", controls);
    }

    // The screen as it stands, with nothing to press (the HUD and the pads, which a finger drives in play).
    static IEnumerator Look(string name) {
        UiRects.Rearm();
        int before = UiRects.Reports;
        yield return run.Until(() => UiRects.Reports > before, 6f, "the UI probe's layout for " + name);
        screens++;
        controls += UiRects.LastControls;
        run.Event("ui-walk", name + " " + UiFit.Mode + " " + Screen.width + "x" + Screen.height + ": " + UiRects.LastControls + " controls reported, " + UiRects.LastIssues + " issues");
        run.Metric("uiWalkScreens", screens);
        run.Metric("uiWalkControls", controls);
    }

    public static IEnumerator Run(PlaytestRun r) {
        run = r;
        screens = 0;
        presses = 0;
        controls = 0;
        UiFit.ForceTouch(true);
        CampaignSave.EnterSandbox();
        run.Log("layout " + UiFit.Mode + " at " + Screen.width + "x" + Screen.height);
        yield return new WaitForSecondsRealtime(1f);

        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.PlayButton == null) {
            run.Problem(PlaytestSeverity.Bug, "ui-walk: no Play button on the menu");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        yield return Walk("menu", () => SceneManager.GetActiveScene().name == MetaProgress.MenuScene, "Play|Campaign|Quit|Erase|Shop|Settings", "");
        menu = Object.FindAnyObjectByType<MainMenu>();
        PlaytestUiFinger.Press(ScreenRect((RectTransform)menu.PlayButton.transform).center);
        yield return run.Until(() => WaveDirector.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the game scene");
        if (run.Done) {
            yield break;
        }
        run.Scene("Arcade");
        yield return new WaitForSecondsRealtime(4f);
        yield return Look("arcade-hud-pads");

        // Level-up cards.
        RunBoons.AddFeathers(RunBoons.Need(RunBoons.Level));
        run.Event("setup", "a level's worth of bolts granted so the level-up cards open");
        yield return run.Until(() => LevelUpScreen.IsOpen, 15f, "the level-up cards");
        if (run.Done) {
            yield break;
        }
        yield return Walk("level-up", () => LevelUpScreen.IsOpen, "", "");
        yield return run.Until(() => !LevelUpScreen.IsOpen, 8f, "the level-up cards to close");

        // Pause, Settings, then Resume.
        PauseMenu.Open();
        yield return run.Until(() => PauseMenu.IsOpen, 5f, "Pause");
        yield return new WaitForSecondsRealtime(0.8f);
        PlaytestUiFinger.Press(ScreenRect((RectTransform)PauseMenu.Instance.SettingsButton.transform).center);
        yield return run.Until(() => SettingsPanel.IsOpen, 5f, "Settings");
        yield return Walk("settings", () => SettingsPanel.IsOpen, "", "Done|Close|Back");
        yield return Walk("pause", () => PauseMenu.IsOpen, "Quit|Restart|Journal|Skip|Hub|Menu", "Resume");

        // The run-end sheet: Slice is struck down (named setup), the sheet's own buttons are pressed except the ones that leave.
        run.Event("setup", "Slice struck down so the run-end sheet opens");
        if (Musician.Instance != null) {
            Musician.Instance.Hurt(Musician.Instance.Health + 50, null);
        }
        yield return run.Until(() => RunEndScreen.IsOpen && RunEndScreen.Instance != null && RunEndScreen.Instance.RetryButton != null && RunEndScreen.Instance.RetryButton.interactable, 30f, "the run-end sheet");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(2f);
        yield return Walk("run-end", () => RunEndScreen.IsOpen, "Retry|Menu|Again|Quit", "");
        PlaytestUiFinger.Press(ScreenRect((RectTransform)RunEndScreen.Instance.MenuButton.transform).center);
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null && Object.FindAnyObjectByType<MainMenu>().CampaignButton != null, 40f, "the menu");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1.5f);

        // The shop (named setup: opened without a finished run).
        menu = Object.FindAnyObjectByType<MainMenu>();
        menu.ShowShopPage(0);
        yield return new WaitForSecondsRealtime(0.5f);
        yield return Walk("shop", null, "Close", "");
        yield return new WaitForSecondsRealtime(0.3f);

        // Campaign: case files, Chapter 1's level, the hub, Journal and Lore.
        menu = Object.FindAnyObjectByType<MainMenu>();
        PlaytestUiFinger.Press(ScreenRect((RectTransform)menu.CampaignButton.transform).center);
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);
        PlaytestUiFinger.Press(ScreenRect((RectTransform)CampaignPanel.Instance.SlotButtons[0].transform).center);
        yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.CardUp && ChapterScene.Instance.BeginButton != null && SceneManager.GetActiveScene().name == CampaignSave.Scene, 45f, "Chapter 1's card");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(3.5f);
        yield return Look("chapter-card");
        PlaytestUiFinger.Press(ScreenRect((RectTransform)ChapterScene.Instance.BeginButton.transform).center);
        yield return run.Until(() => LevelScene.Instance != null && LevelScene.Instance.Yard != null && SceneManager.GetActiveScene().name.StartsWith("Level_"), 45f, "the level scene");
        if (run.Done) {
            yield break;
        }
        run.Scene(SceneManager.GetActiveScene().name);
        yield return new WaitForSecondsRealtime(3f);
        yield return Look("chapter-1-hud-pads");

        run.Event("setup", "Chapter 1 filed in the sandbox file, then the way back to the hub");
        CampaignSave.Complete(0);
        LevelScene.Instance.LeaveToHub();
        yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.Hub != null && ChapterScene.Instance.Hub.Shown && PauseMenu.InHub, 45f, "the hub");
        if (run.Done) {
            yield break;
        }
        run.Scene("Hub");
        yield return new WaitForSecondsRealtime(3f);
        yield return Look("hub-pads");

        JournalPanel.Open(CampaignSave.Active);
        yield return run.Until(() => JournalPanel.IsOpen, 5f, "the Journal");
        yield return Walk("journal", () => JournalPanel.IsOpen, "", "Back|Close");
        yield return new WaitForSecondsRealtime(0.5f);
        LorePanel.Open(CampaignSave.Active);
        yield return run.Until(() => LorePanel.IsOpen, 5f, "the Lore panel");
        yield return Walk("lore", () => LorePanel.IsOpen, "", "Back|Close");

        run.Event("dossier-note", "the chapter dossier (ChapterCeremony) is walked by the ceremony scenario; UiRects reports it whenever it is up");
    }
}
