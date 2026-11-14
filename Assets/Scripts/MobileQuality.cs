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
    }

    // One line for the playtest timeline.
    public static string Describe() {
        return "applied " + Applied + ", shadows " + QualitySettings.shadows + ", aa " + QualitySettings.antiAliasing
            + ", mip limit " + QualitySettings.globalTextureMipmapLimit + ", pixel lights " + QualitySettings.pixelLightCount
            + ", target fps " + Application.targetFrameRate;
    }
}
