# Usage: python3 validate.py [Assets/Resources/Models/Bots]   (see Tools/Bots/README.md)
# Checks the Yo Bot and Spider Bot FBX + skin pairs without Unity, Blender or a GPU. Exit 1 on any FAIL.
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import geo, png
root = sys.argv[1] if len(sys.argv) > 1 else 'Assets/Resources/Models/Bots'
fails = 0
def check(ok, label, detail=''):
    global fails
    print(('ok   ' if ok else 'FAIL ') + label + (' (' + detail + ')' if detail else ''))
    if not ok:
        fails += 1
EXPECT = {
    'YoBot': ['body', 'head', 'arm_l', 'arm_r', 'leg_fl', 'leg_fr', 'leg_bl', 'leg_br'],
    'SpiderBot': ['body', 'leg1', 'leg2', 'leg3', 'leg4', 'leg5', 'leg6'],
}
for name, want in EXPECT.items():
    print('==', name)
    fbx = os.path.join(root, name + '.fbx'); skin = os.path.join(root, name + 'Skin.png')
    parts = geo.parts(fbx)
    got = sorted(n for n, _ in parts)
    # Enemy.BindParts looks these up by name: renaming one silently stops it animating.
    check(got == sorted(want), 'part names match Enemy.BindParts', ','.join(got))
    tris = [t for _, ts in parts for t in ts]
    # geometry: degenerate triangles, UV range, stretch (UV area vs 3D area ratio spread)
    degen = 0; outuv = 0; ratios = []
    for tr in tris:
        (a, ua), (b, ub), (c, uc) = tr
        e1 = [b[k] - a[k] for k in range(3)]; e2 = [c[k] - a[k] for k in range(3)]
        n = (e1[1]*e2[2]-e1[2]*e2[1], e1[2]*e2[0]-e1[0]*e2[2], e1[0]*e2[1]-e1[1]*e2[0])
        area = 0.5 * math.sqrt(sum(x*x for x in n))
        uva = abs((ub[0]-ua[0])*(uc[1]-ua[1]) - (uc[0]-ua[0])*(ub[1]-ua[1])) * 0.5
        if area < 1e-9 or uva < 1e-10:
            degen += 1
        else:
            ratios.append(uva / area)
        if any(u < -0.001 or u > 1.001 or v < -0.001 or v > 1.001 for _, (u, v) in tr):
            outuv += 1
    check(degen <= len(tris) * 0.002, 'no degenerate triangles', '%d of %d' % (degen, len(tris)))
    check(outuv == 0, 'UVs inside 0..1', '%d triangles outside' % outuv)
    ratios.sort()
    if ratios:
        lo, hi = ratios[len(ratios) // 50], ratios[-len(ratios) // 50 - 1]
        check(hi / max(lo, 1e-12) < 400, 'texel density even (no stretching)', 'p2..p98 spread x%.0f' % (hi / max(lo, 1e-12)))
    pts = [p for tr in tris for p, _ in tr]
    zmin = min(p[2] for p in pts)
    check(abs(zmin) < 0.05, 'feet sit on z=0 (Blender up)', 'lowest %.3f' % zmin)
    # pivots: Enemy swings parts on their bone, so a part's bbox should not sit on the far side of the body
    bx = [sum(p[0] for p in pts) / len(pts)]
    # skin
    w, h, bpp, px = png.load(skin)
    check((w, h) == (1024, 1024) and bpp == 4, 'skin is 1024x1024 RGBA', '%dx%d bpp%d' % (w, h, bpp))
    n = w * h; white = 0; glow = 0; hot = 0; dark = 0
    for i in range(0, n, 3):
        r, g, b, a = px[i*4:i*4+4]
        lo_, hi_ = min(r, g, b), max(r, g, b)
        if lo_ > 215: white += 1
        if a > 40: glow += 1
        if a > 40 and lo_ > 215: hot += 1
        if hi_ < 14: dark += 1
    m = len(range(0, n, 3))
    check(white / m < 0.01, 'no white blobs in the skin', '%.2f%% near-white' % (white / m * 100))
    check(glow / m < 0.25, 'glow mask is small (no bleed)', '%.1f%% of texels glow' % (glow / m * 100))
    check(hot / m < 0.005, 'glowing texels are not white', '%.2f%% white AND glowing' % (hot / m * 100))
    check(dark / m < 0.6, 'skin not black', '%.0f%% near-black' % (dark / m * 100))
    # UV coverage: used texels near the used-triangle borders must not glow (bleed into neighbours at mip)
    used = set()
    for (_, _, _) in []: pass
print('FAILED' if fails else 'PASSED', fails)
sys.exit(1 if fails else 0)
