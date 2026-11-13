using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

// Takes the campaign's heavy files out of the WebGL data file (the first download) and lays them beside the build instead, so
// the menu and Arcade load without them and a chapter fetches its own on the player's choice (OnDemand.cs reads them).
//
//   Prepare()   before BuildPlayer: reads the files under Assets/Resources that only the campaign uses, converts the pictures
//               (stills: JPG at most 1280 wide; portraits and skies capped, PNG only where they have alpha), names every file by
//               its content hash, writes Temp/OnDemand/index.json with each file's key, bytes and groups, and moves the originals
//               to Assets/OnDemandStage~ (a folder Unity does not import), so the player build does not pack them.
//   Restore()   after BuildPlayer, in a finally: moves them back (and OnLoad does too if the editor died mid-build), so git sees
//               no change. Nothing is ever deleted or re-exported: the sources are only moved and moved back, with their .meta.
//   Install()   copies Temp/OnDemand into <build>/StreamingAssets/OnDemand, which the page serves as plain files.
//
// What moves: Resources/Voice (every cast line, including Wallit/Slice lines that are not in the Arcade bark keep-set),
// Voice/cinema, Cinematics/Art, Portraits, Skies and the Baked boss, lodger and prop meshes. Groups: "core" (portraits, skies,
// the hub's lodgers and office pieces), "ch1".."ch6" (the stills and voices of the chapter's reels, the lines of its script and
// Story data, its bosses) and "reel_<id>" (one reel's own files, for a reel played on its own). A voice line no chapter names is
// fetched by key when spoken. Arcade keep-set lines stay in Resources so the first swing still has a voice file without a fetch.
[InitializeOnLoad]
public static class OnDemandPack {
    const string Resources_ = "Assets/Resources/";
    const string StageDir = "Assets/OnDemandStage~/";
    const int StillWidth = 1280;
    const int PortraitWidth = 768;
    const int SkyWidth = 2048;

    class Item {
        public string asset;      // Assets/Resources/Voice/x.mp3
        public string key;        // Voice/x
        public string rel;        // path under Resources, with extension
        public byte[] bytes;      // what ships
        public string ext;        // extension of what ships
        public int sourceBytes;
        public readonly SortedSet<string> groups = new SortedSet<string>();
    }

    static readonly List<Item> staged = new List<Item>();
    public static string Report = "";

    static OnDemandPack() {
        // An editor that died between Prepare and Restore leaves its files in the stage folder: put them back.
        EditorApplication.delayCall += () => {
            if (!BuildPipeline.isBuildingPlayer && Directory.Exists(StageDir.TrimEnd('/')) && staged.Count == 0) {
                RestoreFolder();
            }
        };
    }

    static string TempDir {
        get { return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp", "OnDemand"); }
    }

    static string Root {
        get { return Directory.GetParent(Application.dataPath).FullName; }
    }

    // ---------------------------------------------------------------- prepare

    public static void Prepare() {
        staged.Clear();
        Report = "";
        if (Directory.Exists(TempDir)) {
            Directory.Delete(TempDir, true);
        }
        Directory.CreateDirectory(TempDir);
        if (Directory.Exists(StageDir.TrimEnd('/'))) {
            RestoreFolder();
        }

        // 1. what moves
        foreach (string path in Directory.GetFiles(Path.Combine(Root, "Assets", "Resources"), "*", SearchOption.AllDirectories)) {
            string asset = path.Replace('\\', '/');
            asset = asset.Substring(asset.IndexOf("Assets/Resources/", StringComparison.Ordinal));
            if (asset.EndsWith(".meta")) {
                continue;
            }
            string rel = asset.Substring(Resources_.Length);
            Item item = Classify(asset, rel);
            if (item != null) {
                staged.Add(item);
            }
        }

        // 2. what goes with which chapter
        var voiceKeys = new HashSet<string>();
        foreach (Item i in staged) {
            if (i.key.StartsWith("Voice/") && !i.key.StartsWith("Voice/cinema/")) {
                voiceKeys.Add(i.key);
            }
        }
        Dictionary<string, List<string>> reels = ReelFiles(staged);
        AssignGroups(staged, reels, voiceKeys);

        // 3. convert and name
        var index = new StringBuilder("{\"files\":[");
        bool first = true;
        long shipped = 0, source = 0;
        var perGroup = new SortedDictionary<string, long>();
        var perGroupFiles = new SortedDictionary<string, int>();
        foreach (Item item in staged) {
            Convert(item);
            string hash = Hash(item.bytes);
            string file = item.key.Replace('/', '_') + "." + hash + item.ext;
            File.WriteAllBytes(Path.Combine(TempDir, file), item.bytes);
            if (!first) {
                index.Append(',');
            }
            first = false;
            index.Append("{\"k\":\"").Append(item.key).Append("\",\"f\":\"").Append(file).Append("\",\"b\":").Append(item.bytes.Length)
                .Append(",\"g\":\"").Append(string.Join(" ", item.groups)).Append("\"}");
            shipped += item.bytes.Length;
            source += item.sourceBytes;
            string g = item.groups.Count == 0 ? "(by key)" : item.groups.Count == 1 ? Only(item.groups) : "(shared)";
            long have;
            perGroup.TryGetValue(g, out have);
            perGroup[g] = have + item.bytes.Length;
            int n;
            perGroupFiles.TryGetValue(g, out n);
            perGroupFiles[g] = n + 1;
        }
        index.Append("]}");
        File.WriteAllText(Path.Combine(TempDir, "index.json"), index.ToString());

        var report = new StringBuilder();
        report.AppendLine("[OnDemandPack] " + staged.Count + " files leave the first download: " + Mb(source) + " MB in Resources, " + Mb(shipped) + " MB shipped on demand");
        foreach (var pair in perGroup) {
            report.AppendLine("[OnDemandPack]   " + pair.Key + ": " + perGroupFiles[pair.Key] + " files, " + Mb(pair.Value) + " MB");
        }
        Report = report.ToString();
        Debug.Log(Report);

        // 4. move the originals out of the build's reach
        foreach (Item item in staged) {
            string from = Path.Combine(Root, item.asset);
            string to = Path.Combine(Root, StageDir + item.rel);
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            File.Move(from, to);
            if (File.Exists(from + ".meta")) {
                File.Move(from + ".meta", to + ".meta");
            }
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    static string Only(SortedSet<string> set) {
        foreach (string s in set) {
            return s.StartsWith("reel_") ? "(reel only)" : s;
        }
        return "";
    }

    static string Mb(long bytes) {
        return (bytes / 1048576.0).ToString("0.0");
    }

    static string Hash(byte[] bytes) {
        using (MD5 md5 = MD5.Create()) {
            byte[] h = md5.ComputeHash(bytes);
            var sb = new StringBuilder();
            for (int i = 0; i < 4; i++) {
                sb.Append(h[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }

    // Which files leave the data file. null keeps a file in Resources.
    static Item Classify(string asset, string rel) {
        string lower = rel.ToLowerInvariant();
        string ext = Path.GetExtension(lower);
        string name = Path.GetFileName(lower);
        Item item = new Item { asset = asset, rel = rel };
        if (lower.StartsWith("voice/")) {
            if (ext != ".mp3" && ext != ".wav" && ext != ".ogg") {
                return null;   // audio.json stays
            }
            if (ext != ".mp3") {
                return null;   // only Gen's mp3s are fetched as MPEG
            }
            string id = Path.GetFileNameWithoutExtension(rel);
            // Arcade's first-minute bark banks stay in the data file; campaign and the long Wallit/Slice catalogues leave.
            if (WallitVoice.IsArcadeKeep(id) || SliceVoice.IsArcadeKeep(id)) {
                return null;
            }
            item.key = rel.Substring(0, rel.Length - ext.Length);
            return item;
        }
        if (lower.StartsWith("cinematics/art/") && (ext == ".png" || ext == ".jpg" || ext == ".jpeg")) {
            item.key = rel.Substring(0, rel.Length - ext.Length);
            return item;
        }
        if ((lower.StartsWith("portraits/") || lower.StartsWith("skies/") || lower.StartsWith("loadcards/")) && (ext == ".png" || ext == ".jpg" || ext == ".jpeg")) {
            item.key = rel.Substring(0, rel.Length - ext.Length);
            return item;
        }
        if (lower.StartsWith("pbr/hub/") && ext == ".png") {
            item.key = rel.Substring(0, rel.Length - ext.Length);   // the hub's PBR sets: bytes as painted (normals and roughness must not go lossy)
            return item;
        }
        if (lower.StartsWith("baked/") && name.EndsWith(".mesh.txt")) {
            item.key = rel.Substring(0, rel.Length - ".txt".Length);   // Baked/<id>.mesh, what BakedMesh asks Resources for
            return item;
        }
        return null;
    }

    static void Convert(Item item) {
        byte[] src = File.ReadAllBytes(Path.Combine(Root, item.asset));
        item.sourceBytes = src.Length;
        string lower = item.rel.ToLowerInvariant();
        string ext = Path.GetExtension(lower);
        if (item.key.StartsWith("Voice/")) {
            item.bytes = src;
            item.ext = ".mp3";
        } else if (item.key.StartsWith("Pbr/")) {
            item.bytes = src;
            item.ext = ".png";
        } else if (item.key.StartsWith("Baked/")) {
            item.bytes = src;
            item.ext = ".txt";
        } else {
            int cap = item.key.StartsWith("Cinematics/") || item.key.StartsWith("LoadCards/") ? StillWidth : item.key.StartsWith("Skies/") ? SkyWidth : PortraitWidth;
            EncodePicture(item, src, cap, ext);
        }
    }

    // A picture at most `cap` wide: JPG (quality 82) when it has no transparency, else PNG.
    static void EncodePicture(Item item, byte[] src, int cap, string srcExt) {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(src, false)) {
            UnityEngine.Object.DestroyImmediate(tex);
            item.bytes = src;
            item.ext = srcExt == ".jpeg" ? ".jpg" : srcExt;
            return;
        }
        bool alpha = false;
        Color32[] px = tex.GetPixels32();
        for (int i = 0; i < px.Length; i += 7) {
            if (px[i].a < 250) {
                alpha = true;
                break;
            }
        }
        Texture2D use = tex;
        if (tex.width > cap) {
            int w = cap;
            int h = Mathf.Max(1, Mathf.RoundToInt(tex.height * (cap / (float)tex.width)));
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            use = new Texture2D(w, h, TextureFormat.RGBA32, false);
            use.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            use.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }
        if (alpha) {
            item.bytes = use.EncodeToPNG();
            item.ext = ".png";
        } else {
            item.bytes = use.EncodeToJPG(82);
            item.ext = ".jpg";
        }
        if (use != tex) {
            UnityEngine.Object.DestroyImmediate(use);
        }
        UnityEngine.Object.DestroyImmediate(tex);
    }

    // ---------------------------------------------------------------- groups

    // The reel a cinematic file belongs to and the chapter that plays it: ch<n>_open/_close are chapter n's, the boss intros are
    // their boss's chapter (Courier 1, Foreman 3, Server 4, Order-Bearer 6), the endings and credits are chapter 6's.
    static string ChapterOfReel(string id) {
        if (id.StartsWith("ch") && id.Length > 3 && char.IsDigit(id[2])) {
            return "ch" + id[2];
        }
        switch (id) {
            case "boss_courier": return "ch1";
            case "boss_foreman": return "ch3";
            case "boss_server": return "ch4";
            case "boss_order": return "ch6";
        }
        if (id.StartsWith("ending_") || id == "credits") {
            return "ch6";
        }
        return null;
    }

    // reel id -> the keys (stills and cinema voices) it uses.
    static Dictionary<string, List<string>> ReelFiles(List<Item> items) {
        var result = new Dictionary<string, List<string>>();
        var keys = new HashSet<string>();
        foreach (Item i in items) {
            keys.Add(i.key);
        }
        string dir = Path.Combine(Root, "Assets", "Resources", "Cinematics");
        if (!Directory.Exists(dir)) {
            return result;
        }
        foreach (string path in Directory.GetFiles(dir, "*.json")) {
            string json = File.ReadAllText(path);
            if (!json.Contains("\"shots\"")) {
                continue;
            }
            Cutscene.Reel reel = null;
            try {
                reel = JsonUtility.FromJson<Cutscene.Reel>(json);
            } catch (Exception) {
                reel = null;
            }
            if (reel == null || reel.shots == null || string.IsNullOrEmpty(reel.id)) {
                continue;
            }
            var list = new List<string>();
            foreach (Cutscene.Shot s in reel.shots) {
                AddIfKept(list, keys, "Cinematics/Art/" + s.back);
                AddIfKept(list, keys, "Cinematics/Art/" + s.front);
                AddIfKept(list, keys, "Cinematics/Art/" + s.alt);
                AddIfKept(list, keys, "Voice/cinema/" + reel.id + "_" + s.id);
                if (!string.IsNullOrEmpty(s.line) && s.speaker != "W" && s.speaker != "S") {
                    AddIfKept(list, keys, "Voice/" + CastVoice.FileOf(s.speaker, s.line));
                }
            }
            result[reel.id] = list;
        }
        return result;
    }

    static void AddIfKept(List<string> list, HashSet<string> keys, string key) {
        if (!key.EndsWith("/") && keys.Contains(key) && !list.Contains(key)) {
            list.Add(key);
        }
    }

    static void AssignGroups(List<Item> items, Dictionary<string, List<string>> reels, HashSet<string> voiceKeys) {
        var byKey = new Dictionary<string, Item>();
        foreach (Item i in items) {
            byKey[i.key] = i;
        }
        // reels
        foreach (var pair in reels) {
            string chapter = ChapterOfReel(pair.Key);
            foreach (string k in pair.Value) {
                Item item = byKey[k];
                item.groups.Add("reel_" + pair.Key);
                if (chapter != null) {
                    item.groups.Add(chapter);
                }
            }
        }
        // core: portraits, skies, the hub's and office's meshes
        foreach (Item i in items) {
            if (i.key.StartsWith("Portraits/") || i.key.StartsWith("Skies/") || i.key.StartsWith("Pbr/hub/")) {
                i.groups.Add("core");
            } else if (i.key.StartsWith("LoadCards/")) {
                // a chapter's loading card comes with that chapter (card_ch3 -> ch3); the hub's is core
                string id = i.key.Substring("LoadCards/card_".Length);
                i.groups.Add(id.StartsWith("ch") ? id : "core");
            } else if (i.key.StartsWith("Baked/")) {
                string id = i.key.Substring("Baked/".Length);
                string g = BakedGroup(id);
                i.groups.Add(g);
            }
        }
        // the campaign script and Story data: every line a chapter says that has a voice file
        var chapterLines = new Dictionary<string, HashSet<string>>();
        CampaignScript.Doc doc = CampaignScript.Load();
        if (doc != null && doc.chapters != null) {
            var all = new List<CampaignScript.Chapter>(doc.chapters);
            if (doc.ending != null) {
                all.Add(doc.ending);
            }
            foreach (CampaignScript.Chapter c in all) {
                string group = c.n > 0 ? "ch" + c.n : "ch6";
                var set = Lines(chapterLines, group);
                foreach (CampaignScript.Scene sc in c.scenes ?? new CampaignScript.Scene[0]) {
                    foreach (string l in sc.lines ?? new string[0]) {
                        Candidates(set, l);
                    }
                }
                foreach (CampaignScript.Choice ch in c.choices ?? new CampaignScript.Choice[0]) {
                    foreach (CampaignScript.Option op in ch.options ?? new CampaignScript.Option[0]) {
                        foreach (string l in (op.now ?? "").Split('\n')) {
                            Candidates(set, l);
                        }
                    }
                }
            }
        }
        if (Story.Chapters != null) {
            for (int c = 0; c < Story.Chapters.Length; c++) {
                var set = Lines(chapterLines, "ch" + (c + 1));
                Walk(Story.Chapters[c], set, 0);
            }
        }
        foreach (var pair in chapterLines) {
            foreach (string fileKey in pair.Value) {
                Item item;
                if (byKey.TryGetValue("Voice/" + fileKey, out item)) {
                    item.groups.Add(pair.Key);
                }
            }
        }
    }

    static HashSet<string> Lines(Dictionary<string, HashSet<string>> map, string group) {
        HashSet<string> set;
        if (!map.TryGetValue(group, out set)) {
            set = new HashSet<string>();
            map[group] = set;
        }
        return set;
    }

    // Baked meshes by what plays with them: the Courier and the Sorting Yard props in Chapter 1, the gate in 2, the Foreman in
    // 3, the Server in 4, the Reading Room in 5, the Order-Bearer in 6; the lodgers and the office are the hub's (core).
    static string BakedGroup(string id) {
        if (id.StartsWith("courier") || id.StartsWith("yd_")) return "ch1";
        if (id.StartsWith("gt_")) return "ch2";
        if (id.StartsWith("foreman")) return "ch3";
        if (id.StartsWith("server")) return "ch4";
        if (id.StartsWith("rd_")) return "ch5";
        if (id.StartsWith("order_bearer")) return "ch6";
        return "core";
    }

    // A script line is "X|text" (a speaker code), "?|cond|X|text", or plain text for the narrator; the voice file is named by
    // CastVoice.FileOf, so each reading is added and only the ones with a file matter.
    static void Candidates(HashSet<string> set, string raw) {
        if (string.IsNullOrEmpty(raw)) {
            return;
        }
        foreach (string line in raw.Split('\n')) {
            string[] parts = line.Split('|');
            for (int j = 0; j < parts.Length - 1; j++) {
                if (parts[j].Length == 1 && char.IsUpper(parts[j][0])) {
                    string text = string.Join("|", parts, j + 1, parts.Length - j - 1);
                    set.Add(CastVoice.FileOf(parts[j], text));
                }
            }
            set.Add(CastVoice.FileOf("N", line));
        }
    }

    // Every string a Story.Chapter holds, wherever it sits (lines, scenes, objective text, crossroads, the walk's stops...).
    static void Walk(object o, HashSet<string> set, int depth) {
        if (o == null || depth > 5) {
            return;
        }
        string s = o as string;
        if (s != null) {
            Candidates(set, s);
            return;
        }
        Type t = o.GetType();
        if (t.IsPrimitive || t.IsEnum) {
            return;
        }
        var array = o as System.Collections.IEnumerable;
        if (array != null) {
            foreach (object element in array) {
                Walk(element, set, depth + 1);
            }
            return;
        }
        if (t.Namespace != null) {
            return;   // an engine or system type
        }
        foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance)) {
            Walk(f.GetValue(o), set, depth + 1);
        }
    }

    // ---------------------------------------------------------------- after the build

    public static void Install(string buildPath) {
        string target = Path.Combine(buildPath, "StreamingAssets", "OnDemand");
        if (Directory.Exists(target)) {
            Directory.Delete(target, true);
        }
        Directory.CreateDirectory(target);
        if (!Directory.Exists(TempDir)) {
            return;
        }
        foreach (string file in Directory.GetFiles(TempDir)) {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        }
    }

    public static void Restore() {
        staged.Clear();
        if (Directory.Exists(StageDir.TrimEnd('/'))) {
            RestoreFolder();
        }
    }

    // Moves every staged file (and its .meta) back to its place under Resources.
    static void RestoreFolder() {
        string stage = Path.Combine(Root, StageDir.TrimEnd('/'));
        foreach (string path in Directory.GetFiles(stage, "*", SearchOption.AllDirectories)) {
            string rel = path.Substring(stage.Length).TrimStart('/', '\\').Replace('\\', '/');
            string to = Path.Combine(Root, "Assets", "Resources", rel);
            if (File.Exists(to)) {
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            File.Move(path, to);
        }
        Directory.Delete(stage, true);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }
}
