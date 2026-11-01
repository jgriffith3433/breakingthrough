// The Hawkes Gen API from the Unity editor (UnityWebRequest, no dependencies).
// Callbacks run on the main thread from EditorApplication.update, so they can
// touch the AssetDatabase.
//
//   HawkesGenClient.Submit("image.generate", "sickofit",
//       new Dictionary<string, object> { ["prompt"] = "a hell imp" }, null, "enemy-concept",
//       job => HawkesGenClient.WaitAndImport(job.Id, "Assets/Art/Generated", paths => Debug.Log(paths[0])));

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace HawkesGen
{
    public sealed class GenJob
    {
        public string Id, Tool, Project, Status, Detail, Error;
        public List<GenOutput> Outputs = new List<GenOutput>();
        public object Result;
        public double Cost;
        public bool Finished => Status == "done" || Status == "failed" || Status == "cancelled";

        public static GenJob From(Dictionary<string, object> d)
        {
            var j = new GenJob
            {
                Id = d.Str("id"), Tool = d.Str("tool"), Project = d.Str("project"), Status = d.Str("status"),
                Detail = d.Str("detail"), Error = d.Str("error"), Result = d.TryGetValue("result", out var r) ? r : null,
                Cost = d.TryGetValue("cost", out var c) && c != null ? Convert.ToDouble(c) : 0,
            };
            if (d.TryGetValue("outputs", out var o) && o is List<object> list)
                foreach (var item in list)
                    if (item is Dictionary<string, object> od)
                        j.Outputs.Add(new GenOutput { Asset = od.Str("asset"), Kind = od.Str("kind"), Mime = od.Str("mime"), Title = od.Str("title"), Seed = od.TryGetValue("seed", out var s) && s != null ? Convert.ToInt64(s) : 0 });
            return j;
        }
    }

    public sealed class GenOutput
    {
        public string Asset, Kind, Mime, Title;
        public long Seed;
    }

    static class DictExt
    {
        public static string Str(this Dictionary<string, object> d, string k) => d != null && d.TryGetValue(k, out var v) && v != null ? v.ToString() : null;
    }

    public static class HawkesGenClient
    {
        /// <summary>The package version (package.json); sent to Hawkes Gen so Studio can flag projects on an old one.</summary>
        public const string Version = "0.2.0";

        public static string Url
        {
            get => EditorPrefs.GetString("HawkesGen.Url", "http://127.0.0.1:8790");
            set => EditorPrefs.SetString("HawkesGen.Url", value.TrimEnd('/'));
        }

        /// <summary>An API key (hg_...). Not needed on this PC while Hawkes Gen's localOpen setting is on. Kept in EditorPrefs, never in the project.</summary>
        public static string Key
        {
            get => EditorPrefs.GetString("HawkesGen.Key", "");
            set => EditorPrefs.SetString("HawkesGen.Key", value ?? "");
        }

        static void Send(UnityWebRequest req, Action<UnityWebRequest> done)
        {
            if (!string.IsNullOrEmpty(Key)) req.SetRequestHeader("Authorization", "Bearer " + Key);
            req.SetRequestHeader("X-Hawkes-Client", "unity/" + Version);
            var op = req.SendWebRequest();
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                if (!op.isDone) return;
                EditorApplication.update -= tick;
                try { done(req); }
                finally { req.Dispose(); }
            };
            EditorApplication.update += tick;
        }

        static string ErrorOf(UnityWebRequest r)
        {
            if (r.result == UnityWebRequest.Result.Success) return null;
            if (r.result == UnityWebRequest.Result.ConnectionError) return $"Can't reach Hawkes Gen at {Url}. Start it: C:\\Projects\\hawkesgen\\auto-start.bat";
            var body = r.downloadHandler?.text;
            if (!string.IsNullOrEmpty(body) && MiniJson.Parse(body) is Dictionary<string, object> d && d.TryGetValue("error", out var e)) return e.ToString();
            return r.error;
        }

        public static void GetJson(string path, Action<object, string> done)
        {
            var req = UnityWebRequest.Get(Url + "/v1" + path);
            Send(req, r => done(ErrorOf(r) == null ? MiniJson.Parse(r.downloadHandler.text) : null, ErrorOf(r)));
        }

        public static void PostJson(string path, object body, Action<object, string> done)
        {
            var req = new UnityWebRequest(Url + "/v1" + path, "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(MiniJson.Write(body ?? new Dictionary<string, object>()))),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            req.SetRequestHeader("Content-Type", "application/json");
            Send(req, r => done(ErrorOf(r) == null ? MiniJson.Parse(r.downloadHandler.text) : null, ErrorOf(r)));
        }

        /// <summary>Queues a job. inputs: name -> file path (absolute or Assets/...), asset:&lt;id&gt; or a list of them. preset: the project's art direction.</summary>
        public static void Submit(string tool, string project, Dictionary<string, object> parameters, Dictionary<string, object> inputs, string preset, Action<GenJob, string> done, Dictionary<string, object> labels = null)
        {
            var ins = new Dictionary<string, object>();
            if (inputs != null)
                foreach (var kv in inputs)
                    ins[kv.Key] = kv.Value is List<object> l ? (object)l.ConvertAll(AbsolutePath) : AbsolutePath(kv.Value);
            var body = new Dictionary<string, object> { ["tool"] = tool, ["project"] = project, ["params"] = parameters ?? new Dictionary<string, object>(), ["inputs"] = ins };
            if (!string.IsNullOrEmpty(preset)) body["preset"] = preset;
            if (labels != null) body["labels"] = labels;
            PostJson("/jobs", body, (res, err) =>
            {
                if (err != null) { done(null, err); return; }
                var jobs = (res as Dictionary<string, object>)?["jobs"] as List<object>;
                done(jobs != null && jobs.Count > 0 ? GenJob.From((Dictionary<string, object>)jobs[0]) : null, jobs == null ? "No job came back" : null);
            });
        }

        static object AbsolutePath(object v)
        {
            var s = v as string;
            if (s == null || s.StartsWith("asset:") || s.StartsWith("blob:")) return v;
            return Path.GetFullPath(s).Replace('\\', '/');
        }

        public static void GetJob(string id, Action<GenJob, string> done) =>
            GetJson("/jobs/" + id, (res, err) => done(err == null ? GenJob.From((Dictionary<string, object>)res) : null, err));

        /// <summary>Polls a job until it ends. onUpdate gets each change.</summary>
        public static void Wait(string id, Action<GenJob> onUpdate, Action<GenJob, string> done)
        {
            var last = "";
            double next = 0;
            var busy = false;
            EditorApplication.CallbackFunction tick = null;
            tick = () =>
            {
                if (busy || EditorApplication.timeSinceStartup < next) return;
                busy = true;
                GetJob(id, (job, err) =>
                {
                    busy = false;
                    next = EditorApplication.timeSinceStartup + 1.5;
                    if (err != null) { EditorApplication.update -= tick; done(null, err); return; }
                    var sig = job.Status + job.Detail + job.Outputs.Count;
                    if (sig != last) { last = sig; onUpdate?.Invoke(job); }
                    if (!job.Finished) return;
                    EditorApplication.update -= tick;
                    done(job, job.Status == "done" ? null : (job.Error ?? job.Status));
                });
            };
            EditorApplication.update += tick;
        }

        /// <summary>Saves a library asset into the project (e.g. Assets/HawkesGen/x.png) and imports it.</summary>
        public static void Download(string assetId, string assetPath, Action<string, string> done)
        {
            var req = UnityWebRequest.Get(Url + "/v1/files/" + assetId);
            Send(req, r =>
            {
                var err = ErrorOf(r);
                if (err != null) { done(null, err); return; }
                Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
                File.WriteAllBytes(assetPath, r.downloadHandler.data);
                if (assetPath.Replace('\\', '/').StartsWith("Assets/")) AssetDatabase.ImportAsset(assetPath);
                done(assetPath, null);
            });
        }

        /// <summary>Waits for a job, then imports every output into folder (under Assets/).</summary>
        public static void WaitAndImport(string id, string folder, Action<List<string>> done, Action<GenJob> onUpdate = null, Action<string> onError = null)
        {
            Wait(id, onUpdate, (job, err) =>
            {
                if (err != null) { onError?.Invoke(err); Debug.LogError("[Hawkes Gen] " + err); return; }
                var paths = new List<string>();
                var left = job.Outputs.Count;
                if (left == 0) { done(paths); return; }
                foreach (var o in job.Outputs)
                {
                    var ext = ExtFor(o.Mime);
                    var name = $"{job.Tool.Replace('.', '-')}-{(o.Seed != 0 ? o.Seed.ToString() : o.Asset)}{(string.IsNullOrEmpty(o.Title) ? "" : "-" + Safe(o.Title))}{ext}";
                    Download(o.Asset, Path.Combine(folder, name).Replace('\\', '/'), (p, e) =>
                    {
                        if (p != null) paths.Add(p);
                        if (e != null) Debug.LogError("[Hawkes Gen] " + e);
                        if (--left == 0) done(paths);
                    });
                }
            });
        }

        // ------------------------------------------------------------ the library (hawkesgen/docs/library.md)
        // Entities ("sickofit/imp"), their slots ("side", "pose/attack", "clip/walk") and every version of each slot:
        // takes, the current one, old and discarded ones. Jobs made for a slot file their outputs there as takes.

        static void SendJson(string method, string path, object body, Action<object, string> done)
        {
            var req = new UnityWebRequest(Url + "/v1" + path, method)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(MiniJson.Write(body ?? new Dictionary<string, object>()))),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            req.SetRequestHeader("Content-Type", "application/json");
            Send(req, r => done(ErrorOf(r) == null ? MiniJson.Parse(r.downloadHandler.text) : null, ErrorOf(r)));
        }

        static string SlotPath(string entityId, string slot)
        {
            var parts = slot.Split('/');
            for (var i = 0; i < parts.Length; i++) parts[i] = UnityWebRequest.EscapeURL(parts[i]);
            return "/entities/" + entityId + "/slots/" + string.Join("/", parts);
        }

        /// <summary>A file's bytes (e.g. a version's image for a thumbnail): path is under /v1, like "/files/a_...".</summary>
        public static void GetBytes(string path, Action<byte[], string> done)
        {
            var req = UnityWebRequest.Get(Url + "/v1" + path);
            Send(req, r => done(ErrorOf(r) == null ? r.downloadHandler.data : null, ErrorOf(r)));
        }

        /// <summary>Uploads a file (absolute or Assets/... path) to Hawkes Gen; done gets "blob:&lt;sha256&gt;" to use as an input or a version.</summary>
        public static void Upload(string path, Action<string, string> done)
        {
            var full = Path.GetFullPath(path);
            var req = new UnityWebRequest(Url + "/v1/blobs", "POST") { uploadHandler = new UploadHandlerRaw(File.ReadAllBytes(full)), downloadHandler = new DownloadHandlerBuffer() };
            req.SetRequestHeader("X-Filename", Path.GetFileName(full));
            Send(req, r =>
            {
                var err = ErrorOf(r);
                done(err == null ? (MiniJson.Parse(r.downloadHandler.text) as Dictionary<string, object>).Str("blob") : null, err);
            });
        }

        /// <summary>Entities in a project (kind optional): { items: [{ id, name, kind, slots: { side: { current, takes } } }], total }.</summary>
        public static void Entities(string project, string kind, Action<object, string> done) =>
            GetJson("/entities?project=" + UnityWebRequest.EscapeURL(project ?? "") + (string.IsNullOrEmpty(kind) ? "" : "&kind=" + UnityWebRequest.EscapeURL(kind)), done);

        public static void Entity(string entityId, Action<object, string> done) => GetJson("/entities/" + entityId, done);

        /// <summary>Creates (or with upsert, updates) an entity: project, name, kind, optional data (canon, look, slots).</summary>
        public static void CreateEntity(string project, string name, string kind, Dictionary<string, object> data, bool upsert, Action<object, string> done) =>
            PostJson("/entities", new Dictionary<string, object> { ["project"] = project, ["name"] = name, ["kind"] = kind, ["data"] = data ?? new Dictionary<string, object>(), ["upsert"] = upsert }, done);

        public static void UpdateEntity(string entityId, Dictionary<string, object> patch, Action<object, string> done) => SendJson("PATCH", "/entities/" + entityId, patch, done);

        /// <summary>A slot's versions, newest first: { versions: [{ id, n, status, note, asset, file: { kind, url, prompt } }] }.</summary>
        public static void Slot(string entityId, string slot, Action<object, string> done) => GetJson(SlotPath(entityId, slot), done);

        /// <summary>Files a file into a slot: a path (uploaded), asset:&lt;id&gt; or blob:&lt;sha256&gt;. status "current" makes it the one in use.</summary>
        public static void AddVersion(string entityId, string slot, string file, string note, string status, Action<object, string> done)
        {
            void post(string reference)
            {
                var body = new Dictionary<string, object> { ["file"] = reference };
                if (!string.IsNullOrEmpty(note)) body["note"] = note;
                if (!string.IsNullOrEmpty(status)) body["status"] = status;
                PostJson(SlotPath(entityId, slot) + "/versions", body, done);
            }
            if (file.StartsWith("asset:") || file.StartsWith("blob:")) post(file);
            else Upload(file, (blob, err) => { if (err != null) done(null, err); else post(blob); });
        }

        /// <summary>Makes a version current (the old current becomes old), or discards it, restores it ("take") or notes on it.</summary>
        public static void SetVersion(string versionId, string status, string note, Action<object, string> done)
        {
            var body = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(status)) body["status"] = status;
            if (note != null) body["note"] = note;
            SendJson("PATCH", "/versions/" + versionId, body, done);
        }

        /// <summary>New takes for a slot from its own recipe (or make: { tool | preset | steps, params, inputs }); feedback is added. Wait() on the job.</summary>
        public static void Make(string entityId, string slot, Dictionary<string, object> make, string feedback, int count, Action<GenJob, string> done)
        {
            var body = new Dictionary<string, object>();
            if (make != null) body["make"] = make;
            if (!string.IsNullOrEmpty(feedback)) body["feedback"] = feedback;
            if (count > 0) body["count"] = count;
            PostJson(SlotPath(entityId, slot) + "/make", body, (res, err) => done(err == null ? GenJob.From((Dictionary<string, object>)res) : null, err));
        }

        /// <summary>Edits the slot's current version with the feedback ("make the horns longer"); the results come back as takes.</summary>
        public static void Revise(string entityId, string slot, string feedback, int count, Action<GenJob, string> done)
        {
            var body = new Dictionary<string, object> { ["feedback"] = feedback };
            if (count > 0) body["count"] = count;
            PostJson(SlotPath(entityId, slot) + "/revise", body, (res, err) => done(err == null ? GenJob.From((Dictionary<string, object>)res) : null, err));
        }

        static string Safe(string s)
        {
            var sb = new StringBuilder();
            foreach (var c in s) sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            return sb.ToString();
        }

        public static string ExtFor(string mime)
        {
            switch (mime)
            {
                case "image/png": return ".png";
                case "image/jpeg": return ".jpg";
                case "image/webp": return ".webp";
                case "video/mp4": return ".mp4";
                case "video/webm": return ".webm";
                case "audio/mpeg": return ".mp3";
                case "audio/wav": return ".wav";
                case "audio/ogg": return ".ogg";
                case "audio/flac": return ".flac";
                case "model/gltf-binary": return ".glb";
                case "application/json": return ".json";
                default: return ".bin";
            }
        }
    }
}
