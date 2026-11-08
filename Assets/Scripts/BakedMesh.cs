using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Gen-made meshes baked offline by Tools/Bake/glb_bake.py (GLB -> quadric decimation -> rigid split) and kept as text in
// Resources/Baked/<id>.mesh.txt, so the game never needs Blender. Props and lodgers replace the code-built kit piece of the
// same id (KitCatalog.MeshOf), bosses replace the code-built body (CharacterKit.Make). Anything missing or unreadable
// returns null and the caller keeps the code version, so a bad file can never break a fight.
public static class BakedMesh {
    public static bool Enabled = true;

    public class Part {
        public string Name, Set = "metal";
        public Vector3 Pivot;
        public Color Tint = Color.white;
        public readonly List<Vector3> Pos = new List<Vector3>(), Nrm = new List<Vector3>();
        public readonly List<Vector2> Uv = new List<Vector2>();
        public readonly List<int> Tris = new List<int>();

        public Mesh ToMesh(string meshName) {
            var m = new Mesh { name = meshName };
            m.SetVertices(Pos);
            m.SetNormals(Nrm);
            m.SetUVs(0, Uv);
            m.SetTriangles(Tris, 0);
            m.RecalculateBounds();
            return m;
        }
    }

    static readonly Dictionary<string, List<Part>> cache = new Dictionary<string, List<Part>>();

    public static bool Has(string id) {
        return Enabled && Load(id) != null;
    }

    public static List<Part> Load(string id) {
        if (!Enabled) {
            return null;
        }
        List<Part> list;
        if (cache.TryGetValue(id, out list)) {
            return list;
        }
        list = null;
        TextAsset ta = Resources.Load<TextAsset>("Baked/" + id + ".mesh");
        if (ta != null) {
            try {
                list = Parse(ta.text);
            } catch (System.Exception e) {
                Debug.LogWarning("[Baked] " + id + " unreadable, using the code version: " + e.Message);
                list = null;
            }
        }
        cache[id] = list;
        return list;
    }

    static float F(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }

    static List<Part> Parse(string text) {
        var parts = new List<Part>();
        Part cur = null;
        foreach (string raw in text.Split('\n')) {
            string line = raw.Trim();
            if (line.Length == 0) {
                continue;
            }
            string[] w = line.Split(' ');
            switch (w[0]) {
                case "g":
                    cur = new Part { Name = w[1].Replace('_', ' '), Pivot = new Vector3(F(w[2]), F(w[3]), F(w[4])) };
                    if (w.Length > 5) { cur.Set = w[5]; }
                    Color c;
                    if (w.Length > 6 && ColorUtility.TryParseHtmlString(w[6], out c)) { cur.Tint = c; }
                    parts.Add(cur);
                    break;
                case "v":
                    cur.Pos.Add(new Vector3(F(w[1]), F(w[2]), F(w[3])));
                    cur.Nrm.Add(new Vector3(F(w[4]), F(w[5]), F(w[6])));
                    cur.Uv.Add(new Vector2(F(w[7]), F(w[8])));
                    break;
                case "t":
                    int a = int.Parse(w[1]), b = int.Parse(w[2]), d = int.Parse(w[3]);
                    if (a >= cur.Pos.Count || b >= cur.Pos.Count || d >= cur.Pos.Count) {
                        throw new System.Exception("triangle index out of range");
                    }
                    cur.Tris.Add(a); cur.Tris.Add(b); cur.Tris.Add(d);
                    break;
            }
        }
        if (parts.Count == 0) {
            throw new System.Exception("no parts");
        }
        return parts;
    }

    // A single-part prop mesh named like the kit piece (the validator wants name == id).
    public static Mesh Prop(string id) {
        List<Part> p = Load(id);
        return p == null ? null : p[0].ToMesh(id);
    }
}
