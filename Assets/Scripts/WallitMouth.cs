using UnityEngine;

// Wallit's face is painted on his wallet body (one skinned mesh, no jaw bone, no blendshapes), so his mouth is a small
// dark opening built in code and parked on the painted grin: where the grin's UV lies on the mesh, riding the bone
// that skins it. WallitVoice.MouthOpen (the loudness of his voice line) opens it and shuts it again; shut, it is hidden
// and the painted grin shows. SetupWallit adds this to the prefab; CharacterMovement.EnsureWallitOn adds it to a live
// Wallit that already exists (it returns early then), so both paths end up with a mouth.
public class WallitMouth : MonoBehaviour {
    // Where the painted grin sits on WallitSkin.png (UV), measured on the kept body shell, and a fallback in mesh space.
    static readonly Vector2 GrinUv = new Vector2(0.158f, 0.185f);
    static readonly Vector3 FallbackAt = new Vector3(0.035f, 0.927f, 0.051f);
    const float Width = 0.15f;
    const float Height = 0.085f;
    const float Lift = 0.02f;

    public static int Mouths { get; private set; }
    // Playtest: how it moved. Open frames while silent must stay 0.
    public static float MaxOpen;
    public static int OpenWhileSilent;
    public static int OpenWhileSpeaking;
    public static int FramesSpeaking;
    public static int Cycles;

    Transform mouth;
    MeshRenderer view;
    bool built;
    int tries;
    bool wasOpen;

    public Transform Mouth { get { return mouth; } }
    public float Open { get; private set; }

    public static void Attach(GameObject wallit) {
        if (wallit == null) {
            return;
        }
        if (wallit.GetComponent<WallitMouth>() == null) {
            wallit.AddComponent<WallitMouth>();
        }
        Transform root = wallit.transform.parent;
        if (root != null) {
            WallitVoice.Ensure(root.gameObject);
        }
    }

    void LateUpdate() {
        if (!built) {
            if (tries++ < 30) {
                Build();
            }
            return;
        }
        float v = Mathf.Clamp01(WallitVoice.MouthOpen);
        Open = v;
        if (mouth == null) {
            return;
        }
        bool visible = v > 0.04f;
        if (view.enabled != visible) {
            view.enabled = visible;
        }
        if (visible) {
            mouth.localScale = new Vector3(Mathf.Lerp(0.8f, 1f, v), Mathf.Max(0.08f, v), 1f);
        }
        MaxOpen = Mathf.Max(MaxOpen, v);
        bool open = v > 0.2f;
        if (open && !wasOpen) {
            Cycles++;
        }
        wasOpen = open;
        if (WallitVoice.Speaking) {
            FramesSpeaking++;
            if (open) {
                OpenWhileSpeaking++;
            }
        } else if (open) {
            OpenWhileSilent++;
        }
    }

    void Build() {
        SkinnedMeshRenderer body = null;
        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
            if (smr.sharedMesh != null && !smr.gameObject.name.ToLowerInvariant().Contains("bat")) {
                if (body == null || smr.sharedMesh.vertexCount > body.sharedMesh.vertexCount) {
                    body = smr;
                }
            }
        }
        if (body == null || body.bones == null || body.bones.Length == 0) {
            return;
        }
        Mesh mesh = body.sharedMesh;
        Matrix4x4[] bind = mesh.bindposes;
        Vector3 at = FallbackAt;
        int bone = -1;
        if (mesh.isReadable) {
            Vector3[] verts = mesh.vertices;
            Vector2[] uv = mesh.uv;
            BoneWeight[] weights = mesh.boneWeights;
            int[] tris = mesh.GetTriangles(0);
            for (int t = 0; t + 2 < tris.Length; t += 3) {
                int a = tris[t];
                int b = tris[t + 1];
                int c = tris[t + 2];
                Vector3 bc;
                if (!Barycentric(GrinUv, uv[a], uv[b], uv[c], out bc)) {
                    continue;
                }
                at = verts[a] * bc.x + verts[b] * bc.y + verts[c] * bc.z;
                if (weights != null && weights.Length == verts.Length) {
                    bone = Dominant(weights[a], weights[b], weights[c]);
                }
                break;
            }
        }
        if (bone < 0 || bone >= body.bones.Length || body.bones[bone] == null || bone >= bind.Length) {
            bone = FindBone(body, "Spine2");
        }
        if (bone < 0) {
            return;
        }

        Transform parent = body.bones[bone];
        Matrix4x4 toBone = bind[bone];
        // The painted face looks along the model's +Z; the opening stands a little off the wallet's skin.
        Vector3 local = toBone.MultiplyPoint3x4(at + Vector3.forward * Lift);
        Vector3 forward = toBone.MultiplyVector(Vector3.forward).normalized;
        Vector3 up = toBone.MultiplyVector(Vector3.up).normalized;
        var go = new GameObject("WallitMouth");
        go.layer = body.gameObject.layer;
        mouth = go.transform;
        mouth.SetParent(parent, false);
        mouth.localPosition = local;
        mouth.localRotation = Quaternion.LookRotation(forward, up);
        mouth.localScale = Vector3.one;
        // Bone space can be scaled against mesh space: size the opening in the bone's own units.
        float unit = Mathf.Max(0.0001f, toBone.MultiplyVector(Vector3.right).magnitude);
        view = go.AddComponent<MeshRenderer>();
        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = BuildMesh(Width * unit, Height * unit);
        Material mat = new Material(BirdArt.Shared);
        mat.name = "WallitMouth";
        if (mat.HasProperty("_RimStrength")) {
            mat.SetFloat("_RimStrength", 0f);
        }
        view.sharedMaterial = mat;
        view.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        view.receiveShadows = false;
        view.enabled = false;
        built = true;
        Mouths++;
    }

    static int FindBone(SkinnedMeshRenderer body, string part) {
        for (int i = 0; i < body.bones.Length; i++) {
            if (body.bones[i] != null && body.bones[i].name.EndsWith(part)) {
                return i;
            }
        }
        return body.bones.Length > 0 && body.bones[0] != null ? 0 : -1;
    }

    static int Dominant(BoneWeight a, BoneWeight b, BoneWeight c) {
        var sum = new System.Collections.Generic.Dictionary<int, float>();
        Add(sum, a);
        Add(sum, b);
        Add(sum, c);
        int best = -1;
        float most = -1f;
        foreach (var kv in sum) {
            if (kv.Value > most) {
                most = kv.Value;
                best = kv.Key;
            }
        }
        return best;
    }

    static void Add(System.Collections.Generic.Dictionary<int, float> d, BoneWeight w) {
        Put(d, w.boneIndex0, w.weight0);
        Put(d, w.boneIndex1, w.weight1);
        Put(d, w.boneIndex2, w.weight2);
        Put(d, w.boneIndex3, w.weight3);
    }

    static void Put(System.Collections.Generic.Dictionary<int, float> d, int bone, float weight) {
        if (weight <= 0f) {
            return;
        }
        float old;
        d.TryGetValue(bone, out old);
        d[bone] = old + weight;
    }

    static bool Barycentric(Vector2 p, Vector2 a, Vector2 b, Vector2 c, out Vector3 bc) {
        float d = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
        bc = Vector3.zero;
        if (Mathf.Abs(d) < 1e-12f) {
            return false;
        }
        float l1 = ((b.y - c.y) * (p.x - c.x) + (c.x - b.x) * (p.y - c.y)) / d;
        float l2 = ((c.y - a.y) * (p.x - c.x) + (a.x - c.x) * (p.y - c.y)) / d;
        float l3 = 1f - l1 - l2;
        bc = new Vector3(l1, l2, l3);
        return l1 >= -1e-4f && l2 >= -1e-4f && l3 >= -1e-4f;
    }

    // An open mouth in vertex colors, two-sided: a dark oval, a pale upper row of teeth, a red tongue low in it.
    // Its height is 1 at full open (the transform's Y scale shuts it).
    static Mesh BuildMesh(float width, float height) {
        const int seg = 20;
        var verts = new System.Collections.Generic.List<Vector3>();
        var cols = new System.Collections.Generic.List<Color32>();
        var tris = new System.Collections.Generic.List<int>();
        Color32 dark = new Color32(26, 6, 8, 255);
        Color32 teeth = new Color32(240, 232, 214, 255);
        Color32 tongue = new Color32(176, 52, 58, 255);
        AddFan(verts, cols, tris, Vector3.zero, width * 0.5f, height * 0.5f, seg, dark, 0f);
        AddFan(verts, cols, tris, new Vector3(0f, -height * 0.2f, -0.001f), width * 0.3f, height * 0.2f, seg, tongue, 0f);
        // Teeth: a flat strip hugging the top edge.
        int v0 = verts.Count;
        float tw = width * 0.38f;
        float top = height * 0.5f * 0.92f;
        verts.Add(new Vector3(-tw, top, -0.0015f));
        verts.Add(new Vector3(tw, top, -0.0015f));
        verts.Add(new Vector3(tw * 0.9f, top - height * 0.17f, -0.0015f));
        verts.Add(new Vector3(-tw * 0.9f, top - height * 0.17f, -0.0015f));
        for (int i = 0; i < 4; i++) {
            cols.Add(teeth);
        }
        tris.AddRange(new[] { v0, v0 + 1, v0 + 2, v0, v0 + 2, v0 + 3, v0, v0 + 2, v0 + 1, v0, v0 + 3, v0 + 2 });
        var mesh = new Mesh();
        mesh.name = "WallitMouth";
        mesh.SetVertices(verts);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddFan(System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<Color32> cols, System.Collections.Generic.List<int> tris,
        Vector3 center, float rx, float ry, int seg, Color32 color, float z) {
        int start = verts.Count;
        verts.Add(center + new Vector3(0f, 0f, z));
        cols.Add(color);
        for (int i = 0; i < seg; i++) {
            float a = i * Mathf.PI * 2f / seg;
            verts.Add(center + new Vector3(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry, z));
            cols.Add(color);
        }
        for (int i = 0; i < seg; i++) {
            int a = start + 1 + i;
            int b = start + 1 + (i + 1) % seg;
            tris.AddRange(new[] { start, a, b, start, b, a });
        }
    }
}
