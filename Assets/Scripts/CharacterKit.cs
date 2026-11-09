using System;
using System.Collections.Generic;
using UnityEngine;

// Authored character bodies (Docs/CharacterModels.md). Each is a part hierarchy built from KitMesh lathes, extrusions and
// tapered limbs with Model Kit materials, never a Unity primitive. The part names are the contract with Enemy's procedural
// animation: Yo-style rigs have Body, Head, ClawL, ClawR, LegL, LegR; spider rigs have Body, Head, LegL1-3, LegR1-3 (1 is
// the front pair that stabs). +Z is the front, feet on 0. Props (clipboard, order sheet, paper, ledger, scroll, plate) are
// separate meshes the boss scripts hang where their tells expect them.
public static class CharacterKit {
    public struct Def {
        public string Id, Title;
        public bool Boss, Spider;
        public int Budget;
        public string Extra;  // a prop part the boss's tell or the playtest reads by name (validation wants it in the rig)
        public float Scale;   // the rig is authored a little tall; Make fits it to the bot it replaces
    }

    public static readonly Def[] Defs = {
        new Def { Id = "courier", Title = "The Courier", Boss = true, Budget = 12000, Scale = 0.74f },
        new Def { Id = "foreman", Title = "The Foreman", Boss = true, Budget = 12000, Scale = 0.66f },
        new Def { Id = "server", Title = "The Server", Boss = true, Spider = true, Budget = 12000, Scale = 0.7f },
        new Def { Id = "order_bearer", Title = "The Order", Boss = true, Spider = true, Budget = 12000, Scale = 0.8f },
        new Def { Id = "registrar", Title = "The Registrar", Boss = true, Spider = true, Budget = 11500, Scale = 0.85f, Extra = "Ledger" },
        new Def { Id = "plated_alpha", Title = "The Plated Alpha", Boss = true, Budget = 11500, Scale = 0.72f, Extra = "Invalid Plate" },
    };

    public static readonly string[] Props = { "bat", "clipboard", "order_sheet", "paper", "ledger", "scroll", "plate" };

    public static bool Has(string id) {
        foreach (Def d in Defs) {
            if (d.Id == id) {
                return true;
            }
        }
        return false;
    }

    public static Def DefOf(string id) {
        foreach (Def d in Defs) {
            if (d.Id == id) {
                return d;
            }
        }
        return new Def { Id = id, Title = id, Budget = 6000, Scale = 1f };
    }

    public static bool IsSpider(string id) { return DefOf(id).Spider; }

    // ---- public makers -----------------------------------------------------------------------------------------------

    // The rig root, named "Bot" so Enemy treats it as the model. No colliders.
    public static GameObject Make(string id) {
        var root = new GameObject("Bot");
        if (BuildBaked(root.transform, id)) {
            goto finish;
        }
        switch (id) {
            case "courier": BuildCourier(root.transform); break;
            case "foreman": BuildForeman(root.transform); break;
            case "server": BuildServer(root.transform); break;
            case "order_bearer": BuildOrderBearer(root.transform); break;
            case "registrar": BuildRegistrar(root.transform); break;
            case "plated_alpha": BuildPlatedAlpha(root.transform); break;
            default: UnityEngine.Object.Destroy(root); return null;
        }
    finish:
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>()) {
            r.receiveShadows = true;
        }
        root.transform.localScale = Vector3.one * DefOf(id).Scale;
        var idle = root.AddComponent<CharacterIdle>();
        idle.Id = id;
        return root;
    }

    // A Gen body baked by Tools/Bake (Resources/Baked/<id>.mesh.txt): every baked part becomes a child of the rig root at its
    // pivot, named as the animators expect. False when there is none, it is unreadable or a required part is missing, and
    // the code-built body is used instead.
    static bool BuildBaked(Transform root, string id) {
        List<BakedMesh.Part> parts = BakedMesh.Load(id);
        if (parts == null) {
            return false;
        }
        Def d = DefOf(id);
        string[] need = d.Spider
            ? new[] { "Body", "Head", "LegL1", "LegL2", "LegL3", "LegR1", "LegR2", "LegR3" }
            : new[] { "Body", "Head", "ClawL", "ClawR", "LegL", "LegR" };
        foreach (string n in need) {
            if (!parts.Exists(p => p.Name == n)) {
                Debug.LogWarning("[Baked] " + id + " has no part " + n + ", using the code body");
                return false;
            }
        }
        // A part with a Parent hangs off it (a cap on the head) at its pivot minus the parent's, so the parent's turn carries it.
        // Parents are made first, whatever order the file lists them in.
        var made = new Dictionary<string, Transform>();
        var todo = new List<BakedMesh.Part>(parts);
        int guard = todo.Count + 1;
        while (todo.Count > 0 && guard-- > 0) {
            for (int i = todo.Count - 1; i >= 0; i--) {
                BakedMesh.Part p = todo[i];
                Transform at = root;
                Vector3 pos = p.Pivot;
                if (!string.IsNullOrEmpty(p.Parent)) {
                    BakedMesh.Part par = parts.Find(q => q.Name == p.Parent);
                    if (par != null && !made.TryGetValue(p.Parent, out at)) {
                        continue;   // its parent is not made yet: next pass
                    }
                    if (par == null) {
                        at = root;
                    } else {
                        pos = p.Pivot - par.Pivot;
                    }
                }
                Material m = Mat(p.Set, p.Tint);
                made[p.Name] = Part(at, p.Name, pos, p.ToMesh(id + " " + p.Name), m, m).transform;
                todo.RemoveAt(i);
            }
        }
        return true;
    }

    // A prop mesh with its own materials, no collider, no shadow.
    public static GameObject Prop(string id) {
        var go = new GameObject(id);
        switch (id) {
            case "clipboard": {
                Mesh m = Build("Clipboard", k => {
                    k.Tex = 0.4f;
                    k.BoxC(new Vector3(0f, 0f, 0f), new Vector3(0.46f, 0.62f, 0.04f), 0.012f);
                    k.BoxC(new Vector3(0f, 0.3f, 0.03f), new Vector3(0.16f, 0.07f, 0.03f), 0.008f);
                    k.Sub = 1;
                    k.BoxC(new Vector3(0f, -0.03f, 0.03f), new Vector3(0.38f, 0.5f, 0.012f), 0.004f);
                });
                Attach(go, m, Mat("wood", new Color(0.3f, 0.2f, 0.12f)), Mat("paper", new Color(1f, 0.97f, 0.85f)));
                break;
            }
            case "order_sheet": {
                Mesh m = Build("Order Sheet", k => {
                    k.Tex = 0.4f;
                    k.BoxC(new Vector3(0f, 0f, 0f), new Vector3(0.56f, 0.46f, 0.03f), 0.01f);
                    k.Sub = 1;
                    k.BoxC(new Vector3(0f, 0f, 0.022f), new Vector3(0.5f, 0.4f, 0.012f), 0.004f);
                    k.M = Matrix4x4.TRS(new Vector3(0.14f, -0.1f, 0.036f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
                    k.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.07f, 0f), new Vector2(0.07f, 0.012f), new Vector2(0f, 0.012f) }, 10);
                });
                Attach(go, m, Mat("iron", new Color(0.3f, 0.3f, 0.32f)), Mat("paper", new Color(0.96f, 0.92f, 0.8f)));
                break;
            }
            case "paper": {
                Mesh m = Build("Server Paper", k => {
                    k.Tex = 0.4f;
                    k.BoxC(new Vector3(0f, 0f, 0f), new Vector3(0.5f, 0.66f, 0.02f), 0.006f);
                    k.Sub = 1;
                    k.BoxC(new Vector3(0f, 0.31f, 0.012f), new Vector3(0.3f, 0.04f, 0.01f), 0.003f);
                    k.BoxC(new Vector3(0f, 0.1f, 0.012f), new Vector3(0.38f, 0.03f, 0.01f), 0.003f);
                    k.BoxC(new Vector3(0f, 0.01f, 0.012f), new Vector3(0.38f, 0.03f, 0.01f), 0.003f);
                    k.BoxC(new Vector3(0f, -0.08f, 0.012f), new Vector3(0.34f, 0.03f, 0.01f), 0.003f);
                });
                Attach(go, m, Mat("paper", new Color(0.98f, 0.95f, 0.84f)), Mat("iron", new Color(0.14f, 0.12f, 0.16f)));
                break;
            }
            case "ledger": {
                Mesh m = Build("Ledger", k => {
                    k.Tex = 0.4f;
                    k.BoxC(new Vector3(0f, 0f, 0f), new Vector3(0.78f, 0.48f, 0.05f), 0.012f);
                    k.Sub = 1;
                    k.BoxC(new Vector3(-0.18f, 0f, 0.035f), new Vector3(0.33f, 0.4f, 0.025f), 0.005f);
                    k.BoxC(new Vector3(0.18f, 0f, 0.035f), new Vector3(0.33f, 0.4f, 0.025f), 0.005f);
                });
                Attach(go, m, Mat("brass", new Color(0.95f, 0.76f, 0.28f)), Mat("paper", new Color(0.96f, 0.92f, 0.8f)));
                break;
            }
            case "scroll": {
                Mesh m = Build("Order Scroll", k => {
                    k.Tex = 0.4f;
                    k.Lathe(new[] { new Vector2(0f, -0.62f), new Vector2(0.15f, -0.62f), new Vector2(0.16f, -0.56f), new Vector2(0.16f, 0.56f), new Vector2(0.15f, 0.62f), new Vector2(0f, 0.62f) }, 14);
                    k.Sub = 1;
                    // end knobs and the sealed ribbon with its wax seal
                    k.Lathe(new[] { new Vector2(0f, 0.62f), new Vector2(0.07f, 0.63f), new Vector2(0.1f, 0.7f), new Vector2(0.06f, 0.76f), new Vector2(0f, 0.77f) }, 10);
                    k.Lathe(new[] { new Vector2(0f, -0.77f), new Vector2(0.06f, -0.76f), new Vector2(0.1f, -0.7f), new Vector2(0.07f, -0.63f), new Vector2(0f, -0.62f) }, 10);
                    k.Lathe(new[] { new Vector2(0.162f, -0.05f), new Vector2(0.19f, -0.05f), new Vector2(0.19f, 0.05f), new Vector2(0.162f, 0.05f) }, 14);
                    k.M = Matrix4x4.TRS(new Vector3(0.19f, 0f, 0f), Quaternion.Euler(0f, 0f, -90f), Vector3.one);
                    k.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.09f, 0f), new Vector2(0.1f, 0.01f), new Vector2(0.08f, 0.04f), new Vector2(0f, 0.045f) }, 12);
                });
                Attach(go, m, Mat("paper", new Color(0.95f, 0.9f, 0.76f)), Mat("cloth", new Color(0.8f, 0.1f, 0.1f)));
                break;
            }
            case "bat": {
                // A turned wooden bat: knob, taped grip, a swelling barrel and a rounded end, with a gilt band.
                Mesh m = Build("Wallit Bat", k => {
                    k.Tex = 0.4f;
                    k.Lathe(new[] { new Vector2(0f, -0.06f), new Vector2(0.032f, -0.05f), new Vector2(0.02f, 0f), new Vector2(0.02f, 0.3f), new Vector2(0.035f, 0.5f), new Vector2(0.05f, 0.7f), new Vector2(0.056f, 0.82f), new Vector2(0.04f, 0.9f), new Vector2(0f, 0.92f) }, 12);
                    k.Sub = 1;
                    k.Lathe(new[] { new Vector2(0.021f, 0.04f), new Vector2(0.026f, 0.04f), new Vector2(0.026f, 0.27f), new Vector2(0.021f, 0.27f) }, 12);
                    k.Lathe(new[] { new Vector2(0.052f, 0.64f), new Vector2(0.06f, 0.64f), new Vector2(0.06f, 0.67f), new Vector2(0.052f, 0.67f) }, 12);
                });
                Attach(go, m, Mat("wood", new Color(0.82f, 0.62f, 0.36f)), Mat("brass", new Color(1f, 0.82f, 0.4f)));
                break;
            }
            case "plate": {
                Mesh m = Build("Invalid Plate", k => {
                    k.Tex = 0.4f;
                    k.BoxC(new Vector3(0f, 0f, 0f), new Vector3(0.62f, 0.36f, 0.05f), 0.015f);
                    k.Sub = 1;
                    k.BoxC(new Vector3(0f, 0f, 0.03f), new Vector3(0.5f, 0.07f, 0.02f), 0.005f);
                    for (int s = -1; s <= 1; s += 2) {
                        k.M = Matrix4x4.TRS(new Vector3(s * 0.26f, 0.13f, 0.03f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
                        k.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.025f, 0f), new Vector2(0.02f, 0.015f), new Vector2(0f, 0.018f) }, 8);
                    }
                });
                Attach(go, m, Mat("metal", new Color(0.8f, 0.12f, 0.1f)), Mat("paper", new Color(0.97f, 0.93f, 0.85f)));
                break;
            }
            default:
                UnityEngine.Object.Destroy(go);
                return null;
        }
        Renderer r = go.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    // ---- counting ----------------------------------------------------------------------------------------------------

    public static int Triangles(GameObject root) {
        int total = 0;
        foreach (MeshFilter f in root.GetComponentsInChildren<MeshFilter>(true)) {
            Mesh m = f.sharedMesh;
            if (m == null) {
                continue;
            }
            for (int s = 0; s < m.subMeshCount; s++) {
                total += (int)(m.GetIndexCount(s) / 3);
            }
        }
        return total;
    }

    // ---- helpers -----------------------------------------------------------------------------------------------------

    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

    // A Model Kit material for a texture set in a chosen colour (shared per set and colour; the hit flash is a property block).
    static Material Mat(string set, Color c) {
        string key = set + "|" + ColorUtility.ToHtmlStringRGB(c);
        Material m;
        if (mats.TryGetValue(key, out m) && m != null) {
            return m;
        }
        Material src = KitTextures.Get("office", set == "lamp" ? "lamp" : set);
        m = new Material(src);
        m.name = "Char_" + key;
        m.SetColor("_Color", c);
        if (set == "lamp") {
            m.SetColor("_EmissionColor", c * 0.9f);
        }
        m.enableInstancing = true;
        mats[key] = m;
        return m;
    }

    static Mesh Build(string name, Action<KitMesh> make) {
        var k = new KitMesh { Name = name };
        make(k);
        return k.ToMesh();
    }

    static GameObject Attach(GameObject go, Mesh mesh, Material a, Material b) {
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = mesh.subMeshCount > 1 ? new[] { a, b } : new[] { a };
        return go;
    }

    static GameObject Part(Transform parent, string name, Vector3 pos, Mesh mesh, Material a, Material b) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        return Attach(go, mesh, a, b);
    }

    // A tapered limb from a to b (radius ra at a, rb at b), as a lathe turned about the line.
    static void Taper(KitMesh k, Vector3 a, Vector3 b, float ra, float rb, int seg = 8) {
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 1e-4f) {
            return;
        }
        Matrix4x4 keep = k.M;
        k.M = keep * Matrix4x4.TRS(a, Quaternion.FromToRotation(Vector3.up, d), Vector3.one);
        k.Lathe(new[] { new Vector2(0f, 0f), new Vector2(ra * 0.9f, 0f), new Vector2(ra, 0.012f), new Vector2(rb, len - 0.012f), new Vector2(rb * 0.9f, len), new Vector2(0f, len) }, seg);
        k.M = keep;
    }

    // A lathe at a point with a euler turn.
    static void Turned(KitMesh k, Vector3 at, Vector3 euler, Vector2[] profile, int seg) {
        Matrix4x4 keep = k.M;
        k.M = keep * Matrix4x4.TRS(at, Quaternion.Euler(euler), Vector3.one);
        k.Lathe(profile, seg);
        k.M = keep;
    }

    // A lathe knob (a joint or a button): small, closed.
    static void Knob(KitMesh k, Vector3 at, float r, int seg = 8) {
        Turned(k, at, Vector3.zero, new[] { new Vector2(0f, -r), new Vector2(r * 0.8f, -r * 0.6f), new Vector2(r, 0f), new Vector2(r * 0.8f, r * 0.6f), new Vector2(0f, r) }, seg);
    }

    // A claw arm hanging from its shoulder pivot: upper arm, elbow knob, forearm and two pincer fingers.
    static Mesh Claw(string name, float side, float len, float r, bool heavy) {
        return Build(name, k => {
            Vector3 elbow = new Vector3(side * 0.04f, -len * 0.5f, 0.04f);
            Vector3 wrist = new Vector3(side * 0.02f, -len, 0.12f);
            Taper(k, Vector3.zero, elbow, r, r * 0.8f);
            Knob(k, elbow, r * 0.95f);
            k.Sub = 1;
            Taper(k, elbow, wrist, r * 0.8f, r * 1.2f);
            Knob(k, wrist, r * 1.05f);
            float f = heavy ? 1.4f : 1f;
            Taper(k, wrist, wrist + new Vector3(side * 0.04f, -0.08f * f, 0.08f * f), r * 0.55f, r * 0.2f, 6);
            Taper(k, wrist, wrist + new Vector3(-side * 0.04f, -0.08f * f, 0.08f * f), r * 0.55f, r * 0.2f, 6);
        });
    }

    // A leg from the hip pivot: thigh, knee knob, shin and a flat foot.
    static Mesh Leg(string name, float len, float r, float footLen) {
        return Build(name, k => {
            Vector3 knee = new Vector3(0f, -len * 0.5f, 0.03f);
            Vector3 ankle = new Vector3(0f, -len, 0f);
            Taper(k, Vector3.zero, knee, r, r * 0.85f);
            Knob(k, knee, r * 0.95f);
            Taper(k, knee, ankle, r * 0.85f, r * 0.7f);
            k.Sub = 1;
            Turned(k, ankle + new Vector3(0f, -0.01f, 0.02f), new Vector3(90f, 0f, 0f), new[] { new Vector2(0f, -footLen * 0.5f), new Vector2(r * 1.5f, -footLen * 0.45f), new Vector2(r * 1.7f, 0f), new Vector2(r * 1.4f, footLen * 0.5f), new Vector2(0f, footLen * 0.55f) }, 8);
        });
    }

    // ---- the Courier -------------------------------------------------------------------------------------------------

    // A pot-bellied Yo Bot in postal red with a peaked cap, a mail satchel on the hip and a bandolier strap.
    static void BuildCourier(Transform root) {
        Material red = Mat("cloth", new Color(0.78f, 0.2f, 0.18f));
        Material cream = Mat("cloth", new Color(0.96f, 0.9f, 0.74f));
        Material brass = Mat("brass", new Color(1f, 0.82f, 0.4f));
        Material leather = Mat("wood", new Color(0.34f, 0.21f, 0.12f));
        Material iron = Mat("iron", new Color(0.3f, 0.3f, 0.34f));
        Material eye = Mat("lamp", new Color(0.5f, 0.92f, 1f));

        Mesh body = Build("Courier Body", k => {
            k.Tex = 0.5f;
            k.Lathe(new[] { new Vector2(0f, -0.24f), new Vector2(0.16f, -0.23f), new Vector2(0.3f, -0.12f), new Vector2(0.37f, 0.04f), new Vector2(0.34f, 0.2f), new Vector2(0.24f, 0.34f), new Vector2(0.12f, 0.4f), new Vector2(0f, 0.41f) }, 14);
            k.Sub = 1;
            Turned(k, new Vector3(0f, 0.1f, 0f), new Vector3(0f, 0f, 32f), new[] { new Vector2(0.355f, -0.045f), new Vector2(0.395f, -0.045f), new Vector2(0.395f, 0.045f), new Vector2(0.355f, 0.045f) }, 14);
            Turned(k, new Vector3(0f, -0.12f, 0f), Vector3.zero, new[] { new Vector2(0.285f, -0.03f), new Vector2(0.315f, -0.03f), new Vector2(0.315f, 0.03f), new Vector2(0.285f, 0.03f) }, 14);
        });
        GameObject b = Part(root, "Body", new Vector3(0f, 0.36f, 0f), body, red, cream);

        Mesh satchel = Build("Courier Satchel", k => {
            k.Tex = 0.3f;
            k.Box(new Vector3(-0.09f, -0.2f, -0.13f), new Vector3(0.09f, 0.02f, 0.13f), 0.025f);
            k.Sub = 1;
            k.Box(new Vector3(-0.095f, -0.01f, -0.14f), new Vector3(0.095f, 0.05f, 0.14f), 0.015f);
            k.Box(new Vector3(0.09f, -0.04f, -0.02f), new Vector3(0.105f, -0.0f, 0.02f), 0.004f);
        });
        Part(b.transform, "Satchel", new Vector3(0.42f, 0.0f, -0.02f), satchel, leather, brass);

        Mesh pack = Build("Courier Jet Pack", k => {
            k.Tex = 0.4f;
            k.Box(new Vector3(-0.17f, -0.1f, -0.2f), new Vector3(0.17f, 0.3f, 0.0f), 0.03f);
            k.Sub = 1;
            for (int s = -1; s <= 1; s += 2) {
                Turned(k, new Vector3(s * 0.09f, -0.1f, -0.1f), Vector3.zero, new[] { new Vector2(0f, -0.07f), new Vector2(0.05f, -0.07f), new Vector2(0.06f, -0.1f), new Vector2(0.06f, 0f), new Vector2(0f, 0f) }, 8);
            }
        });
        Part(b.transform, "Jet Pack", new Vector3(0f, 0.05f, -0.3f), pack, iron, brass);

        Mesh head = Build("Courier Head", k => {
            k.Tex = 0.4f;
            k.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.15f, 0.0f), new Vector2(0.22f, 0.06f), new Vector2(0.23f, 0.15f), new Vector2(0.18f, 0.25f), new Vector2(0.08f, 0.3f), new Vector2(0f, 0.31f) }, 12);
            k.Sub = 1;
            k.Box(new Vector3(-0.16f, 0.1f, 0.18f), new Vector3(0.16f, 0.2f, 0.235f), 0.02f);
            k.Sub = 0;
        });
        GameObject h = Part(root, "Head", new Vector3(0f, 0.88f, 0.03f), head, cream, eye);

        Mesh cap = Build("Courier Cap", k => {
            k.Tex = 0.4f;
            k.Lathe(new[] { new Vector2(0f, 0.24f), new Vector2(0.24f, 0.24f), new Vector2(0.25f, 0.27f), new Vector2(0.2f, 0.36f), new Vector2(0.1f, 0.4f), new Vector2(0f, 0.41f) }, 12);
            k.Sub = 1;
            k.Box(new Vector3(-0.17f, 0.235f, 0.18f), new Vector3(0.17f, 0.26f, 0.4f), 0.01f);
            Turned(k, new Vector3(0f, 0.31f, 0.235f), new Vector3(75f, 0f, 0f), new[] { new Vector2(0f, 0f), new Vector2(0.05f, 0f), new Vector2(0.05f, 0.015f), new Vector2(0f, 0.02f) }, 8);
        });
        Part(h.transform, "Cap", Vector3.zero, cap, red, brass);

        Part(root, "ClawL", new Vector3(-0.4f, 0.62f, 0.05f), Claw("Courier Claw L", -1f, 0.4f, 0.06f, false), red, iron);
        Part(root, "ClawR", new Vector3(0.4f, 0.62f, 0.05f), Claw("Courier Claw R", 1f, 0.4f, 0.06f, false), red, iron);
        Part(root, "LegL", new Vector3(-0.14f, 0.24f, 0f), Leg("Courier Leg L", 0.24f, 0.055f, 0.2f), red, leather);
        Part(root, "LegR", new Vector3(0.14f, 0.24f, 0f), Leg("Courier Leg R", 0.24f, 0.055f, 0.2f), red, leather);
    }

    // ---- the Foreman -------------------------------------------------------------------------------------------------

    // A broad Mega Yo Bot: an extruded square-shouldered torso, shoulder pistons, a hard hat with a lamp and a hi-vis strap.
    static void BuildForeman(Transform root) {
        Material amber = Mat("metal", new Color(1f, 0.72f, 0.3f));
        Material soot = Mat("iron", new Color(0.26f, 0.25f, 0.27f));
        Material lime = Mat("cloth", new Color(0.8f, 1f, 0.2f));
        Material brass = Mat("brass", new Color(1f, 0.82f, 0.4f));
        Material eye = Mat("lamp", new Color(1f, 0.62f, 0.2f));

        Mesh body = Build("Foreman Torso", k => {
            k.Tex = 0.5f;
            k.ExtrudeZ(new[] { new Vector2(-0.3f, -0.22f), new Vector2(0.3f, -0.22f), new Vector2(0.46f, 0.26f), new Vector2(0.36f, 0.5f), new Vector2(-0.36f, 0.5f), new Vector2(-0.46f, 0.26f) }, -0.26f, 0.26f);
            k.Sub = 1;
            // a hi-vis chest strap on each side and a belt
            for (int s = -1; s <= 1; s += 2) {
                k.Box(new Vector3(s * 0.2f - 0.04f, -0.05f, 0.255f), new Vector3(s * 0.2f + 0.04f, 0.5f, 0.275f), 0.005f);
            }
            k.Box(new Vector3(-0.47f, -0.12f, -0.27f), new Vector3(0.47f, -0.02f, 0.27f), 0.02f);
        });
        GameObject b = Part(root, "Body", new Vector3(0f, 0.5f, 0f), body, amber, lime);

        Mesh shoulders = Build("Foreman Shoulders", k => {
            k.Tex = 0.4f;
            for (int s = -1; s <= 1; s += 2) {
                Turned(k, new Vector3(s * 0.48f, 0.36f, 0f), Vector3.zero, new[] { new Vector2(0f, -0.1f), new Vector2(0.16f, -0.1f), new Vector2(0.2f, 0f), new Vector2(0.16f, 0.1f), new Vector2(0f, 0.12f) }, 10);
            }
            k.Sub = 1;
            for (int s = -1; s <= 1; s += 2) {
                Taper(k, new Vector3(s * 0.46f, 0.3f, -0.1f), new Vector3(s * 0.46f, 0.55f, -0.1f), 0.03f, 0.03f, 6);
            }
        });
        Part(b.transform, "Shoulders", Vector3.zero, shoulders, soot, brass);

        Mesh head = Build("Foreman Head", k => {
            k.Tex = 0.4f;
            k.Box(new Vector3(-0.2f, 0f, -0.15f), new Vector3(0.2f, 0.26f, 0.2f), 0.04f);
            k.Sub = 1;
            k.Box(new Vector3(-0.15f, 0.09f, 0.195f), new Vector3(0.15f, 0.19f, 0.215f), 0.01f);
            k.Sub = 0;
        });
        GameObject h = Part(root, "Head", new Vector3(0f, 1.02f, 0.04f), head, soot, eye);

        Mesh hat = Build("Foreman Hard Hat", k => {
            k.Tex = 0.4f;
            k.Lathe(new[] { new Vector2(0f, 0.25f), new Vector2(0.3f, 0.25f), new Vector2(0.3f, 0.285f), new Vector2(0.23f, 0.31f), new Vector2(0.2f, 0.4f), new Vector2(0.1f, 0.46f), new Vector2(0f, 0.47f) }, 12);
            k.Sub = 1;
            k.Box(new Vector3(-0.025f, 0.3f, -0.2f), new Vector3(0.025f, 0.455f, 0.2f), 0.008f);
            Turned(k, new Vector3(0f, 0.36f, 0.2f), new Vector3(90f, 0f, 0f), new[] { new Vector2(0f, 0f), new Vector2(0.06f, 0f), new Vector2(0.07f, 0.03f), new Vector2(0.04f, 0.06f), new Vector2(0f, 0.06f) }, 8);
        });
        Part(h.transform, "Hard Hat", Vector3.zero, hat, amber, lime);

        Part(root, "ClawL", new Vector3(-0.58f, 0.82f, 0.05f), Claw("Foreman Claw L", -1f, 0.52f, 0.09f, true), soot, amber);
        Part(root, "ClawR", new Vector3(0.58f, 0.82f, 0.05f), Claw("Foreman Claw R", 1f, 0.52f, 0.09f, true), soot, amber);
        Part(root, "LegL", new Vector3(-0.2f, 0.3f, 0f), Leg("Foreman Leg L", 0.3f, 0.09f, 0.3f), soot, amber);
        Part(root, "LegR", new Vector3(0.2f, 0.3f, 0f), Leg("Foreman Leg R", 0.3f, 0.09f, 0.3f), soot, amber);
    }

    // ---- spiders -----------------------------------------------------------------------------------------------------

    // One jointed spider leg from the hip pivot: out and up, a knee knob, then down to a pointed foot.
    static Mesh SpiderLeg(string name, float side, float reachX, float splayZ, float r, float tall, float hipY) {
        return Build(name, k => {
            Vector3 knee = new Vector3(side * 0.3f * reachX, tall * 0.5f, splayZ * 0.5f);
            Vector3 foot = new Vector3(side * 0.62f * reachX, -hipY, splayZ * 1.8f);
            Taper(k, Vector3.zero, knee, r * 1.1f, r * 0.9f, 7);
            k.Sub = 1;
            Knob(k, knee, r * 1.5f, 8);
            Taper(k, knee, foot, r * 0.9f, r * 0.3f, 7);
        });
    }

    static void SpiderLegs(Transform root, string prefix, float hipY, Material a, Material b, float reach, float r, float tall) {
        float[] hipZ = { 0.2f, 0.02f, -0.18f };
        float[] splay = { 0.24f, 0.0f, -0.24f };
        for (int pair = 0; pair < 3; pair++) {
            for (int s = -1; s <= 1; s += 2) {
                string nm = (s < 0 ? "LegL" : "LegR") + (pair + 1);
                Part(root, nm, new Vector3(s * 0.2f, hipY, hipZ[pair]), SpiderLeg(prefix + nm, s, reach, splay[pair], r, tall, hipY), a, b);
            }
        }
    }

    // The Server: a tall, narrow, polite spider in pale blue and ivory, a bow-tie plate under a calm single-eye head, and a
    // front leg that can hold out the paper.
    static void BuildServer(Transform root) {
        Material blue = Mat("metal", new Color(0.7f, 0.84f, 0.98f));
        Material ivory = Mat("cloth", new Color(0.97f, 0.94f, 0.84f));
        Material steel = Mat("iron", new Color(0.48f, 0.52f, 0.6f));
        Material eye = Mat("lamp", new Color(0.45f, 0.75f, 1f));

        Mesh body = Build("Server Body", k => {
            k.Tex = 0.5f;
            k.Lathe(new[] { new Vector2(0f, -0.2f), new Vector2(0.17f, -0.19f), new Vector2(0.27f, -0.06f), new Vector2(0.27f, 0.1f), new Vector2(0.19f, 0.24f), new Vector2(0.08f, 0.3f), new Vector2(0f, 0.31f) }, 12);
            k.Sub = 1;
            // a neat ivory shirt-front and a buttoned stripe
            k.Box(new Vector3(-0.1f, -0.14f, 0.2f), new Vector3(0.1f, 0.24f, 0.265f), 0.02f);
            for (int i = 0; i < 3; i++) {
                Knob(k, new Vector3(0f, -0.08f + i * 0.1f, 0.27f), 0.018f, 6);
            }
        });
        Part(root, "Body", new Vector3(0f, 0.78f, -0.04f), body, blue, ivory);

        Mesh head = Build("Server Head", k => {
            k.Tex = 0.4f;
            k.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.1f, 0f), new Vector2(0.15f, 0.07f), new Vector2(0.14f, 0.17f), new Vector2(0.08f, 0.23f), new Vector2(0f, 0.25f) }, 10);
            k.Sub = 1;
            Knob(k, new Vector3(0f, 0.12f, 0.12f), 0.06f, 8);
            // the bow-tie under the chin
            k.Box(new Vector3(-0.11f, -0.07f, 0.04f), new Vector3(-0.015f, -0.01f, 0.1f), 0.01f);
            k.Box(new Vector3(0.015f, -0.07f, 0.04f), new Vector3(0.11f, -0.01f, 0.1f), 0.01f);
            Knob(k, new Vector3(0f, -0.04f, 0.075f), 0.024f, 6);
        });
        Part(root, "Head", new Vector3(0f, 1.14f, 0.2f), head, ivory, eye);

        SpiderLegs(root, "Server ", 0.78f, steel, ivory, 0.9f, 0.032f, 0.5f);
    }

    // The Order-Bearer: a wide, hooded spider that carries a sealed scroll on a crossbar. It has no face, only a smooth plate:
    // the creature behind the order is never seen.
    static void BuildOrderBearer(Transform root) {
        Material black = Mat("iron", new Color(0.16f, 0.14f, 0.18f));
        Material parchment = Mat("paper", new Color(0.95f, 0.9f, 0.76f));
        Material seal = Mat("cloth", new Color(0.78f, 0.1f, 0.1f));
        Material glow = Mat("lamp", new Color(1f, 0.25f, 0.2f));

        Mesh body = Build("Order Body", k => {
            k.Tex = 0.5f;
            k.Lathe(new[] { new Vector2(0f, -0.2f), new Vector2(0.22f, -0.19f), new Vector2(0.4f, -0.05f), new Vector2(0.42f, 0.1f), new Vector2(0.3f, 0.22f), new Vector2(0.12f, 0.27f), new Vector2(0f, 0.28f) }, 14);
            k.Sub = 1;
            // a crossbar behind the shoulders that holds the scroll, and two red straps around the body
            k.Box(new Vector3(-0.55f, 0.18f, -0.34f), new Vector3(0.55f, 0.26f, -0.24f), 0.02f);
            for (int s = -1; s <= 1; s += 2) {
                Turned(k, new Vector3(0f, 0.04f, 0f), Vector3.zero, new[] { new Vector2(0.405f, s * 0.07f - 0.025f), new Vector2(0.435f, s * 0.07f - 0.025f), new Vector2(0.435f, s * 0.07f + 0.025f), new Vector2(0.405f, s * 0.07f + 0.025f) }, 14);
            }
        });
        GameObject b = Part(root, "Body", new Vector3(0f, 0.6f, -0.04f), body, black, seal);

        // The scroll lies across the back, along X, with the seal turned up.
        GameObject scroll = Prop("scroll");
        scroll.name = "Scroll";
        scroll.transform.SetParent(b.transform, false);
        scroll.transform.localPosition = new Vector3(0f, 0.34f, -0.34f);
        scroll.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        scroll.transform.localScale = Vector3.one * 0.9f;

        Mesh head = Build("Order Head", k => {
            k.Tex = 0.4f;
            // a smooth hood over a blank plate
            k.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.14f, 0f), new Vector2(0.19f, 0.07f), new Vector2(0.17f, 0.17f), new Vector2(0.09f, 0.24f), new Vector2(0f, 0.26f) }, 12);
            k.Sub = 1;
            Knob(k, new Vector3(0f, 0.12f, 0.16f), 0.035f, 8);
        });
        Part(root, "Head", new Vector3(0f, 0.86f, 0.3f), head, black, glow);

        SpiderLegs(root, "Order ", 0.6f, black, seal, 1.15f, 0.045f, 0.3f);
    }

    // ---- the Registrar -----------------------------------------------------------------------------------------------

    // A Titan Spider Bot in gold and black: a wide body with a lectern-like slope across its back, a spectacled head and an open
    // ledger held before it, with a loose page ("Page", a child of "Ledger") that turns. Six legs as the spider rig names them.
    static void BuildRegistrar(Transform root) {
        Material black = Mat("iron", new Color(0.13f, 0.12f, 0.15f));
        Material gold = Mat("brass", new Color(0.96f, 0.76f, 0.26f));
        Material paper = Mat("paper", new Color(0.97f, 0.93f, 0.8f));
        Material ink = Mat("cloth", new Color(0.2f, 0.16f, 0.2f));
        Material eye = Mat("lamp", new Color(1f, 0.82f, 0.3f));

        Mesh body = Build("Registrar Body", k => {
            k.Tex = 0.5f;
            k.Lathe(new[] { new Vector2(0f, -0.26f), new Vector2(0.24f, -0.25f), new Vector2(0.44f, -0.1f), new Vector2(0.5f, 0.08f), new Vector2(0.4f, 0.24f), new Vector2(0.18f, 0.32f), new Vector2(0f, 0.33f) }, 16);
            k.Sub = 1;
            // gilt bands round the belly
            Turned(k, new Vector3(0f, -0.06f, 0f), Vector3.zero, new[] { new Vector2(0.455f, -0.03f), new Vector2(0.505f, -0.03f), new Vector2(0.505f, 0.03f), new Vector2(0.455f, 0.03f) }, 16);
            Turned(k, new Vector3(0f, 0.17f, 0f), Vector3.zero, new[] { new Vector2(0.37f, -0.025f), new Vector2(0.415f, -0.025f), new Vector2(0.415f, 0.025f), new Vector2(0.37f, 0.025f) }, 16);
            // the lectern: a sloped desk on a post across the back, with a lip
            k.Orient(new Vector3(0f, 0.36f, -0.3f), new Vector3(-28f, 0f, 0f));
            k.Box(new Vector3(-0.4f, -0.025f, -0.2f), new Vector3(0.4f, 0.025f, 0.2f), 0.012f);
            k.Box(new Vector3(-0.4f, -0.07f, 0.17f), new Vector3(0.4f, 0.03f, 0.2f), 0.01f);
            k.Reset();
            k.Sub = 1;
            k.Box(new Vector3(-0.06f, 0.1f, -0.34f), new Vector3(0.06f, 0.36f, -0.22f), 0.012f);
        });
        GameObject b = Part(root, "Body", new Vector3(0f, 0.8f, -0.06f), body, black, gold);

        Mesh head = Build("Registrar Head", k => {
            k.Tex = 0.4f;
            k.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.12f, 0f), new Vector2(0.18f, 0.07f), new Vector2(0.17f, 0.18f), new Vector2(0.1f, 0.25f), new Vector2(0f, 0.27f) }, 12);
            k.Sub = 1;
            // a pair of round gold spectacles over two glass eyes
            for (int s = -1; s <= 1; s += 2) {
                Turned(k, new Vector3(s * 0.075f, 0.14f, 0.15f), new Vector3(90f, 0f, 0f), new[] { new Vector2(0.045f, -0.008f), new Vector2(0.062f, -0.008f), new Vector2(0.062f, 0.012f), new Vector2(0.045f, 0.012f) }, 10);
                Knob(k, new Vector3(s * 0.075f, 0.14f, 0.152f), 0.032f, 8);
            }
            k.Box(new Vector3(-0.03f, 0.135f, 0.15f), new Vector3(0.03f, 0.15f, 0.165f), 0.004f);
        });
        Part(root, "Head", new Vector3(0f, 1.2f, 0.34f), head, black, eye);
        // the head's spectacle rims are gold, the lenses lit: sub 1 is the lit lamp set, so the rims share it (a gilt glint)

        // The ledger, held open before the body: a spine, two tilted page blocks and gold-edged covers.
        Mesh ledger = Build("Registrar Ledger", k => {
            k.Tex = 0.4f;
            k.Orient(Vector3.zero, new Vector3(0f, 0f, 12f));
            k.Box(new Vector3(-0.56f, -0.26f, -0.03f), new Vector3(0.0f, 0.26f, 0.0f), 0.008f);
            k.Orient(Vector3.zero, new Vector3(0f, 0f, -12f));
            k.Box(new Vector3(0.0f, -0.26f, -0.03f), new Vector3(0.56f, 0.26f, 0.0f), 0.008f);
            k.Reset();
            k.Box(new Vector3(-0.025f, -0.28f, -0.05f), new Vector3(0.025f, 0.28f, 0.03f), 0.008f);
            k.Sub = 1;
            k.Orient(Vector3.zero, new Vector3(0f, 0f, 12f));
            k.Box(new Vector3(-0.53f, -0.23f, 0f), new Vector3(-0.03f, 0.23f, 0.03f), 0.004f);
            k.Orient(Vector3.zero, new Vector3(0f, 0f, -12f));
            k.Box(new Vector3(0.03f, -0.23f, 0f), new Vector3(0.53f, 0.23f, 0.03f), 0.004f);
            k.Reset();
        });
        GameObject lg = Part(root, "Ledger", new Vector3(0f, 1.0f, 0.72f), ledger, ink, paper);
        lg.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
        Mesh page = Build("Registrar Page", k => {
            k.Tex = 0.3f;
            k.Box(new Vector3(0f, -0.2f, 0f), new Vector3(0.46f, 0.2f, 0.012f), 0.004f);
            k.Sub = 1;
            k.Box(new Vector3(0.06f, 0.12f, 0.012f), new Vector3(0.4f, 0.14f, 0.02f), 0.002f);
            k.Box(new Vector3(0.06f, 0.04f, 0.012f), new Vector3(0.4f, 0.06f, 0.02f), 0.002f);
            k.Box(new Vector3(0.06f, -0.04f, 0.012f), new Vector3(0.34f, -0.02f, 0.02f), 0.002f);
        });
        Part(lg.transform, "Page", new Vector3(0.0f, 0f, 0.03f), page, paper, ink);

        SpiderLegs(root, "Registrar ", 0.8f, black, gold, 1.25f, 0.06f, 0.62f);
    }

    // ---- the Plated Alpha --------------------------------------------------------------------------------------------

    // Chapter 2's Alpha: a Plated Yo Bot grown big. A slab of armour with riveted shoulder plates and a visor helm, and a red
    // INVALID plate bolted to the chest as the part "Invalid Plate".
    static void BuildPlatedAlpha(Transform root) {
        Material steel = Mat("metal", new Color(0.62f, 0.64f, 0.7f));
        Material dark = Mat("iron", new Color(0.24f, 0.24f, 0.28f));
        Material red = Mat("metal", new Color(0.8f, 0.12f, 0.1f));
        Material brass = Mat("brass", new Color(1f, 0.82f, 0.4f));
        Material eye = Mat("lamp", new Color(1f, 0.35f, 0.25f));

        Mesh body = Build("Plated Torso", k => {
            k.Tex = 0.5f;
            k.ExtrudeZ(new[] { new Vector2(-0.3f, -0.24f), new Vector2(0.3f, -0.24f), new Vector2(0.42f, 0.2f), new Vector2(0.34f, 0.46f), new Vector2(-0.34f, 0.46f), new Vector2(-0.42f, 0.2f) }, -0.24f, 0.24f);
            k.Sub = 1;
            // riveted seams across the chest plating
            for (int i = -1; i <= 1; i++) {
                k.Box(new Vector3(i * 0.2f - 0.012f, -0.2f, 0.235f), new Vector3(i * 0.2f + 0.012f, 0.42f, 0.25f), 0.004f);
            }
            k.Box(new Vector3(-0.44f, -0.1f, -0.25f), new Vector3(0.44f, -0.03f, 0.25f), 0.015f);
            for (int s = -1; s <= 1; s += 2) {
                for (int j = 0; j < 3; j++) {
                    Knob(k, new Vector3(s * 0.28f, -0.1f + j * 0.18f, 0.26f), 0.022f, 6);
                }
            }
        });
        GameObject b = Part(root, "Body", new Vector3(0f, 0.5f, 0f), body, steel, dark);

        Mesh pauldrons = Build("Plated Pauldrons", k => {
            k.Tex = 0.4f;
            for (int s = -1; s <= 1; s += 2) {
                // three overlapping curved plates over each shoulder
                for (int j = 0; j < 3; j++) {
                    Turned(k, new Vector3(s * (0.46f + j * 0.02f), 0.34f - j * 0.06f, 0f), new Vector3(0f, 0f, s * (20f + j * 14f)), new[] { new Vector2(0f, -0.02f), new Vector2(0.17f - j * 0.02f, -0.02f), new Vector2(0.19f - j * 0.02f, 0.02f), new Vector2(0.1f, 0.06f), new Vector2(0f, 0.07f) }, 10);
                }
            }
            k.Sub = 1;
            for (int s = -1; s <= 1; s += 2) {
                Knob(k, new Vector3(s * 0.52f, 0.43f, 0.0f), 0.03f, 6);
            }
        });
        Part(b.transform, "Pauldrons", Vector3.zero, pauldrons, steel, brass);

        // The stamped plate on the chest: a part the playtest and the fight look for by name.
        GameObject plate = Prop("plate");
        plate.name = "Invalid Plate";
        plate.transform.SetParent(b.transform, false);
        plate.transform.localPosition = new Vector3(0f, 0.1f, 0.3f);
        plate.transform.localRotation = Quaternion.Euler(-8f, 0f, 4f);
        plate.transform.localScale = Vector3.one * 0.9f;

        Mesh head = Build("Plated Head", k => {
            k.Tex = 0.4f;
            k.Lathe(new[] { new Vector2(0f, 0f), new Vector2(0.17f, 0f), new Vector2(0.22f, 0.08f), new Vector2(0.21f, 0.2f), new Vector2(0.14f, 0.29f), new Vector2(0.05f, 0.33f), new Vector2(0f, 0.34f) }, 14);
            k.Sub = 1;
            // a slit visor with a red glow and a brass crest ridge
            k.Box(new Vector3(-0.15f, 0.12f, 0.17f), new Vector3(0.15f, 0.17f, 0.235f), 0.012f);
            k.Box(new Vector3(-0.02f, 0.2f, -0.2f), new Vector3(0.02f, 0.36f, 0.16f), 0.008f);
        });
        Part(root, "Head", new Vector3(0f, 1.0f, 0.05f), head, steel, eye);

        Part(root, "ClawL", new Vector3(-0.56f, 0.8f, 0.05f), Claw("Plated Claw L", -1f, 0.5f, 0.085f, true), dark, brass);
        Part(root, "ClawR", new Vector3(0.56f, 0.8f, 0.05f), Claw("Plated Claw R", 1f, 0.5f, 0.085f, true), dark, brass);
        Part(root, "LegL", new Vector3(-0.2f, 0.3f, 0f), Leg("Plated Leg L", 0.3f, 0.09f, 0.3f), dark, steel);
        Part(root, "LegR", new Vector3(0.2f, 0.3f, 0f), Leg("Plated Leg R", 0.3f, 0.09f, 0.3f), dark, steel);
    }
}

// The idle personality of a character, added to its rig root by CharacterKit.Make. It runs in LateUpdate on top of Enemy's own
// animation (which sets the rest-relative rotations every frame), only while the character is standing still, so the walk,
// the tell and the strike are never touched. The Courier checks its clipboard and tips its cap, the Foreman tilts his hat
// back and taps his claw, the Server stands politely and bows with the paper, the Order breathes with its seal.
public class CharacterIdle : MonoBehaviour {
    public string Id;
    Transform head, body, clawL, clawR, scroll, page, plate;
    Vector3 lastPos;
    float still;
    float phase;

    void Start() {
        head = Find("Head");
        body = Find("Body");
        clawL = Find("ClawL");
        clawR = Find("ClawR");
        scroll = Find("Scroll");
        page = Find("Page");
        plate = Find("Invalid Plate");
        lastPos = transform.parent != null ? transform.parent.position : transform.position;
        phase = UnityEngine.Random.Range(0f, 5f);
    }

    Transform Find(string n) {
        foreach (Transform t in GetComponentsInChildren<Transform>(true)) {
            if (t.name == n) {
                return t;
            }
        }
        return null;
    }

    // 0 to 1 over an interval of a repeating cycle: rises, holds, falls.
    static float Pulse(float t, float from, float rise, float hold, float fall) {
        if (t < from || t > from + rise + hold + fall) {
            return 0f;
        }
        float u = t - from;
        if (u < rise) {
            return Mathf.SmoothStep(0f, 1f, u / rise);
        }
        if (u < rise + hold) {
            return 1f;
        }
        return 1f - Mathf.SmoothStep(0f, 1f, (u - rise - hold) / fall);
    }

    void LateUpdate() {
        Transform mover = transform.parent != null ? transform.parent : transform;
        float dt = Time.deltaTime;
        if (dt <= 0f) {
            return;
        }
        Vector3 d = mover.position - lastPos;
        d.y = 0f;
        lastPos = mover.position;
        bool moving = d.magnitude / dt > 0.15f;
        still = Mathf.MoveTowards(still, moving ? 0f : 1f, dt * 3f);
        if (still <= 0.01f) {
            return;
        }
        float t = Time.time + phase;
        switch (Id) {
            case "courier": {
                float c = t % 7f;
                float check = Pulse(c, 0.5f, 0.5f, 1.4f, 0.5f);   // raises the clipboard arm, nods at it
                float tap = Mathf.Sin(t * 16f) * 6f * Pulse(c, 1.2f, 0.1f, 0.9f, 0.1f);
                float tip = Pulse(c, 4.2f, 0.35f, 0.7f, 0.35f);   // the other claw tips the cap
                Turn(clawR, Quaternion.Euler(-70f * check + tap, 0f, 0f), still);
                Turn(clawL, Quaternion.Euler(-120f * tip, 0f, 0f), still);
                Turn(head, Quaternion.Euler(16f * check - 6f * tip, 0f, 0f), still);
                break;
            }
            case "foreman": {
                float c = t % 8f;
                float tilt = Pulse(c, 1f, 0.5f, 1.2f, 0.5f);     // hat tipped back, a long look at the shift
                float tap = Pulse(c, 4f, 0.2f, 1.6f, 0.2f);
                Turn(head, Quaternion.Euler(-16f * tilt, 0f, 0f), still);
                Turn(clawR, Quaternion.Euler(-50f * tap + Mathf.Sin(t * 9f) * 5f * tap, 0f, 0f), still);
                Turn(body, Quaternion.Euler(0f, Mathf.Sin(t * 0.6f) * 3f, 0f), still);
                break;
            }
            case "server": {
                float c = t % 6f;
                float bow = Pulse(c, 2f, 0.6f, 0.8f, 0.6f);      // a small polite bow, as if offering
                Turn(head, Quaternion.Euler(14f * bow, 0f, 0f), still);
                Turn(body, Quaternion.Euler(6f * bow, 0f, 0f), still);
                break;
            }
            case "registrar": {
                float c = t % 6f;
                float turn = Pulse(c, 1.5f, 0.7f, 0.2f, 0.5f);   // turns a page, then settles
                Turn(page, Quaternion.Euler(0f, -150f * turn, 0f), still);
                Turn(head, Quaternion.Euler(10f * Pulse(c, 1.2f, 0.4f, 1.2f, 0.4f), 0f, 0f), still);
                break;
            }
            case "plated_alpha": {
                float c = t % 7f;
                float roll = Pulse(c, 1f, 0.4f, 0.6f, 0.5f);      // rolls the shoulders, the INVALID plate rocking
                float knock = Pulse(c, 4f, 0.15f, 1.2f, 0.15f);   // knocks a claw on its own plate
                Turn(body, Quaternion.Euler(0f, 0f, Mathf.Sin(t * 3f) * 3f * roll), still);
                Turn(clawL, Quaternion.Euler(-70f * knock + Mathf.Sin(t * 14f) * 5f * knock, 0f, 0f), still);
                Turn(plate, Quaternion.Euler(0f, 0f, 4f * roll), still);
                break;
            }
            case "order_bearer": {
                float breathe = Mathf.Sin(t * 1.2f);
                if (body != null) {
                    body.localScale = Vector3.one * (1f + 0.02f * breathe * still);
                }
                Turn(scroll, Quaternion.Euler(0f, Mathf.Sin(t * 0.7f) * 4f, 0f), still);
                break;
            }
        }
    }

    // Adds q on top of what the transform's owner set this frame. When nobody rewrote it since our last write (the rotation is
    // still the one we left), the kept base is used, so a frame the owner skipped never stacks the turn.
    class TurnState { public Quaternion Wrote, Base; }
    readonly Dictionary<Transform, TurnState> turns = new Dictionary<Transform, TurnState>();

    void Turn(Transform t, Quaternion q, float weight) {
        if (t == null) {
            return;
        }
        Quaternion b = t.localRotation;
        TurnState st;
        if (!turns.TryGetValue(t, out st)) {
            st = new TurnState();
            turns[t] = st;
        } else if (Quaternion.Angle(st.Wrote, b) < 0.001f) {
            b = st.Base;
        }
        st.Base = b;
        t.localRotation = b * Quaternion.Slerp(Quaternion.identity, q, weight);
        st.Wrote = t.localRotation;
    }
}
