using System;
using System.Collections.Generic;
using UnityEngine;

// What the campaign levels (LevelYard) and the chapter maps (EdgeMap, FloorMap) share when they dress a place from the Model
// Kit (Docs/Levels1to3.md): kit pieces placed with a seeded jitter, buildings made of facade bays and roofs, invisible simple
// colliders, heightfield and baked ground meshes tiled with a kit texture, point lights and a gradient sky. Nothing here
// creates a Unity primitive: SceneValidation fails a scene that contains one.
public static class LevelDress {
    static readonly Dictionary<int, Material> tinted = new Dictionary<int, Material>();

    // ---- kit pieces -------------------------------------------------------------------------------------------------

    public static GameObject Put(Transform parent, string id, Vector3 pos, float yaw = 0f, float scale = 1f) {
        GameObject go = KitCatalog.Make(id, parent);
        if (go == null) {
            Debug.LogWarning("LevelDress: no kit piece '" + id + "'");
            return new GameObject("Missing " + id);
        }
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = Vector3.one * scale;
        MeshRenderer r = go.GetComponent<MeshRenderer>();
        if (r != null) {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        return go;
    }

    // A piece squashed to `y` of its height (paving slabs laid flat as floor), uniform in the ground plane.
    public static GameObject PutFlat(Transform parent, string id, Vector3 pos, float yaw, float xz, float y) {
        GameObject go = Put(parent, id, pos, yaw, 1f);
        go.transform.localScale = new Vector3(xz, y, xz);
        return go;
    }

    public static GameObject PutEuler(Transform parent, string id, Vector3 pos, Vector3 euler, float scale = 1f) {
        GameObject go = Put(parent, id, pos, 0f, scale);
        go.transform.rotation = Quaternion.Euler(euler);
        return go;
    }

    // The same piece with one of its submeshes in another colour (the red mark under a press, a red flag): a copy of the kit's
    // shared material per colour, kept in a cache, never a recoloured cube.
    public static void Tint(GameObject piece, int sub, Color color) {
        MeshRenderer r = piece.GetComponent<MeshRenderer>();
        if (r == null || sub >= r.sharedMaterials.Length) {
            return;
        }
        Material[] mats = r.sharedMaterials;
        Material src = mats[sub];
        int key = src.GetInstanceID() * 31 + (int)(color.r * 255f) * 65536 + (int)(color.g * 255f) * 256 + (int)(color.b * 255f);
        Material m;
        if (!tinted.TryGetValue(key, out m) || m == null) {
            m = new Material(src);
            m.SetColor("_Color", color);
            if (src.HasProperty("_EmissionColor") && src.GetColor("_EmissionColor").maxColorComponent > 0.01f) {
                m.SetColor("_EmissionColor", color);   // a lit lens takes its glow from the colour as well
            }
            m.enableInstancing = true;
            tinted[key] = m;
        }
        mats[sub] = m;
        r.sharedMaterials = mats;
    }

    // An invisible simple collider on its own object (one per building, fence run or stone): kit pieces carry none.
    public static GameObject Proxy(Transform parent, string name, Vector3 centre, Vector3 size, float yaw = 0f) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = centre;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.AddComponent<BoxCollider>().size = size;
        return go;
    }

    public static Light PointLight(Transform parent, Vector3 pos, Color color, float range, float intensity) {
        var go = new GameObject("Light");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        return l;
    }

    // ---- buildings --------------------------------------------------------------------------------------------------

    // A building seen from the front: a row of 4 m facade bays standing on `origin` (the middle of the row, on the ground),
    // turned `yaw` (0 faces +Z). `bays` names the ground floor pieces left to right; upper storeys are house walls. Roof bays,
    // chimneys and one side return at each end close it from the sides. The back is never seen, so it is left open.
    // `solid`, when given, receives one box collider for the whole footprint.
    public static GameObject Facade(Transform parent, string name, Vector3 origin, float yaw, string[] bays, int storeys, Transform solid = null, bool flatRoof = false, bool sides = true) {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = origin;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        int n = bays.Length;
        float half = n * 2f;
        for (int i = 0; i < n; i++) {
            float x = -half + 2f + i * 4f;
            for (int s = 0; s < storeys; s++) {
                string id = s == 0 ? bays[i] : "st_house_wall";
                LocalPut(root.transform, id, new Vector3(x, 3f * s, 0f), 0f);
            }
            if (flatRoof) {
                LocalPut(root.transform, "st_roof_flat", new Vector3(x, 3f * storeys, -1.2f), 0f);
            } else {
                LocalPut(root.transform, "st_roof_pitched", new Vector3(x, 3f * storeys, -1.2f), 0f);
                if (i % 2 == 1) {
                    LocalPut(root.transform, "st_chimney", new Vector3(x + 0.8f, 3f * storeys + 0.55f, -1.7f), 0f);
                }
            }
        }
        for (int e = -1; e <= 1 && sides; e += 2) {
            for (int s = 0; s < storeys; s++) {
                LocalPut(root.transform, "st_house_wall", new Vector3(e * half, 3f * s, -2f), e * 90f);
            }
        }
        if (solid != null) {
            Proxy(solid, name + " collider", origin + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 1.5f * storeys, -1.5f), new Vector3(half * 2f, 3f * storeys, 3f), yaw);
        }
        return root;
    }

    static GameObject LocalPut(Transform parent, string id, Vector3 local, float yaw) {
        GameObject go = KitCatalog.Make(id, parent);
        if (go == null) {
            return new GameObject("Missing " + id);
        }
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        MeshRenderer r = go.GetComponent<MeshRenderer>();
        if (r != null) {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        return go;
    }

    // ---- ground -----------------------------------------------------------------------------------------------------

    public static Material GroundMaterial(string palette, string set, Color mul) {
        var m = new Material(KitTextures.Get(palette, set));
        m.SetColor("_Color", KitTextures.Tint(palette, set) * mul);
        m.enableInstancing = true;
        return m;
    }

    // A grid of ground between two corners, lifted by `height`, mapped at `tile` metres to a texture tile, with vertex colour
    // (the kit shader reads it as ambient occlusion, so hollows are darker). Cells whose middle `skip` returns true are left out.
    public static Mesh GridMesh(string name, float x0, float z0, float x1, float z1, float step, Func<float, float, float> height, float tile, Func<float, float, bool> skip = null) {
        int nx = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / step));
        int nz = Mathf.Max(1, Mathf.RoundToInt((z1 - z0) / step));
        float sx = (x1 - x0) / nx, sz = (z1 - z0) / nz;
        var v = new Vector3[(nx + 1) * (nz + 1)];
        var uv = new Vector2[v.Length];
        var col = new Color32[v.Length];
        var h = new float[v.Length];
        for (int j = 0; j <= nz; j++) {
            for (int i = 0; i <= nx; i++) {
                float x = x0 + i * sx, z = z0 + j * sz;
                float y = height(x, z);
                int k = j * (nx + 1) + i;
                h[k] = y;
                v[k] = new Vector3(x, y, z);
                uv[k] = new Vector2(x / tile, z / tile);
                float grain = 0.86f + 0.14f * Mathf.PerlinNoise(x * 0.21f + 7f, z * 0.21f + 3f);
                float low = Mathf.Clamp01(0.82f + y * 0.09f);
                byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(grain * Mathf.Min(1f, low + 0.12f)) * 255f);
                col[k] = new Color32(b, b, b, 255);
            }
        }
        var t = new List<int>();
        for (int j = 0; j < nz; j++) {
            for (int i = 0; i < nx; i++) {
                if (skip != null && skip(x0 + (i + 0.5f) * sx, z0 + (j + 0.5f) * sz)) {
                    continue;
                }
                int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                t.AddRange(new[] { a, c, b, b, c, d });
            }
        }
        var mesh = new Mesh { name = name };
        mesh.indexFormat = v.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(v);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(t, 0);
        mesh.colors32 = col;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    public static GameObject Surface(Transform parent, string name, Mesh mesh, Material mat, bool collider, int layer) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = layer;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (collider) {
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        return go;
    }

    // Ground for a map that plays on the Arcade's bowl: a skin baked from the live ground (a ray down at every grid point), a
    // hand's breadth above it, round with `radius` m. The Arcade's own ground renderer and scatter are hidden by ArenaHider.
    public static GameObject GroundSkin(Transform parent, string name, Vector3 centre, float radius, float step, Material mat, float tile) {
        int groundMask = 1 << 3;
        Func<float, float, float> height = (x, z) => {
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(x, 80f, z), Vector3.down, out hit, 200f, groundMask, QueryTriggerInteraction.Ignore)) {
                return hit.point.y + 0.02f;
            }
            return centre.y + 0.02f;
        };
        float r2 = radius * radius;
        Mesh mesh = GridMesh(name, centre.x - radius, centre.z - radius, centre.x + radius, centre.z + radius, step, height, tile,
            (x, z) => (x - centre.x) * (x - centre.x) + (z - centre.z) * (z - centre.z) > r2);
        return Surface(parent, name, mesh, mat, false, 0);
    }

    // ---- sky --------------------------------------------------------------------------------------------------------

    public static Material Sky(string name, Color top, Color horizon, Color below, Color disc, Vector3 discDir, float discSize, float haze) {
        Shader shader = Shader.Find("Knoll/ChapterSky");
        if (shader == null) {
            return null;
        }
        var m = new Material(shader) { name = name };
        m.SetColor("_Top", top);
        m.SetColor("_Horizon", horizon);
        m.SetColor("_Ground", below);
        m.SetColor("_Disc", disc);
        m.SetVector("_DiscDir", discDir);
        m.SetFloat("_DiscSize", discSize);
        m.SetFloat("_Haze", haze);
        return m;
    }

    // ---- small helpers ----------------------------------------------------------------------------------------------

    public static float Rand(System.Random rng, float a, float b) {
        return a + (float)rng.NextDouble() * (b - a);
    }
}

// The Arcade's bowl is the ground of the chapter maps. While a map is up its meadow (the Arena's ground renderer, plaza and
// scatter of grass, trees and rocks) is hidden, so the map's own baked ground and its dressing are all that shows; the ground's
// collider stays, and everything comes back when the map goes.
public class ArenaHider : MonoBehaviour {
    readonly List<Renderer> hidden = new List<Renderer>();

    public int Hidden { get { return hidden.Count; } }

    void Awake() {
        GameObject arena = GameObject.Find("Arena");
        if (arena == null) {
            return;
        }
        foreach (Renderer r in arena.GetComponentsInChildren<Renderer>(false)) {
            if (r.enabled) {
                r.enabled = false;
                hidden.Add(r);
            }
        }
    }

    void OnDestroy() {
        foreach (Renderer r in hidden) {
            if (r != null) {
                r.enabled = true;
            }
        }
        hidden.Clear();
    }
}
