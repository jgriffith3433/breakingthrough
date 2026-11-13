using UnityEngine;

// Effects made in code for sounds that have no file yet: Wallit's and the bots' footsteps and the home run's whoosh and
// distant clang. Deterministic noise, so a run sounds the same twice; each footstep variant has its own body.
public static class SfxSynth {
    const int Rate = 44100;

    public static bool Has(string name) {
        return name.StartsWith("step_") || name.StartsWith("land_") || name.StartsWith("spot_") || name == "botstep" || name.StartsWith("bot_") || name == "wallitstep" || name == "homerun_whoosh" || name == "slice_hurt" || name == "homerun_clang";
    }

    // Variant i (0 based) of a synthesised sound, or null.
    public static AudioClip Make(string name, int i) {
        if (name.StartsWith("step_") || name.StartsWith("land_")) {
            return Foot(name, i);
        }
        if (name.StartsWith("spot_")) {
            return Spot(name);
        }
        if (name.StartsWith("bot_")) {
            return BotSound.Make(name, i);
        }
        switch (name) {
            case "botstep": return Step("botstep" + i, 0.16f, 95f - i * 14f, 1400f + i * 500f, 0.55f + i * 0.1f, 11 + i * 7);
            case "wallitstep": return Step("wallitstep" + i, 0.13f, 150f + i * 22f, 2600f + i * 400f, 0.4f, 31 + i * 5);
            case "slice_hurt": return SliceHurt(i);
            case "homerun_whoosh": return Whoosh();
            case "homerun_clang": return Clang();
        }
        return null;
    }

    public static AudioClip Build(string id, float seconds, System.Func<int, float, float> sample) {
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

    // Slice taking a blow: a short breathy, clipped "hnh" (formant-ish pitched buzz dropping, with a breath of noise) over a dull
    // leather thud. Higher and lower takes by variant; distinct from the bat's crack and the bots' hits.
    static AudioClip SliceHurt(int i) {
        var rng = new System.Random(900 + i * 17);
        float lp = 0f;
        float f0 = 210f - i * 22f;
        return Build("slice_hurt" + i, 0.32f, (s, t) => {
            float voiced = Mathf.Sin(2f * Mathf.PI * (f0 - 70f * t) * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * (f0 - 70f * t) * 2.1f * t);
            lp += ((float)(rng.NextDouble() * 2.0 - 1.0) - lp) * 0.22f;
            float voice = (voiced * 0.35f + lp * 0.9f) * Mathf.Exp(-t * 11f) * Mathf.Min(1f, t * 300f);
            float thud = Mathf.Sin(2f * Mathf.PI * (90f + 40f * Mathf.Exp(-t * 30f)) * t) * Mathf.Exp(-t * 26f) * 0.7f;
            return (voice + thud) * 0.8f;
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

    // ---------------------------------------------------------------- surfaces and spots

    // Wallit's feet are leather wallet flaps: a soft slap (a low thump with a short flap of noise), coloured by what is under
    // them. Grass is a muffled swish, stone a dry tap, wood a hollow knock, metal a slap with a short ring. Variant i changes the
    // thump's pitch and the noise seed, so three takes never sound alike; a landing is the same, longer and heavier.
    static AudioClip Foot(string name, int i) {
        bool land = name.StartsWith("land_");
        string surface = name.Substring(5);
        float len = land ? 0.26f : 0.16f;
        float gain = land ? 1.25f : 0.9f;
        float thud = (land ? 95f : 140f) + i * 17f;
        float noiseLp, noiseMix, ring = 0f, ringDecay = 30f, thudDecay = land ? 24f : 36f;
        switch (surface) {
            case "grass": noiseLp = 0.07f; noiseMix = 1.1f; thud *= 0.8f; gain *= 0.8f; break;
            case "stone": noiseLp = 0.5f; noiseMix = 0.7f; thud *= 1.2f; break;
            case "wood": noiseLp = 0.18f; noiseMix = 0.35f; ring = 230f + i * 28f; ringDecay = 22f; break;
            default: noiseLp = 0.4f; noiseMix = 0.6f; ring = 980f + i * 140f; ringDecay = 14f; gain *= 0.9f; break;
        }
        var rng = new System.Random(101 + i * 13 + surface.Length * 7 + (land ? 500 : 0));
        float lp = 0f;
        return Build(name + i, len, (s, t) => {
            float body = Mathf.Sin(2f * Mathf.PI * (thud + 50f * Mathf.Exp(-t * 28f)) * t) * Mathf.Exp(-t * thudDecay);
            lp += ((float)(rng.NextDouble() * 2.0 - 1.0) - lp) * noiseLp;
            float flap = lp * Mathf.Exp(-t * (surface == "grass" ? 28f : 55f)) * noiseMix * 1.5f;
            float tone = ring > 0f ? Mathf.Sin(2f * Mathf.PI * ring * t) * Mathf.Exp(-t * ringDecay) * (surface == "metal" ? 0.35f : 0.5f) : 0f;
            return (body * 0.7f + flap + tone) * gain * Mathf.Min(1f, t * 600f);
        });
    }

    // Stand-ins for the spot emitters until the Hawkes Gen files (Resources/Sfx/spot_<name>.mp3) are in: one take each.
    static AudioClip Spot(string name) {
        var rng = new System.Random(name.Length * 977);
        float lp = 0f;
        switch (name) {
            case "spot_lamp":   // a mains buzz that gutters once
                return Build(name, 3.2f, (s, t) => {
                    float flick = t > 1.6f && t < 1.75f ? 0.2f : 1f;
                    float v = Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 200f * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * 300f * t) * 0.12f;
                    return v * flick * Mathf.Min(1f, t * 8f) * Mathf.Min(1f, (3.2f - t) * 8f) * 0.5f;
                });
            case "spot_sign":   // a slow rusty creak: a falling saw with a wobble
                return Build(name, 2f, (s, t) => {
                    float f = 210f - 60f * t + 18f * Mathf.Sin(2f * Mathf.PI * 7f * t);
                    float ph = (f * t) % 1f;
                    float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 2f));
                    return (ph * 2f - 1f) * 0.25f * env * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 11f * t));
                });
            case "spot_drip":   // three drops, each a small falling blip
                return Build(name, 2.6f, (s, t) => {
                    float v = 0f;
                    float[] at = { 0.1f, 1.0f, 2.0f };
                    for (int k = 0; k < at.Length; k++) {
                        float u = t - at[k];
                        if (u > 0f) {
                            v += Mathf.Sin(2f * Mathf.PI * (1100f + 500f * Mathf.Exp(-u * 40f) - k * 150f) * u) * Mathf.Exp(-u * 28f);
                        }
                    }
                    return v * 0.5f;
                });
            case "spot_phone":  // two double-rings
                return Build(name, 2.2f, (s, t) => {
                    float u = t % 1.1f;
                    float on = u < 0.7f ? 1f : 0f;
                    float trem = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 22f * t);
                    return (Mathf.Sin(2f * Mathf.PI * 1400f * t) + Mathf.Sin(2f * Mathf.PI * 1750f * t)) * 0.2f * on * trem;
                });
            case "spot_type":   // a run of key clacks and the carriage bell
                return Build(name, 2.4f, (s, t) => {
                    float v = 0f;
                    for (int k = 0; k < 9; k++) {
                        float u = t - (0.1f + k * 0.17f + (k % 3) * 0.03f);
                        if (u > 0f) {
                            v += (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-u * 160f) * 0.6f;
                        }
                    }
                    float b = t - 1.9f;
                    if (b > 0f) {
                        v += Mathf.Sin(2f * Mathf.PI * 2400f * b) * Mathf.Exp(-b * 9f) * 0.25f;
                    }
                    return v;
                });
            case "spot_tick":   // a radiator tick or a wall clock: one dry tick, one tock
                return Build(name, 1.2f, (s, t) => {
                    float v = 0f;
                    float[] at = { 0.05f, 0.6f };
                    for (int k = 0; k < at.Length; k++) {
                        float u = t - at[k];
                        if (u > 0f) {
                            v += Mathf.Sin(2f * Mathf.PI * (k == 0 ? 1800f : 1500f) * u) * Mathf.Exp(-u * 90f) * 0.5f;
                        }
                    }
                    return v;
                });
            default:            // spot_machine: a steel clunk and a hiss of steam
                return Build(name, 2.2f, (s, t) => {
                    lp += ((float)(rng.NextDouble() * 2.0 - 1.0) - lp) * 0.5f;
                    float clunk = Mathf.Sin(2f * Mathf.PI * (70f + 30f * Mathf.Exp(-t * 20f)) * t) * Mathf.Exp(-t * 9f) * 0.8f;
                    float hiss = lp * Mathf.Clamp01((t - 0.12f) * 6f) * Mathf.Exp(-(t - 0.12f) * 1.8f) * 0.35f;
                    return clunk + hiss;
                });
        }
    }
}
