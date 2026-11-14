using UnityEngine;

// Per-scene mood: how a place is lit, graded and heard. Chapter 1's yard is a cold midnight (blue shadows, thin colour, a deep
// vignette, wind); the hub office is a lit room (amber light, rich colour, a soft vignette, a hum and a clock) that warms
// with every chapter filed. Everything is code: NightGrade's fields, the scene's lights, and a looping bed synthesised on the
// spot (no GPU, no files). The bed is a looping AudioSource, so Settings' music slider and Sound off scale it
// (SoundSettings.Apply) and it sits under the score. Docs/Story.md keeps the tone: the Department at night, the office as the
// one place that is warm.
public static class SceneMood {
    public enum Kind { Yard, Hub }

    // One place's finishing pass (NightGrade) and its bed.
    public struct Look {
        public string Name;
        public float Exposure, Contrast, Saturation, Vignette, Bloom, Threshold;
        public Color Shadows, Highlights, Lift;
        public Color Key;          // the directional light
        public float KeyIntensity;
        public float BedVolume;
    }

    // Chapter 1: past midnight over the yard. Cold, thin, dark at the edges; the lamps are what bloom.
    public static Look Yard() {
        return new Look {
            Name = "yard",
            Exposure = 1.0f, Contrast = 1.16f, Saturation = 1.05f, Vignette = 1.0f, Bloom = 0.8f, Threshold = 0.84f,
            Shadows = new Color(0.9f, 0.95f, 1.0f), Highlights = new Color(1.14f, 1.04f, 0.86f), Lift = new Color(0.012f, 0.008f, 0.02f),
            Key = new Color(1f, 0.76f, 0.5f), KeyIntensity = 0.9f, BedVolume = 0.35f,
        };
    }

    // The office: lamplight. dawn is 0 (nothing filed) to 1 (all six), which brightens and warms it.
    public static Look Hub(float dawn) {
        dawn = Mathf.Clamp01(dawn);
        return new Look {
            Name = "hub",
            Exposure = Mathf.Lerp(1.14f, 1.24f, dawn), Contrast = 1.04f, Saturation = Mathf.Lerp(1.08f, 1.22f, dawn),
            Vignette = 0.45f, Bloom = Mathf.Lerp(0.95f, 1.1f, dawn), Threshold = 0.8f,
            Shadows = Color.Lerp(new Color(1.0f, 0.93f, 0.88f), new Color(1.05f, 0.92f, 0.8f), dawn),
            Highlights = Color.Lerp(new Color(1.14f, 1.02f, 0.84f), new Color(1.2f, 1.0f, 0.76f), dawn),
            Lift = new Color(0.03f, 0.018f, 0.008f),
            Key = Color.Lerp(new Color(1f, 0.86f, 0.64f), new Color(1f, 0.82f, 0.52f), dawn), KeyIntensity = Mathf.Lerp(1.05f, 1.25f, dawn),
            BedVolume = 0.3f,
        };
    }

    public static Look For(Kind kind, float dawn = 0f) {
        return kind == Kind.Yard ? Yard() : Hub(dawn);
    }

    public static void Grade(NightGrade g, Look l) {
        if (g == null) {
            return;
        }
        g.Exposure = l.Exposure;
        g.Contrast = l.Contrast;
        g.Saturation = l.Saturation;
        g.Vignette = l.Vignette;
        g.Intensity = l.Bloom;
        g.Threshold = l.Threshold;
        g.Shadows = l.Shadows;
        g.Highlights = l.Highlights;
        g.Lift = l.Lift;
    }

    // Puts the mood on a camera (adding the grade when it has none) and tints the directional lights in the loaded scenes.
    public static NightGrade Apply(Camera cam, Look l, bool lights = true) {
        NightGrade g = null;
        if (cam != null) {
            g = cam.GetComponent<NightGrade>();
            if (g == null) {
                cam.allowHDR = !UiFit.Touch;
                g = cam.gameObject.AddComponent<NightGrade>();
            }
            Grade(g, l);
        }
        if (lights) {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) {
                if (light.type == LightType.Directional) {
                    light.color = l.Key;
                    light.intensity = l.KeyIntensity;
                }
            }
        }
        Current = l.Name;
        return g;
    }

    public static string Current { get; private set; } = "";

    // ------------------------------------------------------------------ The bed

    const int Rate = 16000;

    // A looping bed under the score. Yard: wind in slow gusts with a low drone. Hub: a room's hum, a clock tick each second and the
    // odd paper rustle. Every period is a whole number of cycles, so the loop joins without a click.
    public static AudioSource Bed(Transform parent, Kind kind) {
        const float seconds = 8f;
        int count = Mathf.RoundToInt(seconds * Rate);
        var data = new float[count];
        var rng = new System.Random(kind == Kind.Yard ? 11 : 23);
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < count; i++) {
            float t = i / (float)Rate;
            float n = (float)(rng.NextDouble() * 2.0 - 1.0);
            if (kind == Kind.Yard) {
                lp += (n - lp) * 0.035f;
                lp2 += (lp - lp2) * 0.2f;
                float gust = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * (2f * t / seconds)) * Mathf.Sin(2f * Mathf.PI * (3f * t / seconds) + 1f));
                float drone = Mathf.Sin(2f * Mathf.PI * Mathf.Round(55f * seconds) / seconds * t) * 0.12f;
                data[i] = lp2 * 4.2f * gust + drone;
            } else {
                lp += (n - lp) * 0.06f;
                float hum = Mathf.Sin(2f * Mathf.PI * Mathf.Round(100f * seconds) / seconds * t) * 0.14f + Mathf.Sin(2f * Mathf.PI * Mathf.Round(200f * seconds) / seconds * t) * 0.05f;
                float into = t % 1f;
                float tick = into < 0.05f ? Mathf.Sin(2f * Mathf.PI * 1900f * into) * Mathf.Exp(-into * 160f) * 0.5f : 0f;
                float page = Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * (t / seconds) * 1f - 0.6f)) > 0.97f ? n * 0.18f : 0f;
                data[i] = lp * 0.5f + hum + tick + page;
            }
        }
        float peak = 0.0001f;
        for (int i = 0; i < count; i++) {
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }
        for (int i = 0; i < count; i++) {
            data[i] = data[i] / peak * 0.8f;
        }
        AudioClip clip = AudioClip.Create("Bed " + kind, count, 1, Rate, false);
        clip.SetData(data, 0);
        var go = new GameObject("Ambient Bed " + kind);
        go.transform.SetParent(parent, false);
        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.spatialBlend = 0f;
        src.playOnAwake = false;
        src.volume = (kind == Kind.Yard ? Yard() : Hub(0f)).BedVolume;
        src.Play();
        SoundSettings.Apply();   // the slider scales it from the first frame
        return src;
    }
}
