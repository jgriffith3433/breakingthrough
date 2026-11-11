using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// The campaign's heavy files, fetched when the player chooses the campaign instead of shipped in the first download.
//
// The WebGL build (WebGlBuild.BuildWebGLTo -> OnDemandPack) takes the voice lines of every cast member but Wallit and Slice, the
// cinematic stills, the portraits, the painted skies and the baked boss and lodger meshes out of Resources and lays them in
// <build>/StreamingAssets/OnDemand/ as plain mp3, jpg, png and text files named by content hash, with an index.json listing
// each file's key (its old Resources path, "Voice/cinema/ch1_open_s1"), its size and the groups it belongs to. The menu and
// Arcade need none of it, so the page's data file is that much smaller and the menu is up before the bulk arrives.
//
// A group is "core" (portraits, skies, the hub's lodgers and office pieces: everything any chapter uses) or "ch1".."ch6" (that
// chapter's stills, its reels' voices, its bosses, the lines of its script; chapter 6 also carries the ending and credits).
// Need(...) fetches a set of groups with a visible progress line (OnDemandLine) and calls back once they are in; it never polls:
// the work is UnityWebRequest coroutines the player's choice starts (a case file, Begin). A file nothing named ahead of time (a
// hub lodger's line, a boss's bark) is fetched by Fetch(key, ...) the first time it is wanted.
//
// Everything reads through Audio/Texture/Text, which look at what was fetched and then at Resources, so the editor, a build
// that kept the files in Resources and the stand-alone playtests all work with no index at all (Need then calls back at once).
public class OnDemand : MonoBehaviour {
    [Serializable]
    public class Entry {
        public string k;   // the key: the file's old path under Resources, no extension ("Voice/cinema/ch1_open_s1")
        public string f;   // the file under StreamingAssets/OnDemand/ (named by content hash, so a browser keeps it for good)
        public int b;      // bytes
        public string g;   // the groups it is in, separated by spaces ("core", "ch1 ch2"), empty when only fetched by key
    }

    [Serializable]
    public class Index {
        public Entry[] files = new Entry[0];
    }

    public const string Core = "core";
    const int Workers = 6;
    const float RequestSeconds = 25f;
    const float DecodeSeconds = 3f;

    public static string ChapterGroup(int chapter) {
        return "ch" + (chapter + 1);
    }

    // What a chapter needs on top of the campaign's core.
    public static string[] ForChapter(int chapter) {
        return new[] { Core, ChapterGroup(chapter) };
    }

    static OnDemand instance;
    static Index index;
    static bool indexTried;
    static readonly Dictionary<string, Entry> byKey = new Dictionary<string, Entry>();
    static readonly Dictionary<string, List<Entry>> groups = new Dictionary<string, List<Entry>>();
    static readonly Dictionary<string, UnityEngine.Object> cache = new Dictionary<string, UnityEngine.Object>();
    static readonly Dictionary<string, string> texts = new Dictionary<string, string>();
    static readonly HashSet<string> failed = new HashSet<string>();
    static readonly HashSet<string> inflight = new HashSet<string>();
    static readonly Dictionary<string, float> partial = new Dictionary<string, float>();

    // What the playtest reads: how much came over the network on the player's choice, and how it went.
    public static int Fetched { get; private set; }
    public static long FetchedBytes { get; private set; }
    public static int Failed { get { return failed.Count; } }
    public static int Needs { get; private set; }
    public static float LastNeedSeconds { get; private set; }
    public static float LongestFrame { get; private set; }   // the longest frame while a fetch ran (a stall over 1 s is a bug)
    public static string LastLabel { get; private set; } = "";
    public static readonly List<string> Events = new List<string>();

    static int busy;
    static long wantBytes;
    static long doneBytes;

    // True while a Need is running (the progress line is up).
    public static bool Busy { get { return busy > 0; } }

    // 0..1 of the current Need's bytes.
    public static float Progress {
        get {
            if (wantBytes <= 0) {
                return 1f;
            }
            float live = 0f;
            foreach (float v in partial.Values) {
                live += v;
            }
            return Mathf.Clamp01((doneBytes + live) / wantBytes);
        }
    }

    // True when this build moved files out of Resources (the index is read at the first Need).
    public static bool Shipped { get { return index != null && index.files.Length > 0; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
        instance = null;
        index = null;
        indexTried = false;
        byKey.Clear();
        groups.Clear();
        cache.Clear();
        texts.Clear();
        failed.Clear();
        inflight.Clear();
        partial.Clear();
        Fetched = 0;
        FetchedBytes = 0;
        Needs = 0;
        LastNeedSeconds = 0f;
        LongestFrame = 0f;
        LastLabel = "";
        Events.Clear();
        busy = 0;
        wantBytes = 0;
        doneBytes = 0;
    }

    static OnDemand Ensure() {
        if (instance == null) {
            var go = new GameObject("On Demand");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<OnDemand>();
        }
        return instance;
    }

    void Update() {
        if (busy > 0 || inflight.Count > 0) {
            float dt = Time.unscaledDeltaTime;
            if (dt > LongestFrame) {
                LongestFrame = dt;
            }
        }
    }

    // ---------------------------------------------------------------- reading

    // The clip for a key: what was fetched, else Resources (null when neither has it).
    public static AudioClip Audio(string key) {
        UnityEngine.Object o;
        if (cache.TryGetValue(key, out o)) {
            return o as AudioClip;
        }
        return Resources.Load<AudioClip>(key);
    }

    public static Texture2D Texture(string key) {
        UnityEngine.Object o;
        if (cache.TryGetValue(key, out o)) {
            return o as Texture2D;
        }
        return Resources.Load<Texture2D>(key);
    }

    // The text of a key (a baked mesh), else null.
    public static string Text(string key) {
        string s;
        if (texts.TryGetValue(key, out s)) {
            return s;
        }
        TextAsset ta = Resources.Load<TextAsset>(key);
        return ta != null ? ta.text : null;
    }

    // Lets go of a picture a screen is done with. A fetched one stays in the cache (the chapter's later reels and the next
    // load screen reuse it; Release drops it when another chapter is chosen); a Resources one is unloaded as before.
    public static void Unload(UnityEngine.Object asset) {
        if (asset == null) {
            return;
        }
        UnityEngine.Object held;
        if (!string.IsNullOrEmpty(asset.name) && cache.TryGetValue(asset.name, out held) && held == asset) {
            return;
        }
        Resources.UnloadAsset(asset);
    }

    // True when the key is in the shipped index (fetched or still to fetch).
    public static bool Indexed(string key) {
        return !string.IsNullOrEmpty(key) && byKey.ContainsKey(key) && !failed.Contains(key);
    }

    public static bool Cached(string key) {
        return cache.ContainsKey(key) || texts.ContainsKey(key);
    }

    // True when the game can get a file for the key: fetched, shipped, or in Resources. The probes use this.
    public static bool HasAudio(string key) {
        return Cached(key) || Indexed(key) || Resources.Load<AudioClip>(key) != null;
    }

    public static bool HasTexture(string key) {
        return Cached(key) || Indexed(key) || Resources.Load<Texture2D>(key) != null;
    }

    // True when every file of the group is in (or the group is unknown, or failed: the game has fallbacks).
    public static bool GroupReady(string group) {
        List<Entry> list;
        if (!groups.TryGetValue(group, out list)) {
            return true;
        }
        foreach (Entry e in list) {
            if (!Cached(e.k) && !failed.Contains(e.k)) {
                return false;
            }
        }
        return true;
    }

    public static int GroupCount(string group) {
        List<Entry> list;
        return groups.TryGetValue(group, out list) ? list.Count : 0;
    }

    // ---------------------------------------------------------------- fetching

    // Loads the groups and calls done once every file is in (or has failed: the game has a fallback for each: a subtitle, a
    // stand-in disc, the code-built mesh). With no index (the editor, a build that kept its files) it calls done at once.
    public static void Need(string[] ids, string label, Action done) {
        Needs++;
        Ensure().StartCoroutine(Ensure().Run(ids, label, done));
    }

    // A file by key, for what no group named. done gets the clip, texture or null.
    public static void FetchKey(string key, Action<UnityEngine.Object> done) {
        Ensure().StartCoroutine(Ensure().FetchOne(key, done));
    }

    IEnumerator FetchOne(string key, Action<UnityEngine.Object> done) {
        if (!indexTried) {
            yield return LoadIndex();
        }
        Entry e;
        if (!byKey.TryGetValue(key, out e) || failed.Contains(key)) {
            if (done != null) {
                done(null);
            }
            yield break;
        }
        if (!Cached(key)) {
            yield return Get(e);
        }
        UnityEngine.Object o;
        cache.TryGetValue(key, out o);
        if (done != null) {
            done(o);
        }
    }

    IEnumerator Run(string[] ids, string label, Action done) {
        float began = Time.realtimeSinceStartup;
        if (!indexTried) {
            yield return LoadIndex();
        }
        var todo = new Queue<Entry>();
        long total = 0;
        if (Shipped) {
            var seen = new HashSet<string>();
            foreach (string id in ids) {
                List<Entry> list;
                if (!groups.TryGetValue(id, out list)) {
                    continue;
                }
                foreach (Entry e in list) {
                    if (seen.Add(e.k) && !Cached(e.k) && !failed.Contains(e.k)) {
                        todo.Enqueue(e);
                        total += e.b;
                    }
                }
            }
            Release(ids);
        }
        if (todo.Count == 0) {
            if (done != null) {
                done();
            }
            yield break;
        }
        LastLabel = label;
        if (busy == 0) {
            wantBytes = 0;
            doneBytes = 0;
        }
        busy++;
        wantBytes += total;
        OnDemandLine.Show(label);
        Events.Add("need|" + string.Join("+", ids) + "|" + todo.Count + "|" + total);
        int running = 0;
        for (int w = 0; w < Workers; w++) {
            running++;
            StartCoroutine(Worker(todo, () => running--));
        }
        while (running > 0) {
            yield return null;
        }
        busy--;
        if (busy == 0) {
            OnDemandLine.Hide();
        }
        LastNeedSeconds = Time.realtimeSinceStartup - began;
        Events.Add("ready|" + string.Join("+", ids) + "|" + LastNeedSeconds.ToString("0.00"));
        if (done != null) {
            done();
        }
    }

    IEnumerator Worker(Queue<Entry> todo, Action finished) {
        while (todo.Count > 0) {
            Entry e = todo.Dequeue();
            if (Cached(e.k)) {
                continue;
            }
            yield return Get(e);
        }
        finished();
    }

    // One file, once: a second asker waits for the first.
    IEnumerator Get(Entry e) {
        if (inflight.Contains(e.k)) {
            while (inflight.Contains(e.k)) {
                yield return null;
            }
            yield break;
        }
        inflight.Add(e.k);
        string url = Url(e.f);
        string lower = e.f.ToLowerInvariant();
        bool audio = lower.EndsWith(".mp3");
        bool picture = lower.EndsWith(".jpg") || lower.EndsWith(".png");
        for (int attempt = 0; attempt < 2; attempt++) {
            UnityWebRequest req = audio ? UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG)
                : picture ? UnityWebRequestTexture.GetTexture(url, true)
                : UnityWebRequest.Get(url);
            req.timeout = (int)RequestSeconds;
            if (audio) {
                var handler = req.downloadHandler as DownloadHandlerAudioClip;
                if (handler != null) {
                    handler.streamAudio = false;
                    handler.compressed = true;
                }
            }
            AsyncOperation op = req.SendWebRequest();
            while (!op.isDone) {
                partial[e.k] = req.downloadProgress * e.b;
                yield return null;
            }
            partial.Remove(e.k);
            if (req.result == UnityWebRequest.Result.Success) {
                UnityEngine.Object made = null;
                string text = null;
                try {
                    if (audio) {
                        AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
                        if (clip != null) {
                            clip.name = e.k;
                        }
                        made = clip;
                    } else if (picture) {
                        Texture2D tex = DownloadHandlerTexture.GetContent(req);
                        if (tex != null) {
                            tex.name = e.k;
                            tex.wrapMode = TextureWrapMode.Clamp;
                        }
                        made = tex;
                    } else {
                        text = req.downloadHandler.text;
                    }
                } catch (Exception ex) {
                    Debug.LogWarning("[OnDemand] " + e.k + " unreadable: " + ex.Message);
                }
                req.Dispose();
                AudioClip decoded = made as AudioClip;
                if (decoded != null) {
                    if (decoded.loadState == AudioDataLoadState.Unloaded) {
                        decoded.LoadAudioData();
                    }
                    float until = Time.realtimeSinceStartup + DecodeSeconds;
                    while (decoded.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartup < until) {
                        yield return null;
                    }
                }
                if (made != null || text != null) {
                    if (made != null) {
                        cache[e.k] = made;
                    } else {
                        texts[e.k] = text;
                    }
                    doneBytes += e.b;
                    Fetched++;
                    FetchedBytes += e.b;
                    inflight.Remove(e.k);
                    yield break;
                }
            } else {
                Debug.Log("[OnDemand] " + e.k + " not fetched: " + req.error);
                req.Dispose();
            }
        }
        doneBytes += e.b;
        failed.Add(e.k);
        inflight.Remove(e.k);
        Events.Add("failed|" + e.k);
    }

    static string Url(string file) {
        string path = Application.streamingAssetsPath + "/OnDemand/" + file;
        return path.Contains("://") ? path : new Uri(path).AbsoluteUri;
    }

    IEnumerator LoadIndex() {
        indexTried = true;
        using (UnityWebRequest req = UnityWebRequest.Get(Url("index.json"))) {
            req.timeout = (int)RequestSeconds;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) {
                index = null;   // no index: the files are in Resources
                yield break;
            }
            Index read = null;
            try {
                read = JsonUtility.FromJson<Index>(req.downloadHandler.text);
            } catch (Exception ex) {
                Debug.LogWarning("[OnDemand] index.json unreadable: " + ex.Message);
            }
            if (read == null || read.files == null) {
                yield break;
            }
            index = read;
        }
        foreach (Entry e in index.files) {
            byKey[e.k] = e;
            if (string.IsNullOrEmpty(e.g)) {
                continue;
            }
            foreach (string g in e.g.Split(' ')) {
                if (g.Length == 0) {
                    continue;
                }
                List<Entry> list;
                if (!groups.TryGetValue(g, out list)) {
                    list = new List<Entry>();
                    groups[g] = list;
                }
                list.Add(e);
            }
        }
        Events.Add("index|" + index.files.Length);
    }

    // Moving on to another chapter drops the last one's stills and voices (the phone's memory is small); core stays.
    static void Release(string[] keep) {
        var wanted = new HashSet<string>(keep);
        var drop = new List<string>();
        foreach (var pair in groups) {
            if (!pair.Key.StartsWith("ch") || wanted.Contains(pair.Key)) {
                continue;
            }
            foreach (Entry e in pair.Value) {
                if (cache.ContainsKey(e.k) && !InWanted(e, wanted)) {
                    drop.Add(e.k);
                }
            }
        }
        foreach (string k in drop) {
            UnityEngine.Object o;
            if (cache.TryGetValue(k, out o)) {
                cache.Remove(k);
                if (o != null) {
                    Destroy(o);
                }
            }
        }
        if (drop.Count > 0) {
            Events.Add("released|" + drop.Count);
        }
    }

    static bool InWanted(Entry e, HashSet<string> wanted) {
        if (string.IsNullOrEmpty(e.g)) {
            return false;
        }
        foreach (string g in e.g.Split(' ')) {
            if (wanted.Contains(g)) {
                return true;
            }
        }
        return false;
    }
}
