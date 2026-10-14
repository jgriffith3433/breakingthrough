# Breaking Through bird generator. Run inside Blender:
#   exec(open(r"C:\Projects\breakingthrough\Tools\Blender\bt_birds.py").read()); build("Turkey")
# Every species is low-poly, flat shaded, and painted with vertex colors so it matches the knoll.
# Parts are separate objects so the game can animate them: Body, Head, WingL, WingR, LegL, LegR.
# Blender -Y is the bird's front. Feet sit on z = 0.
import bpy, bmesh, math, random, os
from mathutils import Vector, Matrix, Euler

OUT = r"C:\Projects\breakingthrough\Assets\Resources\Models\Birds"


def scene():
    scn = bpy.data.scenes.get("BT_Birds") or bpy.data.scenes.new("BT_Birds")
    bpy.context.window.scene = scn
    return scn


def clear():
    scn = scene()
    for o in list(scn.collection.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes):
        if m.users == 0:
            bpy.data.meshes.remove(m)


def vc_mat():
    m = bpy.data.materials.get("BT_BirdVC")
    if m:
        return m
    m = bpy.data.materials.new("BT_BirdVC")
    m.use_nodes = True
    nt = m.node_tree
    b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    attr = nt.nodes.new("ShaderNodeVertexColor")
    attr.layer_name = "Col"
    nt.links.new(attr.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.85
    return m


def V(*a):
    return Vector(a)


class Part:
    def __init__(self, name, low=False):
        self.name = name
        self.low = low
        self.bm = bmesh.new()
        self.col = self.bm.loops.layers.color.new("Col")

    def paint(self, faces, rgb, jitter=0.07, shade=0.0, zmin=0.0, zmax=1.0):
        # Smooth: color follows each vertex's height, so there are no per-face color steps.
        for f in faces:
            for l in f.loops:
                c = Vector(rgb)
                if shade:
                    t = (l.vert.co.z - zmin) / max(1e-4, zmax - zmin)
                    c = c * (1 - shade + shade * t)
                l[self.col] = (min(c.x, 1), min(c.y, 1), min(c.z, 1), 1)

    def blob(self, center, radii, rgb, segs=(8, 6), rot=(0, 0, 0), jitter=0.07, shade=0.3, belly=None):
        if not self.low:
            segs = (max(segs[0] * 2, 16), max(segs[1] * 2, 12))
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=segs[0], v_segments=segs[1], radius=1.0)
        verts = res["verts"]
        M = Matrix.Translation(Vector(center)) @ Euler(rot).to_matrix().to_4x4() @ Matrix.Diagonal((*radii, 1))
        bmesh.ops.transform(self.bm, matrix=M, verts=verts)
        faces = list({f for v in verts for f in v.link_faces})
        for f in faces:
            f.normal_update()
        zs = [v.co.z for v in verts]
        self.paint(faces, rgb, jitter, shade, min(zs), max(zs))
        if belly:
            # Blend toward the belly color by how much each vertex faces down.
            cz = Vector(center).z
            for f in faces:
                for l in f.loops:
                    down = max(0.0, min(1.0, (cz - l.vert.co.z) / max(1e-4, radii[2]) * 1.4 - 0.2))
                    base = Vector(l[self.col][:3])
                    mix = base.lerp(Vector(belly), down)
                    l[self.col] = (mix.x, mix.y, mix.z, 1)
        return faces

    def tube(self, a, b, r1, r2, rgb, segs=6, jitter=0.05):
        a = Vector(a)
        b = Vector(b)
        d = b - a
        if not self.low:
            segs = max(segs * 2, 12)
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r1, radius2=max(r2, 0.0005), depth=max(d.length, 1e-4))
        verts = res["verts"]
        q = Vector((0, 0, 1)).rotation_difference(d.normalized())
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation((a + b) / 2) @ q.to_matrix().to_4x4(), verts=verts)
        faces = list({f for v in verts for f in v.link_faces})
        self.paint(faces, rgb, jitter)
        return faces

    def chain(self, pts, radii, rgb, segs=6):
        for i in range(len(pts) - 1):
            self.tube(pts[i], pts[i + 1], radii[i], radii[i + 1], rgb, segs)
            if i > 0:
                self.blob(pts[i], (radii[i],) * 3, rgb, segs=(segs, 4), shade=0)

    def cone(self, base, tip, r, rgb, segs=5):
        return self.tube(base, tip, r, 0.0005, rgb, segs)

    def plate(self, pts, rgb, thick=0.015, jitter=0.05):
        verts = [self.bm.verts.new(Vector(p)) for p in pts]
        f = self.bm.faces.new(verts)
        f.normal_update()
        n = f.normal.copy()
        ext = bmesh.ops.extrude_face_region(self.bm, geom=[f])
        nv = [e for e in ext["geom"] if isinstance(e, bmesh.types.BMVert)]
        bmesh.ops.translate(self.bm, vec=n * thick, verts=nv)
        faces = list({ff for v in verts + nv for ff in v.link_faces})
        self.paint(faces, rgb, jitter)
        return faces

    def feather(self, base, direction, side, length, w0, w1, rgb, tip_rgb=None, tip=0.25, thick=0.012):
        base = Vector(base)
        d = Vector(direction).normalized()
        s = Vector(side).normalized()
        mid = base + d * length * (1 - tip if tip_rgb else 1)
        self.plate([base + s * w0, mid + s * w1, mid - s * w1, base - s * w0], rgb, thick)
        if tip_rgb:
            end = base + d * length
            self.plate([mid + s * w1, end + s * w1 * 0.55, end - s * w1 * 0.55, mid - s * w1], tip_rgb, thick)

    def build(self, pivot=(0, 0, 0)):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        bmesh.ops.translate(self.bm, vec=-Vector(pivot), verts=self.bm.verts)
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        o = bpy.data.objects.new(self.name, me)
        scene().collection.objects.link(o)
        me.materials.append(vc_mat())
        for p in me.polygons:
            p.use_smooth = True
        o.location = Vector(pivot)
        return o


# ---------- shared body parts ----------

def legs(rgb, hip, length, thick, toe=0.07, knee_back=0.0, claw=None):
    """hip = (x spread, y, z). Legs pivot at the hip so the game can swing them."""
    for s, name in ((-1, "LegL"), (1, "LegR")):
        p = Part(name)
        h = V(s * hip[0], hip[1], hip[2])
        knee = V(s * hip[0], hip[1] + knee_back, hip[2] * 0.48)
        foot = V(s * hip[0], hip[1] - knee_back * 0.2, 0.015)
        p.tube(h, knee, thick * 1.3, thick, rgb)
        p.blob(knee, (thick * 1.05,) * 3, rgb, segs=(6, 4), shade=0)
        p.tube(knee, foot, thick, thick * 0.8, rgb)
        for a in (-38, 0, 38):
            d = V(math.sin(math.radians(a)), -math.cos(math.radians(a)), 0) * toe
            p.cone(foot, foot + d, thick * 0.65, claw or rgb, 4)
        p.cone(foot, foot + V(0, toe * 0.55, 0.01), thick * 0.55, claw or rgb, 4)
        p.build(h)


def wings(shoulder, rgb, tip_rgb, length, depth, bars=None, open_=0.0, lift=0.0, primaries=5):
    """Folded along the body (open_=0) or spread like a raptor (open_=1)."""
    for s, name in ((-1, "WingL"), (1, "WingR")):
        p = Part(name)
        sh = V(s * shoulder[0], shoulder[1], shoulder[2])
        out = V(s * open_, 1.0 - open_ * 0.75, lift).normalized()
        up = V(0, 0, 1)
        mid = sh + out * length * 0.42
        folded = V(0.045, length * 0.3 + 0.02, depth)
        spread = V(length * 0.42, depth * 1.1, 0.045)
        radii = folded.lerp(spread, open_)
        tilt = math.atan2(out.z, abs(out.x) + 1e-4) * s
        p.blob(mid, tuple(radii), rgb, rot=(0, -tilt * open_, 0), shade=0.25)
        if open_ > 0.5:
            # a second, darker layer of coverts so the spread wing reads as layered feathers
            p.blob(mid + V(0, depth * 0.35, -0.01), (radii.x * 0.8, radii.y * 0.6, radii.z * 0.9), Vector(rgb) * 0.75, rot=(0, -tilt, 0), shade=0)
        if bars:
            p.blob(mid + V(s * 0.03, 0.03, -depth * 0.35), (0.025 + open_ * length * 0.2, length * 0.16, depth * 0.12), bars, shade=0)
        for i in range(primaries):
            t = i / max(1, primaries - 1)
            if open_ >= 0.5:
                # fingered primaries fanning back from the wing tip
                base = sh + out * length * (0.62 + 0.1 * t) + V(0, depth * (-0.6 + 1.4 * t), 0)
                dirv = (out + V(0, 0.15 + 0.7 * t, -0.1)).normalized()
                p.feather(base, dirv, V(0, 1, 0), length * (0.42 - 0.12 * t), depth * 0.18, depth * 0.24,
                          tip_rgb if i % 2 == 0 else Vector(tip_rgb) * 0.85)
                continue
            base = sh + out * length * (0.5 + 0.12 * t) + up * (-depth * 0.35 + depth * 0.55 * t)
            dirv = (out + V(0, 0.25 * t, -0.25 + 0.2 * t)).normalized()
            side = up
            p.feather(base, dirv, side, length * (0.5 + 0.1 * (1 - abs(t - 0.5))), depth * 0.22, depth * 0.3,
                      tip_rgb if i % 2 == 0 else Vector(tip_rgb) * 0.85)
        p.build(sh)


def head(neck_base, neck_pts, neck_r, neck_rgb, skull, skull_r, skull_rgb, beak_len, beak_r, beak_rgb,
         eye_rgb=(0.03, 0.03, 0.03), eye_r=0.013, hook=0.0, extras=None):
    p = Part("Head")
    pts = [Vector(neck_base)] + [Vector(x) for x in neck_pts] + [Vector(skull)]
    radii = [neck_r * (1 - 0.35 * i / (len(pts) - 1)) for i in range(len(pts))]
    p.chain(pts, radii, neck_rgb)
    p.blob(skull, skull_r, skull_rgb, segs=(8, 6), shade=0.15)
    face = Vector(skull) + V(0, -skull_r[1] * 0.85, 0)
    tip = face + V(0, -beak_len, -beak_len * (0.15 + hook))
    p.cone(face, tip, beak_r, beak_rgb, 5)
    if hook:
        p.cone(tip + V(0, 0.002, 0.004), tip + V(0, beak_len * 0.18, -beak_len * 0.35), beak_r * 0.5, beak_rgb, 4)
    for s in (-1, 1):
        p.blob(Vector(skull) + V(s * skull_r[0] * 0.78, -skull_r[1] * 0.35, skull_r[2] * 0.25), (eye_r,) * 3, eye_rgb, segs=(6, 4), shade=0, jitter=0)
    if extras:
        extras(p, Vector(skull))
    p.build(neck_base)


def fan_tail(p, pivot, n, spread, length, rgb, rgb2, tip_rgb, tilt=0.32, w0=0.035, w1=0.07):
    pivot = Vector(pivot)
    for i in range(n):
        a = math.radians(-spread / 2 + i * spread / max(1, n - 1))
        d = V(math.sin(a), tilt, math.cos(a)).normalized()
        side = V(math.cos(a), 0, -math.sin(a))
        p.feather(pivot, d, side, length, w0, w1, rgb if i % 2 else rgb2, tip_rgb, tip=0.2)


# ---------- species ----------

def turkey():
    bronze, dark = (0.32, 0.19, 0.10), (0.17, 0.10, 0.06)
    b = Part("Body")
    b.blob((0, 0.03, 0.42), (0.24, 0.31, 0.24), bronze, segs=(10, 7), belly=dark)
    b.blob((0, -0.16, 0.44), (0.18, 0.14, 0.18), (0.24, 0.14, 0.08))
    b.tube((0, -0.27, 0.45), (0, -0.31, 0.3), 0.018, 0.006, (0.06, 0.05, 0.04))
    fan_tail(b, (0, 0.24, 0.46), 9, 156, 0.42, (0.34, 0.21, 0.11), (0.27, 0.16, 0.09), (0.8, 0.74, 0.62))
    b.build()

    def bits(p, s):
        p.tube(s + V(0, -0.045, 0.04), s + V(0.006, -0.1, -0.09), 0.013, 0.009, (0.72, 0.1, 0.08))
        p.blob(s + V(0, -0.02, -0.065), (0.026, 0.03, 0.05), (0.75, 0.12, 0.1), shade=0)
    head((0, -0.2, 0.55), [(0, -0.25, 0.65)], 0.05, (0.5, 0.58, 0.72), (0, -0.3, 0.75), (0.052, 0.068, 0.058),
         (0.48, 0.6, 0.8), 0.07, 0.02, (0.8, 0.64, 0.36), extras=bits)
    wings((0.2, -0.05, 0.52), (0.29, 0.18, 0.1), (0.38, 0.31, 0.24), 0.42, 0.13, bars=(0.72, 0.68, 0.6))
    legs((0.62, 0.48, 0.44), (0.085, 0.05, 0.28), 0.28, 0.02)


def hen():
    body, belly = (0.62, 0.36, 0.16), (0.5, 0.28, 0.12)
    b = Part("Body")
    b.blob((0, 0.0, 0.36), (0.2, 0.27, 0.21), body, segs=(10, 7), belly=belly)
    b.blob((0, -0.15, 0.42), (0.15, 0.12, 0.16), (0.68, 0.42, 0.2))
    for i in range(5):
        a = math.radians(-30 + i * 15)
        b.feather((0, 0.2, 0.42), V(math.sin(a) * 0.4, 0.6, 1.0), V(1, 0, 0), 0.2, 0.03, 0.05, (0.48, 0.26, 0.1) if i % 2 else (0.4, 0.2, 0.08))
    b.build()

    def bits(p, s):
        for i in range(4):
            p.blob(s + V(0, -0.03 + i * 0.022, 0.055 - abs(i - 1.5) * 0.008), (0.012, 0.016, 0.026), (0.78, 0.1, 0.08), shade=0)
        p.blob(s + V(0, -0.05, -0.05), (0.014, 0.018, 0.028), (0.78, 0.12, 0.1), shade=0)
    head((0, -0.16, 0.48), [(0, -0.2, 0.58)], 0.06, (0.7, 0.45, 0.22), (0, -0.22, 0.64), (0.05, 0.062, 0.055),
         (0.7, 0.46, 0.22), 0.045, 0.016, (0.8, 0.66, 0.3), extras=bits)
    wings((0.18, -0.04, 0.44), (0.56, 0.32, 0.14), (0.42, 0.24, 0.1), 0.34, 0.12)
    legs((0.8, 0.68, 0.25), (0.07, 0.03, 0.22), 0.22, 0.018)


def rooster():
    b = Part("Body")
    b.blob((0, 0.0, 0.42), (0.19, 0.27, 0.2), (0.62, 0.18, 0.08), segs=(10, 7), belly=(0.08, 0.1, 0.06))
    b.blob((0, -0.14, 0.5), (0.15, 0.12, 0.17), (0.82, 0.5, 0.14))
    for i in range(7):
        a = -0.5 + i * 0.17
        base = V(a * 0.08, 0.2, 0.5)
        b.feather(base, V(a * 0.6, 0.55, 0.9), V(1, 0, 0), 0.32 + 0.08 * (3 - abs(i - 3)), 0.02, 0.035,
                  (0.06, 0.22, 0.14) if i % 2 else (0.04, 0.12, 0.1), thick=0.01)
    for i in range(2):
        s = -1 if i == 0 else 1
        pts = [V(0, 0.2, 0.55) + V(s * 0.02, 0, 0)]
        b.feather((s * 0.02, 0.2, 0.56), V(s * 0.1, 0.9, 0.7), V(1, 0, 0), 0.5, 0.02, 0.03, (0.05, 0.25, 0.18), thick=0.01)
    b.build()

    def bits(p, s):
        for i in range(5):
            p.blob(s + V(0, -0.035 + i * 0.022, 0.06 + (0.03 - abs(i - 2) * 0.012)), (0.012, 0.016, 0.04), (0.8, 0.08, 0.06), shade=0)
        p.blob(s + V(0, -0.05, -0.06), (0.016, 0.02, 0.04), (0.8, 0.1, 0.08), shade=0)
    head((0, -0.15, 0.55), [(0, -0.19, 0.67)], 0.06, (0.84, 0.52, 0.14), (0, -0.21, 0.74), (0.048, 0.06, 0.054),
         (0.78, 0.4, 0.12), 0.045, 0.016, (0.8, 0.68, 0.32), extras=bits)
    wings((0.17, -0.04, 0.5), (0.5, 0.14, 0.07), (0.08, 0.14, 0.12), 0.34, 0.12)
    legs((0.8, 0.68, 0.25), (0.07, 0.03, 0.26), 0.26, 0.018, toe=0.075)


def goose():
    b = Part("Body")
    b.blob((0, 0.04, 0.32), (0.2, 0.36, 0.18), (0.42, 0.36, 0.3), segs=(10, 7), belly=(0.82, 0.8, 0.76))
    b.blob((0, -0.22, 0.34), (0.15, 0.12, 0.14), (0.62, 0.58, 0.52))
    for i in range(5):
        b.feather((0, 0.36, 0.34), V((i - 2) * 0.15, 1, 0.25), V(1, 0, 0), 0.14, 0.03, 0.04, (0.12, 0.1, 0.09), thick=0.01)
    b.build()

    def bits(p, s):
        p.blob(s + V(0, 0.01, -0.025), (0.052, 0.04, 0.028), (0.9, 0.9, 0.86), shade=0)
    head((0, -0.3, 0.4), [(0, -0.37, 0.52), (0, -0.36, 0.66)], 0.045, (0.06, 0.06, 0.06), (0, -0.39, 0.73), (0.045, 0.07, 0.045),
         (0.07, 0.07, 0.07), 0.07, 0.022, (0.12, 0.1, 0.08), extras=bits)
    wings((0.17, -0.12, 0.4), (0.38, 0.32, 0.26), (0.18, 0.15, 0.13), 0.5, 0.12, bars=(0.55, 0.5, 0.44))
    legs((0.15, 0.13, 0.12), (0.08, 0.04, 0.18), 0.18, 0.02, toe=0.08)


def swan():
    w = (0.78, 0.79, 0.8)
    b = Part("Body")
    b.blob((0, 0.05, 0.3), (0.22, 0.38, 0.19), w, segs=(10, 7), belly=(0.82, 0.82, 0.8))
    b.feather((0, 0.36, 0.34), V(0, 1, 0.45), V(1, 0, 0), 0.16, 0.05, 0.06, w)
    b.build()

    def bits(p, s):
        p.blob(s + V(0, -0.06, 0.005), (0.018, 0.02, 0.018), (0.05, 0.05, 0.05), shade=0)
    head((0, -0.28, 0.38), [(0, -0.38, 0.5), (0, -0.36, 0.66), (0, -0.3, 0.8)], 0.045, w, (0, -0.35, 0.88), (0.04, 0.065, 0.04),
         w, 0.08, 0.02, (0.82, 0.38, 0.12), extras=bits)
    wings((0.18, -0.1, 0.38), w, (0.84, 0.84, 0.82), 0.55, 0.16, lift=0.25)
    legs((0.1, 0.1, 0.1), (0.08, 0.06, 0.16), 0.16, 0.02, toe=0.09)


def emu():
    shag, shag2 = (0.36, 0.28, 0.2), (0.26, 0.2, 0.15)
    b = Part("Body")
    b.blob((0, 0.05, 0.72), (0.24, 0.34, 0.24), shag, segs=(12, 8), belly=shag2, jitter=0.16)
    for i in range(14):
        a = i / 14 * math.tau
        base = V(math.sin(a) * 0.22, 0.05 + math.cos(a) * 0.3, 0.62)
        b.feather(base, V(math.sin(a) * 0.3, math.cos(a) * 0.3, -1), V(math.cos(a), -math.sin(a), 0), 0.16, 0.04, 0.05, shag2 if i % 2 else shag)
    b.build()
    head((0, -0.22, 0.86), [(0, -0.28, 1.05), (0, -0.27, 1.2)], 0.055, (0.4, 0.46, 0.58), (0, -0.3, 1.3), (0.05, 0.07, 0.05),
         (0.3, 0.3, 0.32), 0.06, 0.02, (0.2, 0.18, 0.16), eye_rgb=(0.7, 0.4, 0.15))
    wings((0.2, -0.05, 0.78), shag2, shag, 0.18, 0.06, primaries=3)
    legs((0.42, 0.42, 0.44), (0.1, 0.08, 0.55), 0.55, 0.032, toe=0.11, knee_back=0.12)


def ostrich():
    b = Part("Body")
    b.blob((0, 0.05, 0.74), (0.26, 0.34, 0.22), (0.06, 0.06, 0.06), segs=(12, 8), jitter=0.2)
    for s in (-1, 1):
        for i in range(4):
            b.feather((s * 0.2, 0.18 + i * 0.03, 0.82), V(s * 0.6, 0.7, -0.2 - i * 0.2), V(0, 0, 1), 0.28, 0.05, 0.08, (0.8, 0.79, 0.76))
    b.feather((0, 0.36, 0.8), V(0, 1, 0.1), V(1, 0, 0), 0.22, 0.08, 0.1, (0.79, 0.78, 0.75))
    b.build()
    head((0, -0.22, 0.86), [(0, -0.28, 1.08), (0, -0.25, 1.28)], 0.05, (0.78, 0.55, 0.52), (0, -0.28, 1.4), (0.055, 0.075, 0.05),
         (0.62, 0.52, 0.5), 0.07, 0.025, (0.62, 0.48, 0.38))
    wings((0.24, -0.02, 0.8), (0.08, 0.08, 0.08), (0.8, 0.79, 0.76), 0.3, 0.1, primaries=4)
    legs((0.78, 0.55, 0.5), (0.11, 0.08, 0.55), 0.55, 0.035, toe=0.12, knee_back=0.12)


def cassowary():
    b = Part("Body")
    b.blob((0, 0.05, 0.62), (0.26, 0.36, 0.26), (0.04, 0.04, 0.05), segs=(12, 8), jitter=0.25)
    for i in range(16):
        a = i / 16 * math.tau
        base = V(math.sin(a) * 0.24, 0.05 + math.cos(a) * 0.32, 0.5)
        b.feather(base, V(math.sin(a) * 0.2, math.cos(a) * 0.2, -1), V(math.cos(a), -math.sin(a), 0), 0.2, 0.04, 0.05, (0.06, 0.06, 0.07))
    b.build()

    def bits(p, s):
        p.plate([s + V(0, 0.04, 0.03), s + V(0, -0.03, 0.04), s + V(0, -0.01, 0.17), s + V(0, 0.06, 0.12)], (0.5, 0.4, 0.26), thick=0.04)
        for side in (-1, 1):
            p.blob(s + V(side * 0.02, -0.02, -0.17), (0.022, 0.025, 0.055), (0.78, 0.1, 0.08), shade=0)
    head((0, -0.24, 0.76), [(0, -0.3, 0.92)], 0.06, (0.1, 0.3, 0.75), (0, -0.31, 1.08), (0.05, 0.07, 0.055),
         (0.15, 0.4, 0.85), 0.07, 0.022, (0.12, 0.12, 0.12), extras=bits)
    wings((0.22, -0.02, 0.66), (0.05, 0.05, 0.06), (0.1, 0.1, 0.1), 0.16, 0.06, primaries=3)
    legs((0.42, 0.38, 0.28), (0.11, 0.08, 0.46), 0.46, 0.04, toe=0.13, knee_back=0.1, claw=(0.15, 0.15, 0.15))


def terror():
    b = Part("Body")
    b.blob((0, 0.08, 0.64), (0.24, 0.38, 0.25), (0.34, 0.27, 0.2), segs=(12, 8), belly=(0.6, 0.52, 0.4))
    for i in range(6):
        b.blob((0, -0.15 + i * 0.07, 0.86), (0.18 - abs(i - 2.5) * 0.02, 0.025, 0.04), (0.14, 0.1, 0.08), shade=0)
    for i in range(5):
        b.feather((0, 0.42, 0.7), V((i - 2) * 0.2, 1, 0.05), V(1, 0, 0), 0.3, 0.03, 0.05, (0.25, 0.2, 0.15) if i % 2 else (0.18, 0.14, 0.1))
    b.build()

    def bits(p, s):
        p.blob(s + V(0, -0.05, 0.05), (0.06, 0.06, 0.03), (0.8, 0.5, 0.2), shade=0)
    head((0, -0.26, 0.8), [(0, -0.36, 0.98)], 0.07, (0.4, 0.32, 0.24), (0, -0.4, 1.08), (0.075, 0.12, 0.08),
         (0.42, 0.34, 0.26), 0.2, 0.06, (0.72, 0.66, 0.5), eye_rgb=(0.95, 0.75, 0.1), eye_r=0.017, hook=0.25, extras=bits)
    wings((0.22, -0.02, 0.72), (0.28, 0.22, 0.16), (0.2, 0.16, 0.12), 0.18, 0.07, primaries=3)
    legs((0.55, 0.48, 0.32), (0.12, 0.1, 0.48), 0.48, 0.045, toe=0.14, knee_back=0.1, claw=(0.2, 0.18, 0.15))


def raptor(body, belly, head_rgb, wing_rgb, tip_rgb, beak_rgb, eye_rgb, crest=None, stripes=None):
    b = Part("Body")
    b.blob((0, 0.05, 0.48), (0.22, 0.34, 0.24), body, segs=(12, 8), belly=belly)
    b.blob((0, -0.16, 0.56), (0.17, 0.13, 0.19), belly)
    for i in range(7):
        b.feather((0, 0.36, 0.48), V((i - 3) * 0.14, 1, -0.15), V(1, 0, 0), 0.34, 0.035, 0.06, tip_rgb if i % 2 else body)
    if stripes:
        for i in range(4):
            b.blob((0, -0.24 + i * 0.012, 0.42 + i * 0.07), (0.15, 0.03, 0.025), stripes, shade=0)
    b.build()

    def bits(p, s):
        if crest:
            for i in range(5):
                p.feather(s + V(0, 0.02, 0.05), V((i - 2) * 0.25, 0.8, 0.9), V(1, 0, 0), 0.2, 0.012, 0.025, crest)
    head((0, -0.2, 0.66), [(0, -0.26, 0.78)], 0.08, head_rgb, (0, -0.28, 0.86), (0.08, 0.1, 0.08), head_rgb,
         0.11, 0.04, beak_rgb, eye_rgb=eye_rgb, eye_r=0.018, hook=0.4, extras=bits)
    wings((0.13, -0.02, 0.6), wing_rgb, tip_rgb, 0.85, 0.18, open_=0.75, lift=0.3, primaries=7)
    legs((0.8, 0.66, 0.2), (0.1, 0.04, 0.3), 0.3, 0.035, toe=0.1, claw=(0.08, 0.07, 0.06))


def roc():
    raptor((0.28, 0.18, 0.1), (0.42, 0.3, 0.18), (0.78, 0.6, 0.3), (0.26, 0.16, 0.09), (0.12, 0.08, 0.05),
           (0.82, 0.66, 0.2), (0.85, 0.55, 0.1))


def thunderbird():
    # Bright gold eyes and markings use the knoll shader's glow band on purpose.
    raptor((0.08, 0.14, 0.36), (0.15, 0.28, 0.6), (0.1, 0.2, 0.5), (0.06, 0.12, 0.34), (0.95, 0.75, 0.15),
           (0.95, 0.75, 0.2), (1.0, 0.9, 0.2), crest=(0.9, 0.18, 0.12), stripes=(0.95, 0.7, 0.12))


SPECIES = {
    "Turkey": (turkey, 0.82), "Hen": (hen, 0.7), "Rooster": (rooster, 0.86), "Goose": (goose, 0.78),
    "Swan": (swan, 0.85), "Emu": (emu, 1.05), "Ostrich": (ostrich, 1.1), "Cassowary": (cassowary, 0.95),
    "Terror": (terror, 0.98), "Roc": (roc, 0.8), "Thunderbird": (thunderbird, 0.9),
}


def build(name, seed=7):
    clear()
    random.seed(seed)
    fn, height = SPECIES[name]
    fn()
    scn = scene()
    parts = list(scn.collection.objects)
    top = max((o.matrix_world @ Vector(c)).z for o in parts for c in o.bound_box)
    k = height / top
    root = bpy.data.objects.new(name, None)
    scn.collection.objects.link(root)
    for o in parts:
        o.parent = root
    root.scale = (k, k, k)
    bpy.context.view_layer.update()
    for o in parts:
        o.select_set(True)
    bpy.ops.object.select_all(action='DESELECT')
    # bake the normalising scale into the meshes and the part pivots
    for o in parts:
        o.data.transform(Matrix.Diagonal((k, k, k, 1)))
        o.location = o.location * k
    root.scale = (1, 1, 1)
    return root


def export(name):
    root = build(name)
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for c in root.children:
        c.select_set(True)
    bpy.context.view_layer.objects.active = root
    path = os.path.join(OUT, name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, mesh_smooth_type='FACE', use_mesh_modifiers=True,
                             add_leaf_bones=False, bake_anim=False, colors_type='SRGB', object_types={'EMPTY', 'MESH'})
    return path
