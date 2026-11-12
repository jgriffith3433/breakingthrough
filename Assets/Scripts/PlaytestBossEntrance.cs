using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;

// The "boss-entrances" scenario: every boss arrives as an event (CinematicBoss.cs). Arcade waves 10, 20 and 30 are played
// through the real wave key (setup: the testing wave key and TrimForPlaytest, as boss-set-pieces does); the seven campaign
// bosses (Courier, Plated Alpha, Foreman, Half-Built, Server, Registrar, Order-Bearer) are dropped on the field one by one
// (setup: WaveDirector.SpawnForChapter / SpawnMiniAt, then their entrance with IgnoreSeen, since a case file plays each once).
// Per entrance: its length (4 to 8 s), the name card up with its centre in the upper third, the stinger, the taunt spoken, the
// boss track within 1 s of the end, and on a phone (390x844) the touch pads hidden during it and back within 0.3 s after.
// The last campaign boss is skipped with a click to show a tap ends it at once and still turns the music.
public static partial class Playtest {
    static IEnumerator BossEntranceRun(PlaytestOptions options) {
        bool phone = Screen.height > Screen.width || Screen.height < 500;
        UiFit.ForceTouch(phone);
        if (phone) {
            MobileQuality.Apply();
            run.Event("mobile-quality", MobileQuality.Describe());
        }
        run.Log("boss-entrances at " + Screen.width + "x" + Screen.height + ", " + (phone ? "touch" : "keys and mouse"));
        yield return run.Until(() => WaveDirector.Instance != null && Enemy.Roster.Count > 0, 30f, "wave 1's bots");
        if (run.Done) {
            yield break;
        }
        bossEntranceStats = new List<float>();
        bossCardTopMax = 0f;
        bossTouchBackMax = 0f;
        int count = 0;

        int[] waves = { 10, 20, 30 };
        for (int i = 0; i < waves.Length && !run.Done && !WaveDirector.IsLost; i++) {
            run.Event("setup", "testing wave key to wave " + waves[i]);
            yield return GoWave(waves[i]);
            WaveDirector.Instance.TrimForPlaytest();
            int before = CinematicDirector.BossIntros;
            yield return run.Until(() => CinematicDirector.BossIntros > before, 30f, "the wave " + waves[i] + " boss entrance");
            if (run.Done) {
                yield break;
            }
            yield return BossEntranceWatch("Arcade wave " + waves[i], phone, false);
            count++;
            if (WaveDirector.IsLost && Musician.Instance != null) {
                break;
            }
        }

        // The campaign six (and the Registrar), set up on the field one at a time.
        yield return GoWave(1);
        ClearField();
        CinematicDirector.IgnoreSeen = true;
        string[] names = { "The Courier", "The Plated Alpha", "The Foreman", "The Half-Built", "The Server", "The Registrar", "The Order" };
        for (int i = 0; i < names.Length && !run.Done; i++) {
            ClearField();
            yield return new WaitForSecondsRealtime(0.3f);
            Enemy boss = SpawnForEntrance(names[i]);
            if (boss == null) {
                run.Problem(PlaytestSeverity.Bug, "boss-entrances: could not set up " + names[i]);
                continue;
            }
            run.Event("setup", names[i] + " placed on the field for its entrance");
            bool ended = false;
            int before = CinematicDirector.BossIntros;
            CinematicDirector.BossResult res = CinematicDirector.PlayBoss(boss, i < 3 ? i : i - 1, -1, () => ended = true);
            if (res != CinematicDirector.BossResult.Played) {
                run.Problem(PlaytestSeverity.Bug, "boss-entrances: " + names[i] + " has no entrance (" + res + ")");
                Object.Destroy(boss.gameObject);
                continue;
            }
            bool skip = i == names.Length - 1;
            yield return BossEntranceWatch(names[i], phone, skip);
            count++;
            if (boss != null) {
                Object.Destroy(boss.gameObject);
            }
        }
        CinematicDirector.IgnoreSeen = false;
        run.Metric("bossEntrances", count);
        run.Metric("bossEntranceCardTopFrac", Mathf.Round(bossCardTopMax * 100f) / 100f);
        if (bossEntranceStats.Count > 0) {
            float lo = 99f, hi = 0f;
            foreach (float s in bossEntranceStats) {
                lo = Mathf.Min(lo, s);
                hi = Mathf.Max(hi, s);
            }
            run.Metric("bossEntranceMinSeconds", Mathf.Round(lo * 10f) / 10f);
            run.Metric("bossEntranceMaxSeconds", Mathf.Round(hi * 10f) / 10f);
        }
        if (phone) {
            run.Metric("bossTouchBackSeconds", Mathf.Round(bossTouchBackMax * 100f) / 100f);
        }
        if (count < 10) {
            run.Problem(PlaytestSeverity.Bug, "boss-entrances: only " + count + " of 10 entrances played");
        }
    }

    static List<float> bossEntranceStats;
    static float bossCardTopMax, bossTouchBackMax;

    static void ClearField() {
        foreach (Enemy e in new List<Enemy>(Enemy.Roster)) {
            if (e != null) {
                Object.Destroy(e.gameObject);
            }
        }
    }

    static Enemy SpawnForEntrance(string name) {
        WaveDirector wd = WaveDirector.Instance;
        Vector3 origin = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
        switch (name) {
            case "The Courier": return wd.SpawnForChapter(BotKind.YoBot, true, 22f, name, 40, false);
            case "The Plated Alpha": return wd.SpawnForChapter(BotKind.PlatedYoBot, true, 22f, name, 55, true);
            case "The Foreman": return wd.SpawnForChapter(BotKind.MegaYoBot, true, 22f, name, 70, true);
            case "The Half-Built": return wd.SpawnMiniAt(BotKind.MegaYoBot, name, 34, new Color(1f, 0.8f, 0.4f), origin + new Vector3(20f, 0f, 0f));
            case "The Server": return wd.SpawnForChapter(BotKind.SpiderBot, true, 22f, name, 60, false);
            case "The Registrar": return wd.SpawnForChapter(BotKind.TitanSpiderBot, true, 22f, name, 80, true);
            case "The Order": return wd.SpawnForChapter(BotKind.SpiderBot, true, 22f, name, 90, true);
        }
        return null;
    }

    // Watches one entrance from its first frame to its end and checks every part of it.
    static IEnumerator BossEntranceWatch(string label, bool phone, bool skip) {
        run.Scene("Boss entrance: " + label);
        int eventsFrom = Mathf.Max(0, CinematicDirector.BossEvents.Count - 1);
        float t0 = Time.unscaledTime;
        bool cardSeen = false, padsDuring = false, skipped = false;
        float cardY = -1f;
        bool shotTaken = false;
        while (CinematicDirector.BossOpen && !run.Done && Time.unscaledTime - t0 < 12f) {
            float t = Time.unscaledTime - t0;
            if (CinematicDirector.BossCardUp && !cardSeen) {
                cardSeen = true;
                cardY = CinematicDirector.BossCardCenterFromTop;
            }
            if (cardSeen && !shotTaken && t > 3.9f) {
                shotTaken = true;
                run.Event("boss-card-shot", label + ": name card up at " + t.ToString("0.0") + " s, centre " + cardY.ToString("0.00") + " from the top");
            }
            if (phone && TouchControls.Instance != null && TouchControls.Instance.IsShown) {
                padsDuring = true;
            }
            if (skip && !skipped && t > 1.2f) {
                skipped = true;
                PlayInput.Tap(KeyCode.Mouse0);   // a tap
            }
            yield return null;
        }
        float seconds = CinematicDirector.LastBossSeconds;
        float endedAt = Time.unscaledTime;
        string title = CinematicDirector.LastBossTitle;
        float back = -1f;
        if (phone) {
            while (Time.unscaledTime - endedAt < 1.5f && !(TouchControls.Instance != null && TouchControls.Instance.IsShown)) {
                yield return null;
            }
            back = Time.unscaledTime - endedAt;
            bossTouchBackMax = Mathf.Max(bossTouchBackMax, back);
        }
        string track = MusicDirector.BossTrack(title);
        while (Time.unscaledTime - endedAt < 1.0f && MusicDirector.Current != track) {
            yield return null;
        }
        bool trackOk = track.Length == 0 || MusicDirector.Current == track;

        bool stinger = false, voice = false;
        for (int i = eventsFrom; i < CinematicDirector.BossEvents.Count; i++) {
            string e = CinematicDirector.BossEvents[i];
            stinger |= e.StartsWith("boss-stinger|");
            voice |= e.StartsWith("boss-voice|");
        }
        run.Event("boss-entrance", label + " (" + title + "): " + seconds.ToString("0.0") + " s" + (skip ? " (skipped by a tap)" : "")
            + ", card " + cardSeen + (cardY >= 0f ? " at " + cardY.ToString("0.00") : "") + ", stinger " + stinger + ", taunt " + voice
            + ", track " + MusicDirector.Current + " " + trackOk + (phone ? ", pads hidden " + !padsDuring + ", back in " + back.ToString("0.00") + " s" : ""));
        if (!skip) {
            bossEntranceStats.Add(seconds);
            if (seconds < 4f || seconds > 8f) {
                run.Problem(PlaytestSeverity.Bug, "boss-entrances: " + label + " lasted " + seconds.ToString("0.0") + " s (want 4 to 8)");
            }
            if (!cardSeen) {
                run.Problem(PlaytestSeverity.Bug, "boss-entrances: " + label + " never showed its name card");
            }
            if (!stinger) {
                run.Problem(PlaytestSeverity.Bug, "boss-entrances: " + label + " had no stinger");
            }
            if (!voice) {
                run.Problem(PlaytestSeverity.Bug, "boss-entrances: " + label + " did not speak");
            }
        } else if (skip && seconds > 3f) {
            run.Problem(PlaytestSeverity.Bug, "boss-entrances: a tap did not skip " + label + " (" + seconds.ToString("0.0") + " s)");
        }
        if (cardSeen) {
            bossCardTopMax = Mathf.Max(bossCardTopMax, 1f - cardY);
            if (phone && cardY > 0.34f) {
                run.Problem(PlaytestSeverity.Bug, "boss-entrances: " + label + " name card is not in the upper third (centre " + cardY.ToString("0.00") + " from the top)");
            }
        }
        if (!trackOk) {
            run.Problem(PlaytestSeverity.Bug, "boss-entrances: " + label + " boss track " + track + " did not start within 1 s of the end (playing '" + MusicDirector.Current + "')");
        }
        if (phone && padsDuring) {
            run.Problem(PlaytestSeverity.Bug, "boss-entrances: " + label + " touch pads stayed up during the entrance");
        }
        if (phone && back > 0.3f) {
            run.Problem(PlaytestSeverity.Bug, "boss-entrances: " + label + " touch pads took " + back.ToString("0.00") + " s to return (want 0.3)");
        }
        yield return new WaitForSecondsRealtime(0.4f);
    }
}
