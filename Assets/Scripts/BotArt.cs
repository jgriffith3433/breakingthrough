using System.Collections.Generic;
using UnityEngine;

// The bots. Until the real models land, each kind is a low-poly stand-in built here from boxes, rods and balls,
// painted with vertex colors and drawn with the knoll shader like the rest of the hill. An export of
// Tools/Blender/bt_bots.py in Resources/Models/Bots/<Kind> replaces the stand-in with no code change.
// Parts are separate objects so Enemy can animate them:
//   Yo Bots:     Body, Head, ClawL, ClawR, LegL, LegR
//   Spider Bots: Body, Head, LegL1..LegL4, LegR1..LegR4 (1 is the front pair, the one that stabs)
// +Z is the front and the feet sit on y = 0.
public static class BotArt {
    struct Look {
        public Color Paint;
        public Color Trim;
        public Color Dark;
        public Color Eye;
        public float Wide;
        public float Legs;
        public bool Plates;
        public bool Horns;
        public bool Ram;
        public bool Pincers;
        public bool Crown;
    }

    struct PartMesh {
        public string Name;
        public Vector3 Pivot;
        public Mesh Mesh;
    }

    static readonly Dictionary<BotKind, GameObject> sources = new Dictionary<BotKind, GameObject>();
    static readonly Dictionary<BotKind, PartMesh[]> standIns = new Dictionary<BotKind, PartMesh[]>();

    static GameObject Source(BotKind kind) {
        GameObject source;
        if (!sources.TryGetValue(kind, out source)) {
            source = Resources.Load<GameObject>("Models/Bots/" + kind);
            sources[kind] = source;
        }
        return source;
    }

    static Look LookOf(BotKind kind) {
        Color dark = new Color(0.1f, 0.1f, 0.12f);
        switch (kind) {
            case BotKind.PlatedYoBot:
                return new Look { Paint = new Color(0.5f, 0.56f, 0.64f), Trim = new Color(0.3f, 0.33f, 0.38f), Dark = dark, Eye = new Color(1f, 0.62f, 0.2f), Wide = 1.18f, Plates = true };
            case BotKind.MegaYoBot:
                return new Look { Paint = new Color(0.78f, 0.2f, 0.14f), Trim = new Color(0.22f, 0.22f, 0.25f), Dark = dark, Eye = new Color(1f, 0.92f, 0.35f), Wide = 1.08f, Plates = true, Horns = true };
            case BotKind.SpiderBot:
                return new Look { Paint = new Color(0.26f, 0.44f, 0.46f), Trim = new Color(0.18f, 0.24f, 0.26f), Dark = dark, Eye = new Color(1f, 0.25f, 0.15f), Legs = 1f };
            case BotKind.StalkerSpiderBot:
                return new Look { Paint = new Color(0.74f, 0.76f, 0.8f), Trim = new Color(0.4f, 0.42f, 0.46f), Dark = dark, Eye = new Color(0.45f, 0.9f, 1f), Legs = 1.1f };
            case BotKind.RunnerSpiderBot:
                return new Look { Paint = new Color(0.86f, 0.45f, 0.14f), Trim = new Color(0.24f, 0.2f, 0.18f), Dark = dark, Eye = new Color(0.95f, 0.95f, 0.9f), Legs = 1.3f };
            case BotKind.RammerSpiderBot:
                return new Look { Paint = new Color(0.7f, 0.6f, 0.42f), Trim = new Color(0.36f, 0.3f, 0.22f), Dark = dark, Eye = new Color(1f, 0.3f, 0.2f), Legs = 1f, Ram = true };
            case BotKind.ShadowSpiderBot:
                return new Look { Paint = new Color(0.15f, 0.12f, 0.22f), Trim = new Color(0.3f, 0.2f, 0.42f), Dark = new Color(0.05f, 0.05f, 0.07f), Eye = new Color(0.8f, 0.35f, 1f), Legs = 1.05f };
            case BotKind.CrusherSpiderBot:
                return new Look { Paint = new Color(0.5f, 0.28f, 0.18f), Trim = new Color(0.3f, 0.2f, 0.14f), Dark = dark, Eye = new Color(1f, 0.55f, 0.15f), Legs = 0.95f, Pincers = true };
            case BotKind.ColossusSpiderBot:
                return new Look { Paint = new Color(0.25f, 0.35f, 0.7f), Trim = new Color(0.14f, 0.18f, 0.36f), Dark = dark, Eye = new Color(0.9f, 0.95f, 1f), Legs = 1.15f, Ram = true };
            case BotKind.TitanSpiderBot:
                // Warm gold trim glows through BirdArt.Glowing.
                return new Look { Paint = new Color(0.36f, 0.28f, 0.12f), Trim = new Color(0.95f, 0.75f, 0.15f), Dark = new Color(0.12f, 0.1f, 0.08f), Eye = new Color(1f, 0.9f, 0.2f), Legs = 1.2f, Pincers = true, Crown = true };
            default:
                return new Look { Paint = new Color(0.86f, 0.64f, 0.22f), Trim = new Color(0.3f, 0.3f, 0.33f), Dark = dark, Eye = new Color(0.4f, 0.9f, 1f), Wide = 1f };
        }
    }

    // The color its debris flies off in.
    public static Color PaintOf(BotKind kind) {
        return LookOf(kind).Paint;
    }

    // tint is a hue to lean toward; amount 0 keeps the authored colors.
    public static GameObject Make(BotKind kind, Color tint, float amount) {
        Material material = kind == BotKind.TitanSpiderBot ? BirdArt.Glowing : BirdArt.Shared;
        GameObject source = Source(kind);
        GameObject made;
        if (source != null) {
            made = Object.Instantiate(source);
            foreach (Collider collider in made.GetComponentsInChildren<Collider>()) {
                Object.Destroy(collider);
            }
        } else {
            made = new GameObject();
            foreach (PartMesh part in StandIn(kind)) {
                var go = new GameObject(part.Name);
                go.transform.SetParent(made.transform, false);
                go.transform.localPosition = part.Pivot;
                go.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
                go.AddComponent<MeshRenderer>();
            }
        }
        made.name = "Bot";
        foreach (MeshRenderer renderer in made.GetComponentsInChildren<MeshRenderer>()) {
            renderer.sharedMaterial = material;
            renderer.receiveShadows = true;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter != null && amount > 0.001f) {
                filter.sharedMesh = BirdArt.Tinted(filter.sharedMesh, tint, amount);
            }
        }
        return made;
    }

    // Built once per kind, then shared by every bot of that kind so they batch.
    static PartMesh[] StandIn(BotKind kind) {
        PartMesh[] parts;
        if (standIns.TryGetValue(kind, out parts)) {
            return parts;
        }
        var list = new List<PartMesh>();
        Look look = LookOf(kind);
        if (BotRoster.IsSpider(kind)) {
            BuildSpider(list, kind, look);
        } else {
            BuildYoBot(list, kind, look);
        }
        parts = list.ToArray();
        standIns[kind] = parts;
        return parts;
    }

    static void Add(List<PartMesh> list, BotKind kind, string name, Vector3 pivot, BotMesh mesh) {
        list.Add(new PartMesh { Name = name, Pivot = pivot, Mesh = mesh.ToMesh(kind + " " + name) });
    }

    // Two legs, a jet pack and two claws. About 0.9 tall before the roster scales it.
    static void BuildYoBot(List<PartMesh> list, BotKind kind, Look k) {
        float w = k.Wide;

        var body = new BotMesh();
        body.Box(new Vector3(0f, 0.34f, 0f), new Vector3(0.26f * w, 0.08f, 0.18f), k.Dark);
        body.Box(new Vector3(0f, 0.5f, 0f), new Vector3(0.36f * w, 0.26f, 0.26f), k.Paint);
        body.Box(new Vector3(0f, 0.5f, 0.135f), new Vector3(0.24f * w, 0.16f, 0.02f), k.Trim);
        body.Ball(new Vector3(0f, 0.52f, 0.15f), 0.03f, k.Eye);
        body.Box(new Vector3(0f, 0.52f, -0.17f), new Vector3(0.24f * w, 0.22f, 0.1f), k.Trim);
        for (int s = -1; s <= 1; s += 2) {
            body.Rod(new Vector3(s * 0.07f * w, 0.42f, -0.18f), new Vector3(s * 0.07f * w, 0.35f, -0.18f), 0.035f, k.Dark);
        }
        if (k.Plates) {
            for (int s = -1; s <= 1; s += 2) {
                body.Box(new Vector3(s * 0.22f * w, 0.63f, 0f), new Vector3(0.13f, 0.06f, 0.2f), k.Trim, Quaternion.Euler(0f, 0f, -s * 14f));
            }
            body.Box(new Vector3(0f, 0.4f, 0.12f), new Vector3(0.28f * w, 0.07f, 0.04f), k.Trim);
        }
        Add(list, kind, "Body", Vector3.zero, body);

        Vector3 neck = new Vector3(0f, 0.63f, 0.01f);
        var head = new BotMesh();
        head.Rod(Vector3.zero, new Vector3(0f, 0.05f, 0f), 0.045f, k.Dark);
        float tall = k.Horns ? 0.19f : 0.16f;
        head.Box(new Vector3(0f, 0.04f + tall * 0.5f, 0f), new Vector3(0.28f * w, tall, 0.22f), k.Paint);
        head.Box(new Vector3(0f, 0.05f + tall * 0.55f, 0.112f), new Vector3(0.22f * w, 0.05f, 0.012f), k.Eye);
        Vector3 aerial = new Vector3(0.08f * w, 0.04f + tall, -0.03f);
        head.Rod(aerial, aerial + new Vector3(0.02f, 0.1f, 0f), 0.012f, k.Dark);
        head.Ball(aerial + new Vector3(0.02f, 0.11f, 0f), 0.025f, k.Eye);
        if (k.Horns) {
            for (int s = -1; s <= 1; s += 2) {
                Vector3 root = new Vector3(s * 0.13f * w, 0.04f + tall * 0.8f, 0f);
                head.Rod(root, root + new Vector3(s * 0.06f, 0.07f, 0.02f), 0.022f, k.Trim);
            }
        }
        Add(list, kind, "Head", neck, head);

        for (int s = -1; s <= 1; s += 2) {
            Vector3 shoulder = new Vector3(s * 0.21f * w, 0.58f, 0f);
            var claw = new BotMesh();
            Vector3 elbow = new Vector3(s * 0.02f, -0.16f, 0.04f);
            Vector3 wrist = new Vector3(s * 0.02f, -0.23f, 0.15f);
            claw.Ball(Vector3.zero, 0.055f, k.Dark);
            claw.Rod(Vector3.zero, elbow, 0.036f, k.Trim);
            claw.Ball(elbow, 0.04f, k.Dark);
            claw.Rod(elbow, wrist, 0.034f, k.Trim);
            claw.Ball(wrist, 0.045f, k.Paint);
            claw.Rod(wrist, wrist + new Vector3(s * 0.035f, -0.01f, 0.1f), 0.018f, k.Dark);
            claw.Rod(wrist, wrist + new Vector3(-s * 0.025f, -0.01f, 0.1f), 0.018f, k.Dark);
            claw.Rod(wrist, wrist + new Vector3(0f, -0.06f, 0.07f), 0.016f, k.Dark);
            Add(list, kind, s < 0 ? "ClawL" : "ClawR", shoulder, claw);
        }

        for (int s = -1; s <= 1; s += 2) {
            Vector3 hip = new Vector3(s * 0.1f * w, 0.32f, 0f);
            var leg = new BotMesh();
            leg.Box(new Vector3(0f, -0.13f, 0f), new Vector3(0.1f, 0.24f, 0.11f), k.Trim);
            leg.Ball(new Vector3(0f, -0.15f, 0.05f), 0.045f, k.Dark);
            leg.Box(new Vector3(0f, -0.295f, 0.035f), new Vector3(0.14f, 0.055f, 0.22f), k.Dark);
            Add(list, kind, s < 0 ? "LegL" : "LegR", hip, leg);
        }
    }

    // A thorax, a big abdomen with rear jets, a head full of eyes and eight legs. About 0.8 tall.
    static void BuildSpider(List<PartMesh> list, BotKind kind, Look k) {
        float reach = k.Legs;

        var body = new BotMesh();
        body.Box(new Vector3(0f, 0.5f, 0.06f), new Vector3(0.36f, 0.18f, 0.38f), k.Paint);
        body.Box(new Vector3(0f, 0.6f, -0.3f), new Vector3(0.48f, 0.3f, 0.5f), k.Paint, Quaternion.Euler(-10f, 0f, 0f));
        body.Box(new Vector3(0f, 0.765f, -0.3f), new Vector3(0.3f, 0.025f, 0.4f), k.Trim, Quaternion.Euler(-10f, 0f, 0f));
        body.Box(new Vector3(0f, 0.6f, -0.3f), new Vector3(0.5f, 0.06f, 0.3f), k.Trim, Quaternion.Euler(-10f, 0f, 0f));
        for (int s = -1; s <= 1; s += 2) {
            body.Rod(new Vector3(s * 0.1f, 0.52f, -0.5f), new Vector3(s * 0.1f, 0.47f, -0.58f), 0.04f, k.Dark);
            body.Ball(new Vector3(s * 0.18f, 0.5f, 0.06f), 0.06f, k.Dark);
        }
        if (k.Crown) {
            for (int i = 0; i < 5; i++) {
                float z = -0.48f + i * 0.09f;
                body.Rod(new Vector3(0f, 0.77f + i * 0.015f, z), new Vector3(0f, 0.86f + i * 0.015f, z + 0.03f), 0.025f, k.Trim);
            }
        }
        Add(list, kind, "Body", Vector3.zero, body);

        Vector3 neck = new Vector3(0f, 0.5f, 0.25f);
        var head = new BotMesh();
        head.Box(new Vector3(0f, 0.02f, 0.08f), new Vector3(0.26f, 0.14f, 0.16f), k.Paint);
        for (int s = -1; s <= 1; s += 2) {
            head.Ball(new Vector3(s * 0.05f, 0.06f, 0.165f), 0.028f, k.Eye);
            head.Ball(new Vector3(s * 0.1f, 0.035f, 0.15f), 0.022f, k.Eye);
            if (k.Pincers) {
                Vector3 root = new Vector3(s * 0.09f, -0.03f, 0.15f);
                Vector3 bend = root + new Vector3(s * 0.08f, -0.02f, 0.12f);
                head.Rod(root, bend, 0.03f, k.Trim);
                head.Rod(bend, bend + new Vector3(-s * 0.08f, 0f, 0.06f), 0.024f, k.Dark);
            } else {
                head.Rod(new Vector3(s * 0.05f, -0.04f, 0.15f), new Vector3(s * 0.03f, -0.12f, 0.2f), 0.016f, k.Dark);
            }
        }
        if (k.Ram) {
            head.Box(new Vector3(0f, 0.03f, 0.18f), new Vector3(0.42f, 0.24f, 0.04f), k.Trim);
        }
        Add(list, kind, "Head", neck, head);

        float[] hipZ = { 0.2f, 0.1f, 0f, -0.1f };
        float[] splay = { 0.2f, 0.07f, -0.07f, -0.2f };
        for (int pair = 0; pair < 4; pair++) {
            for (int s = -1; s <= 1; s += 2) {
                Vector3 hip = new Vector3(s * 0.18f, 0.5f, hipZ[pair]);
                Vector3 knee = new Vector3(s * 0.28f * reach, 0.26f, splay[pair] * reach);
                Vector3 foot = new Vector3(s * 0.52f * reach, -0.5f, splay[pair] * 1.8f * reach);
                var leg = new BotMesh();
                leg.Rod(Vector3.zero, knee, 0.036f, k.Trim);
                leg.Ball(knee, 0.048f, k.Dark);
                leg.Rod(knee, foot, 0.028f, k.Paint);
                leg.Ball(foot, 0.024f, k.Dark);
                Add(list, kind, (s < 0 ? "LegL" : "LegR") + (pair + 1), hip, leg);
            }
        }
    }
}

// Flat-shaded, vertex-colored low-poly pieces: every triangle has its own vertices.
public sealed class BotMesh {
    readonly List<Vector3> vertices = new List<Vector3>();
    readonly List<Color> colors = new List<Color>();
    readonly List<int> triangles = new List<int>();

    public void Box(Vector3 center, Vector3 size, Color color) {
        Box(center, size, color, Quaternion.identity);
    }

    public void Box(Vector3 center, Vector3 size, Color color, Quaternion rotation) {
        Vector3 h = size * 0.5f;
        var c = new Vector3[8];
        for (int i = 0; i < 8; i++) {
            Vector3 corner = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
            c[i] = center + rotation * corner;
        }
        // Lighter on top, darker underneath, so the boxes read at night.
        Color top = Color.Lerp(color, Color.white, 0.08f);
        Color under = color * 0.72f;
        under.a = 1f;
        Quad(c[4], c[6], c[2], c[0], color);
        Quad(c[3], c[7], c[5], c[1], color);
        Quad(c[1], c[5], c[4], c[0], under);
        Quad(c[6], c[7], c[3], c[2], top);
        Quad(c[2], c[3], c[1], c[0], color);
        Quad(c[5], c[7], c[6], c[4], color);
    }

    // A six-sided rod from a to b with capped ends.
    public void Rod(Vector3 a, Vector3 b, float radius, Color color, int sides = 6) {
        Vector3 axis = b - a;
        if (axis.sqrMagnitude < 1e-8f) {
            return;
        }
        Vector3 dir = axis.normalized;
        Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        Vector3 up = Vector3.Cross(side, dir);
        var ringA = new Vector3[sides];
        var ringB = new Vector3[sides];
        for (int i = 0; i < sides; i++) {
            float t = i / (float)sides * Mathf.PI * 2f;
            Vector3 offset = (side * Mathf.Cos(t) + up * Mathf.Sin(t)) * radius;
            ringA[i] = a + offset;
            ringB[i] = b + offset;
        }
        for (int i = 0; i < sides; i++) {
            int j = (i + 1) % sides;
            Quad(ringA[i], ringB[i], ringB[j], ringA[j], color);
            Triangle(a, ringA[i], ringA[j], color);
            Triangle(b, ringB[j], ringB[i], color);
        }
    }

    // A low-poly ball: eyes, joints, lights.
    public void Ball(Vector3 center, float radius, Color color) {
        const int lat = 4;
        const int lon = 6;
        for (int y = 0; y < lat; y++) {
            float v0 = y / (float)lat * Mathf.PI;
            float v1 = (y + 1) / (float)lat * Mathf.PI;
            for (int x = 0; x < lon; x++) {
                float u0 = x / (float)lon * Mathf.PI * 2f;
                float u1 = (x + 1) / (float)lon * Mathf.PI * 2f;
                Vector3 p00 = center + Point(u0, v0) * radius;
                Vector3 p10 = center + Point(u1, v0) * radius;
                Vector3 p01 = center + Point(u0, v1) * radius;
                Vector3 p11 = center + Point(u1, v1) * radius;
                if (y > 0) {
                    Triangle(p00, p10, p11, color);
                }
                if (y < lat - 1) {
                    Triangle(p00, p11, p01, color);
                }
            }
        }
    }

    static Vector3 Point(float u, float v) {
        return new Vector3(Mathf.Cos(u) * Mathf.Sin(v), Mathf.Cos(v), Mathf.Sin(u) * Mathf.Sin(v));
    }

    void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color) {
        Triangle(a, b, c, color);
        Triangle(a, c, d, color);
    }

    void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color) {
        int index = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        colors.Add(color);
        colors.Add(color);
        colors.Add(color);
        triangles.Add(index);
        triangles.Add(index + 1);
        triangles.Add(index + 2);
    }

    public Mesh ToMesh(string name) {
        var mesh = new Mesh();
        mesh.name = name;
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
