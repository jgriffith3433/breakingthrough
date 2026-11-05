using UnityEditor;

// Cinematic keyframes (Resources/Cinematics/Art, 1280x720 JPEGs from Hawkes Gen): keep them at their own size (no
// power-of-two rescale, which would squash 16:9 into 1024x512), no mipmaps (they are drawn full screen), clamped, and
// crunch-compressed so 82 stills stay a few MB in the WebGL data. CutsceneShots loads them one shot at a time and unloads
// them when the reel closes.
class CinematicArtImport : AssetPostprocessor
{
    const string Folder = "Assets/Resources/Cinematics/Art/";

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder))
            return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = false;
        importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        importer.filterMode = UnityEngine.FilterMode.Bilinear;
        importer.isReadable = false;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.crunchedCompression = true;
        importer.compressionQuality = 60;
    }
}
