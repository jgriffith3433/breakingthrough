using System.Collections.Generic;
using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.UI;

// The "ceremony" scenario's check of the chapter-end ceremony (ChapterCeremony.cs), a partial of Playtest. It runs after
// CampaignRun has played Chapter 1 to the card. Every press is a tap on a UI object (a finger on a phone).
public static partial class Playtest {
    static IEnumerator CeremonyFlow(ChapterRun chapter) {
        UiFit.ForceTouch(true);
        ChapterCeremony c = chapter.Ceremony;
        if (c == null) {
            run.Problem(PlaytestSeverity.Bug, "ceremony: the Chapter Complete card showed no ceremony");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        float t0 = ChapterCeremony.StartedAt;
        run.Event("ceremony-start", "rank " + CampaignGrade.Letter(chapter.Rank) + " on " + chapter.Title);
        yield return run.Until(() => ChapterCeremony.StatsTyped >= 8, 20f, "eight rows typed onto the dossier (XP bar, Extra, Lore log, Ranks)");
        run.Event("ceremony-stats", ChapterCeremony.StatsTyped + " stats typed, " + ChapterCeremony.Ticks + " typewriter ticks, " + (Time.unscaledTime - t0).ToString("0.0") + " s in");
        run.Metric("ceremonyStatsTyped", ChapterCeremony.StatsTyped);
        run.Metric("ceremonyTicks", ChapterCeremony.Ticks);
        if (ChapterCeremony.StatsTyped < 8 || ChapterCeremony.Ticks < 8) {
            run.Problem(PlaytestSeverity.Bug, "ceremony: fewer than four stats typed or no typewriter ticks (" + ChapterCeremony.StatsTyped + ", " + ChapterCeremony.Ticks + ")");
        }
        run.Event("ceremony-xp-bar", "XP bar filled to " + ChapterCeremony.BarShown.ToString("0.00") + ", filled " + ChapterCeremony.BarFilled);
        run.Metric("ceremonyXpBar", ChapterCeremony.BarFilled ? 1 : 0);
        if (!ChapterCeremony.BarFilled) {
            run.Problem(PlaytestSeverity.Bug, "ceremony: the XP bar never filled");
        }
        foreach (string e in ChapterRun.Events) {
            if (e.StartsWith("ceremony|ledger|")) {
                run.Event("ceremony-ledger", e.Substring(16));   // XP, the Extra's count, the lore log, the ranks so far
            }
        }
        yield return run.Until(() => ChapterCeremony.RankStamped.Length > 0, 8f, "the rank stamp");
        run.Event("ceremony-rank", "stamp " + ChapterCeremony.RankStamped + ", sting " + ChapterCeremony.StingPlayed);
        run.Metric("ceremonyStingDistinct", ChapterCeremony.StingPlayed == "rank_" + CampaignGrade.Letter(chapter.Rank).ToLowerInvariant() ? 1 : 0);
        yield return run.Until(() => ChapterCeremony.FiledShown, 8f, "FILED across the sheet");
        run.Event("ceremony-filed", "FILED stamped " + (Time.unscaledTime - t0).ToString("0.0") + " s in");
        yield return run.Until(() => ChapterCeremony.LineSaid.Length > 0, 5f, "Wallit's line");
        bool continueBeforeLine = ChapterCeremony.ContinueShown && ChapterCeremony.LineSaid.Length == 0;
        run.Event("ceremony-line", "Wallit: \"" + ChapterCeremony.LineSaid + "\" (rank " + CampaignGrade.Letter(chapter.Rank) + ")");
        yield return run.Until(() => ChapterCeremony.ContinueShown, 8f, "Continue");
        float seconds = ChapterCeremony.ContinueAt - t0;
        run.Event("ceremony-continue", "Continue after " + seconds.ToString("0.0") + " s");
        run.Metric("ceremonySeconds", Mathf.Round(seconds * 10f) / 10f);
        if (seconds < 6f || seconds > 10f) {
            run.Problem(PlaytestSeverity.Warning, "ceremony: it took " + seconds.ToString("0.0") + " s, wanted 6 to 10");
        }
        if (continueBeforeLine) {
            run.Problem(PlaytestSeverity.Bug, "ceremony: Continue showed before Wallit spoke");
        }

        // Layout, in CSS px: the dossier's smallest text and the Continue button, the stamp the largest thing.
        float unitPx = Screen.height / Mathf.Max(1f, UiFit.Size.y) / Mathf.Max(0.5f, UiFit.PixelRatio);
        float minText = 999f;
        foreach (Text t in c.Panel.GetComponentsInChildren<Text>(true)) {
            if (t == null || t.text.Length == 0 || !t.gameObject.activeInHierarchy) {
                continue;
            }
            int used = t.cachedTextGenerator != null && t.cachedTextGenerator.fontSizeUsedForBestFit > 0 && t.resizeTextForBestFit ? t.cachedTextGenerator.fontSizeUsedForBestFit : t.fontSize;
            minText = Mathf.Min(minText, used * c.Panel.localScale.x * unitPx);
        }
        float contPx = c.ContinueButton.GetComponent<RectTransform>().rect.height * unitPx;
        float stampPx = c.StampBox.rect.height * c.Panel.localScale.x * unitPx;
        bool largest = true;
        foreach (Text t in c.Panel.GetComponentsInChildren<Text>(true)) {
            if (t != null && t.gameObject.activeInHierarchy && t.transform != c.StampBox.GetChild(1) && t.fontSize > 190 && t.text != "FILED") {
                largest = false;
            }
        }
        run.Event("ceremony-layout", "smallest text " + minText.ToString("0.0") + " px, Continue " + contPx.ToString("0") + " px tall, stamp " + stampPx.ToString("0") + " px, screen " + Screen.width + "x" + Screen.height);
        run.Metric("ceremonyMinTextPx", Mathf.Round(minText * 10f) / 10f);
        run.Metric("ceremonyContinuePx", Mathf.Round(contPx));
        run.Metric("ceremonyStampLargest", largest && stampPx >= 200f ? 1 : 0);
        if (minText < 14f) {
            run.Problem(PlaytestSeverity.Bug, "ceremony: dossier text is " + minText.ToString("0.0") + " px, wanted 14 or more");
        }
        if (contPx < 48f) {
            run.Problem(PlaytestSeverity.Bug, "ceremony: Continue is " + contPx.ToString("0") + " px tall, wanted 48 or more");
        }
        CheckScreen("ceremony");

        // Every written line of the dossier (stat rows, rank caption, Wallit's line, footer) must have its own rect:
        // the FILED stamp and rank letter are the sheet's marks and are meant to sit over it.
        var lines = new List<Rect>();
        foreach (Text t in c.Panel.GetComponentsInChildren<Text>(true)) {
            if (t == null || t.text.Length == 0 || !t.gameObject.activeInHierarchy || t.text == "FILED" || t.fontSize > 100) {
                continue;
            }
            Vector3[] corners = new Vector3[4];
            t.rectTransform.GetWorldCorners(corners);
            lines.Add(Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y));
        }
        int endOverlaps = 0;
        for (int i = 0; i < lines.Count; i++) {
            for (int j = i + 1; j < lines.Count; j++) {
                float w = Mathf.Min(lines[i].xMax, lines[j].xMax) - Mathf.Max(lines[i].xMin, lines[j].xMin);
                float h = Mathf.Min(lines[i].yMax, lines[j].yMax) - Mathf.Max(lines[i].yMin, lines[j].yMin);
                if (w > 2f && h > 2f && w * h / Mathf.Min(lines[i].width * lines[i].height, lines[j].width * lines[j].height) > 0.2f) {
                    endOverlaps++;
                }
            }
        }
        run.Event("ceremony-lines", lines.Count + " lines on the dossier, overlaps " + endOverlaps + ", Wallit \"" + ChapterCeremony.LineSaid + "\"");
        run.Metric("endOverlaps", endOverlaps);
        if (endOverlaps > 0) {
            run.Problem(PlaytestSeverity.Bug, "ceremony: " + endOverlaps + " written lines overlap");
        }

        // Played again, two taps skip it to Continue.
        yield return new WaitForSecondsRealtime(1f);
        chapter.ReplayCeremony();
        yield return null;
        c = chapter.Ceremony;
        yield return new WaitForSecondsRealtime(1.2f);
        Tap(c.Catcher);
        yield return new WaitForSecondsRealtime(0.15f);
        Tap(c.Catcher);
        yield return run.Until(() => ChapterCeremony.ContinueShown, 2f, "Continue after two taps");
        run.Event("ceremony-skip", "two taps skipped to Continue (taps " + ChapterCeremony.Taps + ", skipped " + ChapterCeremony.Skipped + ")");
        run.Metric("ceremonySkipTaps", ChapterCeremony.Taps);
        if (!ChapterCeremony.ContinueShown || !ChapterCeremony.Skipped) {
            run.Problem(PlaytestSeverity.Bug, "ceremony: two taps did not skip to Continue");
        }
        yield return new WaitForSecondsRealtime(0.8f);
        Tap(c.ContinueButton.gameObject);
        run.Event("tap", "Continue");
        yield return new WaitForSecondsRealtime(2f);
    }
}
