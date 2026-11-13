using UnityEditor;
using UnityEngine;

// Cap every texture for the WebGL player (the web build is what phones download). Textures in Resources go into the one
// data file the page fetches before the menu shows, so a 4096 skin costs the same as sixteen 1024 ones. The WebGL override
// caps the size at 1024 and crunch-compresses it; the editor and other platforms keep the source. The cinematic stills have
// their own rule (CinematicArtImport, 1280x720 full-screen keyframes: capped at 1280 here so 2048 never ships) and the
// skins keep SkinImportRules' crunch quality. Runs after those (callbackOrder) so it only ever lowers a size.
class TextureSizeRules : AssetPostprocessor
{
    const int Cap = 1024;
    const int StillCap = 1280;

    public override uint GetVersion()
    {
        return 2;
    }

    public override int GetPostprocessOrder()
    {
        return 100;
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/") || assetPath.StartsWith("Assets/Kit/") || assetPath.StartsWith("Assets/Editor/"))
            return;
        var importer = assetImporter as TextureImporter;
        bool sprite = importer != null && (importer.textureType == TextureImporterType.Sprite || importer.textureType == TextureImporterType.GUI);
        if (importer == null || importer.textureType != TextureImporterType.Default && importer.textureType != TextureImporterType.NormalMap && !sprite)
            return;
        int cap = assetPath.StartsWith("Assets/Resources/Cinematics/Art/") ? StillCap : Cap;
        TextureImporterPlatformSettings web = importer.GetPlatformTextureSettings("WebGL");
        web.name = "WebGL";
        web.overridden = true;
        web.maxTextureSize = web.maxTextureSize > 0 ? Mathf.Min(web.maxTextureSize, cap) : cap;
        web.maxTextureSize = Mathf.Min(web.maxTextureSize, Mathf.Max(importer.maxTextureSize, 32), cap);
        web.format = TextureImporterFormat.Automatic;
        web.textureCompression = TextureImporterCompression.Compressed;
        web.crunchedCompression = true;
        // UI art (sprites) keeps a higher crunch quality: flat colour and hard edges show blocks first. Before this rule a sprite
        // texture shipped with whatever its .meta said (often uncompressed RGBA).
        web.compressionQuality = sprite ? 75 : 60;
        importer.SetPlatformTextureSettings(web);
    }
}
