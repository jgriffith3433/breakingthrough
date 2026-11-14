using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The "upgrade-pick" scenario: the picker after wave 1 (three boons, three families, one readable line each), each card on screen
// and tappable, the pick showing on the bat's trail in wave 2, and the run-end rank with the numbers behind it. Touch is forced
// (taps only), so it is the 390x844 phone check when run with the mobile viewport. Setup shortcuts, named in events: wave 1's
// bots are struck off, and Slice is struck down at the end.
public static class PlaytestPick {
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

    public static IEnumerator Run(PlaytestRun run) {
        UiFit.ForceTouch(true);
        CampaignSave.EnterSandbox();
        MetaProgress.EnterSandbox(true);
        yield return new WaitForSecondsRealtime(1f);
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.PlayButton == null) {
            run.Problem(PlaytestSeverity.Bug, "upgrade-pick: no Play button on the menu");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        Tap(menu.PlayButton.gameObject);
        yield return run.Until(() => WaveDirector.Instance != null && SceneManager.GetActiveScene().name == MetaProgress.GameScene, 60f, "the game scene");
        if (run.Done) {
            yield break;
        }
        run.Scene("Arcade wave 1");
        Color plain = BatEquipment.TrailHead;

        run.Event("setup", "wave 1's bots are struck off until the wave is cleared");
        float until = Time.realtimeSinceStartup + 120f;
        while (!LevelUpScreen.IsOpen && Time.realtimeSinceStartup < until && !run.Done) {
            var copy = new List<Enemy>(Enemy.Roster);
            foreach (Enemy e in copy) {
                if (e != null && e.Alive) {
                    e.Damage(9999f, DamageKind.Bat, false);
                }
            }
            yield return new WaitForSecondsRealtime(0.4f);
        }
        if (run.Done) {
            yield break;
        }
        if (!LevelUpScreen.IsOpen) {
            run.Problem(PlaytestSeverity.Bug, "upgrade-pick: the picker did not open after wave 1");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        yield return new WaitForSecondsRealtime(1.6f);   // the cards deal face down, then flip

        List<BoonId> offered = LevelUpScreen.Offered;
        var families = new HashSet<BoonFamily>();
        var names = new List<string>();
        for (int i = 0; i < offered.Count; i++) {
            BoonInfo info = RunBoons.Info(offered[i]);
            BoonFamily f = RunBoons.FamilyOf(offered[i]);
            families.Add(f);
            names.Add(RunBoons.FamilyName(f) + ": " + info.Name + " (" + info.About + ")");
        }
        run.Event("pick-offer", LevelUpScreen.Heading + " | " + string.Join(" | ", names.ToArray()));
        run.Metric("pickFamilies", families.Count);
        if (offered.Count != 3 || families.Count != 3 || families.Contains(BoonFamily.None)) {
            run.Problem(PlaytestSeverity.Bug, "upgrade-pick: the first picker offered " + offered.Count + " boons from " + families.Count + " families");
        }

        Rect safe = Screen.safeArea;
        float px = Mathf.Max(1f, UiFit.PixelRatio);
        float minTap = 9999f;
        int outside = 0;
        for (int i = 0; i < 3; i++) {
            GameObject card = LevelUpScreen.CardObject(i);
            if (card == null || !card.activeInHierarchy) {
                outside++;
                continue;
            }
            Rect r = Box((RectTransform)card.transform);
            minTap = Mathf.Min(minTap, Mathf.Min(r.width, r.height) / px);
            if (r.xMin < safe.xMin - 1.5f || r.xMax > safe.xMax + 1.5f || r.yMin < safe.yMin - 1.5f || r.yMax > safe.yMax + 1.5f) {
                outside++;
            }
        }
        run.Event("pick-fit", UiFit.Mode + " " + Screen.width + "x" + Screen.height + ": " + outside + " cards outside the safe area, smallest card " + Mathf.Round(minTap) + " px");
        run.Metric("pickMinTapPx", Mathf.Round(minTap));
        if (outside > 0 || minTap < 44f) {
            run.Problem(PlaytestSeverity.Bug, "upgrade-pick: a card is off screen or under 44 px (" + outside + " outside, " + minTap + " px)");
        }

        // Tap the first card. The picker takes the tap once its cards are dealt.
        int picks = RunBoons.Picks;
        BoonId chosen = offered.Count > 0 ? offered[0] : BoonId.Heavy;
        float tapUntil = Time.realtimeSinceStartup + 10f;
        while (LevelUpScreen.IsOpen && Time.realtimeSinceStartup < tapUntil) {
            GameObject card = LevelUpScreen.CardObject(0);
            if (card != null) {
                Tap(card);
            }
            yield return new WaitForSecondsRealtime(0.5f);
        }
        run.Event("pick-taken", RunBoons.Info(chosen).Name + ", picks " + picks + " to " + RunBoons.Picks);
        if (RunBoons.Picks == picks) {
            run.Problem(PlaytestSeverity.Bug, "upgrade-pick: tapping a card did not take the upgrade");
        }

        // Wave 2: the bat shows it. Swing by finger when the pad is up, then read the trail colour the swing draws.
        yield return run.Until(() => WaveDirector.Instance != null && WaveDirector.Wave >= 2 && !LevelUpScreen.IsOpen, 40f, "wave 2");
        float swingUntil = Time.realtimeSinceStartup + 8f;
        Vector2 at;
        while (Time.realtimeSinceStartup < swingUntil && !run.Done) {
            if (TouchControls.Spot("Swing", out at)) {
                TouchControls.BotFinger(1, at, true);
                yield return new WaitForSecondsRealtime(0.12f);
                TouchControls.BotFinger(1, Vector2.zero, false);
            }
            yield return new WaitForSecondsRealtime(0.5f);
            if (BatEquipment.TrailHead != plain) {
                break;
            }
        }
        bool changed = BatEquipment.TrailHead != plain || BatMarks.Instance != null && BatMarks.Instance.Line.Length > 0 && RunBoons.FamilyOf(chosen) == BoonFamily.Reach;
        run.Metric("pickTrailChanged", changed ? 1 : 0);
        run.Event("pick-look", "wave 2, " + RunBoons.Info(chosen).Name + ": trail " + ColorUtility.ToHtmlStringRGB(BatEquipment.TrailHead) + " against plain " + ColorUtility.ToHtmlStringRGB(plain)
            + (BatMarks.Instance != null ? "; bat: " + BatMarks.Instance.Line : ""));
        if (!changed) {
            run.Problem(PlaytestSeverity.Bug, "upgrade-pick: the bat's swing looks the same in wave 2 after taking " + RunBoons.Info(chosen).Name);
        }

        // The run-end sheet: Slice struck down (setup).
        run.Event("setup", "Slice is struck down so the run-end sheet opens");
        float downUntil = Time.realtimeSinceStartup + 20f;
        while (Musician.Instance != null && Musician.Instance.Alive && Time.realtimeSinceStartup < downUntil) {
            Musician.Instance.Hurt(9999);
            yield return new WaitForSecondsRealtime(0.3f);
        }
        yield return run.Until(() => !string.IsNullOrEmpty(RunEndScreen.RankLine), 30f, "the run-end rank");
        if (run.Done) {
            yield break;
        }
        run.Event("rank-shown", RunEndScreen.RankLine);
        yield return new WaitForSecondsRealtime(2f);
    }
}
