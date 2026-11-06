using System.Collections.Generic;
using UnityEngine;

// A chapter's own map, built inside its own Unity scene (Assets/Maps/Map_<Name>.unity), not out of the shared arena's
// blocks. ChapterMap loads the scene additively and attaches the chapter's OwnMap subclass to the scene's "Map Root"
// object; the subclass builds the place: a terrain skirt shaped for the place, a sky of its own, lamps, props, a laid
// route, a landmark, a hazard, a scripted set piece and a music bed.
//
// Rules held here: nothing has a collider (bots walk straight lines; the playfield within 150 m of the circle stays
// free of anything solid), no spark/glitter particles, no camera shake. Everything is drawn in vertex colour on the
// bots' knoll shader, so it sits in the same night as the rest of the game. `Features` names each piece for the
// playtest ("route:", "landmark:", "hazard:", "setpiece:").
public abstract class OwnMap : MonoBehaviour {
    public enum Shape { Box, Cylinder, Cone }

    public readonly List<string> Features = new List<string>();
    public readonly List<string> Log = new List<string>();   // what the set piece did, for the playtest
    public int Beats { get; protected set; }
    public Material Sky { get; private set; }
    public AudioSource Music { get; private set; }
    public int Pieces { get; private set; }
    public abstract string Key { get; }

    protected Story.Chapter chapter;
    protected Vector3 center;
    protected Transform root;
    protected readonly List<Object> made = new List<Object>();
    readonly Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();
    Material flat, glowing;
    AudioSource oneShot;

    // The subclass for a map name (ChapterMap.Defs), added to the scene's root object.
    public static OwnMap Attach(string key, GameObject host) {
        switch (key) {
            case "hollow": return host.AddComponent<HollowMap>();
            case "hollow-edge": return host.AddComponent<EdgeMap>();
            case "clearing-floor": return host.AddComponent<FloorMap>();
            case "long-walk": return host.AddComponent<WalkMap>();
            case "reading-room": return host.AddComponent<ReadingMap>();
            case "hollow-filed": return host.AddComponent<FiledMap>();
            case "office": return host.AddComponent<OfficeMap>();
        }
        return null;
    }

    public void Boot(Story.Chapter data) {
        chapter = data;
        root = transform;
        center = Musician.Instance != null ? Musician.Instance.transform.position : Vector3.zero;
        flat = new Material(BirdArt.Shared) { name = "Own map (flat)" };
        if (flat.HasProperty("_RimStrength")) {
            flat.SetFloat("_RimStrength", 0.1f);
        }
        glowing = new Material(BirdArt.Glowing) { name = "Own map (glow)" };
        made.Add(flat);
        made.Add(glowing);
        Build();
        Debug.Log("[OwnMap] " + Key + " built: " + Pieces + " pieces, " + Features.Count + " features");
    }

    protected abstract void Build();

    // A page of the Reading Room has been read (ChapterRun.PageRead); the other places ignore it.
    public virtual void PageRead(int page) { }

    // The chapter's boss arena (a kit-built dais with its own props: ar_server_dais, ar_gallery_dais, ar_threshold_dais), away from the
    // route. The boss walks out of it (WaveDirector.SpawnForChapter reads BossSpot). No colliders. The map builds the pieces; this
    // names the spot and returns a group for them.
    public bool HasArena { get; private set; }
    public Vector3 BossSpot { get; private set; }
    public float ArenaBearing { get; private set; }

    protected Transform DeclareArena(string name, float bearing, float dist) {
        Transform g = Group(name);
        HasArena = true;
        BossSpot = Polar(bearing, dist);
        ArenaBearing = bearing;
        return g;
    }

    // The playtest's named setup shortcut: make the set piece happen now.
    public abstract void Trigger();

    protected void Beat(string what) {
        Beats++;
        Log.Add(what);
    }

    void OnDestroy() {
        foreach (Object o in made) {
            if (o != null) {
                Destroy(o);
            }
        }
        made.Clear();
    }

    // ------------------------------------------------------------------ Placing things
    protected Vector3 Polar(float bearing, float dist, float y = 0f) {
        float a = bearing * Mathf.Deg2Rad;
        return center + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * dist + Vector3.up * y;
    }

    protected Transform Group(string name) {
        var g = new GameObject(name);
        g.transform.SetParent(root, false);
        return g.transform;
    }

    protected GameObject Piece(Transform parent, string name, Vector3 pos, Vector3 size, Color color, float yaw = 0f, float roll = 0f, Shape shape = Shape.Box, bool glow = false) {
        var g = new GameObject(name);
        g.transform.SetParent(parent != null ? parent : root, false);
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(0f, yaw, roll);
        g.transform.localScale = size;
        g.AddComponent<MeshFilter>().sharedMesh = Tinted(shape, color);
        var r = g.AddComponent<MeshRenderer>();
        r.sharedMaterial = glow ? glowing : flat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        Pieces++;
        return g;
    }

    protected Light Lamp(Transform parent, Vector3 pos, Color color, float range, float intensity) {
        var g = new GameObject("Lamp");
        g.transform.SetParent(parent != null ? parent : root, false);
        g.transform.position = pos;
        Light l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        return l;
    }

    // A laid path of flat slabs along a bearing.
    protected void Path(Transform parent, float bearing, float from, float to, float width, float step, Color a, Color b, float y = 0.1f) {
        int i = 0;
        for (float d = from; d <= to; d += step, i++) {
            Piece(parent, "Slab " + i, Polar(bearing, d, y), new Vector3(width, 0.08f, step * 0.86f), i % 2 == 0 ? a : b, bearing);
        }
    }

    // ------------------------------------------------------------------ Meshes in vertex colour (no assets, no colliders)
    Mesh Tinted(Shape shape, Color color) {
        Color32 c32 = color;
        c32.a = 255;
        int key = c32.r | (c32.g << 8) | (c32.b << 16) | ((int)shape << 24);
        if (meshes.TryGetValue(key, out Mesh m)) {
            return m;
        }
        m = shape == Shape.Box ? BoxMesh() : PrismMesh(14, shape == Shape.Cone ? 0f : 1f);
        var colors = new Color32[m.vertexCount];
        for (int i = 0; i < colors.Length; i++) {
            colors[i] = c32;
        }
        m.colors32 = colors;
        m.name = "Own " + shape;
        meshes[key] = m;
        made.Add(m);
        return m;
    }

    static Mesh BoxMesh() {
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var t = new List<int>();
        Vector3[] normals = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        foreach (Vector3 nrm in normals) {
            Vector3 u = Mathf.Abs(nrm.y) > 0.5f ? Vector3.right : Vector3.up;
            Vector3 w = Vector3.Cross(nrm, u);
            int s = v.Count;
            v.Add((nrm + u + w) * 0.5f);
            v.Add((nrm + u - w) * 0.5f);
            v.Add((nrm - u - w) * 0.5f);
            v.Add((nrm - u + w) * 0.5f);
            for (int i = 0; i < 4; i++) {
                n.Add(nrm);
            }
            t.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
        }
        var mesh = new Mesh();
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.SetTriangles(t, 0);
        return mesh;
    }

    // Unit height and width (centred): topRatio 1 is a cylinder, 0 a cone.
    static Mesh PrismMesh(int sides, float topRatio) {
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var t = new List<int>();
        for (int i = 0; i <= sides; i++) {
            float a = i / (float)sides * Mathf.PI * 2f;
            var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            v.Add(dir * 0.5f + Vector3.down * 0.5f);
            v.Add(dir * 0.5f * topRatio + Vector3.up * 0.5f);
            Vector3 nrm = (dir + Vector3.up * (1f - topRatio) * 0.5f).normalized;
            n.Add(nrm);
            n.Add(nrm);
        }
        for (int i = 0; i < sides; i++) {
            int b = i * 2;
            t.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 });
        }
        int cap = v.Count;
        v.Add(Vector3.down * 0.5f);
        n.Add(Vector3.down);
        for (int i = 0; i <= sides; i++) {
            float a = i / (float)sides * Mathf.PI * 2f;
            v.Add(new Vector3(Mathf.Sin(a) * 0.5f, -0.5f, Mathf.Cos(a) * 0.5f));
            n.Add(Vector3.down);
        }
        for (int i = 0; i < sides; i++) {
            t.AddRange(new[] { cap, cap + 2 + i, cap + 1 + i });
        }
        if (topRatio > 0f) {
            int top = v.Count;
            v.Add(Vector3.up * 0.5f);
            n.Add(Vector3.up);
            for (int i = 0; i <= sides; i++) {
                float a = i / (float)sides * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 0.5f * topRatio + Vector3.up * 0.5f);
                n.Add(Vector3.up);
            }
            for (int i = 0; i < sides; i++) {
                t.AddRange(new[] { top, top + 1 + i, top + 2 + i });
            }
        }
        var mesh = new Mesh();
        mesh.SetVertices(v);
        mesh.SetNormals(n);
        mesh.SetTriangles(t, 0);
        return mesh;
    }

    // ------------------------------------------------------------------ Terrain: a skirt round the playfield, shaped for the place
    // Starts just outside the arena's ground (205 m) and runs out to 560 m. `height(angleDeg, dist)` is the lift; `tint`
    // the vertex colour for a height and distance. One mesh, no collider.
    protected void Terrain(string name, System.Func<float, float, float> height, System.Func<float, float, float, Color> tint) {
        const int around = 120;
        const int rings = 14;
        const float inner = 203f, outer = 560f;
        var v = new Vector3[(around + 1) * (rings + 1)];
        var c = new Color32[v.Length];
        for (int r = 0; r <= rings; r++) {
            float u = r / (float)rings;
            float dist = Mathf.Lerp(inner, outer, u * u * 0.6f + u * 0.4f);
            for (int a = 0; a <= around; a++) {
                float deg = a / (float)around * 360f;
                float rad = deg * Mathf.Deg2Rad;
                float h = height(deg % 360f, dist);
                v[r * (around + 1) + a] = center + new Vector3(Mathf.Sin(rad) * dist, h - 0.12f, Mathf.Cos(rad) * dist);
                Color32 col = tint(deg % 360f, dist, h);
                col.a = 255;
                c[r * (around + 1) + a] = col;
            }
        }
        var t = new List<int>();
        for (int r = 0; r < rings; r++) {
            for (int a = 0; a < around; a++) {
                int i = r * (around + 1) + a;
                int j = i + around + 1;
                t.AddRange(new[] { i, j, i + 1, i + 1, j, j + 1 });
            }
        }
        var mesh = new Mesh { name = name };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(v);
        mesh.SetTriangles(t, 0);
        mesh.colors32 = c;
        mesh.RecalculateNormals();
        made.Add(mesh);
        var g = new GameObject(name);
        g.transform.SetParent(root, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        var rend = g.AddComponent<MeshRenderer>();
        rend.sharedMaterial = flat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
    }

    // Deterministic smooth noise in [0,1] for hills, so a map looks the same every time it loads.
    protected static float Noise(float x, float y, float seed) {
        return Mathf.PerlinNoise(x * 0.013f + seed * 17.3f, y * 0.013f + seed * 5.1f);
    }

    // ------------------------------------------------------------------ Sky
    protected void SetSky(Color top, Color horizon, Color below, Color disc, Vector3 discDir, float discSize, float haze) {
        Shader shader = Shader.Find("Knoll/ChapterSky");
        if (shader == null) {
            return;
        }
        Sky = new Material(shader) { name = "Sky " + Key };
        Sky.SetColor("_Top", top);
        Sky.SetColor("_Horizon", horizon);
        Sky.SetColor("_Ground", below);
        Sky.SetColor("_Disc", disc);
        Sky.SetVector("_DiscDir", discDir);
        Sky.SetFloat("_DiscSize", discSize);
        Sky.SetFloat("_Haze", haze);
        made.Add(Sky);
    }

    // ------------------------------------------------------------------ Music and hits, made in code (no GPU, no files)
    // A loop of held tones (frequencies snapped to whole cycles so it joins seamlessly), a slow swell, an optional pulse and an
    // optional struck note on a beat. Follows the Settings music volume (SoundSettings.Apply reads looping sources).
    protected void MakeMusic(float seconds, float[] notes, float swells, float pulseHz, float pulseDepth, float strikeEvery, float strikeHz, float volume) {
        const int rate = 22050;
        int count = Mathf.RoundToInt(seconds * rate);
        var data = new float[count];
        for (int n = 0; n < notes.Length; n++) {
            float f = Mathf.Max(1f, Mathf.Round(notes[n] * seconds)) / seconds;
            float detune = 1f + 0.0018f * (n % 2 == 0 ? 1f : -1f);
            float phase = n * 1.7f;
            for (int i = 0; i < count; i++) {
                float t = i / (float)rate;
                float swell = 0.55f + 0.45f * Mathf.Sin(Mathf.PI * 2f * (swells * t / seconds + n * 0.23f));
                float tone = Mathf.Sin(Mathf.PI * 2f * f * t + phase) * 0.6f + Mathf.Sin(Mathf.PI * 2f * f * 2f * detune * t) * 0.18f + Mathf.Sin(Mathf.PI * 2f * f * 3f * t) * 0.06f;
                data[i] += tone * swell / notes.Length;
            }
        }
        if (pulseHz > 0f) {
            float ph = Mathf.Max(1f, Mathf.Round(pulseHz * seconds)) / seconds;
            for (int i = 0; i < count; i++) {
                float t = i / (float)rate;
                float p = 0.5f + 0.5f * Mathf.Sin(Mathf.PI * 2f * ph * t);
                data[i] *= 1f - pulseDepth * Mathf.Pow(p, 3f);
            }
        }
        if (strikeEvery > 0f) {
            int hits = Mathf.Max(1, Mathf.RoundToInt(seconds / strikeEvery));
            float f = Mathf.Max(1f, Mathf.Round(strikeHz * strikeEvery)) / strikeEvery;
            for (int h = 0; h < hits; h++) {
                int start = Mathf.RoundToInt(h * seconds / hits * rate);
                for (int i = 0; i < rate * 2 && i < count; i++) {
                    float t = i / (float)rate;
                    int at = (start + i) % count;
                    data[at] += Mathf.Sin(Mathf.PI * 2f * f * t) * Mathf.Exp(-t * 2.4f) * 0.35f;
                }
            }
        }
        float peak = 0.0001f;
        for (int i = 0; i < count; i++) {
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }
        for (int i = 0; i < count; i++) {
            data[i] = data[i] / peak * 0.8f;
        }
        AudioClip clip = AudioClip.Create("Music " + Key, count, 1, rate, false);
        clip.SetData(data, 0);
        made.Add(clip);
        var g = new GameObject("Map Music");
        g.transform.SetParent(root, false);
        Music = g.AddComponent<AudioSource>();
        Music.clip = clip;
        Music.loop = true;
        Music.spatialBlend = 0f;
        Music.volume = volume;
        Music.playOnAwake = false;
        Music.Play();
        SoundSettings.Apply();
    }

    // A short low thump, for a press or a crusher landing.
    AudioClip thud;

    protected void Thud(float volume = 0.8f) {
        if (thud == null) {
            const int rate = 22050;
            var data = new float[rate / 2];
            for (int i = 0; i < data.Length; i++) {
                float t = i / (float)rate;
                data[i] = Mathf.Sin(Mathf.PI * 2f * (62f - 30f * t) * t) * Mathf.Exp(-t * 9f) * 0.9f + (Mathf.PerlinNoise(i * 0.31f, 0.5f) - 0.5f) * Mathf.Exp(-t * 30f);
            }
            thud = AudioClip.Create("Thud", data.Length, 1, rate, false);
            thud.SetData(data, 0);
            made.Add(thud);
            oneShot = gameObject.AddComponent<AudioSource>();
            oneShot.spatialBlend = 0f;
            oneShot.playOnAwake = false;
        }
        oneShot.PlayOneShot(thud, volume * SoundSettings.EffectsLevel);
    }

    // ------------------------------------------------------------------ Hazards
    // A blow on a patch of ground: bots in it (not bosses) take a plain hit and are thrown; Wallit in it flinches. Returns bots hit.
    protected int Strike(Vector3 at, float radius, float power, out bool wallit) {
        int hit = 0;
        wallit = false;
        float r2 = radius * radius;
        foreach (Enemy e in new List<Enemy>(Enemy.Roster)) {
            if (e == null || !e.Alive || e.Boss) {
                continue;
            }
            Vector3 d = e.transform.position - at;
            d.y = 0f;
            if (d.sqrMagnitude <= r2) {
                e.Hit(power);
                e.Knock(d.normalized * 7f + Vector3.up * 3f, 0.3f);
                hit++;
            }
        }
        PlayerGrowth p = PlayerGrowth.Instance;
        if (p != null) {
            Vector3 d = p.transform.position - at;
            d.y = 0f;
            if (d.sqrMagnitude <= r2) {
                p.OnHit();
                wallit = true;
            }
        }
        return hit;
    }

    // A head that rises, hangs on a red warning light, and slams down on its mark; calls `landed` at the slam. Used by the
    // stamp press (armed when Wallit comes near) and the Floor's crushers (always cycling until the belt stops).
    public class Slammer : MonoBehaviour {
        public float High, Low, Period = 3.4f, Offset;
        public float ArmRadius;          // 0: always cycling; else only while Wallit is within this many metres
        public bool Frozen;              // the belt has stopped: hold high and quiet
        public bool Forced;              // the playtest's setup shortcut: act as if armed
        public Light Warning;
        public System.Action<int> Landed;
        public int Slams { get; private set; }
        float clock;
        bool slammed;

        void Update() {
            if (Frozen) {
                Place(1f);
                SetWarning(0f);
                return;
            }
            bool armed = ArmRadius <= 0f || Forced;
            if (!armed) {
                PlayerGrowth p = PlayerGrowth.Instance;
                if (p != null) {
                    Vector3 d = p.transform.position - transform.position;
                    d.y = 0f;
                    armed = d.magnitude <= ArmRadius;
                }
            }
            if (!armed) {
                Place(1f);
                SetWarning(0f);
                clock = 0f;
                slammed = false;
                return;
            }
            clock += Time.deltaTime;
            float t = Mathf.Repeat(clock + Offset, Period) / Period;
            // Rise slowly, hang with the warning lit, slam down, rest.
            float k = t < 0.55f ? Mathf.SmoothStep(0f, 1f, t / 0.55f) : t < 0.7f ? 1f : t < 0.78f ? 1f - (t - 0.7f) / 0.08f : 0f;
            Place(k);
            SetWarning(t >= 0.4f && t < 0.78f ? Mathf.Clamp01((t - 0.4f) / 0.3f) : t >= 0.78f && t < 0.84f ? 1.6f : 0f);
            if (t >= 0.78f && !slammed) {
                slammed = true;
                Slams++;
                if (Landed != null) {
                    Landed(Slams);
                }
            } else if (t < 0.7f) {
                slammed = false;
            }
        }

        void Place(float k) {
            Vector3 p = transform.position;
            p.y = Mathf.Lerp(Low, High, k);
            transform.position = p;
        }

        void SetWarning(float level) {
            if (Warning != null) {
                Warning.intensity = level * 3.2f;
            }
        }
    }
}
