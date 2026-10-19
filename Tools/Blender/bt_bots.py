# Breaking Through bot generator: the low-poly stand-ins, until the real bot models land. Run inside Blender:
#   exec(open(r"C:\Projects\breakingthrough\Tools\Blender\bt_bots.py").read()); export("YoBot")
# or export_all(). Output goes to Assets/Resources/Models/Bots/<Kind>.fbx, which BotArt loads in place of the
# stand-in it builds in code (the same shapes, so an export changes nothing until someone improves it here).
# Every part is a separate rigid object so the game can animate it:
#   Yo Bots:     Body, Head, ClawL, ClawR, LegL, LegR
#   Spider Bots: Body, Head, LegL1..LegL4, LegR1..LegR4 (1 is the front pair, the one that stabs)
# Flat shaded, painted with vertex colors. Blender -Y is the bot's front. Feet sit on z = 0.
# Numbers below are in the game's axes (x right, y up, z forward), as in BotArt.cs; U() turns them into Blender's.
import bpy, bmesh, math, os
from mathutils import Vector, Matrix, Euler

OUT = r"C:\Projects\breakingthrough\Assets\Resources\Models\Bots"


def U(x, y, z):
    """Game axes (x right, y up, z forward) to Blender (x right, -y forward, z up)."""
    return Vector((x, -z, y))


def scene():
    scn = bpy.data.scenes.get("BT_Bots") or bpy.data.scenes.new("BT_Bots")
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
    m = bpy.data.materials.get("BT_BotVC")
    if m:
        return m
    m = bpy.data.materials.new("BT_BotVC")
    m.use_nodes = True
    nt = m.node_tree
    b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    attr = nt.nodes.new("ShaderNodeVertexColor")
    attr.layer_name = "Col"
    nt.links.new(attr.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.6
    return m


class Part:
    def __init__(self, name, pivot=(0, 0, 0)):
        self.name = name
        self.pivot = U(*pivot)
        self.bm = bmesh.new()
        self.col = self.bm.loops.layers.color.new("Col")

    def paint(self, verts, rgb, top=None, under=None):
        faces = {f for v in verts for f in v.link_faces}
        for f in faces:
            f.normal_update()
            c = rgb
            if top and f.normal.z > 0.7:
                c = top
            elif under and f.normal.z < -0.7:
                c = under
            for l in f.loops:
                l[self.col] = (min(c[0], 1), min(c[1], 1), min(c[2], 1), 1)

    def box(self, center, size, rgb, rot=(0, 0, 0)):
        """center and size in game axes, relative to the part's pivot; rot is game-axes Euler degrees."""
        res = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = res["verts"]
        sx, sy, sz = size
        r = Euler((math.radians(rot[0]), -math.radians(rot[2]), math.radians(rot[1])), "XYZ").to_matrix().to_4x4()
        M = Matrix.Translation(U(*center)) @ r @ Matrix.Diagonal((sx, sz, sy, 1))
        bmesh.ops.transform(self.bm, matrix=M, verts=verts)
        top = tuple(min(1, c * 0.92 + 0.08) for c in rgb)
        under = tuple(c * 0.72 for c in rgb)
        self.paint(verts, rgb, top, under)

    def rod(self, a, b, r, rgb, sides=6):
        a = U(*a)
        b = U(*b)
        d = b - a
        if d.length < 1e-6:
            return
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=True, segments=sides, radius1=r, radius2=r, depth=d.length)
        verts = res["verts"]
        q = Vector((0, 0, 1)).rotation_difference(d.normalized())
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation((a + b) / 2) @ q.to_matrix().to_4x4(), verts=verts)
        self.paint(verts, rgb)

    def ball(self, center, r, rgb):
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=6, v_segments=4, radius=r)
        verts = res["verts"]
        bmesh.ops.translate(self.bm, vec=U(*center), verts=verts)
        self.paint(verts, rgb)

    def build(self):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        o = bpy.data.objects.new(self.name, me)
        scene().collection.objects.link(o)
        me.materials.append(vc_mat())
        for p in me.polygons:
            p.use_smooth = False
        o.location = self.pivot
        return o


DARK = (0.1, 0.1, 0.12)

# paint, trim, dark, eye, and the extras each kind carries (BotArt.LookOf).
LOOKS = {
    "YoBot": dict(paint=(0.86, 0.64, 0.22), trim=(0.3, 0.3, 0.33), eye=(0.4, 0.9, 1.0), wide=1.0),
    "PlatedYoBot": dict(paint=(0.5, 0.56, 0.64), trim=(0.3, 0.33, 0.38), eye=(1.0, 0.62, 0.2), wide=1.18, plates=True),
    "MegaYoBot": dict(paint=(0.78, 0.2, 0.14), trim=(0.22, 0.22, 0.25), eye=(1.0, 0.92, 0.35), wide=1.08, plates=True, horns=True),
    "SpiderBot": dict(paint=(0.26, 0.44, 0.46), trim=(0.18, 0.24, 0.26), eye=(1.0, 0.25, 0.15), legs=1.0),
    "StalkerSpiderBot": dict(paint=(0.74, 0.76, 0.8), trim=(0.4, 0.42, 0.46), eye=(0.45, 0.9, 1.0), legs=1.1),
    "RunnerSpiderBot": dict(paint=(0.86, 0.45, 0.14), trim=(0.24, 0.2, 0.18), eye=(0.95, 0.95, 0.9), legs=1.3),
    "RammerSpiderBot": dict(paint=(0.7, 0.6, 0.42), trim=(0.36, 0.3, 0.22), eye=(1.0, 0.3, 0.2), legs=1.0, ram=True),
    "ShadowSpiderBot": dict(paint=(0.15, 0.12, 0.22), trim=(0.3, 0.2, 0.42), eye=(0.8, 0.35, 1.0), legs=1.05, dark=(0.05, 0.05, 0.07)),
    "CrusherSpiderBot": dict(paint=(0.5, 0.28, 0.18), trim=(0.3, 0.2, 0.14), eye=(1.0, 0.55, 0.15), legs=0.95, pincers=True),
    "ColossusSpiderBot": dict(paint=(0.25, 0.35, 0.7), trim=(0.14, 0.18, 0.36), eye=(0.9, 0.95, 1.0), legs=1.15, ram=True),
    # Warm gold trim glows in game (BirdArt.Glowing).
    "TitanSpiderBot": dict(paint=(0.36, 0.28, 0.12), trim=(0.95, 0.75, 0.15), eye=(1.0, 0.9, 0.2), legs=1.2, pincers=True, crown=True, dark=(0.12, 0.1, 0.08)),
}


def yo_bot(k):
    w = k.get("wide", 1.0)
    paint, trim, dark, eye = k["paint"], k["trim"], k.get("dark", DARK), k["eye"]

    b = Part("Body")
    b.box((0, 0.34, 0), (0.26 * w, 0.08, 0.18), dark)
    b.box((0, 0.5, 0), (0.36 * w, 0.26, 0.26), paint)
    b.box((0, 0.5, 0.135), (0.24 * w, 0.16, 0.02), trim)
    b.ball((0, 0.52, 0.15), 0.03, eye)
    b.box((0, 0.52, -0.17), (0.24 * w, 0.22, 0.1), trim)
    for s in (-1, 1):
        b.rod((s * 0.07 * w, 0.42, -0.18), (s * 0.07 * w, 0.35, -0.18), 0.035, dark)
    if k.get("plates"):
        for s in (-1, 1):
            b.box((s * 0.22 * w, 0.63, 0), (0.13, 0.06, 0.2), trim, rot=(0, 0, -s * 14))
        b.box((0, 0.4, 0.12), (0.28 * w, 0.07, 0.04), trim)
    b.build()

    tall = 0.19 if k.get("horns") else 0.16
    h = Part("Head", (0, 0.63, 0.01))
    h.rod((0, 0, 0), (0, 0.05, 0), 0.045, dark)
    h.box((0, 0.04 + tall * 0.5, 0), (0.28 * w, tall, 0.22), paint)
    h.box((0, 0.05 + tall * 0.55, 0.112), (0.22 * w, 0.05, 0.012), eye)
    ax, ay, az = 0.08 * w, 0.04 + tall, -0.03
    h.rod((ax, ay, az), (ax + 0.02, ay + 0.1, az), 0.012, dark)
    h.ball((ax + 0.02, ay + 0.11, az), 0.025, eye)
    if k.get("horns"):
        for s in (-1, 1):
            rx, ry = s * 0.13 * w, 0.04 + tall * 0.8
            h.rod((rx, ry, 0), (rx + s * 0.06, ry + 0.07, 0.02), 0.022, trim)
    h.build()

    for s, name in ((-1, "ClawL"), (1, "ClawR")):
        c = Part(name, (s * 0.21 * w, 0.58, 0))
        elbow = (s * 0.02, -0.16, 0.04)
        wrist = (s * 0.02, -0.23, 0.15)
        c.ball((0, 0, 0), 0.055, dark)
        c.rod((0, 0, 0), elbow, 0.036, trim)
        c.ball(elbow, 0.04, dark)
        c.rod(elbow, wrist, 0.034, trim)
        c.ball(wrist, 0.045, paint)
        wx, wy, wz = wrist
        c.rod(wrist, (wx + s * 0.035, wy - 0.01, wz + 0.1), 0.018, dark)
        c.rod(wrist, (wx - s * 0.025, wy - 0.01, wz + 0.1), 0.018, dark)
        c.rod(wrist, (wx, wy - 0.06, wz + 0.07), 0.016, dark)
        c.build()

    for s, name in ((-1, "LegL"), (1, "LegR")):
        g = Part(name, (s * 0.1 * w, 0.32, 0))
        g.box((0, -0.13, 0), (0.1, 0.24, 0.11), trim)
        g.ball((0, -0.15, 0.05), 0.045, dark)
        g.box((0, -0.295, 0.035), (0.14, 0.055, 0.22), dark)
        g.build()


def spider_bot(k):
    reach = k.get("legs", 1.0)
    paint, trim, dark, eye = k["paint"], k["trim"], k.get("dark", DARK), k["eye"]

    b = Part("Body")
    b.box((0, 0.5, 0.06), (0.36, 0.18, 0.38), paint)
    b.box((0, 0.6, -0.3), (0.48, 0.3, 0.5), paint, rot=(-10, 0, 0))
    b.box((0, 0.765, -0.3), (0.3, 0.025, 0.4), trim, rot=(-10, 0, 0))
    b.box((0, 0.6, -0.3), (0.5, 0.06, 0.3), trim, rot=(-10, 0, 0))
    for s in (-1, 1):
        b.rod((s * 0.1, 0.52, -0.5), (s * 0.1, 0.47, -0.58), 0.04, dark)
        b.ball((s * 0.18, 0.5, 0.06), 0.06, dark)
    if k.get("crown"):
        for i in range(5):
            z = -0.48 + i * 0.09
            b.rod((0, 0.77 + i * 0.015, z), (0, 0.86 + i * 0.015, z + 0.03), 0.025, trim)
    b.build()

    h = Part("Head", (0, 0.5, 0.25))
    h.box((0, 0.02, 0.08), (0.26, 0.14, 0.16), paint)
    for s in (-1, 1):
        h.ball((s * 0.05, 0.06, 0.165), 0.028, eye)
        h.ball((s * 0.1, 0.035, 0.15), 0.022, eye)
        if k.get("pincers"):
            root = (s * 0.09, -0.03, 0.15)
            bend = (root[0] + s * 0.08, root[1] - 0.02, root[2] + 0.12)
            h.rod(root, bend, 0.03, trim)
            h.rod(bend, (bend[0] - s * 0.08, bend[1], bend[2] + 0.06), 0.024, dark)
        else:
            h.rod((s * 0.05, -0.04, 0.15), (s * 0.03, -0.12, 0.2), 0.016, dark)
    if k.get("ram"):
        h.box((0, 0.03, 0.18), (0.42, 0.24, 0.04), trim)
    h.build()

    hip_z = (0.2, 0.1, 0.0, -0.1)
    splay = (0.2, 0.07, -0.07, -0.2)
    for pair in range(4):
        for s, side in ((-1, "LegL"), (1, "LegR")):
            g = Part(side + str(pair + 1), (s * 0.18, 0.5, hip_z[pair]))
            knee = (s * 0.28 * reach, 0.26, splay[pair] * reach)
            foot = (s * 0.52 * reach, -0.5, splay[pair] * 1.8 * reach)
            g.rod((0, 0, 0), knee, 0.036, trim)
            g.ball(knee, 0.048, dark)
            g.rod(knee, foot, 0.028, paint)
            g.ball(foot, 0.024, dark)
            g.build()


KINDS = list(LOOKS.keys())


def build(name):
    clear()
    look = LOOKS[name]
    if name.endswith("SpiderBot"):
        spider_bot(look)
    else:
        yo_bot(look)
    scn = scene()
    parts = list(scn.collection.objects)
    root = bpy.data.objects.new(name, None)
    scn.collection.objects.link(root)
    for o in parts:
        o.parent = root
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


def export_all():
    return [export(name) for name in KINDS]
