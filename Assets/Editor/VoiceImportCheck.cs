using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Fails when a voice clip under Assets/Resources/Voice uses an import setting WebGL cannot play reliably: Streaming (WebGL
// has no streaming audio threads), a format other than Vorbis/MP3/AAC/PCM-family that the browser path cannot decode, a
// CompressedInMemory clip that code reads samples from ("Cannot get data on compressed samples"), or short lines that are
// not preloaded. Runs before every player build and from Tools > Audio > Check Voice Imports. Reads settings only.
public class VoiceImportCheck : IPreprocessBuildWithReport
{
    const string Voice = "Assets/Resources/Voice";

    public int callbackOrder { get { return -100; } }

    public void OnPreprocessBuild(BuildReport report)
    {
        string problems = Run();
        if (problems.Length > 0)
            throw new BuildFailedException("Voice clips WebGL cannot play:\n" + problems);
    }

    [MenuItem("Tools/Audio/Check Voice Imports")]
    static void Menu()
    {
        string problems = Run();
        if (problems.Length == 0) Debug.Log("[VoiceImportCheck] every voice clip is WebGL-safe");
        else Debug.LogError("[VoiceImportCheck] Voice clips WebGL cannot play:\n" + problems);
    }

    // Empty when every clip is fine; else one line per problem.
    public static string Run()
    {
        var sb = new StringBuilder();
        var seen = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Voice }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as AudioImporter;
            if (imp == null) continue;
            seen.Add(path);
            AudioImporterSampleSettings s = imp.GetOverrideSampleSettings("WebGL");
            if (!imp.ContainsSampleSettingsOverride("WebGL")) s = imp.defaultSampleSettings;
            if (s.loadType == AudioClipLoadType.Streaming)
                sb.AppendLine(path + ": Load Type Streaming (WebGL cannot stream)");
            if (s.loadType == AudioClipLoadType.CompressedInMemory && s.compressionFormat != AudioCompressionFormat.Vorbis)
                sb.AppendLine(path + ": Compressed In Memory needs Vorbis on WebGL, is " + s.compressionFormat);
            if (s.compressionFormat != AudioCompressionFormat.Vorbis && s.compressionFormat != AudioCompressionFormat.PCM
                && s.compressionFormat != AudioCompressionFormat.ADPCM)
                sb.AppendLine(path + ": format " + s.compressionFormat + " is not WebGL-safe (use Vorbis)");
            if (s.loadType != AudioClipLoadType.DecompressOnLoad && s.loadType != AudioClipLoadType.CompressedInMemory)
                sb.AppendLine(path + ": unsupported load type " + s.loadType);
            if (!s.preloadAudioData && !imp.preloadAudioData)
                sb.AppendLine(path + ": Preload Audio Data is off (first trigger would be late or silent)");
            if (imp.loadInBackground)
                sb.AppendLine(path + ": Load In Background is on (a line can fire before it has loaded)");
        }
        return sb.ToString();
    }
}
