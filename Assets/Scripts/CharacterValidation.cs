using System.Collections.Generic;
using UnityEngine;

// Validation of the character models (Docs/CharacterModels.md). Builds every CharacterKit rig and checks: no built-in primitive
// mesh as a body, the part names the animators bind, at least 24 vertices a part-mesh, the triangle budget (6,000 a
// character, 12,000 a boss), and that no two characters share one mesh shape (a recoloured copy). Wallit, Slice, the bots and
// the hub's lodgers are measured and printed too (their budgets are checked, their meshes are the shipped ones). Returns a
// KitReport so the PlayMode test (by reflection) and the `characters` playtest can print it.
public static class CharacterValidation {
    public const int CharacterBudget = 6000;
    public const int BossBudget = 12000;

    static readonly string[] YoParts = { "Body", "Head", "ClawL", "ClawR", "LegL", "LegR" };
    static readonly string[] SpiderParts = { "Body", "Head", "LegL1", "LegL2", "LegL3", "LegR1", "LegR2", "LegR3" };

    public static KitReport Run() {
        var r = new KitReport();
        KitCatalog.Init();
        var seen = new Dictionary<string, string>();
        foreach (CharacterKit.Def d in CharacterKit.Defs) {
            GameObject root = CharacterKit.Make(d.Id);
            if (root == null) {
                r.Failures.Add(d.Id + ": no rig");
                continue;
            }
            int tris = 0, verts = 0, meshes = 0;
            Bounds b = new Bounds();
            bool any = false;
            foreach (MeshFilter f in root.GetComponentsInChildren<MeshFilter>(true)) {
                Mesh m = f.sharedMesh;
                if (m == null) {
                    r.Failures.Add(d.Id + ": " + f.name + " has no mesh");
                    continue;
                }
                meshes++;
                int t = 0;
                for (int s = 0; s < m.subMeshCount; s++) {
                    t += (int)(m.GetIndexCount(s) / 3);
                }
                tris += t;
                verts += m.vertexCount;
                if (KitValidation.IsPrimitiveName(m.name) || KitValidation.IsPrimitiveName(f.name)) {
                    r.Primitives++;
                    r.Failures.Add(d.Id + ": " + f.name + " is a built-in primitive mesh (" + m.name + ")");
                }
                if (t <= 12 && f.name != "Seal") {
                    r.Failures.Add(d.Id + ": " + f.name + " is only " + t + " triangles (a plain box)");
                }
                if (m.vertexCount < KitValidation.MinVertices) {
                    r.Failures.Add(d.Id + ": " + f.name + " has " + m.vertexCount + " vertices, " + KitValidation.MinVertices + " wanted");
                }
                MeshRenderer mr = f.GetComponent<MeshRenderer>();
                if (mr == null || mr.sharedMaterial == null || mr.sharedMaterial.mainTexture == null) {
                    r.Failures.Add(d.Id + ": " + f.name + " has no textured material");
                }
                Bounds wb = mr != null ? mr.bounds : new Bounds();
                if (!any) { b = wb; any = true; } else { b.Encapsulate(wb); }
            }
            string[] need = d.Spider ? SpiderParts : YoParts;
            foreach (string part in need) {
                if (Find(root.transform, part) == null) {
                    r.Failures.Add(d.Id + ": missing part '" + part + "' (the animators bind it by name)");
                }
            }
            if (!string.IsNullOrEmpty(d.Extra) && Find(root.transform, d.Extra) == null) {
                r.Failures.Add(d.Id + ": missing prop part '" + d.Extra + "' (its tell reads it by name)");
            }
            int budget = d.Boss ? BossBudget : CharacterBudget;
            if (tris > budget || tris > d.Budget) {
                r.Failures.Add(d.Id + ": " + tris + " triangles is over its budget of " + Mathf.Min(budget, d.Budget));
            }
            string sig = meshes + "|" + tris + "|" + verts;
            string other;
            if (seen.TryGetValue(sig, out other)) {
                r.Failures.Add(d.Id + ": same mesh shape as " + other + " (a recoloured copy is not a new character)");
            } else {
                seen[sig] = d.Id;
            }
            r.Pieces++;
            r.Triangles += tris;
            r.Vertices += verts;
            r.MaxPieceTriangles = Mathf.Max(r.MaxPieceTriangles, tris);
            r.Lines.Add("character " + d.Id + " parts=" + meshes + " vertices=" + verts + " triangles=" + tris + " budget=" + budget + " size=" + b.size.x.ToString("0.0") + "x" + b.size.y.ToString("0.0") + "x" + b.size.z.ToString("0.0"));
            Object.DestroyImmediate(root);
        }
        foreach (string pid in CharacterKit.Props) {
            GameObject p = CharacterKit.Prop(pid);
            if (p == null) {
                r.Failures.Add("prop " + pid + ": not built");
                continue;
            }
            Mesh m = p.GetComponent<MeshFilter>().sharedMesh;
            int t = 0;
            for (int s = 0; s < m.subMeshCount; s++) {
                t += (int)(m.GetIndexCount(s) / 3);
            }
            if (KitValidation.IsPrimitiveName(m.name)) {
                r.Primitives++;
                r.Failures.Add("prop " + pid + ": built-in primitive mesh");
            }
            r.Lines.Add("prop " + pid + " triangles=" + t);
            Object.DestroyImmediate(p);
        }
        // The shipped ones: reported, and held to the 6,000 budget.
        Measure(r, "wallit", Resources.Load<GameObject>("Wallit/Wallit"));
        Measure(r, "slice", Resources.Load<GameObject>("Slice/Slice"));
        Measure(r, "yo_bot", BotArt.Make(BotKind.YoBot, Color.white, 0f));
        Measure(r, "spider_bot", BotArt.Make(BotKind.SpiderBot, Color.white, 0f));
        foreach (string id in new[] { "tick", "brim", "spare", "chime" }) {
            Mesh m = KitCatalog.MeshOf("of_lodger_" + id);
            int t = 0;
            for (int s = 0; s < m.subMeshCount; s++) {
                t += (int)(m.GetIndexCount(s) / 3);
            }
            if (t > CharacterBudget) {
                r.Failures.Add(id + ": " + t + " triangles is over " + CharacterBudget);
            }
            r.Lines.Add("lodger " + id + " triangles=" + t);
        }
        return r;
    }

    static void Measure(KitReport r, string id, GameObject go) {
        if (go == null) {
            r.Lines.Add("shipped " + id + " not in this build");
            return;
        }
        int t = 0;
        bool primitive = false;
        foreach (MeshFilter f in go.GetComponentsInChildren<MeshFilter>(true)) {
            if (f.sharedMesh == null) continue;
            if (KitValidation.IsPrimitiveName(f.sharedMesh.name)) primitive = true;
            for (int s = 0; s < f.sharedMesh.subMeshCount; s++) t += (int)(f.sharedMesh.GetIndexCount(s) / 3);
        }
        foreach (SkinnedMeshRenderer sk in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
            if (sk.sharedMesh == null) continue;
            if (KitValidation.IsPrimitiveName(sk.sharedMesh.name)) primitive = true;
            for (int s = 0; s < sk.sharedMesh.subMeshCount; s++) t += (int)(sk.sharedMesh.GetIndexCount(s) / 3);
        }
        if (primitive) {
            r.Primitives++;
            r.Failures.Add(id + ": a body mesh is a built-in primitive");
        }
        if (t > CharacterBudget) {
            r.Failures.Add(id + ": " + t + " triangles is over " + CharacterBudget);
        }
        r.Lines.Add("shipped " + id + " triangles=" + t);
        r.Pieces++;
        r.Triangles += t;
    }

    static Transform Find(Transform root, string name) {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) {
            if (t.name == name) {
                return t;
            }
        }
        return null;
    }
}
