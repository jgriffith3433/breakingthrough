using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// The scene scan for chapters 1 to 6 and the hub (Docs/Levels1to3.md and Docs/Levels4to6AndHub.md, section 6 and 11). It walks every renderer under the roots it is given and
// reports built-in primitive meshes (Cube, Sphere, Capsule, Cylinder, Plane, Quad by mesh name, and the old OwnMap boxes), the
// triangles in the whole scene and in the camera's view, an estimate of the draw calls (one per mesh and material, instanced
// 500 to a batch where the material allows it), and the lights and colliders. Characters (bots, Slice, Wallit, pickups) are not
// scenery and are skipped. A few small accents may stay primitive when they are listed here by object name; none are today.
public class SceneScanReport {
    public string Scene = "";
    public int Renderers, Primitives, Triangles, VisibleTriangles, DrawCalls, Lights, Colliders, MeshColliders, KitPieces, Materials;
    public readonly List<string> PrimitiveNames = new List<string>();
    public readonly List<string> Failures = new List<string>();
    public bool Ok { get { return Failures.Count == 0; } }

    public string Summary() {
        return "scene=" + Scene + " renderers=" + Renderers + " kitPieces=" + KitPieces + " primitives=" + Primitives + " triangles=" + Triangles + " visibleTriangles=" + VisibleTriangles
            + " drawCalls=" + DrawCalls + " materials=" + Materials + " lights=" + Lights + " colliders=" + Colliders + " meshColliders=" + MeshColliders + " failures=" + Failures.Count;
    }

    public string Text() {
        var sb = new StringBuilder();
        sb.AppendLine(Summary());
        foreach (string p in PrimitiveNames) {
            sb.AppendLine("PRIMITIVE " + p);
        }
        foreach (string f in Failures) {
            sb.AppendLine("FAIL " + f);
        }
        return sb.ToString();
    }
}

public static class SceneValidation {
    public const int MaxVisibleTriangles = 150000;   // Docs/ModelKit.md section 4
    public const int MaxDrawCalls = 400;
    public const int InstanceBatch = 500;
    // Objects (by name prefix) that may stay a primitive mesh: documented small accents. Empty on purpose.
    public static readonly string[] Accents = { };

    static bool IsAccent(string name) {
        foreach (string a in Accents) {
            if (name.StartsWith(a)) {
                return true;
            }
        }
        return false;
    }

    static bool IsCharacter(Component c) {
        return c.GetComponentInParent<Enemy>() != null || c.GetComponentInParent<Companion>() != null || c.GetComponentInParent<CharacterMovement>() != null
            || c.GetComponentInParent<Musician>() != null || c.GetComponentInParent<PlayerGrowth>() != null;
    }

    public static bool IsPrimitiveMesh(Mesh mesh) {
        if (mesh == null) {
            return false;
        }
        return KitValidation.IsPrimitiveName(mesh.name) || mesh.name.StartsWith("Own Box") || mesh.name.StartsWith("Own Cylinder") || mesh.name.StartsWith("Own Cone");
    }

    static int Triangles(Mesh mesh) {
        int t = 0;
        for (int s = 0; s < mesh.subMeshCount; s++) {
            t += (int)(mesh.GetIndexCount(s) / 3);
        }
        return t;
    }

    // What the chapter now playing is made of: Chapter 1's level scene, or a chapter map's scene plus the props the chapter run adds
    // (the hub: the office's scene plus ChapterHub's world).
    public static List<GameObject> ChapterRoots() {
        var roots = new List<GameObject>();
        if (LevelScene.Instance != null) {
            roots.AddRange(SceneManager.GetActiveScene().GetRootGameObjects());
            return roots;
        }
        ChapterMap map = ChapterMap.Current;
        if (map != null && map.MapScene.isLoaded) {
            roots.AddRange(map.MapScene.GetRootGameObjects());
        }
        if (ChapterRun.Instance != null) {
            roots.AddRange(ChapterRun.Instance.DressingRoots());
        }
        if (ChapterHub.Instance != null && ChapterHub.Instance.WorldRoot != null) {
            roots.Add(ChapterHub.Instance.WorldRoot.gameObject);   // the hub's people, pickups, door leaves and board
        }
        return roots;
    }

    public static SceneScanReport ScanChapter() {
        Camera cam = Camera.main;
        SceneScanReport r = Scan(ChapterRoots(), cam);
        r.Scene = LevelScene.Instance != null ? LevelScene.Instance.SceneName : (ChapterMap.Current != null ? ChapterMap.Current.MapName : "none");
        return r;
    }

    public static SceneScanReport Scan(IEnumerable<GameObject> roots, Camera cam) {
        var report = new SceneScanReport();
        Plane[] planes = cam != null ? GeometryUtility.CalculateFrustumPlanes(cam) : null;
        var groups = new Dictionary<long, int>();
        var instanced = new Dictionary<long, bool>();
        var materials = new HashSet<Material>();
        foreach (GameObject root in roots) {
            if (root == null) {
                continue;
            }
            foreach (Light light in root.GetComponentsInChildren<Light>(false)) {
                if (light.type != LightType.Directional) {
                    report.Lights++;
                }
            }
            foreach (Collider c in root.GetComponentsInChildren<Collider>(false)) {
                if (c is TerrainCollider || IsCharacter(c) || c.isTrigger) {
                    continue;
                }
                report.Colliders++;
                if (c is MeshCollider) {
                    report.MeshColliders++;
                }
            }
            foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(false)) {
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || IsCharacter(mr)) {
                    continue;
                }
                Mesh mesh = mf.sharedMesh;
                report.Renderers++;
                int tris = Triangles(mesh);
                report.Triangles += tris;
                if (mr.GetComponent<KitPiece>() != null) {
                    report.KitPieces++;
                }
                if (IsPrimitiveMesh(mesh) && !IsAccent(mr.name)) {
                    report.Primitives++;
                    if (report.PrimitiveNames.Count < 40) {
                        report.PrimitiveNames.Add(mr.name + " (" + mesh.name + ", " + tris + " triangles)");
                    }
                }
                if (!mr.enabled || (planes != null && !GeometryUtility.TestPlanesAABB(planes, mr.bounds))) {
                    continue;
                }
                report.VisibleTriangles += tris;
                Material[] mats = mr.sharedMaterials;
                for (int s = 0; s < mats.Length && s < mesh.subMeshCount; s++) {
                    if (mats[s] == null) {
                        continue;
                    }
                    materials.Add(mats[s]);
                    long key = ((long)mesh.GetInstanceID() << 20) ^ (long)mats[s].GetInstanceID();
                    int n;
                    groups.TryGetValue(key, out n);
                    groups[key] = n + 1;
                    instanced[key] = mats[s].enableInstancing;
                }
            }
        }
        foreach (KeyValuePair<long, int> g in groups) {
            report.DrawCalls += instanced[g.Key] ? (g.Value + InstanceBatch - 1) / InstanceBatch : g.Value;
        }
        report.Materials = materials.Count;
        if (report.Primitives > 0) {
            report.Failures.Add(report.Primitives + " renderer(s) use a built-in primitive mesh: " + string.Join("; ", report.PrimitiveNames.ToArray()));
        }
        if (report.VisibleTriangles > MaxVisibleTriangles) {
            report.Failures.Add("visible triangles " + report.VisibleTriangles + " over " + MaxVisibleTriangles);
        }
        if (report.DrawCalls > MaxDrawCalls) {
            report.Failures.Add("estimated draw calls " + report.DrawCalls + " over " + MaxDrawCalls);
        }
        if (report.Renderers == 0) {
            report.Failures.Add("no renderers found: the scene did not fill in");
        }
        return report;
    }
}
