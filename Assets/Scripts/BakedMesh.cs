using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// Gen-made meshes baked offline by Tools/Bake/glb_bake.py (GLB -> quadric decimation -> rigid split) and kept as text in
// Resources/Baked/<id>.mesh.txt, so the game never needs Blender. Props and lodgers replace the code-built kit piece of the
// same id (KitCatalog.MeshOf), bosses replace the code-built body (CharacterKit.Make). Anything missing or unreadable
// returns null and the caller keeps the code version, so a bad file can never break a fight.
public static class BakedMesh {
    public static bool Enabled = true;

    // Bad normals the baked files held, and the meshes they were in, mended so far this session (chapter2-hud reports them:
    // that is the NaN source the playtest could not name, proved present and removed).
    public static int MendedVertices;
    public static int MendedMeshes;
    // Parts found inside out at load and turned outward (should stay 0 for the shipped files).
    public static int FlippedParts;

    public class Part {
        public string Name, Set = "metal", Parent = "";
        public Vector3 Pivot;
        public Color Tint = Color.white;
        public readonly List<Vector3> Pos = new List<Vector3>(), Nrm = new List<Vector3>();
        public readonly List<Vector2> Uv = new List<Vector2>();
        public readonly List<int> Tris = new List<int>();

        public Mesh ToMesh(string meshName) {
            var m = new Mesh { name = meshName };
            List<Vector3> normals = MendedNormals();
            PointOutward(normals);
            m.SetVertices(Pos);
            m.SetNormals(normals);
            if (Mended > 0) {
                MendedVertices += Mended;
                MendedMeshes++;
            }
            m.SetUVs(0, Uv);
            m.SetTriangles(Tris, 0);
            m.RecalculateBounds();
            return m;
        }

        // Normals the file lists that a shader cannot use: (0,0,0) (the bake tool writes it where a vertex's faces cancel out,
        // as on thin double-sided Gen geometry or where decimation folded two faces together), not finite, or far from unit.
        // KitLit carries a normal through the tangent-space transform and normalises it, so a zero one turns into NaN, and
        // a single NaN pixel in the HDR frame is what bloom smeared into J's black squares on Chapter 2's gate plaza (the
        // booth, turnstiles and arch had 7 to 20 of them each). Such a vertex gets the normal of the biggest face that uses
        // it, or up when it has none. A good normal is passed on untouched.
        public List<Vector3> MendedNormals() {
            int n = Pos.Count;
            var faceNormal = new Vector3[n];
            var faceArea = new float[n];
            for (int i = 0; i + 2 < Tris.Count; i += 3) {
                int a = Tris[i], b = Tris[i + 1], c = Tris[i + 2];
                Vector3 cross = Vector3.Cross(Pos[b] - Pos[a], Pos[c] - Pos[a]);
                float area = cross.magnitude;
                if (!(area > 1e-12f)) {   // a flat sliver, or NaN: written so NaN fails it too
                    continue;
                }
                cross /= area;
                int[] corner = { a, b, c };
                for (int k = 0; k < 3; k++) {
                    if (area > faceArea[corner[k]]) {
                        faceArea[corner[k]] = area;
                        faceNormal[corner[k]] = cross;
                    }
                }
            }
            var result = new List<Vector3>(n);
            Mended = 0;
            for (int i = 0; i < n; i++) {
                Vector3 v = i < Nrm.Count ? Nrm[i] : Vector3.zero;
                float sq = v.sqrMagnitude;
                if (sq > 0.25f && sq < 4f) {   // roughly unit; NaN and zero fail both tests
                    result.Add(v);
                    continue;
                }
                Mended++;
                result.Add(faceArea[i] > 0f ? faceNormal[i] : Vector3.up);
            }
            return result;
        }

        // The one load step that points a bake outward: a closed part whose triangles sum to a negative signed volume (clockwise
        // front faces, so outward is positive) is inside out, from a mirrored export or a flipped axis. It gets its winding
        // reversed and its normals negated here, so no negative scale or two-sided material is ever needed. Counted in Flipped.
        void PointOutward(List<Vector3> normals) {
            double volume = 0;
            Vector3 c = Vector3.zero;
            foreach (Vector3 p in Pos) c += p;
            if (Pos.Count > 0) c /= Pos.Count;
            for (int i = 0; i + 2 < Tris.Count; i += 3) {
                Vector3 a = Pos[Tris[i]] - c, b = Pos[Tris[i + 1]] - c, d = Pos[Tris[i + 2]] - c;
                volume += Vector3.Dot(a, Vector3.Cross(b - a, d - a)) / 6.0;
            }
            if (!(volume < -1e-6)) return;
            for (int i = 0; i + 2 < Tris.Count; i += 3) {
                int t = Tris[i + 1]; Tris[i + 1] = Tris[i + 2]; Tris[i + 2] = t;
            }
            for (int i = 0; i < normals.Count; i++) normals[i] = -normals[i];
            FlippedParts++;
        }

        // How many normals the last ToMesh had to replace (the playtests read it).
        public int Mended;
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
        string meshText = OnDemand.Text("Baked/" + id + ".mesh");
        if (meshText != null) {
            try {
                list = Parse(meshText);
            } catch (System.Exception e) {
                Debug.LogWarning("[Baked] " + id + " unreadable, using the code version: " + e.Message);
                list = null;
            }
        }
        if (list != null || !OnDemand.Indexed("Baked/" + id + ".mesh")) {
            cache[id] = list;   // a mesh still to be fetched keeps the code version for now and is asked for again
        }
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
                    if (w.Length > 7) { cur.Parent = w[7].Replace('_', ' '); }
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
