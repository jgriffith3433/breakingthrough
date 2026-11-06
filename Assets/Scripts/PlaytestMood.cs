using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;

// The "moods" scenario (SceneMood): the hub and Chapter 1 must look and sound like different places. It opens the sandbox case file
// in the hub (chapter 1 filed by setup, never the real save), reads what the camera really draws and what plays there, then taps
// the chapter board's Begin and reads the same in the Sorting Yard. Per place: the grade, a frame sampled off the camera
// (brightness, warmth = red minus blue, colour) through the grade, the bed and the score (playing, volume), and the frame rate.
// Checked: the two frames differ clearly in warmth and brightness, each place has a bed or a score playing, and the music slider
// really moves the bed. Events mood-hub, mood-yard, mood-compare, mood-volume; metrics moodWarmthGap, moodBrightGap, fpsHub, fpsYard.
public static class PlaytestMood {
    struct Frame { public float Luma, Warm, Sat; }

    static Frame Sample(Camera cam) {
        var rt = RenderTexture.GetTemporary(96, 54, 24, RenderTextureFormat.ARGB32);
        RenderTexture old = cam.targetTexture, active = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = old;
        RenderTexture.active = rt;
        var tex = new Texture2D(96, 54, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 96, 54), 0, 0);
        RenderTexture.active = active;
        RenderTexture.ReleaseTemporary(rt);
        Color[] px = tex.GetPixels();
        Object.Destroy(tex);
        var f = new Frame();
        foreach (Color c in px) {
            f.Luma += 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            f.Warm += c.r - c.b;
            float mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            f.Sat += mx > 0.001f ? (mx - mn) / mx : 0f;
        }
        f.Luma /= px.Length;
        f.Warm /= px.Length;
        f.Sat /= px.Length;
        return f;
    }

    // Seconds of frames: the mean frame rate.
    static IEnumerator Fps(float seconds, System.Action<float> done) {
        float t = 0f;
        int frames = 0;
        while (t < seconds) {
            yield return null;
            t += Time.unscaledDeltaTime;
            frames++;
        }
        done(frames / Mathf.Max(0.01f, t));
    }

    static string Heard(AudioSource bed) {
        return "bed " + (bed != null && bed.isPlaying ? "playing vol " + bed.volume.ToString("0.00") : "SILENT") + ", score '" + MusicDirector.Current + "' " + (MusicDirector.Playing ? "playing vol " + MusicDirector.Volume.ToString("0.00") : "not playing (missing or still loading)");
    }

    static void Tap(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    public static IEnumerator Run(PlaytestRun run) {
        yield return run.Until(() => ChapterScene.Instance != null && ChapterScene.Instance.Hub != null && ChapterScene.Instance.Hub.Shown && ChapterScene.Instance.Hub.Bed != null, 20f, "the hub");
        if (run.Done) {
            yield break;
        }
        run.Metric("campaignLoaded", 1);
        ChapterHub hub = ChapterScene.Instance.Hub;
        run.Scene("Hub mood");
        yield return new WaitForSecondsRealtime(3f);
        Camera cam = Camera.main;
        Frame hubFrame = Sample(cam);
        float fpsHub = 0f;
        yield return Fps(4f, v => fpsHub = v);
        NightGrade g = cam.GetComponent<NightGrade>();
        run.Event("mood-hub", "mood " + SceneMood.Current + " (" + hub.Mood.Name + "), grade " + (g != null ? "exposure " + g.Exposure.ToString("0.00") + " saturation " + g.Saturation.ToString("0.00") + " vignette " + g.Vignette.ToString("0.00") + " bloom " + g.Intensity.ToString("0.00") : "NONE") + ", key light #" + ColorUtility.ToHtmlStringRGB(hub.Mood.Key) + "; frame luma " + hubFrame.Luma.ToString("0.000") + " warmth " + hubFrame.Warm.ToString("0.000") + " colour " + hubFrame.Sat.ToString("0.000") + "; " + Heard(hub.Bed) + "; " + fpsHub.ToString("0") + " fps");
        run.Metric("fpsHub", Mathf.Round(fpsHub));
        bool hubVoiced = hub.Bed != null && hub.Bed.isPlaying;
        if (!hubVoiced) {
            run.Problem(PlaytestSeverity.Bug, "moods: the hub has no ambient bed playing");
        }

        // The music slider moves the bed: half the slider, half the bed.
        float full = hub.Bed.volume;
        SoundSettings.Set(SoundSettings.On, 0.5f, SoundSettings.Sfx);
        float half = hub.Bed.volume;
        SoundSettings.Set(SoundSettings.On, 1f, SoundSettings.Sfx);
        run.Event("mood-volume", "hub bed at music 1.0: " + full.ToString("0.00") + ", at 0.5: " + half.ToString("0.00") + (half < full * 0.9f ? " (the slider moves it)" : " (the slider does NOT move it)"));
        if (!(half < full * 0.9f)) {
            run.Problem(PlaytestSeverity.Bug, "moods: the music slider does not change the hub bed");
        }

        // On to Chapter 1: the board's chapter card, then Begin.
        ChapterScene.Instance.ShowCard(0);
        yield return new WaitForSecondsRealtime(1f);
        if (ChapterScene.Instance.BeginButton == null) {
            run.Problem(PlaytestSeverity.Bug, "moods: no Begin button on chapter 1's card");
            run.Finish(PlaytestOutcome.Stuck);
            yield break;
        }
        Tap(ChapterScene.Instance.BeginButton.gameObject);
        run.Event("tap", "Begin chapter 1");
        yield return run.Until(() => LevelScene.Instance != null && LevelScene.Instance.Bed != null && LevelScene.Instance.Cam != null, 60f, "Chapter 1's yard");
        if (run.Done) {
            yield break;
        }
        run.Scene("Yard mood");
        yield return new WaitForSecondsRealtime(4f);
        LevelScene level = LevelScene.Instance;
        Frame yardFrame = Sample(level.Cam);
        float fpsYard = 0f;
        yield return Fps(6f, v => fpsYard = v);
        NightGrade yg = level.Cam.GetComponent<NightGrade>();
        run.Event("mood-yard", "mood " + SceneMood.Current + ", grade " + (yg != null ? "exposure " + yg.Exposure.ToString("0.00") + " saturation " + yg.Saturation.ToString("0.00") + " vignette " + yg.Vignette.ToString("0.00") + " bloom " + yg.Intensity.ToString("0.00") : "NONE") + "; frame luma " + yardFrame.Luma.ToString("0.000") + " warmth " + yardFrame.Warm.ToString("0.000") + " colour " + yardFrame.Sat.ToString("0.000") + "; " + Heard(level.Bed) + "; " + fpsYard.ToString("0") + " fps");
        run.Metric("fpsYard", Mathf.Round(fpsYard));
        if (level.Bed == null || !level.Bed.isPlaying) {
            run.Problem(PlaytestSeverity.Bug, "moods: Chapter 1's yard has no ambient bed playing");
        }

        float warmGap = hubFrame.Warm - yardFrame.Warm;
        float lumaGap = hubFrame.Luma - yardFrame.Luma;
        run.Metric("moodWarmthGap", Mathf.Round(warmGap * 1000f) / 1000f);
        run.Metric("moodBrightGap", Mathf.Round(lumaGap * 1000f) / 1000f);
        bool differ = warmGap > 0.03f && lumaGap > 0.02f;
        run.Event("mood-compare", "hub is " + (warmGap > 0 ? "warmer" : "colder") + " than the yard by " + warmGap.ToString("0.000") + " and " + (lumaGap > 0 ? "brighter" : "darker") + " by " + lumaGap.ToString("0.000") + (differ ? ": clearly different moods" : ": TOO ALIKE"));
        if (!differ) {
            run.Problem(PlaytestSeverity.Bug, "moods: the hub and chapter 1 look alike (warmth gap " + warmGap.ToString("0.000") + ", brightness gap " + lumaGap.ToString("0.000") + ")");
        }
        float fps = Mathf.Min(fpsHub, fpsYard);
        if (fps < 30f) {
            run.Problem(PlaytestSeverity.Bug, "moods: frame rate " + fps.ToString("0") + " fps is under 30 (hub " + fpsHub.ToString("0") + ", yard " + fpsYard.ToString("0") + ")");
        }
        run.Finish(PlaytestOutcome.Done);
    }
}
