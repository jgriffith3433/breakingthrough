using System.Collections;
using Hawkes.Playtest;
using UnityEngine;

// The "arcade-score" scenario: Arcade's reactive score (ArcadeScore.cs). Plays by keys, or by fingers only on a phone viewport
// (390x844). Nothing is made before wave 1; then the bot holds fire until four bots stand within 6 m of Slice and checks two
// layers come in on a bar line; fights the wave out and checks the layers are gone within 4 s of the clear; then the wave key (a
// named setup) to the bosses of waves 10, 20 and 30: each must add a layer at its phase change and play a 3-5 s cadence on death.
public static partial class Playtest {
    static IEnumerator ArcadeScoreRun(PlaytestOptions options) {
        animTouch = Screen.height > Screen.width || Screen.height < 500;
        UiFit.ForceTouch(animTouch);
        if (animTouch) {
            MobileQuality.Apply();
            run.Event("mobile-quality", MobileQuality.Describe());
        }
        bool preWave = ArcadeScore.Instance == null || !ArcadeScore.Prepared;
        run.Event("score-before-wave1", "stems made before wave 1: " + (preWave ? "none" : "some") + ", stem downloads " + ArcadeScore.StemFetches);
        run.Metric("stemDownloadsBeforeWave1", preWave ? 0 : 1);
        yield return run.Until(() => WaveDirector.Instance != null && Enemy.Roster.Count > 0 && ArcadeScore.Instance != null, 30f, "wave 1's bots and the score");
        if (run.Done) { yield break; }
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        yield return run.Until(() => ArcadeScore.Prepared, 10f, "the stems");
        run.Event("score-stems", "stems made at wave " + ArcadeScore.PreparedAtWave + ", downloads " + ArcadeScore.StemFetches);

        // Pressure: no swings until four bots are within 6 m of Slice (her health is topped up so the wave isn't lost).
        run.Scene("Score: bots close in on Slice");
        float t0 = Time.realtimeSinceStartup;
        while (!run.Done && !WaveDirector.IsLost && Time.realtimeSinceStartup - t0 < 60f && ArcadeScore.MaxLayersOn < 2) {
            if (LevelUpScreen.IsOpen) { PlayInput.Tap(KeyCode.Alpha1); }
            if (Musician.Instance != null && Musician.Instance.Health < 40) { Musician.Instance.Heal(60); }
            run.Note("near " + ArcadeScore.Near + ", danger " + ArcadeScore.Danger.ToString("0.00") + ", layers " + ArcadeScore.LayersOn);
            yield return null;
        }
        run.Event("score-layers", ArcadeScore.MaxLayersOn + " layers under pressure, near " + ArcadeScore.Near + ", danger " + ArcadeScore.Danger.ToString("0.00") + ", bar lateness max " + ArcadeScore.BarLateMax.ToString("0.000") + " s");
        run.Metric("scoreLayersUnderPressure", ArcadeScore.MaxLayersOn);
        run.Metric("scoreBarLateMaxMs", Mathf.Round(ArcadeScore.BarLateMax * 1000f));
        run.Metric("scoreMaxStreams", ArcadeScore.MaxStreams);
        if (ArcadeScore.MaxLayersOn < 2) {
            run.Problem(PlaytestSeverity.Bug, "arcade-score: under pressure (" + ArcadeScore.Near + " bots within 6 m) fewer than 2 layers came in");
        }
        if (animTouch && ArcadeScore.MaxStreams > 3) {
            run.Problem(PlaytestSeverity.Bug, "arcade-score: " + ArcadeScore.MaxStreams + " music sources played at once on the phone tier (limit 3)");
        }

        // Clear the wave: the layers go within 4 s.
        run.Scene("Score: the wave clears");
        yield return AnimFight(player, 90f, () => WaveDirector.IsBuilding || WaveDirector.Wave > 1);
        yield return run.Until(() => ArcadeScore.LayersOn == 0 && ArcadeScore.LastClearFadeSeconds >= 0f, 8f, "the layers to fade after the clear");
        run.Event("score-clear", "layers silent " + ArcadeScore.LastClearFadeSeconds.ToString("0.0") + " s after the clear (limit 4.0)");
        run.Metric("scoreClearFadeSeconds", Mathf.Round(ArcadeScore.LastClearFadeSeconds * 10f) / 10f);
        if (ArcadeScore.LastClearFadeSeconds < 0f || ArcadeScore.LastClearFadeSeconds > 4f) {
            run.Problem(PlaytestSeverity.Bug, "arcade-score: layers took " + ArcadeScore.LastClearFadeSeconds.ToString("0.0") + " s to go after the wave clear (limit 4)");
        }

        // The bosses.
        int[] waves = { 10, 20, 30 };
        for (int i = 0; i < waves.Length && !run.Done && !WaveDirector.IsLost; i++) {
            run.Event("setup", "testing wave key to wave " + waves[i]);
            yield return GoWave(waves[i]);
            WaveDirector.Instance.TrimForPlaytest();
            run.Scene("Score: boss of wave " + waves[i]);
            int bossStarts = ArcadeScore.BossLayerStarts;
            int cadences = ArcadeScore.Cadences;
            float tb = Time.realtimeSinceStartup;
            float nextHeal = 0f;
            while (!run.Done && !WaveDirector.IsLost && ArcadeScore.Cadences == cadences && Time.realtimeSinceStartup - tb < 150f) {
                if (Time.realtimeSinceStartup >= nextHeal && Musician.Instance != null && Musician.Instance.Alive) {
                    nextHeal = Time.realtimeSinceStartup + 4f;
                    Musician.Instance.Heal(100);
                }
                yield return AnimFight(player, 0.25f, null);
            }
            bool layer = ArcadeScore.BossLayerStarts > bossStarts;
            bool cadence = ArcadeScore.Cadences > cadences;
            run.Event("score-boss", "wave " + waves[i] + ": phase layer " + (layer ? "added" : "MISSING") + ", cadence " + (cadence ? ArcadeScore.LastCadenceSeconds.ToString("0.0") + " s" : "MISSING"));
            run.Metric("scoreBossLayer" + waves[i], layer ? 1 : 0);
            run.Metric("scoreCadenceSeconds" + waves[i], cadence ? Mathf.Round(ArcadeScore.LastCadenceSeconds * 10f) / 10f : 0f);
            if (!layer) {
                run.Problem(PlaytestSeverity.Bug, "arcade-score: the boss of wave " + waves[i] + " added no layer at its phase change");
            }
            if (!cadence || ArcadeScore.LastCadenceSeconds < 3f || ArcadeScore.LastCadenceSeconds > 5f) {
                run.Problem(PlaytestSeverity.Bug, "arcade-score: no 3-5 s cadence on the death of the wave " + waves[i] + " boss");
            }
        }
        foreach (string e in ArcadeScore.Events) {
            run.Log("score|" + e);
        }
    }
}
