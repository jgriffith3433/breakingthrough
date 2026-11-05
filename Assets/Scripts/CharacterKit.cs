using System;
using System.Collections.Generic;
using UnityEngine;

// Authored character models (Docs/CharacterModels.md). Every body part is a KitMesh solid (tapered tubes, lathes, extrusions,
// bevelled boxes), textured from the Kit's texture sets and parented in a part hierarchy that CharacterRig animates.
// Layout of a character: Root (CharacterRig) / Model / parts. Parts are named like the bots' (Body, Head, LegL ...). No
// primitives: GameObject.CreatePrimitive is never called here, and CharacterValidation checks it.
public class CharacterDef {
    public string Id, Name;
    public int Budget;                  // triangles
    public bool Boss;
    public Func<GameObject> Build;
}

public static partial class CharacterKit {
    public static readonly List<CharacterDef> All = new List<CharacterDef>();
    static readonly Dictionary<string, CharacterDef> byId = new Dictionary<string, CharacterDef>();
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static string current = "";
    static bool built;

    static void Add(string id, string name, int budget, bool boss, Func<GameObject> build) {
        var d = new CharacterDef { Id = id, Name = name, Budget = budget, Boss = boss, Build = build };
        All.Add(d);
        byId[id] = d;
    }

    public static void Init() {
        if (built) {
            return;
        }
        built = true;
        Add("wallit", "Wallit", 6000, false, BuildWallit);
        Add("slice", "Slice", 6000, false, BuildSlice);
        Add("yobot", "Yo Bot", 6000, false, BuildYoBot);
        Add("spiderbot", "Spider Bot", 6000, false, BuildSpiderBot);
        Add("courier", "The Courier", 12000, true, BuildCourier);
        Add("foreman", "The Foreman", 12000, true, BuildForeman);
        Add("server", "The Server", 12000, true, BuildServer);
        Add("orderbearer", "The Order-Bearer", 12000, true, BuildOrderBearer);
        Add("tick", "Tick", 6000, false, BuildTick);
        Add("brim", "Brim", 6000, false, BuildBrim);
        Add("spare", "Spare", 6000, false, BuildSpare);
        Add("chime", "Chime", 6000, false, BuildChime);
    }

    public static CharacterDef Def(string id) { Init(); CharacterDef d; return byId.TryGetValue(id, out d) ? d : null; }

    // A character at the origin, feet on y = 0, facing +Z, with a CharacterRig on its root.
    public static GameObject Make(string id) {
        CharacterDef d = Def(id);
        if (d == null) {
            return null;
        }
        current = id;
        GameObject go = d.Build();
        go.name = d.Name;
        CharacterRig rig = go.AddComponent<CharacterRig>();
        rig.Id = id;
        return go;
    }

    // ---- helpers ----------------------------------------------------------------------------------------------------

    public static Color Hex(int rgb) {
        return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
    }

    // Texture sets used as: cloth = fabric and leather, metal/iron/brass = hardware, wood, paper, glass = eyes and lenses,
    // plaster = skin and shell, stone = rough shell. glow lights the material (visors, eyes, seals).
    static Material Mat(string set, Color c, float gloss, Color? glow) {
        string key = set + "|" + c.ToString() + "|" + gloss + "|" + (glow.HasValue ? glow.Value.ToString() : "-");
        Material m;
        if (mats.TryGetValue(key, out m) && m != null) {
            return m;
        }
        m = new Material(SafeShader.Find("Knoll/KitLit"));
        m.name = "Char_" + set;
        m.mainTexture = KitTextures.Albedo(set);
        m.SetTexture("_BumpMap", KitTextures.Normal(set));
        m.SetColor("_Color", c);
        float g = gloss >= 0f ? gloss : (set == "metal" || set == "iron" ? 0.4f : set == "brass" ? 0.5f : set == "glass" ? 0.8f : 0.1f);
        m.SetFloat("_Glossiness", g);
        m.SetFloat("_Metallic", set == "metal" || set == "iron" || set == "brass" ? 0.3f : 0f);
        if (glow.HasValue) {
            m.SetColor("_EmissionColor", glow.Value);
        }
        m.enableInstancing = true;
        mats[key] = m;
        return m;
    }

    // One named part: a child of parent at a pivot, with a KitMesh solid built around that pivot.
    static Transform Part(Transform parent, string name, Vector3 pos, string set, Color col, Action<KitMesh> build, float gloss = -1f, Color? glow = null) {
        var m = new KitMesh();
        m.Name = current + " " + name;
        m.Tex = 0.45f;
        build(m);
        m.Reset();
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.AddComponent<MeshFilter>().sharedMesh = m.ToMesh();
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = Mat(set, col, gloss, glow);
        r.receiveShadows = true;
        return go.transform;
    }

    // The root and its Model child; parts hang from Model.
    static Transform NewRoot(out GameObject root) {
        root = new GameObject("Character");
        var model = new GameObject("Model");
        model.transform.SetParent(root.transform, false);
        return model.transform;
    }

    // A tapered tube from a to b (radius r0 at a, r1 at b) with rounded ends: the limb, leg, horn and tail builder.
    public static void Tube(KitMesh m, Vector3 a, Vector3 b, float r0, float r1, int seg = 8) {
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 1e-4f) {
            return;
        }
        Matrix4x4 keep = m.M;
        m.M = keep * Matrix4x4.TRS(a, Quaternion.FromToRotation(Vector3.up, d / len), Vector3.one);
        m.Lathe(new[] {
            new Vector2(0f, -r0 * 0.5f), new Vector2(r0 * 0.75f, -r0 * 0.3f), new Vector2(r0, 0f),
            new Vector2(r1, len), new Vector2(r1 * 0.75f, len + r1 * 0.3f), new Vector2(0f, len + r1 * 0.5f) }, seg);
        m.M = keep;
    }

    // An ellipsoid (a lathe of a half circle) centred at c with the given radii: heads, joints, bellies.
    public static void Blob(KitMesh m, Vector3 c, Vector3 radii, int seg = 10, int rings = 6) {
        Matrix4x4 keep = m.M;
        m.M = keep * Matrix4x4.TRS(c, Quaternion.identity, radii);
        var prof = new List<Vector2>();
        for (int i = 0; i <= rings; i++) {
            float a = Mathf.Lerp(-90f, 90f, (float)i / rings) * Mathf.Deg2Rad;
            prof.Add(new Vector2(Mathf.Max(0f, Mathf.Cos(a)), Mathf.Sin(a)));
        }
        prof[0] = new Vector2(0f, -1f);
        prof[rings] = new Vector2(0f, 1f);
        m.Lathe(prof, seg);
        m.M = keep;
    }

    // A solid placed with a rotation (euler) and centre: bevelled box.
    public static void BoxAt(KitMesh m, Vector3 centre, Vector3 size, Vector3 euler, float bevel = 0.012f) {
        Matrix4x4 keep = m.M;
        m.M = keep * Matrix4x4.TRS(centre, Quaternion.Euler(euler), Vector3.one);
        m.Box(-size * 0.5f, size * 0.5f, bevel);
        m.M = keep;
    }

    // A flat shape extruded along z, placed at c with euler rotation.
    public static void Plate(KitMesh m, IList<Vector2> poly, float depth, Vector3 c, Vector3 euler) {
        Matrix4x4 keep = m.M;
        m.M = keep * Matrix4x4.TRS(c, Quaternion.Euler(euler), Vector3.one);
        m.ExtrudeZ(poly, -depth * 0.5f, depth * 0.5f);
        m.M = keep;
    }

    // A lathe placed at c with euler rotation (a barrel, a bell, a hat, a cylinder prop).
    public static void Turn(KitMesh m, IList<Vector2> prof, Vector3 c, Vector3 euler, int seg = 12) {
        Matrix4x4 keep = m.M;
        m.M = keep * Matrix4x4.TRS(c, Quaternion.Euler(euler), Vector3.one);
        m.Lathe(prof, seg);
        m.M = keep;
    }

    public static List<Vector2> RoundRect(float w, float h, float r, int steps = 4) {
        var p = new List<Vector2>();
        float x = w * 0.5f - r, y = h * 0.5f - r;
        p.AddRange(KitMesh.Arc(x, y, r, 0f, 90f, steps));
        p.AddRange(KitMesh.Arc(-x, y, r, 90f, 180f, steps));
        p.AddRange(KitMesh.Arc(-x, -y, r, 180f, 270f, steps));
        p.AddRange(KitMesh.Arc(x, -y, r, 270f, 360f, steps));
        return p;
    }

    static Vector2 V(float x, float y) { return new Vector2(x, y); }

    // A boot or foot: a flattened blob with a toe.
    static void Foot(KitMesh m, Vector3 c, float w, float len) {
        Blob(m, c, new Vector3(w, w * 0.7f, len), 10, 5);
    }

    // ---- Wallit -----------------------------------------------------------------------------------------------------
    // About 1.0 m: a squat wallet on two short legs, a loose flap with a crooked grin, a bat on the shoulder.
    static GameObject BuildWallit() {
        GameObject root;
        Transform model = NewRoot(out root);
        Color leather = Hex(0x8a5a34), dark = Hex(0x4a2e18), cream = Hex(0xe8dcc0), bill = Hex(0x6f9a5b), wood = Hex(0xd6a866), red = Hex(0xb3262a);

        Transform body = Part(model, "Body", new Vector3(0f, 0.5f, 0f), "cloth", leather, m => {
            Plate(m, RoundRect(0.5f, 0.5f, 0.1f, 5), 0.26f, Vector3.zero, Vector3.zero);
            BoxAt(m, new Vector3(0f, -0.12f, 0.14f), new Vector3(0.36f, 0.2f, 0.05f), new Vector3(-6f, 0f, 0f), 0.02f);   // pocket
            Turn(m, new[] { V(0f, -0.04f), V(0.26f, -0.04f), V(0.28f, 0f), V(0.28f, 0.03f), V(0f, 0.03f) }, new Vector3(0f, 0.01f, 0f), new Vector3(0f, 0f, 0f), 8); // belt hoop
        });
        Part(body, "Stitch", Vector3.zero, "paper", cream, m => {
            BoxAt(m, new Vector3(0f, -0.02f, 0.135f), new Vector3(0.44f, 0.014f, 0.01f), Vector3.zero, 0.003f);
            BoxAt(m, new Vector3(0.235f, -0.02f, 0.02f), new Vector3(0.012f, 0.4f, 0.012f), Vector3.zero, 0.003f);
            BoxAt(m, new Vector3(-0.235f, -0.02f, 0.02f), new Vector3(0.012f, 0.4f, 0.012f), Vector3.zero, 0.003f);
        });
        // The flap is the head: a folded cap with the crooked grin cut into its front.
        Transform head = Part(body, "Head", new Vector3(0f, 0.22f, -0.02f), "cloth", leather, m => {
            Plate(m, RoundRect(0.54f, 0.3f, 0.09f, 5), 0.3f, new Vector3(0f, 0.15f, 0.02f), new Vector3(-8f, 0f, 0f));
            Blob(m, new Vector3(0f, 0.3f, 0.0f), new Vector3(0.12f, 0.05f, 0.12f), 8, 4);   // clasp knob
        });
        Part(head, "Grin", Vector3.zero, "cloth", Hex(0x1c0f08), m => {
            var smile = new List<Vector2>();
            smile.AddRange(KitMesh.Arc(0f, 0.22f, 0.16f, 215f, 330f, 7));
            var inner = KitMesh.Arc(0f, 0.22f, 0.11f, 330f, 215f, 7);
            smile.AddRange(inner);
            Plate(m, smile, 0.03f, new Vector3(0.02f, 0.02f, 0.18f), new Vector3(-8f, 0f, 6f));
        });
        Part(head, "Teeth", Vector3.zero, "paper", cream, m => {
            for (int i = 0; i < 3; i++) {
                BoxAt(m, new Vector3(-0.06f + i * 0.06f, 0.095f - Mathf.Abs(i - 1) * 0.0f, 0.2f), new Vector3(0.035f, 0.04f, 0.015f), new Vector3(-8f, 0f, 6f), 0.004f);
            }
        });
        Part(head, "Eyes", Vector3.zero, "paper", Hex(0xf6f2e6), m => {
            Blob(m, new Vector3(-0.1f, 0.2f, 0.18f), new Vector3(0.05f, 0.06f, 0.025f), 8, 4);
            Blob(m, new Vector3(0.1f, 0.22f, 0.18f), new Vector3(0.05f, 0.07f, 0.025f), 8, 4);
        });
        Part(head, "Pupils", Vector3.zero, "glass", Hex(0x120c08), m => {
            Blob(m, new Vector3(-0.09f, 0.2f, 0.2f), new Vector3(0.022f, 0.03f, 0.014f), 8, 4);
            Blob(m, new Vector3(0.11f, 0.22f, 0.2f), new Vector3(0.022f, 0.03f, 0.014f), 8, 4);
        });
        Part(body, "Bill", new Vector3(0.06f, 0.0f, 0.15f), "paper", bill, m => {
            BoxAt(m, new Vector3(0f, 0.08f, 0f), new Vector3(0.16f, 0.2f, 0.012f), new Vector3(0f, 0f, -12f), 0.004f);
            BoxAt(m, new Vector3(-0.07f, 0.05f, 0.012f), new Vector3(0.14f, 0.18f, 0.012f), new Vector3(0f, 0f, 14f), 0.004f);
        });
        Part(body, "Ink", new Vector3(0f, 0.14f, 0.14f), "cloth", red, m => {
            Blob(m, new Vector3(-0.15f, 0f, 0f), new Vector3(0.07f, 0.045f, 0.012f), 8, 4);
        });
        for (int s = -1; s <= 1; s += 2) {
            string side = s < 0 ? "L" : "R";
            Transform leg = Part(model, "Leg" + side, new Vector3(s * 0.12f, 0.26f, 0f), "cloth", dark, m => {
                Tube(m, Vector3.zero, new Vector3(0f, -0.2f, 0f), 0.065f, 0.05f);
                Foot(m, new Vector3(0f, -0.235f, 0.04f), 0.085f, 0.13f);
            });
        }
        // Arms: the left hangs and balls a fist, the right holds the bat over the shoulder.
        Part(model, "ArmL", new Vector3(-0.28f, 0.6f, 0f), "cloth", leather, m => {
            Tube(m, Vector3.zero, new Vector3(-0.04f, -0.2f, 0.05f), 0.05f, 0.04f);
            Blob(m, new Vector3(-0.04f, -0.23f, 0.06f), new Vector3(0.055f, 0.055f, 0.055f), 8, 4);
        });
        Transform armR = Part(model, "ArmR", new Vector3(0.28f, 0.6f, 0f), "cloth", leather, m => {
            Tube(m, Vector3.zero, new Vector3(0.05f, -0.14f, 0.1f), 0.05f, 0.04f);
            Blob(m, new Vector3(0.05f, -0.17f, 0.11f), new Vector3(0.06f, 0.06f, 0.06f), 8, 4);
        });
        Transform bat = Part(armR, "Prop", new Vector3(0.05f, -0.17f, 0.11f), "wood", wood, m => {
            // Handle at the fist, barrel over the shoulder: tapered lathe along +Y tilted back.
            Turn(m, new[] { V(0f, -0.12f), V(0.022f, -0.12f), V(0.02f, 0f), V(0.026f, 0.2f), V(0.04f, 0.4f), V(0.052f, 0.55f), V(0.05f, 0.64f), V(0.03f, 0.68f), V(0f, 0.69f) }, Vector3.zero, new Vector3(-25f, 0f, -16f), 12);
        });
        Part(bat, "Grip", Vector3.zero, "cloth", Hex(0x23262c), m => {
            Turn(m, new[] { V(0f, -0.12f), V(0.026f, -0.12f), V(0.025f, 0.02f), V(0f, 0.02f) }, Vector3.zero, new Vector3(-25f, 0f, -16f), 10);
        });
        return root;
    }

    // ---- Slice ------------------------------------------------------------------------------------------------------
    // About 1.7 m: tall, narrow, long coat with a flared asymmetric hem, big top-knot, a scalpel held low.
    static GameObject BuildSlice() {
        GameObject root;
        Transform model = NewRoot(out root);
        Color coat = Hex(0x1d1924), coat2 = Hex(0x2a2433), hair = Hex(0x2c2040), skin = Hex(0xd9d0d8), steel = Hex(0xcdd5df), boot = Hex(0x15131a);

        Transform body = Part(model, "Body", new Vector3(0f, 0.95f, 0f), "cloth", coat, m => {
            // Torso and flared skirt as one turned shape: waist, chest, collar.
            Turn(m, new[] { V(0f, -0.5f), V(0.3f, -0.5f), V(0.26f, -0.3f), V(0.17f, -0.05f), V(0.15f, 0.04f), V(0.19f, 0.2f), V(0.2f, 0.34f), V(0.13f, 0.42f), V(0.12f, 0.5f), V(0.08f, 0.52f), V(0f, 0.52f) }, Vector3.zero, Vector3.zero, 14);
            BoxAt(m, new Vector3(0.18f, -0.34f, 0.04f), new Vector3(0.18f, 0.3f, 0.12f), new Vector3(0f, 0f, 16f), 0.02f);   // the longer coat tail on one side
        });
        Part(body, "Lapel", Vector3.zero, "cloth", coat2, m => {
            BoxAt(m, new Vector3(-0.07f, 0.2f, 0.16f), new Vector3(0.05f, 0.4f, 0.03f), new Vector3(0f, 0f, 8f), 0.008f);
            BoxAt(m, new Vector3(0.07f, 0.2f, 0.16f), new Vector3(0.05f, 0.4f, 0.03f), new Vector3(0f, 0f, -8f), 0.008f);
            Turn(m, new[] { V(0.14f, 0.46f), V(0.17f, 0.46f), V(0.15f, 0.54f), V(0.12f, 0.54f) }, Vector3.zero, Vector3.zero, 12);   // high collar
        });
        Part(body, "Belt", new Vector3(0f, -0.04f, 0f), "iron", Hex(0x2a2a30), m => {
            Turn(m, new[] { V(0.17f, -0.03f), V(0.19f, -0.03f), V(0.19f, 0.03f), V(0.17f, 0.03f) }, Vector3.zero, Vector3.zero, 14);
            for (int i = 0; i < 5; i++) {
                float a = (-70f + i * 35f) * Mathf.Deg2Rad;
                Blob(m, new Vector3(Mathf.Sin(a) * 0.19f, 0f, Mathf.Cos(a) * 0.19f), new Vector3(0.016f, 0.016f, 0.016f), 6, 3);
            }
        });
        Part(body, "Bandage", new Vector3(-0.17f, -0.06f, 0.06f), "paper", Hex(0xe9e4d6), m => {
            Turn(m, new[] { V(0.02f, -0.03f), V(0.04f, -0.03f), V(0.04f, 0.03f), V(0.02f, 0.03f) }, Vector3.zero, new Vector3(90f, 0f, 0f), 10);
        });
        Transform head = Part(body, "Head", new Vector3(0f, 0.56f, 0f), "plaster", skin, m => {
            Tube(m, new Vector3(0f, -0.04f, 0f), new Vector3(0f, 0.05f, 0.01f), 0.04f, 0.035f, 8);   // neck
            Blob(m, new Vector3(0f, 0.15f, 0.01f), new Vector3(0.1f, 0.12f, 0.1f), 12, 7);
        }, 0.1f);
        Part(head, "Hair", Vector3.zero, "cloth", hair, m => {
            Blob(m, new Vector3(0f, 0.17f, -0.015f), new Vector3(0.115f, 0.12f, 0.115f), 12, 6);
            Blob(m, new Vector3(0f, 0.34f, -0.02f), new Vector3(0.065f, 0.065f, 0.065f), 8, 5);          // top-knot
            BoxAt(m, new Vector3(0f, 0.2f, 0.095f), new Vector3(0.2f, 0.07f, 0.03f), new Vector3(-10f, 0f, 0f), 0.01f);   // heavy fringe
            Tube(m, new Vector3(0.08f, 0.17f, 0.02f), new Vector3(0.09f, 0.0f, 0.04f), 0.03f, 0.014f, 6);   // side lock
        });
        Part(head, "Eyes", Vector3.zero, "glass", Hex(0x0d0b10), m => {
            BoxAt(m, new Vector3(-0.04f, 0.14f, 0.103f), new Vector3(0.04f, 0.012f, 0.01f), new Vector3(0f, 0f, -6f), 0.002f);
            BoxAt(m, new Vector3(0.04f, 0.14f, 0.103f), new Vector3(0.04f, 0.012f, 0.01f), new Vector3(0f, 0f, 6f), 0.002f);
        });
        Part(head, "Mouth", Vector3.zero, "cloth", Hex(0x5a2a3a), m => {
            BoxAt(m, new Vector3(0f, 0.075f, 0.1f), new Vector3(0.035f, 0.008f, 0.01f), Vector3.zero, 0.002f);
        });
        for (int s = -1; s <= 1; s += 2) {
            string side = s < 0 ? "L" : "R";
            Part(model, "Leg" + side, new Vector3(s * 0.09f, 0.62f, 0f), "cloth", Hex(0x211d29), m => {
                Tube(m, Vector3.zero, new Vector3(0f, -0.3f, 0.01f), 0.075f, 0.055f);
                Tube(m, new Vector3(0f, -0.3f, 0.01f), new Vector3(0f, -0.56f, 0f), 0.055f, 0.045f);
                Blob(m, new Vector3(0f, -0.3f, 0.03f), new Vector3(0.06f, 0.06f, 0.06f), 8, 4);   // knee
            });
            Part(model.Find("Leg" + side), "Boot", new Vector3(0f, -0.58f, 0.02f), "cloth", boot, m => {
                Turn(m, new[] { V(0f, 0.08f), V(0.06f, 0.08f), V(0.065f, 0.0f), V(0.05f, -0.03f), V(0f, -0.03f) }, Vector3.zero, Vector3.zero, 10);
                BoxAt(m, new Vector3(0f, -0.015f, 0.05f), new Vector3(0.1f, 0.05f, 0.18f), Vector3.zero, 0.015f);
            });
        }
        Part(model, "ArmL", new Vector3(-0.2f, 1.42f, 0f), "cloth", coat, m => {
            Tube(m, Vector3.zero, new Vector3(-0.03f, -0.24f, 0.02f), 0.055f, 0.045f);
            Tube(m, new Vector3(-0.03f, -0.24f, 0.02f), new Vector3(-0.02f, -0.46f, 0.08f), 0.045f, 0.03f);
            Blob(m, new Vector3(-0.02f, -0.49f, 0.09f), new Vector3(0.035f, 0.045f, 0.035f), 8, 4);
        });
        Transform armR = Part(model, "ArmR", new Vector3(0.2f, 1.42f, 0f), "cloth", coat, m => {
            Tube(m, Vector3.zero, new Vector3(0.05f, -0.24f, 0.03f), 0.055f, 0.045f);
            Tube(m, new Vector3(0.05f, -0.24f, 0.03f), new Vector3(0.08f, -0.44f, 0.14f), 0.045f, 0.03f);
            Blob(m, new Vector3(0.08f, -0.47f, 0.15f), new Vector3(0.035f, 0.045f, 0.035f), 8, 4);
        });
        Transform knife = Part(armR, "Prop", new Vector3(0.08f, -0.47f, 0.15f), "metal", steel, m => {
            // Scalpel: a thin handle and a long curved blade pointing forward and down.
            Turn(m, new[] { V(0f, -0.06f), V(0.014f, -0.06f), V(0.016f, 0.1f), V(0.01f, 0.14f), V(0f, 0.14f) }, Vector3.zero, new Vector3(90f, 0f, 0f), 8);
            Plate(m, new[] { V(0f, 0.0f), V(0.012f, 0.0f), V(0.34f, 0.01f), V(0.36f, -0.01f), V(0.02f, -0.02f) }, 0.006f, new Vector3(0f, 0f, 0.14f), new Vector3(0f, -90f, -8f));
        }, 0.85f, new Color(0.12f, 0.14f, 0.18f));
        return root;
    }
}
