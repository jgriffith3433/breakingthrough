using UnityEngine;

// Lighter rendering on phones and tablets (J: "make the WebGL build lighter for phones"). Runs once before the first scene
// and again whenever touch is first seen (a playtest forces touch after boot), so the desktop never pays for it and a phone
// never has to find a setting. What it turns down: shadows, anisotropic filtering, anti-aliasing, soft particles,
// reflection probes, LOD distance, the top texture mip (a quarter of the texture memory), and the frame rate (60, not a
// 120 Hz screen's 120). Bloom is lightened in NightGrade by UiFit.Touch. Nothing here changes layout or gameplay.
public static class MobileQuality {
    public static bool Applied { get; private set; }

    // Caps for the phone tier (the soak run's live counts are judged flat from wave 5 to 20): every pooled particle system
    // holds half its particles, at most MaxPickups bolts lie on the ground at once (more merge their value into the oldest),
    // and chests carry no light of their own (the lanterns are the only lights; pixelLightCount 1 limits the rest).
    public const float ParticleScale = 0.5f;
    public const int MaxPickups = 40;
    public static bool Phone { get { return UiFit.Touch; } }
    public static int ParticleMax(int desktop) {
        return Phone ? Mathf.Max(8, Mathf.RoundToInt(desktop * ParticleScale)) : desktop;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot() {
        Apply();
    }

    // Safe to call every scene: it only acts once, and only on a touch device.
    public static void Apply() {
        if (Applied || !UiFit.Touch) {
            return;
        }
        Applied = true;
        QualitySettings.shadows = ShadowQuality.Disable;
        QualitySettings.shadowDistance = 0f;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
        QualitySettings.antiAliasing = 0;
        QualitySettings.softParticles = false;
        QualitySettings.realtimeReflectionProbes = false;
        QualitySettings.lodBias = Mathf.Min(QualitySettings.lodBias, 0.7f);
        QualitySettings.globalTextureMipmapLimit = Mathf.Max(QualitySettings.globalTextureMipmapLimit, 1);
        QualitySettings.pixelLightCount = Mathf.Min(QualitySettings.pixelLightCount, 1);   // each pixel light is another forward pass over every object it touches
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        LightBudget.Install();
    }

    // One line for the playtest timeline.
    public static string Describe() {
        return "applied " + Applied + ", shadows " + QualitySettings.shadows + ", aa " + QualitySettings.antiAliasing
            + ", mip limit " + QualitySettings.globalTextureMipmapLimit + ", pixel lights " + QualitySettings.pixelLightCount
            + ", target fps " + Application.targetFrameRate;
    }
}

// The phone's light budget (J: phone fps p5 was 20 to 24 in the chapter scenes and cutscenes). Chapter maps, levels, the
// hub, the lanterns and the film light each add their own lights with no shared count. On a phone this keeps at most
// MaxPoint point/spot lights switched on (the ones nearest the camera, with a margin so none flickers on and off), at most
// MaxDirectional directional lights (the key and one rim), and no light casting a real-time shadow, so the act looks (set
// by the key light and the grade, never by a lamp's range) stay while the extra forward passes go. Casters are switched off
// on every renderer too: the only shadow on a phone is Wallit's blob. Report() gives the playtest its numbers per scene.
public class LightBudget : MonoBehaviour {
    public const int MaxPoint = 4;
    public const int MaxDirectional = 2;
    const float Every = 0.5f;
    static LightBudget instance;
    public static int Live, Kept, Cut, Casters;
    public static System.Action<string> Sink;   // the playtest's line per scene
    float next;
    string reported;
    float sceneAt;

    public static void Install() {
        if (instance != null || !Application.isPlaying) {
            return;
        }
        var go = new GameObject("Light Budget");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        instance = go.AddComponent<LightBudget>();
    }

    void Update() {
        if (Time.unscaledTime < next) {
            return;
        }
        next = Time.unscaledTime + Every;
        Enforce();
        string sc = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        int loaded = UnityEngine.SceneManagement.SceneManager.sceneCount;
        string key = sc + "+" + loaded;
        if (key != reported) {
            reported = key;
            sceneAt = Time.unscaledTime + 4f;   // let the map and its lighting settle
        } else if (Sink != null && sceneAt > 0f && Time.unscaledTime >= sceneAt) {
            sceneAt = 0f;
            Sink(Report(sc + (loaded > 1 ? " (+map)" : "")));
        }
    }

    static readonly System.Collections.Generic.List<Light> pts = new System.Collections.Generic.List<Light>();
    static readonly System.Collections.Generic.List<float> dist = new System.Collections.Generic.List<float>();

    public static void Enforce() {
        Camera cam = Camera.main;
        Vector3 at = cam != null ? cam.transform.position : Vector3.zero;
        pts.Clear();
        dist.Clear();
        int dirs = 0;
        Live = 0;
        Cut = 0;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) {
            if (l == null || !l.gameObject.activeInHierarchy) {
                continue;
            }
            if (l.shadows != LightShadows.None) {
                l.shadows = LightShadows.None;
            }
            if (l.type == LightType.Directional) {
                if (l.enabled && l.intensity > 0.001f && ++dirs > MaxDirectional && l.name != "Moon" && l.name != "Hub Light") {
                    l.enabled = false;
                    Cut++;
                }
                continue;
            }
            if (!l.enabled && l.GetComponent<LightBudgetHold>() == null) {
                continue;   // the owner switched it off, not the budget
            }
            pts.Add(l);
            dist.Add((l.transform.position - at).sqrMagnitude / Mathf.Max(1f, l.range * l.range));
        }
        // Nearest (scaled by range) first; the current keepers get a margin so two similar lights do not swap every pass.
        var order = new int[pts.Count];
        for (int i = 0; i < order.Length; i++) {
            order[i] = i;
            if (pts[i].enabled) {
                dist[i] *= 0.8f;
            }
        }
        System.Array.Sort(order, (a, b) => dist[a].CompareTo(dist[b]));
        Kept = 0;
        for (int k = 0; k < order.Length; k++) {
            Light l = pts[order[k]];
            bool on = k < MaxPoint;
            if (on) {
                Kept++;
                if (!l.enabled) {
                    l.enabled = true;
                }
            } else if (l.enabled) {
                if (l.GetComponent<LightBudgetHold>() == null) {
                    l.gameObject.AddComponent<LightBudgetHold>();
                }
                l.enabled = false;
                Cut++;
            }
        }
        Live = Kept + Mathf.Min(dirs, MaxDirectional);
        Casters = 0;
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) {
            if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Casters++;
            }
        }
    }

    // The playtest line: the scene, real-time lights now on, lights the budget switched off, casters it cleared, and the
    // renderers the camera sees (draw calls are about that many before batching and instancing).
    public static string Report(string scene) {
        Enforce();
        int visible = 0;
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) {
            if (r.enabled && r.isVisible) {
                visible++;
            }
        }
        return scene + ": lights " + Live + " (budget " + (MaxPoint + MaxDirectional) + "), switched off " + Cut
            + ", shadow casters cleared " + Casters + ", visible renderers " + visible + ", pixel lights " + QualitySettings.pixelLightCount;
    }
}

// Marks a light the budget switched off, so it can be brought back when it is nearer the camera again.
public class LightBudgetHold : MonoBehaviour { }
