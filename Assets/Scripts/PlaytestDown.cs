using System;
using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The "down-retry" scenario: five forced downs, three in Arcade (Slice's bar emptied: slow-motion beat, camera hold on her, Wallit's
// "Slice! Hold on!", the music dip, then the STRUCK OFF sheet with Retry first and focused; two Retries and one Menu) and two in
// Chapter 1's level (Wallit's last health point: the same beat on him with Slice's line, then the checkpoint). On a phone
// (390x844, touch) every press is a finger on the button, on desktop it is Enter.
//
// Every frame from the down until control is back is sampled (state, timescale, input lock); a change of any of the three is a
// `down-frame` event. Metrics: run.downs, run.downStalls, run.retryTaps (most taps one Retry took), run.retrySeconds (the sheet to
// control, the slowest), run.padReturnSeconds (touch pads back after a Retry), run.slowMoSeconds, run.cameraCuts, run.voicedLines,
// run.musicDip, and on a phone run.retryHeightPx / run.retryTopPct / run.revivePadPx. Setup shortcuts (named setup events):
// WaveDirector.DevLose and LevelScene.DevDown; everything after them is the game's own flow and real input.
public static class PlaytestDown {
    static PlaytestRun run;
    static int stalls;
    static float worstRetry;
    static int worstTaps;
    static float worstPads;
    static float minBeat = 99f;
    static float maxBeat;
    static bool phone;
    static float lastScale = -1f;
    static string lastState = "";
    static bool lastLock;
    static int frames;

    static void Tap(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
    }

    // One frame of the down's state; a change of state, timescale or lock is logged.
    static void Sample(string where) {
        frames++;
        string state = DownFlow.State.ToString();
        bool locked = DownFlow.InputLocked;
        float scale = Time.timeScale;
        if (state != lastState || locked != lastLock || Mathf.Abs(scale - lastScale) > 0.01f) {
            run.Event("down-frame", where + " state=" + state + " timescale=" + scale.ToString("0.00") + " inputLocked=" + locked + " frame=" + frames);
            lastState = state;
            lastLock = locked;
            lastScale = scale;
        }
    }

    // Waits for `done`, sampling every frame; a timeout is a stall (the state never exited).
    static IEnumerator Watch(string where, Func<bool> done, float seconds) {
        float until = Time.realtimeSinceStartup + seconds;
        while (!done() && !run.Done) {
            Sample(where);
            if (Time.realtimeSinceStartup > until) {
                stalls++;
                run.Problem(PlaytestSeverity.Bug, "down-retry: " + where + " never finished in " + seconds + " s (state " + DownFlow.State + ", timescale " + Time.timeScale.ToString("0.00") + ")");
                yield break;
            }
            yield return null;
        }
        Sample(where);
    }

    static void CheckBeat(string who, int cutsBefore, int voicedBefore) {
        float beat = DownFlow.LastBeatSeconds;
        minBeat = Mathf.Min(minBeat, beat);
        maxBeat = Mathf.Max(maxBeat, beat);
        bool slow = beat >= 0.5f && beat <= 1f && DownFlow.LowestScale < 0.5f;
        bool cut = DownFlow.Cuts == cutsBefore + 1;
        bool voice = DownFlow.Voiced == voicedBefore + 1;
        run.Event("down-beat", who + ": slow-mo " + beat.ToString("0.00") + " s (lowest timescale " + DownFlow.LowestScale.ToString("0.00") + "), camera cut " + cut + ", voiced line " + (voice ? "\"" + DownFlow.LastLine + "\" by " + DownFlow.LastWho : "none") + ", lowest music " + DownFlow.LowestMusic.ToString("0.00"));
        if (!slow || !cut || !voice) {
            run.Problem(PlaytestSeverity.Bug, "down-retry: the " + who + " down beat is missing a part (slow-mo " + slow + ", camera cut " + cut + ", one voiced line " + voice + ")");
        }
    }

    static Rect Box(RectTransform rect) {
        var c = new Vector3[4];
        rect.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    // On a phone: Retry (and the Revive pad) are big and in the lower third.
    static void CheckPhoneLayout() {
        RunEndScreen screen = RunEndScreen.Instance;
        if (screen == null || screen.RetryButton == null) {
            return;
        }
        Rect retry = Box((RectTransform)screen.RetryButton.transform);
        float third = UnityEngine.Screen.height / 3f;
        run.Metric("run.retryHeightPx", Mathf.Round(retry.height));
        run.Metric("run.retryTopPct", Mathf.Round(retry.yMax / UnityEngine.Screen.height * 100f));
        run.Event("retry-box", "Retry " + Mathf.Round(retry.width) + "x" + Mathf.Round(retry.height) + " px, top at " + Mathf.Round(retry.yMax) + " of " + UnityEngine.Screen.height + " (lower third ends at " + Mathf.Round(third) + ")");
        if (retry.height < 64f || retry.yMax > third + 1f) {
            run.Problem(PlaytestSeverity.Bug, "down-retry: Retry is " + Mathf.Round(retry.height) + " px tall with its top at " + Mathf.Round(retry.yMax) + " px; it must be 64 px or more and inside the lower third (" + Mathf.Round(third) + " px)");
        }
        Rect revive;
        if (TouchControls.PadBox("Revive", out revive)) {
            run.Metric("run.revivePadPx", Mathf.Round(Mathf.Min(revive.width, revive.height)));
            run.Event("revive-box", "Revive pad " + Mathf.Round(revive.width) + "x" + Mathf.Round(revive.height) + " px, top at " + Mathf.Round(revive.yMax));
            if (Mathf.Min(revive.width, revive.height) < 64f || revive.yMax > third + 1f) {
                run.Problem(PlaytestSeverity.Bug, "down-retry: the Revive pad is under 64 px or above the lower third");
            }
        }
    }

    // Slice falls: the beat, the sheet, then Retry (or Menu) the way the platform presses it.
    static IEnumerator ArcadeDown(int n, bool retry) {
        yield return run.Until(() => WaveDirector.Instance != null && !WaveDirector.IsLost && !LevelUpScreen.IsOpen && Time.timeScale > 0.99f, 30f, "an Arcade run to knock down (" + n + ")");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(3f);   // a little real play first
        int cuts = DownFlow.Cuts;
        int voiced = DownFlow.Voiced;
        int taps = DownFlow.RetryTaps;
        lastState = "";
        run.Event("setup", "down " + n + ": Slice falls (WaveDirector.DevLose)");
        WaveDirector.Instance.DevLose();
        yield return Watch("down " + n + " beat", () => DownFlow.State != DownFlow.Phase.Beat && DownFlow.Downs >= n, DownFlow.MaxSeconds + 1.5f);
        CheckBeat("Slice's", cuts, voiced);
        yield return Watch("down " + n + " sheet", () => RunEndScreen.IsOpen && RunEndScreen.Instance != null && RunEndScreen.Instance.RetryButton != null && RunEndScreen.Instance.RetryButton.interactable, 6f);
        if (run.Done) {
            yield break;
        }
        // Retry is the first button and holds the focus.
        GameObject selected = MenuNav.Selected;
        bool focused = selected != null && RunEndScreen.Instance.RetryButton != null && selected == RunEndScreen.Instance.RetryButton.gameObject;
        run.Event("retry-focus", "Retry first and focused: " + focused);
        if (!focused) {
            run.Problem(PlaytestSeverity.Bug, "down-retry: Retry is not the focused button on the run-end sheet");
        }
        if (phone && n == 1) {
            CheckPhoneLayout();
        }
        if (!retry) {
            Tap(RunEndScreen.Instance.MenuButton.gameObject);
            yield return Watch("down " + n + " to the menu", () => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Time.timeScale > 0.99f, 20f);
            yield break;
        }
        float sheetAt = DownFlow.SheetAt;
        if (phone) {
            Tap(RunEndScreen.Instance.RetryButton.gameObject);
        } else {
            PlayInput.Tap(KeyCode.Return);
        }
        run.Event("tap", phone ? "Retry tapped" : "Retry: Enter");
        yield return Watch("down " + n + " retry", () => DownFlow.State == DownFlow.Phase.None && DownFlow.ControlBackAt > sheetAt && WaveDirector.Instance != null && !WaveDirector.IsLost && SceneManager.GetActiveScene().name == MetaProgress.GameScene, StallSecondsWide());
        if (run.Done) {
            yield break;
        }
        float took = DownFlow.ControlBackAt - sheetAt;
        int tapsUsed = DownFlow.RetryTaps - taps;
        worstRetry = Mathf.Max(worstRetry, took);
        worstTaps = Mathf.Max(worstTaps, tapsUsed);
        run.Event("retry", "Retry took " + tapsUsed + " tap(s), " + took.ToString("0.00") + " s from the sheet to control, timescale " + Time.timeScale.ToString("0.00"));
        if (Time.timeScale < 0.99f) {
            stalls++;
            run.Problem(PlaytestSeverity.Bug, "down-retry: timescale " + Time.timeScale.ToString("0.00") + " after control came back");
        }
        if (phone) {
            float backAt = Time.unscaledTime;
            float padsAt = -1f;
            float until = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < until && !run.Done) {
                if (TouchControls.PadsShown) {
                    padsAt = Time.unscaledTime;
                    break;
                }
                yield return null;
            }
            float pads = padsAt < 0f ? 99f : Mathf.Max(0f, padsAt - DownFlow.ControlBackAt);
            worstPads = Mathf.Max(worstPads, pads);
            run.Event("pads-back", "touch pads back " + pads.ToString("0.00") + " s after control");
            TouchControls.LiftBotFingers();
        }
    }

    // Retry has a few seconds for the scene load; a wider watch so the load itself is not mistaken for a stall.
    static float StallSecondsWide() {
        return 25f;
    }

    // Wallit goes down in Chapter 1's level: the beat on him, then the checkpoint.
    static IEnumerator LevelDown(int n) {
        yield return run.Until(() => LevelScene.Instance != null && LevelScene.Instance.Yard != null && Time.timeScale > 0.99f && !Cutscene.IsOpen, 30f, "the level to knock Wallit down (" + n + ")");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(2f);
        int cuts = DownFlow.Cuts;
        int voiced = DownFlow.Voiced;
        lastState = "";
        float at = Time.unscaledTime;
        run.Event("setup", "down " + n + ": Wallit goes down (LevelScene.DevDown)");
        LevelScene.Instance.DevDown();
        yield return Watch("down " + n + " beat", () => DownFlow.State != DownFlow.Phase.Beat && DownFlow.Downs >= n, DownFlow.MaxSeconds + 1.5f);
        CheckBeat("Wallit's", cuts, voiced);
        yield return Watch("down " + n + " checkpoint", () => DownFlow.ControlBackAt > at && Time.timeScale > 0.99f, 8f);
        if (run.Done) {
            yield break;
        }
        float took = DownFlow.ControlBackAt - at;
        run.Event("resume", "Wallit back at the checkpoint " + took.ToString("0.00") + " s after the fall, timescale " + Time.timeScale.ToString("0.00"));
        if (Time.timeScale < 0.99f) {
            stalls++;
            run.Problem(PlaytestSeverity.Bug, "down-retry: timescale " + Time.timeScale.ToString("0.00") + " after Wallit's checkpoint");
        }
        if (phone) {
            float pads = -1f;
            float until = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < until && !run.Done) {
                if (TouchControls.PadsShown) {
                    pads = 0f;
                    break;
                }
                yield return null;
            }
            if (pads < 0f) {
                stalls++;
                run.Problem(PlaytestSeverity.Bug, "down-retry: the touch pads were not back after Wallit's checkpoint");
            }
        }
        yield return new WaitForSecondsRealtime(1f);
    }

    public static IEnumerator Run(PlaytestRun r) {
        run = r;
        stalls = 0;
        worstRetry = 0f;
        worstTaps = 0;
        worstPads = 0f;
        minBeat = 99f;
        maxBeat = 0f;
        frames = 0;
        lastScale = -1f;
        lastState = "";
        DownFlow.ResetCounters();
        phone = UnityEngine.Screen.width <= 900 || UnityEngine.Screen.height <= 500;
        if (phone) {
            UiFit.ForceTouch(true);
        }
        MetaProgress.EnterSandbox(true);
        CampaignSave.EnterSandbox();
        ColdOpen.Suppress = true;
        run.Log("down-retry on " + (phone ? "a phone" : "desktop") + " " + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height + ", layout " + UiFit.Mode);

        // Arcade: three downs.
        yield return Playtest.ToMenuPublic();
        if (run.Done) {
            yield break;
        }
        yield return Playtest.StartArcadePublic();
        if (run.Done) {
            yield break;
        }
        run.Scene("Arcade");
        yield return ArcadeDown(1, true);
        if (run.Done) {
            yield break;
        }
        yield return ArcadeDown(2, true);
        if (run.Done) {
            yield break;
        }
        yield return ArcadeDown(3, false);
        if (run.Done) {
            yield break;
        }

        // Chapter 1: Wallit's two downs.
        yield return new WaitForSecondsRealtime(1.5f);
        MainMenu menu = UnityEngine.Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.CampaignButton == null) {
            run.Problem(PlaytestSeverity.Bug, "down-retry: no Campaign button on the menu");
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
        yield return LevelDown(4);
        if (run.Done) {
            yield break;
        }
        yield return LevelDown(5);
        if (run.Done) {
            yield break;
        }

        int downs = DownFlow.Downs;
        run.Metric("run.downs", downs);
        run.Metric("run.downStalls", stalls + DownFlow.Stalls);
        run.Metric("run.retryTaps", worstTaps);
        run.Metric("run.retrySeconds", Mathf.Round(worstRetry * 100f) / 100f);
        run.Metric("run.slowMoSeconds", Mathf.Round(maxBeat * 100f) / 100f);
        run.Metric("run.cameraCuts", DownFlow.Cuts);
        run.Metric("run.voicedLines", DownFlow.Voiced);
        run.Metric("run.musicDip", Mathf.Round(DownFlow.LowestMusic * 100f) / 100f);
        run.Metric("run.downFrames", frames);
        if (phone) {
            run.Metric("run.padReturnSeconds", Mathf.Round(worstPads * 100f) / 100f);
            if (worstPads > 0.3f) {
                run.Problem(PlaytestSeverity.Bug, "down-retry: the touch pads took " + worstPads.ToString("0.00") + " s to come back (0.3 s allowed)");
            }
        }
        run.Event("down-summary", downs + " downs, " + (stalls + DownFlow.Stalls) + " stalls, Retry " + worstTaps + " tap(s) and " + worstRetry.ToString("0.00") + " s from the sheet to control, slow-mo " + minBeat.ToString("0.00") + "-" + maxBeat.ToString("0.00") + " s");
        if (downs != 5) {
            run.Problem(PlaytestSeverity.Bug, "down-retry: " + downs + " downs staged, expected 5");
        }
        if (stalls + DownFlow.Stalls > 0) {
            run.Problem(PlaytestSeverity.Bug, "down-retry: " + (stalls + DownFlow.Stalls) + " down stall(s)");
        }
        if (worstTaps > 2) {
            run.Problem(PlaytestSeverity.Bug, "down-retry: Retry took " + worstTaps + " taps (2 allowed)");
        }
        if (worstRetry > 3f) {
            run.Problem(PlaytestSeverity.Bug, "down-retry: " + worstRetry.ToString("0.00") + " s from the sheet to control (3 s allowed)");
        }
    }
}
