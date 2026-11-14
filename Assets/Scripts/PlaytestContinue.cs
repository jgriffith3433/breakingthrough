using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The "continue-run" scenario: Arcade's saved run. Play to the end of wave 3 (setup: the waves' bots are felled with
// Enemy.Hit, cards are taken by tap or key), leave for the menu (the sandbox keeps the snapshot in memory, the way a reload
// finds it in PlayerPrefs), check "Continue run (wave 4)" is there, press it and check wave 4 starts with the same boons,
// health and bolts. Then Slice falls (setup: WaveDirector.DevLose): the snapshot is gone and the menu has no Continue card.
// On a phone (390x844 touch) every press is a finger on the button and the card is measured: 44 px or taller, in the lower
// half. Events: continue-card, continue-resume, continue-cleared. Metrics: run.continueWave, run.continueCardPx,
// run.continueCardCenterPct, run.continueTaps.
public static class PlaytestContinue {
    static PlaytestRun run;
    static bool phone;

    static void Tap(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    static int BoonTotal() {
        int total = 0;
        foreach (int rank in RunBoons.RanksCopy()) {
            total += rank;
        }
        return total;
    }

    static string State() {
        int health = Musician.Instance != null ? Musician.Instance.Health : -1;
        int max = Musician.Instance != null ? Musician.Instance.MaxHealth : -1;
        return "boons=" + string.Join(",", RunBoons.RanksCopy()) + " health=" + health + "/" + max + " level=" + RunBoons.Level + " bolts=" + RunBoons.Feathers + " rerolls=" + RunBoons.Rerolls;
    }

    static IEnumerator ToMenu() {
        Time.timeScale = 1f;
        SceneManager.LoadScene(MetaProgress.MenuScene);
        yield return null;
        yield return run.Until(() => Object.FindAnyObjectByType<MainMenu>() != null && Object.FindAnyObjectByType<MainMenu>().Ready, 30f, "the main menu");
        yield return new WaitForSecondsRealtime(1.2f);
    }

    static IEnumerator TakeCards() {
        float quiet = 0f;
        float until = Time.realtimeSinceStartup + 25f;
        while (!run.Done && quiet < 2f && Time.realtimeSinceStartup < until) {
            if (LevelUpScreen.IsOpen && LevelUpScreen.CardCount > 0) {
                quiet = 0f;
                if (phone) {
                    Tap(LevelUpScreen.CardObject(0));
                    run.Event("tap", "card 1");
                } else {
                    PlayInput.Tap(KeyCode.Alpha1);
                    run.Event("key", "1");
                }
                yield return new WaitForSecondsRealtime(0.8f);
            } else {
                quiet += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }

    public static IEnumerator Run(PlaytestRun r) {
        run = r;
        phone = Screen.width <= 900 || Screen.height <= 500;
        if (phone) {
            UiFit.ForceTouch(true);
            MobileQuality.Apply();
        }
        MetaProgress.EnterSandbox(true);
        RunSave.ForgetMemory();
        run.Log("continue-run on " + (phone ? "a phone" : "desktop") + " " + Screen.width + "x" + Screen.height + ", layout " + UiFit.Mode);

        // No snapshot yet: the menu has no card.
        yield return ToMenu();
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.PlayButton == null) {
            run.Problem(PlaytestSeverity.Bug, "continue-run: no menu");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        if (menu.ContinueButton != null) {
            run.Problem(PlaytestSeverity.Bug, "continue-run: a Continue run card shows before any run was saved");
        }
        run.Event("continue-card", "fresh menu: card " + (menu.ContinueButton != null ? "present" : "absent"));

        // Play wave 1 to the end of wave 3.
        if (phone) {
            Tap(menu.PlayButton.gameObject);
            run.Event("tap", "Play");
        } else {
            PlayInput.Tap(KeyCode.Return);
            run.Event("key", "Enter");
        }
        yield return run.Until(() => WaveDirector.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the game scene");
        if (run.Done) {
            yield break;
        }
        run.Scene("Wave 1");
        float until = Time.realtimeSinceStartup + 240f;
        run.Event("setup", "waves 1-3: the bots are felled with Enemy.Hit; cards are taken by " + (phone ? "tap" : "key"));
        while (!run.Done && WaveDirector.Wave < 4 && Time.realtimeSinceStartup < until) {
            if (LevelUpScreen.IsOpen) {
                yield return TakeCards();
            }
            foreach (Enemy enemy in new List<Enemy>(Enemy.Roster)) {
                if (enemy != null && enemy.Alive) {
                    enemy.Hit(1000f);
                }
            }
            yield return null;
        }
        if (run.Done) {
            yield break;
        }
        if (WaveDirector.Wave < 4) {
            run.Problem(PlaytestSeverity.Bug, "continue-run: wave 3 never cleared in 240 s");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        yield return TakeCards();
        if (!RunSave.Exists || RunSave.Wave != 4) {
            run.Problem(PlaytestSeverity.Bug, "continue-run: no snapshot for wave 4 after wave 3 cleared (found wave " + RunSave.Wave + ")");
        }
        string before = State();
        int boonsBefore = BoonTotal();
        int healthBefore = Musician.Instance.Health;
        int boltsBefore = RunBoons.Feathers;
        int levelBefore = RunBoons.Level;
        run.Event("continue-saved", "wave 3 cleared, snapshot wave " + RunSave.Wave + ": " + before);
        run.Metric("run.continueWave", RunSave.Wave);

        // "Reload": back to the menu, then the card.
        yield return ToMenu();
        menu = Object.FindAnyObjectByType<MainMenu>();
        Button card = menu != null ? menu.ContinueButton : null;
        if (card == null || !card.isActiveAndEnabled) {
            run.Problem(PlaytestSeverity.Bug, "continue-run: no Continue run card on the menu with a saved run");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        Text label = card.GetComponentInChildren<Text>();
        string words = label != null ? label.text : "";
        var c = new Vector3[4];
        ((RectTransform)card.transform).GetWorldCorners(c);
        float ratio = UiFit.PixelRatio;
        float heightPx = (c[2].y - c[0].y) / ratio;
        float centerPct = (c[0].y + c[2].y) * 0.5f / Screen.height * 100f;
        run.Metric("run.continueCardPx", Mathf.Round(heightPx));
        run.Metric("run.continueCardCenterPct", Mathf.Round(centerPct));
        run.Event("continue-card", "\"" + words + "\", " + Mathf.Round((c[2].x - c[0].x) / ratio) + "x" + Mathf.Round(heightPx) + " css px, centre " + Mathf.Round(centerPct) + "% up the screen");
        if (words != "Continue run (wave 4)") {
            run.Problem(PlaytestSeverity.Bug, "continue-run: the card reads \"" + words + "\", not \"Continue run (wave 4)\"");
        }
        if (heightPx < 44f || c[0].y < 0f || c[2].y > Screen.height) {
            run.Problem(PlaytestSeverity.Bug, "continue-run: the Continue run card is " + Mathf.Round(heightPx) + " px tall or off screen; it needs 44 px or more, all on screen");
        }
        if (phone && centerPct > 50f) {
            run.Problem(PlaytestSeverity.Bug, "continue-run: the Continue run card is in the upper half of the screen (centre " + Mathf.Round(centerPct) + "%)");
        }

        // One tap starts the saved wave.
        if (phone) {
            Tap(card.gameObject);
            run.Event("tap", "Continue run");
        } else {
            Tap(card.gameObject);
            run.Event("click", "Continue run");
        }
        run.Metric("run.continueTaps", 1);
        yield return run.Until(() => WaveDirector.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the game scene after Continue run");
        if (run.Done) {
            yield break;
        }
        yield return null;
        yield return null;
        string after = State();
        bool same = BoonTotal() == boonsBefore && Musician.Instance.Health == healthBefore && RunBoons.Feathers == boltsBefore && RunBoons.Level == levelBefore;
        run.Event("continue-resume", "wave " + WaveDirector.Wave + " from the snapshot. before: " + before + " | after: " + after);
        if (WaveDirector.Wave != 4) {
            run.Problem(PlaytestSeverity.Bug, "continue-run: Continue run started wave " + WaveDirector.Wave + ", not wave 4");
        }
        if (!same) {
            run.Problem(PlaytestSeverity.Bug, "continue-run: the resumed run differs from the saved one (" + before + " vs " + after + ")");
        }
        run.Scene("Wave 4");
        yield return new WaitForSecondsRealtime(4f);
        if (WaveDirector.Wave != 4) {
            run.Problem(PlaytestSeverity.Bug, "continue-run: the resumed wave moved off wave 4 before it was fought");
        }

        // A lost run clears the card.
        run.Event("setup", "Slice falls (WaveDirector.DevLose)");
        WaveDirector.Instance.DevLose();
        yield return new WaitForSecondsRealtime(0.5f);
        run.Event("continue-cleared", "after the fall: snapshot " + (RunSave.Exists ? "still there" : "gone"));
        if (RunSave.Exists) {
            run.Problem(PlaytestSeverity.Bug, "continue-run: the snapshot survived a lost run");
        }
        yield return ToMenu();
        menu = Object.FindAnyObjectByType<MainMenu>();
        bool gone = menu != null && menu.ContinueButton == null;
        run.Event("continue-cleared", "menu after the fall: card " + (gone ? "absent" : "present"));
        if (!gone) {
            run.Problem(PlaytestSeverity.Bug, "continue-run: the Continue run card is still on the menu after a lost run");
        }
    }
}
