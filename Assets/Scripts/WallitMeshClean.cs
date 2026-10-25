using System.Collections.Generic;
using UnityEngine;

// Wallit.fbx (a Gen bake) holds the whole character twice: two near-identical shells (about 6.5k and 6.1k triangles)
// interleaved with each other, plus the bill fan. Where they cross they z-fight, which reads as a white sliver down
// the legs and torso. This drops the smaller duplicate shell from Wallit's body mesh at load, in code, so the FBX
// itself is never touched. Tools/Wallit/wallit_check.py reports the same shells from the file.
public static class WallitMeshClean {
    public static int LastDropped;
    public static int LastShells;

    public static void Apply(GameObject wallit) {
        LastDropped = 0;
        LastShells = 0;
        if (wallit == null) {
            return;
        }
        foreach (SkinnedMeshRenderer smr in wallit.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
            if (smr == null || smr.sharedMesh == null || smr.gameObject.name.ToLowerInvariant().Contains("bat")) {
                continue;
            }
            Mesh source = smr.sharedMesh;
            if (source.name.EndsWith("(clean)") || source.subMeshCount != 1 || !source.isReadable) {
                continue;
            }
            Mesh clean = Clean(source);
            if (clean != null) {
                smr.sharedMesh = clean;
            }
        }
    }

    static Mesh Clean(Mesh source) {
        Vector3[] verts = source.vertices;
        int[] tris = source.GetTriangles(0);
        int triCount = tris.Length / 3;
        if (triCount < 8000) {
            return null;
        }

        // Weld by position (UV and normal seams split vertices) and group triangles into shells.
        float cell = Mathf.Max(source.bounds.size.magnitude * 1e-4f, 1e-6f);
        var weld = new Dictionary<Vector3Int, int>();
        int[] group = new int[verts.Length];
        for (int i = 0; i < verts.Length; i++) {
            var key = new Vector3Int(Mathf.RoundToInt(verts[i].x / cell), Mathf.RoundToInt(verts[i].y / cell), Mathf.RoundToInt(verts[i].z / cell));
            int id;
            if (!weld.TryGetValue(key, out id)) {
                id = weld.Count;
                weld[key] = id;
            }
            group[i] = id;
        }
        int[] parent = new int[weld.Count];
        for (int i = 0; i < parent.Length; i++) {
            parent[i] = i;
        }
        for (int t = 0; t < triCount; t++) {
            Union(parent, group[tris[t * 3]], group[tris[t * 3 + 1]]);
            Union(parent, group[tris[t * 3]], group[tris[t * 3 + 2]]);
        }
        var shellTris = new Dictionary<int, int>();
        for (int t = 0; t < triCount; t++) {
            int root = Find(parent, group[tris[t * 3]]);
            int n;
            shellTris.TryGetValue(root, out n);
            shellTris[root] = n + 1;
        }
        var roots = new List<int>(shellTris.Keys);
        roots.Sort((a, b) => shellTris[b].CompareTo(shellTris[a]));
        LastShells = roots.Count;

        // Two big shells of nearly the same size are one character drawn twice: keep the larger.
        if (roots.Count < 2 || shellTris[roots[1]] < 3000 || shellTris[roots[1]] < shellTris[roots[0]] * 0.6f) {
            return null;
        }
        int drop = roots[1];
        var kept = new List<int>(tris.Length);
        for (int t = 0; t < triCount; t++) {
            if (Find(parent, group[tris[t * 3]]) == drop) {
                continue;
            }
            kept.Add(tris[t * 3]);
            kept.Add(tris[t * 3 + 1]);
            kept.Add(tris[t * 3 + 2]);
        }

        Mesh clean = Object.Instantiate(source);
        clean.name = source.name + " (clean)";
        clean.SetTriangles(kept, 0, false);
        clean.bounds = source.bounds;
        LastDropped = shellTris[drop];
        return clean;
    }

    static int Find(int[] parent, int a) {
        while (parent[a] != a) {
            parent[a] = parent[parent[a]];
            a = parent[a];
        }
        return a;
    }

    static void Union(int[] parent, int a, int b) {
        a = Find(parent, a);
        b = Find(parent, b);
        if (a != b) {
            parent[b] = a;
        }
    }
}
