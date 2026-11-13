using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Arcade's place: the ruin of the Dead Letter Office behind the circle (docs/world-bible.md, A). Built once at the start of an
// Arcade run, entirely from Model Kit pieces (KitCatalogHollow.cs and the yard palette), outside the 150 m the bots walk and
// with no collider. A leaning brick front with an arched door, a broken upper storey with a clock that has no hands, a giant
// rusted post box and a fallen one, sorting shelves spilling envelopes, rubble and parcels; then the rim: a hedge run and a
// picket run all the way round at 186 m (drawn instanced), lamp posts with light pools, parcels and paper drifts.
// Every piece is seated on the Ground layer by a raycast at build time; the landmark's pieces are combined per material so the
// whole place is a handful of draw calls. The landmark stands on the bearing Wallit faces at the start, so it is in the first frame.
public class HollowPostOffice : MonoBehaviour {
    public static HollowPostOffice Instance { get; private set; }
    public static bool Built;
    public static Vector3 LandmarkBase;
    public static Vector3 Circle;
    public static int Props, Floating, Sunk, DrawCalls, LampPosts, Parcels;
    public static float MinDistanceFromCircle = 9999f;
    public static float WorstFloat, WorstSink;
    public static readonly int[] SideHedges = new int[4];   // north (+Z), east (+X), south, west

    const float LandmarkDistance = 162f;
    const float RimRadius = 186f;
    const float PickRadius = 183.6f;
    const float SegmentLength = 4f;

    readonly List<Vector3> pools = new List<Vector3>();
    Transform dress;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => Ensure();
        Ensure();
    }

    static void Ensure() {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != MetaProgress.GameScene || FindAnyObjectByType<HollowPostOffice>() != null) {
            return;
        }
        new GameObject("Hollow Post Office").AddComponent<HollowPostOffice>();
    }

    IEnumerator Start() {
        Instance = this;
        Built = false;
        yield return null;
        yield return null;
        if (ChapterRun.Instance != null) {   // a campaign chapter has its own map
            Destroy(gameObject);
            yield break;
        }
        Musician musician = Musician.Instance;
        Circle = musician != null ? musician.transform.position : Vector3.zero;
        Vector3 dir = Vector3.forward;
        CharacterMovement player = FindAnyObjectByType<CharacterMovement>();
        if (player != null) {
            dir = player.transform.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) {
                dir = Vector3.forward;
            }
            dir.Normalize();
        }
        Build(dir);
    }

    void Build(Vector3 dir) {
        Props = Floating = Sunk = DrawCalls = LampPosts = Parcels = 0;
        MinDistanceFromCircle = 9999f;
        WorstFloat = WorstSink = 0f;
        for (int i = 0; i < 4; i++) {
            SideHedges[i] = 0;
        }
        dress = transform;
        Vector3 at = Circle + dir * LandmarkDistance;
        at.y = LevelDress.GroundY(at.x, at.z, Circle.y);
        LandmarkBase = at;
        Quaternion face = Quaternion.LookRotation(-dir, Vector3.up);

        BuildLandmark(at, face);
        BuildRim();
        BuildLamps(dir);
        BuildPools();
        Built = true;
        Debug.Log("[HollowPostOffice] props=" + Props + " drawCalls~" + DrawCalls + " floating=" + Floating + " sunk=" + Sunk + " minDist=" + MinDistanceFromCircle.ToString("0"));
    }

    // ---- the office --------------------------------------------------------------------------------------------------

    // x: along the front (right when facing the circle), z: toward the circle, all metres from the landmark's base point.
    Vector3 W(Vector3 at, Quaternion face, float x, float z) {
        Vector3 p = at + face * new Vector3(x, 0f, z);
        p.y = LevelDress.GroundY(p.x, p.z, at.y);
        return p;
    }

    GameObject Place(Transform parent, string id, Vector3 pos, Quaternion rot, float scale, bool audit = true) {
        GameObject go = KitCatalog.Make(id, parent);
        if (go == null) {
            return null;
        }
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) {
            Destroy(c);
        }
        go.transform.position = pos;
        go.transform.rotation = rot;
        go.transform.localScale = Vector3.one * scale;
        if (audit) {
            Record(go);
        }
        return go;
    }

    // Props, the ground contact audit, and the distance from the circle (nothing may stand in the bots' 150 m).
    void Record(GameObject go) {
        Props++;
        Vector3 p = go.transform.position;
        Vector2 flat = new Vector2(p.x - Circle.x, p.z - Circle.z);
        MinDistanceFromCircle = Mathf.Min(MinDistanceFromCircle, flat.magnitude);
        float ground = LevelDress.GroundY(p.x, p.z, p.y);
        float gap = p.y - ground;
        if (gap > 0.02f) {
            Floating++;
            WorstFloat = Mathf.Max(WorstFloat, gap);
        } else if (gap < -0.05f) {
            Sunk++;
            WorstSink = Mathf.Min(WorstSink, gap);
        }
    }

    void BuildLandmark(Vector3 at, Quaternion face) {
        var root = new GameObject("Dead Letter Office").transform;
        root.SetParent(dress, false);

        Place(root, "hl_office_front", at, face, 2f);
        // The broken storey: tipped a few degrees on the wall top, which is what makes the facade lean.
        GameObject upper = Place(root, "hl_office_upper", at + Vector3.up * 14.9f, face * Quaternion.Euler(0f, 0f, 2.6f), 2f, false);
        if (upper != null) {
            upper.transform.position = at + Vector3.up * 14.9f;
            GameObject clock = KitCatalog.Make("hl_clock_face", upper.transform);
            foreach (Collider c in clock.GetComponentsInChildren<Collider>()) {
                Destroy(c);
            }
            clock.transform.localPosition = new Vector3(0f, 0.38f, 0.42f);
            clock.transform.localRotation = Quaternion.identity;
            clock.transform.localScale = Vector3.one * 0.8f;
        }
        // Rubble heaps at both ends and a fallen chunk of cornice at the door.
        Place(root, "fd_rubble", W(at, face, -19f, 3f), face * Quaternion.Euler(0f, 20f, 0f), 3.2f);
        Place(root, "fd_rubble", W(at, face, 19.5f, 2f), face * Quaternion.Euler(0f, -35f, 0f), 2.8f);
        Place(root, "fd_rubble", W(at, face, 4f, 9f), face * Quaternion.Euler(0f, 80f, 0f), 1.1f);
        // The giant post boxes: one still standing and rusted, one fallen and open.
        GameObject box = Place(root, "hl_post_box", W(at, face, -25f, 6f), face * Quaternion.Euler(0f, 18f, 0f), 7f);
        Rust(box, new Color(0.62f, 0.34f, 0.2f));
        GameObject fallen = Place(root, "yd_fallen_mailbox", W(at, face, 27f, 5f), face * Quaternion.Euler(0f, -25f, 0f), 6f);
        Rust(fallen, new Color(0.5f, 0.3f, 0.2f));
        // Sorting shelves: two inside the door (seen through it and the windows), two out front on their sides of the path.
        Place(root, "hl_shelf", W(at, face, -4.5f, -3.2f), face, 2.2f);
        Place(root, "hl_shelf", W(at, face, 4.5f, -3.2f), face, 2.2f);
        Place(root, "hl_shelf", W(at, face, -12f, 8f), face * Quaternion.Euler(0f, 16f, 0f), 2.4f);
        Place(root, "hl_shelf", W(at, face, 13f, 7f), face * Quaternion.Euler(0f, -22f, 0f), 2.4f);
        // Parcels and paper spilled across the doorstep and the front.
        float[] px = { -9f, -6.5f, -3f, 3.2f, 6.8f, 9.5f, -15f, 15f, -2f, 8f };
        float[] pz = { 6f, 8.5f, 4.5f, 6.5f, 9f, 5f, 4f, 7f, 8f, 3.5f };
        for (int i = 0; i < px.Length; i++) {
            Place(root, "hl_parcel", W(at, face, px[i], pz[i]), face * Quaternion.Euler(0f, i * 61f, 0f), 2f + (i % 3) * 0.4f);
            Parcels++;
        }
        for (int i = 0; i < 6; i++) {
            Place(root, "hl_paper_drift", W(at, face, -14f + i * 5.6f, 6f + (i % 3) * 1.5f), face * Quaternion.Euler(0f, i * 73f, 0f), 3f);
        }
        DrawCalls += Combine(root, "Dead Letter Office (combined)");
    }

    static void Rust(GameObject piece, Color c) {
        if (piece != null) {
            LevelDress.Tint(piece, 0, c);
        }
    }

    // One mesh per material for everything under `root` (and its renderers are removed): a pile of kit pieces becomes a few draws.
    int Combine(Transform root, string label) {
        var groups = new Dictionary<Material, List<CombineInstance>>();
        foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>()) {
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || mf.sharedMesh == null) {
                continue;
            }
            Material[] mats = mr.sharedMaterials;
            for (int s = 0; s < mf.sharedMesh.subMeshCount && s < mats.Length; s++) {
                List<CombineInstance> list;
                if (!groups.TryGetValue(mats[s], out list)) {
                    list = new List<CombineInstance>();
                    groups[mats[s]] = list;
                }
                list.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = mf.transform.localToWorldMatrix });
            }
        }
        for (int i = root.childCount - 1; i >= 0; i--) {
            Destroy(root.GetChild(i).gameObject);
        }
        int n = 0;
        foreach (KeyValuePair<Material, List<CombineInstance>> g in groups) {
            var mesh = new Mesh { name = label + " " + n, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(g.Value.ToArray(), true, true);
            mesh.RecalculateBounds();
            var go = new GameObject(label + " " + n);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = g.Key;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            n++;
        }
        return n;
    }

    // ---- the rim -----------------------------------------------------------------------------------------------------

    void BuildRim() {
        Vector3 landmarkFlat = LandmarkBase;
        var hedges = new List<Matrix4x4>();
        var pickets = new List<Matrix4x4>();
        int count = Mathf.RoundToInt(2f * Mathf.PI * RimRadius / SegmentLength);
        for (int i = 0; i < count; i++) {
            float a0 = i * 2f * Mathf.PI / count;
            float a1 = (i + 1) * 2f * Mathf.PI / count;
            Vector3 mid = Circle + new Vector3(Mathf.Sin((a0 + a1) * 0.5f), 0f, Mathf.Cos((a0 + a1) * 0.5f)) * RimRadius;
            if (new Vector2(mid.x - landmarkFlat.x, mid.z - landmarkFlat.z).magnitude < 34f) {
                continue;   // the office's rubble and rubble-side boxes stand here
            }
            hedges.Add(Segment(a0, a1, RimRadius));
            pickets.Add(Segment(a0, a1, PickRadius));
            float deg = (a0 + a1) * 0.5f * Mathf.Rad2Deg;
            SideHedges[(int)(Mathf.Repeat(deg + 45f, 360f) / 90f) % 4]++;
        }
        InstanceRun("Hedge Run", "hl_hedge", hedges);
        InstanceRun("Picket Run", "hl_picket", pickets);
    }

    // One 4 m run laid along the rim, seated on the ground by its two ends and middle, facing the circle.
    Matrix4x4 Segment(float a0, float a1, float radius) {
        Vector3 p0 = Circle + new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * radius;
        Vector3 p1 = Circle + new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * radius;
        Vector3 pm = (p0 + p1) * 0.5f;
        p0.y = LevelDress.GroundY(p0.x, p0.z, Circle.y);
        p1.y = LevelDress.GroundY(p1.x, p1.z, Circle.y);
        float gm = LevelDress.GroundY(pm.x, pm.z, Circle.y);
        pm.y = Mathf.Min((p0.y + p1.y) * 0.5f, gm);
        Vector3 right = (p1 - p0).normalized;
        Vector3 inward = Circle - pm;
        inward.y = 0f;
        inward.Normalize();
        Vector3 up = Vector3.Cross(inward, right).normalized;
        Quaternion rot = Quaternion.LookRotation(Vector3.Cross(right, up), up);
        float gap = pm.y - gm;
        if (gap > 0.02f) {
            Floating++;
            WorstFloat = Mathf.Max(WorstFloat, gap);
        } else if (gap < -0.05f) {
            Sunk++;
            WorstSink = Mathf.Min(WorstSink, gap);
        }
        Props++;
        MinDistanceFromCircle = Mathf.Min(MinDistanceFromCircle, new Vector2(pm.x - Circle.x, pm.z - Circle.z).magnitude);
        return Matrix4x4.TRS(pm, rot, Vector3.one);
    }

    void InstanceRun(string name, string id, List<Matrix4x4> poses) {
        if (poses.Count == 0) {
            return;
        }
        Mesh mesh = KitCatalog.MeshOf(id);
        KitDef def = KitCatalog.Def(id);
        Material mat = KitTextures.Get(def.Palette, def.Main);
        var go = new GameObject(name);
        go.transform.SetParent(dress, false);
        go.AddComponent<ArenaScatterDraw>().Set(mesh, mat, UnityEngine.Rendering.ShadowCastingMode.Off, poses.ToArray());
        DrawCalls += Mathf.CeilToInt(poses.Count / 1023f);
    }

    // ---- lamp posts and their pools of light ---------------------------------------------------------------------------

    void BuildLamps(Vector3 dir) {
        var root = new GameObject("Rim Lamps").transform;
        root.SetParent(dress, false);
        // Twelve round the rim, four more flanking the office.
        for (int i = 0; i < 12; i++) {
            float a = (i + 0.5f) * 30f * Mathf.Deg2Rad;
            Vector3 p = Circle + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 178f;
            if (new Vector2(p.x - LandmarkBase.x, p.z - LandmarkBase.z).magnitude < 36f) {
                continue;
            }
            PlaceLamp(root, p);
        }
        Quaternion face = Quaternion.LookRotation(-dir, Vector3.up);
        float[] fx = { -17f, 17f, -33f, 33f };
        for (int i = 0; i < fx.Length; i++) {
            PlaceLamp(root, W(LandmarkBase, face, fx[i], 8f));
        }
        DrawCalls += Combine(root, "Rim Lamps (combined)");
    }

    void PlaceLamp(Transform root, Vector3 p) {
        p.y = LevelDress.GroundY(p.x, p.z, Circle.y);
        Vector3 toCircle = Circle - p;
        toCircle.y = 0f;
        Place(root, "st_lamp_post", p, Quaternion.LookRotation(toCircle.normalized, Vector3.up), 2.4f);
        LampPosts++;
        pools.Add(p + toCircle.normalized * 1.5f);
    }

    // The pools: one mesh, a soft radial gradient laid on the ground by vertex (each vertex raycast to the live ground).
    void BuildPools() {
        if (pools.Count == 0) {
            return;
        }
        const int Rings = 3, Seg = 14;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        foreach (Vector3 c in pools) {
            int baseIndex = verts.Count;
            verts.Add(Ground(c.x, c.z));
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int r = 1; r <= Rings; r++) {
                float rad = 7f * r / Rings;
                for (int s = 0; s < Seg; s++) {
                    float a = s * Mathf.PI * 2f / Seg;
                    float x = Mathf.Sin(a) * rad, z = Mathf.Cos(a) * rad;
                    verts.Add(Ground(c.x + x, c.z + z));
                    uvs.Add(new Vector2(0.5f + x / 14f, 0.5f + z / 14f));
                }
            }
            for (int s = 0; s < Seg; s++) {
                int n = (s + 1) % Seg;
                tris.AddRange(new[] { baseIndex, baseIndex + 1 + s, baseIndex + 1 + n });   // up-facing: clockwise from above
                for (int r = 1; r < Rings; r++) {
                    int a0 = baseIndex + 1 + (r - 1) * Seg, b0 = baseIndex + 1 + r * Seg;
                    tris.AddRange(new[] { a0 + s, b0 + s, b0 + n, a0 + s, b0 + n, a0 + n });
                }
            }
        }
        var mesh = new Mesh { name = "Lamp Pools" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++) {
            for (int x = 0; x < 64; x++) {
                float d = Mathf.Clamp01(new Vector2(x - 31.5f, y - 31.5f).magnitude / 31.5f);
                float v = Mathf.Pow(1f - d, 2f);
                tex.SetPixel(x, y, new Color(1f * v, 0.82f * v, 0.5f * v, v));
            }
        }
        tex.Apply();
        Shader sh = SafeShader.Find("Legacy Shaders/Particles/Additive");
        var mat = new Material(sh);
        mat.mainTexture = tex;
        if (mat.HasProperty("_TintColor")) {
            mat.SetColor("_TintColor", new Color(0.55f, 0.42f, 0.24f, 0.5f));
        } else {
            mat.color = new Color(1f, 0.85f, 0.55f, 0.35f);
        }
        var go = new GameObject("Lamp Pools");
        go.transform.SetParent(dress, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        DrawCalls += 1;
    }

    static Vector3 Ground(float x, float z) {
        return new Vector3(x, LevelDress.GroundY(x, z, 0f) + 0.015f, z);
    }

    // What the playtest reads: the office's middle, up on the clock, for the camera to project.
    public static Vector3 ClockPoint { get { return LandmarkBase + Vector3.up * 19f; } }
}
