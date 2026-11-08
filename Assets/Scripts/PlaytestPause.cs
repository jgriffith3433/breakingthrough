using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The "touch-pause" scenario: Pause and Settings by finger on a phone (mobile viewport, or 844x390; touch is forced). In an
// Arcade run, then in Chapter 1's level, then in the hub: the Pause button is tapped (it must be on screen and inside the
// safe area), Pause opens, Settings opens from it, sound is switched and a slider set, Done closes Settings back to Pause, and
// Resume returns to play. In each place the pause deck and the Settings panel must fit the screen with no scrolling, in the
// orientation it runs in and in the other one. Events: pause-button, pause-opened, settings-opened, setting-changed,
// pause-fits, settings-fits, resumed. Metric touchPauseCycles counts the cycles that completed.
public static class PlaytestPause {
    static PlaytestRun run;
    static int cycles;

    static void Tap(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    static Rect Screen(RectTransform rect) {
        var c = new Vector3[4];
        rect.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    static bool Inside(Rect r, Rect area) {
        return r.xMin >= area.xMin - 1.5f && r.xMax <= area.xMax + 1.5f && r.yMin >= area.yMin - 1.5f && r.yMax <= area.yMax + 1.5f;
    }

    static void Slide(Slider slider, float at) {
        Rect track = Screen(slider.handleRect != null ? (RectTransform)slider.handleRect.parent : (RectTransform)slider.transform);
        var data = new PointerEventData(EventSystem.current) {
            button = PointerEventData.InputButton.Left,
            position = new Vector2(Mathf.Lerp(track.xMin, track.xMax, at), track.center.y),
        };
        ExecuteEvents.Execute(slider.gameObject, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(slider.gameObject, data, ExecuteEvents.pointerUpHandler);
    }

    // Does the open pause deck or Settings panel (and every button on it) sit inside the safe area?
    static void Fits(string kind, string where, RectTransform panel) {
        Rect safe = UnityEngine.Screen.safeArea;
        Rect box = Screen(panel);
        bool ok = Inside(box, safe);
        int out_ = 0;
        foreach (Button b in panel.GetComponentsInChildren<Button>()) {
            if (b.isActiveAndEnabled && !Inside(Screen((RectTransform)b.transform), safe)) {
                out_++;
            }
        }
        foreach (Slider s in panel.GetComponentsInChildren<Slider>()) {
            if (s.isActiveAndEnabled && !Inside(Screen((RectTransform)s.transform), safe)) {
                out_++;
            }
        }
        run.Event(kind + "-fits", where + " " + UiFit.Mode + " " + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height + ": panel " + (ok ? "inside" : "OUTSIDE") + " the safe area, " + out_ + " controls outside; no scrolling");
        if (!ok || out_ > 0) {
            run.Problem(PlaytestSeverity.Bug, "touch-pause: the " + kind + " screen does not fit in " + where + " (" + UiFit.Mode + ", panel " + ok + ", " + out_ + " controls outside)");
        }
    }

    // Pause button, Pause, Settings, a change, Done, Resume. Returns true if all of it worked.
    static IEnumerator Cycle(string where, bool turn) {
        PauseMenu pause = PauseMenu.Instance;
        yield return run.Until(() => PauseMenu.Instance != null && PauseMenu.Instance.PauseButton != null && PauseMenu.Instance.PauseButton.isActiveAndEnabled, 20f, "the Pause button in " + where);
        if (run.Done) {
            yield break;
        }
        pause = PauseMenu.Instance;
        yield return null;
        RectTransform pr = (RectTransform)pause.PauseButton.transform;
        Rect box = Screen(pr);
        float px = UiFit.PixelRatio;
        bool inSafe = Inside(box, UnityEngine.Screen.safeArea);
        float css = Mathf.Min(box.width, box.height) / px;
        run.Event("pause-button", where + ": on screen " + inSafe + ", " + Mathf.Round(css) + " css px across");
        if (!inSafe) {
            run.Problem(PlaytestSeverity.Bug, "touch-pause: the Pause button is not inside the safe area in " + where);
        }
        if (css < 44f) {
            run.Problem(PlaytestSeverity.Bug, "touch-pause: the Pause button is only " + Mathf.Round(css) + " px across in " + where + " (want 44 or more)");
        }
        Tap(pause.PauseButton.gameObject);
        yield return run.Until(() => PauseMenu.IsOpen, 5f, "Pause to open in " + where);
        if (run.Done) {
            yield break;
        }
        run.Event("pause-opened", where);
        yield return new WaitForSecondsRealtime(0.8f);
        if (Time.timeScale > 0.01f) {
            run.Problem(PlaytestSeverity.Bug, "touch-pause: the game kept running behind Pause in " + where);
        }
        Fits("pause", where, pause.Deck);
        Tap(pause.SettingsButton.gameObject);
        yield return run.Until(() => SettingsPanel.IsOpen, 5f, "Settings to open in " + where);
        if (run.Done) {
            yield break;
        }
        run.Event("settings-opened", where);
        yield return new WaitForSecondsRealtime(0.8f);
        Fits("settings", where, SettingsPanel.Instance.Panel);

        if (turn && UnityEngine.Screen.width != UnityEngine.Screen.height) {
            // The same two screens with the phone turned, then back.
            int w = UnityEngine.Screen.width;
            int h = UnityEngine.Screen.height;
            UnityEngine.Screen.SetResolution(h, w, false);
            yield return new WaitForSecondsRealtime(1.5f);
            if (UnityEngine.Screen.width == h) {
                Fits("settings", where + "-turned", SettingsPanel.Instance.Panel);
                Tap(SettingsPanel.Instance.DoneButton.gameObject);
                yield return new WaitForSecondsRealtime(0.4f);
                Fits("pause", where + "-turned", pause.Deck);
                Tap(pause.SettingsButton.gameObject);
                yield return new WaitForSecondsRealtime(0.4f);
            } else {
                run.Event("screen-turn-skipped", "the screen would not turn at " + where);
            }
            UnityEngine.Screen.SetResolution(w, h, false);
            yield return new WaitForSecondsRealtime(1.5f);
        }

        SettingsPanel panel = SettingsPanel.Instance;
        bool was = SoundSettings.On;
        Tap(panel.SoundButton.gameObject);
        yield return new WaitForSecondsRealtime(0.5f);
        run.Event("setting-changed", where + ": sound " + (SoundSettings.On ? "on" : "off") + " (was " + (was ? "on" : "off") + ")");
        if (SoundSettings.On == was) {
            run.Problem(PlaytestSeverity.Bug, "touch-pause: the Sound button did not switch sound in " + where);
        }
        Tap(panel.SoundButton.gameObject);
        Slide(panel.MusicSlider, 0.4f);
        yield return new WaitForSecondsRealtime(0.5f);
        run.Event("setting-changed", where + ": music " + Mathf.RoundToInt(SoundSettings.Music * 100f) + "%");
        if (Mathf.Abs(SoundSettings.Music - 0.4f) > 0.1f) {
            run.Problem(PlaytestSeverity.Bug, "touch-pause: the music slider set " + SoundSettings.Music.ToString("0.00") + " for a touch at 40% in " + where);
        }
        Tap(panel.DoneButton.gameObject);
        yield return run.Until(() => !SettingsPanel.IsOpen, 5f, "Settings to close in " + where);
        if (run.Done) {
            yield break;
        }
        if (!PauseMenu.IsOpen) {
            run.Problem(PlaytestSeverity.Bug, "touch-pause: closing Settings also closed Pause in " + where);
            yield break;
        }
        Tap(pause.ResumeButton.gameObject);
        yield return new WaitForSecondsRealtime(0.6f);
        if (PauseMenu.IsOpen || Time.timeScale <= 0.01f) {
            run.Problem(PlaytestSeverity.Bug, "touch-pause: the game stayed paused after Resume in " + where);
            yield break;
        }
        run.Event("resumed", where);
        cycles++;
        run.Metric("touchPauseCycles", cycles);
    }

    public static IEnumerator Run(PlaytestRun r) {
        run = r;
        cycles = 0;
        UiFit.ForceTouch(true);
        CampaignSave.EnterSandbox();
        run.Log("layout " + UiFit.Mode + " at " + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height);
        yield return new WaitForSecondsRealtime(1f);

        // Arcade.
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.PlayButton == null) {
            run.Problem(PlaytestSeverity.Bug, "touch-pause: no Play button on the menu");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        Tap(menu.PlayButton.gameObject);
        yield return run.Until(() => WaveDirector.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the game scene");
        if (run.Done) {
            yield break;
        }
        run.Scene("Arcade");
        yield return new WaitForSecondsRealtime(4f);
        yield return Cycle("arcade", true);
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);

        // Back to the menu through Pause's own Quit (asked twice), then Campaign, Chapter 1's level.
        Tap(PauseMenu.Instance.PauseButton.gameObject);
        yield return run.Until(() => PauseMenu.IsOpen, 5f, "Pause before Quit");
        if (run.Done) {
            yield break;
        }
        Tap(PauseMenu.Instance.QuitButton.gameObject);
        yield return null;
        Tap(PauseMenu.Instance.QuitButton.gameObject);
        run.Event("tap", "Quit to menu (asked twice)");
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null && Object.FindAnyObjectByType<MainMenu>().CampaignButton != null, 30f, "the menu");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1.5f);
        menu = Object.FindAnyObjectByType<MainMenu>();
        Tap(menu.CampaignButton.gameObject);
        yield return run.Until(() => CampaignPanel.IsOpen, 10f, "the case files");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);
        Tap(CampaignPanel.Instance.SlotButtons[0].gameObject);
        yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.CardUp && ChapterScene.Instance.BeginButton != null && SceneManager.GetActiveScene().name == CampaignSave.Scene, 45f, "Chapter 1's card");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(3.5f);
        Tap(ChapterScene.Instance.BeginButton.gameObject);
        yield return run.Until(() => LevelScene.Instance != null && LevelScene.Instance.Yard != null && SceneManager.GetActiveScene().name.StartsWith("Level_"), 45f, "the level scene");
        if (run.Done) {
            yield break;
        }
        run.Scene(SceneManager.GetActiveScene().name);
        yield return new WaitForSecondsRealtime(3f);
        yield return Cycle("chapter-1", true);
        if (run.Done) {
            yield break;
        }

        // The hub: a finished Chapter 1 (named setup shortcut: the sandbox file files it), leave by the level's own way out.
        run.Event("setup", "Chapter 1 filed in the sandbox file, then the way back to the hub");
        CampaignSave.Complete(0);
        LevelScene.Instance.LeaveToHub();
        yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.Hub != null && ChapterScene.Instance.Hub.Shown && PauseMenu.InHub, 45f, "the hub");
        if (run.Done) {
            yield break;
        }
        run.Scene("Hub");
        yield return new WaitForSecondsRealtime(3f);
        yield return Cycle("hub", true);
    }
}
