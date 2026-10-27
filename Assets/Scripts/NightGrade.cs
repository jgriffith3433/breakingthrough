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
    public Color Shadows = new Color(0.8f, 0.9f, 1.16f);
    public Color Highlights = new Color(1.08f, 1.0f, 0.9f);
    public Color Lift = new Color(0.0f, 0.012f, 0.03f);

    Material material;
    readonly RenderTexture[] levels = new RenderTexture[8];

    static readonly int FilterId = Shader.PropertyToID("_Filter");
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    static readonly int SourceId = Shader.PropertyToID("_SourceTex");
    static readonly int GradeId = Shader.PropertyToID("_Grade");
    static readonly int ShadowsId = Shader.PropertyToID("_Shadows");
    static readonly int HighlightsId = Shader.PropertyToID("_Highlights");
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

        float knee = Threshold * Knee + 0.00001f;
        material.SetVector(FilterId, new Vector4(Threshold, Threshold - knee, 2f * knee, 0.25f / knee));
        material.SetFloat(IntensityId, Intensity);
        material.SetVector(GradeId, new Vector4(Exposure, Contrast, Saturation, Vignette));
        material.SetColor(ShadowsId, Shadows);
        material.SetColor(HighlightsId, Highlights);
        material.SetColor(LiftId, Lift);

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
