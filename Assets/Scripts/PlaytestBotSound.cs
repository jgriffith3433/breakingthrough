using System.Collections;
using Hawkes.Playtest;
using UnityEngine;

// The "bot-sound" scenario (BotSound.cs): bots of each family loop their own positional movement sound (at most 6 at once,
// nearest first), wind-up tells play before blows, deaths vary. The bot plays the wave (wave key setup to a bigger wave) and
// logs loops, tells, deaths and repeats.
public static partial class Playtest {
    static IEnumerator BotSoundRun(PlaytestOptions options) {
        UiFit.ForceTouch(Screen.height > Screen.width || Screen.height < 500);
        run.Event("bot-sound-setup", "setup: wave key to a wave of 15+ bots");
        yield return run.Until(() => WaveDirector.Instance != null, 40f, "the wave director");
        if (run.Done) {
            yield break;
        }
        run.Scene("Bots by ear");
        float end = Time.realtimeSinceStartup + 90f;
        int peakBots = 0;
        while (Time.realtimeSinceStartup < end) {
            peakBots = Mathf.Max(peakBots, Enemy.Roster.Count);
            yield return new WaitForSeconds(2f);
            run.Event("bot-loops", "bots " + Enemy.Roster.Count + ", loops now " + BotSound.LoopsNow + ", peak " + BotSound.PeakLoops + ", tells " + BotSound.Tells + ", deaths " + BotSound.Deaths + ", families " + string.Join(",", new System.Collections.Generic.List<string>(BotSound.Families).ToArray()));
        }
        run.Event("bot-sound-result", "peak bots " + peakBots + ", peak loops " + BotSound.PeakLoops + " (limit " + BotSound.MaxLoops + "), repeats " + GameAudio.Repeats);
        run.Metric("botLoopsPeak", BotSound.PeakLoops);
        run.Metric("botTells", BotSound.Tells);
        run.Metric("botDeaths", BotSound.Deaths);
        foreach (string line in BotSound.Log) {
            run.Event("bot-sound-log", line);
        }
        run.Finish(PlaytestOutcome.Done);
    }
}
