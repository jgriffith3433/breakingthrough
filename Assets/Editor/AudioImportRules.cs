using UnityEditor;
using UnityEngine;

// Audio under Resources goes into the WebGL data file, which the page downloads before anything shows. Most of these clips
// have no .meta in git, so the build machine imported them at Unity's default quality 100: about three times the size of
// the mp3s themselves, and the main reason build 18's data file was 89.6 MB against 33 MB for build 13. Encode them near
// their source instead: voice lines (Gen's 56 kbps mono at 32 kHz) mono at 22 kHz, sound effects at half quality.
// The score is not here: it lives in StreamingAssets/Music and is fetched when it plays (MusicDirector.Fetch).
class AudioImportRules : AssetPostprocessor
{
    const string Voice = "Assets/Resources/Voice/";
    const string Root = "Assets/Resources/";

    public override uint GetVersion()
    {
        return 2;
    }

    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(Root))
            return;
        var importer = assetImporter as AudioImporter;
        if (importer == null)
            return;
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        if (assetPath.StartsWith(Voice))
        {
            importer.forceToMono = true;
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride = 22050;
            settings.quality = 0.35f;
        }
        else
        {
            // Sound effects stay stereo (their level and pan are unchanged), at 32 kHz and a lower Vorbis quality: short
            // hits and swishes, the first download's second-biggest sound folder after the bark keep-set.
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride = 32000;
            settings.quality = 0.4f;
        }
        importer.defaultSampleSettings = settings;
    }
}
