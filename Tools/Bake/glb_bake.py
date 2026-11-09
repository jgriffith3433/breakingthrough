#!/usr/bin/env python3
"""Bake a Hawkes Gen GLB into the game's mesh text format. Pure Python 3 stdlib: no Blender, no numpy.

  glb_bake.py in.glb out.mesh.txt --tris 1500 --height 2.0 [--parts spec.json] [--pivot base|center]

Steps: read the GLB (every mesh node, node transforms applied) -> weld -> vertex-cluster if huge ->
quadric edge collapse to --tris -> scale to --height (or --size x,y,z max) and stand on y=0 centred on x/z
(Gen GLBs are glTF +Y up, +Z front) -> optional rigid split into named parts by region table ->
box-projected UVs at 1 m a tile (the kit textures are tileable) -> text file.

Format (Assets/Resources/Baked/<id>.mesh.txt, read by BakedMesh.cs):
  g <name> <px> <py> <pz> <set> <#rrggbb> [parent]   a part, its pivot (rig space), its kit texture set and tint (spaces in names are _);
                                 with a parent the part is a child of it, at its pivot minus the parent's (a cap on a head)
  v x y z nx ny nz u v           vertices relative to the pivot
  t a b c                        triangles (indices within the part)
Prints the triangle count of every part. Never fails silently: exits 1 on a bad file.
"""
import sys, json, struct, math, heapq, argparse

def read_glb(path):
    data = open(path, 'rb').read()
    magic, ver, length = struct.unpack_from('<III', data, 0)
    if magic != 0x46546C67:
        raise SystemExit('not a GLB: ' + path)
    off, js, binc = 12, None, b''
    while off < len(data):
        cl, ct = struct.unpack_from('<II', data, off)
        chunk = data[off + 8: off + 8 + cl]
        if ct == 0x4E4F534A: js = json.loads(chunk.decode('utf8'))
        elif ct == 0x004E4942 and not binc: binc = chunk
        off += 8 + cl
    return js, binc

CT = {5120: ('b', 1), 5121: ('B', 1), 5122: ('h', 2), 5123: ('H', 2), 5125: ('I', 4), 5126: ('f', 4)}
NC = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}

def accessor(js, binc, idx):
    a = js['accessors'][idx]
    bv = js['bufferViews'][a['bufferView']]
    fmt, size = CT[a['componentType']]
    n = NC[a['type']]
    stride = bv.get('byteStride') or size * n
    base = bv.get('byteOffset', 0) + a.get('byteOffset', 0)
    out = []
    for i in range(a['count']):
        out.append(struct.unpack_from('<' + fmt * n, binc, base + i * stride))
    return out

def mat_mul(a, b):
    return [sum(a[r * 4 + k] * b[k * 4 + c] for k in range(4)) for r in range(4) for c in range(4)]

def node_matrix(nd):
    if 'matrix' in nd:
        m = nd['matrix']  # column-major
        return [m[c * 4 + r] for r in range(4) for c in range(4)]
    t = nd.get('translation', [0, 0, 0]); q = nd.get('rotation', [0, 0, 0, 1]); s = nd.get('scale', [1, 1, 1])
    x, y, z, w = q
    r = [[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
         [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
         [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]]
    return [r[0][0] * s[0], r[0][1] * s[1], r[0][2] * s[2], t[0],
            r[1][0] * s[0], r[1][1] * s[1], r[1][2] * s[2], t[1],
            r[2][0] * s[0], r[2][1] * s[1], r[2][2] * s[2], t[2], 0, 0, 0, 1]

def load_triangles(path):
    js, binc = read_glb(path)
    verts, tris = [], []
    def visit(ni, parent):
        nd = js['nodes'][ni]
        m = mat_mul(parent, node_matrix(nd))
        if 'mesh' in nd:
            for prim in js['meshes'][nd['mesh']]['primitives']:
                if prim.get('mode', 4) != 4: continue
                pos = accessor(js, binc, prim['attributes']['POSITION'])
                base = len(verts)
                for p in pos:
                    verts.append((m[0] * p[0] + m[1] * p[1] + m[2] * p[2] + m[3],
                                  m[4] * p[0] + m[5] * p[1] + m[6] * p[2] + m[7],
                                  m[8] * p[0] + m[9] * p[1] + m[10] * p[2] + m[11]))
                if 'indices' in prim:
                    ix = [i[0] for i in accessor(js, binc, prim['indices'])]
                else:
                    ix = list(range(len(pos)))
                for i in range(0, len(ix) - 2, 3):
                    tris.append((base + ix[i], base + ix[i + 1], base + ix[i + 2]))
        for c in nd.get('children', []): visit(c, m)
    ident = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]
    sc = js['scenes'][js.get('scene', 0)]
    for n in sc['nodes']: visit(n, ident)
    if not tris: raise SystemExit('no triangles in ' + path)
    return verts, tris

def weld(verts, tris, eps=1e-5):
    key, remap, out = {}, [0] * len(verts), []
    for i, v in enumerate(verts):
        k = (round(v[0] / eps), round(v[1] / eps), round(v[2] / eps))
        j = key.get(k)
        if j is None:
            j = len(out); key[k] = j; out.append(v)
        remap[i] = j
    t2 = []
    for a, b, c in tris:
        a, b, c = remap[a], remap[b], remap[c]
        if a != b and b != c and a != c: t2.append((a, b, c))
    return out, t2

def drop_small(verts, tris, frac):
    """Remove floating bits: connected pieces with fewer triangles than frac of the biggest piece."""
    par = list(range(len(verts)))
    def find(a):
        while par[a] != a:
            par[a] = par[par[a]]; a = par[a]
        return a
    for a, b, c in tris:
        ra, rb, rc = find(a), find(b), find(c)
        par[rb] = ra; par[find(rc)] = ra
    cnt = {}
    for a, b, c in tris:
        r = find(a); cnt[r] = cnt.get(r, 0) + 1
    big = max(cnt.values())
    keep = [t for t in tris if cnt[find(t[0])] >= frac * big]
    print('dropped %d floating triangles in %d pieces' % (len(tris) - len(keep), sum(1 for v in cnt.values() if v < frac * big)))
    return verts, keep

def _cluster_once(verts, tris, res):
    xs = [v[0] for v in verts]; ys = [v[1] for v in verts]; zs = [v[2] for v in verts]
    ext = max(max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
    cell = ext / res
    acc, idx, out = {}, [0] * len(verts), []
    for i, v in enumerate(verts):
        k = (int(v[0] / cell), int(v[1] / cell), int(v[2] / cell))
        if k not in acc:
            acc[k] = [len(out), 0.0, 0.0, 0.0, 0]; out.append(None)
        a = acc[k]; a[1] += v[0]; a[2] += v[1]; a[3] += v[2]; a[4] += 1
        idx[i] = a[0]
    for a in acc.values(): out[a[0]] = (a[1] / a[4], a[2] / a[4], a[3] / a[4])
    t2, seen = [], set()
    for a, b, c in tris:
        a, b, c = idx[a], idx[b], idx[c]
        if a == b or b == c or a == c: continue
        k = tuple(sorted((a, b, c)))
        if k in seen: continue
        seen.add(k); t2.append((a, b, c))
    return out, t2

def sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def norm(a):
    l = math.sqrt(dot(a, a)) or 1.0
    return (a[0] / l, a[1] / l, a[2] / l)

def plane_q(p0, p1, p2):
    n = cross(sub(p1, p0), sub(p2, p0))
    area2 = math.sqrt(dot(n, n))
    if area2 < 1e-20: return None
    n = (n[0] / area2, n[1] / area2, n[2] / area2)
    d = -dot(n, p0)
    a, b, c = n
    return [a * a, a * b, a * c, a * d, b * b, b * c, b * d, c * c, c * d, d * d], area2 * 0.5

def qadd(q1, q2): return [x + y for x, y in zip(q1, q2)]

def qerr(q, v):
    x, y, z = v
    return (q[0] * x * x + 2 * q[1] * x * y + 2 * q[2] * x * z + 2 * q[3] * x + q[4] * y * y + 2 * q[5] * y * z + 2 * q[6] * y +
            q[7] * z * z + 2 * q[8] * z + q[9])

def solve(q):
    a = [[q[0], q[1], q[2]], [q[1], q[4], q[5]], [q[2], q[5], q[7]]]
    b = [-q[3], -q[6], -q[8]]
    det = (a[0][0] * (a[1][1] * a[2][2] - a[1][2] * a[2][1]) - a[0][1] * (a[1][0] * a[2][2] - a[1][2] * a[2][0]) +
           a[0][2] * (a[1][0] * a[2][1] - a[1][1] * a[2][0]))
    if abs(det) < 1e-12: return None
    def d3(m): return (m[0][0] * (m[1][1] * m[2][2] - m[1][2] * m[2][1]) - m[0][1] * (m[1][0] * m[2][2] - m[1][2] * m[2][0]) +
                       m[0][2] * (m[1][0] * m[2][1] - m[1][1] * m[2][0]))
    out = []
    for c in range(3):
        m = [row[:] for row in a]
        for r in range(3): m[r][c] = b[r]
        out.append(d3(m) / det)
    return tuple(out)

def decimate(verts, tris, target):
    """Garland-Heckbert quadric edge collapse with boundary preservation and a fold-over check."""
    V = [list(v) for v in verts]
    Q = [[0.0] * 10 for _ in V]
    faces = {}
    vf = [set() for _ in V]
    for fi, t in enumerate(tris):
        r = plane_q(V[t[0]], V[t[1]], V[t[2]])
        if r is None: continue
        faces[fi] = list(t)
        for k in t:
            Q[k] = qadd(Q[k], r[0]); vf[k].add(fi)
    # boundary edges get a heavy perpendicular plane so open rims don't erode
    ecount = {}
    for t in faces.values():
        for i in range(3):
            e = tuple(sorted((t[i], t[(i + 1) % 3]))); ecount[e] = ecount.get(e, 0) + 1
    for (a, b), c in ecount.items():
        if c == 1:
            for t in faces.values():
                if a in t and b in t:
                    n = cross(sub(V[b], V[a]), cross(sub(V[t[1]], V[t[0]]), sub(V[t[2]], V[t[0]])))
                    ln = math.sqrt(dot(n, n))
                    if ln < 1e-20: break
                    n = (n[0] / ln, n[1] / ln, n[2] / ln); d = -dot(n, V[a]); p, q_, r_ = n
                    w = 100.0
                    qq = [w * x for x in (p * p, p * q_, p * r_, p * d, q_ * q_, q_ * r_, q_ * d, r_ * r_, r_ * d, d * d)]
                    Q[a] = qadd(Q[a], qq); Q[b] = qadd(Q[b], qq)
                    break
    alive = [True] * len(V)
    ver = [0] * len(V)
    heap = []
    def cost(a, b):
        q = qadd(Q[a], Q[b])
        best, bv = None, None
        for cand in (solve(q), tuple((V[a][i] + V[b][i]) / 2 for i in range(3)), tuple(V[a]), tuple(V[b])):
            if cand is None: continue
            e = qerr(q, cand)
            if best is None or e < best: best, bv = e, cand
        return best, bv
    def push(a, b):
        e, p = cost(a, b)
        heapq.heappush(heap, (e, a, b, ver[a], ver[b], p))
    seen = set()
    for t in faces.values():
        for i in range(3):
            a, b = t[i], t[(i + 1) % 3]
            k = (min(a, b), max(a, b))
            if k not in seen: seen.add(k); push(*k)
    nf = len(faces)
    def flips(a, b, p):
        for fi in vf[a] | vf[b]:
            t = faces[fi]
            if a in t and b in t: continue
            pts = [V[k] if k not in (a, b) else p for k in t]
            nb = cross(sub(V[t[1]], V[t[0]]), sub(V[t[2]], V[t[0]]))
            na = cross(sub(pts[1], pts[0]), sub(pts[2], pts[0]))
            la, lb = math.sqrt(dot(na, na)), math.sqrt(dot(nb, nb))
            if la < 1e-14 or lb < 1e-14: return True
            if dot(na, nb) / (la * lb) < 0.2: return True
        return False
    while nf > target and heap:
        e, a, b, va, vb, p = heapq.heappop(heap)
        if not (alive[a] and alive[b]) or ver[a] != va or ver[b] != vb: continue
        if flips(a, b, p):
            continue
        V[a] = list(p); Q[a] = qadd(Q[a], Q[b]); alive[b] = False; ver[a] += 1
        for fi in list(vf[b]):
            t = faces[fi]
            t[:] = [a if k == b else k for k in t]
            vf[a].add(fi)
        for fi in list(vf[a]):
            t = faces[fi]
            if len(set(t)) < 3:
                for k in set(t): vf[k].discard(fi)
                del faces[fi]; nf -= 1
        vf[b] = set()
        nbrs = set()
        for fi in vf[a]:
            for k in faces[fi]:
                if k != a: nbrs.add(k)
        for k in nbrs: push(min(a, k), max(a, k))
    used = sorted({k for t in faces.values() for k in t})
    m = {k: i for i, k in enumerate(used)}
    return [tuple(V[k]) for k in used], [(m[t[0]], m[t[1]], m[t[2]]) for t in faces.values()]

def fit(verts, height, size, keep_origin):
    mn = [min(v[i] for v in verts) for i in range(3)]
    mx = [max(v[i] for v in verts) for i in range(3)]
    ext = [mx[i] - mn[i] for i in range(3)]
    if size:
        s = min(size[i] / ext[i] for i in range(3) if size[i] > 0 and ext[i] > 1e-9)
    else:
        s = height / ext[1]
    cx, cz = (mn[0] + mx[0]) / 2, (mn[2] + mx[2]) / 2
    return [((v[0] - cx) * s, (v[1] - mn[1]) * s, (v[2] - cz) * s) for v in verts]

def vertex_normals(verts, tris):
    n = [[0.0, 0.0, 0.0] for _ in verts]
    for a, b, c in tris:
        fn = cross(sub(verts[b], verts[a]), sub(verts[c], verts[a]))
        for k in (a, b, c):
            for i in range(3): n[k][i] += fn[i]
    return [norm(x) for x in n]

def box_uv(p, n, tile=1.0):
    ax = max(range(3), key=lambda i: abs(n[i]))
    if ax == 0: return (p[2] / tile, p[1] / tile)
    if ax == 1: return (p[0] / tile, p[2] / tile)
    return (p[0] / tile, p[1] / tile)

def split_parts(verts, tris, spec, extent):
    """spec: {"parts":[{"name","box":[x0,y0,z0,x1,y1,z1] (fractions of height/halfwidth), "pivot":[x,y,z] metres}], "default":"Body"}"""
    h = extent
    names = [p['name'] for p in spec['parts']]
    groups = {n: [] for n in names + [spec.get('default', 'Body')]}
    for t in tris:
        c = [sum(verts[k][i] for k in t) / 3 for i in range(3)]
        chosen = spec.get('default', 'Body')
        for p in spec['parts']:
            b = p['box']
            k = [1.0, 1.0, 1.0] if p.get('metres') else h   # "metres": true = the box is in rig-space metres
            if all(b[i] * k[i] <= c[i] <= b[i + 3] * k[i] for i in range(3)):
                chosen = p['name']; break
        groups[chosen].append(t)
    groups = {n: g for n, g in groups.items() if not n.startswith('_')}   # a part named _x only cuts loose bits away
    pivots = {p['name']: p['pivot'] for p in spec['parts']}
    pivots.setdefault(spec.get('default', 'Body'), [0, 0, 0])
    return groups, pivots, {p['name']: (p.get('set', 'metal'), p.get('color', '#ffffff'), p.get('parent', '')) for p in spec['parts']}

def emit(path, groups, pivots, verts, tile, looks=None):
    lines = []
    report = []
    for name, ts in groups.items():
        if not ts: continue
        piv = pivots.get(name, [0, 0, 0])
        used = sorted({k for t in ts for k in t})
        m = {k: i for i, k in enumerate(used)}
        sub_v = [verts[k] for k in used]
        sub_t = [(m[a], m[b], m[c]) for a, b, c in ts]
        nrm = vertex_normals(sub_v, sub_t)
        look = (looks or {}).get(name, ('metal', '#ffffff', ''))
        lines.append('g %s %.5f %.5f %.5f %s %s%s' % (name.replace(' ', '_'), piv[0], piv[1], piv[2], look[0], look[1],
                                                       (' ' + look[2].replace(' ', '_')) if look[2] else ''))
        for p, n in zip(sub_v, nrm):
            u, v = box_uv(p, n, tile)
            lines.append('v %.4f %.4f %.4f %.3f %.3f %.3f %.3f %.3f' % (p[0] - piv[0], p[1] - piv[1], p[2] - piv[2], n[0], n[1], n[2], u, v))
        for a, b, c in sub_t: lines.append('t %d %d %d' % (a, b, c))
        report.append((name, len(sub_t), len(sub_v)))
    open(path, 'w').write('\n'.join(lines) + '\n')
    return report

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('glb'); ap.add_argument('out')
    ap.add_argument('--tris', type=int, default=1500)
    ap.add_argument('--height', type=float, default=1.0)
    ap.add_argument('--size', help='max x,y,z in metres; the model is scaled to fit')
    ap.add_argument('--parts'); ap.add_argument('--tile', type=float, default=1.0)
    ap.add_argument('--yaw', type=float, default=0.0, help='turn about Y before fitting (degrees)')
    ap.add_argument('--drop-small', type=float, default=0.0, help='drop floating pieces under this fraction of the biggest piece (0.01 for characters)')
    a = ap.parse_args()
    verts, tris = load_triangles(a.glb)
    print('read %d verts, %d tris' % (len(verts), len(tris)))
    verts, tris = weld(verts, tris)
    if a.drop_small > 0:
        verts, tris = drop_small(verts, tris, a.drop_small)
    if len(tris) > a.tris * 6:
        verts, tris = _cluster_once(verts, tris, int(math.sqrt(a.tris * 6) * 14))
    verts, tris = decimate(verts, tris, a.tris)
    # glTF is right-handed, Unity left-handed: mirror X and reverse the winding (+Z stays the front)
    verts = [(-v[0], v[1], v[2]) for v in verts]
    tris = [(t[0], t[2], t[1]) for t in tris]
    if a.yaw:
        c, s = math.cos(math.radians(a.yaw)), math.sin(math.radians(a.yaw))
        verts = [(v[0] * c + v[2] * s, v[1], -v[0] * s + v[2] * c) for v in verts]
    size = [float(x) for x in a.size.split(',')] if a.size else None
    verts = fit(verts, a.height, size, False)
    ext = (max(abs(v[0]) for v in verts), max(v[1] for v in verts), max(abs(v[2]) for v in verts))
    if a.parts:
        spec = json.load(open(a.parts))
        groups, pivots, looks = split_parts(verts, tris, spec, ext)
    else:
        groups, pivots, looks = {'Mesh': tris}, {}, {}
    rep = emit(a.out, groups, pivots, verts, a.tile, looks)
    total = sum(r[1] for r in rep)
    print('%s: %d tris total (%s) extent x%.2f y%.2f z%.2f' % (a.out, total, ', '.join('%s %d' % (r[0], r[1]) for r in rep), ext[0] * 2, ext[1], ext[2] * 2))

if __name__ == '__main__':
    main()
