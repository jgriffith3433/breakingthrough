using System.Collections;
using Hawkes.Playtest;
using UnityEngine;

// The "exposure" scenario: J's report that the light is blown out and gets worse each wave. It plays Arcade, uses the testing wave
// key (a named setup shortcut) to reach waves 1, 5, 10, 15, 20, 25 and 30 and at each one reads the frame the camera draws
// through the grade: mean luminance, the share of pure-white pixels and of near-black ones, plus the post pass's effective
// exposure, bloom and threshold (NightGrade's shared rule). Metrics exposureLuma_<wave>, exposureWhitePct_<wave>,
// exposureDrift (largest luminance change inside one act, as a share) and exposureBloom_<wave>. On a phone viewport it runs on
// the phone tier (no HDR, touch). Problems: more than 2% pure white, or the mean climbing more than 10% inside an act.
// ExposureSample is public so a chapter, hub or finale scenario can log the same number.
public static partial class Playtest {
    public static string ExposureSample(string label, out float luma, out float whitePct) {
        luma = 0f;
        whitePct = 0f;
        Camera cam = Camera.main;
        if (cam == null) {
            return label + ": no camera";
        }
        FilmSample(cam, out Texture2D tex);
        Color32[] px = tex.GetPixels32();
        Object.Destroy(tex);
        int white = 0, black = 0;
        double sum = 0;
        foreach (Color32 c in px) {
            sum += (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
            if (c.r >= 254 && c.g >= 254 && c.b >= 254) {
                white++;
            } else if (c.r < 8 && c.g < 8 && c.b < 8) {
                black++;
            }
        }
        luma = (float)(sum / px.Length);
        whitePct = 100f * white / px.Length;
        NightGrade g = cam.GetComponent<NightGrade>();
        string rule = g != null ? " exposure " + g.EffectiveExposure.ToString("0.00") + " bloom " + g.EffectiveBloom.ToString("0.00") + " threshold " + g.EffectiveThreshold.ToString("0.00") : " no grade";
        run.Metric("light.meanLuma", Mathf.Round(luma * 1000f) / 1000f);
        run.Metric("light.clippedPct", Mathf.Round(whitePct * 100f) / 100f);
        run.Event("light", label + ": light.meanLuma " + luma.ToString("0.000") + " light.clippedPct " + whitePct.ToString("0.00") + " guard " + NightGrade.GuardScale.ToString("0.00") + " reads " + NightGrade.GuardReads);
        if (luma < NightGrade.LumaMin || luma > NightGrade.LumaMax) {
            run.Problem(PlaytestSeverity.Bug, "exposure: mean luma " + luma.ToString("0.00") + " outside 0.12-0.70 at " + label);
        }
        run.Event("exposure", label + ": mean luma " + luma.ToString("0.000") + ", pure white " + whitePct.ToString("0.00") + "%, near black " + (100f * black / px.Length).ToString("0.0") + "%, hdr " + cam.allowHDR + rule);
        return label;
    }

    static IEnumerator ExposureRun(PlaytestOptions options) {
        animTouch = Screen.height > Screen.width || Screen.height < 500;
        UiFit.ForceTouch(animTouch);
        if (animTouch) {
            MobileQuality.Apply();
            run.Event("mobile-quality", MobileQuality.Describe());
        }
        run.Log("exposure at " + Screen.width + "x" + Screen.height + ", " + (animTouch ? "touch" : "keys and mouse"));
        yield return run.Until(() => WaveDirector.Instance != null && Enemy.Roster.Count > 0, 30f, "wave 1's bots");
        yield return run.Until(() => ArcadeActs.Instance != null && ArcadeActs.Act == 0, 8f, "act 1");
        if (run.Done) {
            yield break;
        }
        var player = Object.FindAnyObjectByType<CharacterMovement>();
        int[] waves = { 1, 10, 20, 30 };
        float[] lumas = new float[waves.Length];
        float drift = 0f;
        for (int i = 0; i < waves.Length && !run.Done && !WaveDirector.IsLost; i++) {
            int wave = waves[i];
            if (wave > 1) {
                run.Event("setup", "testing wave key to wave " + wave);
                yield return GoWave(wave);
                if (LevelUpScreen.IsOpen) {
                    PlayInput.Tap(KeyCode.Alpha1);
                }
            }
            yield return AnimFight(player, 5f, null);
            yield return new WaitForSecondsRealtime(2f);   // past any act blend
            float luma, white;
            ExposureSample("wave " + wave + " act " + (ArcadeActs.Act + 1) + (animTouch ? " phone" : " desktop") + " bots " + Enemy.Roster.Count, out luma, out white);
            lumas[i] = luma;
            run.Metric("light.meanLuma_w" + wave, Mathf.Round(luma * 1000f) / 1000f);
            run.Metric("light.clippedPct_w" + wave, Mathf.Round(white * 100f) / 100f);
            run.Metric("exposureLuma_" + wave, Mathf.Round(luma * 1000f) / 1000f);
            run.Metric("exposureWhitePct_" + wave, Mathf.Round(white * 100f) / 100f);
            if (white > 2f) {
                run.Problem(PlaytestSeverity.Bug, "exposure: " + white.ToString("0.0") + "% of the frame is pure white at wave " + wave);
            }
            // Inside one act (waves 1-10, 11-20, 21-30) the mean must stay flat.
            if (i > 0 && Tuning.ActOf(waves[i - 1]) == Tuning.ActOf(wave) && lumas[i - 1] > 0.001f) {
                float change = (luma - lumas[i - 1]) / lumas[i - 1];
                drift = Mathf.Max(drift, change);
                if (change > 0.1f) {
                    run.Problem(PlaytestSeverity.Bug, "exposure: the frame brightens " + (change * 100f).ToString("0") + "% from wave " + waves[i - 1] + " to " + wave + " inside one act");
                }
            }
        }
        run.Metric("exposureDrift", Mathf.Round(drift * 1000f) / 1000f);
        TouchControls.LiftBotFingers();
        PlayInput.Release();
    }
}
