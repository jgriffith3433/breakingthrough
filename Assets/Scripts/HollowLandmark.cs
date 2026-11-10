using UnityEngine;
using UnityEngine.SceneManagement;

// Arcade's far landmark: the Department's stamp press on the wall at the rim, 190 m out (no collider). It lifts and falls on a
// beat that quickens with the wave, and the light turns colder from wave 10 and toward dawn at wave 30 (docs/world-bible.md, A). ArcadeActs owns the light when it is up.
public class HollowLandmark : MonoBehaviour {
    public static int Stamps { get; private set; }
    public static float MaxGapSeconds { get; private set; }

    const float Distance = 190f;
    Transform head;
    Vector3 headHome;
    float phase;
    float lastStampAt;
    Light sun;
    Color sunBase;
    float fogBase;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        SceneManager.sceneLoaded += (s, m) => Ensure();
        Ensure();
    }

    static void Ensure() {
        if (SceneManager.GetActiveScene().name != MetaProgress.GameScene || FindAnyObjectByType<HollowLandmark>() != null) {
            return;
        }
        new GameObject("Hollow Landmark").AddComponent<HollowLandmark>();
    }

    void Start() {
        Stamps = 0;
        MaxGapSeconds = 0f;
        lastStampAt = Time.time;
        Musician musician = Musician.Instance;
        Vector3 center = musician != null ? musician.transform.position : Vector3.zero;
        Vector3 at = center + new Vector3(Mathf.Sin(0.6f), 0f, Mathf.Cos(0.6f)) * Distance;
        at.y = GroundY(at);
        transform.position = at;
        transform.rotation = Quaternion.LookRotation(center - at);

        Material dark = new Material(SafeShader.Find("Unlit/Color"));
        dark.color = new Color(0.03f, 0.035f, 0.06f);
        Material red = new Material(SafeShader.Find("Unlit/Color"));
        red.color = new Color(0.55f, 0.08f, 0.1f);
        Box("Press Frame L", new Vector3(-9f, 14f, 0f), new Vector3(3f, 28f, 3f), dark);
        Box("Press Frame R", new Vector3(9f, 14f, 0f), new Vector3(3f, 28f, 3f), dark);
        Box("Press Bed", new Vector3(0f, 1.5f, 0f), new Vector3(24f, 3f, 8f), dark);
        head = Box("Press Head", new Vector3(0f, 22f, 0f), new Vector3(15f, 5f, 6f), dark).transform;
        Box("Press Face", new Vector3(0f, -3f, 0f), new Vector3(13f, 0.6f, 5f), red).transform.SetParent(head, false);
        head.GetChild(0).localPosition = new Vector3(0f, -2.8f, 0f);
        headHome = head.localPosition;

        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None)) {
            if (l.type == LightType.Directional) {
                sun = l;
                sunBase = l.color;
                break;
            }
        }
        fogBase = RenderSettings.fogDensity;
    }

    GameObject Box(string name, Vector3 local, Vector3 size, Material material) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = local;
        go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size);
        MeshRenderer r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    static Mesh BoxMesh(Vector3 s) {
        Vector3 h = s * 0.5f;
        var v = new System.Collections.Generic.List<Vector3>();
        var t = new System.Collections.Generic.List<int>();
        Vector3[] n = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (Vector3 d in n) {
            Vector3 a = Mathf.Abs(d.y) > 0.5f ? Vector3.right : Vector3.up;
            Vector3 b = Vector3.Cross(d, a);
            Vector3 c = Vector3.Scale(d, h);
            Vector3 ua = Vector3.Scale(a, h), ub = Vector3.Scale(b, h);
            int i = v.Count;
            v.Add(c - ua - ub); v.Add(c + ua - ub); v.Add(c + ua + ub); v.Add(c - ua + ub);
            Vector3 face = Vector3.Cross(v[i + 1] - v[i], v[i + 2] - v[i]);
            if (Vector3.Dot(face, d) > 0f) {
                t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
            } else {
                t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
        }
        var m = new Mesh();
        m.SetVertices(v);
        m.SetTriangles(t, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    void Update() {
        int wave = WaveDirector.Wave;
        float period = wave >= 20 ? 3f : wave >= 10 ? 5f : 8f;
        float before = phase;
        phase += Time.deltaTime / period;
        // slow lift, fast fall: the stamp lands as the phase wraps
        float p = phase % 1f;
        float lift = p < 0.85f ? Mathf.SmoothStep(0f, 1f, p / 0.85f) : 1f - Mathf.Pow((p - 0.85f) / 0.15f, 2f);
        if (head != null) {
            head.localPosition = headHome + Vector3.up * lift * 7f - Vector3.up * 0f;
        }
        if (Mathf.Floor(phase) > Mathf.Floor(before) && Time.timeScale > 0.01f) {
            Stamps++;
            MaxGapSeconds = Mathf.Max(MaxGapSeconds, Time.time - lastStampAt);
            lastStampAt = Time.time;
            GameAudio.Play("stamp", 0.5f, 0.7f);
        }
        if (sun != null && ArcadeActs.Instance == null) {
            float cold = Mathf.Clamp01((wave - 9) / 10f);
            Color col = Color.Lerp(sunBase, new Color(0.55f, 0.68f, 1f), cold);
            float dawn = Mathf.Clamp01((wave - 26) / 4f);
            sun.color = Color.Lerp(col, new Color(1f, 0.72f, 0.6f), dawn);
            RenderSettings.fogDensity = fogBase * (1f - 0.25f * dawn);
        }
    }

    static float GroundY(Vector3 at) {
        if (Physics.Raycast(new Vector3(at.x, at.y + 60f, at.z), Vector3.down, out RaycastHit hit, 200f, 1 << 3, QueryTriggerInteraction.Ignore)) {
            return hit.point.y;
        }
        return at.y;
    }
}
