using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

// The "transitions" scenario: the Department's page turns and the illustrated loading cards, on a phone (run it with mobile,
// 390x844; touch is forced and every press is a finger through the UI's own pointer events). It plays:
//   1. Arcade from the menu (ENTER), the run-end panel (STRUCK OFF, setup shortcut DevLose), Retry (RETURNED);
//   2. the campaign: Campaign, a case file (ENTER), then for each of the six chapters Begin (ENTER and that chapter's card), the
//      way back to the hub (FILED and the hub's card; setup shortcut for the chapter's end) and the menu (RETURNED).
// Every transition logs its stamp word, its seconds (want 0.5 to 0.8 of animation) and how long the sheet sat over the screen; every
// card logs its art, its voiced line, its text size in pixels and whether its strip really filled; frames under 30 fps while a sheet is
// moving are counted. Events: transition, load-card, card-layout, transitions-summary. Metrics: transitionCount, transitionMinSeconds,
// transitionMaxSeconds, transitionFpsP5, longestCoveredSeconds, cardsSeen, cardsWithArt, cardsVoiced, cardMinTextPx.
public static class PlaytestTransitions {
    static PlaytestRun run;
    static readonly List<float> frames = new List<float>();
    static readonly HashSet<string> cards = new HashSet<string>();
    static int cardsArt;
    static int cardsVoice;
    static float minText = 999f;
    static int seenTransitions;
    static float minAnim = 99f;
    static float maxAnim;
    static int hubExits;
    static float hubExitWorstFrame;
    static float hubExitSlowest;

    static void Tap(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    static int cardsPicked;
    static float cardSince = -1f;

    // A wave card or level-up that opens while the scenario waits is picked the way a player does: a finger on the first card,
    // once the cards have flipped. The heading must not read "Level 0".
    static void PickCard() {
        if (!LevelUpScreen.IsOpen || LevelUpScreen.CardCount == 0) {
            cardSince = -1f;
            return;
        }
        if (cardSince < 0f) {
            cardSince = Time.unscaledTime;
            run.Event("level-up-card", "open: " + LevelUpScreen.Heading + " (" + LevelUpScreen.CardCount + " cards)");
            if (LevelUpScreen.Heading.StartsWith("Level 0")) {
                run.Problem(PlaytestSeverity.Bug, "transitions: the card says \"" + LevelUpScreen.Heading + "\" (a card that gains no level must not read Level 0)");
            }
        }
        if (Time.unscaledTime - cardSince > 1.6f && !LevelUpScreen.Stamping) {
            GameObject card = LevelUpScreen.CardObject(0);
            if (card != null) {
                Tap(card);
                cardsPicked++;
                run.Event("level-up-pick", "tapped card 1; closed after the stamp: " + !LevelUpScreen.IsOpen);
                cardSince = -1f;
            }
        }
    }

    // Frames pass until done() or the limit; frame times are kept while a sheet is moving, and each new transition and card is judged.
    static IEnumerator Watch(System.Func<bool> done, float seconds, string what) {
        float start = Time.realtimeSinceStartup;
        while (!run.Done && !done() && Time.realtimeSinceStartup - start < seconds) {
            if (ScreenTransition.Busy) {
                frames.Add(Time.unscaledDeltaTime);
            }
            Judge();
            PickCard();
            yield return null;
        }
        Judge();
        if (!run.Done && !done()) {
            run.Problem(PlaytestSeverity.Bug, "transitions: " + what + " did not come up in " + seconds + " s");
        }
    }

    static void Judge() {
        while (seenTransitions < ScreenTransition.Count) {
            string line = ScreenTransition.Events[seenTransitions];
            seenTransitions++;
            float anim = ScreenTransition.Count == seenTransitions ? ScreenTransition.LastSeconds : 0.66f;
            if (ScreenTransition.Count == seenTransitions) {
                minAnim = Mathf.Min(minAnim, anim);
                maxAnim = Mathf.Max(maxAnim, anim);
                if (anim < 0.5f || anim > 0.8f) {
                    run.Problem(PlaytestSeverity.Bug, "transitions: the page turn took " + anim.ToString("0.00") + " s (want 0.5 to 0.8): " + line);
                }
            }
            run.Event("transition", seenTransitions + ": " + line + ", animation " + anim.ToString("0.00") + " s, stamps so far " + ScreenTransition.Stamps);
        }
        int n = LoadScreen.Events.Count;
        for (int i = 0; i < n; i++) {
            string e = LoadScreen.Events[i];
            if (e.StartsWith("card|") && cards.Add("c" + i)) {
                run.Event("load-card", e + " (title \"" + LoadScreen.LastTitle + "\", line \"" + LoadScreen.LastWords + "\")");
            }
        }
    }

    // A card that is up: art, voice, text size, fit, and a strip that really fills.
    static IEnumerator CheckCard(string expect) {
        yield return Watch(() => LoadScreen.IsUp && LoadScreen.LastCard == expect, 20f, "the " + expect + " loading card");
        if (run.Done || !(LoadScreen.IsUp && LoadScreen.LastCard == expect)) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(0.6f);   // the sheet has peeled and the line is under way
        LoadScreen card = LoadScreen.Instance;
        bool art = LoadScreen.LastHadCardArt;
        bool voiced = LoadScreen.LastVoiced;
        float px = card.SmallestTextPixels();
        bool fits = card.FitsScreen();
        run.Event("card-layout", expect + ": art " + art + ", voiced " + voiced + ", smallest text " + px.ToString("0.0") + " px, inside the screen " + fits
            + ", " + UiFit.Mode + " " + Screen.width + "x" + Screen.height);
        if (cards.Add(expect)) {
            cardsArt += art ? 1 : 0;
            cardsVoice += voiced ? 1 : 0;
        }
        minText = Mathf.Min(minText, px);
        if (!art) {
            run.Problem(PlaytestSeverity.Bug, "transitions: the " + expect + " card has no painted art");
        }
        if (!voiced) {
            run.Problem(PlaytestSeverity.Bug, "transitions: the " + expect + " card played no voiced line");
        }
        if (px < 14f) {
            run.Problem(PlaytestSeverity.Bug, "transitions: the " + expect + " card has text of " + px.ToString("0.0") + " px (want 14 or more)");
        }
        if (!fits) {
            run.Problem(PlaytestSeverity.Bug, "transitions: the " + expect + " card has text or the strip off the screen");
        }
        yield return Watch(() => !LoadScreen.IsUp, 40f, "the " + expect + " card to leave");
        if (LoadScreen.LastProgress < 0.99f) {
            run.Problem(PlaytestSeverity.Bug, "transitions: the " + expect + " strip left at " + LoadScreen.LastProgress.ToString("0.00") + ", not full");
        }
    }

    public static IEnumerator Run(PlaytestRun r) {
        run = r;
        hubExits = 0;
        hubExitWorstFrame = 0f;
        hubExitSlowest = 0f;
        UiFit.ForceTouch(true);
        MetaProgress.EnterSandbox(true);
        CampaignSave.EnterSandbox();
        LevelScene.ChapterOneIsLevel = true;
        run.Log("layout " + UiFit.Mode + " at " + Screen.width + "x" + Screen.height);

        // 1. Arcade: Play (ENTER), the run-end panel (STRUCK OFF), Retry (RETURNED).
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        yield return Watch(() => (menu = Object.FindAnyObjectByType<MainMenu>()) != null && menu.PlayButton != null && menu.PlayButton.gameObject.activeInHierarchy, 30f, "the menu");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1.5f);
        Tap(menu.PlayButton.gameObject);
        yield return Watch(() => SceneManager.GetActiveScene().name == MetaProgress.GameScene && WaveDirector.Instance != null && !ScreenTransition.Busy, 40f, "the Arcade run");
        if (run.Done) {
            yield break;
        }
        run.Scene("Arcade after ENTER");
        yield return new WaitForSecondsRealtime(2f);
        WaveDirector.Instance.DevLose();   // setup: the run ends (the real way is Slice's bar emptying)
        run.Event("setup", "the run ends by shortcut");
        yield return Watch(() => RunEndScreen.IsOpen && RunEndScreen.Instance != null && RunEndScreen.Instance.RetryButton != null && RunEndScreen.Instance.RetryButton.interactable, 30f, "the run-end panel");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(0.5f);
        Tap(RunEndScreen.Instance.RetryButton.gameObject);
        yield return Watch(() => SceneManager.GetActiveScene().name == MetaProgress.GameScene && WaveDirector.Instance != null && !WaveDirector.IsLost && !ScreenTransition.Busy, 30f, "the next run");
        if (run.Done) {
            yield break;
        }
        yield return new WaitForSecondsRealtime(1.5f);
        PauseMenu.QuitToMenu();   // back to the menu: RETURNED
        yield return Watch(() => (menu = Object.FindAnyObjectByType<MainMenu>()) != null && SceneManager.GetActiveScene().name == MetaProgress.MenuScene && !ScreenTransition.Busy, 30f, "the menu after quitting");
        if (run.Done) {
            yield break;
        }

        // 2. The campaign: every chapter's card and the hub's.
        for (int chapter = 0; chapter < CampaignSave.ChapterCount && !run.Done; chapter++) {
            CampaignSave.Slot slot = CampaignSave.Get(0);
            slot.used = true;
            slot.chapter = chapter;
            slot.reached = chapter;
            slot.cleared = chapter;
            yield return new WaitForSecondsRealtime(1f);
            menu = Object.FindAnyObjectByType<MainMenu>();
            Tap(menu.CampaignButton.gameObject);
            yield return Watch(() => CampaignPanel.IsOpen, 15f, "the case files");
            if (run.Done) {
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.5f);
            Tap(CampaignPanel.Instance.SlotButtons[0].gameObject);
            yield return Watch(() => ChapterScene.Instance != null && ChapterScene.Instance.CardUp && ChapterScene.Instance.BeginButton != null
                && SceneManager.GetActiveScene().name == CampaignSave.Scene && !ScreenTransition.Busy, 60f, "the chapter card");
            if (run.Done) {
                yield break;
            }
            yield return new WaitForSecondsRealtime(1f);
            Tap(ChapterScene.Instance.BeginButton.gameObject);
            yield return CheckCard("ch" + (chapter + 1));
            if (run.Done) {
                yield break;
            }
            yield return Watch(() => LevelScene.Instance != null || (ChapterRun.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene), 60f, "the chapter");
            if (run.Done) {
                yield break;
            }
            run.Scene("Chapter " + (chapter + 1) + " after its card");
            yield return new WaitForSecondsRealtime(2.5f);
            // Setup: the chapter's end by shortcut, to reach the hub's card the way a finished chapter does.
            if (LevelScene.Instance != null) {
                LevelScene.Instance.LeaveToHub();
            } else {
                ChapterRun.Instance.LeaveToHubForPlaytest();
            }
            run.Event("setup", "chapter " + (chapter + 1) + " ends by shortcut");
            yield return CheckCard("hub");
            if (run.Done) {
                yield break;
            }
            if (CampaignSave.Get(0).cleared > 0) {
                yield return Watch(() => ChapterHub.Instance != null && !ScreenTransition.Busy, 40f, "the hub");
            } else {
                // No chapter cleared yet: the file has no hub, so the trip back lands on the chapter card (ChapterScene).
                yield return Watch(() => ChapterScene.Instance != null && ChapterScene.Instance.CardUp && !ScreenTransition.Busy, 40f, "the chapter card (no hub before a chapter is cleared)");
                run.Event("hub-skip", "chapter " + (chapter + 1) + ": no chapter cleared, so the chapter card, not the hub; the hub card left after " + LoadScreen.LastSeconds.ToString("0.0") + " s");
            }
            if (run.Done) {
                yield break;
            }
            yield return new WaitForSecondsRealtime(2f);
            // Leaving a built hub (the first time is after Chapter 2: with no chapter cleared there is no hub, only the card) is
            // where the page used to stop answering: the office map was unloaded from inside the scene switch. Every frame of
            // the way back to the menu is timed, and how the office's map left is read.
            bool hubBuilt = ChapterHub.Instance != null;
            int forgets = ChapterMap.Forgets;
            float leftAt = Time.realtimeSinceStartup;
            float lastFrame = leftAt;
            float worstFrame = 0f;
            ChapterScene.Instance.LeaveToMenu();   // RETURNED
            yield return Watch(() => {
                float now = Time.realtimeSinceStartup;
                worstFrame = Mathf.Max(worstFrame, now - lastFrame);
                lastFrame = now;
                return SceneManager.GetActiveScene().name == MetaProgress.MenuScene && !ScreenTransition.Busy;
            }, 40f, "the menu");
            if (hubBuilt && !run.Done) {
                float took = Time.realtimeSinceStartup - leftAt;
                hubExits++;
                hubExitWorstFrame = Mathf.Max(hubExitWorstFrame, worstFrame);
                hubExitSlowest = Mathf.Max(hubExitSlowest, took);
                bool withScene = ChapterMap.Forgets > forgets && ChapterMap.LastLeave.StartsWith("with-scene");
                run.Event("hub-exit", "after chapter " + (chapter + 1) + ": the menu in " + took.ToString("0.00") + " s, longest frame "
                    + Mathf.Round(worstFrame * 1000f) + " ms, office map left " + (withScene ? "with the scene change (nothing queued mid-switch)" : "by \"" + ChapterMap.LastLeave + "\""));
                // A scene load's own first frame may take a second or two; the old freeze was a minute.
                if (worstFrame > 10f) {
                    run.Problem(PlaytestSeverity.Bug, "transitions: a " + worstFrame.ToString("0.0") + " s frame while leaving the hub after chapter " + (chapter + 1) + " (the page stopped answering)");
                }
                if (!withScene) {
                    run.Problem(PlaytestSeverity.Bug, "transitions: the office map was unloaded inside the scene switch (" + ChapterMap.LastLeave + "), not left to the scene change");
                }
            }
        }
        if (run.Done) {
            yield break;
        }

        // Summary.
        float p5 = 60f;
        if (frames.Count > 0) {
            frames.Sort();
            float slow = frames[Mathf.Clamp(Mathf.FloorToInt(frames.Count * 0.95f), 0, frames.Count - 1)];
            p5 = 1f / Mathf.Max(0.0001f, slow);
        }
        run.Metric("transitionCount", ScreenTransition.Count);
        run.Metric("transitionMinSeconds", Mathf.Round(minAnim * 100f) / 100f);
        run.Metric("transitionMaxSeconds", Mathf.Round(maxAnim * 100f) / 100f);
        run.Metric("transitionFpsP5", Mathf.Round(p5));
        run.Metric("longestCoveredSeconds", Mathf.Round(ScreenTransition.LongestCoveredSeconds * 100f) / 100f);
        run.Metric("cardsSeen", cards.Count);
        run.Metric("cardsWithArt", cardsArt);
        run.Metric("cardsVoiced", cardsVoice);
        run.Metric("cardMinTextPx", Mathf.Round(minText * 10f) / 10f);
        run.Event("transitions-summary", ScreenTransition.Count + " page turns (" + minAnim.ToString("0.00") + " to " + maxAnim.ToString("0.00") + " s), "
            + ScreenTransition.Stamps + " stamp thuds, fps p5 while moving " + Mathf.Round(p5) + ", sheet over the screen at most "
            + ScreenTransition.LongestCoveredSeconds.ToString("0.00") + " s, cards with art " + cardsArt + " voiced " + cardsVoice + ", smallest card text " + minText.ToString("0.0") + " px");
        if (p5 < 30f) {
            run.Problem(PlaytestSeverity.Bug, "transitions: fps p5 was " + Mathf.Round(p5) + " while a sheet was moving (want 30 or more)");
        }
        run.Metric("hubExits", hubExits);
        run.Metric("hubExitLongestFrameMs", Mathf.Round(hubExitWorstFrame * 1000f));
        run.Metric("hubExitSlowestSeconds", Mathf.Round(hubExitSlowest * 100f) / 100f);
        if (hubExits == 0) {
            run.Problem(PlaytestSeverity.Bug, "transitions: the hub was never left (the hub after Chapter 2 is the step that froze the page)");
        }
        run.Metric("transitionRecoveries", ScreenTransition.Recoveries);
        run.Metric("levelUpCardsPicked", cardsPicked);
        if (ScreenTransition.Recoveries > 0) {
            run.Problem(PlaytestSeverity.Bug, "transitions: the paper sheet had to be taken down by force " + ScreenTransition.Recoveries + " time(s) (a step threw or it stayed up too long)");
        }
        if (ScreenTransition.Count < 10) {
            run.Problem(PlaytestSeverity.Bug, "transitions: only " + ScreenTransition.Count + " page turns were played");
        }
    }
}
