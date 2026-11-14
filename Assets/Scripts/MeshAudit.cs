using UnityEngine;

// The mesh audit: every KitCatalog piece, every CharacterKit rig and prop and every Gen bake (BakedMesh) is checked for
// inside-out faces (negative signed volume, or most triangle windings disagreeing with the stored normals), zero or NaN normals,
// and negative scale anywhere in a spawned hierarchy. Fix failures in the builders (winding, hand-set normals), never with a
// two-sided material. Returns a KitReport; counts are in Inside, ZeroNormals and NegativeScales. Called by the PlayMode test
// and the `mesh-audit` playtest.
public class MeshAuditReport : KitReport {
    public int Inside, ZeroNormals, NegativeScales, Meshes, MirroredText;
    public new string Summary() {
        return "meshes=" + Meshes + " insideOut=" + Inside + " zeroNormals=" + ZeroNormals + " negativeScale=" + NegativeScales + " mirroredText=" + MirroredText;
    }
}

public static class MeshAudit {
    public static MeshAuditReport Run() {
        var r = new MeshAuditReport();
        KitCatalog.Init();
        foreach (KitDef d in KitCatalog.All) {
            CheckMesh(r, "piece " + d.Id, KitCatalog.MeshOf(d.Id));
            GameObject go = KitCatalog.Make(d.Id);
            CheckScale(r, "piece " + d.Id, go);
            Object.DestroyImmediate(go);
        }
        foreach (CharacterKit.Def d in CharacterKit.Defs) {
            GameObject root = CharacterKit.Make(d.Id);
            if (root == null) continue;
            foreach (MeshFilter f in root.GetComponentsInChildren<MeshFilter>(true)) {
                CheckMesh(r, "character " + d.Id + "/" + f.name, f.sharedMesh);
            }
            CheckScale(r, "character " + d.Id, root);
            Object.DestroyImmediate(root);
        }
        foreach (string pid in CharacterKit.Props) {
            GameObject p = CharacterKit.Prop(pid);
            if (p == null) continue;
            foreach (MeshFilter f in p.GetComponentsInChildren<MeshFilter>(true)) {
                CheckMesh(r, "prop " + pid, f.sharedMesh);
            }
            CheckScale(r, "prop " + pid, p);
            Object.DestroyImmediate(p);
        }
        var signs = new System.Collections.Generic.List<string>();
        r.MirroredText += SignAudit.Scan(Camera.main, signs);
        foreach (string f in signs) {
            r.Failures.Add("quality.mirroredText: " + f);
        }
        r.Lines.Add("audit " + r.Summary());
        return r;
    }

    public static void CheckScale(MeshAuditReport r, string label, GameObject root) {
        if (root == null) return;
        foreach (TextMesh tm in root.GetComponentsInChildren<TextMesh>(true)) {
            if (tm.transform.localToWorldMatrix.determinant < 0f) {
                r.MirroredText++;
                r.Failures.Add("quality.mirroredText: " + label + ": " + tm.name + " is mirrored by a negative scale");
            }
        }
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) {
            Vector3 s = t.lossyScale;
            if (s.x < 0f || s.y < 0f || s.z < 0f) {
                r.NegativeScales++;
                r.Failures.Add(label + ": " + t.name + " has negative scale " + s + " (bake a mirrored mesh instead)");
            }
        }
    }

    public static void CheckMesh(MeshAuditReport r, string label, Mesh m) {
        if (m == null) return;
        r.Meshes++;
        Vector3[] v = m.vertices;
        Vector3[] n = m.normals;
        int zero = 0;
        for (int i = 0; i < n.Length; i++) {
            float q = n[i].sqrMagnitude;
            if (float.IsNaN(q) || q < 0.25f) zero++;
        }
        if (n.Length != v.Length) {
            zero += v.Length;
        }
        if (zero > 0) {
            r.ZeroNormals++;
            r.Failures.Add(label + ": " + zero + " zero, NaN or missing normals");
        }
        Vector3 c = m.bounds.center;
        double volume = 0;
        int tris = 0, disagree = 0;
        for (int s = 0; s < m.subMeshCount; s++) {
            int[] idx = m.GetTriangles(s);
            for (int i = 0; i + 2 < idx.Length; i += 3) {
                Vector3 a = v[idx[i]] - c, b = v[idx[i + 1]] - c, d = v[idx[i + 2]] - c;
                volume += Vector3.Dot(a, Vector3.Cross(b - a, d - a)) / 6.0;
                Vector3 fn = Vector3.Cross(b - a, d - a);
                if (fn.sqrMagnitude < 1e-12f || n.Length != v.Length) continue;
                tris++;
                if (Vector3.Dot(fn, n[idx[i]] + n[idx[i + 1]] + n[idx[i + 2]]) < 0f) disagree++;
            }
        }
        // Unity winds front faces clockwise, so cross(b-a, c-a) points out of the front; an outward closed mesh has a
        // positive volume under this sum's sign convention flipped, hence the test is on the negated total.
        bool inside = -volume < 0.0 && Mathf.Abs((float)volume) > 1e-6f && IsClosedish(m);
        if (!inside && tris > 0 && disagree > tris * 0.5f) inside = true;
        if (inside) {
            r.Inside++;
            r.Failures.Add(label + ": inside out (signed volume " + (-volume).ToString("0.000") + ", " + disagree + "/" + tris + " windings against their normals)");
        }
    }

    // Open sheets (walls with one face, flags) have no meaningful volume; only judge meshes that enclose space.
    static bool IsClosedish(Mesh m) {
        Vector3 s = m.bounds.size;
        return s.x > 0.05f && s.y > 0.05f && s.z > 0.05f;
    }
}
