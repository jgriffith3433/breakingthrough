using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Arcade run-end screen: counting, best stamp, goals ladder, cast exchanges, Retry on phone.
// Acceptance: Docs/Story.md "Run-end exchanges"; brief criteria 1-5. Scenario id: run-end.
public static class PlaytestRunEnd {
    public static IEnumerator Run(PlaytestRun run) {
        MetaProgress.EnterSandbox(true);
        RunEndTalk.ClearRecent();
        UiFit.ForceTouch(true);
        ColdOpen.Suppress = true;

        yield return Playtest.ToMenuPublic();
        if (run.Done) {
            yield break;
        }
        run.Scene("Main Menu");
        yield return Playtest.StartArcadePublic();
        if (run.Done) {
            yield break;
        }
        run.Scene("Arcade");
        yield return run.Until(() => WaveDirector.Instance != null && !WaveDirector.IsLost, 30f, "Arcade run ready");
        if (run.Done) {
            yield break;
        }

        var heard = new List<string>();

        // Loss 1: early (wave 1).
        yield return OneLoss(run, heard, "early", false, false, false);
        if (run.Done) {
            yield break;
        }
        yield return RetryAndWait(run);
        if (run.Done) {
            yield break;
        }

        // Clear wave 1 for real so record can stamp a new best on a later loss.
        yield return ClearCurrentWave(run);
        if (run.Done) {
            yield break;
        }
        yield return JumpWave(run, 8);
        yield return OneLoss(run, heard, "yo_bot", true, false, false);
        if (run.Done) {
            yield break;
        }
        yield return RetryAndWait(run);
        if (run.Done) {
            yield break;
        }

        yield return JumpWave(run, 9);
        yield return OneLoss(run, heard, "spider", false, true, false);
        if (run.Done) {
            yield break;
        }
        yield return RetryAndWait(run);
        if (run.Done) {
            yield break;
        }

        yield return JumpWave(run, 11);
        yield return OneLoss(run, heard, "boss", false, false, true);
        if (run.Done) {
            yield break;
        }
        yield return RetryAndWait(run);
        if (run.Done) {
            yield break;
        }

        // Reach wave 10 so SettleRun can finish the Night Band look goal for real.
        yield return JumpWave(run, 10);
        if (Musician.Instance != null) {
            int drop = Mathf.Max(1, Musician.Instance.Health - SliceVoice.LowHealth);
            Musician.Instance.Hurt(drop, null);
            if (Musician.Instance.Health <= 0) {
                Musician.Instance.ReviveForWave();
            }
        }
        yield return OneLoss(run, heard, "nearly", false, false, false);
        if (run.Done) {
            yield break;
        }

        run.Event("cast-exchanges", heard.Count + " distinct: " + string.Join(", ", heard.ToArray()));
        run.Metric("runEndExchanges", heard.Count);
        if (heard.Count < 5) {
            run.Problem(PlaytestSeverity.Bug, "run-end: only " + heard.Count + " distinct cast exchanges over 5 losses");
        }
        if (!heard.Contains("early") || !heard.Contains("yo_bot")) {
            run.Problem(PlaytestSeverity.Warning, "run-end: expected early and yo_bot among the five exchanges");
        }

        bool sawBest = false;
        // Phone layout once the fifth screen is ready.
        yield return run.Until(() => RunEndScreen.IsOpen && RunEndScreen.Instance != null
            && RunEndScreen.Instance.RetryButton != null && RunEndScreen.Instance.RetryButton.interactable, 20f, "Retry ready on run-end");
        if (run.Done) {
            yield break;
        }
        sawBest = RunEndScreen.BestStamped;
        yield return new WaitForSecondsRealtime(0.3f);
        CheckPhoneLayout(run);
        run.Progress(0.7f);

        bool lookOn = RunGoals.HasLook("night_band");
        string batLine = BatMarks.Instance != null ? BatMarks.Instance.Line : "";
        run.Event("goal-look", lookOn
            ? "Night Band owned" + (batLine.IndexOf("Night Band") >= 0 ? " and on the bat" : "")
            : "Night Band not unlocked");
        run.Metric("goalLookOwned", lookOn ? 1 : 0);
        if (!lookOn) {
            run.Problem(PlaytestSeverity.Bug, "run-end: reaching wave 10 did not unlock the Night Band look");
        }
        if (!sawBest) {
            // Cleared wave 1 in a fresh sandbox should stamp a new best on that loss's screen; if not, still note it.
            run.Event("best-stamp", "not seen on the last screen (may have stamped on an earlier loss)");
        }

        Button retry = RunEndScreen.Instance.RetryButton;
        float tapAt = Time.realtimeSinceStartup;
        TapButton(retry.gameObject);
        run.Event("retry-tap", "one finger on Retry");
        yield return run.Until(() => SceneManager.GetActiveScene().name == MetaProgress.GameScene
            && WaveDirector.Instance != null && !WaveDirector.IsLost, 10f, "next run after Retry");
        if (run.Done) {
            yield break;
        }
        run.Event("run-start", "Retry reloaded Arcade in " + (Time.realtimeSinceStartup - tapAt).ToString("0.00") + " s");
        float botAt = -1f;
        float waitBot = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - waitBot < 6f && !run.Done) {
            if (Enemy.Roster.Count > 0) {
                botAt = Time.realtimeSinceStartup - tapAt;
                break;
            }
            yield return null;
        }
        run.Metric("retryToBotSeconds", botAt < 0f ? 99f : Mathf.Round(botAt * 100f) / 100f);
        run.Event("first-bot", botAt < 0f ? "none within 6 s" : "first bot at " + botAt.ToString("0.00") + " s after Retry");
        if (botAt < 0f || botAt > 4f) {
            run.Problem(PlaytestSeverity.Bug, "run-end: first bot after Retry took " + (botAt < 0f ? ">6" : botAt.ToString("0.00")) + " s (want ≤4)");
        }

        // Bat look visible on the fresh run.
        yield return new WaitForSecondsRealtime(0.5f);
        if (lookOn && BatMarks.Instance != null && BatMarks.Instance.Line.IndexOf("Night Band") < 0) {
            run.Problem(PlaytestSeverity.Warning, "run-end: Night Band owned but BatMarks line does not name it yet");
        }

        UiFit.ForceTouch(false);
        run.Progress(1f);
        if (!run.Done) {
            run.Finish(PlaytestOutcome.Done);
        }
    }

    static IEnumerator OneLoss(PlaytestRun run, List<string> heard, string expectId, bool yo, bool spider, bool boss) {
        yield return run.Until(() => WaveDirector.Instance != null && !WaveDirector.IsLost && !LevelUpScreen.IsOpen, 20f, "alive before loss " + expectId);
        if (run.Done) {
            yield break;
        }
        if (yo || spider || boss) {
            BotKind kind = boss ? BotKind.MegaYoBot : spider ? BotKind.SpiderBot : BotKind.YoBot;
            Enemy foe = Drop(kind, boss);
            if (foe != null && Musician.Instance != null) {
                Musician.Instance.Hurt(1, foe);
            }
        }
        run.Event("setup", "loss " + expectId + " at wave " + WaveDirector.Wave);
        WaveDirector.Instance.DevLose();
        yield return run.Until(() => RunEndScreen.IsOpen, 8f, "run-end screen for " + expectId);
        if (run.Done) {
            yield break;
        }
        yield return run.Until(() => RunEndScreen.CountingDone, 12f, "counting done for " + expectId);
        if (run.Done) {
            yield break;
        }
        run.Event("run-end-count", "wave/coin count finished, sound played");
        if (RunEndScreen.BestStamped) {
            run.Event("best-stamp", "new best stamped");
        }
        if (RunEndScreen.GoalsStamped > 0) {
            run.Event("goal-done", RunEndScreen.GoalsStamped + " goal(s) stamped · " + string.Join(",", RunGoals.JustDone));
        }
        yield return run.Until(() => RunEndScreen.Instance != null && RunEndScreen.Instance.RetryButton != null
            && RunEndScreen.Instance.RetryButton.interactable && !string.IsNullOrEmpty(RunEndScreen.ExchangeId), 16f, "cast exchange + Retry ready");
        if (run.Done) {
            yield break;
        }
        string id = RunEndScreen.ExchangeId;
        run.Event("cast-exchange", id + " · " + (RunEndScreen.LastSubtitle ?? ""));
        if (!heard.Contains(id)) {
            heard.Add(id);
        }
        if (id != expectId && expectId != "nearly") {
            run.Event("cast-mismatch", "wanted " + expectId + " got " + id);
        }
        if (string.IsNullOrEmpty(RunEndScreen.LastSubtitle) || RunEndScreen.LastSubtitle.IndexOf('\n') >= 0) {
            run.Problem(PlaytestSeverity.Bug, "run-end: subtitle must be one line");
        }
    }

    static IEnumerator RetryAndWait(PlaytestRun run) {
        yield return run.Until(() => RunEndScreen.Instance != null && RunEndScreen.Instance.RetryButton != null
            && RunEndScreen.Instance.RetryButton.interactable, 16f, "Retry interactable");
        if (run.Done) {
            yield break;
        }
        TapButton(RunEndScreen.Instance.RetryButton.gameObject);
        yield return run.Until(() => WaveDirector.Instance != null && !WaveDirector.IsLost, 12f, "run after Retry");
    }

    static IEnumerator ClearCurrentWave(PlaytestRun run) {
        float until = Time.realtimeSinceStartup + 45f;
        while (Time.realtimeSinceStartup < until && !run.Done && WaveDirector.Wave <= 1) {
            if (LevelUpScreen.IsOpen) {
                PlayInput.Tap(KeyCode.Alpha1);
                yield return new WaitForSecondsRealtime(0.2f);
                continue;
            }
            // Kill every live bot; vacuum leftovers.
            Enemy[] copy = Enemy.Roster.ToArray();
            for (int i = 0; i < copy.Length; i++) {
                if (copy[i] != null && copy[i].Alive) {
                    copy[i].Damage(999f, DamageKind.Bat, true);
                }
            }
            yield return new WaitForSecondsRealtime(0.35f);
            if (WaveDirector.Waiting) {
                PlayInput.Tap(KeyCode.Return);
                yield return new WaitForSecondsRealtime(0.3f);
            }
        }
        run.Event("setup", "cleared early waves; now on wave " + WaveDirector.Wave + ", best " + MetaProgress.BestWave);
    }

    static IEnumerator JumpWave(PlaytestRun run, int wave) {
        while (WaveDirector.Wave < wave && !run.Done) {
            PlayInput.Tap(KeyCode.Equals);
            yield return new WaitForSecondsRealtime(0.15f);
            if (LevelUpScreen.IsOpen) {
                PlayInput.Tap(KeyCode.Alpha1);
                yield return new WaitForSecondsRealtime(0.2f);
            }
        }
        run.Event("setup", "wave key to " + WaveDirector.Wave);
    }

    static Enemy Drop(BotKind kind, bool boss) {
        if (WaveDirector.Instance == null) {
            return null;
        }
        Vector3 at = Musician.Instance != null
            ? Musician.Instance.transform.position + new Vector3(2f, 0f, 2f)
            : new Vector3(2f, 0f, 2f);
        Enemy enemy = WaveDirector.Instance.SpawnPlaced(kind, at);
        if (enemy == null) {
            return null;
        }
        enemy.Dormant = false;
        if (boss) {
            enemy.MakeBoss();
        }
        return enemy;
    }

    static void CheckPhoneLayout(PlaytestRun run) {
        int overlaps = 0;
        float retryArea = 0f;
        float otherMax = 0f;
        bool subtitleOne = true;
        if (RunEndScreen.Instance == null) {
            run.Problem(PlaytestSeverity.Bug, "run-end: no screen for layout check");
            return;
        }
        foreach (Text text in RunEndScreen.Instance.GetComponentsInChildren<Text>(true)) {
            if (!text.isActiveAndEnabled || text.text == null) {
                continue;
            }
            if (text.text.StartsWith("WALLIT") || text.text.StartsWith("SLICE")) {
                if (text.text.IndexOf('\n') >= 0) {
                    subtitleOne = false;
                }
            }
        }
        foreach (Button button in RunEndScreen.Instance.GetComponentsInChildren<Button>(true)) {
            if (!button.isActiveAndEnabled) {
                continue;
            }
            Rect r = ScreenRect(button.GetComponent<RectTransform>());
            float area = r.width * r.height;
            if (button == RunEndScreen.Instance.RetryButton) {
                retryArea = area;
            } else {
                otherMax = Mathf.Max(otherMax, area);
            }
        }
        var rects = new List<System.Tuple<Rect, Transform>>();
        foreach (Text text in RunEndScreen.Instance.GetComponentsInChildren<Text>(true)) {
            if (text.isActiveAndEnabled) {
                rects.Add(System.Tuple.Create(ScreenRect(text.rectTransform), text.transform));
            }
        }
        foreach (Button button in RunEndScreen.Instance.GetComponentsInChildren<Button>(true)) {
            if (button.isActiveAndEnabled) {
                rects.Add(System.Tuple.Create(ScreenRect(button.GetComponent<RectTransform>()), button.transform));
            }
        }
        for (int i = 0; i < rects.Count; i++) {
            for (int j = i + 1; j < rects.Count; j++) {
                if (Related(rects[i].Item2, rects[j].Item2)) {
                    continue;
                }
                if (Overlap(rects[i].Item1, rects[j].Item1) > 0.35f) {
                    overlaps++;
                }
            }
        }
        bool fits = true;
        foreach (RectTransform rt in RunEndScreen.Instance.GetComponentsInChildren<RectTransform>(true)) {
            if (!rt.gameObject.activeInHierarchy) {
                continue;
            }
            Rect r = ScreenRect(rt);
            if (r.width > 4f && r.height > 4f
                && (r.yMax > Screen.height + 4f || r.yMin < -4f || r.xMin < -4f || r.xMax > Screen.width + 4f)) {
                fits = false;
            }
        }
        run.Event("run-end-layout", "overlaps " + overlaps + ", Retry largest " + (retryArea >= otherMax - 1f)
            + ", subtitle one line " + subtitleOne + ", fits " + fits);
        run.Metric("runEndOverlaps", overlaps);
        run.Metric("endOverlaps", overlaps);
        run.Metric("runEndRetryLargest", retryArea >= otherMax - 1f ? 1 : 0);
        run.Metric("runEndSubtitleOneLine", subtitleOne ? 1 : 0);
        run.Metric("runEndFits", fits ? 1 : 0);
        if (overlaps > 0) {
            run.Problem(PlaytestSeverity.Bug, "run-end: layout overlap check " + overlaps);
        }
        if (retryArea + 1f < otherMax) {
            run.Problem(PlaytestSeverity.Bug, "run-end: Retry is not the largest button");
        }
        if (!subtitleOne) {
            run.Problem(PlaytestSeverity.Bug, "run-end: subtitle is not one line");
        }
        if (!fits) {
            run.Problem(PlaytestSeverity.Bug, "run-end: screen does not fit without scrolling at 390x844");
        }
    }

    static void TapButton(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        if (UiFit.Touch) {
            RectTransform rt = target.GetComponent<RectTransform>();
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
            TouchControls.BotFinger(8, screen, true);
            TouchControls.BotFinger(8, Vector2.zero, false);
        }
    }

    static Rect ScreenRect(RectTransform rt) {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        float x0 = corners[0].x, x1 = corners[2].x, y0 = corners[0].y, y1 = corners[2].y;
        return Rect.MinMaxRect(Mathf.Min(x0, x1), Mathf.Min(y0, y1), Mathf.Max(x0, x1), Mathf.Max(y0, y1));
    }

    static float Overlap(Rect a, Rect b) {
        float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
        float h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        if (w <= 0f || h <= 0f) {
            return 0f;
        }
        float smaller = Mathf.Min(a.width * a.height, b.width * b.height);
        return smaller <= 0f ? 0f : w * h / smaller;
    }

    static bool Related(Transform a, Transform b) {
        return a == b || a.IsChildOf(b) || b.IsChildOf(a);
    }
}
