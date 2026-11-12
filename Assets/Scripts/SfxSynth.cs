using UnityEngine;

// Effects made in code for sounds that have no file yet: Wallit's and the bots' footsteps and the home run's whoosh and
// distant clang. Deterministic noise, so a run sounds the same twice; each footstep variant has its own body.
public static class SfxSynth {
    const int Rate = 44100;

    public static bool Has(string name) {
        return name == "botstep" || name == "wallitstep" || name == "homerun_whoosh" || name == "homerun_clang"
            || name == "sliceparry";
    }

    // Variant i (0 based) of a synthesised sound, or null.
    public static AudioClip Make(string name, int i) {
        switch (name) {
            case "botstep": return Step("botstep" + i, 0.16f, 95f - i * 14f, 1400f + i * 500f, 0.55f + i * 0.1f, 11 + i * 7);
            case "wallitstep": return Step("wallitstep" + i, 0.13f, 150f + i * 22f, 2600f + i * 400f, 0.4f, 31 + i * 5);
            case "homerun_whoosh": return Whoosh();
            case "homerun_clang": return Clang();
            case "sliceparry": return ParryClink(i);
        }
        return null;
    }

    // Slice's Act 2 parry: a short steel clink (stand-in until Resources/Sfx/sliceparry.mp3 lands from Gen).
    static AudioClip ParryClink(int i) {
        float baseHz = 2200f + i * 180f;
        return Build("sliceparry" + i, 0.18f, (s, t) => {
            float env = Mathf.Exp(-t * 55f);
            float click = Mathf.Sin(2f * Mathf.PI * baseHz * t) * env;
            float ring = Mathf.Sin(2f * Mathf.PI * (baseHz * 1.67f) * t) * Mathf.Exp(-t * 28f) * 0.45f;
            float grit = (Mathf.PerlinNoise(t * 900f, i * 3.1f) * 2f - 1f) * Mathf.Exp(-t * 90f) * 0.25f;
            return (click + ring + grit) * Mathf.Min(1f, t * 600f) * 0.7f;
        });
    }

    static AudioClip Build(string id, float seconds, System.Func<int, float, float> sample) {
        int n = Mathf.CeilToInt(seconds * Rate);
        var data = new float[n];
        for (int s = 0; s < n; s++) {
            data[s] = Mathf.Clamp(sample(s, s / (float)Rate), -1f, 1f);
        }
        var clip = AudioClip.Create(id, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // A thud (a falling sine) with a short grit of filtered noise on top.
    static AudioClip Step(string id, float len, float thud, float grit, float gritMix, int seed) {
        var rng = new System.Random(seed);
        float lp = 0f;
        float k = Mathf.Clamp01(grit / Rate * 6f);
        return Build(id, len, (s, t) => {
            float env = Mathf.Exp(-t * 38f);
            float body = Mathf.Sin(2f * Mathf.PI * (thud + 60f * Mathf.Exp(-t * 30f)) * t) * env;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * k;
            return (body * (1f - gritMix * 0.5f) + lp * Mathf.Exp(-t * 70f) * gritMix * 1.4f) * 0.8f;
        });
    }

    // Air rushing past: noise swept up through a band, rising then fading.
    static AudioClip Whoosh() {
        var rng = new System.Random(77);
        float lp = 0f;
        const float len = 0.7f;
        return Build("homerun_whoosh", len, (s, t) => {
            float u = t / len;
            float env = Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.7f));
            float k = Mathf.Lerp(0.03f, 0.35f, u);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * k;
            return lp * env * 2.2f;
        });
    }

    // A far-off bell-like clang: inharmonic partials, long soft tail.
    static AudioClip Clang() {
        float[] partials = { 1f, 2.76f, 5.4f, 8.93f };
        float[] gains = { 1f, 0.55f, 0.3f, 0.15f };
        return Build("homerun_clang", 1.4f, (s, t) => {
            float sum = 0f;
            for (int p = 0; p < partials.Length; p++) {
                sum += Mathf.Sin(2f * Mathf.PI * 540f * partials[p] * t) * gains[p] * Mathf.Exp(-t * (3f + p * 3.5f));
            }
            return sum * Mathf.Min(1f, t * 400f) * 0.5f;
        });
    }
}
