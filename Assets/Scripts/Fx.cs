using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum FxKind {
    Flash,
    Smoke,
    Feather,
    Ring,
    Note,
    Dust,
    Debris
}

// One pooled particle system per look. Callers emit bursts at a point; nothing is spawned per effect.
// Textures are drawn in code, so the whole set needs no art files.
// Kept deliberately calm: soft flashes, smoke, a few chunky plates off a broken bot, ground rings. No spark or glitter spray.
public class Fx : MonoBehaviour {
    static Fx instance;
    readonly Dictionary<FxKind, ParticleSystem> systems = new Dictionary<FxKind, ParticleSystem>();
    static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Ensure();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        Ensure();
    }

    static void Ensure() {
        if (SceneManager.GetActiveScene().name != MetaProgress.GameScene || FindAnyObjectByType<Fx>() != null) {
            return;
        }
        new GameObject("Fx").AddComponent<Fx>();
    }

    void Awake() {
        instance = this;
        Make(FxKind.Flash, "glow", true, 80, 0.18f, 0f, false);
        Make(FxKind.Smoke, "smoke", false, 300, 1.6f, -0.25f, false);
        Make(FxKind.Feather, "feather", false, 300, 1.6f, 0.35f, false);
        Make(FxKind.Ring, "ring", true, 40, 0.45f, 0f, false, true);
        Make(FxKind.Note, "note", true, 60, 2.4f, -0.08f, false);
        Make(FxKind.Dust, "smoke", false, 200, 0.7f, -0.05f, false);
        Make(FxKind.Debris, "plate", false, 200, 1.3f, 0.9f, false);
    }

    void OnDestroy() {
        if (instance == this) {
            instance = null;
        }
    }

    void Make(FxKind kind, string texture, bool additive, int max, float life, float gravity, bool stretched, bool flat = false) {
        var go = new GameObject("Fx " + kind);
        go.transform.SetParent(transform, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = life;
        main.gravityModifier = gravity;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            kind == FxKind.Smoke || kind == FxKind.Dust
                ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.15f), new GradientAlphaKey(0f, 1f) }
                : kind == FxKind.Feather || kind == FxKind.Debris
                    ? new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) }
                    : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = fade;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve curve;
        switch (kind) {
            case FxKind.Smoke:
            case FxKind.Dust:
                curve = new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(1f, 1.6f));
                break;
            case FxKind.Ring:
                curve = new AnimationCurve(new Keyframe(0f, 0.1f, 0f, 4f), new Keyframe(1f, 1f));
                break;
            case FxKind.Flash:
                curve = new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.2f));
                break;
            case FxKind.Note:
                curve = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0.8f));
                break;
            default:
                curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.15f));
                break;
        }
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, curve);

        if (kind == FxKind.Feather || kind == FxKind.Smoke || kind == FxKind.Dust) {
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = kind == FxKind.Feather ? 0.08f : 0.12f;
            limit.limit = kind == FxKind.Feather ? 0.6f : 0.4f;
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
        }
        if (kind == FxKind.Debris) {
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
        }
        if (kind == FxKind.Note) {
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = kind == FxKind.Note ? 0.4f : 0.6f;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.4f;
        }

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("BT/Particle");
        var material = new Material(shader != null ? shader : SafeShader.Find("Sprites/Default"));
        material.mainTexture = Texture(texture);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_Boost", additive ? 1.4f : 1f);
        material.renderQueue = additive ? 3100 : 3050;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortMode = ParticleSystemSortMode.None;
        if (stretched) {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.06f;
            renderer.lengthScale = 1.5f;
        } else if (flat) {
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
        } else {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }
        renderer.maxParticleSize = 4f;
        ps.Play();
        systems[kind] = ps;
    }

    public static void Emit(FxKind kind, Vector3 at, int count, Color color, float size, float speed, float spread = 1f, float life = -1f) {
        if (instance == null || count <= 0) {
            return;
        }
        ParticleSystem ps;
        if (!instance.systems.TryGetValue(kind, out ps) || ps == null) {
            return;
        }
        var p = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++) {
            Vector3 dir = Random.insideUnitSphere;
            if (kind == FxKind.Note) {
                dir.y = Mathf.Abs(dir.y) + 0.6f;
            } else if (kind == FxKind.Feather) {
                dir.y = Mathf.Abs(dir.y) * 1.2f + 0.2f;
            } else if (kind == FxKind.Debris) {
                dir.y = Mathf.Abs(dir.y) * 1.2f + 0.5f;
            }
            p.position = at + Random.insideUnitSphere * 0.15f * spread * size;
            p.velocity = dir.normalized * speed * Random.Range(0.5f, 1.1f);
            p.startSize = size * Random.Range(0.7f, 1.25f);
            p.startColor = Color.Lerp(color, Color.white, Random.Range(0f, 0.15f));
            p.rotation = Random.Range(0f, 360f);
            if (life > 0f) {
                p.startLifetime = life * Random.Range(0.8f, 1.2f);
            } else {
                p.ResetStartLifetime();
            }
            ps.Emit(p, 1);
        }
    }

    // A flat ring that grows outward. Size is the final diameter.
    public static void Ring(Vector3 at, float diameter, Color color, float life = 0.45f) {
        if (instance == null) {
            return;
        }
        ParticleSystem ps;
        if (!instance.systems.TryGetValue(FxKind.Ring, out ps)) {
            return;
        }
        var p = new ParticleSystem.EmitParams();
        p.position = at + Vector3.up * 0.08f;
        p.velocity = Vector3.zero;
        p.startSize = diameter;
        p.startColor = color;
        p.startLifetime = life;
        ps.Emit(p, 1);
    }

    // Recipes the rest of the game calls.
    public static void Hit(Vector3 at, float scale, bool crit) {
        float s = Mathf.Clamp(scale, 0.6f, 4f);
        Emit(FxKind.Flash, at, 1, crit ? new Color(1f, 0.8f, 0.35f, 0.8f) : new Color(1f, 0.9f, 0.75f, 0.45f), (crit ? 1.1f : 0.55f) * s, 0f);
    }

    // A bot coming apart: a soft electric flash, a puff of dark smoke and a few plates in its paint.
    public static void Death(Vector3 at, float scale, Color paint, bool big) {
        float s = Mathf.Clamp(scale, 0.6f, 12f);
        float root = Mathf.Sqrt(s);
        Vector3 core = at + Vector3.up * 0.4f * s;
        Emit(FxKind.Flash, core, 1, new Color(0.6f, 0.85f, 1f, 0.7f), (big ? 1.6f : 1f) * root, 0f);
        Emit(FxKind.Smoke, core, big ? 4 : 2, new Color(0.2f, 0.2f, 0.22f, 0.5f), 0.7f * root, 0.8f, 1.2f);
        Color dark = new Color(paint.r * 0.45f, paint.g * 0.45f, paint.b * 0.45f, 1f);
        int plates = big ? 8 : 4;
        Emit(FxKind.Debris, core, plates - plates / 3, paint, 0.32f * root, 3.4f * root, 1.5f);
        Emit(FxKind.Debris, core, plates / 3, dark, 0.26f * root, 3f * root, 1.5f);
        Emit(FxKind.Dust, at, big ? 10 : 4, new Color(0.75f, 0.72f, 0.65f, 0.6f), 0.9f * root, 1.4f);
        if (big) {
            Ring(at, 4f * root, new Color(0.65f, 0.85f, 1f, 0.8f), 0.6f);
        }
    }

    // The puff under a bot's jump jets as it lifts off.
    public static void JetPuff(Vector3 at, float scale) {
        float s = Mathf.Clamp(scale, 0.4f, 30f);
        float root = Mathf.Sqrt(s);
        Emit(FxKind.Flash, at + Vector3.up * 0.1f * s, 1, new Color(0.55f, 0.8f, 1f, 0.4f), 0.45f * root, 0f);
        Emit(FxKind.Smoke, at + Vector3.up * 0.05f, 2, new Color(0.62f, 0.64f, 0.7f, 0.4f), 0.45f * root, 0.7f * root, 0.8f, 0.8f);
    }

    public static void Explosion(Vector3 at, float radius, Color color) {
        float r = Mathf.Max(1f, radius);
        Emit(FxKind.Flash, at, 1, new Color(color.r, color.g, color.b, 0.9f), r * 1.5f, 0f);
        Emit(FxKind.Smoke, at + Vector3.up * 0.3f, Mathf.Clamp(Mathf.RoundToInt(r * 1.2f), 2, 7), new Color(0.22f, 0.2f, 0.22f, 0.5f), r * 0.8f, 1f, r * 0.6f);
        Ring(at, r * 2.2f, new Color(color.r, color.g, color.b, 0.7f), 0.4f);
    }

    public static void Notes(Vector3 at, int count) {
        Color[] tones = { new Color(1f, 0.85f, 0.45f), new Color(0.7f, 0.9f, 1f), new Color(1f, 0.6f, 0.8f) };
        for (int i = 0; i < count; i++) {
            Emit(FxKind.Note, at, 1, tones[Random.Range(0, tones.Length)], Random.Range(0.35f, 0.55f), 0.8f, 1.5f);
        }
    }

    public static void Footstep(Vector3 at, float scale) {
        Emit(FxKind.Dust, at + Vector3.up * 0.05f, 2, new Color(0.62f, 0.58f, 0.5f, 0.45f), 0.6f * Mathf.Sqrt(scale), 0.6f);
    }

    // ---------- procedural textures ----------

    public static Texture2D Texture(string name) {
        Texture2D tex;
        if (textures.TryGetValue(name, out tex) && tex != null) {
            return tex;
        }
        const int n = 64;
        tex = new Texture2D(n, n, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.name = "fx_" + name;
        var px = new Color[n * n];
        for (int y = 0; y < n; y++) {
            for (int x = 0; x < n; x++) {
                float u = (x + 0.5f) / n * 2f - 1f;
                float v = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a;
                switch (name) {
                    case "spark":
                        a = Mathf.Clamp01(1f - Mathf.Abs(u) * 4f) * Mathf.Clamp01(1f - Mathf.Abs(v) * 1.1f);
                        a = a * a;
                        break;
                    case "plate":
                        // a chunky square plate with clipped corners, a bevel and a bolt hole
                        float edge = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                        float corner = Mathf.Abs(u) + Mathf.Abs(v);
                        a = Mathf.Clamp01((0.72f - edge) * 24f) * Mathf.Clamp01((1.15f - corner) * 24f) * Mathf.Clamp01((r - 0.16f) * 24f);
                        float bevel = edge > 0.58f || corner > 1.0f ? 0.7f : 1f;
                        px[y * n + x] = new Color(bevel, bevel, bevel, a);
                        continue;
                    case "ring":
                        a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) * 9f);
                        a = a * a * (r < 1f ? 1f : 0f) + Mathf.Clamp01(0.82f - r) * 0.15f;
                        break;
                    case "smoke":
                        float wob = Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.5f + 0.5f;
                        a = Mathf.Clamp01(1f - r) * wob;
                        a = Mathf.SmoothStep(0f, 1f, a);
                        break;
                    case "feather":
                        // a leaf-shaped vane with a darker quill down the middle
                        float along = (v + 1f) * 0.5f;
                        float width = Mathf.Sin(along * Mathf.PI) * 0.42f * (1f - along * 0.25f);
                        a = Mathf.Clamp01((width - Mathf.Abs(u)) * 18f) * Mathf.Clamp01((0.98f - along) * 20f);
                        float quill = Mathf.Clamp01(1f - Mathf.Abs(u) * 30f);
                        float barbs = 0.85f + 0.15f * Mathf.Sin((v * 30f) + Mathf.Abs(u) * 18f);
                        px[y * n + x] = new Color(barbs * (1f - quill * 0.35f), barbs * (1f - quill * 0.35f), barbs * (1f - quill * 0.35f), a);
                        continue;
                    case "note":
                        // an eighth note: head, stem, flag
                        float hx = u + 0.25f;
                        float hy = v + 0.45f;
                        float head = Mathf.Clamp01((0.22f - Mathf.Sqrt(hx * hx * 0.8f + hy * hy * 1.6f)) * 30f);
                        float stem = (u > -0.07f && u < 0.02f && v > -0.45f && v < 0.7f) ? 1f : 0f;
                        float fx = u - 0.02f;
                        float fy = v - 0.7f;
                        float flag = (fx > 0f && fx < 0.35f && fy < 0f && fy > -0.32f && Mathf.Abs(fy + fx * 0.9f) < 0.09f) ? 1f : 0f;
                        a = Mathf.Max(head, Mathf.Max(stem, flag));
                        break;
                    default:
                        a = Mathf.Clamp01(1f - r);
                        a = a * a * (3f - 2f * a);
                        break;
                }
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply(true, true);
        textures[name] = tex;
        return tex;
    }
}
