using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Character model validation (Docs/CharacterModels.md, section 4). Builds every character and fails when a body mesh is a
// built-in primitive (by mesh or object name, or by being a bare 8-vertex box), has fewer than 24 vertices, a character has
// too few parts or lacks a required one, or its triangles go over the budget (6,000; 12,000 for bosses). Prints the triangle
// count of every character. Called by the CharacterValidationTests test and the character-gallery playtest.
public class CharacterReport {
    public readonly List<string> Lines = new List<string>();
    public readonly List<string> Failures = new List<string>();
    public int Characters, Triangles, Primitives, Parts;
    public readonly Dictionary<string, int> PerCharacter = new Dictionary<string, int>();
    public bool Ok { get { return Failures.Count == 0; } }
    public string Summary() { return "characters=" + Characters + " parts=" + Parts + " triangles=" + Triangles + " primitives=" + Primitives + " failures=" + Failures.Count; }
    public string Text() {
        var sb = new StringBuilder();
        foreach (string l in Lines) { sb.AppendLine(l); }
        foreach (string f in Failures) { sb.AppendLine("FAIL " + f); }
        sb.AppendLine(Summary());
        return sb.ToString();
    }
}

public static class CharacterValidation {
    public const int MinCharacters = 12;
    public const int MinParts = 8;
    public const int MinVertices = 24;

    static readonly Dictionary<string, string[]> Required = new Dictionary<string, string[]> {
        { "wallit", new[] { "Body", "Head", "ArmL", "ArmR", "LegL", "LegR", "Prop" } },
        { "slice", new[] { "Body", "Head", "ArmL", "ArmR", "LegL", "LegR", "Prop" } },
        { "yobot", new[] { "Body", "Head", "ClawL", "ClawR", "LegL", "LegR" } },
        { "spiderbot", new[] { "Body", "Head", "LegL1", "LegL2", "LegL3", "LegR1", "LegR2", "LegR3" } },
        { "courier", new[] { "Body", "Head", "ClawL", "ClawR", "LegL", "LegR" } },
        { "foreman", new[] { "Body", "Head", "ClawL", "ClawR", "LegL", "LegR" } },
        { "server", new[] { "Body", "Head", "LegL1", "LegL2", "LegL3", "LegR1", "LegR2", "LegR3" } },
        { "orderbearer", new[] { "Body", "Head", "ClawL", "ClawR", "LegL", "LegR", "Scroll" } },
    };

    static bool IsPrimitiveName(string n) { return KitValidation.IsPrimitiveName(n); }

    public static CharacterReport Run() {
        var r = new CharacterReport();
        CharacterKit.Init();
        foreach (CharacterDef d in CharacterKit.All) {
            GameObject go = CharacterKit.Make(d.Id);
            if (go == null) {
                r.Failures.Add(d.Id + ": did not build");
                continue;
            }
            r.Characters++;
            int tris = 0, parts = 0, prims = 0;
            var names = new HashSet<string>();
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) {
                names.Add(t.name);
                if (IsPrimitiveName(t.name)) {
                    prims++;
                    r.Failures.Add(d.Id + ": object " + t.name + " is a primitive");
                }
            }
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true)) {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null) {
                    r.Failures.Add(d.Id + ": " + mf.name + " has no mesh");
                    continue;
                }
                parts++;
                tris += mesh.triangles.Length / 3;
                if (IsPrimitiveName(mesh.name)) {
                    prims++;
                    r.Failures.Add(d.Id + ": " + mf.name + " uses the built-in mesh " + mesh.name);
                }
                if (mesh.vertexCount < MinVertices) {
                    prims++;
                    r.Failures.Add(d.Id + ": " + mf.name + " has only " + mesh.vertexCount + " vertices (primitive-like)");
                }
                if (mf.GetComponent<Collider>() != null) {
                    r.Failures.Add(d.Id + ": " + mf.name + " has a collider (hitboxes live on Enemy and Musician)");
                }
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mr.sharedMaterial == null || mr.sharedMaterial.mainTexture == null) {
                    r.Failures.Add(d.Id + ": " + mf.name + " has no textured material");
                }
            }
            if (parts < MinParts) {
                r.Failures.Add(d.Id + ": only " + parts + " parts");
            }
            string[] need;
            if (Required.TryGetValue(d.Id, out need)) {
                foreach (string n in need) {
                    if (!names.Contains(n)) {
                        r.Failures.Add(d.Id + ": missing part " + n);
                    }
                }
            }
            if (tris > d.Budget) {
                r.Failures.Add(d.Id + ": " + tris + " triangles, over " + d.Budget);
            }
            r.Lines.Add(d.Id + " parts=" + parts + " triangles=" + tris + " budget=" + d.Budget + (d.Boss ? " boss" : ""));
            r.PerCharacter[d.Id] = tris;
            r.Triangles += tris;
            r.Parts += parts;
            r.Primitives += prims;
            if (Application.isPlaying) {
                Object.Destroy(go);
            } else {
                Object.DestroyImmediate(go);
            }
        }
        if (r.Characters < MinCharacters) {
            r.Failures.Add("only " + r.Characters + " characters, need " + MinCharacters);
        }
        return r;
    }
}
