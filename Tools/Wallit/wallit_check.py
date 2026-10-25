# Usage: python3 Tools/Wallit/wallit_check.py [Wallit.fbx]
# Reports what is wrong with Gen's Wallit.fbx without Blender or a GPU: the shells in the body mesh (the bake draws
# the character twice, and the two copies z-fight: Assets/Scripts/WallitMeshClean.cs drops the smaller at load),
# skin weights (influences per vertex, unnormalised, unweighted). Tools/Bots/render.py draws it from every side. Plain Python 3, reuses Tools/Bots readers.
import sys, os, math, collections
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Bots'))
import geo
from fbxdump import read, find

fbx = sys.argv[1] if len(sys.argv) > 1 else 'Assets/Art/Wallit/Wallit.fbx'
tris = dict(geo.parts(fbx))['Body']
key = lambda p: tuple(round(c, 4) for c in p)
par = {}
def f(a):
    while par.setdefault(a, a) != a:
        par[a] = par[par[a]]; a = par[a]
    return a
for t in tris:
    k = [key(p[0]) for p in t]
    for j in (1, 2): par[f(k[j])] = f(k[0])
shells = collections.defaultdict(list)
for t in tris: shells[f(key(t[0][0]))].append(t)
shells = sorted(shells.values(), key=len, reverse=True)
for i, ts in enumerate(shells):
    P = [p[0] for t in ts for p in t]
    print('shell', i, len(ts), 'tris, bounds', [(round(min(p[k] for p in P), 3), round(max(p[k] for p in P), 3)) for k in range(3)])
dup = len(shells) > 1 and len(shells[1]) >= 3000 and len(shells[1]) >= 0.6 * len(shells[0])
print('DUPLICATE SHELL: drop shell 1 (%d tris)' % len(shells[1]) if dup else 'no duplicate shell')

top = read(fbx)
O = [n for n in top if n[0] == 'Objects'][0]
nv = sum(len(find(k, 'Vertices')[0][1][0]) // 3 for k in O[2] if k[0] == 'Geometry' and k[1][1].startswith('Wallit'))
inf = collections.defaultdict(float); cnt = collections.Counter()
for k in O[2]:
    if k[0] == 'Deformer' and k[1][2] == 'Cluster' and find(k, 'Indexes'):
        for i, w in zip(find(k, 'Indexes')[0][1][0], find(k, 'Weights')[0][1][0]): inf[i] += w; cnt[i] += 1
print('skin: %d/%d vertices weighted, %d not normalised, max influences %d' % (len(inf), nv, sum(1 for v in inf.values() if abs(v - 1) > 0.02), max(cnt.values())))
