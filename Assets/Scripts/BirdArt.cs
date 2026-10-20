using System.Collections.Generic;
using UnityEngine;

// The shared look for everything painted with vertex colors on the hill: the bots (BotArt) and the lantern glass.
// Drawn with the knoll shader, so they sit in the same world.
public static class BirdArt {
    static Material shared;
    static Material glowing;

    // Same look, but warm yellow vertex colors glow: lantern glass, the Titan Spider Bot's gold trim and eyes.
    public static Material Glowing {
        get {
            if (glowing == null) {
                glowing = new Material(Shared);
                glowing.name = "Birds (glowing)";
                if (glowing.HasProperty("_GlowBand")) {
                    glowing.SetFloat("_GlowBand", 1f);
                }
            }
            return glowing;
        }
    }
    static readonly Dictionary<long, Mesh> tinted = new Dictionary<long, Mesh>();

    public static Material Shared {
        get {
            if (shared == null) {
                Shader shader = SafeShader.Find("Knoll/VertexColorLit");
                if (shader == null) {
                    shader = SafeShader.Find("Standard");
                }
                shared = new Material(shader);
                shared.name = "Birds (shared)";
                shared.enableInstancing = true;
                if (shared.HasProperty("_Cull")) {
                    shared.SetFloat("_Cull", 0f);
                }
                // Birds get a stronger moon rim than the scenery so they pop off the dark field.
                if (shared.HasProperty("_RimStrength")) {
                    shared.SetFloat("_RimStrength", 0.6f);
                }
                if (shared.HasProperty("_GlowBand")) {
                    shared.SetFloat("_GlowBand", 0f);
                }
            }
            return shared;
        }
    }

    // A copy of the mesh with its vertex colors leaned toward tint. Copies are cached, so a flock shares them.
    public static Mesh Tinted(Mesh mesh, Color tint, float amount) {
        if (mesh == null) {
            return null;
        }
        int r = Mathf.RoundToInt(Mathf.Clamp01(tint.r / 2f) * 63f);
        int g = Mathf.RoundToInt(Mathf.Clamp01(tint.g / 2f) * 63f);
        int b = Mathf.RoundToInt(Mathf.Clamp01(tint.b / 2f) * 63f);
        int a = Mathf.RoundToInt(Mathf.Clamp01(amount) * 31f);
        long key = ((long)mesh.GetInstanceID() << 32) ^ (r << 17) ^ (g << 11) ^ (b << 5) ^ a;
        Mesh copy;
        if (tinted.TryGetValue(key, out copy) && copy != null) {
            return copy;
        }
        copy = Object.Instantiate(mesh);
        copy.name = mesh.name + " tinted";
        Color[] colors = copy.colors;
        if (colors == null || colors.Length == 0) {
            tinted[key] = copy;
            return copy;
        }
        Color hue = new Color(tint.r, tint.g, tint.b);
        float peak = Mathf.Max(hue.r, Mathf.Max(hue.g, hue.b));
        if (peak > 0.001f) {
            hue /= peak;
        }
        for (int i = 0; i < colors.Length; i++) {
            Color c = colors[i];
            float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            Color painted = hue * Mathf.Clamp01(lum * 2.1f + 0.06f);
            colors[i] = new Color(
                Mathf.Clamp01(Mathf.Lerp(c.r, painted.r, amount)),
                Mathf.Clamp01(Mathf.Lerp(c.g, painted.g, amount)),
                Mathf.Clamp01(Mathf.Lerp(c.b, painted.b, amount)),
                c.a);
        }
        copy.colors = colors;
        tinted[key] = copy;
        return copy;
    }
}
