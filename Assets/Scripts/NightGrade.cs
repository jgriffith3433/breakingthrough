using UnityEngine;

// The camera's finishing pass: bloom so glows glow, a cool-shadow / warm-light grade, and a vignette.
// Added to the main camera by NightAtmosphere. Tweak the fields live in the inspector while playing.
[RequireComponent(typeof(Camera))]
public class NightGrade : MonoBehaviour {
    [Header("Bloom")]
    [Range(0f, 2f)] public float Threshold = 0.86f;
    [Range(0f, 1f)] public float Knee = 0.5f;
    [Range(0f, 3f)] public float Intensity = 0.85f;
    [Range(1, 8)] public int Iterations = 6;

    [Header("Grade")]
    [Range(0.5f, 2f)] public float Exposure = 1.08f;
    [Range(0.5f, 1.6f)] public float Contrast = 1.12f;
    [Range(0f, 2f)] public float Saturation = 0.9f;
    [Range(0f, 2f)] public float Vignette = 0.85f;
    public Color Shadows = DeptPalette.GradeShadows;
    public Color Highlights = DeptPalette.GradeHighlights;
    public Color Lift = DeptPalette.GradeLift;

    // Colour-grading LUT strips (256x16) of the two looks being blended, set by FilmLight. LutAmount 0 = off.
    [HideInInspector] public Texture2D LutA, LutB;
    [HideInInspector] public float LutBlend, LutAmount;

    // 0..1: how much of Slice's health is gone (StakesFeel). The post pass drains the screen's edges toward grey by it.
    [HideInInspector] public float Stakes;

    // The one exposure rule every place shares (Arcade acts, hub, chapters, finale): whatever a Look asks for, the pass never
    // exposes above MaxExposure, never lets bloom exceed MaxBloom, and only blooms what is really bright (a threshold at or
    // above BloomFloor). Phones render in 8-bit LDR, where 1.0 is already white, so they get a lower bloom, a higher floor, a
    // later shoulder and a dither that breaks up banding in the vignette and fog gradients. Looks keep their colour and
    // contrast; only the level is held. See Docs/FilmLight.md.
    public const float MaxExposure = 1.04f;
    public static float MaxBloom { get { return UiFit.Touch ? 0.3f : 0.5f; } }
    public static float BloomFloor { get { return UiFit.Touch ? 0.92f : 1.0f; } }
    public static float ShoulderStart { get { return UiFit.Touch ? 0.66f : 0.6f; } }
    public static float BloomClamp { get { return UiFit.Touch ? 2f : 4f; } }   // one pixel feeds bloom at most this bright: a crowd of glows cannot pile up
    public static float DitherAmount { get { return UiFit.Touch ? 1.5f : 0.75f; } }   // in 1/255 steps

    public float EffectiveExposure { get { return Mathf.Min(Exposure, MaxExposure); } }
    public float EffectiveBloom { get { return Mathf.Min(Intensity, MaxBloom); } }
    public float EffectiveThreshold { get { return Mathf.Max(Threshold, BloomFloor); } }

    Material material;
    readonly RenderTexture[] levels = new RenderTexture[8];

    static readonly int FilterId = Shader.PropertyToID("_Filter");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int SourceId = Shader.PropertyToID("_SourceTex");
    static readonly int GradeId = Shader.PropertyToID("_Grade");
    static readonly int ShadowsId = Shader.PropertyToID("_Shadows");
    static readonly int HighlightsId = Shader.PropertyToID("_Highlights");
    static readonly int StakesId = Shader.PropertyToID("_Stakes");
    static readonly int LutAId = Shader.PropertyToID("_LutA");
    static readonly int LutBId = Shader.PropertyToID("_LutB");
    static readonly int LutId = Shader.PropertyToID("_Lut");
    static readonly int ToneId = Shader.PropertyToID("_Tone");
    static readonly int LiftId = Shader.PropertyToID("_Lift");

    void OnRenderImage(RenderTexture source, RenderTexture destination) {
        if (material == null) {
            Shader shader = Shader.Find("Hidden/BT/Post");
            if (shader == null || !shader.isSupported) {
                Graphics.Blit(source, destination);
                enabled = false;
                return;
            }
            material = new Material(shader);
            material.hideFlags = HideFlags.HideAndDontSave;
        }

        float threshold = EffectiveThreshold;
        float knee = threshold * Knee + 0.00001f;
        material.SetVector(FilterId, new Vector4(threshold, threshold - knee, 2f * knee, 0.25f / knee));
        material.SetFloat(IntensityId, EffectiveBloom);
        material.SetVector(GradeId, new Vector4(EffectiveExposure, Contrast, Saturation, Vignette));
        material.SetVector(ToneId, new Vector4(ShoulderStart, BloomClamp, DitherAmount / 255f, 0f));
        material.SetColor(ShadowsId, Shadows);
        material.SetColor(HighlightsId, Highlights);
        material.SetColor(LiftId, Lift);
        material.SetFloat(StakesId, Stakes);
        bool lut = LutA != null && LutB != null && LutAmount > 0f;
        material.SetTexture(LutAId, lut ? LutA : Texture2D.blackTexture);
        material.SetTexture(LutBId, lut ? LutB : Texture2D.blackTexture);
        material.SetVector(LutId, new Vector4(LutBlend, lut ? LutAmount : 0f, 0f, 0f));

        // A phone's screen is 3x its CSS size: start the bloom at a quarter and stop after four steps, or the
        // fill rate alone stalls an iPhone.
        bool phone = UiFit.Touch;
        int iterations = phone ? Mathf.Min(Iterations, 4) : Iterations;
        int width = source.width / (phone ? 4 : 2);
        int height = source.height / (phone ? 4 : 2);
        RenderTextureFormat format = source.format;
        RenderTexture current = RenderTexture.GetTemporary(width, height, 0, format);
        levels[0] = current;
        Graphics.Blit(source, current, material, 0);
        int i = 1;
        for (; i < iterations; i++) {
            width /= 2;
            height /= 2;
            if (height < 2 || width < 2) {
                break;
            }
            RenderTexture next = RenderTexture.GetTemporary(width, height, 0, format);
            levels[i] = next;
            Graphics.Blit(current, next, material, 1);
            current = next;
        }
        for (i -= 2; i >= 0; i--) {
            RenderTexture larger = levels[i];
            levels[i] = null;
            Graphics.Blit(current, larger, material, 2);
            RenderTexture.ReleaseTemporary(current);
            current = larger;
        }
        material.SetTexture(SourceId, source);
        Graphics.Blit(current, destination, material, 3);
        RenderTexture.ReleaseTemporary(current);
    }

    void OnDestroy() {
        if (material != null) {
            Destroy(material);
        }
    }
}
