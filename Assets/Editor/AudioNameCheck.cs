using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

// Build check: every music file the game asks for by name must exist in StreamingAssets/Music (the player fetches them by URL,
// so a missing one is a 404 at run time). It reads the names the code passes to MusicDirector, the beds in the reels' JSON,
// the derived names (ch<n>_score, amb_ch<n>, boss_<name>) and the files MusicDirector claims to have made.
// Run: Tools > Build > Check audio names, WebGlBuild (before BuildPlayer) or -executeMethod AudioNameCheck.Run.
public static class AudioNameCheck {
    const string MusicDir = "Assets/StreamingAssets/Music";

    [MenuItem("Tools/Build/Check audio names")]
    public static void Run() {
        List<string> problems = Problems(null);
        foreach (string p in problems) {
            Debug.LogError("[AudioNameCheck] " + p);
        }
        Debug.Log("[AudioNameCheck] " + (problems.Count == 0 ? "ok" : problems.Count + " problem(s)"));
        if (Application.isBatchMode && problems.Count > 0) {
            EditorApplication.Exit(1);
        }
    }

    // The check on a deliberately missing name: it must report it (exit code 1 in batch mode if it does not).
    // Unity.exe -batchmode -quit -executeMethod AudioNameCheck.SelfTest
    public static void SelfTest() {
        bool caught = Problems(new[] { "no_such_cue_for_test" }).Exists(p => p.Contains("no_such_cue_for_test"));
        bool clean = Problems(null).Count == 0;
        Debug.Log("[AudioNameCheck] missing name caught: " + caught + ", real names clean: " + clean);
        if (Application.isBatchMode && !(caught && clean)) {
            EditorApplication.Exit(1);
        }
    }

    // Throws when a name has no file: the build stops before it ships a 404.
    public static void Require() {
        List<string> problems = Problems(null);
        if (problems.Count > 0) {
            throw new BuildFailedException("Audio names with no file:\n" + string.Join("\n", problems.ToArray()));
        }
    }

    // extra: names to treat as asked for too (the deliberate-miss test passes one).
    public static List<string> Problems(IEnumerable<string> extra) {
        var have = new HashSet<string>();
        if (Directory.Exists(MusicDir)) {
            foreach (string f in Directory.GetFiles(MusicDir, "*.mp3")) {
                have.Add(Path.GetFileNameWithoutExtension(f));
            }
        }
        var wanted = new SortedDictionary<string, string>();   // name -> where it is asked for
        Action<string, string> want = (n, where) => { if (!string.IsNullOrEmpty(n) && !wanted.ContainsKey(n)) wanted[n] = where; };

        foreach (string n in MusicDirector.ClaimedFiles()) want(n, "MusicDirector (claimed as made)");
        for (int i = 1; i <= 6; i++) { want("ch" + i + "_score", "ChapterTrack"); want("amb_ch" + i, "AmbienceFor"); }
        foreach (string b in new[] { "The Courier", "Plated Alpha", "Foreman", "Server", "Registrar", "Order-Bearer" }) {
            want(MusicDirector.BossTrack(b), "BossTrack(" + b + ")");
        }
        if (extra != null) foreach (string n in extra) want(n, "extra");

        // Literals in code: Play(track[, fallback]) needs the track only when it claims a file (checked above) and the fallback always;
        // a single-argument Play, Ambience and a rank stinger need their file.
        var play = new Regex("MusicDirector\\.(Play|Ambience|Stinger)\\(\\s*\"([^\"]*)\"(?:\\s*,\\s*\"([^\"]*)\")?\\s*\\)");
        foreach (string cs in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)) {
            string text = File.ReadAllText(cs, Encoding.UTF8);
            foreach (Match m in play.Matches(text)) {
                string kind = m.Groups[1].Value, a = m.Groups[2].Value, fb = m.Groups[3].Value;
                string where = Path.GetFileName(cs) + " " + kind + "(\"" + a + "\")";
                if (kind == "Stinger") continue;   // a stinger with no file plays a stand-in; claimed ones are checked above
                if (fb.Length > 0) { want(fb, where + " fallback"); continue; }
                want(a, where);
            }
        }
        // The beds named by the shot reels (Resources/Cinematics/<id>.json); "pad" is synthesised.
        var bed = new Regex("\"bed\"\\s*:\\s*\"([^\"]*)\"");
        foreach (string js in Directory.GetFiles("Assets/Resources/Cinematics", "*.json")) {
            Match m = bed.Match(File.ReadAllText(js, Encoding.UTF8));
            if (m.Success && m.Groups[1].Value != "pad") want(m.Groups[1].Value, Path.GetFileName(js) + " bed");
        }

        var problems = new List<string>();
        foreach (KeyValuePair<string, string> w in wanted) {
            if (!have.Contains(w.Key)) {
                problems.Add(MusicDir + "/" + w.Key + ".mp3 is missing (asked for by " + w.Value + ")");
            }
        }
        return problems;
    }
}
