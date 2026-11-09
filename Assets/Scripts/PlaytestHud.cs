using System.Collections;
using System.Collections.Generic;
using System.Text;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.UI;

// The eyes of the "chapter2-hud" scenario: J's top-priority bug was a black block over the top middle of Chapter 2, No Fixed
// Address, on "Back to the circle" (the Slice bar, the chapter title and part of her nameplate drawn on top of it). In his
// screenshot the two blocks sit on the 32/64 px grid of the bloom's lowest levels and cut straight through the world-space
// nameplate while the UI draws over them: a NaN or Inf pixel in the HDR frame that Hidden/BT/Post (BTPost.shader) smeared
// through its six levels. The scenario walks out to the gate and back and photographs that corner, and this class reads
// what the browser's screenshot cannot:
//   * ScanFrame renders the main camera through the real grade into a small frame and looks for a solid, exactly black,
//     rectangular block (a bloom smear is a clean rectangle; a dark night sky or a shadow never is, and the sky here is
//     never darker than about 0.05). A block is a Bug.
//   * RawNan renders the same camera with the grade switched off into a half-float target, flags NaN, Inf and over-64
//     pixels with a tiny shader (Hidden/BT/NanProbe), reads the flags back as plain colour and counts them. BTPost now
//     hides such pixels, so the block is gone, but their source is not: when there are any, Culprits renders again with
//     each shader's renderers (and the sky) hidden in turn and names the group whose removal makes them vanish, so the
//     next agent has the real cause and not a guess.
// Everything is read from camera renders; the page's screenshot is the review's own copy of the same moment (hud-shot
// events hold still for a second and a half so a screenshot lands on each).
public static class PlaytestHud {
    public static int Shots;
    public static int Scans;
    public static int BlackFrames;
    public static int NanFrames;
    public static int MostYoBots;
    public static float TopMiddleBlackMax;     // percent of the top-middle region that was exactly black, worst scan
    public static int NanMax;
    static int diagnosed;
    static float nextTick;
    static bool hdrKnown;

    const int ScanHeight = 360;
    const int RawHeight = 540;

    struct Frame {
        public bool Ok;
        public int Width, Height;
        public bool Block;
        public RectInt BlockRect;
        public int BlockPixels;
        public float TopMiddleBlack;   // percent
        public float TopMiddleLuma;
        public float Luma;
    }

    // The desktop camera renders in HDR (NightAtmosphere, SceneMood: allowHDR = !UiFit.Touch); a phone does not, so only
    // a desktop run can show this bug. Said once per run, and a warning when the run could not have shown it.
    public static void Hdr(PlaytestRun run) {
        Camera cam = Camera.main;
        bool hdr = cam != null && cam.allowHDR;
        NightGrade grade = cam != null ? cam.GetComponent<NightGrade>() : null;
        run.Event("hdr", "screen " + Screen.width + "x" + Screen.height + ", camera HDR " + (hdr ? "on" : "OFF") + ", touch layout " + UiFit.Touch + ", grade " + (grade != null && grade.enabled ? "on (bloom " + grade.Iterations + " levels)" : "missing")
            + ", shader " + (Shader.Find("Hidden/BT/Post") != null ? "Hidden/BT/Post found" : "Hidden/BT/Post MISSING") + ", half-float targets " + SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf));
        run.Metric("hdrOn", hdr ? 1 : 0);
        hdrKnown = true;
        if (!hdr) {
            run.Problem(PlaytestSeverity.Warning, "chapter2-hud: the camera renders without HDR here (touch layout), so this run cannot show the bloom black block; run it on desktop");
        }
        if (grade == null) {
            run.Problem(PlaytestSeverity.Warning, "chapter2-hud: the main camera has no NightGrade, so there is no bloom to check");
        }
    }

    // Yo Bots (not Spider Bots) that are alive and inside the camera's view, asleep or awake.
    public static int YoBotsOnScreen() {
        Camera cam = Camera.main;
        if (cam == null) {
            return 0;
        }
        int count = 0;
        for (int i = 0; i < Enemy.Roster.Count; i++) {
            Enemy e = Enemy.Roster[i];
            if (e == null || !e.Alive || BotRoster.IsSpider(e.Kind)) {
                continue;
            }
            Vector3 v = cam.WorldToViewportPoint(e.transform.position + Vector3.up * 0.8f);
            if (v.z > 0.5f && v.x > 0.02f && v.x < 0.98f && v.y > 0.02f && v.y < 0.98f) {
                count++;
            }
        }
        MostYoBots = Mathf.Max(MostYoBots, count);
        return count;
    }

    static Rect RectOf(RectTransform rt) {
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
    }

    static string Box(Rect r) {
        return Mathf.RoundToInt(r.xMin) + "," + Mathf.RoundToInt(Screen.height - r.yMax) + " " + Mathf.RoundToInt(r.width) + "x" + Mathf.RoundToInt(r.height);
    }

    // What is at the top middle: Slice's bar and title, the chapter title and place, and her world-space nameplate.
    // Screen boxes are "x,y WxH" from the top left, in pixels.
    public static string Describe(ChapterRun chapter) {
        var sb = new StringBuilder();
        GameObject bar = GameObject.Find("Musician Health");
        if (bar != null) {
            Transform back = bar.transform.Find("Back");
            if (back != null) {
                sb.Append("Slice bar [").Append(Box(RectOf((RectTransform)back))).Append("] ");
            }
        }
        if (Musician.Instance != null) {
            sb.Append(Musician.Instance.Title).Append(' ').Append(Musician.Instance.Health).Append('/').Append(Musician.Instance.MaxHealth).Append("; ");
        }
        foreach (Text t in Object.FindObjectsByType<Text>(FindObjectsSortMode.None)) {
            if (!t.isActiveAndEnabled || t.canvas == null || t.canvas.renderMode != RenderMode.ScreenSpaceOverlay) {
                continue;
            }
            if (chapter != null && (t.text == chapter.Title || t.text == chapter.Place)) {
                sb.Append("title \"").Append(t.text).Append("\" [").Append(Box(RectOf(t.rectTransform))).Append("]; ");
            }
        }
        Camera cam = Camera.main;
        foreach (TextMesh tm in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None)) {
            if (!tm.gameObject.activeInHierarchy || cam == null || tm.text == null || !tm.text.StartsWith(Musician.Name)) {
                continue;
            }
            Vector3 sp = cam.WorldToScreenPoint(tm.transform.position);
            bool inView = sp.z > 0f && sp.x >= 0f && sp.x <= Screen.width && sp.y >= 0f && sp.y <= Screen.height;
            sb.Append("nameplate \"").Append(tm.text.Replace('\n', ' ')).Append("\" at ").Append(Mathf.RoundToInt(sp.x)).Append(',').Append(Mathf.RoundToInt(Screen.height - sp.y)).Append(inView ? " (in view)" : " (off screen)").Append("; ");
        }
        return sb.ToString();
    }

    static int FrameWidth(int height) {
        return Mathf.Clamp(Mathf.RoundToInt(height * (float)Screen.width / Mathf.Max(1, Screen.height)), 160, 1920);
    }

    // The camera through the real grade into a small LDR frame, then a look for a solid black rectangle.
    static Frame ScanFrame(Camera cam) {
        var f = new Frame();
        if (cam == null) {
            return f;
        }
        int h = ScanHeight;
        int w = FrameWidth(h);
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
        RenderTexture oldTarget = cam.targetTexture;
        RenderTexture active = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = oldTarget;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        RenderTexture.active = active;
        RenderTexture.ReleaseTemporary(rt);
        Color32[] px = tex.GetPixels32();
        Object.Destroy(tex);
        f.Ok = true;
        f.Width = w;
        f.Height = h;

        var black = new bool[px.Length];
        double luma = 0;
        for (int i = 0; i < px.Length; i++) {
            Color32 c = px[i];
            black[i] = c.r <= 2 && c.g <= 2 && c.b <= 2;
            luma += (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
        }
        f.Luma = (float)(luma / px.Length);

        // The top-middle region of J's shot: x 33 to 73 percent, the top 43 percent (texture rows count from the bottom).
        int x0 = Mathf.RoundToInt(w * 0.33f), x1 = Mathf.RoundToInt(w * 0.73f), y0 = Mathf.RoundToInt(h * 0.57f);
        int region = 0, regionBlack = 0;
        double regionLuma = 0;
        for (int y = y0; y < h; y++) {
            for (int x = x0; x < x1; x++) {
                int i = y * w + x;
                region++;
                if (black[i]) {
                    regionBlack++;
                }
                regionLuma += (0.299f * px[i].r + 0.587f * px[i].g + 0.114f * px[i].b) / 255f;
            }
        }
        f.TopMiddleBlack = region > 0 ? 100f * regionBlack / region : 0f;
        f.TopMiddleLuma = region > 0 ? (float)(regionLuma / region) : 0f;

        // Connected black pixels; a block is big (at least 0.4 percent of the frame, 3 percent across), and its bounding box
        // is at least 90 percent black, which a shadow or a dark sky is not.
        var seen = new bool[px.Length];
        var stack = new int[px.Length];
        int minArea = Mathf.Max(64, px.Length / 250);
        int minW = Mathf.Max(8, w * 3 / 100), minH = Mathf.Max(8, h * 3 / 100);
        for (int start = 0; start < px.Length; start++) {
            if (!black[start] || seen[start]) {
                continue;
            }
            int top = 0, count = 0;
            int minX = w, maxX = 0, minY = h, maxY = 0;
            stack[top++] = start;
            seen[start] = true;
            while (top > 0) {
                int p = stack[--top];
                int x = p % w, y = p / w;
                count++;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
                if (x > 0 && black[p - 1] && !seen[p - 1]) { seen[p - 1] = true; stack[top++] = p - 1; }
                if (x < w - 1 && black[p + 1] && !seen[p + 1]) { seen[p + 1] = true; stack[top++] = p + 1; }
                if (y > 0 && black[p - w] && !seen[p - w]) { seen[p - w] = true; stack[top++] = p - w; }
                if (y < h - 1 && black[p + w] && !seen[p + w]) { seen[p + w] = true; stack[top++] = p + w; }
            }
            int bw = maxX - minX + 1, bh = maxY - minY + 1;
            if (count >= minArea && bw >= minW && bh >= minH && count >= 0.9f * bw * bh && count > f.BlockPixels) {
                f.Block = true;
                f.BlockPixels = count;
                f.BlockRect = new RectInt(minX, minY, bw, bh);
            }
        }
        return f;
    }

    static Material probeMaterial;

    // The raw HDR frame, grade off: how many pixels are NaN or Inf (the return value) and how many are finite but brighter
    // than 64 (hot). The frame goes into a half-float target, then through Hidden/BT/NanProbe into a plain 8-bit flag image,
    // which is read back the way the moods scenario reads a frame: no float readback, so it works in a browser too.
    // Renderers in `off` (and the sky when `noSky`) are hidden for this one render and put back. -1 means it cannot be done here.
    static int RawNan(Camera cam, List<Renderer> off, bool noSky, int h, out int hot, out Vector2 first) {
        hot = 0;
        first = new Vector2(-1f, -1f);
        NightGrade grade = cam != null ? cam.GetComponent<NightGrade>() : null;
        if (cam == null || !cam.allowHDR || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)) {
            return -1;
        }
        if (probeMaterial == null) {
            Shader shader = Shader.Find("Hidden/BT/NanProbe");
            if (shader == null || !shader.isSupported) {
                return -1;
            }
            probeMaterial = new Material(shader);
            probeMaterial.hideFlags = HideFlags.HideAndDontSave;
        }
        int w = FrameWidth(h);
        bool gradeWas = grade != null && grade.enabled;
        CameraClearFlags flags = cam.clearFlags;
        Color background = cam.backgroundColor;
        var was = new List<bool>();
        if (off != null) {
            foreach (Renderer r in off) {
                was.Add(r != null && r.enabled);
                if (r != null) {
                    r.enabled = false;
                }
            }
        }
        if (grade != null) {
            grade.enabled = false;
        }
        if (noSky) {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
        }
        var hdr = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGBHalf);
        var flagsRt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        RenderTexture oldTarget = cam.targetTexture;
        RenderTexture active = RenderTexture.active;
        cam.targetTexture = hdr;
        cam.Render();
        cam.targetTexture = oldTarget;
        Graphics.Blit(hdr, flagsRt, probeMaterial);
        RenderTexture.active = flagsRt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        RenderTexture.active = active;
        RenderTexture.ReleaseTemporary(hdr);
        RenderTexture.ReleaseTemporary(flagsRt);
        Color32[] px = tex.GetPixels32();
        Object.Destroy(tex);
        if (grade != null) {
            grade.enabled = gradeWas;
        }
        cam.clearFlags = flags;
        cam.backgroundColor = background;
        if (off != null) {
            for (int i = 0; i < off.Count; i++) {
                if (off[i] != null) {
                    off[i].enabled = was[i];
                }
            }
        }
        int bad = 0;
        for (int i = 0; i < px.Length; i++) {
            if (px[i].r > 128) {
                if (bad == 0) {
                    first = new Vector2((i % w + 0.5f) / w, (i / w + 0.5f) / h);
                }
                bad++;
            } else if (px[i].g > 128) {
                hot++;
            }
        }
        return bad;
    }

    static string ShaderOf(Renderer r) {
        Material m = r.sharedMaterial;
        return m != null && m.shader != null ? m.shader.name : "(no material)";
    }

    // The cause of the NaN pixels in the first run was in the data, not in a shader: the Gen-baked meshes (gate arch, booth,
    // turnstiles, stamp press, bosses, lodgers) listed vertices with a normal of (0,0,0), which KitLit's tangent-space
    // transform normalises into NaN. BakedMesh now mends them as it loads. This looks at every readable mesh on a live
    // renderer and counts the vertices whose normal a shader still could not normalise (zero, NaN or Inf), so a guard in a
    // shader can never hide a bad mesh from this scenario. `first` names the first mesh found.
    static int BadNormalVertices(out int meshes, out int scanned, out string first) {
        meshes = 0;
        scanned = 0;
        first = "";
        int bad = 0;
        var seen = new HashSet<Mesh>();
        var list = new List<Mesh>();
        foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)) {
            if (mf.sharedMesh != null && mf.gameObject.activeInHierarchy) {
                list.Add(mf.sharedMesh);
            }
        }
        foreach (SkinnedMeshRenderer sr in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None)) {
            if (sr.sharedMesh != null && sr.gameObject.activeInHierarchy) {
                list.Add(sr.sharedMesh);
            }
        }
        foreach (Mesh mesh in list) {
            if (!seen.Add(mesh) || !mesh.isReadable) {
                continue;
            }
            Vector3[] n = mesh.normals;
            if (n == null || n.Length == 0) {
                continue;
            }
            scanned++;
            int here = 0;
            for (int i = 0; i < n.Length; i++) {
                if (!(n[i].sqrMagnitude > 0.25f)) {   // a zero vector fails it, and so does NaN
                    here++;
                }
            }
            if (here > 0) {
                meshes++;
                bad += here;
                if (first.Length == 0) {
                    first = mesh.name + " (" + here + " of " + n.Length + ")";
                }
            }
        }
        return bad;
    }

    static bool normalsSaid;

    // Said once, at the end of the run (the whole chapter is loaded by then): what the baked files held and what is left.
    static void Normals(PlaytestRun run) {
        if (normalsSaid) {
            return;
        }
        normalsSaid = true;
        int meshes, scanned;
        string first;
        int bad = BadNormalVertices(out meshes, out scanned, out first);
        run.Metric("badNormalVertices", bad);
        run.Metric("bakedNormalsMended", BakedMesh.MendedVertices);
        run.Event("normals", scanned + " readable meshes on live renderers: " + bad + " vertices with a zero, NaN or Inf normal left" + (bad > 0 ? " in " + meshes + " meshes (first " + first + ")" : "")
            + "; the baked Gen meshes held " + BakedMesh.MendedVertices + " such normals in " + BakedMesh.MendedMeshes + " meshes, mended as they load");
        if (bad > 0) {
            run.Problem(PlaytestSeverity.Warning, "chapter2-hud: " + bad + " vertices still have a zero, NaN or Inf normal (first " + first + "), which a lit shader turns into a NaN pixel");
        }
    }

    // Which group of renderers makes the NaN pixels: hide each shader's renderers in turn and render again. The groups
    // whose removal clears (or cuts) the count are the cause. The sky is a skybox material, not a renderer, so it is tried by
    // replacing it with a plain colour. Also says how many renderers have a near-zero scale (the zero-scale bots suspect).
    static string Culprits(Camera cam, int baseNan) {
        var groups = new Dictionary<string, List<Renderer>>();
        int tiny = 0;
        string tinyName = "";
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) {
            if (!r.enabled || !r.gameObject.activeInHierarchy) {
                continue;
            }
            string key = ShaderOf(r);
            List<Renderer> list;
            if (!groups.TryGetValue(key, out list)) {
                list = new List<Renderer>();
                groups[key] = list;
            }
            list.Add(r);
            Vector3 s = r.transform.lossyScale;
            if (Mathf.Min(Mathf.Abs(s.x), Mathf.Min(Mathf.Abs(s.y), Mathf.Abs(s.z))) < 0.001f) {
                tiny++;
                if (tinyName.Length == 0) {
                    tinyName = r.transform.root.name + "/" + r.name;
                }
            }
        }
        var names = new List<string>(groups.Keys);
        names.Sort((a, b) => groups[b].Count.CompareTo(groups[a].Count));
        var sb = new StringBuilder();
        sb.Append("NaN pixels with everything drawn: ").Append(baseNan).Append("; ");
        int tested = 0;
        foreach (string key in names) {
            if (tested++ >= 14) {
                break;
            }
            int hot;
            Vector2 first;
            int n = RawNan(cam, groups[key], false, RawHeight, out hot, out first);
            if (n >= 0 && n != baseNan) {
                sb.Append("without ").Append(key).Append(" (").Append(groups[key].Count).Append(" renderers): ").Append(n).Append("; ");
            }
        }
        int skyHot;
        Vector2 skyFirst;
        int skyless = RawNan(cam, null, true, RawHeight, out skyHot, out skyFirst);
        if (skyless >= 0 && skyless != baseNan) {
            sb.Append("with the sky replaced by a colour: ").Append(skyless).Append("; ");
        }
        sb.Append(tiny).Append(" renderer(s) with a scale under 0.001").Append(tiny > 0 ? " (first " + tinyName + ")" : "").Append(".");
        return sb.ToString();
    }

    // One look: the final frame for a black block, the raw frame for NaN. `at` names the moment. Quiet unless it finds
    // something or `say` is set. Returns true when a black block was found.
    static bool Probe(PlaytestRun run, string at, bool say) {
        Camera cam = Camera.main;
        if (cam == null) {
            return false;
        }
        Scans++;
        Frame f = ScanFrame(cam);
        int hot;
        Vector2 first;
        int nan = RawNan(cam, null, false, say ? RawHeight : ScanHeight, out hot, out first);
        TopMiddleBlackMax = Mathf.Max(TopMiddleBlackMax, f.TopMiddleBlack);
        NanMax = Mathf.Max(NanMax, nan);
        string frame = f.Ok ? "frame " + f.Width + "x" + f.Height + " luma " + f.Luma.ToString("0.00") + ", top middle luma " + f.TopMiddleLuma.ToString("0.00") + " and " + f.TopMiddleBlack.ToString("0.0") + "% exactly black" : "no frame";
        string raw = nan < 0 ? "raw HDR frame not readable here" : "raw HDR frame: " + nan + " NaN or Inf, " + hot + " over 64";
        if (f.Block) {
            BlackFrames++;
            if (BlackFrames <= 4) {
                run.Event("black-block", at + ": a solid black block " + f.BlockRect.width + "x" + f.BlockRect.height + " px (" + f.BlockPixels + " px, " + (100f * f.BlockPixels / (f.Width * f.Height)).ToString("0.0") + "% of the frame) at x " + f.BlockRect.x + ", y " + (f.Height - f.BlockRect.yMax) + " from the top, in a " + f.Width + "x" + f.Height + " frame; " + raw);
            }
            if (BlackFrames == 1) {
                run.Problem(PlaytestSeverity.Bug, "chapter2-hud: a solid black block " + f.BlockRect.width + "x" + f.BlockRect.height + " px shows in the frame at " + at);
            }
        }
        if (nan > 0) {
            NanFrames++;
            string spot = first.x >= 0f ? " first at viewport " + first.x.ToString("0.00") + "," + first.y.ToString("0.00") : "";
            string who = "";
            if (diagnosed < 2) {
                diagnosed++;
                who = " " + Culprits(cam, nan);
            }
            if (NanFrames <= 6) {
                run.Event("hdr-nan", at + ": " + raw + spot + "." + who + " BTPost hides them, so no black block shows, but something still draws them");
            }
            if (NanFrames == 1) {
                run.Problem(PlaytestSeverity.Warning, "chapter2-hud: the raw HDR frame has " + nan + " NaN or Inf pixels at " + at + " (the bloom guard hides them; the source is still there)");
            }
        }
        if (say && !f.Block) {
            run.Event("black-scan", at + ": no black block; " + frame + "; " + raw);
        }
        return f.Block;
    }

    // A quiet look every few seconds while the bot walks: cheap enough not to stall a frame, silent when clean.
    public static void Tick(PlaytestRun run, string at) {
        float now = Time.realtimeSinceStartup;
        if (now < nextTick) {
            return;
        }
        nextTick = now + 4f;
        Probe(run, at, false);
    }

    // A shot of the top-middle HUD: the scene is named, the facts and the view are logged, the bot keeps `tick` going
    // (batting, say) and stands for `hold` seconds so a screenshot of the page lands on it, and the frame is scanned at the
    // start and the end.
    public static IEnumerator Moment(PlaytestRun run, ChapterRun chapter, string name, string moment, float hold, System.Action tick) {
        Shots++;
        run.Scene("Chapter 2 HUD: " + name);
        yield return null;
        int bots = YoBotsOnScreen();
        run.Event("hud-shot", moment + " | Yo Bots on screen: " + bots + " | " + Describe(chapter));
        Probe(run, name + " (start)", true);
        float until = Time.realtimeSinceStartup + hold;
        while (!run.Done && Time.realtimeSinceStartup < until) {
            if (tick != null) {
                tick();
            }
            yield return null;
        }
        if (!run.Done) {
            Probe(run, name + " (end)", true);
        }
    }

    // The end of the scenario: the numbers the review reads, and the verdict.
    public static void Finish(PlaytestRun run) {
        Normals(run);
        run.Metric("hudShots", Shots);
        run.Metric("blackScans", Scans);
        run.Metric("blackBlocks", BlackFrames);
        run.Metric("topMiddleBlackPct", Mathf.Round(TopMiddleBlackMax * 10f) / 10f);
        run.Metric("hdrNanFrames", NanFrames);
        run.Metric("hdrNanPixelsMax", NanMax);
        run.Metric("yoBotsOnScreenMax", MostYoBots);
        if (!hdrKnown) {
            run.Problem(PlaytestSeverity.Warning, "chapter2-hud: the HDR state was never read");
        }
        if (MostYoBots == 0) {
            run.Problem(PlaytestSeverity.Warning, "chapter2-hud: no Yo Bot was ever on screen, so the shots do not show the moment J reported");
        }
        run.Event("hud-verdict", Shots + " shots, " + Scans + " scans, black blocks " + BlackFrames + ", frames with NaN or Inf in the raw HDR frame " + NanFrames + " (worst " + NanMax + " px), most Yo Bots on screen " + MostYoBots
            + ", worst top-middle black " + TopMiddleBlackMax.ToString("0.0") + "%" + (BlackFrames == 0 ? ": no black block showed" : ": THE BLACK BLOCK IS STILL THERE"));
        run.Finish(PlaytestOutcome.Done);
    }

    // A fresh run starts clean (the statics live as long as the page does).
    public static void Reset() {
        Shots = 0;
        Scans = 0;
        BlackFrames = 0;
        NanFrames = 0;
        MostYoBots = 0;
        TopMiddleBlackMax = 0f;
        NanMax = 0;
        diagnosed = 0;
        nextTick = 0f;
        hdrKnown = false;
        normalsSaid = false;
    }
}
