using System.Collections.Generic;
using UnityEngine;

// The Blender birds in Resources/Models/Birds. Each has Body, Head, WingL, WingR, LegL and LegR parts.
// They are painted with vertex colors and drawn with the knoll shader, so they sit in the same world.
public static class BirdArt {
    static Material shared;
    static Material glowing;

    // Same look, but warm yellow vertex colors glow: lantern glass, the Thunderbird's eyes and markings.
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
    static readonly Dictionary<string, GameObject> sources = new Dictionary<string, GameObject>();
    static readonly Dictionary<long, Mesh> tinted = new Dictionary<long, Mesh>();

    public static Material Shared {
        get {
            if (shared == null) {
                Shader shader = Shader.Find("Knoll/VertexColorLit");
                if (shader == null) {
                    shader = Shader.Find("Standard");
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

    static GameObject Source(FowlKind kind) {
        string name = kind.ToString();
        GameObject source;
        if (!sources.TryGetValue(name, out source)) {
            source = Resources.Load<GameObject>("Models/Birds/" + name);
            sources[name] = source;
        }
        return source;
    }

    public static bool Has(FowlKind kind) {
        return Source(kind) != null;
    }

    // tint is a hue to lean toward; amount 0 keeps the authored colors.
    public static GameObject Make(FowlKind kind, Color tint, float amount) {
        GameObject source = Source(kind);
        if (source == null) {
            return null;
        }
        GameObject made = Object.Instantiate(source);
        made.name = "Turkey";
        foreach (Collider collider in made.GetComponentsInChildren<Collider>()) {
            Object.Destroy(collider);
        }
        foreach (MeshRenderer renderer in made.GetComponentsInChildren<MeshRenderer>()) {
            renderer.sharedMaterial = kind == FowlKind.Thunderbird ? Glowing : Shared;
            renderer.receiveShadows = true;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter != null && amount > 0.001f) {
                filter.sharedMesh = Tinted(filter.sharedMesh, tint, amount);
            }
        }
        return made;
    }

    static Mesh Tinted(Mesh mesh, Color tint, float amount) {
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
