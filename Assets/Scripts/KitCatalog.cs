using System;
using System.Collections.Generic;
using UnityEngine;

// The Model Kit (Docs/ModelKit.md): every piece, by id, with the code that builds its mesh. The runtime builds pieces from
// here (KitCatalog.Make), and Tools > Kit > Build Kit Assets bakes the same meshes, materials and prefabs into Assets/Kit.
// Grid: 0.5 m units. Pieces stand on y = 0, face +Z, and pivot at the bottom centre of their footprint.
public class KitDef {
    public string Id, Palette, Main, Accent;
    public Action<KitMesh> Build;
    public string Reason = "";     // why a piece may have fewer than 24 vertices (none do)
}

public static partial class KitCatalog {
    public const float Grid = 0.5f;

    public static readonly List<KitDef> All = new List<KitDef>();
    static readonly Dictionary<string, KitDef> byId = new Dictionary<string, KitDef>();
    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    static bool built;

    public static readonly string[] Palettes = { "street", "office", "yard", "gate", "floor", "walk", "reading", "filed", "arena" };

    static void Add(string id, string palette, string main, string accent, Action<KitMesh> build) {
        var d = new KitDef { Id = id, Palette = palette, Main = main, Accent = accent, Build = build };
        All.Add(d);
        byId[id] = d;
    }

    public static void Init() {
        if (built) {
            return;
        }
        built = true;
        AddStreet();
        AddOffice();
        AddYard();
        AddGate();
        AddFloor();
        AddWalk();
        AddReading();
        AddFiled();
        AddArena();
        AddLevels();
    }

    public static KitDef Def(string id) { Init(); KitDef d; return byId.TryGetValue(id, out d) ? d : null; }

    public static List<KitDef> OfPalette(string palette) {
        Init();
        var list = new List<KitDef>();
        foreach (KitDef d in All) {
            if (d.Palette == palette) {
                list.Add(d);
            }
        }
        return list;
    }

    public static Mesh MeshOf(string id) {
        Init();
        Mesh m;
        if (meshes.TryGetValue(id, out m) && m != null) {
            return m;
        }
        KitDef d = byId[id];
        var km = new KitMesh { Name = id };
        km.Tex = 1f;
        d.Build(km);
        km.Reset();
        m = km.ToMesh();
        meshes[id] = m;
        return m;
    }

    public static Material[] MaterialsOf(KitDef d, Mesh mesh) {
        var list = new List<Material> { KitTextures.Get(d.Palette, d.Main) };
        if (mesh.subMeshCount > 1) {
            list.Add(KitTextures.Get(d.Palette, d.Accent));
        }
        return list.ToArray();
    }

    // A piece as a GameObject (no colliders). Place it at grid positions; it stands on y = 0 of its parent.
    public static GameObject Make(string id, Transform parent = null) {
        KitDef d = Def(id);
        if (d == null) {
            return null;
        }
        Mesh mesh = MeshOf(id);
        var go = new GameObject(id);
        if (parent != null) {
            go.transform.SetParent(parent, false);
        }
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = MaterialsOf(d, mesh);
        var piece = go.AddComponent<KitPiece>();
        piece.Id = id;
        piece.Palette = d.Palette;
        Vector3 size = mesh.bounds.size;
        piece.Footprint = new Vector2Int(Mathf.CeilToInt(size.x / Grid - 0.01f), Mathf.CeilToInt(size.z / Grid - 0.01f));
        return go;
    }

    // ---- small helpers for the piece code --------------------------------------------------------------------------

    static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }
    static void B(KitMesh m, float x0, float y0, float z0, float x1, float y1, float z1, float ch = 0f) { m.Box(V(x0, y0, z0), V(x1, y1, z1), ch); }
    static void BC(KitMesh m, float cx, float cy, float cz, float sx, float sy, float sz, float ch = 0f) { m.BoxC(V(cx, cy, cz), V(sx, sy, sz), ch); }
    static List<Vector2> P(params float[] v) {
        var list = new List<Vector2>();
        for (int i = 0; i + 1 < v.Length; i += 2) {
            list.Add(new Vector2(v[i], v[i + 1]));
        }
        return list;
    }
    static void Lat(KitMesh m, Vector3 at, float yaw, List<Vector2> prof, int seg = 12, bool smooth = true) {
        m.At(at, yaw);
        m.Lathe(prof, seg, smooth);
        m.Reset();
    }
    // A box rotated about the world axes, centred at c.
    static void BR(KitMesh m, Vector3 c, Vector3 euler, Vector3 size, float ch = 0f) {
        m.Orient(c, euler);
        m.BoxC(Vector3.zero, size, ch);
        m.Reset();
    }
    static void S0(KitMesh m) { m.Sub = 0; }
    static void S1(KitMesh m) { m.Sub = 1; }

    // Four legs under a rectangle.
    static void Legs(KitMesh m, float x0, float x1, float z0, float z1, float h, float w, float ch = 0.005f) {
        B(m, x0, 0, z0, x0 + w, h, z0 + w, ch);
        B(m, x1 - w, 0, z0, x1, h, z0 + w, ch);
        B(m, x0, 0, z1 - w, x0 + w, h, z1, ch);
        B(m, x1 - w, 0, z1 - w, x1, h, z1, ch);
    }

    // A row of drawer fronts on the +Z face of a carcass, each with a pull on the accent submesh.
    static void Drawers(KitMesh m, float x0, float x1, float y0, float y1, float z, int rows, float gap = 0.015f, bool pulls = true) {
        float h = (y1 - y0) / rows;
        for (int i = 0; i < rows; i++) {
            float a = y0 + i * h + gap, b = y0 + (i + 1) * h - gap;
            m.Sub = 0;
            B(m, x0 + gap, a, z, x1 - gap, b, z + 0.025f, 0.006f);
            if (pulls) {
                m.Sub = 1;
                float cx = (x0 + x1) * 0.5f, cy = (a + b) * 0.5f;
                B(m, cx - 0.06f, cy - 0.012f, z + 0.025f, cx + 0.06f, cy + 0.012f, z + 0.05f, 0.004f);
            }
        }
        m.Sub = 0;
    }

    static void Ring(KitMesh m, Vector3 centre, float radius, int count, Vector3 size, float ch = 0f) {
        for (int i = 0; i < count; i++) {
            float a = 360f * i / count;
            float rad = a * Mathf.Deg2Rad;
            Vector3 p = centre + V(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
            BR(m, p, V(0f, -a + 90f, 0f), size, ch);
        }
    }

    // ---- STREET -----------------------------------------------------------------------------------------------------

    static void AddStreet() {
        Add("st_shopfront", "street", "brick", "glass", m => {
            var win = new KitMesh.Opening(-1.8f, 0.4f, 0.9f, 2.5f);
            var door = new KitMesh.Opening(0.9f, 1.9f, 0f, 2.3f);
            m.Wall(4f, 3f, 0.3f, new[] { win, door });
            m.Frame(win, 0.3f, 0.12f, 0.07f, 0.12f);
            m.Frame(door, 0.3f, 0.1f, 0.07f, 0f);
            m.Pane(win);
            m.Mullions(win, 2, 2, 0.05f);
            m.DoorLeaf(door, -0.02f);
            B(m, -2.08f, 2.72f, -0.2f, 2.08f, 3.04f, 0.26f, 0.025f);    // cornice
            B(m, -2.04f, 0f, 0.15f, 0.9f, 0.14f, 0.2f, 0.01f);          // stall riser plinth
            B(m, 0.8f, 0f, 0.15f, 2f, 0.1f, 0.55f, 0.015f);             // doorstep
        });
        Add("st_awning", "street", "cloth", "iron", m => {
            for (int i = 0; i < 8; i++) {
                float x = -1.96f + i * 0.5f;
                float lift = (i % 2) * 0.012f;
                BR(m, V(x + 0.25f, 2.55f + lift, 0.45f), V(24f, 0f, 0f), V(0.5f, 0.035f, 1.0f), 0.004f);
                m.Face(Vector3.forward, new[] { V(x + 0.01f, 2.12f, 0.93f), V(x + 0.49f, 2.12f, 0.93f), V(x + 0.25f, 1.9f, 0.96f) });
                m.Face(Vector3.back, new[] { V(x + 0.01f, 2.12f, 0.93f), V(x + 0.49f, 2.12f, 0.93f), V(x + 0.25f, 1.9f, 0.96f) });
            }
            S1(m);
            for (int s = -1; s <= 1; s += 2) {
                BR(m, V(s * 1.9f, 2.35f, 0.4f), V(-45f, 0f, 0f), V(0.05f, 0.05f, 1.0f), 0.005f);
                B(m, s * 1.9f - 0.04f, 2.3f, -0.04f, s * 1.9f + 0.04f, 2.8f, 0.02f, 0.005f);
            }
            B(m, -2f, 2.6f, -0.04f, 2f, 2.66f, 0.04f, 0.005f);
        });
        Add("st_lamp_post", "street", "iron", "lamp", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .22f, 0, .22f, .1f, .15f, .2f, .15f, .4f, .1f, .5f, .07f, .55f, .07f, 1.45f, .1f, 1.5f, .1f, 1.55f, .07f, 1.6f, .07f, 3.2f, .1f, 3.25f, .1f, 3.3f, 0, 3.3f), 12);
            BR(m, V(0.2f, 3.3f, 0), V(0, 0, 40f), V(0.5f, 0.05f, 0.05f), 0.005f);
            BR(m, V(0.5f, 3.42f, 0), V(0, 0, 10f), V(0.4f, 0.05f, 0.05f), 0.005f);
            Lat(m, V(0.62f, 3.3f, 0), 0f, P(0, 0, .1f, 0, .12f, .04f, .12f, .05f, .1f, .08f, 0, .08f), 8);
            Lat(m, V(0.62f, 3.55f, 0), 0f, P(0, 0, .15f, 0, .13f, .05f, .05f, .12f, 0, .14f), 8);
            for (int i = 0; i < 4; i++) {
                float a = i * 90f + 45f;
                BR(m, V(0.62f + Mathf.Cos(a * Mathf.Deg2Rad) * 0.11f, 3.43f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.11f), V(0, 0, 0), V(0.015f, 0.25f, 0.015f));
            }
            S1(m);
            Lat(m, V(0.62f, 3.34f, 0), 0f, P(0, 0, .08f, 0, .1f, .08f, .08f, .18f, 0, .2f), 8);
        });
        Add("st_bench", "street", "wood", "iron", m => {
            for (int i = 0; i < 4; i++) {
                B(m, -0.9f, 0.44f, -0.2f + i * 0.1f, 0.9f, 0.48f, -0.12f + i * 0.1f, 0.006f);
            }
            for (int i = 0; i < 3; i++) {
                m.Orient(V(0, 0.5f, -0.24f), V(-12f, 0, 0));
                m.Box(V(-0.9f, 0.08f + i * 0.13f, 0), V(0.9f, 0.18f + i * 0.13f, 0.035f), 0.006f);
                m.Reset();
            }
            S1(m);
            for (int s = -1; s <= 1; s += 2) {
                float x = s * 0.82f;
                B(m, x - 0.03f, 0f, -0.24f, x + 0.03f, 0.46f, -0.18f, 0.005f);
                B(m, x - 0.03f, 0f, 0.14f, x + 0.03f, 0.46f, 0.2f, 0.005f);
                BR(m, V(x, 0.5f, -0.25f), V(-12f, 0, 0), V(0.06f, 0.75f, 0.05f), 0.005f);
                B(m, x - 0.035f, 0.64f, -0.28f, x + 0.035f, 0.68f, 0.24f, 0.006f);
                m.Orient(V(x, 0.28f, 0.0f), V(0, 0, 90f));
                m.Cyl(0.1f, -0.025f, 0.025f, 10, 0.008f);
                m.Reset();
                S1(m);
            }
        });
        Add("st_bin", "street", "metal", "iron", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .26f, 0, .27f, .05f, .3f, .28f, .32f, .3f, .32f, .34f, .31f, .36f, .33f, .62f, .35f, .64f, .35f, .68f, .34f, .7f, .35f, .85f, .37f, .9f, .37f, .96f, .35f, .98f, 0, .98f), 12);
            S1(m);
            Lat(m, V(0, 0, 0), 0f, P(0, .98f, .38f, .98f, .38f, 1.02f, .3f, 1.1f, .18f, 1.15f, .05f, 1.18f, .05f, 1.22f, 0, 1.22f), 12);
            B(m, .35f, .6f, -.04f, .43f, .66f, .04f, .004f);
            B(m, -.43f, .6f, -.04f, -.35f, .66f, .04f, .004f);
            B(m, -.1f, 1.18f, -.02f, .1f, 1.22f, .02f, .004f);
        });
        Add("st_crate", "street", "wood", "iron", m => {
            B(m, -.38f, .06f, -.38f, .38f, .8f, .38f);
            for (int k = 0; k < 4; k++) {
                float y0 = .08f + k * .18f;
                B(m, -.46f, y0, -.42f, .46f, y0 + .15f, -.38f, .006f);
                B(m, -.46f, y0, .38f, .46f, y0 + .15f, .42f, .006f);
                B(m, -.42f, y0, -.46f, -.38f, y0 + .15f, .46f, .006f);
                B(m, .38f, y0, -.46f, .42f, y0 + .15f, .46f, .006f);
            }
            B(m, -.5f, 0f, -.5f, .5f, .06f, .5f, .01f);
            B(m, -.5f, .8f, -.5f, .5f, .88f, .5f, .015f);
            BR(m, V(0, .46f, .43f), V(0, 0, 38f), V(.9f, .06f, .03f), .004f);
            S1(m);
            for (int sx = -1; sx <= 1; sx += 2) {
                for (int sz = -1; sz <= 1; sz += 2) {
                    B(m, sx * .47f - .03f, .06f, sz * .47f - .03f, sx * .47f + .03f, .82f, sz * .47f + .03f, .005f);
                }
            }
        });
        Add("st_hydrant", "street", "metal", "iron", m => {
            Lat(m, V(0, 0, 0), 0f, P(0, 0, .2f, 0, .2f, .06f, .14f, .1f, .12f, .14f, .12f, .5f, .14f, .54f, .14f, .6f, .13f, .64f, .1f, .72f, .06f, .78f, 0, .8f), 12);
            for (int s = -1; s <= 1; s += 2) {
                m.Orient(V(s * .1f, .42f, 0), V(0, 0, -90f * s));
                m.Lathe(P(0, 0, .06f, 0, .06f, .1f, .08f, .12f, .08f, .16f, .05f, .18f, 0, .18f), 8);
                m.Reset();
            }
            S1(m);
            Lat(m, V(0, .78f, 0), 0f, P(0, 0, .05f, 0, .05f, .06f, .03f, .09f, 0, .1f), 8);
        });
        Add("st_house_wall", "street", "plaster", "glass", m => {
            var w1 = new KitMesh.Opening(-1.4f, -0.5f, 0.9f, 2.3f);
            var w2 = new KitMesh.Opening(0.5f, 1.4f, 0.9f, 2.3f);
            m.Wall(4f, 3f, 0.3f, new[] { w1, w2 });
            m.Frame(w1, 0.3f, 0.1f, 0.06f, 0.1f);
            m.Frame(w2, 0.3f, 0.1f, 0.06f, 0.1f);
            m.Pane(w1);
            m.Pane(w2);
            m.Mullions(w1, 2, 3, 0.04f);
            m.Mullions(w2, 2, 3, 0.04f);
            B(m, -2.04f, 0f, 0.14f, 2.04f, 0.4f, 0.2f, 0.012f);        // plinth
            B(m, -2.04f, 2.58f, 0.14f, 2.04f, 2.66f, 0.2f, 0.01f);     // string course
            B(m, -2.08f, 2.82f, -0.2f, 2.08f, 3.04f, 0.26f, 0.02f);    // eaves cornice
            B(m, -2.02f, 0f, -0.15f, -1.92f, 3f, 0.18f, 0.01f);        // quoins
            B(m, 1.92f, 0f, -0.15f, 2.02f, 3f, 0.18f, 0.01f);
        });
        Add("st_door_wall", "street", "plaster", "wood", m => {
            var door = new KitMesh.Opening(-0.5f, 0.5f, 0f, 2.2f);
            var transom = new KitMesh.Opening(-0.5f, 0.5f, 2.32f, 2.8f);
            m.Wall(4f, 3f, 0.3f, new[] { door, transom });
            S1(m);
            m.Frame(door, 0.3f, 0.12f, 0.07f, 0f);
            m.Frame(transom, 0.3f, 0.08f, 0.05f, 0f);
            m.DoorLeaf(door, -0.02f);
            m.Mullions(transom, 3, 1, 0.04f);
            B(m, -0.7f, 2.9f, 0.1f, 0.7f, 3f, 0.32f, 0.015f);          // hood
            B(m, -0.8f, 0f, 0.15f, 0.8f, 0.14f, 0.7f, 0.015f);         // step
            B(m, -0.9f, 0f, 0.14f, -0.72f, 2.8f, 0.24f, 0.01f);        // pilasters
            B(m, 0.72f, 0f, 0.14f, 0.9f, 2.8f, 0.24f, 0.01f);
            S0(m);
            B(m, -2.04f, 0f, 0.14f, 2.04f, 0.4f, 0.2f, 0.012f);
            B(m, -2.08f, 2.82f, -0.2f, 2.08f, 3.04f, 0.26f, 0.02f);
            m.Sub = 1;
            m.Pane(transom);
            S0(m);
        });
        Add("st_roof_pitched", "street", "slate", "iron", m => {
            float ang = Mathf.Atan2(1.2f, 2.2f) * Mathf.Rad2Deg, len = Mathf.Sqrt(2.2f * 2.2f + 1.2f * 1.2f);
            for (int s = -1; s <= 1; s += 2) {
                BR(m, V(s * 1.1f, 0.6f, 0), V(0, 0, -s * ang), V(len, 0.1f, 3.2f), 0.012f);
            }
            // slate courses laid on each slope
            for (int s = -1; s <= 1; s += 2) {
                for (int c = 0; c < 6; c++) {
                    float t = 0.22f + c * 0.36f;
                    float x = s * (2.2f - t * Mathf.Cos(ang * Mathf.Deg2Rad));
                    float y = 0.0f + t * Mathf.Sin(ang * Mathf.Deg2Rad) + 0.07f;
                    BR(m, V(x, y, 0), V(0, 0, -s * ang), V(0.28f, 0.035f, 3.24f), 0.006f);
                }
            }
            BR(m, V(0, 1.2f, 0), V(0, 0, 45f), V(0.22f, 0.22f, 3.3f), 0.01f);   // ridge cap
            m.ExtrudeZ(new List<Vector2> { new Vector2(-2f, 0f), new Vector2(2f, 0f), new Vector2(0f, 1.05f) }, 1.45f, 1.55f);
            m.ExtrudeZ(new List<Vector2> { new Vector2(-2f, 0f), new Vector2(2f, 0f), new Vector2(0f, 1.05f) }, -1.55f, -1.45f);
            S1(m);
            B(m, -2.3f, -0.05f, 1.62f, 2.3f, 0.05f, 1.7f, 0.006f);     // fascia boards
            B(m, -2.3f, -0.05f, -1.7f, 2.3f, 0.05f, -1.62f, 0.006f);
        });
        Add("st_roof_flat", "street", "concrete", "iron", m => {
            B(m, -2f, 0f, -1.5f, 2f, 0.18f, 1.5f, 0.02f);
            B(m, -2f, 0.18f, -1.5f, 2f, 0.55f, -1.38f, 0.015f);
            B(m, -2f, 0.18f, 1.38f, 2f, 0.55f, 1.5f, 0.015f);
            B(m, -2f, 0.18f, -1.38f, -1.88f, 0.55f, 1.38f, 0.015f);
            B(m, 1.88f, 0.18f, -1.38f, 2f, 0.55f, 1.38f, 0.015f);
            B(m, -2.06f, 0.55f, -1.56f, 2.06f, 0.62f, -1.32f, 0.012f);   // coping
            B(m, -2.06f, 0.55f, 1.32f, 2.06f, 0.62f, 1.56f, 0.012f);
            B(m, -2.06f, 0.55f, -1.32f, -1.82f, 0.62f, 1.32f, 0.012f);
            B(m, 1.82f, 0.55f, -1.32f, 2.06f, 0.62f, 1.32f, 0.012f);
            B(m, 0.4f, 0.18f, -0.7f, 1.4f, 0.55f, 0.2f, 0.03f);        // hatch
            BR(m, V(0.9f, 0.6f, 0.25f), V(-14f, 0, 0), V(1.05f, 0.06f, 0.9f), 0.01f);
            S1(m);
            Lat(m, V(-1.2f, 0.18f, -0.5f), 0f, P(0, 0, .12f, 0, .12f, .08f, .08f, .1f, .08f, .9f, .12f, .94f, 0, .94f), 8);
        });
        Add("st_chimney", "street", "brick", "iron", m => {
            B(m, -.4f, 0f, -.4f, .4f, .5f, .4f, .01f);
            B(m, -.33f, .5f, -.33f, .33f, 1.5f, .33f, .008f);
            B(m, -.37f, 1.5f, -.37f, .37f, 1.58f, .37f, .01f);
            B(m, -.4f, 1.58f, -.4f, .4f, 1.66f, .4f, .01f);
            B(m, -.3f, 1.66f, -.3f, .3f, 1.74f, .3f, .008f);
            for (int i = 0; i < 4; i++) {
                B(m, -.35f, .6f + i * .22f, -.355f, .35f, .62f + i * .22f, .355f, .002f);
            }
            S1(m);
            Lat(m, V(0, 1.74f, 0), 0f, P(0, 0, .2f, 0, .2f, .06f, .16f, .1f, .14f, .3f, .17f, .34f, 0, .34f), 8);
        });
        Add("st_kerb", "street", "concrete", "iron", m => {
            for (int i = 0; i < 3; i++) {
                float x0 = -1.5f + i * 1.02f;
                B(m, x0, 0f, 0.55f, x0 + 0.98f, 0.18f, 0.8f, 0.03f);
            }
            B(m, -1.5f, 0f, -0.25f, 0f, 0.07f, 0.52f, 0.015f);
            B(m, 0.02f, 0f, -0.25f, 1.52f, 0.07f, 0.52f, 0.015f);
            B(m, -1.5f, 0f, -0.55f, 1.52f, 0.05f, -0.27f, 0.01f);
            S1(m);
            for (int i = 0; i < 5; i++) {
                B(m, 0.9f, 0.065f, -0.1f + i * 0.07f, 1.1f, 0.08f, -0.07f + i * 0.07f, 0.001f);
            }
        });
    }
}
