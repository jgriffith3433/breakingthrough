using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The "comfort" scenario's body (Playtest.CutsceneStep hands over to it once Chapter 1's opening scene is up). Real input:
// P opens the pause menu over the scene, the Settings buttons are pressed like a mouse, the pad's buttons (Start, A, Y and the
// stick) are tapped through PlayInput, and the Skip button is clicked as a finger would. Named setup shortcuts: the dialogue
// choice page and the short scenes are put up with Cutscene.Play. Events comfort-*, metrics comfort*.
public static class PlaytestComfort {
    static PlaytestRun run;

    static void Click(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    static IEnumerator Wait(float seconds) {
        yield return new WaitForSecondsRealtime(seconds);
    }

    static int SceneSize(Cutscene scene) {
        return scene.IsReel ? scene.SubtitleFontSize : scene.LineFontSize;
    }

    public static IEnumerator Run(PlaytestRun r) {
        run = r;
        Cutscene scene = Cutscene.Instance;
        yield return Wait(2.5f);
        if (!Cutscene.IsOpen) {
            run.Problem(PlaytestSeverity.Bug, "comfort: no opening scene to test");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        bool reel = scene.IsReel;
        int baseSize = SceneSize(scene);
        float baseZoom = scene.Zoom;
        run.Event("comfort-baseline", (reel ? "reel" : "page scene") + ": text " + baseSize + " units, backing " + scene.SubtitleBacked + ", reduced motion " + ComfortOptions.ReducedMotion + ", subtitles " + ComfortOptions.SizeName(ComfortOptions.SubtitleSize));
        string pageBefore = reel ? "" : scene.Page.ToString();

        // 1. The pause key over a scene: the pause menu opens with Skip scene and Restart chapter, and the scene does not turn.
        PlayInput.Tap(KeyCode.P);
        yield return run.Until(() => PauseMenu.IsOpen, 4f, "the pause menu over the scene (P)");
        if (run.Done) {
            yield break;
        }
        yield return Wait(0.4f);
        PauseMenu pause = PauseMenu.Instance;
        bool skipShown = pause.SkipSceneButton.gameObject.activeInHierarchy;
        bool restartShown = pause.RestartButton.gameObject.activeInHierarchy;
        bool held = Cutscene.IsOpen && (reel || scene.Page.ToString() == pageBefore);
        run.Event("comfort-pause", "pause over the scene: Skip scene " + skipShown + ", Restart chapter " + restartShown + ", scene held " + held);
        run.Metric("comfortPauseOverScene", skipShown && restartShown && held ? 1 : 0);
        if (!skipShown || !restartShown || !held) {
            run.Problem(PlaytestSeverity.Bug, "comfort: the pause menu over a scene lacks Skip scene / Restart chapter, or the key also turned the page");
        }

        // 2. Settings: subtitles Largest, high contrast on, camera Calm, pressed like a mouse.
        Click(pause.SettingsButton.gameObject);
        yield return run.Until(() => SettingsPanel.IsOpen, 4f, "Settings from the pause menu");
        if (run.Done) {
            yield break;
        }
        yield return Wait(0.3f);
        SettingsPanel panel = SettingsPanel.Instance;
        Click(panel.SubtitleButton.gameObject);
        yield return Wait(0.2f);
        Click(panel.SubtitleButton.gameObject);
        yield return Wait(0.2f);
        Click(panel.ContrastButton.gameObject);
        yield return Wait(0.2f);
        Click(panel.MotionButton.gameObject);
        yield return Wait(0.3f);
        run.Event("comfort-settings", "buttons read: subtitles \"" + panel.SubtitleButton.GetComponentInChildren<Text>().text + "\", contrast \"" + panel.ContrastButton.GetComponentInChildren<Text>().text + "\", camera \"" + panel.MotionButton.GetComponentInChildren<Text>().text + "\"");
        Click(panel.DoneButton.gameObject);
        yield return Wait(0.4f);

        // 3. The pad's Start resumes (MenuNav.PadStart), and the scene shows the options.
        PlayInput.Tap(KeyCode.JoystickButton7);
        yield return run.Until(() => !PauseMenu.IsOpen, 4f, "the pause menu to close on the pad's Start");
        if (run.Done) {
            yield break;
        }
        yield return Wait(1.2f);
        int bigSize = Cutscene.IsOpen ? SceneSize(scene) : 0;
        run.Event("comfort-options", "text " + baseSize + " -> " + bigSize + " units; subtitle plate " + (Cutscene.IsOpen && scene.SubtitleBacked) + "; zoom " + baseZoom.ToString("0.00") + " -> " + (Cutscene.IsOpen ? scene.Zoom.ToString("0.00") : "-") + "; camera weight " + (Cutscene.IsOpen ? scene.CameraWeight.ToString("0.00") : "-"));
        run.Metric("comfortTextBase", baseSize);
        run.Metric("comfortTextLarge", bigSize);
        if (Cutscene.IsOpen) {
            if (bigSize < baseSize * 1.4f) {
                run.Problem(PlaytestSeverity.Bug, "comfort: Largest subtitles did not grow the text (" + baseSize + " -> " + bigSize + ")");
            }
            if (reel && !scene.SubtitleBacked && scene.Subtitle.Length > 0) {
                run.Problem(PlaytestSeverity.Bug, "comfort: high contrast gave the subtitle no solid plate");
            }
            if (reel && scene.Zoom > 1.001f) {
                run.Problem(PlaytestSeverity.Bug, "comfort: reduced motion left the reel zooming (" + scene.Zoom.ToString("0.00") + ")");
            }
            if (!reel && scene.CameraWeight > 0.01f) {
                run.Problem(PlaytestSeverity.Bug, "comfort: reduced motion left the cutscene camera sweeping (weight " + scene.CameraWeight.ToString("0.00") + ")");
            }
        }

        // 4. The pad's A turns the page / steps the reel; Y skips the scene.
        if (Cutscene.IsOpen) {
            int before = reel ? Cutscene.ShotsShown : scene.Page;
            if (!reel && scene.Typing) {
                PlayInput.Tap(KeyCode.JoystickButton0);
                yield return Wait(0.5f);
            }
            PlayInput.Tap(KeyCode.JoystickButton0);
            yield return Wait(0.8f);
            int after = reel ? Cutscene.ShotsShown : scene.Page;
            run.Event("comfort-pad-advance", "A: " + before + " -> " + after);
            run.Metric("comfortPadAdvance", after > before ? 1 : 0);
            if (after <= before && Cutscene.IsOpen) {
                run.Problem(PlaytestSeverity.Bug, "comfort: the gamepad's A did not turn the page or step the reel");
            }
        }
        if (Cutscene.IsOpen) {
            PlayInput.Tap(KeyCode.JoystickButton3);
            yield return run.Until(() => !Cutscene.IsOpen, 4f, "the scene to close on the pad's Y");
            if (run.Done) {
                yield break;
            }
        }
        run.Event("comfort-pad-skip", "Y skipped the scene; pad skips " + Cutscene.PadSkips + ", time scale " + Time.timeScale.ToString("0.0"));
        run.Metric("comfortPadSkips", Cutscene.PadSkips);
        if (Cutscene.PadSkips < 1 || Time.timeScale < 0.99f) {
            run.Problem(PlaytestSeverity.Bug, "comfort: the pad's Y did not leave the scene cleanly");
        }
        yield return Wait(1f);

        // 5. A dialogue choice with the pad: the stick moves to the second answer, A confirms it (setup: the choice page is put up).
        Choices.Row row = Choices.All[0];
        Cutscene.Play(new[] { "B|on", "Q|" + row.Id, "N|(end of scene)" }, null);
        yield return run.Until(() => Cutscene.IsOpen && Cutscene.Instance.Choosing, 5f, "the choice page");
        if (run.Done) {
            yield break;
        }
        yield return Wait(0.6f);
        PlayInput.Axis("Horizontal", 1f);
        yield return Wait(0.25f);
        PlayInput.Axis("Horizontal", 0f);
        yield return Wait(0.25f);
        PlayInput.Tap(KeyCode.JoystickButton0);
        yield return Wait(0.6f);
        int picked = Choices.Picked(row.Id);
        run.Event("comfort-pad-choice", row.Id + ": stick right then A saved option " + picked + " (expected 2)");
        run.Metric("comfortPadChoice", picked == 2 ? 1 : 0);
        if (picked != 2) {
            run.Problem(PlaytestSeverity.Bug, "comfort: the stick and A did not make the second dialogue choice (saved " + picked + ")");
        }
        while (Cutscene.IsOpen) {
            PlayInput.Tap(KeyCode.JoystickButton0);
            yield return Wait(0.5f);
            if (Cutscene.Instance.Page > 20) {
                break;
            }
        }

        // 6. Touch: the Skip button is tapped like a finger.
        Cutscene.Play(new[] { "B|on", "N|One.", "N|Two.", "N|Three." }, null);
        yield return run.Until(() => Cutscene.IsOpen, 4f, "a short scene");
        yield return Wait(0.7f);
        int skippedBefore = Cutscene.Skipped;
        Click(scene.SkipButton.gameObject);
        yield return Wait(0.5f);
        run.Event("comfort-touch-skip", "Skip button tapped: scene open " + Cutscene.IsOpen + ", skipped " + skippedBefore + " -> " + Cutscene.Skipped);
        run.Metric("comfortTouchSkip", Cutscene.Skipped > skippedBefore && !Cutscene.IsOpen ? 1 : 0);
        if (Cutscene.IsOpen) {
            run.Problem(PlaytestSeverity.Bug, "comfort: the Skip button did not leave the scene");
        }

        // 7. Skip scene from the pause menu.
        Cutscene.Play(new[] { "B|on", "N|One.", "N|Two.", "N|Three." }, null);
        yield return run.Until(() => Cutscene.IsOpen, 4f, "a scene to pause");
        yield return Wait(0.7f);
        PlayInput.Tap(KeyCode.P);
        yield return run.Until(() => PauseMenu.IsOpen, 4f, "the pause menu");
        yield return Wait(0.4f);
        int menuSkips = PauseMenu.SceneSkips;
        Click(PauseMenu.Instance.SkipSceneButton.gameObject);
        yield return Wait(0.6f);
        run.Event("comfort-pause-skip", "Skip scene in the pause menu: scene open " + Cutscene.IsOpen + ", pause open " + PauseMenu.IsOpen + ", time scale " + Time.timeScale.ToString("0.0"));
        run.Metric("comfortPauseSkip", PauseMenu.SceneSkips > menuSkips && !Cutscene.IsOpen && !PauseMenu.IsOpen ? 1 : 0);
        if (Cutscene.IsOpen || PauseMenu.IsOpen || Time.timeScale < 0.99f) {
            run.Problem(PlaytestSeverity.Bug, "comfort: Skip scene in the pause menu did not leave the scene and resume play");
        }

        // 8. Restart chapter from the pause menu, asked twice; the load screen shows the chapter and the new run comes up.
        ChapterRun before0 = ChapterRun.Instance;
        int screens = LoadScreen.Shown;
        long memBefore = ChapterMap.Memory();
        yield return Wait(0.5f);
        PlayInput.Tap(KeyCode.P);
        yield return run.Until(() => PauseMenu.IsOpen, 4f, "the pause menu in play");
        yield return Wait(0.4f);
        Click(PauseMenu.Instance.RestartButton.gameObject);
        yield return Wait(0.3f);
        run.Event("comfort-restart-ask", "Restart chapter asks twice: \"" + PauseMenu.Instance.RestartButton.GetComponentInChildren<Text>().text + "\"");
        float pressed = Time.realtimeSinceStartup;
        Click(PauseMenu.Instance.RestartButton.gameObject);
        yield return run.Until(() => LoadScreen.Shown > screens, 4f, "the load screen after Restart chapter");
        if (run.Done) {
            yield break;
        }
        run.Event("comfort-load-screen", "load screen up for \"" + LoadScreen.LastTitle + "\" (chapter art " + LoadScreen.LastHadArt + ")");
        yield return run.Until(() => ChapterRun.Instance != null && ChapterRun.Instance != before0 && !LoadScreen.IsUp, 60f, "the restarted chapter and the load screen to leave");
        if (run.Done) {
            yield break;
        }
        float took = Time.realtimeSinceStartup - pressed;
        long memAfter = ChapterMap.Memory();
        run.Metric("comfortRestartSeconds", Mathf.Round(took * 10f) / 10f);
        run.Metric("comfortRestartMemoryGrowthMB", Mathf.Round((memAfter - memBefore) / 1048576f));
        run.Event("comfort-restart", "chapter restarted in " + took.ToString("0.0") + " s (load screen " + LoadScreen.LastSeconds.ToString("0.0") + " s), scene count " + SceneManager.sceneCount + ", memory " + Mathf.Round(memBefore / 1048576f) + " -> " + Mathf.Round(memAfter / 1048576f) + " MB");
        if (SceneManager.sceneCount > 2) {
            run.Problem(PlaytestSeverity.Bug, "comfort: " + SceneManager.sceneCount + " scenes are open after a chapter restart (the old map was not unloaded)");
        }
        if (memAfter - memBefore > 96L * 1048576L) {
            run.Problem(PlaytestSeverity.Bug, "comfort: memory grew " + Mathf.Round((memAfter - memBefore) / 1048576f) + " MB over a chapter restart");
        }
        run.Finish(PlaytestOutcome.Done);
    }
}
