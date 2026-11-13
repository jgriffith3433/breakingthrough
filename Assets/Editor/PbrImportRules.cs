using UnityEditor;
using UnityEngine;

// The Gen-painted PBR sets (Resources/Pbr/hollow and Resources/Pbr/hub, PbrKit): <set>_a.png is sRGB with roughness in its alpha, <set>_n.png is a
// linear tangent-space normal read as plain RGB by Knoll/PbrSplat and Knoll/KitLit. Both repeat, with mips and trilinear filtering.
// The web, Android and iOS get ASTC (6x6 albedo, 5x5 normal): a 512 set is about 0.15 MB with mips, and the phone tier drops the top mip
// (MobileQuality). No hand-made .meta files: the build server imports these with defaults, so the rule sets everything (lesson of
// AudioImportRules and SkinImportRules). TextureSizeRules skips this folder so its crunch override never replaces ASTC.
class PbrImportRules : AssetPostprocessor {
    public const string Folder = "Assets/Resources/Pbr/";

    public override uint GetVersion() { return 1; }

    void OnPreprocessTexture() {
        if (!assetPath.StartsWith(Folder)) {
            return;
        }
        var ti = (TextureImporter)assetImporter;
        bool normal = assetPath.EndsWith("_n.png");
        ti.textureType = TextureImporterType.Default;
        ti.sRGBTexture = !normal;
        ti.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;   // the albedo's alpha is roughness data
        ti.alphaIsTransparency = false;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.filterMode = FilterMode.Trilinear;
        ti.anisoLevel = 4;
        ti.mipmapEnabled = true;
        ti.isReadable = false;
        ti.maxTextureSize = 512;
        ti.textureCompression = TextureImporterCompression.Compressed;
        ti.crunchedCompression = false;
        var ps = new TextureImporterPlatformSettings {
            name = "WebGL", overridden = true, maxTextureSize = 512,
            format = normal ? TextureImporterFormat.ASTC_5x5 : TextureImporterFormat.ASTC_6x6,
            textureCompression = TextureImporterCompression.Compressed
        };
        ti.SetPlatformTextureSettings(ps);
        ps.name = "Android"; ti.SetPlatformTextureSettings(ps);
        ps.name = "iPhone"; ti.SetPlatformTextureSettings(ps);
    }
}
