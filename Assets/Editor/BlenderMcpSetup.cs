using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

// MCP for Blender is a socket inside the Blender GUI, not a Unity package.
// This checks that socket and, when it is down, installs the add-on and opens Blender.
[InitializeOnLoad]
static class BlenderMcpSetup
{
    const int Port = 9876;
    const string SessionKey = "BlenderMcpSetup.Ensured";
    const string AddonFileName = "blender_mcp.py";

    const string EnsureScript = @"import bpy

_tries = 0

def ensure_blender_mcp():
    global _tries
    _tries += 1
    if bpy.app.background:
        print('BlenderMCP: a GUI session is required')
        return None
    try:
        import addon_utils
        _enabled, loaded = addon_utils.check('blender_mcp')
        if not loaded:
            addon_utils.enable('blender_mcp', default_set=True, persistent=True)
            try:
                bpy.ops.wm.save_userpref()
            except Exception as exc:
                print('BlenderMCP: preferences were not saved:', exc)
        if bpy.ops.blendermcp.start_server.poll():
            bpy.ops.blendermcp.start_server()
            print('BlenderMCP: server start requested on port', bpy.context.scene.blendermcp_port)
            return None
    except Exception as exc:
        print('BlenderMCP: not ready yet:', exc)
    if _tries >= 20:
        print('BlenderMCP: could not start the server')
        return None
    return 0.5

bpy.app.timers.register(ensure_blender_mcp, first_interval=0.5)
";

    static double _pollUntil;
    static double _nextProbe;
    static bool _blenderWasAlreadyOpen;

    static BlenderMcpSetup()
    {
        EditorApplication.delayCall += AutoEnsureOnce;
    }

    static void AutoEnsureOnce()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (SessionState.GetBool(SessionKey, false))
            return;

        SessionState.SetBool(SessionKey, true);
        Ensure();
    }

    [MenuItem("Tools/Blender MCP/Ensure Server")]
    static void EnsureFromMenu()
    {
        SessionState.SetBool(SessionKey, true);
        Ensure();
    }

    static void Ensure()
    {
        WarnIfCursorConfigIsMissing();

        if (Ping())
        {
            Debug.Log("Blender MCP is listening on port " + Port + ".");
            return;
        }

        if (IsBlenderRunning())
        {
            Debug.Log("Blender is open. Waiting for MCP for Blender on port " + Port + ".");
            BeginPoll(8, blenderWasAlreadyOpen: true);
            return;
        }

        if (!AddonInstalled() && !InstallAddon())
            return;

        string blender = FindBlender();
        if (string.IsNullOrEmpty(blender))
        {
            Debug.LogWarning("Blender MCP is not listening, and blender.exe was not found under Program Files.");
            return;
        }

        if (!LaunchBlender(blender))
            return;

        BeginPoll(30, blenderWasAlreadyOpen: false);
    }

    static void BeginPoll(double seconds, bool blenderWasAlreadyOpen)
    {
        _blenderWasAlreadyOpen = blenderWasAlreadyOpen;
        _pollUntil = EditorApplication.timeSinceStartup + seconds;
        _nextProbe = 0;
        EditorApplication.update -= OnPoll;
        EditorApplication.update += OnPoll;
    }

    static void OnPoll()
    {
        if (EditorApplication.timeSinceStartup < _nextProbe)
            return;

        _nextProbe = EditorApplication.timeSinceStartup + 1.0;

        if (Ping())
        {
            EditorApplication.update -= OnPoll;
            Debug.Log("Blender MCP is listening on port " + Port + ".");
            return;
        }

        if (EditorApplication.timeSinceStartup < _pollUntil)
            return;

        EditorApplication.update -= OnPoll;
        if (_blenderWasAlreadyOpen)
        {
            Debug.LogWarning(
                "Blender is open, but nothing answered a MCP ping on port " + Port + ". " +
                "Enable Interface: MCP for Blender in Edit > Preferences > Add-ons, then in the 3D Viewport press N and start the server from the MCP for Blender tab.");
            return;
        }

        Debug.LogWarning(
            "Blender was started, but MCP for Blender never answered on port " + Port + ". " +
            "Check the Blender system console for a BlenderMCP message.");
    }

    static bool Ping()
    {
        Socket socket = null;
        try
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IAsyncResult connect = socket.BeginConnect("127.0.0.1", Port, null, null);
            if (!connect.AsyncWaitHandle.WaitOne(400))
                return false;

            socket.EndConnect(connect);
            socket.ReceiveTimeout = 1500;
            byte[] payload = Encoding.UTF8.GetBytes("{\"type\":\"ping\",\"params\":{}}");
            socket.Send(payload);

            var buffer = new byte[512];
            int read = socket.Receive(buffer);
            if (read <= 0)
                return false;

            string text = Encoding.UTF8.GetString(buffer, 0, read);
            return text.Contains("\"pong\"");
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            if (socket != null)
                socket.Close();
        }
    }

    static bool IsBlenderRunning()
    {
        return Process.GetProcessesByName("blender").Length > 0;
    }

    static string FindBlender()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string root = Path.Combine(programFiles, "Blender Foundation");
        if (!Directory.Exists(root))
            return null;

        string bestPath = null;
        Version bestVersion = null;
        foreach (string dir in Directory.GetDirectories(root))
        {
            string exe = Path.Combine(dir, "blender.exe");
            if (!File.Exists(exe))
                continue;

            Version version = ParseBlenderVersion(Path.GetFileName(dir));
            if (bestPath == null || (version != null && (bestVersion == null || version > bestVersion)))
            {
                bestPath = exe;
                bestVersion = version;
            }
        }

        return bestPath;
    }

    static Version ParseBlenderVersion(string folderName)
    {
        int space = folderName.LastIndexOf(' ');
        if (space < 0 || space == folderName.Length - 1)
            return null;

        Version version;
        if (Version.TryParse(folderName.Substring(space + 1), out version))
            return version;
        return null;
    }

    static bool AddonInstalled()
    {
        string blender = FindBlender();
        if (string.IsNullOrEmpty(blender))
            return false;

        return File.Exists(AddonPath(blender));
    }

    static string AddonPath(string blenderExe)
    {
        string folder = Path.GetFileName(Path.GetDirectoryName(blenderExe));
        Version version = ParseBlenderVersion(folder);
        string versionFolder = version != null ? version.ToString() : "5.0";
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Blender Foundation", "Blender", versionFolder, "scripts", "addons", AddonFileName);
    }

    static bool InstallAddon()
    {
        string uvx = FindUvx();
        if (string.IsNullOrEmpty(uvx))
        {
            Debug.LogWarning("The Blender MCP add-on is missing, and uvx.exe was not found, so it could not be installed.");
            return false;
        }

        EditorUtility.DisplayProgressBar("Blender MCP", "Installing the MCP for Blender add-on...", 0.5f);
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = uvx,
                Arguments = "mcp-for-blender install-addon",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(start))
            {
                if (process == null)
                    return false;

                if (!process.WaitForExit(120000))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (Exception)
                    {
                    }

                    Debug.LogWarning("Installing the Blender MCP add-on timed out.");
                    return false;
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Installing the Blender MCP add-on failed: " + exception.Message);
            return false;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (AddonInstalled())
            return true;

        Debug.LogWarning("The Blender MCP add-on installer finished, but " + AddonFileName + " is still missing.");
        return false;
    }

    static string FindUvx()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string winget = Path.Combine(local, "Microsoft", "WinGet", "Links", "uvx.exe");
        if (File.Exists(winget))
            return winget;

        string path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
            return null;

        foreach (string entry in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrEmpty(entry))
                continue;

            string candidate = Path.Combine(entry.Trim(), "uvx.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    static bool LaunchBlender(string blenderExe)
    {
        string scriptPath = Path.Combine(Path.GetTempPath(), "breakingthrough-ensure-blender-mcp.py");
        File.WriteAllText(scriptPath, EnsureScript);

        Debug.Log("Starting Blender so MCP for Blender can listen on port " + Port + ".");
        var start = new ProcessStartInfo
        {
            FileName = blenderExe,
            Arguments = "--python \"" + scriptPath + "\"",
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(blenderExe)
        };

        try
        {
            return Process.Start(start) != null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Blender did not start: " + exception.Message);
            return false;
        }
    }

    static void WarnIfCursorConfigIsMissing()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string mcpPath = Path.Combine(projectRoot, ".cursor", "mcp.json");
        if (!File.Exists(mcpPath))
        {
            Debug.LogWarning("Cursor has no .cursor/mcp.json, so it will not launch the Blender MCP client.");
            return;
        }

        string json = File.ReadAllText(mcpPath);
        if (json.IndexOf("mcp-for-blender", StringComparison.Ordinal) < 0)
            Debug.LogWarning(mcpPath + " has no mcp-for-blender server, so Cursor cannot talk to Blender.");
    }
}
