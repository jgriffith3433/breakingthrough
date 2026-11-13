using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// The "title-scene" scenario: the live Hollow behind the main menu. It reads the stage (Wallit, Slice, bots peeking over the
// hedge, the coin, the drifting camera), times the logo's stamp from the menu appearing (wanted under 3 s), samples the frame
// rate for 4 s (30 or more on a phone), checks Play sits in the lower third and is 64 px or taller, then taps Play and times
// the push-in to control (under 2 s) while watching for a black frame. Run it with mobile (390x844) for the phone checks.
// Events title-cast, title-logo, title-motion, title-play-layout, title-fps, title-play; metrics titleLogoSeconds, titleFps,
// titlePlayTopFrac, titlePlayPx, titlePlaySeconds, titleBlackSeconds.
public static class PlaytestTitle {
    static void Tap(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    public static IEnumerator Run(PlaytestRun run) {
        MetaProgress.EnterSandbox(true);
        CampaignSave.EnterSandbox();
        if (UnityEngine.Screen.width <= 900 || UnityEngine.Screen.height < 600) {
            UiFit.ForceTouch(true);
        }
        run.Scene("Main Menu");
        // The scenario can start before the menu scene has built its stage (or while a cold start is carrying the page into Arcade):
        // get to the menu scene and wait until the menu has finished building before reading it.
        if (SceneManager.GetActiveScene().name != MetaProgress.MenuScene) {
            Time.timeScale = 1f;
            SceneManager.LoadScene(MetaProgress.MenuScene);
        }
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the main menu");
        MainMenu early = Object.FindAnyObjectByType<MainMenu>();
        if (early != null && early.SkippedMenu) {
            yield return run.Until(() => SceneManager.GetActiveScene().name != MetaProgress.MenuScene, 30f, "the cold-start run");
            Time.timeScale = 1f;
            SceneManager.LoadScene(MetaProgress.MenuScene);
            yield return run.Until(() => { MainMenu m = Object.FindAnyObjectByType<MainMenu>(); return m != null && !m.SkippedMenu; }, 30f, "the main menu");
        }
        yield return run.Until(() => { MainMenu m = Object.FindAnyObjectByType<MainMenu>(); return m != null && m.Ready && m.PlayButton != null; }, 30f, "the menu to finish building");
        if (run.Done) {
            yield break;
        }
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.PlayButton == null || menu.Stage == null) {
            run.Problem(PlaytestSeverity.Bug, "title-scene: the menu has no live stage behind it");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        float shown = Time.unscaledTime;
        MenuStage stage = menu.Stage;
        Vector3 cam0 = stage.CameraLocal;
        yield return run.Until(() => MenuLogoStamp.Landed, 6f, "the logo stamp landing");
        if (run.Done) {
            yield break;
        }
        float logo = MenuLogoStamp.LandedAt - shown;
        run.Metric("titleLogoSeconds", Mathf.Round(logo * 100f) / 100f);
        run.Event("title-logo", "the logo stamped in with an ink splat and a thud " + logo.ToString("0.00") + " s after the menu appeared (wanted under 3)");
        if (logo > 3f) {
            run.Problem(PlaytestSeverity.Bug, "title-scene: the logo took " + logo.ToString("0.0") + " s to stamp in");
        }

        // The cast and its motion: Wallit and Slice, the bots over the hedge, the coin, the drifting camera.
        int frames = 0;
        float t0 = Time.unscaledTime;
        int maxUp = 0;
        while (Time.unscaledTime - t0 < 4f) {
            frames++;
            maxUp = Mathf.Max(maxUp, stage.BotsUp);
            yield return null;
        }
        float fps = frames / Mathf.Max(0.1f, Time.unscaledTime - t0);
        run.Metric("titleFps", Mathf.Round(fps));
        run.Event("title-fps", "the title scene ran at " + fps.ToString("0") + " fps over 4 s");
        if (fps < 30f) {
            run.Problem(PlaytestSeverity.Bug, "title-scene: the title scene ran at " + fps.ToString("0") + " fps (wanted 30 or more)");
        }
        yield return run.Until(() => stage.PeeksSeen >= 1 && stage.CoinFlips >= 1, 12f, "a bot peeking over the hedge and a coin flip");
        float drift = (stage.CameraLocal - cam0).magnitude;
        run.Event("title-cast", "Wallit, Slice and " + (stage.ModelsShown - 2) + " bots in the Hollow, " + stage.PeeksSeen + " peeks, " + stage.CoinFlips + " coin flips");
        run.Event("title-motion", "the camera drifted " + drift.ToString("0.00") + " m");
        run.Metric("titleCastModels", stage.ModelsShown);
        if (stage.ModelsShown < 4) {
            run.Problem(PlaytestSeverity.Bug, "title-scene: fewer than Wallit, Slice and two bots on the title");
        }
        if (drift < 0.02f) {
            run.Problem(PlaytestSeverity.Bug, "title-scene: the title camera does not move");
        }

        // Play's place and size.
        var rt = (RectTransform)menu.PlayButton.transform;
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        float px = UiFit.PixelRatio;
        float topFrac = c[1].y / Mathf.Max(1f, UnityEngine.Screen.height);
        float heightPx = (c[1].y - c[0].y) / px;
        run.Metric("titlePlayTopFrac", Mathf.Round(topFrac * 100f) / 100f);
        run.Metric("titlePlayPx", Mathf.Round(heightPx));
        run.Event("title-play-layout", UiFit.Mode + " " + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height + ": Play top at " + (topFrac * 100f).ToString("0") + "% of the height, " + heightPx.ToString("0") + " CSS px tall");
        if (UiFit.Mode == UiMode.Tall && (topFrac > 0.34f || heightPx < 64f)) {
            run.Problem(PlaytestSeverity.Bug, "title-scene: Play is not in the lower third at 64 px or more");
        }

        // One tap on Play: the camera pushes in and the run starts; no black frame.
        float tapped = Time.realtimeSinceStartup;
        Tap(menu.PlayButton.gameObject);
        float blackStart = -1f;
        float longestBlack = 0f;
        float pushed = 0f;
        while (!(SceneManager.GetActiveScene().name == MetaProgress.GameScene && WaveDirector.Instance != null)) {
            if (Time.realtimeSinceStartup - tapped > 20f) {
                run.Problem(PlaytestSeverity.Bug, "title-scene: Play did not reach the run in 20 s");
                run.Finish(PlaytestOutcome.Stuck);
                yield break;
            }
            pushed = Mathf.Max(pushed, stage != null ? stage.PushDepth : pushed);
            // A black frame: no overlay holding the last picture and no active menu stage.
            bool covered = stage != null || MenuHandoff.Active;
            if (!covered) {
                if (blackStart < 0f) {
                    blackStart = Time.realtimeSinceStartup;
                }
            } else if (blackStart >= 0f) {
                longestBlack = Mathf.Max(longestBlack, Time.realtimeSinceStartup - blackStart);
                blackStart = -1f;
            }
            yield return null;
        }
        float seconds = Time.realtimeSinceStartup - tapped;
        run.Metric("titlePlaySeconds", Mathf.Round(seconds * 100f) / 100f);
        run.Metric("titleBlackSeconds", Mathf.Round(longestBlack * 100f) / 100f);
        run.Event("title-play", "Play pushed the camera in (" + pushed.ToString("0.00") + " of the way) and the run began in " + seconds.ToString("0.00") + " s, the last picture held over it (black " + longestBlack.ToString("0.00") + " s)");
        if (pushed < 0.5f) {
            run.Problem(PlaytestSeverity.Bug, "title-scene: Play did not push the camera in");
        }
        if (seconds > 2f) {
            run.Problem(PlaytestSeverity.Bug, "title-scene: Play took " + seconds.ToString("0.0") + " s to reach the run (wanted under 2)");
        }
        if (longestBlack > 0.3f) {
            run.Problem(PlaytestSeverity.Bug, "title-scene: a black frame of " + longestBlack.ToString("0.00") + " s between the title and the run");
        }
        yield return new WaitForSecondsRealtime(1.5f);
        run.Finish(PlaytestOutcome.Done);
    }
}
