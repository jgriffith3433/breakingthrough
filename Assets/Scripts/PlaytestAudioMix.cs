using System.Collections;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;

// The "audio-mix" scenario: Settings opened by tap, the three sliders measured and set by drag-free value changes,
// persistence read back, the snapshot and per-place reverb decay logged, and the music duck read while a voice line plays.
public static class PlaytestAudioMix {
    static void Tap(GameObject target) {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
    }

    public static IEnumerator Run(PlaytestRun run) {
        yield return Playtest.ToMenuPublic();
        MainMenu menu = Object.FindAnyObjectByType<MainMenu>();
        if (menu == null || menu.SettingsButton == null) {
            run.Problem(PlaytestSeverity.Bug, "audio-mix: no Settings button");
            yield break;
        }
        Tap(menu.SettingsButton.gameObject);
        yield return run.Until(() => SettingsPanel.IsOpen, 5f, "Settings");
        if (run.Done) {
            yield break;
        }
        SettingsPanel p = SettingsPanel.Instance;
        Slider[] sliders = { p.MusicSlider, p.VoiceSlider, p.EffectsSlider };
        string[] names = { "music", "voice", "effects" };
        float scale = UiFit.PixelRatio > 0f ? 1f / UiFit.PixelRatio : 1f;
        for (int i = 0; i < 3; i++) {
            var rt = (RectTransform)sliders[i].transform;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float px = (corners[1].y - corners[0].y) * scale;
            run.Event("slider-height", names[i] + " " + px.ToString("0") + " px");
            if (px < 44f) {
                run.Problem(PlaytestSeverity.Bug, "audio-mix: " + names[i] + " slider is " + px.ToString("0") + " px tall");
            }
        }
        p.MusicSlider.value = 0.6f;
        p.VoiceSlider.value = 0.7f;
        p.EffectsSlider.value = 0.5f;
        yield return null;
        string json = SoundSettings.Json();
        bool back = SoundSettings.LoadFrom(json) && Mathf.Abs(SoundSettings.VoiceSlider - 0.7f) < 0.01f && Mathf.Abs(SoundSettings.Music - 0.6f) < 0.01f;
        run.Event("settings-persisted", json + " same " + back);
        if (!back) {
            run.Problem(PlaytestSeverity.Bug, "audio-mix: slider values did not persist");
        }
        SoundSettings.Set(true, 1f, 1f);
        SoundSettings.SetVoice(1f);
        SettingsPanel.Close();

        run.Event("reverb", "hub " + AudioMix.DecayOf("hub") + " s, hollow " + AudioMix.DecayOf("hollow") + " s, floor " + AudioMix.DecayOf("floor") + " s");
        GameAudio.Play("batcrack", 1f);
        run.Event("reverb-now", AudioMix.Place + " decay " + AudioMix.ReverbDecay + " snapshot " + AudioMix.Current);

        float before = AudioMix.MusicDuck;
        WallitVoice.Claim("audio-mix", 1.5f);
        yield return new WaitForSecondsRealtime(0.8f);
        float during = AudioMix.MusicDuck;
        run.Metric("musicDuckDb", 20f * Mathf.Log10(Mathf.Max(0.001f, during / before)));
        run.Event("duck", "music " + (20f * Mathf.Log10(during / before)).ToString("0.0") + " dB under a line");
    }
}
