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
        ti.textureCompression = TextureImporterCompression.Uncompressed;
    }
}
