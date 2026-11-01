// Window > Hawkes Gen: run any Hawkes Gen tool or project preset from the
// editor and import the results into Assets, work the project's entities in
// the library (each slot's takes: use one, discard, revise with feedback, make
// more; hawkesgen/docs/library.md), or bring in anything from the shared
// library (every project's generated art, video, 3D, music, voice).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HawkesGen
{
    public sealed class HawkesGenWindow : EditorWindow
    {
        [MenuItem("Window/Hawkes Gen")]
        public static void Open() => GetWindow<HawkesGenWindow>("Hawkes Gen").Show();

        enum Tab { Generate, Entities, Library, Settings }

        Tab tab;
        string status = "connecting…";
        bool online;
        List<Dictionary<string, object>> tools = new List<Dictionary<string, object>>();
        Dictionary<string, object> presets;
        int toolIndex, presetIndex;
        readonly Dictionary<string, object> values = new Dictionary<string, object>();
        readonly Dictionary<string, UnityEngine.Object> inputObjects = new Dictionary<string, UnityEngine.Object>();
        readonly Dictionary<string, string> inputRefs = new Dictionary<string, string>();
        bool showAdvanced;
        Vector2 scroll;
        readonly List<GenJob> recent = new List<GenJob>();
        string libSearch = "";
        string libKind = "";
        List<Dictionary<string, object>> libItems = new List<Dictionary<string, object>>();

        string Project
        {
            get => EditorPrefs.GetString("HawkesGen.Project." + Application.productName, Slug(Application.productName));
            set => EditorPrefs.SetString("HawkesGen.Project." + Application.productName, value);
        }

        string ImportFolder
        {
            get => EditorPrefs.GetString("HawkesGen.Folder." + Application.productName, "Assets/HawkesGen");
            set => EditorPrefs.SetString("HawkesGen.Folder." + Application.productName, value);
        }

        static string Slug(string s) => new string((s ?? "project").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        void OnEnable() => Refresh();

        void Refresh()
        {
            HawkesGenClient.GetJson("/health", (res, err) =>
            {
                online = err == null;
                status = err ?? "connected";
                Repaint();
                if (!online) return;
                HawkesGenClient.GetJson("/tools", (r, e) =>
                {
                    if (e == null) tools = ((List<object>)((Dictionary<string, object>)r)["tools"]).Cast<Dictionary<string, object>>().OrderBy(t => t.Str("id")).ToList();
                    Repaint();
                });
                LoadPresets();
            });
        }

        void LoadPresets()
        {
            HawkesGenClient.GetJson("/presets/" + Project, (r, e) =>
            {
                presets = e == null ? r as Dictionary<string, object> : null;
                Repaint();
            });
        }

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                tab = (Tab)GUILayout.Toolbar((int)tab, new[] { "Generate", "Entities", "Library", "Settings" }, EditorStyles.toolbarButton, GUILayout.Width(320));
                GUILayout.FlexibleSpace();
                GUILayout.Label(online ? "● " + status : "○ " + status, EditorStyles.miniLabel);
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton)) Refresh();
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (!online) EditorGUILayout.HelpBox($"Hawkes Gen isn't reachable at {HawkesGenClient.Url}. Start it with C:\\Projects\\hawkesgen\\auto-start.bat, then Refresh.", MessageType.Warning);
            else if (tab == Tab.Generate) DrawGenerate();
            else if (tab == Tab.Entities) DrawEntities();
            else if (tab == Tab.Library) DrawLibrary();
            if (tab == Tab.Settings) DrawSettings();
            EditorGUILayout.EndScrollView();
        }

        // ------------------------------------------------------------ generate

        Dictionary<string, object> CurrentTool => tools.Count > 0 ? tools[Mathf.Clamp(toolIndex, 0, tools.Count - 1)] : null;

        List<string> PresetNames()
        {
            var names = new List<string> { "(no preset)" };
            if (presets != null && presets.TryGetValue("presets", out var p) && p is Dictionary<string, object> d) names.AddRange(d.Keys);
            return names;
        }

        void DrawGenerate()
        {
            var newProject = EditorGUILayout.DelayedTextField(new GUIContent("Project", "Which project this is for (costs, library and presets are per project)."), Project);
            if (newProject != Project) { Project = newProject; LoadPresets(); }
            var names = PresetNames();
            var newPreset = EditorGUILayout.Popup(new GUIContent("Preset", "The project's art direction (presets/<project>.json in Hawkes Gen)."), Mathf.Clamp(presetIndex, 0, names.Count - 1), names.ToArray());
            if (newPreset != presetIndex)
            {
                presetIndex = newPreset;
                var tool = PresetTool(names[presetIndex]);
                if (tool != null) toolIndex = Math.Max(0, tools.FindIndex(t => t.Str("id") == tool));
            }
            if (presetIndex > 0) EditorGUILayout.HelpBox(PresetDescription(names[presetIndex]), MessageType.None);

            if (tools.Count == 0) { EditorGUILayout.LabelField("Loading tools…"); return; }
            toolIndex = EditorGUILayout.Popup("Tool", toolIndex, tools.Select(t => $"{t.Str("id")}  —  {t.Str("title")}").ToArray());
            var cur = CurrentTool;
            EditorGUILayout.HelpBox(cur.Str("description"), MessageType.Info);

            if (cur["inputs"] is Dictionary<string, object> ins && ins.Count > 0)
            {
                EditorGUILayout.LabelField("Inputs", EditorStyles.boldLabel);
                foreach (var kv in ins)
                {
                    var spec = (Dictionary<string, object>)kv.Value;
                    inputObjects.TryGetValue(kv.Key, out var o);
                    inputObjects[kv.Key] = EditorGUILayout.ObjectField(new GUIContent($"{kv.Key}{(spec.Str("required") == "True" ? " *" : "")}", spec.Str("description") ?? spec.Str("kind")), o, typeof(UnityEngine.Object), false);
                    inputRefs.TryGetValue(kv.Key, out var r);
                    inputRefs[kv.Key] = EditorGUILayout.TextField(new GUIContent("   or path / asset:id"), r ?? "");
                }
            }

            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            var ps = (Dictionary<string, object>)cur["params"];
            foreach (var kv in ps.Where(p => !IsAdvanced(p.Value))) DrawParam(cur.Str("id"), kv.Key, (Dictionary<string, object>)kv.Value);
            if (ps.Any(p => IsAdvanced(p.Value)))
            {
                showAdvanced = EditorGUILayout.Foldout(showAdvanced, "More settings");
                if (showAdvanced) foreach (var kv in ps.Where(p => IsAdvanced(p.Value))) DrawParam(cur.Str("id"), kv.Key, (Dictionary<string, object>)kv.Value);
            }

            ImportFolder = EditorGUILayout.TextField(new GUIContent("Import into", "Outputs are saved and imported here (under Assets/)."), ImportFolder);
            using (new EditorGUI.DisabledScope(!ImportFolder.StartsWith("Assets")))
            {
                if (GUILayout.Button(cur.Str("provider") == "comfy" ? "Run on the GPU fleet" : "Run", GUILayout.Height(28))) Run(cur);
            }
            if (cur.Str("provider") == "comfy") EditorGUILayout.HelpBox("GPU tools run on Hawkes Gen's AWS servers (a few cents per run on a running server). With no server running, the job waits until one is started from the Hawkes Gen dashboard.", MessageType.None);

            if (recent.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("This session", EditorStyles.boldLabel);
                foreach (var j in recent.AsEnumerable().Reverse().Take(10))
                    EditorGUILayout.LabelField($"{j.Tool}: {j.Status}", j.Error ?? j.Detail ?? "", EditorStyles.wordWrappedMiniLabel);
            }
        }

        static bool IsAdvanced(object spec) => spec is Dictionary<string, object> d && d.Str("advanced") == "True";

        string PresetTool(string name) => presets != null && presets["presets"] is Dictionary<string, object> d && d.TryGetValue(name, out var p) ? ((Dictionary<string, object>)p).Str("tool") : null;
        string PresetDescription(string name) => presets != null && presets["presets"] is Dictionary<string, object> d && d.TryGetValue(name, out var p) ? ((Dictionary<string, object>)p).Str("description") ?? name : name;

        void DrawParam(string toolId, string name, Dictionary<string, object> spec)
        {
            var key = toolId + "/" + name;
            values.TryGetValue(key, out var v);
            var def = spec.TryGetValue("default", out var dv) ? dv : null;
            var label = new GUIContent(name + (spec.Str("required") == "True" ? " *" : ""), spec.Str("description"));
            switch (spec.Str("type"))
            {
                case "text":
                case "json":
                    EditorGUILayout.LabelField(label);
                    values[key] = EditorGUILayout.TextArea(v as string ?? (def is string ds ? ds : def != null ? MiniJson.Write(def) : ""), GUILayout.MinHeight(48));
                    break;
                case "integer":
                    values[key] = EditorGUILayout.LongField(label, v is long l ? l : def != null ? Convert.ToInt64(def) : 0);
                    break;
                case "number":
                    values[key] = EditorGUILayout.DoubleField(label, v is double d ? d : def != null ? Convert.ToDouble(def, CultureInfo.InvariantCulture) : 0);
                    break;
                case "boolean":
                    values[key] = EditorGUILayout.Toggle(label, v is bool b ? b : def is bool db && db);
                    break;
                case "enum":
                    var opts = ((List<object>)spec["values"]).Select(x => x.ToString()).ToArray();
                    var idx = Array.IndexOf(opts, v as string ?? def as string);
                    values[key] = opts[EditorGUILayout.Popup(label, Math.Max(0, idx), opts)];
                    break;
                default:
                    values[key] = EditorGUILayout.TextField(label, v as string ?? def as string ?? "");
                    break;
            }
        }

        void Run(Dictionary<string, object> tool)
        {
            var id = tool.Str("id");
            var ps = (Dictionary<string, object>)tool["params"];
            var parameters = new Dictionary<string, object>();
            foreach (var kv in ps)
            {
                if (!values.TryGetValue(id + "/" + kv.Key, out var v)) continue;
                var spec = (Dictionary<string, object>)kv.Value;
                var def = spec.TryGetValue("default", out var d) ? d : null;
                if (v is string s && s == "" && spec.Str("required") != "True") continue;
                if (spec.Str("type") == "json" && v is string js) v = MiniJson.Parse(js);
                if (spec.Str("type") == "integer" && v is long lv && lv == 0 && def == null && kv.Key == "seed") continue;
                if (def != null && MiniJson.Write(def) == MiniJson.Write(v)) continue;
                parameters[kv.Key] = v;
            }
            var inputs = new Dictionary<string, object>();
            if (tool["inputs"] is Dictionary<string, object> ins)
                foreach (var name in ins.Keys)
                {
                    if (inputObjects.TryGetValue(name, out var o) && o != null) inputs[name] = AssetDatabase.GetAssetPath(o);
                    else if (inputRefs.TryGetValue(name, out var r) && !string.IsNullOrWhiteSpace(r))
                    {
                        var parts = r.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                        inputs[name] = parts.Count == 1 ? (object)parts[0] : parts.Cast<object>().ToList();
                    }
                }
            var names = PresetNames();
            var preset = presetIndex > 0 ? names[presetIndex] : null;
            HawkesGenClient.Submit(id, Project, parameters, inputs, preset, (job, err) =>
            {
                if (err != null) { EditorUtility.DisplayDialog("Hawkes Gen", err, "OK"); return; }
                recent.Add(job);
                Repaint();
                HawkesGenClient.WaitAndImport(job.Id, Path.Combine(ImportFolder, id.Replace('.', '-')).Replace('\\', '/'), paths =>
                {
                    Debug.Log($"[Hawkes Gen] {id} done: {string.Join(", ", paths)}");
                    if (paths.Count > 0) EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(paths[0]));
                    Repaint();
                }, update =>
                {
                    var i = recent.FindIndex(x => x.Id == update.Id);
                    if (i >= 0) recent[i] = update;
                    Repaint();
                }, e => Repaint());
            });
        }

        // ------------------------------------------------------------ library

        void DrawLibrary()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                libSearch = EditorGUILayout.TextField("Search", libSearch);
                libKind = EditorGUILayout.TextField(libKind, GUILayout.Width(70));
                if (GUILayout.Button("Find", GUILayout.Width(60))) SearchLibrary();
            }
            EditorGUILayout.LabelField("Kind: image, video, audio, mesh (blank for all). Every project's work is here.", EditorStyles.miniLabel);
            foreach (var a in libItems)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    var labels = a.TryGetValue("labels", out var l) && l is Dictionary<string, object> ld ? string.Join(" ", ld.Values) : "";
                    EditorGUILayout.LabelField($"{a.Str("title") ?? labels}", $"{a.Str("project")} · {a.Str("tool")} · {a.Str("kind")}");
                    if (GUILayout.Button("Import", GUILayout.Width(60)))
                    {
                        var path = Path.Combine(ImportFolder, "library", a.Str("id") + HawkesGenClient.ExtFor(a.Str("mime"))).Replace('\\', '/');
                        HawkesGenClient.Download(a.Str("id"), path, (p, e) =>
                        {
                            if (e != null) Debug.LogError("[Hawkes Gen] " + e);
                            else EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(p));
                        });
                    }
                    if (GUILayout.Button("Use as input", GUILayout.Width(90)) && CurrentTool?["inputs"] is Dictionary<string, object> ins && ins.Count > 0)
                    {
                        inputRefs[ins.Keys.First()] = "asset:" + a.Str("id");
                        tab = Tab.Generate;
                    }
                }
            }
        }

        void SearchLibrary()
        {
            var q = $"/assets?limit=60&q={Uri.EscapeDataString(libSearch ?? "")}{(string.IsNullOrWhiteSpace(libKind) ? "" : "&kind=" + Uri.EscapeDataString(libKind.Trim()))}";
            HawkesGenClient.GetJson(q, (r, e) =>
            {
                libItems = e == null ? ((List<object>)((Dictionary<string, object>)r)["items"]).Cast<Dictionary<string, object>>().ToList() : new List<Dictionary<string, object>>();
                if (e != null) Debug.LogError("[Hawkes Gen] " + e);
                Repaint();
            });
        }

        // ------------------------------------------------------------ entities (the library: docs/library.md)

        string entKind = "";
        List<Dictionary<string, object>> entities = new List<Dictionary<string, object>>();
        Dictionary<string, object> entity;
        readonly HashSet<string> openSlots = new HashSet<string>();
        readonly Dictionary<string, List<Dictionary<string, object>>> slotVersions = new Dictionary<string, List<Dictionary<string, object>>>();
        readonly Dictionary<string, string> feedback = new Dictionary<string, string>();
        readonly Dictionary<string, Texture2D> thumbs = new Dictionary<string, Texture2D>();
        readonly HashSet<string> thumbLoading = new HashSet<string>();
        readonly Dictionary<string, string> slotBusy = new Dictionary<string, string>();
        bool entitiesLoaded;

        void LoadEntities()
        {
            entitiesLoaded = true;
            HawkesGenClient.Entities(Project, entKind.Trim(), (r, e) =>
            {
                if (e != null) { Debug.LogError("[Hawkes Gen] " + e); return; }
                entities = ((List<object>)((Dictionary<string, object>)r)["items"]).Cast<Dictionary<string, object>>().ToList();
                if (entity != null) entity = entities.FirstOrDefault(x => x.Str("id") == entity.Str("id"));
                Repaint();
            });
        }

        void LoadSlot(string slot)
        {
            var id = entity.Str("id");
            HawkesGenClient.Slot(id, slot, (r, e) =>
            {
                if (e != null) { Debug.LogError("[Hawkes Gen] " + e); return; }
                slotVersions[id + " " + slot] = ((List<object>)((Dictionary<string, object>)r)["versions"]).Cast<Dictionary<string, object>>().ToList();
                Repaint();
            });
        }

        Texture2D Thumb(Dictionary<string, object> v)
        {
            var file = v.TryGetValue("file", out var f) ? f as Dictionary<string, object> : null;
            if (file == null || file.Str("kind") != "image") return null;
            var asset = v.Str("asset");
            if (thumbs.TryGetValue(asset, out var t)) return t;
            if (thumbLoading.Add(asset))
                HawkesGenClient.GetBytes("/files/" + asset, (bytes, e) =>
                {
                    if (bytes == null) return;
                    var tex = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                    if (tex.LoadImage(bytes)) thumbs[asset] = tex;
                    Repaint();
                });
            return null;
        }

        void DrawEntities()
        {
            if (!entitiesLoaded) LoadEntities();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Project", Project, GUILayout.ExpandWidth(true));
                entKind = EditorGUILayout.TextField(entKind, GUILayout.Width(90));
                if (GUILayout.Button("List", GUILayout.Width(50))) LoadEntities();
            }
            EditorGUILayout.LabelField("Kind filter (blank for all). Picks made here show in Studio's Library tab and the product's tools.", EditorStyles.miniLabel);
            if (entities.Count == 0)
            {
                EditorGUILayout.HelpBox($"No entities in {Project} yet. A product's tools bring them in (hawkes.library.json), or make one in Studio's Library tab.", MessageType.Info);
                return;
            }

            var names = entities.Select(x => $"{x.Str("name")}  ({x.Str("kind")})").ToArray();
            var index = Mathf.Max(0, entities.FindIndex(x => x.Str("id") == entity?.Str("id")));
            var picked = EditorGUILayout.Popup("Entity", index, names);
            if (entity == null || picked != index) { entity = entities[picked]; openSlots.Clear(); }

            var data = entity.TryGetValue("data", out var d) ? d as Dictionary<string, object> : null;
            if (data != null && data.Str("look") != null) EditorGUILayout.LabelField(data.Str("look"), EditorStyles.wordWrappedMiniLabel);

            var slots = entity.TryGetValue("slots", out var s) ? s as Dictionary<string, object> : new Dictionary<string, object>();
            foreach (var slot in slots.Keys.OrderBy(k => k).ToList())
            {
                var sum = slots[slot] as Dictionary<string, object>;
                Dictionary<string, object> cur = null;
                if (sum != null && sum.TryGetValue("current", out var c)) cur = c as Dictionary<string, object>;
                var takes = sum?.Str("takes") ?? "0";
                var key = entity.Str("id") + " " + slot;
                var open = openSlots.Contains(slot);
                var now = EditorGUILayout.Foldout(open, $"{slot}   {(cur != null ? "v" + cur.Str("n") : "none picked")}   {takes} takes", true);
                if (now != open)
                {
                    if (now) { openSlots.Add(slot); LoadSlot(slot); }
                    else openSlots.Remove(slot);
                }
                if (!now) continue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    if (slotVersions.TryGetValue(key, out var versions))
                        foreach (var v in versions) DrawVersion(slot, v);
                    feedback.TryGetValue(key, out var fb);
                    feedback[key] = EditorGUILayout.TextField("Feedback", fb ?? "");
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Revise current") && !string.IsNullOrWhiteSpace(feedback[key])) RunSlot(slot, true, feedback[key]);
                        if (GUILayout.Button("Make takes")) RunSlot(slot, false, feedback[key]);
                        if (slotBusy.TryGetValue(key, out var busy)) GUILayout.Label(busy, EditorStyles.miniLabel);
                    }
                }
            }
        }

        void DrawVersion(string slot, Dictionary<string, object> v)
        {
            var status = v.Str("status");
            if (status == "discarded") return;
            using (new EditorGUILayout.HorizontalScope())
            {
                var t = Thumb(v);
                GUILayout.Box(t, GUILayout.Width(64), GUILayout.Height(64));
                using (new EditorGUILayout.VerticalScope())
                {
                    var file = v.TryGetValue("file", out var f) ? f as Dictionary<string, object> : null;
                    EditorGUILayout.LabelField($"v{v.Str("n")}  {status}  {file?.Str("kind")}", EditorStyles.boldLabel);
                    if (!string.IsNullOrEmpty(v.Str("note"))) EditorGUILayout.LabelField(v.Str("note"), EditorStyles.wordWrappedMiniLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (status != "current" && GUILayout.Button("Use this", GUILayout.Width(70))) SetVersion(slot, v.Str("id"), "current");
                        if (status != "current" && GUILayout.Button("Discard", GUILayout.Width(60))) SetVersion(slot, v.Str("id"), "discarded");
                        if (GUILayout.Button("Import", GUILayout.Width(60)))
                        {
                            var name = slot.Replace("/", "_") + "-v" + v.Str("n") + HawkesGenClient.ExtFor(file?.Str("mime"));
                            var path = Path.Combine(ImportFolder, "library", entity.Str("id").Replace("/", "_"), name).Replace(Path.DirectorySeparatorChar, '/');
                            HawkesGenClient.Download(v.Str("asset"), path, (p, e) =>
                            {
                                if (e != null) Debug.LogError("[Hawkes Gen] " + e);
                                else EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(p));
                            });
                        }
                    }
                }
            }
        }

        void SetVersion(string slot, string versionId, string status)
        {
            HawkesGenClient.SetVersion(versionId, status, null, (r, e) =>
            {
                if (e != null) Debug.LogError("[Hawkes Gen] " + e);
                LoadSlot(slot);
                LoadEntities();
            });
        }

        void RunSlot(string slot, bool revise, string fb)
        {
            var key = entity.Str("id") + " " + slot;
            var entityId = entity.Str("id");
            Action<GenJob, string> started = (job, err) =>
            {
                if (err != null) { slotBusy[key] = err; Repaint(); return; }
                slotBusy[key] = job.Status;
                HawkesGenClient.Wait(job.Id, j => { slotBusy[key] = j.Detail ?? j.Status; Repaint(); }, (j, e) =>
                {
                    slotBusy.Remove(key);
                    if (e != null) Debug.LogError("[Hawkes Gen] " + e);
                    if (entity?.Str("id") == entityId) LoadSlot(slot);
                    LoadEntities();
                });
                Repaint();
            };
            if (revise) HawkesGenClient.Revise(entityId, slot, fb, 1, started);
            else HawkesGenClient.Make(entityId, slot, null, fb, 0, started);
        }

        // ------------------------------------------------------------ settings

        void DrawSettings()
        {
            HawkesGenClient.Url = EditorGUILayout.DelayedTextField("Service URL", HawkesGenClient.Url);
            HawkesGenClient.Key = EditorGUILayout.PasswordField(new GUIContent("API key", "Only needed when Hawkes Gen is on another machine or localOpen is off. Stored in EditorPrefs, never in the project."), HawkesGenClient.Key);
            EditorGUILayout.HelpBox("Dashboard: http://127.0.0.1:8790 (fleet, library, presets, costs).", MessageType.None);
            if (GUILayout.Button("Open the dashboard")) Application.OpenURL(HawkesGenClient.Url + "/");
        }
    }
}
