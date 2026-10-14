# Scenery for the Moonlit Hollow arena. Run in Blender after bt_birds.py (it reuses Part):
#   exec(open(r"...\Tools\Blender\bt_birds.py").read()); exec(open(r"...\Tools\Blender\bt_arena.py").read()); export_arena()
# Smooth shaded, vertex colored. Exported one FBX per prop into Assets/Art/Arena.
import bpy, bmesh, math, random, os
from mathutils import Vector, Matrix, noise

ARENA_OUT = r"C:\Projects\breakingthrough\Assets\Art\Arena"


def pine(name, seed, height=10.0, tiers=5):
    random.seed(seed)
    p = Part(name)
    trunk = (0.24, 0.15, 0.1)
    p.tube((0, 0, 0), (0, 0, height * 0.35), height * 0.035, height * 0.025, trunk, segs=6)
    base = height * 0.2
    for i in range(tiers):
        t = i / (tiers - 1)
        z0 = base + t * height * 0.62
        r = height * (0.3 - 0.2 * t) * random.uniform(0.92, 1.08)
        h = height * (0.3 - 0.08 * t)
        dark = Vector((0.05, 0.13, 0.09)) * (0.8 + 0.35 * t)
        faces = p.tube((0, 0, z0), (random.uniform(-0.1, 0.1), random.uniform(-0.1, 0.1), z0 + h), r, 0.02, tuple(dark), segs=8)
        # droop the skirt so tiers read as branches
        for f in faces:
            for v in f.verts:
                d = Vector((v.co.x, v.co.y)).length
                if d > r * 0.6 and v.co.z < z0 + h * 0.2:
                    v.co.z -= h * 0.18 * random.uniform(0.6, 1.0)
    return p.build((0, 0, 0))


def broadleaf(name, seed, height=8.0):
    random.seed(seed)
    p = Part(name)
    p.tube((0, 0, 0), (0.2, 0.1, height * 0.5), height * 0.05, height * 0.035, (0.25, 0.17, 0.11), segs=6)
    p.tube((0.2, 0.1, height * 0.45), (height * 0.18, 0, height * 0.65), height * 0.025, height * 0.015, (0.25, 0.17, 0.11), segs=5)
    for i in range(6):
        a = i / 6 * math.tau + random.uniform(-0.3, 0.3)
        c = Vector((math.cos(a) * height * 0.16, math.sin(a) * height * 0.16, height * random.uniform(0.62, 0.8)))
        s = height * random.uniform(0.17, 0.24)
        green = Vector((0.07, 0.16, 0.08)) * random.uniform(0.85, 1.2)
        p.blob(tuple(c), (s, s, s * 0.8), tuple(green), segs=(8, 6), shade=0.45)
    p.blob((0, 0, height * 0.88), (height * 0.2,) * 3, (0.08, 0.18, 0.09), segs=(8, 6), shade=0.45)
    return p.build((0, 0, 0))


def rock(name, seed, size=2.0, flat=0.6):
    random.seed(seed)
    p = Part(name)
    res = bmesh.ops.create_icosphere(p.bm, subdivisions=3, radius=1.0)
    off = Vector((random.uniform(0, 50), random.uniform(0, 50), random.uniform(0, 50)))
    for v in res["verts"]:
        n = noise.noise(v.co * 1.4 + off) * 0.28 + noise.noise(v.co * 3.1 + off) * 0.1
        v.co = v.co * (1 + n)
        v.co.z *= flat
        v.co *= size
    for v in res["verts"]:
        v.co.z = max(v.co.z, -size * 0.25)
    p.bm.normal_update()
    faces = list(p.bm.faces)
    for f in faces:
        for l in f.loops:
            up = max(0.0, l.vert.normal.z) if l.vert.is_valid else 0.0
            stone = Vector((0.26, 0.27, 0.3)) * (0.75 + 0.35 * (l.vert.co.z / size + 0.25))
            moss = Vector((0.12, 0.2, 0.1))
            c = stone.lerp(moss, max(0.0, min(1.0, (up - 0.55) * 2.5)))
            l[p.col] = (c.x, c.y, c.z, 1)
    return p.build((0, 0, 0))


def bush(name, seed, size=1.2):
    random.seed(seed)
    p = Part(name)
    for i in range(5):
        a = random.uniform(0, math.tau)
        c = (math.cos(a) * size * 0.35, math.sin(a) * size * 0.35, size * random.uniform(0.35, 0.55))
        s = size * random.uniform(0.35, 0.5)
        p.blob(c, (s, s, s * 0.8), tuple(Vector((0.06, 0.14, 0.07)) * random.uniform(0.85, 1.2)), segs=(8, 6), shade=0.5)
    return p.build((0, 0, 0))


def grass(name, seed, blades=9, height=0.7):
    random.seed(seed)
    p = Part(name, low=True)
    for i in range(blades):
        a = random.uniform(0, math.tau)
        base = Vector((math.cos(a) * 0.12, math.sin(a) * 0.12, 0))
        lean = Vector((math.cos(a), math.sin(a), 0)) * random.uniform(0.1, 0.3)
        h = height * random.uniform(0.6, 1.1)
        side = Vector((-math.sin(a), math.cos(a), 0)) * 0.03
        segs = 4
        pts_l, pts_r = [], []
        for k in range(segs + 1):
            t = k / segs
            c = base + lean * (t * t) * h + Vector((0, 0, t * h))
            w = (1 - t) * 1.0
            pts_l.append(c + side * w)
            pts_r.append(c - side * w)
        vl = [p.bm.verts.new(q) for q in pts_l]
        vr = [p.bm.verts.new(q) for q in pts_r]
        for k in range(segs):
            f = p.bm.faces.new((vl[k], vr[k], vr[k + 1], vl[k + 1]))
            for l in f.loops:
                t = l.vert.co.z / h
                c = Vector((0.05, 0.12, 0.06)).lerp(Vector((0.14, 0.27, 0.12)), t)
                l[p.col] = (c.x, c.y, c.z, 1)
    return p.build((0, 0, 0))


def flowers(name, seed, color):
    random.seed(seed)
    p = Part(name, low=True)
    for i in range(6):
        a = random.uniform(0, math.tau)
        r = random.uniform(0, 0.25)
        base = Vector((math.cos(a) * r, math.sin(a) * r, 0))
        h = random.uniform(0.25, 0.45)
        top = base + Vector((random.uniform(-0.05, 0.05), random.uniform(-0.05, 0.05), h))
        p.tube(base, top, 0.008, 0.006, (0.08, 0.18, 0.08), segs=3)
        p.blob(tuple(top), (0.035, 0.035, 0.02), color, segs=(6, 3), shade=0.0)
    return p.build((0, 0, 0))


def export_arena():
    clear()
    os.makedirs(ARENA_OUT, exist_ok=True)
    made = [
        pine("Pine_A", 1), pine("Pine_B", 2, height=12.0, tiers=6), pine("Pine_C", 3, height=8.0, tiers=4),
        broadleaf("Broadleaf_A", 4),
        rock("Rock_A", 5, 2.2, 0.6), rock("Rock_B", 6, 3.5, 0.45), rock("Rock_C", 7, 1.0, 0.7), rock("Boulder", 8, 6.0, 0.55),
        bush("Bush_A", 9),
        grass("Grass_A", 10), grass("Grass_B", 11, blades=13, height=0.9),
        flowers("Flowers_White", 12, (0.85, 0.85, 0.8)), flowers("Flowers_Blue", 13, (0.45, 0.6, 0.95)), flowers("Flowers_Gold", 14, (0.82, 0.7, 0.3)),
    ]
    out = []
    for o in made:
        bpy.ops.object.select_all(action='DESELECT')
        o.select_set(True)
        bpy.context.view_layer.objects.active = o
        path = os.path.join(ARENA_OUT, o.name + ".fbx")
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                                 axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE',
                                 add_leaf_bones=False, bake_anim=False, colors_type='SRGB')
        out.append(o.name)
    return out
