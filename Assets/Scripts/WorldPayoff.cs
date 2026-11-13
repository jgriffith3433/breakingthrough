using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The payoff, played not told (docs/world-bible.md, beat 15): a door in the Department's wall at the rim opens when the last
// Yo Bot of a wave falls and warm light spills out; the circle's own light climbs a step with every wave cleared; at waves 10,
// 20 and 30 the wall itself breaks (chunky brick plates drop, the piers lower to stumps), so by the finale the wall is gone to
// the stumps Chapter 6 opens on. Arcade reads WaveDirector (Waiting = the break); Chapter 6 calls LastFell when a wave's last
// bot falls. 196 m out, no colliders, no particles, no camera shake.
public class WorldPayoff : MonoBehaviour {
    public static WorldPayoff Instance { get; private set; }
    public static int DoorOpens { get; private set; }
    public static int Breaks { get; private set; }
    public static int GlowSteps { get; private set; }
    public static float DoorOpenness { get { return Instance != null ? Instance.open : 0f; } }
    public static float Glow { get { return Instance != null ? Instance.glow : 0f; } }
    public static readonly List<string> Events = new List<string>();

    const float Distance = 196f;
    const float Bearing = 3.55f;
    static readonly float[] PierTop = { 9f, 6f, 3.6f, 1.8f };

    Vector3 center;
    Transform pierL, pierR, lintel, hinge, beyond;
    Light ringLight, doorLight;
    float open, openTarget, glow, glowTarget, closeAt = -1f;
    float[] pierH = { 9f, 9f };
    float pierGoal = 9f;
    int stage;
    bool chapterMode;
    readonly List<Chunk> chunks = new List<Chunk>();

    class Chunk {
        public Transform t; public Vector3 p0, v; public float born, flight, spin;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        SceneManager.sceneLoaded += (s, m) => Ensure(false);
        Ensure(false);
    }

    static WorldPayoff Ensure(bool chapter) {
        if (SceneManager.GetActiveScene().name != MetaProgress.GameScene) {
            return null;
        }
        if (Instance == null) {
            WorldPayoff existing = FindAnyObjectByType<WorldPayoff>();
            if (existing == null) {
                existing = new GameObject("World Payoff").AddComponent<WorldPayoff>();
            }
            existing.chapterMode = chapter;
        }
        return Instance != null ? Instance : FindAnyObjectByType<WorldPayoff>();
    }

    // Chapter 6 (and the playtest): the last bot of wave n has fallen. Opens the door for a while and brightens the circle.
    public static void LastFell(int wave) {
        WorldPayoff p = Ensure(true);
        if (p != null) {
            p.chapterMode = true;
            p.Cleared(wave, true);
        }
    }

    // The wall gives way (stage 1..3). Public so the finale and the playtest can call it directly.
    public static void BreakWall(int toStage) {
        WorldPayoff p = Ensure(true);
        if (p != null) {
            p.Break(Mathf.Clamp(toStage, 1, 3));
        }
    }

    void Awake() {
        Instance = this;
        Events.Clear();
        DoorOpens = 0;
        Breaks = 0;
        GlowSteps = 0;
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    void Start() {
        if (ChapterRun.Instance != null && !ChapterRun.Instance.IsFinal && !chapterMode) {
            Destroy(gameObject);   // only Arcade and Chapter 6 have this wall
            return;
        }
        Musician m = Musician.Instance;
        center = m != null ? m.transform.position : Vector3.zero;
        Vector3 at = center + new Vector3(Mathf.Sin(Bearing), 0f, Mathf.Cos(Bearing)) * Distance;
        at.y = GroundY(at);
        transform.position = at;
        transform.rotation = Quaternion.LookRotation(new Vector3(center.x - at.x, 0f, center.z - at.z));

        Material brick = Flat(new Color(0.46f, 0.30f, 0.26f));
        Material pale = Flat(new Color(0.78f, 0.72f, 0.62f));
        Material warm = Flat(new Color(1f, 0.82f, 0.45f));
        bool stumps = ChapterRun.Instance != null;   // Chapter 6 opens on a wall already gone to stumps
        float top = stumps ? PierTop[3] : PierTop[0];
        pierL = Box("Wall Pier L", new Vector3(-8f, 0f, 0f), brick);
        pierR = Box("Wall Pier R", new Vector3(8f, 0f, 0f), brick);
        pierH[0] = pierH[1] = pierGoal = top;
        SizePiers();
        stage = stumps ? 3 : 0;
        if (!stumps) {
            lintel = Box("Wall Lintel", new Vector3(0f, 7.5f, 0f), brick);
            lintel.localScale = new Vector3(4f, 3f, 2f);
            lintel.localPosition = new Vector3(0f, 7.5f, 0f);
        }
        // The frame the door hangs in: two pale jambs so it still reads as a doorway once the wall is stumps.
        Box("Jamb L", new Vector3(-2.3f, 0f, 0f), pale).localScale = new Vector3(0.6f, 6.2f, 2.2f);
        Box("Jamb R", new Vector3(2.3f, 0f, 0f), pale).localScale = new Vector3(0.6f, 6.2f, 2.2f);
        foreach (string n in new[] { "Jamb L", "Jamb R" }) {
            Transform j = transform.Find(n);
            j.localPosition = new Vector3(j.localPosition.x, 3.1f, 0f);
        }
        beyond = Box("Light Beyond", new Vector3(0f, 3f, -0.9f), warm);
        beyond.localScale = new Vector3(4f, 6f, 0.05f);
        beyond.gameObject.SetActive(false);
        hinge = new GameObject("Door Hinge").transform;
        hinge.SetParent(transform, false);
        hinge.localPosition = new Vector3(-2f, 0f, 0.3f);
        Transform leaf = Box("Door Leaf", Vector3.zero, Flat(new Color(0.30f, 0.20f, 0.17f)));
        leaf.SetParent(hinge, false);
        leaf.localScale = new Vector3(3.9f, 6f, 0.3f);
        leaf.localPosition = new Vector3(1.95f, 3f, 0f);

        doorLight = new GameObject("Door Light").AddComponent<Light>();
        doorLight.transform.SetParent(transform, false);
        doorLight.transform.localPosition = new Vector3(0f, 3f, 3f);
        doorLight.type = LightType.Point;
        doorLight.color = new Color(1f, 0.78f, 0.42f);
        doorLight.range = 20f;
        doorLight.intensity = 0f;
        doorLight.shadows = LightShadows.None;

        ringLight = new GameObject("Circle Glow").AddComponent<Light>();
        ringLight.transform.position = center + Vector3.up * 3f;
        ringLight.type = LightType.Point;
        ringLight.color = new Color(1f, 0.85f, 0.5f);
        ringLight.range = 24f;
        ringLight.intensity = 0f;
        ringLight.shadows = LightShadows.None;
        if (ChapterRun.Instance != null) {
            glow = glowTarget = 0.5f;
        }
    }

    Transform Box(string name, Vector3 local, Material m) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = local;
        go.AddComponent<MeshFilter>().sharedMesh = UnitBox();
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go.transform;
    }

    static Mesh unit;
    static Mesh UnitBox() {
        if (unit == null) {
            unit = HollowLandmark.BoxMesh(Vector3.one);
        }
        return unit;
    }

    static Material Flat(Color c) {
        Material m = new Material(SafeShader.Find("Unlit/Color"));
        m.color = c;
        return m;
    }

    void SizePiers() {
        pierL.localScale = new Vector3(12f, Mathf.Max(0.5f, pierH[0]), 2f);
        pierL.localPosition = new Vector3(-8f, pierH[0] * 0.5f, 0f);
        pierR.localScale = new Vector3(12f, Mathf.Max(0.5f, pierH[1]), 2f);
        pierR.localPosition = new Vector3(8f, pierH[1] * 0.5f, 0f);
    }

    // A wave is down: the door opens, the circle steps brighter, a ring spreads from it.
    void Cleared(int wave, bool timed) {
        DoorOpens++;
        GlowSteps++;
        openTarget = 1f;
        glowTarget = Mathf.Clamp01(chapterMode ? 0.5f + 0.18f * GlowSteps : wave / 30f + 0.1f);
        if (timed) {
            closeAt = Time.time + 9f;
        }
        Events.Add("door-open|wave " + wave);
        Events.Add("circle-bright|" + glowTarget.ToString("0.00"));
        GameAudio.Play("slot_shutter", 0.7f);
        Fx.Ring(center + Vector3.up * 0.2f, 9f, new Color(1f, 0.85f, 0.45f, 0.8f), 1.4f);
        if (!chapterMode && wave > 0 && wave % 10 == 0) {
            Break(wave / 10);
        }
    }

    void Break(int toStage) {
        if (toStage <= stage || pierL == null) {
            return;
        }
        stage = toStage;
        Breaks++;
        pierGoal = PierTop[Mathf.Clamp(stage, 0, 3)];
        Events.Add("wall-break|stage " + stage);
        GameAudio.Play("stamp_heavy", 0.9f, 0.6f);
        if (lintel != null) {
            Shed(lintel.position, 6);
            Destroy(lintel.gameObject);
            lintel = null;
        }
        Shed(pierL.position + Vector3.up * pierH[0] * 0.6f, 6);
        Shed(pierR.position + Vector3.up * pierH[1] * 0.6f, 6);
    }

    // Chunky brick plates arc down off the wall and lie where they land (no physics, no colliders, no sparks).
    void Shed(Vector3 from, int count) {
        for (int i = 0; i < count; i++) {
            GameObject go = new GameObject("Wall Plate");
            go.AddComponent<MeshFilter>().sharedMesh = UnitBox();
            go.AddComponent<MeshRenderer>();
            go.transform.SetParent(transform.parent, true);
            go.transform.localScale = new Vector3(Random.Range(1.2f, 2.2f), Random.Range(0.7f, 1.2f), Random.Range(1f, 1.8f));
            go.GetComponent<MeshRenderer>().sharedMaterial = Flat(Color.Lerp(new Color(0.46f, 0.30f, 0.26f), new Color(0.36f, 0.24f, 0.22f), Random.value));
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Chunk c = new Chunk();
            c.t = go.transform;
            c.p0 = from + new Vector3(Random.Range(-3f, 3f), Random.Range(-1f, 1f), Random.Range(-1f, 1f));
            c.t.position = c.p0;
            c.flight = Random.Range(0.8f, 1.2f);
            Vector3 end = c.p0 + (transform.forward * Random.Range(-1f, 4f) + transform.right * Random.Range(-5f, 5f));
            float landY = GroundY(end) + c.t.localScale.y * 0.5f;
            c.v = new Vector3((end.x - c.p0.x) / c.flight, (landY - c.p0.y + 0.5f * 19.6f * c.flight * c.flight) / c.flight, (end.z - c.p0.z) / c.flight);
            c.spin = Random.Range(-90f, 90f);
            c.born = Time.time;
            chunks.Add(c);
        }
        while (chunks.Count > 40) {
            Destroy(chunks[0].t.gameObject);
            chunks.RemoveAt(0);
        }
    }

    void Update() {
        if (!chapterMode && ChapterRun.Instance == null) {
            // Arcade: the door stands open through the break between waves and shuts when the next wave starts.
            bool waiting = WaveDirector.Waiting && WaveDirector.Wave > 1;
            if (waiting && openTarget < 0.5f) {
                Cleared(WaveDirector.Wave - 1, false);
            } else if (!waiting) {
                openTarget = 0f;
            }
        } else if (closeAt > 0f && Time.time > closeAt) {
            openTarget = 0f;
            closeAt = -1f;
        }
        open = Mathf.MoveTowards(open, openTarget, Time.deltaTime / 1.1f);
        float e = Mathf.SmoothStep(0f, 1f, open);
        if (hinge != null) {
            hinge.localRotation = Quaternion.Euler(0f, -105f * e, 0f);
            beyond.gameObject.SetActive(e > 0.02f);
            doorLight.intensity = 1.6f * e;
        }
        glow = Mathf.MoveTowards(glow, glowTarget, Time.deltaTime / 2.5f);
        if (ringLight != null) {
            ringLight.intensity = 0.25f + 1.4f * glow;
        }
        if (pierL != null && !Mathf.Approximately(pierH[0], pierGoal)) {
            pierH[0] = Mathf.MoveTowards(pierH[0], pierGoal, Time.deltaTime * 4f);
            pierH[1] = Mathf.MoveTowards(pierH[1], pierGoal, Time.deltaTime * 4f);
            SizePiers();
        }
        for (int i = 0; i < chunks.Count; i++) {
            Chunk c = chunks[i];
            float t = Time.time - c.born;
            if (t < c.flight) {
                c.t.position = c.p0 + c.v * t + Vector3.down * 0.5f * 19.6f * t * t;
                c.t.Rotate(Vector3.up, c.spin * Time.deltaTime, Space.World);
            }
        }
    }

    static float GroundY(Vector3 at) {
        if (Physics.Raycast(new Vector3(at.x, at.y + 60f, at.z), Vector3.down, out RaycastHit hit, 200f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            return hit.point.y;
        }
        return at.y;
    }
}
