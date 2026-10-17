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
            BuildWebGLTo(Path.Combine(versionFolder, ProductFolder));
            UpdateGamePageVersion(GamePage, next);
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

    public static void BuildWebGLTo(string buildPath)
    {
        if (Directory.Exists(buildPath))
        {
            Directory.Delete(buildPath, true);
        }

        Directory.CreateDirectory(buildPath);
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.initialMemorySize = 256;

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[]
            {
                "Assets/MainMenu.unity",
                "Assets/scene2.unity"
            },
            locationPathName = buildPath,
            target = BuildTarget.WebGL
        });

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

    public static void UpdateGamePageVersion(string gamePage, int version)
    {
        string html = File.ReadAllText(gamePage);
        string updated = Regex.Replace(
            html,
            @"window\.(PLACE_BUILD|GAME_BUILD)\s*=\s*""v\d+""",
            match => "window." + match.Groups[1].Value + " = \"v" + version + "\"");
        if (updated == html && !Regex.IsMatch(html, @"window\.(PLACE_BUILD|GAME_BUILD)\s*=\s*""v" + version + @""""))
        {
            throw new Exception("Could not find window.PLACE_BUILD or window.GAME_BUILD in " + gamePage);
        }

        File.WriteAllText(gamePage, updated);
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
        foreach (string page in SitePages(SiteRoot))
        {
            string html = File.ReadAllText(page);
            string updated = WithFooter(html, template, Path.GetFullPath(page) == landing);
            if (updated != html)
            {
                File.WriteAllText(page, updated);
                changed++;
            }
        }
        Debug.Log("[WebGlBuild] Site footer: " + changed + " page(s) updated");
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
