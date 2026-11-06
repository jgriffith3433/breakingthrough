using UnityEditor;

// The painted skins Wallit, Slice and the bots are drawn with (Resources/Wallit, Resources/Slice, Resources/Models/Bots) have no
// .meta in git, so the build machine imported them as plain DXT with mipmaps: about 9 MB of the WebGL data file. Crunch them
// (same size, same mipmaps, same alpha glow) so they cost a few hundred KB each. Nothing reads their pixels at runtime.
class SkinImportRules : AssetPostprocessor
{
    static readonly string[] Folders =
    {
        "Assets/Resources/Wallit/",
        "Assets/Resources/Slice/",
        "Assets/Resources/Models/Bots/"
    };

    public override uint GetVersion()
    {
        return 1;
    }

    void OnPreprocessTexture()
    {
        bool skin = false;
        foreach (string folder in Folders)
        {
            if (assetPath.StartsWith(folder) && assetPath.EndsWith(".png"))
                skin = true;
        }
        if (!skin)
            return;
        var importer = assetImporter as TextureImporter;
        if (importer == null)
            return;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.crunchedCompression = true;
        importer.compressionQuality = 75;
    }
}
