using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Tools → Build → WebGL. Each publish lands in <site>/Game/vN/<gameName> and the site's game page is pointed at it.
// The site defaults to C:\Projects\theplacedepartmentwebsite (the folder next to this project).
public class WebGlDeployWindow : EditorWindow
{
    private int detectedVersion = -1;
    private int newVersion = 1;
    private Vector2 scroll;
    private string log = "";
    private bool busy;

    [MenuItem("Tools/Build/WebGL")]
    public static void ShowWindow()
    {
        var window = GetWindow<WebGlDeployWindow>("WebGL Deploy");
        window.minSize = new Vector2(480, 520);
        window.RefreshVersionInfo();
    }

    private void OnEnable()
    {
        RefreshVersionInfo();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Breaking Through — WebGL Deploy", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (detectedVersion >= 1)
        {
            EditorGUILayout.HelpBox("Current version: v" + detectedVersion + "\nNew version:     v" + newVersion, MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("No published build yet. The next build will be v1.", MessageType.Info);
        }

        if (GUILayout.Button("Refresh Version Info"))
        {
            RefreshVersionInfo();
        }

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        string site = EditorGUILayout.TextField("Website folder", WebGlBuild.SiteRoot);
        if (GUILayout.Button("Browse", GUILayout.Width(70)))
        {
            string picked = EditorUtility.OpenFolderPanel("Website folder", WebGlBuild.SiteRoot, "");
            if (!string.IsNullOrEmpty(picked))
            {
                site = picked;
            }
        }
        EditorGUILayout.EndHorizontal();
        if (site != WebGlBuild.SiteRoot)
        {
            WebGlBuild.SiteRoot = site;
            RefreshVersionInfo();
        }
        DrawReadOnly("Game page", WebGlBuild.GamePage);
        DrawReadOnly("Build output", Path.Combine(WebGlBuild.GameFolder, "v" + newVersion, WebGlBuild.ProductFolder));
        DrawReadOnly("Local game", WebGlBuild.LocalUrl);
        if (!File.Exists(WebGlBuild.GamePage))
        {
            EditorGUILayout.HelpBox("No Game/index.html in that folder. Pick the website folder.", MessageType.Warning);
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Steps", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(busy))
        {
            if (GUILayout.Button("1. Create Version Folder"))
            {
                RunStep(CreateVersionFolder);
            }

            if (GUILayout.Button("2. Build WebGL"))
            {
                RunStep(BuildWebGL);
            }

            if (GUILayout.Button("3. Point Game Page at New Version"))
            {
                RunStep(UpdateGamePage);
            }

            if (GUILayout.Button("4. Delete Previous Version"))
            {
                RunStep(DeleteOldVersion);
            }

            if (GUILayout.Button("5. Start Site and Open Browser"))
            {
                RunStep(StartSite);
            }
        }

        EditorGUILayout.Space(8);
        using (new EditorGUI.DisabledScope(busy))
        {
            var previous = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.75f, 0.4f);
            if (GUILayout.Button("Run Full Deploy", GUILayout.Height(36)))
            {
                ConfirmAndDeploy();
            }

            GUI.backgroundColor = previous;
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(log, GUILayout.ExpandHeight(true));
        if (GUILayout.Button("Clear Log"))
        {
            log = "";
        }

        EditorGUILayout.EndScrollView();
    }

    private void ConfirmAndDeploy()
    {
        RefreshVersionInfo();
        if (!EditorUtility.DisplayDialog(
                "Full Deploy",
                "This will:\n" +
                "1. Create " + Path.Combine(WebGlBuild.GameFolder, "v" + newVersion) + "\n" +
                "2. Build WebGL into that folder as " + WebGlBuild.ProductFolder + "\n" +
                "3. Point the game page at v" + newVersion + "\n" +
                "4. Delete the previous build folder\n" +
                "5. Start the local site and open the game\n\n" +
                "The editor will be busy while Unity compiles the WebGL player.\n\n" +
                "Continue?",
                "Deploy",
                "Cancel"))
        {
            return;
        }

        busy = true;
        log = "";
        Log("Deploy queued.");
        EditorApplication.delayCall += () =>
        {
            try
            {
                Log("Full deploy started " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                CreateVersionFolder();
                BuildWebGL();
                UpdateGamePage();
                DeleteOldVersion();
                StartSite();
                Log("Deploy complete. Version v" + newVersion);
            }
            catch (Exception ex)
            {
                Log("DEPLOY FAILED: " + ex.Message);
                RemoveIncompleteVersion();
                Debug.LogError("[WebGlBuild] " + ex);
            }
            finally
            {
                busy = false;
                Repaint();
            }
        };
    }

    private void RefreshVersionInfo()
    {
        detectedVersion = WebGlBuild.DetectVersion(WebGlBuild.GameFolder);
        newVersion = Math.Max(1, detectedVersion + 1);
        Repaint();
    }

    private void CreateVersionFolder()
    {
        RefreshVersionInfo();
        string folder = Path.Combine(WebGlBuild.GameFolder, "v" + newVersion);
        Log("Creating " + folder);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }

        Directory.CreateDirectory(folder);
    }

    private void BuildWebGL()
    {
        string buildPath = Path.Combine(WebGlBuild.GameFolder, "v" + newVersion, WebGlBuild.ProductFolder);
        Log("Building WebGL to " + buildPath);
        WebGlBuild.BuildWebGLTo(buildPath);
        Log("WebGL build finished.");
    }

    private void UpdateGamePage()
    {
        Log("Pointing " + WebGlBuild.GamePage + " at v" + newVersion);
        WebGlBuild.UpdateGamePageVersion(WebGlBuild.GamePage, newVersion);
        WebGlBuild.InstallSoundPrompt(WebGlBuild.GamePage, newVersion);
        WebGlBuild.InstallLoadQuiet(WebGlBuild.GamePage, newVersion);
        WebGlBuild.InstallPlaytestBridge(WebGlBuild.GamePage, newVersion);
        Log("Installing What's new (whatsnew.html, releases.json, js/hawkes-release.js and its links)");
        WebGlBuild.InstallWhatsNew(WebGlBuild.GamePage, newVersion);
        Log("Stamping the site footer onto the pages outside Game/");
        WebGlBuild.InstallSiteFooter();
    }

    private void RemoveIncompleteVersion()
    {
        string buildDir = Path.Combine(WebGlBuild.GameFolder, "v" + newVersion, WebGlBuild.ProductFolder, "Build");
        bool finished = Directory.Exists(buildDir) && Directory.GetFiles(buildDir, "*.wasm.unityweb").Length > 0;
        if (finished)
        {
            return;
        }

        string folder = Path.Combine(WebGlBuild.GameFolder, "v" + newVersion);
        if (!Directory.Exists(folder))
        {
            return;
        }

        Directory.Delete(folder, true);
        Log("Removed incomplete " + folder);
    }

    private void DeleteOldVersion()
    {
        if (detectedVersion < 1 || detectedVersion == newVersion)
        {
            Log("No previous version folder to delete.");
            return;
        }

        string oldFolder = Path.Combine(WebGlBuild.GameFolder, "v" + detectedVersion);
        Log("Deleting " + oldFolder);
        if (Directory.Exists(oldFolder))
        {
            Directory.Delete(oldFolder, true);
        }
    }

    private void StartSite()
    {
        string bat = Path.Combine(WebGlBuild.SiteRoot, "auto-start.bat");
        Log("Starting " + bat);
        if (!File.Exists(bat))
        {
            throw new Exception("auto-start.bat was not found: " + bat);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c \"" + bat + "\"",
            WorkingDirectory = WebGlBuild.SiteRoot,
            UseShellExecute = true
        });

        string url = WebGlBuild.LocalUrl;
        EditorApplication.delayCall += () => Application.OpenURL(url);
        Log("Opened " + url);
    }

    private void RunStep(Action action)
    {
        busy = true;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.Message);
            Debug.LogError("[WebGlBuild] " + ex);
        }
        finally
        {
            busy = false;
            Repaint();
        }
    }

    private void Log(string message)
    {
        log += message + "\n";
        Repaint();
    }

    private static void DrawReadOnly(string label, string value)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(label);
        EditorGUILayout.SelectableLabel(value, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        EditorGUILayout.EndHorizontal();
    }
}

public static class WebGlBuild
{
    const string SiteKey = "bt_webgl_site_root";
    const string DefaultSiteName = "theplacedepartmentwebsite";
    const string DefaultGameName = "PlaceDepartment";
    const string SoundScript = "hawkes-sound.js";
    const string LoadQuietScript = "hawkes-load-quiet.js";
    const string PlaytestBridgeScript = "hawkes-playtest-bridge.js";

    public static string ProjectRoot
    {
        get { return Directory.GetParent(Application.dataPath).FullName; }
    }

    // The website the game is published into. Remembered per machine; defaults to the sibling folder.
    public static string SiteRoot
    {
        get
        {
            string saved = EditorPrefs.GetString(SiteKey, "");
            if (!string.IsNullOrEmpty(saved))
            {
                return saved;
            }
            return Path.Combine(Directory.GetParent(ProjectRoot).FullName, DefaultSiteName);
        }
        set { EditorPrefs.SetString(SiteKey, value); }
    }

    public static string GameFolder
    {
        get { return Path.Combine(SiteRoot, "Game"); }
    }

    // Unity names the build files after the output folder, so this must match the page's gameName.
    public static string ProductFolder
    {
        get
        {
            if (File.Exists(GamePage))
            {
                Match match = Regex.Match(File.ReadAllText(GamePage), @"var\s+gameName\s*=\s*""([^""]+)""");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            return DefaultGameName;
        }
    }

    // The site's own server port, read from its auto-start.bat.
    public static string LocalUrl
    {
        get
        {
            int port = 4173;
            string bat = Path.Combine(SiteRoot, "auto-start.bat");
            if (File.Exists(bat))
            {
                Match match = Regex.Match(File.ReadAllText(bat), @"localhost:(\d+)");
                if (match.Success)
                {
                    port = int.Parse(match.Groups[1].Value);
                }
            }
            return "http://localhost:" + port + "/Game/";
        }
    }

    public static string GamePage
    {
        get { return Path.Combine(GameFolder, "index.html"); }
    }

    public static void Build()
    {
        FullDeploy();
    }

    /// The cloud build (Hawkes Studio's project build entry, method "WebGlBuild.CloudBuild"): Hawkes Gen runs it with
    /// env HAWKES_OUT, the folder whose contents are the build, and no website checkout. It builds the same player as
    /// FullDeploy into HAWKES_OUT/PlaceDepartment (laid out like the website's Game/v(n)/) and changes no website: Studio
    /// writes Game/build.json and window.HAWKES_BUILD_URL into the site's own Game/index.html (publish.page "site").
    public static void CloudBuild()
    {
        bool batch = Application.isBatchMode;
        try
        {
            string outDir = Environment.GetEnvironmentVariable("HAWKES_OUT");
            if (string.IsNullOrEmpty(outDir))
            {
                throw new Exception("HAWKES_OUT is not set (the cloud build names the folder the build goes into)");
            }
            Directory.CreateDirectory(outDir);
            int version = 0;
            int.TryParse(Environment.GetEnvironmentVariable("BUILD_VERSION") ?? "", out version);
            WriteBuildStamp(version);
            try
            {
                BuildWebGLTo(Path.Combine(outDir, DefaultGameName));
            }
            finally
            {
                RemoveBuildStamp();
            }
            // The website's Game page loads <build address>PlaceDepartment/Build/PlaceDepartment.loader.js: a build without
            // it would publish and then 404 on the site (build web 5 did), so it fails here instead.
            string loader = Path.Combine(outDir, DefaultGameName, "Build", DefaultGameName + ".loader.js");
            if (!File.Exists(loader))
            {
                throw new Exception("the build has no " + DefaultGameName + "/Build/" + DefaultGameName + ".loader.js (the Game page loads it from there)");
            }
            // A file beside the game's folder, so the build is never one lone folder: Studio unwraps an output that is a
            // single folder, and the site's page loads PlaceDepartment/Build/... from the build's address.
            File.WriteAllText(Path.Combine(outDir, "hawkes-build.json"),
                "{\"game\":\"" + DefaultGameName + "\",\"commit\":\"" + (Environment.GetEnvironmentVariable("HAWKES_COMMIT") ?? "") + "\"}\n");
            // The Unity project's own beta address in Studio serves this folder's root: send it to the game's own page in
            // PlaceDepartment/ (Unity's template), keeping the query (playtest flags), so it plays instead of "Not found".
            File.WriteAllText(Path.Combine(outDir, "index.html"),
                "<!doctype html>\n<meta charset=\"utf-8\">\n<title>The Place Department</title>\n" +
                "<script>location.replace(\"" + DefaultGameName + "/index.html\" + location.search + location.hash);</script>\n" +
                "<p><a href=\"" + DefaultGameName + "/index.html\">Play The Place Department</a></p>\n");
            Debug.Log("[WebGlBuild] Cloud build done in " + outDir);
            if (batch)
            {
                EditorApplication.Exit(0);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[WebGlBuild] Cloud build FAILED: " + ex.Message + "\n" + ex.StackTrace);
            if (batch)
            {
                EditorApplication.Exit(1);
            }
            else
            {
                throw;
            }
        }
    }

    public static void FullDeploy()
    {
        bool batch = Application.isBatchMode;
        int next = 1;
        try
        {
            if (!File.Exists(GamePage))
            {
                throw new Exception("Game page not found: " + GamePage);
            }

            int current = DetectVersion(GameFolder);
            next = current + 1;
            if (next < 1)
            {
                next = 1;
            }

            string versionFolder = Path.Combine(GameFolder, "v" + next);
            if (Directory.Exists(versionFolder))
            {
                Directory.Delete(versionFolder, true);
            }

            Directory.CreateDirectory(versionFolder);
            WriteBuildStamp(next);
            try
            {
                BuildWebGLTo(Path.Combine(versionFolder, ProductFolder));
            }
            finally
            {
                RemoveBuildStamp();
            }
            UpdateGamePageVersion(GamePage, next);
            InstallSoundPrompt(GamePage, next);
            InstallLoadQuiet(GamePage, next);
            InstallPlaytestBridge(GamePage, next);
            InstallWhatsNew(GamePage, next);
            InstallSiteFooter();

            if (current >= 1 && current != next)
            {
                string oldFolder = Path.Combine(GameFolder, "v" + current);
                if (Directory.Exists(oldFolder))
                {
                    Directory.Delete(oldFolder, true);
                }
            }

            Debug.Log("[WebGlBuild] Published v" + next);
            if (batch)
            {
                EditorApplication.Exit(0);
            }
        }
        catch (Exception ex)
        {
            RemoveIncomplete(next);
            Debug.LogError("[WebGlBuild] FAILED: " + ex.Message + "\n" + ex.StackTrace);
            if (batch)
            {
                EditorApplication.Exit(1);
            }
            else
            {
                throw;
            }
        }
    }

    public static int DetectVersion(string gameFolder)
    {
        int max = 0;
        if (!Directory.Exists(gameFolder))
        {
            return max;
        }

        foreach (string dir in Directory.GetDirectories(gameFolder))
        {
            Match match = Regex.Match(Path.GetFileName(dir), @"^v(\d+)$");
            if (!match.Success)
            {
                continue;
            }

            int version = int.Parse(match.Groups[1].Value);
            if (version > max)
            {
                max = version;
            }
        }

        return max;
    }

    // Hawkes Analytics: every event says which build the player is on. The build carries
    // Resources/HawkesBuild.json ({ version, commit, date }) only while it is being made; RunAnalytics hands it to the
    // page as window.HAWKES_BUILD. The version is the player version plus the Game/v<n> folder ("1.0+v12"). The commit
    // is HAWKES_COMMIT when the build sets it, else this checkout's HEAD when nothing tracked differs from it (the
    // build server's copy is the merge base plus uploaded edits, so its HEAD is not the commit being built).
    const string BuildStampAsset = "Assets/Resources/HawkesBuild.json";

    public static void WriteBuildStamp(int version)
    {
        string commit = Environment.GetEnvironmentVariable("HAWKES_COMMIT") ?? "";
        if (!Regex.IsMatch(commit, "^[0-9a-fA-F]{4,40}$"))
        {
            commit = "";
            string head = Git("rev-parse HEAD");
            if (Regex.IsMatch(head, "^[0-9a-f]{40}$") && Git("status --porcelain --untracked-files=no") == "")
            {
                commit = head;
            }
        }
        string json = "{\"version\":\"" + Application.version + "+v" + version + "\""
            + (commit != "" ? ",\"commit\":\"" + commit.ToLowerInvariant() + "\"" : "")
            + ",\"date\":\"" + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") + "\"}";
        File.WriteAllText(Path.Combine(ProjectRoot, BuildStampAsset), json);
        AssetDatabase.ImportAsset(BuildStampAsset, ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("[WebGlBuild] Build stamp: " + json);
    }

    public static void RemoveBuildStamp()
    {
        if (File.Exists(Path.Combine(ProjectRoot, BuildStampAsset)))
        {
            AssetDatabase.DeleteAsset(BuildStampAsset);
        }
    }

    // One git command in this project, or "" when git isn't there or fails.
    static string Git(string args)
    {
        try
        {
            var info = new ProcessStartInfo("git", "-C \"" + ProjectRoot + "\" " + args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (Process git = Process.Start(info))
            {
                string output = git.StandardOutput.ReadToEnd();
                git.StandardError.ReadToEnd();
                if (!git.WaitForExit(15000) || git.ExitCode != 0)
                {
                    return "";
                }
                return output.Trim();
            }
        }
        catch (Exception)
        {
            return "";
        }
    }

    public static void BuildWebGLTo(string buildPath)
    {
        SetupWallit.EnsureBuiltForDeploy();
        SetupSlice.EnsureBuiltForDeploy();
        SetupBots.EnsureBuiltForDeploy();
        if (Directory.Exists(buildPath))
        {
            Directory.Delete(buildPath, true);
        }

        Directory.CreateDirectory(buildPath);
        // Gzip, never Brotli. The build's address (Game/build.json's base on sites.hawkesgen.ai) sends the .unityweb files as
        // application/octet-stream with no Content-Encoding, so the browser never unpacks them itself. The Game page's loader
        // streams a gzip file through the browser's native DecompressionStream while it downloads; it cannot do that for
        // Brotli, so a Brotli build reached Unity's JavaScript decoder, which unpacked the whole ~18 MB on the main thread
        // after the last byte: a minute of frozen page on the phone twin (build 135), and gameplay never came. Gzip costs a
        // few MB more on the wire and unpacks for free. The campaign's heavy files are not in the data file at all
        // (OnDemandPack), so this is the menu and Arcade only. Engine modules nothing references are stripped from the player.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;   // a returning player's browser keeps the data file
        PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
        PlayerSettings.stripEngineCode = true;
        // The wasm is the second-largest first download (7 MB): size-optimised IL2CPP, managed code stripped to what is used,
        // and exception support only for exceptions the game throws itself (no implicit null/bounds checks wired through wasm).
        PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
        // High strips the most managed code (build 128 had Medium). Exception support stays at explicitly thrown ones only: the
        // save loader and the on-demand fetch catch exceptions, and with None a caught exception would end the page instead.
        PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        // No wasm trap checks on integer divide (the game never relies on one), and the emscripten pass that optimises for the
        // smallest .wasm (-Oz, no LTO so the cloud build stays as fast as before) instead of runtime speed.
        PlayerSettings.WebGL.wasmArithmeticExceptions = WebGLWasmArithmeticExceptions.Ignore;
        UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.DiskSize;
        PlayerSettings.WebGL.initialMemorySize = 256;
        // Phones kill a tab that grows past about 1.5 GB: the heap may grow from 256 MB but not to 2 GB.
        PlayerSettings.WebGL.maximumMemorySize = 1536;
        PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
        // Unity's splash logo alone was ~2.7 MB in the data file (build 121); phones never need it.
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;

        // Rebuild the Arena so the player ships instanced scatter (shared FBX meshes) instead of the old combined
        // ArenaScatter_*.asset blobs (~40 MB of the data file on build 121), and clear retired bow model refs.
        BuildArena.RebuildForPlayerBuild();

        // The campaign's voices, stills, portraits, skies and boss meshes leave the data file for the build's StreamingAssets/OnDemand
        // (OnDemandPack): the originals are moved aside for the build and always moved back.
        BuildReport report;
        try
        {
            OnDemandPack.Prepare();
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[]
            {
                "Assets/MainMenu.unity",
                "Assets/scene2.unity",
                "Assets/Campaign.unity",
                "Assets/Maps/Map_Hollow.unity",
                "Assets/Maps/Map_Edge.unity",
                "Assets/Maps/Map_Floor.unity",
                "Assets/Maps/Map_Walk.unity",
                "Assets/Maps/Map_Reading.unity",
                "Assets/Maps/Map_Filed.unity",
                "Assets/Maps/Map_Office.unity",
                "Assets/Maps/Level_1_Yard.unity"
                // KitPreview is an editor gallery, not part of the playable first download.
            },
            locationPathName = buildPath,
            target = BuildTarget.WebGL
        });
        }
        finally
        {
            OnDemandPack.Restore();
        }

        if (report.summary.result == BuildResult.Succeeded)
        {
            RequireGzip(buildPath);
            OnDemandPack.Install(buildPath);
            LogSizes(report, buildPath);
        }

        if (report.summary.result != BuildResult.Succeeded)
        {
            var details = new StringBuilder();
            details.Append("WebGL build failed: ").Append(report.summary.result);
            foreach (var step in report.steps)
            {
                foreach (var message in step.messages)
                {
                    if (message.type != LogType.Error && message.type != LogType.Exception)
                    {
                        continue;
                    }

                    details.AppendLine();
                    details.Append(message.content);
                }
            }

            throw new Exception(details.ToString());
        }
    }

    // What the first download holds, by folder, and the biggest assets in it, from the build report: logged and written beside the
    // build as build-report.txt. AGENTS.md ("Download size") keeps the before and after of the on-demand split.
    // The Game page unpacks only gzip without the main thread (see the compression setting in BuildWebGLTo): a data, wasm or
    // framework file in any other format would freeze a phone after the download, so the build fails here instead.
    static void RequireGzip(string buildPath)
    {
        string buildDir = Path.Combine(buildPath, "Build");
        foreach (string kind in new[] { ".data.unityweb", ".wasm.unityweb", ".framework.js.unityweb" })
        {
            string[] files = Directory.Exists(buildDir) ? Directory.GetFiles(buildDir, "*" + kind) : new string[0];
            if (files.Length == 0)
            {
                throw new Exception("the build has no *" + kind + " (the Game page loads it)");
            }
            byte[] head = new byte[2];
            using (FileStream stream = File.OpenRead(files[0]))
            {
                if (stream.Read(head, 0, 2) < 2 || head[0] != 0x1f || head[1] != 0x8b)
                {
                    throw new Exception(Path.GetFileName(files[0]) + " is not gzip: the Game page would hand it to Unity's JavaScript unpacker and freeze the page");
                }
            }
        }
    }

    static void LogSizes(BuildReport report, string buildPath)
    {
        var text = new StringBuilder();
        long data = 0, wasm = 0, onDemand = 0, other = 0;
        string buildDir = Path.Combine(buildPath, "Build");
        if (Directory.Exists(buildDir))
        {
            foreach (string file in Directory.GetFiles(buildDir))
            {
                long length = new FileInfo(file).Length;
                string name = Path.GetFileName(file).ToLowerInvariant();
                if (name.Contains(".data")) data += length;
                else if (name.Contains(".wasm")) wasm += length;
                else other += length;
            }
        }
        string onDemandDir = Path.Combine(buildPath, "StreamingAssets", "OnDemand");
        if (Directory.Exists(onDemandDir))
        {
            foreach (string file in Directory.GetFiles(onDemandDir))
            {
                onDemand += new FileInfo(file).Length;
            }
        }
        text.AppendLine("first download (Build/): data " + Mb(data) + " MB, wasm " + Mb(wasm) + " MB, loader and framework " + Mb(other) + " MB");
        text.AppendLine("on demand (StreamingAssets/OnDemand): " + Mb(onDemand) + " MB, fetched per chapter on the player's choice");
        text.AppendLine("uncompressed build size by the report: " + Mb((long)report.summary.totalSize) + " MB");
        var byFolder = new SortedDictionary<string, long>();
        var big = new List<KeyValuePair<string, long>>();
        foreach (PackedAssets packed in report.packedAssets)
        {
            foreach (PackedAssetInfo info in packed.contents)
            {
                string path = info.sourceAssetPath ?? "";
                if (path.Length == 0)
                {
                    continue;
                }
                string[] parts = path.Split('/');
                string folder = parts.Length > 3 ? parts[0] + "/" + parts[1] + "/" + parts[2] : parts.Length > 1 ? parts[0] + "/" + parts[1] : path;
                long have;
                byFolder.TryGetValue(folder, out have);
                byFolder[folder] = have + (long)info.packedSize;
                big.Add(new KeyValuePair<string, long>(path, (long)info.packedSize));
            }
        }
        // By kind (the before/after table in the pull request reads these): the same packed sizes, grouped by what the asset is.
        var byKind = new SortedDictionary<string, long>();
        foreach (PackedAssets packed in report.packedAssets)
        {
            foreach (PackedAssetInfo info in packed.contents)
            {
                string kind = KindOf(info.type, info.sourceAssetPath ?? "");
                long have;
                byKind.TryGetValue(kind, out have);
                byKind[kind] = have + (long)info.packedSize;
            }
        }
        text.AppendLine("in the data file, by kind (before compression):");
        foreach (var pair in byKind)
        {
            text.AppendLine("  " + pair.Key + ": " + Mb(pair.Value) + " MB");
        }
        text.AppendLine("in the data file, by folder:");
        foreach (var pair in byFolder)
        {
            if (pair.Value > 262144)
            {
                text.AppendLine("  " + pair.Key + ": " + Mb(pair.Value) + " MB");
            }
        }
        big.Sort((a, b) => b.Value.CompareTo(a.Value));
        text.AppendLine("largest assets:");
        for (int i = 0; i < big.Count && i < 25; i++)
        {
            text.AppendLine("  " + Mb(big[i].Value) + " MB  " + big[i].Key);
        }
        string result = text.ToString();
        Debug.Log("[WebGlBuild] Download size\n" + OnDemandPack.Report + result);
        File.WriteAllText(Path.Combine(buildPath, "build-report.txt"), OnDemandPack.Report + result);
    }

    static string Mb(long bytes)
    {
        return (bytes / 1048576.0).ToString("0.0");
    }

    // The build report's category for one packed asset: textures, audio, meshes (models), animation clips, shaders, scenes,
    // fonts, scripts and the rest.
    static string KindOf(Type type, string path)
    {
        if (type == typeof(Texture2D) || type == typeof(Cubemap) || type == typeof(RenderTexture)) return "textures";
        if (type == typeof(AudioClip)) return "audio";
        if (type == typeof(Mesh)) return "meshes";
        if (type == typeof(AnimationClip) || type == typeof(Avatar)) return "animation";
        if (type == typeof(Shader)) return "shaders";
        if (type == typeof(Font)) return "fonts";
        if (type == typeof(Material)) return "materials";
        if (type == typeof(TextAsset)) return "text data";
        if (type == typeof(MonoScript)) return "scripts";
        if (path.EndsWith(".unity")) return "scenes";
        return "other";
    }

    public static void UpdateGamePageVersion(string gamePage, int version)
    {
        string html = File.ReadAllText(gamePage);
        string q = "\"";
        string pattern = "window\\.(PLACE_BUILD|GAME_BUILD)\\s*=\\s*" + q + "v\\d+" + q;
        string updated = Regex.Replace(
            html,
            pattern,
            match => "window." + match.Groups[1].Value + " = " + q + "v" + version + q);
        string current = "window\\.(PLACE_BUILD|GAME_BUILD)\\s*=\\s*" + q + "v" + version + q;
        if (updated == html && !Regex.IsMatch(html, current))
        {
            throw new Exception("Could not find window.PLACE_BUILD or window.GAME_BUILD in " + gamePage);
        }

        File.WriteAllText(gamePage, updated);
    }

    // The studio's "Tap to play" prompt (Tools/Web/hawkes-sound.js): copied next to the game page, with a <script> for
    // it at the end of the page's <head>, so it is there before Unity's loader makes an AudioContext. Run on every
    // deploy, so the page always has this project's copy; the ?v= makes browsers fetch a new one.
    public static void InstallSoundPrompt(string gamePage, int version)
    {
        string source = Path.Combine(ProjectRoot, "Tools", "Web", SoundScript);
        if (!File.Exists(source))
        {
            throw new Exception("The Tap to play prompt is missing: " + source);
        }

        // LF, as the website repo keeps its files (this project's checkout can be CRLF on Windows).
        string gameDir = Path.GetDirectoryName(gamePage);
        File.WriteAllText(Path.Combine(gameDir, SoundScript), File.ReadAllText(source).Replace("\r\n", "\n"));
        // Cast portraits for the Tap card (Wallit, Slice, Yo Bot, Spider Bot) so first paint shows the game.
        string castSrc = Path.Combine(ProjectRoot, "Tools", "Web", "cast");
        if (Directory.Exists(castSrc))
        {
            string castDst = Path.Combine(gameDir, "cast");
            Directory.CreateDirectory(castDst);
            foreach (string file in Directory.GetFiles(castSrc))
            {
                string name = Path.GetFileName(file);
                if (name.StartsWith("."))
                {
                    continue;
                }
                File.Copy(file, Path.Combine(castDst, name), true);
            }
        }
        string html = File.ReadAllText(gamePage);
        string tag = "<script src=\"" + SoundScript + "?v=v" + version + "\" data-hawkes-sound></script>";
        string updated = Regex.Replace(html, @"<script[^>]*data-hawkes-sound[^>]*></script>", tag);
        if (updated == html && !html.Contains(tag))
        {
            int head = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
            if (head < 0)
            {
                throw new Exception("No </head> in " + gamePage + " for the Tap to play prompt");
            }
            string newline = html.Contains("\r\n") ? "\r\n" : "\n";
            updated = html.Insert(head, "  " + tag + newline);
        }

        if (updated != html)
        {
            File.WriteAllText(gamePage, updated);
        }
    }

    // Studio reads the Game page's window.hawkesPlaytest stand-in before Unity's bridge replaces it, so it only saw the
    // stand-in's own ids. This writes every scenario id the build registers (read from the PlaytestScenario rows in
    // Assets/Scripts/Playtest*.cs) to scenarios.json next to the page, installs Tools/Web/hawkes-playtest-bridge.js
    // (which patches the stand-in to list them and forwards start to Unity, and dismisses the portrait gate) and tags
    // the page. Runs on every deploy.
    public static void InstallPlaytestBridge(string gamePage, int version)
    {
        string source = Path.Combine(ProjectRoot, "Tools", "Web", PlaytestBridgeScript);
        if (!File.Exists(source))
        {
            throw new Exception("The playtest bridge script is missing: " + source);
        }

        var ids = new List<string>();
        foreach (string file in Directory.GetFiles(Path.Combine(Application.dataPath, "Scripts"), "Playtest*.cs"))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(file), "new PlaytestScenario\\(\"([a-z0-9][a-z0-9-]*)\""))
            {
                if (!ids.Contains(m.Groups[1].Value))
                {
                    ids.Add(m.Groups[1].Value);
                }
            }
        }

        string folder = Path.GetDirectoryName(gamePage);
        File.WriteAllText(Path.Combine(folder, "scenarios.json"), "{\"scenarios\":[\"" + string.Join("\",\"", ids.ToArray()) + "\"]}");
        File.WriteAllText(Path.Combine(folder, PlaytestBridgeScript), File.ReadAllText(source).Replace("\r\n", "\n"));
        string html = File.ReadAllText(gamePage);
        string tag = "<script src=\"" + PlaytestBridgeScript + "?v=v" + version + "\" data-hawkes-playtest-bridge></script>";
        string updated = Regex.Replace(html, @"<script[^>]*data-hawkes-playtest-bridge[^>]*></script>", tag);
        if (updated == html && !html.Contains("data-hawkes-playtest-bridge"))
        {
            // First script in <head>, so it is in place before the page's stand-in is assigned.
            Match head = Regex.Match(html, @"<head[^>]*>");
            if (!head.Success)
            {
                throw new Exception("No <head> in " + gamePage + " for the playtest bridge script");
            }
            string newline = html.Contains("\r\n") ? "\r\n" : "\n";
            updated = html.Insert(head.Index + head.Length, newline + "  " + tag);
        }

        if (updated != html)
        {
            File.WriteAllText(gamePage, updated);
        }
    }

    // Keeps first-five's wordsMinuteOne under 40: progress %/MB and rotating load lines must not create distinct
    // document.body.innerText lines. Tools/Web/hawkes-load-quiet.js pins those to data-* / one short line; the site's
    // Game/index.html should already do the same (the-place-department PR for the loader). Install on every FullDeploy.
    public static void InstallLoadQuiet(string gamePage, int version)
    {
        string source = Path.Combine(ProjectRoot, "Tools", "Web", LoadQuietScript);
        if (!File.Exists(source))
        {
            throw new Exception("The load-quiet script is missing: " + source);
        }

        File.WriteAllText(Path.Combine(Path.GetDirectoryName(gamePage), LoadQuietScript), File.ReadAllText(source).Replace("\r\n", "\n"));
        string html = File.ReadAllText(gamePage);
        string tag = "<script src=\"" + LoadQuietScript + "?v=v" + version + "\" data-hawkes-load-quiet></script>";
        string updated = Regex.Replace(html, @"<script[^>]*data-hawkes-load-quiet[^>]*></script>", tag);
        if (updated == html && !html.Contains("data-hawkes-load-quiet"))
        {
            // Sit just before the sound prompt when present, else before </head>.
            Match sound = Regex.Match(html, @"<script[^>]*data-hawkes-sound[^>]*></script>");
            string newline = html.Contains("\r\n") ? "\r\n" : "\n";
            if (sound.Success)
            {
                updated = html.Insert(sound.Index, "  " + tag + newline);
            }
            else
            {
                int head = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
                if (head < 0)
                {
                    throw new Exception("No </head> in " + gamePage + " for the load-quiet script");
                }
                updated = html.Insert(head, "  " + tag + newline);
            }
        }

        if (updated != html)
        {
            File.WriteAllText(gamePage, updated);
        }
    }

    // The site's "What's new", the way the other Hawkes sites have it, from Tools/Web on every deploy:
    // - js/hawkes-release.js (the studio's release script) and whatsnew.html (with the site footer already on it, so
    //   InstallSiteFooter leaves it alone) are this project's: the site gets this copy each time.
    // - releases.json only when the site has none. After that the site's copy is the record: Hawkes Studio adds a
    //   release at every go live, and a deploy must not take it back. The first release gets this deploy's build.
    // - a "What's new" link with the version on the landing page (in the nav, before Play) and on the game page (the
    //   version in the corner), each a block between <!-- hawkes-whatsnew --> markers that is replaced, not added again.
    // Files that didn't change aren't rewritten.
    const string WhatsNewStart = "<!-- hawkes-whatsnew";
    const string WhatsNewEnd = "<!-- /hawkes-whatsnew -->";

    static string WebTemplate(string name)
    {
        string path = Path.Combine(ProjectRoot, "Tools", "Web", name);
        if (!File.Exists(path))
        {
            throw new Exception("What's new is missing a file: " + path);
        }
        // LF, as the website repo keeps its files (this project's checkout can be CRLF on Windows).
        return File.ReadAllText(path).Replace("\r\n", "\n");
    }

    static void WriteIfChanged(string file, string text)
    {
        if (File.Exists(file) && File.ReadAllText(file) == text)
        {
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        File.WriteAllText(file, text);
    }

    public static void InstallWhatsNew(string gamePage, int version)
    {
        WriteIfChanged(Path.Combine(SiteRoot, "js", "hawkes-release.js"), WebTemplate("hawkes-release.js"));

        string page = WebTemplate("whatsnew.html");
        if (File.Exists(FooterTemplate))
        {
            page = WithFooter(page, File.ReadAllText(FooterTemplate).Replace("\r\n", "\n").Trim(), false);
        }
        WriteIfChanged(Path.Combine(SiteRoot, "whatsnew.html"), page);

        string releases = Path.Combine(SiteRoot, "releases.json");
        if (!File.Exists(releases))
        {
            var empty = new Regex("\"build\"\\s*:\\s*\"\"");
            File.WriteAllText(releases, empty.Replace(WebTemplate("releases.json"), "\"build\": \"" + version + "\"", 1));
            Debug.Log("[WebGlBuild] What's new: wrote the first releases.json (build " + version + ")");
        }

        string landing = Path.Combine(SiteRoot, "index.html");
        if (File.Exists(landing))
        {
            string html = File.ReadAllText(landing);
            string updated = WithBlock(html, WebTemplate("whatsnew-landing.html"), "<a class=\"nav-play\"", false);
            if (updated == html && !html.Contains(WhatsNewStart))
            {
                Debug.LogWarning("[WebGlBuild] What's new: no <a class=\"nav-play\"> on " + landing + " to put the link before");
            }
            WriteIfChanged(landing, updated);
        }

        string game = File.ReadAllText(gamePage);
        string block = WebTemplate("whatsnew-game.html").Replace("hawkes-release.js\"", "hawkes-release.js?v=v" + version + "\"");
        string withLink = WithBlock(game, block, "</body>", true);
        if (withLink == game && !game.Contains(WhatsNewStart))
        {
            throw new Exception("No </body> in " + gamePage + " for the What's new link");
        }
        WriteIfChanged(gamePage, withLink);
    }

    // Replaces the block between the What's new markers, or puts it on its own line before the anchor (the first one,
    // or the last with last), indented like the anchor's line.
    public static string WithBlock(string html, string block, string anchor, bool last)
    {
        string nl = html.Contains("\r\n") ? "\r\n" : "\n";
        block = block.Replace("\r\n", "\n").Trim().Replace("\n", nl);

        int start = html.IndexOf(WhatsNewStart, StringComparison.Ordinal);
        if (start >= 0)
        {
            int end = html.IndexOf(WhatsNewEnd, start, StringComparison.Ordinal);
            if (end >= 0)
            {
                int from = html.LastIndexOf('\n', Math.Max(0, start - 1)) + 1;
                string pad = html.Substring(from, start - from);
                if (!Regex.IsMatch(pad, @"^[ \t]*$"))
                {
                    pad = "";
                }
                return html.Substring(0, start) + block.Replace(nl, nl + pad) + html.Substring(end + WhatsNewEnd.Length);
            }
        }

        int at = last
            ? html.LastIndexOf(anchor, StringComparison.OrdinalIgnoreCase)
            : html.IndexOf(anchor, StringComparison.OrdinalIgnoreCase);
        if (at < 0)
        {
            return html;
        }
        int lineStart = html.LastIndexOf('\n', Math.Max(0, at - 1)) + 1;
        string indent = html.Substring(lineStart, at - lineStart);
        if (!Regex.IsMatch(indent, @"^[ \t]*$"))
        {
            return html.Substring(0, at) + block + nl + html.Substring(at);
        }
        return html.Substring(0, lineStart) + indent + block.Replace(nl, nl + indent) + nl + html.Substring(lineStart);
    }

    // The "Powered by HAWKES GEN" footer (Tools/Web/site-footer.html) goes on every page of the site except the game:
    // each .html outside Game/. The landing page (the root index.html) also keeps the Hawkes Biomedical line between
    // the template's landing markers; other pages drop it. A page that has the footer gets it replaced, so editing the
    // template and deploying again updates every page. Pages that didn't change aren't rewritten.
    const string FooterStart = "<!-- hawkes-footer";
    const string FooterEnd = "<!-- /hawkes-footer -->";

    public static string FooterTemplate
    {
        get { return Path.Combine(ProjectRoot, "Tools", "Web", "site-footer.html"); }
    }

    public static void InstallSiteFooter()
    {
        if (!File.Exists(FooterTemplate))
        {
            Debug.LogWarning("[WebGlBuild] No footer template at " + FooterTemplate + "; the site pages keep their old footer");
            return;
        }

        string template = File.ReadAllText(FooterTemplate).Replace("\r\n", "\n").Trim();
        string landing = Path.GetFullPath(Path.Combine(SiteRoot, "index.html"));
        int changed = 0;
        List<string> listed = new List<string>();
        foreach (string page in SitePages(SiteRoot))
        {
            string html = File.ReadAllText(page);
            string updated = WithFooter(html, template, Path.GetFullPath(page) == landing);
            if (updated != html)
            {
                File.WriteAllText(page, updated);
                changed++;
            }
            if (updated.Contains(FooterStart))
            {
                listed.Add("/" + Path.GetRelativePath(SiteRoot, page).Replace('\\', '/'));
            }
        }
        Debug.Log("[WebGlBuild] Site footer: " + changed + " page(s) updated");
        WritePageList(listed);
    }

    // site/pages.json: every page that carries the footer, so the `site` playtest checks them all, linked or not.
    static void WritePageList(List<string> pages)
    {
        pages.Sort(StringComparer.Ordinal);
        string json = "{\n  \"note\": \"Written by breakingthrough's WebGL deploy: the pages with the Hawkes footer\",\n  \"pages\": [\n"
            + string.Join(",\n", pages.ConvertAll(p => "    \"" + p.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""))
            + "\n  ]\n}\n";
        string file = Path.Combine(SiteRoot, "site", "pages.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        if (!File.Exists(file) || File.ReadAllText(file) != json)
        {
            File.WriteAllText(file, json);
        }
        Debug.Log("[WebGlBuild] Site page list: " + pages.Count + " page(s) in site/pages.json");
    }

    // Every .html in the site, skipping the game (Game/), dot folders (.git) and node_modules.
    static IEnumerable<string> SitePages(string root)
    {
        foreach (string file in Directory.GetFiles(root, "*.html"))
        {
            yield return file;
        }
        foreach (string dir in Directory.GetDirectories(root))
        {
            string name = Path.GetFileName(dir);
            if (name.StartsWith(".") || name == "node_modules" || (Path.GetFullPath(dir) == Path.GetFullPath(GameFolder)))
            {
                continue;
            }
            foreach (string file in SitePages(dir))
            {
                yield return file;
            }
        }
    }

    public static string WithFooter(string html, string template, bool landing)
    {
        string block = landing
            ? Regex.Replace(template, @"[ \t]*<!-- /?landing -->\n", "")
            : Regex.Replace(template, @"[ \t]*<!-- landing -->[\s\S]*?<!-- /landing -->\n", "");
        string nl = html.Contains("\r\n") ? "\r\n" : "\n";
        block = block.Replace("\n", nl);

        int start = html.IndexOf(FooterStart, StringComparison.Ordinal);
        if (start >= 0)
        {
            int end = html.IndexOf(FooterEnd, start, StringComparison.Ordinal);
            if (end >= 0)
            {
                int from = html.LastIndexOf('\n', Math.Max(0, start - 1)) + 1;
                string pad = html.Substring(from, start - from);
                if (!Regex.IsMatch(pad, @"^[ \t]*$"))
                {
                    pad = "";
                }
                return html.Substring(0, start) + block.Replace(nl, nl + pad) + html.Substring(end + FooterEnd.Length);
            }
        }

        // New: after the page's last </main>, or else just before </body>, indented like the line it follows.
        int at = html.LastIndexOf("</main>", StringComparison.OrdinalIgnoreCase);
        if (at >= 0)
        {
            at += "</main>".Length;
        }
        else
        {
            at = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                return html;
            }
            at = html.LastIndexOf('\n', at) + 1;
        }
        int lineStart = html.LastIndexOf('\n', Math.Max(0, at - 1)) + 1;
        string indent = Regex.Match(html.Substring(lineStart), @"^[ \t]*").Value;
        string indented = indent + block.Replace(nl, nl + indent);
        return at == lineStart
            ? html.Substring(0, at) + indented + nl + html.Substring(at)
            : html.Substring(0, at) + nl + nl + indented + html.Substring(at);
    }

    private static void RemoveIncomplete(int version)
    {
        string buildDir = Path.Combine(GameFolder, "v" + version, ProductFolder, "Build");
        bool finished = Directory.Exists(buildDir) && Directory.GetFiles(buildDir, "*.wasm.unityweb").Length > 0;
        if (finished)
        {
            return;
        }

        string folder = Path.Combine(GameFolder, "v" + version);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }
}
