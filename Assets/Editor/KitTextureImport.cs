using UnityEditor;
using UnityEngine;

// Gen-made Model Kit textures (Resources/Kit/<set>_a.png, _n.png): albedo is sRGB, the normal map is linear, both repeat.
public class KitTextureImport : AssetPostprocessor {
    void OnPreprocessTexture() {
        if (!assetPath.StartsWith("Assets/Resources/Kit/")) {
            return;
        }
        var ti = (TextureImporter)assetImporter;
        bool normal = assetPath.EndsWith("_n.png");
        ti.textureType = TextureImporterType.Default;
        ti.sRGBTexture = !normal;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.mipmapEnabled = true;
        ti.textureCompression = TextureImporterCompression.Compressed;
        ti.crunchedCompression = false;
        // ASTC 6x6 on web and phones keeps a 512 set near 0.4 MB with mips; the phone tier drops the top mip (MobileQuality).
        var ps = new TextureImporterPlatformSettings { name = "WebGL", overridden = true, maxTextureSize = 1024, format = normal ? TextureImporterFormat.ASTC_5x5 : TextureImporterFormat.ASTC_6x6, textureCompression = TextureImporterCompression.Compressed };
        ti.SetPlatformTextureSettings(ps);
        ps.name = "Android"; ps.format = normal ? TextureImporterFormat.ASTC_5x5 : TextureImporterFormat.ASTC_6x6; ti.SetPlatformTextureSettings(ps);
        ps.name = "iPhone"; ti.SetPlatformTextureSettings(ps);
    }
}
