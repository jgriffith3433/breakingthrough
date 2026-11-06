using System;
using System.Collections.Generic;
using UnityEngine;

// The Model Kit's geometry library (Docs/ModelKit.md). Pieces are authored from bevelled boxes, lathes, extrusions with
// ear-clipped caps and wall panels with cut-out openings, not from Unity's primitives. Faces are added as flat or smooth
// polygons: the winding is chosen from an outward direction, so a piece never needs hand-checked vertex order.
// Two submeshes: Sub 0 is the piece's main material, Sub 1 its accent (glass, brass, cloth).
public class KitMesh {
    public const int PX = 1, NX = 2, PY = 4, NY = 8, PZ = 16, NZ = 32, All = 63;

    public struct Opening {
        public float X0, X1, Y0, Y1;
        public bool Arch;   // the top half-circle of the opening is cut as an arch (Y1 is the crown)
        public Opening(float x0, float x1, float y0, float y1, bool arch = false) { X0 = x0; X1 = x1; Y0 = y0; Y1 = y1; Arch = arch; }
    }

    public int Sub;                 // submesh new faces go to
    public float Tex = 1f;          // metres per texture tile
    public Matrix4x4 M = Matrix4x4.identity;
    public string Name = "KitPiece";

    readonly List<Vector3> pos = new List<Vector3>();
    readonly List<Vector3> nrm = new List<Vector3>();
    readonly List<Vector2> uv = new List<Vector2>();
    readonly List<int>[] tris = { new List<int>(), new List<int>() };

    public int Triangles { get { return (tris[0].Count + tris[1].Count) / 3; } }
    public int Vertices { get { return pos.Count; } }

    // ---- transform helpers ------------------------------------------------------------------------------------------

    public void At(Vector3 p, float yaw = 0f) { M = Matrix4x4.TRS(p, Quaternion.Euler(0f, yaw, 0f), Vector3.one); }
    public void Orient(Vector3 p, Vector3 euler) { M = Matrix4x4.TRS(p, Quaternion.Euler(euler), Vector3.one); }
    public void Reset() { M = Matrix4x4.identity; Sub = 0; }

    // ---- faces ------------------------------------------------------------------------------------------------------

    // A convex polygon facing outDir (before M). smoothN, when given, is one normal per point (before M); uvs likewise.
    public void Face(Vector3 outDir, Vector3[] pts, Vector3[] smoothN = null, Vector2[] uvs = null) {
        int n = pts.Length;
        var p = new List<Vector3>(n);
        var sn = new List<Vector3>(n);
        var tv = new List<Vector2>(n);
        for (int i = 0; i < n; i++) {
            Vector3 w = M.MultiplyPoint3x4(pts[i]);
            if (p.Count > 0 && (w - p[p.Count - 1]).sqrMagnitude < 1e-8f) {
                continue;
            }
            p.Add(w);
            sn.Add(smoothN != null ? M.MultiplyVector(smoothN[i]).normalized : Vector3.zero);
            tv.Add(uvs != null ? uvs[i] : Vector2.zero);
        }
        if (p.Count > 1 && (p[0] - p[p.Count - 1]).sqrMagnitude < 1e-8f) {
            p.RemoveAt(p.Count - 1);
            sn.RemoveAt(sn.Count - 1);
            tv.RemoveAt(tv.Count - 1);
        }
        if (p.Count < 3) {
            return;
        }
        Vector3 normal = Vector3.zero;
        for (int i = 0; i < p.Count; i++) {
            normal += Vector3.Cross(p[i], p[(i + 1) % p.Count]);
        }
        if (normal.sqrMagnitude < 1e-10f) {
            return;
        }
        normal.Normalize();
        Vector3 hint = M.MultiplyVector(outDir);
        if (Vector3.Dot(normal, hint) < 0f) {
            p.Reverse();
            sn.Reverse();
            tv.Reverse();
        }
        int baseIndex = pos.Count;
        for (int i = 0; i < p.Count; i++) {
            pos.Add(p[i]);
            nrm.Add(smoothN != null ? sn[i] : normal);
            uv.Add(uvs != null ? tv[i] : Project(p[i], normal));
        }
        for (int i = 1; i < p.Count - 1; i++) {
            tris[Sub].Add(baseIndex);
            tris[Sub].Add(baseIndex + i);
            tris[Sub].Add(baseIndex + i + 1);
        }
    }

    Vector2 Project(Vector3 p, Vector3 n) {
        float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
        if (ax >= ay && ax >= az) {
            return new Vector2(p.z / Tex, p.y / Tex);
        }
        if (ay >= az) {
            return new Vector2(p.x / Tex, p.z / Tex);
        }
        return new Vector2(p.x / Tex, p.y / Tex);
    }

    // ---- solids -----------------------------------------------------------------------------------------------------

    static readonly Vector3[] Axes = { Vector3.right, Vector3.up, Vector3.forward };

    // A box with chamfered edges (ch > 0 adds the edge strips and corner triangles). faces masks which main faces to emit.
    public void Box(Vector3 min, Vector3 max, float ch = 0f, int faces = All) {
        Vector3 size = max - min;
        float lim = Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.45f;
        ch = Mathf.Clamp(ch, 0f, lim);
        if (ch < 0.011f) {
            ch = 0f;   // a bevel that small is invisible and costs 32 triangles
        }
        var c = new float[3][];
        for (int a = 0; a < 3; a++) {
            c[a] = new[] { min[a], min[a] + ch, max[a] - ch, max[a] };
        }
        Func<int, int, int, Vector3> V = (ix, iy, iz) => new Vector3(c[0][ix], c[1][iy], c[2][iz]);
        int[] bit = { PX, NX, PY, NY, PZ, NZ };
        // main faces
        for (int a = 0; a < 3; a++) {
            for (int s = 0; s < 2; s++) {
                if ((faces & bit[a * 2 + s]) == 0) {
                    continue;
                }
                int o = s == 0 ? 3 : 0;
                int b1 = (a + 1) % 3, b2 = (a + 2) % 3;
                var pts = new Vector3[4];
                int k = 0;
                for (int u = 1; u <= 2; u++) {
                    for (int v = 1; v <= 2; v++) {
                        int u2 = u == 1 ? v : 3 - v;
                        var idx = new int[3];
                        idx[a] = o;
                        idx[b1] = u;
                        idx[b2] = u2;
                        pts[k++] = V(idx[0], idx[1], idx[2]);
                    }
                }
                // pts are in (1,1),(1,2),(2,2),(2,1) order: a loop
                Face(Axes[a] * (s == 0 ? 1f : -1f), pts);
            }
        }
        if (ch <= 0f) {
            return;
        }
        // edge strips
        for (int a = 0; a < 3; a++) {
            for (int b = a + 1; b < 3; b++) {
                int cx = 3 - a - b;
                for (int sa = 0; sa < 2; sa++) {
                    for (int sb = 0; sb < 2; sb++) {
                        if ((faces & (bit[a * 2 + sa] | bit[b * 2 + sb])) == 0) {
                            continue;
                        }
                        int oa = sa == 0 ? 3 : 0, ia = sa == 0 ? 2 : 1;
                        int ob = sb == 0 ? 3 : 0, ib = sb == 0 ? 2 : 1;
                        var pts = new Vector3[4];
                        for (int q = 0; q < 4; q++) {
                            var idx = new int[3];
                            bool second = q >= 2;
                            idx[a] = second ? ia : oa;
                            idx[b] = second ? ob : ib;
                            idx[cx] = (q == 0 || q == 3) ? 1 : 2;
                            pts[q] = V(idx[0], idx[1], idx[2]);
                        }
                        Vector3 dir = Axes[a] * (sa == 0 ? 1f : -1f) + Axes[b] * (sb == 0 ? 1f : -1f);
                        Face(dir, pts);
                    }
                }
            }
        }
        // corners
        for (int sx = 0; sx < 2; sx++) {
            for (int sy = 0; sy < 2; sy++) {
                for (int sz = 0; sz < 2; sz++) {
                    int ox = sx == 0 ? 3 : 0, ix = sx == 0 ? 2 : 1;
                    int oy = sy == 0 ? 3 : 0, iy = sy == 0 ? 2 : 1;
                    int oz = sz == 0 ? 3 : 0, iz = sz == 0 ? 2 : 1;
                    var pts = new[] { V(ox, iy, iz), V(ix, oy, iz), V(ix, iy, oz) };
                    Face(new Vector3(sx == 0 ? 1f : -1f, sy == 0 ? 1f : -1f, sz == 0 ? 1f : -1f), pts);
                }
            }
        }
    }

    public void BoxC(Vector3 centre, Vector3 size, float ch = 0f, int faces = All) {
        Box(centre - size * 0.5f, centre + size * 0.5f, ch, faces);
    }

    // A turned solid. profile is (radius, y) going up the outside with the inside on the left (cap the top by going back to r = 0).
    public void Lathe(IList<Vector2> profile, int seg = 12, bool smooth = true, float sweep = 360f, float startDeg = 0f) {
        float rRef = 0.01f;
        for (int i = 0; i < profile.Count; i++) {
            rRef = Mathf.Max(rRef, profile[i].x);
        }
        float vAcc = 0f;
        for (int i = 0; i < profile.Count - 1; i++) {
            Vector2 p0 = profile[i], p1 = profile[i + 1];
            Vector2 d = p1 - p0;
            float segLen = d.magnitude;
            if (segLen < 1e-5f || (p0.x < 1e-5f && p1.x < 1e-5f)) {
                vAcc += segLen;
                continue;
            }
            Vector2 n2 = new Vector2(d.y, -d.x).normalized;
            for (int s = 0; s < seg; s++) {
                float a0 = (startDeg + sweep * s / seg) * Mathf.Deg2Rad;
                float a1 = (startDeg + sweep * (s + 1) / seg) * Mathf.Deg2Rad;
                Vector3 A = new Vector3(p0.x * Mathf.Cos(a0), p0.y, p0.x * Mathf.Sin(a0));
                Vector3 B = new Vector3(p0.x * Mathf.Cos(a1), p0.y, p0.x * Mathf.Sin(a1));
                Vector3 C = new Vector3(p1.x * Mathf.Cos(a1), p1.y, p1.x * Mathf.Sin(a1));
                Vector3 D = new Vector3(p1.x * Mathf.Cos(a0), p1.y, p1.x * Mathf.Sin(a0));
                float am = (a0 + a1) * 0.5f;
                Vector3 hint = new Vector3(n2.x * Mathf.Cos(am), n2.y, n2.x * Mathf.Sin(am));
                Vector3 n0 = new Vector3(n2.x * Mathf.Cos(a0), n2.y, n2.x * Mathf.Sin(a0));
                Vector3 n1 = new Vector3(n2.x * Mathf.Cos(a1), n2.y, n2.x * Mathf.Sin(a1));
                float u0 = a0 * rRef / Tex, u1 = a1 * rRef / Tex;
                float v0 = vAcc / Tex, v1 = (vAcc + segLen) / Tex;
                Face(hint, new[] { A, B, C, D },
                    smooth ? new[] { n0, n1, n1, n0 } : null,
                    new[] { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1) });
            }
            vAcc += segLen;
        }
    }

    // A bevelled, capped cylinder (never a primitive: it is a lathe with a chamfered rim).
    public void Cyl(float r, float y0, float y1, int seg = 10, float bevel = 0.02f, Vector3? at = null) {
        float b = Mathf.Min(bevel, r * 0.4f, (y1 - y0) * 0.4f);
        var prof = new List<Vector2> { new Vector2(0f, y0), new Vector2(r - b, y0), new Vector2(r, y0 + b), new Vector2(r, y1 - b), new Vector2(r - b, y1), new Vector2(0f, y1) };
        if (at.HasValue) {
            Matrix4x4 keep = M;
            M = M * Matrix4x4.Translate(at.Value);
            Lathe(prof, seg);
            M = keep;
        } else {
            Lathe(prof, seg);
        }
    }

    // A simple polygon (x, y) pushed along z, with ear-clipped caps.
    public void ExtrudeZ(IList<Vector2> input, float z0, float z1) {
        var poly = new List<Vector2>();
        foreach (Vector2 q in input) {
            if (poly.Count == 0 || (q - poly[poly.Count - 1]).sqrMagnitude > 1e-8f) {
                poly.Add(q);
            }
        }
        if (poly.Count > 1 && (poly[0] - poly[poly.Count - 1]).sqrMagnitude < 1e-8f) {
            poly.RemoveAt(poly.Count - 1);
        }
        int n = poly.Count;
        float area = 0f;
        for (int i = 0; i < n; i++) {
            Vector2 a = poly[i], b = poly[(i + 1) % n];
            area += a.x * b.y - b.x * a.y;
        }
        bool ccw = area > 0f;
        for (int i = 0; i < n; i++) {
            Vector2 a = poly[i], b = poly[(i + 1) % n];
            Vector2 e = b - a;
            Vector2 outward = ccw ? new Vector2(e.y, -e.x) : new Vector2(-e.y, e.x);
            Face(new Vector3(outward.x, outward.y, 0f), new[] { new Vector3(a.x, a.y, z0), new Vector3(b.x, b.y, z0), new Vector3(b.x, b.y, z1), new Vector3(a.x, a.y, z1) });
        }
        foreach (int[] t in EarClip(poly, ccw)) {
            Face(Vector3.forward, new[] { new Vector3(poly[t[0]].x, poly[t[0]].y, z1), new Vector3(poly[t[1]].x, poly[t[1]].y, z1), new Vector3(poly[t[2]].x, poly[t[2]].y, z1) });
            Face(Vector3.back, new[] { new Vector3(poly[t[0]].x, poly[t[0]].y, z0), new Vector3(poly[t[1]].x, poly[t[1]].y, z0), new Vector3(poly[t[2]].x, poly[t[2]].y, z0) });
        }
    }

    static List<int[]> EarClip(IList<Vector2> poly, bool ccw) {
        var result = new List<int[]>();
        var idx = new List<int>();
        for (int i = 0; i < poly.Count; i++) {
            idx.Add(i);
        }
        if (!ccw) {
            idx.Reverse();
        }
        int guard = 0;
        while (idx.Count > 3 && guard++ < 500) {
            bool clipped = false;
            for (int i = 0; i < idx.Count; i++) {
                int ia = idx[(i + idx.Count - 1) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                Vector2 a = poly[ia], b = poly[ib], c = poly[ic];
                if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) <= 1e-7f) {
                    continue;
                }
                bool inside = false;
                for (int j = 0; j < idx.Count; j++) {
                    int k = idx[j];
                    if (k == ia || k == ib || k == ic) {
                        continue;
                    }
                    if (InTri(poly[k], a, b, c)) {
                        inside = true;
                        break;
                    }
                }
                if (inside) {
                    continue;
                }
                result.Add(new[] { ia, ib, ic });
                idx.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped) {
                break;
            }
        }
        if (idx.Count == 3) {
            result.Add(new[] { idx[0], idx[1], idx[2] });
        }
        return result;
    }

    static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c) {
        float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
        float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
        bool neg = d1 < -1e-6f || d2 < -1e-6f || d3 < -1e-6f;
        bool posi = d1 > 1e-6f || d2 > 1e-6f || d3 > 1e-6f;
        return !(neg && posi);
    }

    // Points of an arc about (cx, cy) for polygons, from a0 to a1 degrees.
    public static List<Vector2> Arc(float cx, float cy, float r, float a0, float a1, int steps) {
        var list = new List<Vector2>();
        for (int i = 0; i <= steps; i++) {
            float a = Mathf.Lerp(a0, a1, (float)i / steps) * Mathf.Deg2Rad;
            list.Add(new Vector2(cx + r * Mathf.Cos(a), cy + r * Mathf.Sin(a)));
        }
        return list;
    }

    // ---- walls, frames, doors ---------------------------------------------------------------------------------------

    // A wall panel centred on x = 0, standing on y = 0, thickness t about z = 0, with rectangular (or arched) holes cut through.
    public void Wall(float w, float h, float t, IList<Opening> holes, float ch = 0.02f) {
        var xs = new List<float> { -w * 0.5f, w * 0.5f };
        var ys = new List<float> { 0f, h };
        foreach (Opening o in holes) {
            xs.Add(o.X0);
            xs.Add(o.X1);
            ys.Add(o.Y0);
            ys.Add(o.Y1);
        }
        xs.Sort();
        ys.Sort();
        int nx = xs.Count - 1, ny = ys.Count - 1;
        var solid = new bool[nx, ny];
        for (int i = 0; i < nx; i++) {
            for (int j = 0; j < ny; j++) {
                float cx = (xs[i] + xs[i + 1]) * 0.5f, cy = (ys[j] + ys[j + 1]) * 0.5f;
                bool hole = xs[i + 1] - xs[i] < 1e-4f || ys[j + 1] - ys[j] < 1e-4f;
                foreach (Opening o in holes) {
                    if (cx > o.X0 && cx < o.X1 && cy > o.Y0 && cy < o.Y1) {
                        hole = true;
                    }
                }
                solid[i, j] = !hole;
            }
        }
        float hz = t * 0.5f;
        for (int i = 0; i < nx; i++) {
            for (int j = 0; j < ny; j++) {
                if (!solid[i, j] || xs[i + 1] - xs[i] < 1e-4f || ys[j + 1] - ys[j] < 1e-4f) {
                    continue;
                }
                int mask = PZ | NZ;
                if (i == 0 || !solid[i - 1, j]) mask |= NX;
                if (i == nx - 1 || !solid[i + 1, j]) mask |= PX;
                if (j == 0 || !solid[i, j - 1]) mask |= NY;
                if (j == ny - 1 || !solid[i, j + 1]) mask |= PY;
                Box(new Vector3(xs[i], ys[j], -hz), new Vector3(xs[i + 1], ys[j + 1], hz), 0f, mask);
            }
        }
        foreach (Opening o in holes) {
            if (!o.Arch) {
                continue;
            }
            float r = (o.X1 - o.X0) * 0.5f, cx = (o.X0 + o.X1) * 0.5f, spring = o.Y1 - r;
            var left = Arc(cx, spring, r, 180f, 90f, 6);
            left.Add(new Vector2(o.X0, o.Y1));
            var right = Arc(cx, spring, r, 90f, 0f, 6);
            right.Add(new Vector2(o.X1, o.Y1));
            // arch spandrels fill the corners above the spring line; they sit where the grid left the opening empty
            ExtrudeZ(Reorder(left), -hz, hz);
            ExtrudeZ(Reorder(right), -hz, hz);
        }
    }

    static List<Vector2> Reorder(List<Vector2> pts) { return pts; }

    // A moulded frame about an opening on the +Z face: four boxes standing proud, with a deeper sill.
    public void Frame(Opening o, float t, float width = 0.1f, float proud = 0.06f, float sill = 0.1f, float ch = 0.015f) {
        float z0 = t * 0.5f - 0.02f, z1 = t * 0.5f + proud;
        float x0 = o.X0, x1 = o.X1, y0 = o.Y0, y1 = o.Y1;
        Box(new Vector3(x0 - width, y0, z0), new Vector3(x0, y1 + width, z1), ch);
        Box(new Vector3(x1, y0, z0), new Vector3(x1 + width, y1 + width, z1), ch);
        Box(new Vector3(x0, y1, z0), new Vector3(x1, y1 + width, z1), ch);
        if (sill > 0f) {
            Box(new Vector3(x0 - width * 1.4f, y0 - sill * 0.5f, z0), new Vector3(x1 + width * 1.4f, y0, z1 + 0.06f), ch);
        }
    }

    // A pane set in the middle of the wall, double-sided, on the accent submesh.
    public void Pane(Opening o, float inset = 0f) {
        int keep = Sub;
        Sub = 1;
        var p = new[] { new Vector3(o.X0, o.Y0, inset), new Vector3(o.X1, o.Y0, inset), new Vector3(o.X1, o.Y1, inset), new Vector3(o.X0, o.Y1, inset) };
        Face(Vector3.forward, p);
        Face(Vector3.back, p);
        Sub = keep;
    }

    // Cross bars over an opening.
    public void Mullions(Opening o, int cols, int rows, float bar = 0.04f) {
        for (int i = 1; i < cols; i++) {
            float x = Mathf.Lerp(o.X0, o.X1, (float)i / cols);
            Box(new Vector3(x - bar * 0.5f, o.Y0, 0.0f), new Vector3(x + bar * 0.5f, o.Y1, 0.05f), 0.005f);
        }
        for (int j = 1; j < rows; j++) {
            float y = Mathf.Lerp(o.Y0, o.Y1, (float)j / rows);
            Box(new Vector3(o.X0, y - bar * 0.5f, 0.0f), new Vector3(o.X1, y + bar * 0.5f, 0.05f), 0.005f);
        }
    }

    // A door leaf with raised panels and a knob, standing in an opening.
    public void DoorLeaf(Opening o, float z, float thick = 0.05f) {
        Box(new Vector3(o.X0 + 0.02f, o.Y0, z), new Vector3(o.X1 - 0.02f, o.Y1 - 0.02f, z + thick), 0.01f);
        float w = o.X1 - o.X0, h = o.Y1 - o.Y0;
        for (int i = 0; i < 2; i++) {
            for (int j = 0; j < 2; j++) {
                float px0 = o.X0 + w * (0.12f + i * 0.42f), px1 = px0 + w * 0.34f;
                float py0 = o.Y0 + h * (0.08f + j * 0.46f), py1 = py0 + h * (j == 0 ? 0.38f : 0.4f);
                Box(new Vector3(px0, py0, z + thick), new Vector3(px1, py1, z + thick + 0.02f), 0.008f);
            }
        }
        Matrix4x4 keep = M;
        M = M * Matrix4x4.Translate(new Vector3(o.X1 - w * 0.12f, o.Y0 + h * 0.48f, z + thick + 0.02f));
        Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.03f, 0f), new Vector2(0.03f, 0.03f), new Vector2(0.02f, 0.05f), new Vector2(0f, 0.05f) }, 8);
        M = keep;
    }

    // ---- output -----------------------------------------------------------------------------------------------------

    public Mesh ToMesh() {
        var mesh = new Mesh();
        mesh.name = Name;
        mesh.indexFormat = pos.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(pos);
        mesh.SetNormals(nrm);
        mesh.SetUVs(0, uv);
        float minY = float.MaxValue;
        for (int i = 0; i < pos.Count; i++) {
            minY = Mathf.Min(minY, pos[i].y);
        }
        var colors = new Color32[pos.Count];
        for (int i = 0; i < pos.Count; i++) {
            float t = Mathf.Clamp01((pos[i].y - minY) / 0.9f);
            float ao = Mathf.Lerp(0.6f, 1f, t * t * (3f - 2f * t));
            if (nrm[i].y < -0.5f) {
                ao *= 0.75f;
            }
            byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(ao) * 255f);
            colors[i] = new Color32(b, b, b, 255);
        }
        mesh.colors32 = colors;
        bool two = tris[1].Count > 0;
        mesh.subMeshCount = two ? 2 : 1;
        mesh.SetTriangles(tris[0], 0);
        if (two) {
            mesh.SetTriangles(tris[1], 1);
        }
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    // ---- shared runtime pieces --------------------------------------------------------------------------------------

    static Mesh disc;

    // A flat turned disc the size of Unity's cylinder (radius 0.5, y -1..1), for ground marks scaled flat, so a
    // telegraph is never a primitive. One shared mesh; the caller gets its own object with a MeshFilter and renderer.
    public static GameObject Disc(string name) {
        if (disc == null) {
            var k = new KitMesh { Name = "Mark Disc" };
            k.Lathe(new List<Vector2> { new Vector2(0f, -1f), new Vector2(0.5f, -1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f) }, 32);
            disc = k.ToMesh();
        }
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = disc;
        go.AddComponent<MeshRenderer>();
        return go;
    }
}
