using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds the menu and the knoll into Builds/WebGL. Gzip with a fallback so a plain static host can still load it.
public static class WebGlBuild
{
    [MenuItem("Tools/Build/WebGL")]
    public static void Build()
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        string outDir = Path.Combine(root, "Builds", "WebGL");
        Directory.CreateDirectory(outDir);

        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.initialMemorySize = 256;

        var options = new BuildPlayerOptions
        {
            scenes = new[]
            {
                "Assets/MainMenu.unity",
                "Assets/scene2.unity"
            },
            locationPathName = outDir,
            target = BuildTarget.WebGL
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log("WebGL build " + summary.result + " at " + outDir + " (" + summary.totalErrors + " errors).");
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
