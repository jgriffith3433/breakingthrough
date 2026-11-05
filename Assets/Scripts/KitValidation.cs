using System.Collections.Generic;
using System.Text;
using UnityEngine;

// The Model Kit's validator (Docs/ModelKit.md section 5). Fails when a piece is a built-in primitive mesh (by name, or by
// being a plain box), has fewer than 24 vertices without a documented reason, has no texture or no instancing, repeats
// another piece's mesh (a recoloured copy), or when the totals go over the budget. Prints triangles and texture memory per
// piece and in total. Called by Tools > Kit > Validate Kit, the PlayMode test and the kit-preview playtest.
public class KitReport {
    public readonly List<string> Lines = new List<string>();
    public readonly List<string> Failures = new List<string>();
    public int Pieces, Triangles, Vertices, Primitives, MaxPieceTriangles;
    public long TextureBytes;
    public double TextureMB { get { return TextureBytes / 1048576.0; } }
    public readonly Dictionary<string, int> PerPalette = new Dictionary<string, int>();
    public bool Ok { get { return Failures.Count == 0; } }
    public string Summary() {
        return "pieces=" + Pieces + " triangles=" + Triangles + " vertices=" + Vertices + " textureMB=" + TextureMB.ToString("0.00") + " primitives=" + Primitives + " maxPieceTris=" + MaxPieceTriangles + " failures=" + Failures.Count;
    }
    public string Text() {
        var sb = new StringBuilder();
        foreach (string l in Lines) {
            sb.AppendLine(l);
        }
        foreach (string f in Failures) {
            sb.AppendLine("FAIL " + f);
        }
        sb.AppendLine(Summary());
        return sb.ToString();
    }
}

public static class KitValidation {
    public const int MinPieces = 60;
    public const int MinPerPalette = 8;
    public const int MinVertices = 24;
    public const int MaxPieceTriangles = 1500;
    public const int MaxVisibleTriangles = 150000;
    public const long MaxTextureBytes = 64L * 1024 * 1024;
    static readonly string[] PrimitiveNames = { "Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad" };
    static readonly string[] ChapterPalettes = { "yard", "gate", "floor", "walk", "reading", "filed" };

    public static bool IsPrimitiveName(string name) {
        if (string.IsNullOrEmpty(name)) {
            return false;
        }
        foreach (string p in PrimitiveNames) {
            if (name == p || name.StartsWith(p + " ") || name.StartsWith(p + ".") || name == p + " Instance") {
                return true;
            }
        }
        return false;
    }

    public static KitReport Run() {
        var r = new KitReport();
        KitCatalog.Init();
        var seen = new Dictionary<string, string>();
        var textures = new HashSet<Texture>();
        foreach (KitDef d in KitCatalog.All) {
            Mesh mesh = KitCatalog.MeshOf(d.Id);
            Material[] mats = KitCatalog.MaterialsOf(d, mesh);
            int tris = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) {
                tris += (int)(mesh.GetIndexCount(s) / 3);
            }
            long texBytes = 0;
            var own = new HashSet<Texture>();
            foreach (Material mat in mats) {
                Texture a = mat.mainTexture, n = mat.GetTexture("_BumpMap");
                if (a == null) {
                    r.Failures.Add(d.Id + ": material " + mat.name + " has no texture");
                }
                if (!mat.enableInstancing) {
                    r.Failures.Add(d.Id + ": material " + mat.name + " is not GPU-instanced");
                }
                if (a != null && own.Add(a)) {
                    texBytes += KitTextures.Bytes(a as Texture2D);
                }
                if (n != null && own.Add(n)) {
                    texBytes += KitTextures.Bytes(n as Texture2D);
                }
                if (a != null) textures.Add(a);
                if (n != null) textures.Add(n);
            }
            bool primitive = IsPrimitiveName(mesh.name) || mesh.name != d.Id;
            if (tris <= 12 && string.IsNullOrEmpty(d.Reason)) {
                primitive = true;   // a plain box is a cube with another name
            }
            if (primitive) {
                r.Primitives++;
                r.Failures.Add(d.Id + ": mesh is a built-in primitive (" + mesh.name + ", " + tris + " triangles)");
            }
            if (mesh.vertexCount < MinVertices && string.IsNullOrEmpty(d.Reason)) {
                r.Failures.Add(d.Id + ": only " + mesh.vertexCount + " vertices and no documented reason");
            }
            if (tris > MaxPieceTriangles) {
                r.Failures.Add(d.Id + ": " + tris + " triangles is over the " + MaxPieceTriangles + " per piece");
            }
            if (mesh.normals == null || mesh.normals.Length != mesh.vertexCount || mesh.uv == null || mesh.uv.Length != mesh.vertexCount) {
                r.Failures.Add(d.Id + ": missing normals or UVs");
            }
            Vector3 size = mesh.bounds.size;
            string sig = mesh.vertexCount + "|" + tris + "|" + size.x.ToString("0.00") + "|" + size.y.ToString("0.00") + "|" + size.z.ToString("0.00");
            string other;
            if (seen.TryGetValue(sig, out other)) {
                r.Failures.Add(d.Id + ": same mesh as " + other + " (a recoloured copy is not a new piece)");
            } else {
                seen[sig] = d.Id;
            }
            r.Pieces++;
            r.Triangles += tris;
            r.Vertices += mesh.vertexCount;
            r.MaxPieceTriangles = Mathf.Max(r.MaxPieceTriangles, tris);
            int count;
            r.PerPalette.TryGetValue(d.Palette, out count);
            r.PerPalette[d.Palette] = count + 1;
            r.Lines.Add("piece " + d.Id + " palette=" + d.Palette + " vertices=" + mesh.vertexCount + " triangles=" + tris + " textureMB=" + (texBytes / 1048576.0).ToString("0.000") + " materials=" + mats.Length + " size=" + size.x.ToString("0.0") + "x" + size.y.ToString("0.0") + "x" + size.z.ToString("0.0"));
        }
        foreach (Texture t in textures) {
            r.TextureBytes += KitTextures.Bytes(t as Texture2D);
        }
        if (r.Pieces < MinPieces) {
            r.Failures.Add("only " + r.Pieces + " pieces, " + MinPieces + " wanted");
        }
        foreach (string p in ChapterPalettes) {
            int n;
            r.PerPalette.TryGetValue(p, out n);
            if (n < MinPerPalette) {
                r.Failures.Add("palette " + p + " has " + n + " pieces, " + MinPerPalette + " wanted");
            }
        }
        if (r.TextureBytes > MaxTextureBytes) {
            r.Failures.Add("textures use " + r.TextureMB.ToString("0.0") + " MB, over 64 MB");
        }
        if (r.Triangles > MaxVisibleTriangles) {
            r.Failures.Add("all pieces together are " + r.Triangles + " triangles, over " + MaxVisibleTriangles);
        }
        foreach (KeyValuePair<string, int> kv in r.PerPalette) {
            r.Lines.Add("palette " + kv.Key + " pieces=" + kv.Value);
        }
        return r;
    }

    static readonly Dictionary<Mesh, int> triCache = new Dictionary<Mesh, int>();

    // Triangles of every mesh renderer whose bounds are inside the camera's frustum (what could be drawn in this view).
    public static int VisibleTriangles(Camera cam) {
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
        int total = 0;
        foreach (MeshRenderer mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) {
            if (!mr.enabled || !mr.gameObject.activeInHierarchy || !GeometryUtility.TestPlanesAABB(planes, mr.bounds)) {
                continue;
            }
            MeshFilter mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) {
                continue;
            }
            int t;
            if (!triCache.TryGetValue(mf.sharedMesh, out t)) {
                t = 0;
                for (int s = 0; s < mf.sharedMesh.subMeshCount; s++) {
                    t += (int)(mf.sharedMesh.GetIndexCount(s) / 3);
                }
                triCache[mf.sharedMesh] = t;
            }
            total += t;
        }
        return total;
    }
}
