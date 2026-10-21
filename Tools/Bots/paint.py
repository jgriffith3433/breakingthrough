# Usage: python3 paint.py <Bot.fbx> <DoD reference.png> <out Skin.png> yo|spider  (see Tools/Bots/README.md)
# Repaints a Gen bot's 1K texture by projecting its DoD reference along the mesh's front axis (Blender -Y),
# the view Gen built the mesh from. Alpha is the glow mask. Spider Bot gets a painted glass front on +X.
import sys, math, zlib, struct; sys.path.insert(0, __import__('os').path.dirname(__import__('os').path.abspath(__file__)))
import geo, png
fbx, ref, out, which = sys.argv[1:5]
T = 1024
P = geo.parts(fbx)
w, h, bpp, px = png.load(ref)
green = which == 'spider'
def raw(x, y):
    o = (y * w + x) * bpp
    return px[o], px[o + 1], px[o + 2], (px[o + 3] if bpp == 4 else 255)
def isfg(x, y):
    r, g, b, a = raw(x, y)
    if green:
        return not (g > r + 35 and g > b + 35)
    return a > 128
# image fg bbox and mesh bbox (x, z)
fx0, fx1, fy0, fy1 = (88, 1370, 30, 700) if green else (20, 584, 20, 620)
allv = [p for _, t in P for tr in t for p, _ in tr]
mx0 = min(p[0] for p in allv); mx1 = max(p[0] for p in allv)
mz0 = min(p[2] for p in allv); mz1 = max(p[2] for p in allv)
my0 = min(p[1] for p in allv); my1 = max(p[1] for p in allv)
def toimg(x, z):
    return (fx0 + (x - mx0) / (mx1 - mx0) * (fx1 - fx0), fy1 - (z - mz0) / (mz1 - mz0) * (fy1 - fy0))
DEFAULT = (12, 22, 70) if green else (36, 38, 44)
cache = {}
def sample(ix, iy):
    ix = min(w - 1, max(0, int(ix))); iy = min(h - 1, max(0, int(iy)))
    key = (ix, iy)
    if key in cache:
        return cache[key]
    res = None
    for rad in range(0, 14, 2):
        best = None
        for dy in range(-rad, rad + 1, 1 if rad < 4 else 2):
            for dx in range(-rad, rad + 1, 1 if rad < 4 else 2):
                if max(abs(dx), abs(dy)) != rad:
                    continue
                x, y = ix + dx, iy + dy
                if 0 <= x < w and 0 <= y < h and isfg(x, y):
                    best = raw(x, y)[:3]
                    break
            if best:
                break
        if best:
            res = best
            break
    if res is None:
        res = DEFAULT
    cache[key] = res
    return res
# front depth buffer (smallest y = nearest the -Y camera), at half reference resolution
DS = 2
dw, dh = w // DS + 1, h // DS + 1
depth = [1e9] * (dw * dh)
for _, t in P:
    for tr in t:
        q = [(toimg(p[0], p[2]), p[1]) for p, _ in tr]
        (ax, ay), az = q[0][0], q[0][1]
        (bx, by), bz = q[1][0], q[1][1]
        (cx, cy), cz = q[2][0], q[2][1]
        ax /= DS; ay /= DS; bx /= DS; by /= DS; cx /= DS; cy /= DS
        den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-12:
            continue
        for y in range(max(0, int(min(ay, by, cy))), min(dh - 1, int(max(ay, by, cy)) + 1) + 1):
            for x in range(max(0, int(min(ax, bx, cx))), min(dw - 1, int(max(ax, bx, cx)) + 1) + 1):
                w0 = ((by - cy) * (x - cx) + (cx - bx) * (y - cy)) / den
                w1 = ((cy - ay) * (x - cx) + (ax - cx) * (y - cy)) / den
                w2 = 1 - w0 - w1
                if w0 < -0.02 or w1 < -0.02 or w2 < -0.02:
                    continue
                d = w0 * az + w1 * bz + w2 * cz
                if d < depth[y * dw + x]:
                    depth[y * dw + x] = d
def frontdepth(ix, iy):
    x = min(dw - 1, max(0, int(ix / DS))); y = min(dh - 1, max(0, int(iy / DS)))
    best = 1e9
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            xx, yy = x + dx, y + dy
            if 0 <= xx < dw and 0 <= yy < dh:
                best = min(best, depth[yy * dw + xx])
    return best
EPS = 0.025 * (my1 - my0)

def lum(c):
    return 0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2]

def glow_of(c):
    r, g, b = c
    if not green:
        # The HUD screen and the core: cyan.
        if b > 100 and g > 90 and b > r + 45:
            return min(255, int((max(g, b) - 90) * 2.2))
        return 0
    if min(r, g, b) > 165:
        return min(255, int((min(r, g, b) - 165) * 3.5))
    if b > 150 and g > 120 and r < 140:
        return 160
    return 0

# Spider Bot pod: centre and radius in mesh space; +X is the glass front once BotArt turns it to lead.
POD = (-0.03, -0.146, 0.5); POD_AXES = (0.27, 0.35, 0.29)
def smooth(e0, e1, x):
    t = max(0.0, min(1.0, (x - e0) / (e1 - e0)))
    return t * t * (3 - 2 * t)
def glass_front(d):
    # d: unit direction from the pod centre. u across (Blender Y, -1 front..1 back), v up.
    u, v = d[1], d[2]
    edge = d[0]
    interior = (40, 90, 150)
    tint = (110, 170, 230)
    fres = smooth(0.95, 0.55, edge)
    col = [interior[k] * (1 - fres) + tint[k] * fres for k in range(3)]
    glow = 0
    # The pilot: head and shoulders, dark against the console light, cyan rim.
    head = math.hypot(u / 0.2, (v - 0.2) / 0.22)
    body = (abs(u) < 0.34 - max(0, v + 0.05) * 0.4) and (-0.45 < v < 0.02)
    shoulders = math.hypot(u / 0.4, (v + 0.04) / 0.16) < 1
    if head < 1 or body or shoulders:
        col = [4, 6, 12]
        if 0.78 < head < 1 or (shoulders and math.hypot(u / 0.4, (v + 0.04) / 0.16) > 0.85 and v > -0.04):
            col = [60, 200, 230]; glow = 150
    # Consoles: a glowing band low in the dome.
    elif -0.62 < v < -0.42 and abs(u) < 0.62:
        stripe = (int((u + 1) * 9) % 2 == 0)
        col = [70, 220, 255] if stripe else [30, 120, 190]
        glow = 255 if stripe else 140
    # Glass highlight up and to one side.
    hl = math.hypot((u + 0.38) / 0.16, (v - 0.48) / 0.09)
    if hl < 1:
        a = 1 - hl
        col = [col[k] * (1 - a) + 235 * a for k in range(3)]
        glow = max(glow, int(140 * a))
    # Tron frame ring around the glass.
    if 0.5 < edge < 0.58:
        col = [225, 245, 255]; glow = 255
    return col, glow

img = bytearray(T * T * 4)
filled = bytearray(T * T)
for name, t in P:
    for tr in t:
        (p0, t0), (p1, t1), (p2, t2) = tr
        ax, ay = t0[0] * T, (1 - t0[1]) * T
        bx, by = t1[0] * T, (1 - t1[1]) * T
        cx, cy = t2[0] * T, (1 - t2[1]) * T
        den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-12:
            continue
        e1 = [p1[k] - p0[k] for k in range(3)]; e2 = [p2[k] - p0[k] for k in range(3)]
        n = (e1[1] * e2[2] - e1[2] * e2[1], e1[2] * e2[0] - e1[0] * e2[2], e1[0] * e2[1] - e1[1] * e2[0])
        nl = math.sqrt(sum(c * c for c in n)) or 1
        n = [c / nl for c in n]
        for y in range(max(0, int(min(ay, by, cy)) - 1), min(T - 1, int(max(ay, by, cy)) + 1) + 1):
            for x in range(max(0, int(min(ax, bx, cx)) - 1), min(T - 1, int(max(ax, bx, cx)) + 1) + 1):
                sx, sy = x + 0.5, y + 0.5
                w0 = ((by - cy) * (sx - cx) + (cx - bx) * (sy - cy)) / den
                w1 = ((cy - ay) * (sx - cx) + (ax - cx) * (sy - cy)) / den
                w2 = 1 - w0 - w1
                if w0 < -0.05 or w1 < -0.05 or w2 < -0.05:
                    continue
                P3 = [w0 * p0[k] + w1 * p1[k] + w2 * p2[k] for k in range(3)]
                ix, iy = toimg(P3[0], P3[2])
                col = None; glow = 0
                if green:
                    d = [(P3[k] - POD[k]) / POD_AXES[k] for k in range(3)]
                    dl = math.sqrt(sum(c * c for c in d)) or 1
                    d = [c / dl for c in d]
                    if dl < 1.2 and d[0] > 0.5 and d[0] > abs(d[1]) * 0.9:
                        col, glow = glass_front(d)
                if col is None:
                    c = sample(ix, iy)
                    visible = P3[1] <= frontdepth(ix, iy) + EPS
                    if visible and not green and name == 'head' and abs(n[1]) < 0.6:
                        visible = False
                    if visible:
                        col = list(c); glow = glow_of(c)
                    elif not green and name == 'head':
                        # Back of the laptop lid: brushed gunmetal, no screen.
                        k = 0.9 + 0.1 * math.sin(P3[0] * 90) * math.sin(P3[2] * 70)
                        col = [44 * k, 47 * k, 54 * k]
                    else:
                        l = lum(c)
                        col = [(c[k] * 0.7 + l * 0.3) * 0.6 for k in range(3)]
                        if green:
                            glow = glow_of(c) // 2 if min(c) > 165 else 0
                o = (y * T + x)
                if filled[o] == 2 and not (w0 >= 0 and w1 >= 0 and w2 >= 0):
                    continue
                img[o * 4:o * 4 + 4] = bytes([max(0, min(255, int(v))) for v in col] + [glow])
                filled[o] = 2 if (w0 >= 0 and w1 >= 0 and w2 >= 0) else 1
# dilate into the gutters so mips and filtering never pull in black
for it in range(8):
    nxt = bytearray(filled)
    for y in range(T):
        row = y * T
        for x in range(T):
            if filled[row + x]:
                continue
            acc = [0, 0, 0, 0]; cnt = 0
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                xx, yy = x + dx, y + dy
                if 0 <= xx < T and 0 <= yy < T and filled[yy * T + xx]:
                    o = (yy * T + xx) * 4
                    for k in range(4):
                        acc[k] += img[o + k]
                    cnt += 1
            if cnt:
                o = (row + x) * 4
                img[o:o + 4] = bytes(a // cnt for a in acc)
                nxt[row + x] = 1
    filled = nxt
for i in range(T * T):
    if not filled[i]:
        img[i * 4:i * 4 + 4] = bytes(list(DEFAULT) + [0])
rawrows = b''.join(b'\x00' + bytes(img[y * T * 4:(y + 1) * T * 4]) for y in range(T))
def chunk(t, d):
    return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
open(out, 'wb').write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', T, T, 8, 6, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(rawrows, 9)) + chunk(b'IEND', b''))
print('wrote', out)
