using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;

// The "bot-sound" scenario (BotSound.cs): bots of each family loop their own positional movement sound (at most 6 at once,
// nearest first), a wind-up tell plays 0.4-0.8 s before every blow, deaths vary and never repeat. On a phone (390x844) it plays
// by fingers only (AnimFight: stick, Swing, cards, Next wave). Setup shortcuts: a crowd of 18 bots of every family dropped round
// Wallit (SpawnBird), and the testing wave key to the boss wave.
public static partial class Playtest {
    static IEnumerator BotSoundRun(PlaytestOptions options) {
        animTouch = Screen.height > Screen.width || Screen.height < 500;
        UiFit.ForceTouch(animTouch);
        if (animTouch) {
            MobileQuality.Apply();
            run.Event("mobile-quality", MobileQuality.Describe());
        }
        run.Event("bot-sound-setup", (animTouch ? "touch 390x844 phone tier, fingers only" : "keys and mouse") + ", loop limit " + BotSound.MaxLoops);
        yield return run.Until(() => WaveDirector.Instance != null && Enemy.Roster.Count > 0, 40f, "wave 1's bots");
        if (run.Done) {
            yield break;
        }
        var player = Object.FindAnyObjectByType<CharacterMovement>();

        run.Scene("Bots by ear: wave 1 Yo Bots");
        yield return BotSoundFight(player, 22f, null);
        BotSoundTell("wave-1");
        if (run.Done) {
            yield break;
        }

        run.Scene("Bots by ear: a crowd of every family");
        run.Event("setup", "18 bots dropped round Wallit: Yo Bots, Mega Yo Bots, Spider Bots and the six chapter variants");
        TouchControls.LiftBotFingers();
        animStickDown = false;
        PlayInput.Hold(KeyCode.Mouse0, false);
        PlayInput.Hold(KeyCode.LeftShift, false);
        Vector3 origin = player.transform.position;
        string[] variants = { "Dispatch Yo Bot", "Returned Yo Bot", "Reissue Yo Bot", "Queue-Skip Yo Bot", "Clerk Yo Bot", "Sealed Yo Bot" };
        int dropped = 0;
        for (int i = 0; i < 18; i++) {
            float ang = i / 18f * Mathf.PI * 2f;
            Vector3 at = origin + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (10f + (i % 4) * 1.5f);
            BotKind kind = i % 9 == 0 ? BotKind.MegaYoBot : (i % 3 == 1 ? BotKind.SpiderBot : BotKind.YoBot);
            Enemy e = SpawnBird(at, false, kind);
            if (e == null) {
                continue;
            }
            dropped++;
            if (kind == BotKind.YoBot && i % 3 == 0 && i / 3 < variants.Length) {
                e.MakeVariant(BotTrait.Sprinter, variants[i / 3]);
            }
        }
        run.Event("bot-sound-crowd", dropped + " dropped, " + Enemy.Roster.Count + " bots alive");
        int peakBots = Enemy.Roster.Count;
        float start = Time.realtimeSinceStartup;
        float nextLog = 0f;
        while (!run.Done && !WaveDirector.IsLost && Time.realtimeSinceStartup - start < 45f) {
            yield return BotSoundFight(player, 1f, null);
            peakBots = Mathf.Max(peakBots, Enemy.Roster.Count);
            if (Time.realtimeSinceStartup >= nextLog) {
                nextLog = Time.realtimeSinceStartup + 4f;
                BotSoundTell("crowd");
            }
        }
        BotSoundTell("crowd-end");
        if (run.Done) {
            yield break;
        }

        run.Scene("Bots by ear: the boss wave");
        run.Event("setup", "testing wave key to wave 10 for the Alpha's ratchet tell and collapse");
        yield return GoWave(10);
        yield return BotSoundFight(player, 40f, null);
        TouchControls.LiftBotFingers();
        PlayInput.Release();
        if (run.Done) {
            yield break;
        }
        BotSoundTell("boss-wave");

        // The verdict, from what the mixer really did.
        run.Event("bot-sound-result", "peak bots " + peakBots + ", peak loops " + BotSound.PeakLoops + " (limit " + BotSound.MaxLoops + "), tells " + BotSound.Tells
            + ", blows " + BotSound.Hits + " of which " + BotSound.InWindow + " landed 0.4-0.8 s after their tell, deaths " + BotSound.Deaths + ", layers " + BotSound.Layers
            + ", repeats " + GameAudio.Repeats);
        run.Metric("botLoopsPeak", BotSound.PeakLoops);
        run.Metric("botBotsPeak", peakBots);
        run.Metric("botTells", BotSound.Tells);
        run.Metric("botBlows", BotSound.Hits);
        run.Metric("botBlowsInWindow", BotSound.InWindow);
        run.Metric("botDeaths", BotSound.Deaths);
        run.Metric("botLayers", BotSound.Layers);
        run.Metric("botSoundRepeats", GameAudio.Repeats);
        foreach (KeyValuePair<string, string> kv in BotSound.MoveClip) {
            Vector2 pan = BotSound.PanRange.ContainsKey(kv.Key) ? BotSound.PanRange[kv.Key] : Vector2.zero;
            run.Event("bot-move-sound", kv.Key + " loops " + kv.Value + ", panned from " + pan.x.ToString("0.00") + " (left) to " + pan.y.ToString("0.00") + " (right)");
        }
        foreach (string line in BotSound.Log) {
            run.Event("bot-sound-log", line);
        }

        if (BotSound.PeakLoops > BotSound.MaxLoops) {
            run.Problem(PlaytestSeverity.Bug, "up to " + BotSound.PeakLoops + " bot movement loops played at once (limit " + BotSound.MaxLoops + ")");
        }
        if (peakBots >= 15 && BotSound.PeakLoops < 2) {
            run.Problem(PlaytestSeverity.Bug, "a wave of " + peakBots + " bots played only " + BotSound.PeakLoops + " movement loops");
        }
        foreach (string family in new[] { "yo", "spider", "mega" }) {
            if (!BotSound.MoveClip.ContainsKey(family)) {
                run.Problem(PlaytestSeverity.Bug, family + " bots never played a movement loop");
            }
        }
        if (BotSound.Hits > 0 && BotSound.InWindow < BotSound.Hits * 0.8f) {
            run.Problem(PlaytestSeverity.Bug, "only " + BotSound.InWindow + " of " + BotSound.Hits + " blows landed 0.4-0.8 s after their tell");
        }
        if (BotSound.Hits == 0) {
            run.Problem(PlaytestSeverity.Bug, "no bot blow landed, so no wind-up tell was timed");
        }
        if (BotSound.Deaths == 0) {
            run.Problem(PlaytestSeverity.Bug, "no bot died with its family's death sound");
        }
        if (GameAudio.Repeats > 0) {
            run.Problem(PlaytestSeverity.Bug, "a sound variant played twice in a row " + GameAudio.Repeats + " times");
        }
        run.Finish(PlaytestOutcome.Done);
    }

    static IEnumerator BotSoundFight(CharacterMovement player, float seconds, System.Func<bool> stop) {
        yield return AnimFight(player, seconds, stop);
    }

    static void BotSoundTell(string where) {
        run.Event("bot-loops", where + ": bots " + Enemy.Roster.Count + ", loops now " + BotSound.LoopsNow + ", peak " + BotSound.PeakLoops + ", tells " + BotSound.Tells
            + ", blows " + BotSound.Hits + " (" + BotSound.InWindow + " in 0.4-0.8 s), deaths " + BotSound.Deaths + ", layers " + BotSound.Layers
            + ", families " + string.Join(",", new List<string>(BotSound.Families).ToArray()));
    }
}
