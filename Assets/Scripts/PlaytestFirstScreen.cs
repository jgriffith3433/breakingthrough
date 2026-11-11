using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The "first-screen" scenario: the main menu on a fresh save shows the logo, Play, Campaign and Settings and nothing to read;
// one tap on Play starts Arcade; a release build ignores the = and - wave keys; after a finished run the Shop appears and opens
// the upgrades; the ?mode= address parser. On a phone viewport (mobile, or 844x390) touch is forced and every button is audited
// for size (44 CSS px), the safe area, overlaps and Play being the largest. Events first-screen, first-screen-layout,
// first-screen-play, wave-keys-release, shop-shown, shop-open, url-mode. Metrics firstScreenOverlaps, firstScreenMinTapPx,
// firstScreenPlayLargest, firstScreenPlaySeconds, firstScreenButtons.
public static class PlaytestFirstScreen {
    static PlaytestRun run;

    static void Tap(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    static Rect Box(RectTransform rect) {
        var c = new Vector3[4];
        rect.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    // Every visible button of the menu, in screen pixels.
    static List<Button> Visible(MainMenu menu) {
        var list = new List<Button>();
        foreach (Button b in menu.GetComponentsInChildren<Button>(false)) {
            if (b.isActiveAndEnabled) {
                list.Add(b);
            }
        }
        return list;
    }

    static string Label(Button b) {
        Text t = b.GetComponentInChildren<Text>();
        return t != null ? t.text : b.name;
    }

    static void Audit(MainMenu menu, string where, bool strict = true) {
        List<Button> buttons = Visible(menu);
        float px = UiFit.PixelRatio;
        Rect safe = UnityEngine.Screen.safeArea;
        int overlaps = 0;
        int outside = 0;
        int small = 0;
        float minTap = 9999f;
        float playArea = 0f;
        float biggestOther = 0f;
        var names = new List<string>();
        for (int i = 0; i < buttons.Count; i++) {
            Rect a = Box((RectTransform)buttons[i].transform);
            names.Add(Label(buttons[i]));
            float css = Mathf.Min(a.width, a.height) / px;
            minTap = Mathf.Min(minTap, css);
            if (css < 44f) {
                small++;
            }
            if (a.xMin < safe.xMin - 1.5f || a.xMax > safe.xMax + 1.5f || a.yMin < safe.yMin - 1.5f || a.yMax > safe.yMax + 1.5f) {
                outside++;
            }
            float area = a.width * a.height;
            if (buttons[i] == menu.PlayButton) {
                playArea = area;
            } else {
                biggestOther = Mathf.Max(biggestOther, area);
            }
            for (int j = i + 1; j < buttons.Count; j++) {
                if (a.Overlaps(Box((RectTransform)buttons[j].transform))) {
                    overlaps++;
                }
            }
        }
        bool largest = playArea > biggestOther;
        run.Event("first-screen-layout", where + " " + UiFit.Mode + " " + UnityEngine.Screen.width + "x" + UnityEngine.Screen.height + ": " + buttons.Count + " buttons (" + string.Join(", ", names.ToArray()) + "), " + overlaps + " overlaps, " + small + " under 44 px (smallest " + Mathf.Round(minTap) + "), " + outside + " outside the safe area, Play largest " + largest);
        run.Metric("firstScreenOverlaps", overlaps);
        run.Metric("firstScreenMinTapPx", Mathf.Round(minTap));
        run.Metric("firstScreenPlayLargest", largest ? 1 : 0);
        run.Metric("firstScreenButtons", buttons.Count);
        if (!strict) {
            return;
        }
        if (overlaps > 0) {
            run.Problem(PlaytestSeverity.Bug, "first-screen: " + overlaps + " buttons overlap at " + where);
        }
        if (small > 0) {
            run.Problem(PlaytestSeverity.Bug, "first-screen: " + small + " buttons are under 44 px at " + where);
        }
        if (outside > 0) {
            run.Problem(PlaytestSeverity.Bug, "first-screen: " + outside + " buttons leave the safe area at " + where);
        }
        if (!largest) {
            run.Problem(PlaytestSeverity.Bug, "first-screen: Play is not the largest button at " + where);
        }
    }

    // Words on the menu canvas that are showing: the logo, the buttons' own labels and nothing else.
    static string ReadWords(MainMenu menu) {
        var words = new List<string>();
        foreach (Text t in menu.GetComponentsInChildren<Text>(false)) {
            if (t.isActiveAndEnabled && !string.IsNullOrEmpty(t.text) && t.GetComponentInParent<Button>() == null) {
                words.Add(t.text);
            }
        }
        return string.Join(" | ", words.ToArray());
    }

    public static IEnumerator Run(PlaytestRun current) {
        run = current;
        MetaProgress.EnterSandbox(true);
        CampaignSave.EnterSandbox();
        bool phone = UnityEngine.Screen.width <= 900 || UnityEngine.Screen.height <= 900 && UnityEngine.Screen.width < UnityEngine.Screen.height * 2;
        if (UnityEngine.Screen.height < 600 || UnityEngine.Screen.width < 600) {
            phone = true;
        }
        if (phone) {
            UiFit.ForceTouch(true);
        }
        run.Scene("Main Menu");
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.PlayButton == null) {
            run.Problem(PlaytestSeverity.Bug, "first-screen: the main menu has no Play button");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);

        // The address parser, on samples (the page's own address is whatever the runner loaded).
        string[] samples = { "https://x.test/Game/?mode=arcade", "https://x.test/Game/?a=1&mode=campaign#top", "https://x.test/Game/", "https://x.test/Game/?mode=other", "https://x.test/Game/?playtest=smoke" };
        string[] want = { "arcade", "campaign", "", "", "" };
        var got = new List<string>();
        for (int i = 0; i < samples.Length; i++) {
            string mode = MainMenu.ParseMode(samples[i]);
            got.Add(mode.Length == 0 ? "menu" : mode);
            if (mode != want[i]) {
                run.Problem(PlaytestSeverity.Bug, "first-screen: ?mode= parsing read \"" + mode + "\" from " + samples[i]);
            }
        }
        run.Event("url-mode", "samples read as " + string.Join(", ", got.ToArray()) + "; this page's own mode is \"" + MainMenu.UrlMode() + "\"");

        // The fresh menu: logo, Play, Campaign, Settings. No Shop, no hint, no quote.
        bool fresh = MetaProgress.RunsFinished == 0 && MetaProgress.BestWave == 0 && string.IsNullOrEmpty(WaveDirector.LastRun);
        run.Event("first-screen", "fresh save " + fresh + ": buttons " + string.Join(", ", Visible(menu).ConvertAll(Label).ToArray()) + "; other words: " + ReadWords(menu) + "; Shop " + (menu.ShopButton == null ? "absent" : "shown"));
        if (fresh && menu.ShopButton != null) {
            run.Problem(PlaytestSeverity.Bug, "first-screen: the Shop shows before a first finished run");
        }
        Audit(menu, "fresh menu");
        yield return new WaitForSecondsRealtime(1f);

        // One tap on Play.
        float tapped = Time.realtimeSinceStartup;
        Tap(menu.PlayButton.gameObject);
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.GameScene && WaveDirector.Instance != null, 20f, "Arcade after one tap on Play");
        if (run.Done) {
            yield break;
        }
        float seconds = Time.realtimeSinceStartup - tapped;
        run.Metric("firstScreenPlaySeconds", Mathf.Round(seconds * 100f) / 100f);
        run.Event("first-screen-play", "one tap on Play: Arcade wave " + WaveDirector.Wave + " in " + seconds.ToString("0.00") + " s (wanted under 2)");
        if (seconds > 2f) {
            run.Problem(PlaytestSeverity.Bug, "first-screen: Play took " + seconds.ToString("0.0") + " s to start the run");
        }

        // A release build ignores = and -.
        yield return new WaitForSecondsRealtime(1.5f);
        WaveDirector.ReleaseKeysProbe = true;
        int before = WaveDirector.Wave;
        PlayInput.Tap(KeyCode.Equals);
        yield return new WaitForSecondsRealtime(0.4f);
        PlayInput.Tap(KeyCode.Minus);
        yield return new WaitForSecondsRealtime(0.4f);
        int after = WaveDirector.Wave;
        WaveDirector.ReleaseKeysProbe = false;
        run.Event("wave-keys-release", "wave " + before + " before, " + after + " after = and - in a release build");
        run.Metric("waveKeysIgnored", before == after ? 1 : 0);
        if (before != after) {
            run.Problem(PlaytestSeverity.Bug, "first-screen: = or - moved the wave (" + before + " to " + after + ") in a release build");
        }

        // Setup shortcut: one run counts as finished, then the menu again.
        run.Event("setup", "one finished run recorded in the sandbox");
        MetaProgress.NoteRunFinished();
        Time.timeScale = 1f;
        SceneManager.LoadScene(MetaProgress.MenuScene);
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.MenuScene && Object.FindAnyObjectByType<MainMenu>() != null, 30f, "the menu after the run");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1f);
        menu = Object.FindAnyObjectByType<MainMenu>();
        run.Event("shop-shown", "after a finished run Shop is " + (menu.ShopButton != null && menu.ShopButton.isActiveAndEnabled ? "shown" : "absent"));
        if (menu.ShopButton == null) {
            run.Problem(PlaytestSeverity.Bug, "first-screen: no Shop button after a finished run");
            yield break;
        }
        Audit(menu, "menu with Shop");
        Tap(menu.ShopButton.gameObject);
        yield return new WaitForSecondsRealtime(0.5f);
        int rows = 0;
        foreach (Text t in menu.GetComponentsInChildren<Text>(false)) {
            for (int i = 0; i < MetaProgress.Catalog.Length; i++) {
                if (t.isActiveAndEnabled && t.text == MetaProgress.Catalog[i].Name) {
                    rows++;
                }
            }
        }
        run.Event("shop-open", "Shop " + (menu.ShopIsOpen ? "open" : "closed") + ", " + rows + " upgrade forms showing");
        if (!menu.ShopIsOpen || rows == 0) {
            run.Problem(PlaytestSeverity.Bug, "first-screen: Shop did not open the upgrades");
        }
        Audit(menu, "shop", false);
        yield return new WaitForSecondsRealtime(1f);
        run.Finish(PlaytestOutcome.Done);
    }
}
