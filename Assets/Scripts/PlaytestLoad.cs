using System.Collections;
using System.Runtime.InteropServices;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// The "first-load" scenario: the first download and the chapter download, on a phone, with fingers only (taps through the UI's own
// pointer events, as campaign-touch does). Play it in a mobile viewport (390x844). It checks:
//   1. the menu is up and its Campaign button answers (menu-interactive, metric menuInteractiveSeconds: seconds since the player
//      started; the page's own load is the harness's network log) and the campaign's files are not fetched yet (onDemandBeforeMenu 0);
//   2. tapping Campaign and a case file brings down the campaign's core files with the one-line progress (progress-line: its words,
//      that it sits at the bottom inside the safe area and on screen, never over the top half), the card opens;
//   3. Begin on Chapter 1 shows the line again, gives control within 15 s (chapter-control, metric chapterControlSeconds), with no
//      missing art or voice (onDemandFailed 0, castVoiceMissing 0), and no frame over 1 s while it loaded (onDemandLongestFrameMs).
// When the build kept everything in Resources (the editor), nothing is fetched and the line never shows: the events say so.
public static class PlaytestLoad {
    static PlaytestRun run;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern float HawkesPageSeconds();
#else
    static float HawkesPageSeconds() {
        return 0f;
    }
#endif

    static void Tap(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    // Frames pass until done() or the limit; each frame the progress line, if up, is checked once.
    static IEnumerator Watch(System.Func<bool> done, float seconds, string what, string phase) {
        float start = Time.realtimeSinceStartup;
        bool seen = false;
        while (!run.Done && !done() && Time.realtimeSinceStartup - start < seconds) {
            if (OnDemandLine.IsUp && !seen) {
                seen = true;
                CheckLine(phase);
            }
            yield return null;
        }
        if (!done() && !run.Done) {
            run.Problem(PlaytestSeverity.Bug, "first-load: " + what + " did not come up in " + seconds + " s");
        }
    }

    // The line: Department words with a percentage, bottom of the screen, inside the safe area, on screen, one line tall.
    static void CheckLine(string phase) {
        Rect r = OnDemandLine.PlateRect;
        Vector2 canvas = OnDemandLine.CanvasSize;
        string words = OnDemandLine.Shown;
        float safeBottom = OnDemandLine.SafeBottom;
        bool onScreen = r.xMin >= -0.5f && r.xMax <= canvas.x + 0.5f && r.yMin >= -0.5f && r.yMax <= canvas.y + 0.5f;
        bool inSafe = r.yMin >= safeBottom - 0.5f;
        bool low = r.center.y < canvas.y * 0.25f;
        bool wording = words.Contains("%") && (words.StartsWith("Filing") || words.StartsWith("Pulling"));
        run.Event("progress-line", phase + ": \"" + words + "\" plate " + Mathf.Round(r.x) + "," + Mathf.Round(r.y) + " " + Mathf.Round(r.width) + "x" + Mathf.Round(r.height)
            + " of " + Mathf.Round(canvas.x) + "x" + Mathf.Round(canvas.y) + ", safe bottom " + Mathf.Round(safeBottom)
            + ", on screen " + onScreen + ", inside safe area " + inSafe + ", at the bottom " + low + ", words " + wording);
        run.Metric("progressLine_" + phase + "_ok", onScreen && inSafe && low && wording ? 1 : 0);
        if (!onScreen || !inSafe || !low) {
            run.Problem(PlaytestSeverity.Bug, "first-load: the progress line is not at the bottom inside the safe area (" + phase + ")");
        }
        if (!wording) {
            run.Problem(PlaytestSeverity.Bug, "first-load: the progress line reads \"" + words + "\" (want Filing/Pulling ... n%)");
        }
    }

    public static IEnumerator Run(PlaytestRun r) {
        run = r;
        UiFit.ForceTouch(true);
        CampaignSave.EnterSandbox();
        run.Log("layout " + UiFit.Mode + " at " + Screen.width + "x" + Screen.height);

        // 1. The menu answers before any campaign file has come down.
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        float waitMenu = Time.realtimeSinceStartup;
        while (!run.Done && (menu == null || menu.CampaignButton == null || !menu.CampaignButton.gameObject.activeInHierarchy) && Time.realtimeSinceStartup - waitMenu < 30f) {
            yield return null;
            menu = Object.FindAnyObjectByType<MainMenu>();
        }
        if (run.Done) {
            yield break;
        }
        if (menu == null || menu.CampaignButton == null) {
            run.Problem(PlaytestSeverity.Bug, "first-load: no Campaign button on the main menu");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        float menuAt = Time.realtimeSinceStartup;
        run.Event("menu-interactive", "the Campaign button is up " + menuAt.ToString("0.0") + " s after the player started; the campaign's files fetched so far: "
            + OnDemand.Fetched + " (" + (OnDemand.FetchedBytes / 1048576f).ToString("0.0") + " MB)");
        run.Metric("menuInteractiveSeconds", Mathf.Round(menuAt * 10f) / 10f);
        // The page's own clock: the download and the unpacking before Unity ran are in it (build 135 froze for a minute
        // there, unpacking Brotli in JavaScript; the build is gzip now, unpacked by the browser while it downloads).
        float pageAt = HawkesPageSeconds();
        if (pageAt > 0f) {
            float beforeUnity = Mathf.Max(0f, pageAt - menuAt);
            run.Event("page-to-menu", "the menu answers " + pageAt.ToString("0.0") + " s after the page opened; download and unpacking before Unity ran: "
                + beforeUnity.ToString("0.0") + " s");
            run.Metric("pageToMenuSeconds", Mathf.Round(pageAt * 10f) / 10f);
            run.Metric("pageBeforeUnitySeconds", Mathf.Round(beforeUnity * 10f) / 10f);
        }
        run.Metric("onDemandBeforeMenu", OnDemand.Fetched);
        if (OnDemand.Fetched > 0) {
            run.Problem(PlaytestSeverity.Bug, "first-load: campaign files were fetched before the player chose the campaign");
        }
        yield return new WaitForSecondsRealtime(1f);

        // 2. Campaign, then the first case file: the core files come down with the line up, the card opens.
        Tap(menu.CampaignButton.gameObject);
        yield return Watch(() => CampaignPanel.IsOpen, 10f, "the case files", "campaign");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(0.5f);
        int showings = OnDemandLine.Showings;
        float openAt = Time.realtimeSinceStartup;
        Tap(CampaignPanel.Instance.SlotButtons[0].gameObject);
        yield return Watch(() => ChapterScene.Instance != null && ChapterScene.Instance.CardUp && ChapterScene.Instance.BeginButton != null && SceneManager.GetActiveScene().name == CampaignSave.Scene,
            45f, "Chapter 1's card", "case-file");
        if (run.Done) {
            yield break;
        }
        float cardSeconds = Time.realtimeSinceStartup - openAt;
        run.Event("case-file-open", "the card is up " + cardSeconds.ToString("0.0") + " s after the file was tapped; progress line shown " + (OnDemandLine.Showings - showings)
            + " time(s); fetched " + OnDemand.Fetched + " files (" + (OnDemand.FetchedBytes / 1048576f).ToString("0.0") + " MB)");
        run.Metric("caseFileSeconds", Mathf.Round(cardSeconds * 10f) / 10f);
        yield return new WaitForSecondsRealtime(3.5f);

        // 3. Begin: the chapter's stills, voices and bodies, then control.
        showings = OnDemandLine.Showings;
        float beginAt = Time.realtimeSinceStartup;
        Tap(ChapterScene.Instance.BeginButton.gameObject);
        yield return Watch(() => LevelScene.Instance != null && LevelScene.Instance.Yard != null && SceneManager.GetActiveScene().name.StartsWith("Level_"), 45f, "the level scene", "chapter");
        if (run.Done) {
            yield break;
        }
        float control = Time.realtimeSinceStartup - beginAt;
        run.Scene(SceneManager.GetActiveScene().name);
        run.Event("chapter-control", "Chapter 1 gives control " + control.ToString("0.0") + " s after Begin; progress line shown " + (OnDemandLine.Showings - showings) + " time(s)");
        run.Metric("chapterControlSeconds", Mathf.Round(control * 10f) / 10f);
        if (control > 15f) {
            run.Problem(PlaytestSeverity.Bug, "first-load: Chapter 1 took " + control.ToString("0.0") + " s after Begin to give control (want 15 or less)");
        }
        yield return new WaitForSecondsRealtime(2f);

        // 4. What the downloads cost.
        bool shipped = OnDemand.Shipped;
        run.Event("on-demand", (shipped ? "shipped: " : "all in Resources: ") + OnDemand.Fetched + " files, " + (OnDemand.FetchedBytes / 1048576f).ToString("0.0") + " MB, "
            + OnDemand.Failed + " failed, longest frame while fetching " + Mathf.Round(OnDemand.LongestFrame * 1000f) + " ms, " + OnDemand.Needs + " needs; "
            + string.Join(" / ", OnDemand.Events));
        run.Metric("onDemandFiles", OnDemand.Fetched);
        run.Metric("onDemandMb", Mathf.Round(OnDemand.FetchedBytes / 104857.6f) / 10f);
        run.Metric("onDemandFailed", OnDemand.Failed);
        run.Metric("onDemandLongestFrameMs", Mathf.Round(OnDemand.LongestFrame * 1000f));
        run.Metric("castVoiceMissing", CastVoice.Missing);
        if (OnDemand.Failed > 0) {
            run.Problem(PlaytestSeverity.Bug, "first-load: " + OnDemand.Failed + " campaign file(s) did not download");
        }
        if (OnDemand.LongestFrame > 1f) {
            run.Problem(PlaytestSeverity.Bug, "first-load: the screen froze for " + OnDemand.LongestFrame.ToString("0.0") + " s while the chapter's files loaded");
        }
        if (shipped && OnDemandLine.Showings == 0) {
            run.Problem(PlaytestSeverity.Bug, "first-load: the campaign's files came down with no progress line");
        }
        run.Finish(PlaytestOutcome.Done);
    }
}
