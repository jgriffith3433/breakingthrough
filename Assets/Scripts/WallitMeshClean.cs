using System.Collections.Generic;
using UnityEngine;

// Wallit.fbx (a Gen bake) holds the whole character twice: two near-identical shells (about 6.5k and 6.1k triangles)
// interleaved with each other, plus the bill fan. Where they cross they z-fight, which reads as a white sliver down
// the legs and torso. This drops the smaller duplicate shell from Wallit's body mesh at load, softens the hard skin-weight
// lines that drag the back into spikes, and welds normals across UV seams, in code, so the FBX itself is never touched. Tools/Wallit/wallit_check.py reports the same shells from the file.
public static class WallitMeshClean {
    public static int LastDropped;
    public static int LastShells;
    public static int LastSoftened;

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
        bool dropShell = roots.Count >= 2 && shellTris[roots[1]] >= 3000 && shellTris[roots[1]] >= shellTris[roots[0]] * 0.6f;
        int drop = dropShell ? roots[1] : -1;
        var kept = new List<int>(tris.Length);
        for (int t = 0; t < triCount; t++) {
            if (drop >= 0 && Find(parent, group[tris[t * 3]]) == drop) {
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
        LastDropped = dropShell ? shellTris[drop] : 0;
        LastSoftened = SoftenSeams(clean, kept, group, weld.Count);
        WeldNormals(clean, group, weld.Count);
        return clean;
    }

    // The bake skins each part of the body to one bone and the weights jump from one to the next along a hard line (hips
    // to thigh, torso to arm). The big flat triangles on the back straddle that line, so a swinging leg drags them out
    // into spikes. Blend the weights of the vertices on and beside every such line, a couple of passes, so the skin
    // bends instead of tearing.
    static int SoftenSeams(Mesh mesh, List<int> kept, int[] group, int groups) {
        BoneWeight[] bw = mesh.boneWeights;
        if (bw == null || bw.Length != group.Length) {
            return 0;
        }
        var weights = new Dictionary<int, float>[groups];
        for (int i = 0; i < bw.Length; i++) {
            if (weights[group[i]] != null) {
                continue;
            }
            var d = new Dictionary<int, float>();
            Add(d, bw[i].boneIndex0, bw[i].weight0);
            Add(d, bw[i].boneIndex1, bw[i].weight1);
            Add(d, bw[i].boneIndex2, bw[i].weight2);
            Add(d, bw[i].boneIndex3, bw[i].weight3);
            weights[group[i]] = d;
        }
        var near = new HashSet<int>[groups];
        for (int t = 0; t + 2 < kept.Count; t += 3) {
            for (int k = 0; k < 3; k++) {
                int a = group[kept[t + k]];
                int b = group[kept[t + (k + 1) % 3]];
                if (a == b) {
                    continue;
                }
                if (near[a] == null) {
                    near[a] = new HashSet<int>();
                }
                if (near[b] == null) {
                    near[b] = new HashSet<int>();
                }
                near[a].Add(b);
                near[b].Add(a);
            }
        }
        var seam = new HashSet<int>();
        for (int g = 0; g < groups; g++) {
            if (weights[g] == null || near[g] == null) {
                continue;
            }
            int dom = Dominant(weights[g]);
            foreach (int n in near[g]) {
                if (weights[n] != null && Dominant(weights[n]) != dom) {
                    seam.Add(g);
                    seam.Add(n);
                }
            }
        }
        for (int pass = 0; pass < 2; pass++) {
            var next = new Dictionary<int, Dictionary<int, float>>();
            foreach (int g in seam) {
                var blend = new Dictionary<int, float>();
                foreach (var kv in weights[g]) {
                    Add(blend, kv.Key, kv.Value * 0.5f);
                }
                float share = 0.5f / near[g].Count;
                foreach (int n in near[g]) {
                    if (weights[n] == null) {
                        continue;
                    }
                    foreach (var kv in weights[n]) {
                        Add(blend, kv.Key, kv.Value * share);
                    }
                }
                next[g] = blend;
            }
            foreach (var kv in next) {
                weights[kv.Key] = kv.Value;
            }
        }
        for (int i = 0; i < bw.Length; i++) {
            if (!seam.Contains(group[i])) {
                continue;
            }
            var list = new List<KeyValuePair<int, float>>(weights[group[i]]);
            list.Sort((x, y) => y.Value.CompareTo(x.Value));
            float sum = 0f;
            for (int k = 0; k < 4 && k < list.Count; k++) {
                sum += list[k].Value;
            }
            if (sum <= 0f) {
                continue;
            }
            BoneWeight w = new BoneWeight();
            if (list.Count > 0) { w.boneIndex0 = list[0].Key; w.weight0 = list[0].Value / sum; }
            if (list.Count > 1) { w.boneIndex1 = list[1].Key; w.weight1 = list[1].Value / sum; }
            if (list.Count > 2) { w.boneIndex2 = list[2].Key; w.weight2 = list[2].Value / sum; }
            if (list.Count > 3) { w.boneIndex3 = list[3].Key; w.weight3 = list[3].Value / sum; }
            bw[i] = w;
        }
        mesh.boneWeights = bw;
        return seam.Count;
    }

    static void Add(Dictionary<int, float> d, int bone, float weight) {
        if (weight <= 0f) {
            return;
        }
        float old;
        d.TryGetValue(bone, out old);
        d[bone] = old + weight;
    }

    static int Dominant(Dictionary<int, float> d) {
        int best = -1;
        float bestW = -1f;
        foreach (var kv in d) {
            if (kv.Value > bestW) {
                best = kv.Key;
                bestW = kv.Value;
            }
        }
        return best;
    }

    // UV seams split vertices, and each half keeps its own normal, which draws as a stitched line across the back.
    // Average the normals of the halves that sit at one point and face roughly the same way; real creases stay hard.
    static void WeldNormals(Mesh mesh, int[] group, int groups) {
        Vector3[] normals = mesh.normals;
        if (normals == null || normals.Length != group.Length) {
            return;
        }
        var members = new List<int>[groups];
        for (int i = 0; i < group.Length; i++) {
            if (members[group[i]] == null) {
                members[group[i]] = new List<int>();
            }
            members[group[i]].Add(i);
        }
        var result = new Vector3[normals.Length];
        for (int i = 0; i < normals.Length; i++) {
            Vector3 sum = Vector3.zero;
            foreach (int j in members[group[i]]) {
                if (Vector3.Dot(normals[i], normals[j]) > 0.5f) {
                    sum += normals[j];
                }
            }
            result[i] = sum.sqrMagnitude > 1e-8f ? sum.normalized : normals[i];
        }
        mesh.normals = result;
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
