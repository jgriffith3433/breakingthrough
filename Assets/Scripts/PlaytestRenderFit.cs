using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The "render-fit" scenario: the picture is the right shape and nothing is cut off. At the main menu, in Arcade and in the
// campaign hub it reads RenderFit.Measure (the canvas's backing store against its CSS box and against the camera) and fails when
// the camera's aspect is off by 1% or more or any pixel is cropped. In Arcade it also finds the HUD's rightmost edge and fails
// when any visible control or text runs past the right of the screen. Real taps (a finger on a phone viewport) press Play and the
// case file; the hub's file is a sandbox setup shortcut. Events render-fit (one per screen), render-fit-hud; metrics
// render.aspectError, render.cropPx (the worst of the screens), render.renderScale, render.hudRightPx (HUD right edge,
// CSS px from the right of the screen: 0 or more is visible), render.hudOffRight.
public static class PlaytestRenderFit {
    static PlaytestRun run;
    static float worstError;
    static float worstCrop;

    static void Tap(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    static void Check(string where) {
        RenderFit.Sample s = RenderFit.Measure();
        worstError = Mathf.Max(worstError, s.aspectError);
        worstCrop = Mathf.Max(worstCrop, s.cropPx);
        run.Event("render-fit", where + ": " + RenderFit.Describe(s));
        run.Metric("render.aspectError", Mathf.Round(worstError * 10000f) / 10000f);
        run.Metric("render.cropPx", worstCrop);
        run.Metric("render.renderScale", Mathf.Round(s.renderScale * 100f) / 100f);
        if (s.aspectError >= 0.01f) {
            run.Problem(PlaytestSeverity.Bug, "render-fit: at " + where + " the picture is stretched, aspect error " + s.aspectError.ToString("0.0000") + " (camera " + s.cameraAspect.ToString("0.000") + ", backing " + s.backW + "x" + s.backH + ", css " + s.cssW.ToString("0") + "x" + s.cssH.ToString("0") + ")");
        }
        if (s.cropPx > 0f) {
            run.Problem(PlaytestSeverity.Bug, "render-fit: at " + where + " " + s.cropPx.ToString("0") + " px of the picture are cut off (css " + s.cssW.ToString("0") + "x" + s.cssH.ToString("0") + " in a " + s.viewW.ToString("0") + "x" + s.viewH.ToString("0") + " viewport)");
        }
        if (s.page && s.renderScale > 2.01f) {
            run.Problem(PlaytestSeverity.Bug, "render-fit: render scale " + s.renderScale.ToString("0.00") + " is over the cap of 2 at " + where);
        }
    }

    // The right edge of everything the player can see on the overlay canvases, against the screen's right edge.
    static void Hud(string where) {
        float screenW = UnityEngine.Screen.width;
        float px = UiFit.PixelRatio;
        float right = 0f;
        int off = 0;
        string worst = "";
        var c = new Vector3[4];
        foreach (Graphic g in Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None)) {
            if (g == null || !g.isActiveAndEnabled || g.color.a < 0.05f || g.canvas == null || g.canvas.renderMode == RenderMode.WorldSpace) {
                continue;
            }
            var rt = g.rectTransform;
            rt.GetWorldCorners(c);
            float width = c[2].x - c[0].x;
            if (width <= 1f || width >= screenW * 0.98f) {
                continue; // a full-screen wash is not a control
            }
            Text text = g as Text;
            if (text != null && string.IsNullOrEmpty(text.text)) {
                continue;
            }
            if (c[0].x >= screenW) {
                continue; // parked off screen on purpose (a panel waiting to slide in)
            }
            right = Mathf.Max(right, c[2].x);
            if (c[2].x > screenW + 1.5f) {
                off++;
                worst = g.name;
            }
        }
        float margin = (screenW - right) / px;
        run.Event("render-fit-hud", where + ": the HUD's right edge is " + margin.ToString("0") + " css px inside the screen; " + off + " pieces run past it" + (off > 0 ? " (" + worst + ")" : ""));
        run.Metric("render.hudRightPx", Mathf.Round(margin));
        run.Metric("render.hudOffRight", off);
        if (off > 0) {
            run.Problem(PlaytestSeverity.Bug, "render-fit: " + off + " HUD pieces run past the right edge of the screen at " + where + " (" + worst + ")");
        }
    }

    public static IEnumerator Run(PlaytestRun current) {
        run = current;
        worstError = 0f;
        worstCrop = 0f;
        MetaProgress.EnterSandbox(true);
        CampaignSave.EnterSandbox();
        bool phone = UnityEngine.Screen.width <= 900 || UnityEngine.Screen.height <= 600;
        if (phone) {
            UiFit.ForceTouch(true);
        }
        run.Scene("Main Menu");
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.PlayButton == null) {
            run.Problem(PlaytestSeverity.Bug, "render-fit: the main menu has no Play button");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        yield return new WaitForSecondsRealtime(1.5f);
        Check("main menu");

        // One tap on Play: Arcade, a few seconds in, with the HUD up.
        Tap(menu.PlayButton.gameObject);
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.GameScene && WaveDirector.Instance != null, 20f, "Arcade after one tap on Play");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(4f);
        Check("Arcade");
        Hud("Arcade");
        yield return new WaitForSecondsRealtime(2f);
        Check("Arcade, later");

        // The hub: setup shortcut, a filed sandbox case file, then a tap on Campaign and the file.
        run.Event("setup", "sandbox case file 1 with one chapter cleared, so the file opens in the hub");
        CampaignSave.Slot file = CampaignSave.Get(0);
        file.used = true;
        file.cleared = 1;
        file.chapter = 1;
        file.reached = 1;
        ChapterScene.HubNext = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(MetaProgress.MenuScene);
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the menu");
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
        yield return run.Until(() => ChapterHub.Instance != null && SceneManager.GetActiveScene().name == CampaignSave.Scene, 45f, "the hub");
        if (run.Done) {
            yield break;
        }
        run.Scene("Hub");
        yield return new WaitForSecondsRealtime(3f);
        Check("hub");
        Hud("hub");
        ChapterScene.HubNext = false;
        run.Finish(PlaytestOutcome.Done);
    }
}
