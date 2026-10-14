using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// The Moonlit Hollow: an open meadow bowl around the piano, built for a wave game.
// Birds walk straight lines at the stage, so the playfield (out past the spawn ring at 150 m)
// has no obstacles at all; everything solid-looking lives on the rising rim as backdrop.
// Runs once when scene2 opens without an "Arena", or from Tools > Arena > Rebuild Arena.
[InitializeOnLoad]
static class BuildArena
{
    public const string RootName = "Arena";
    const string ArtFolder = "Assets/Art/Arena";
    const string GeneratedFolder = "Assets/Art/Arena/Generated";
    const string MaterialPath = "Assets/Art/Knoll/KnollLit.mat";

    const float Flat = 14f;        // plaza and build ring stay level
    const float FieldEnd = 150f;   // spawns reach out to here
    const float RimStart = 145f;
    const float Outer = 280f;
    const float WallRadius = 205f; // up in the rim forest, well before the slope gets too steep to walk
    const int Rings = 110;
    const int Segments = 160;

    static int _attempts;

    static BuildArena()
    {
        EditorApplication.delayCall += TryBuild;
    }

    [MenuItem("Tools/Arena/Rebuild Arena")]
    static void RebuildFromMenu()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
            Object.DestroyImmediate(existing);
        Build();
    }

    static void TryBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            if (_attempts++ < 40)
                EditorApplication.delayCall += TryBuild;
            return;
        }
        if (EditorSceneManager.GetActiveScene().name != "scene2" || GameObject.Find(RootName) != null)
            return;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ArtFolder + "/Pine_A.fbx") == null)
        {
            if (_attempts++ < 40)
                EditorApplication.delayCall += TryBuild;
            return;
        }
        Build();
    }

    static void Build()
    {
        GameObject piano = GameObject.Find("PianoRecital");
        Vector3 center = piano != null ? piano.transform.position : Vector3.zero;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Debug.LogWarning("Arena: KnollLit material is missing.");
            return;
        }

        ClearOldMap();
        Directory.CreateDirectory(GeneratedFolder);
        var root = new GameObject(RootName);
        root.isStatic = true;

        var noise = new System.Random(20261014);
        float seedX = (float)noise.NextDouble() * 1000f;
        float seedZ = (float)noise.NextDouble() * 1000f;

        // Ground
        Mesh ground = GroundMesh(center, seedX, seedZ);
        SaveMesh(ground, "ArenaGround");
        var groundGo = new GameObject("Ground");
        groundGo.transform.SetParent(root.transform, false);
        groundGo.layer = 3;
        groundGo.isStatic = true;
        groundGo.AddComponent<MeshFilter>().sharedMesh = ground;
        var groundRenderer = groundGo.AddComponent<MeshRenderer>();
        groundRenderer.sharedMaterial = material;
        groundRenderer.shadowCastingMode = ShadowCastingMode.Off;
        groundGo.AddComponent<MeshCollider>().sharedMesh = ground;

        // Invisible wall up on the rim so nobody walks out of the bowl.
        Mesh wall = WallMesh(center);
        SaveMesh(wall, "ArenaWall");
        var wallGo = new GameObject("Boundary");
        wallGo.transform.SetParent(root.transform, false);
        wallGo.layer = 2; // Ignore Raycast: ground probes and aim rays pass through it
        wallGo.isStatic = true;
        wallGo.AddComponent<MeshCollider>().sharedMesh = wall;

        // Stone plaza under the piano
        Mesh plaza = PlazaMesh(center, noise);
        SaveMesh(plaza, "ArenaPlaza");
        var plazaGo = new GameObject("Plaza");
        plazaGo.transform.SetParent(root.transform, false);
        plazaGo.isStatic = true;
        plazaGo.AddComponent<MeshFilter>().sharedMesh = plaza;
        plazaGo.AddComponent<MeshRenderer>().sharedMaterial = material;

        // Scatter
        var buckets = new Dictionary<int, List<CombineInstance>>();
        Scatter(buckets, center, noise, seedX, seedZ);
        foreach (KeyValuePair<int, List<CombineInstance>> pair in buckets)
        {
            var combined = new Mesh();
            combined.name = "ArenaScatter_" + pair.Key;
            combined.indexFormat = IndexFormat.UInt32;
            combined.CombineMeshes(pair.Value.ToArray(), true, true);
            combined.RecalculateBounds();
            SaveMesh(combined, combined.name);
            var go = new GameObject(combined.name);
            go.transform.SetParent(root.transform, false);
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = combined;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            // Grass and flowers do not cast shadows; trees and rocks do.
            renderer.shadowCastingMode = pair.Key >= 100 ? ShadowCastingMode.Off : ShadowCastingMode.On;
        }

        AssetDatabase.SaveAssets();
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Arena built: open meadow bowl, stone plaza, rim forest. Houses and the old knoll are gone.");
    }

    static void ClearOldMap()
    {
        var doomed = new List<GameObject>();
        foreach (GameObject go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            string n = go.name;
            if (n == "Knoll" || n == "House" || n == "Tree" || n == "KnollDressing" || n == "PracticeTargets" || n == RootName)
                doomed.Add(go);
        }
        foreach (GameObject go in doomed)
            Object.DestroyImmediate(go);
    }

    // Height above the plaza at a world point.
    public static float Height(float x, float z, Vector3 center, float seedX, float seedZ)
    {
        float dx = x - center.x;
        float dz = z - center.z;
        float r = Mathf.Sqrt(dx * dx + dz * dz);
        float n = Mathf.PerlinNoise(x * 0.025f + seedX, z * 0.025f + seedZ) - 0.5f;
        float n2 = Mathf.PerlinNoise(x * 0.08f + seedZ, z * 0.08f + seedX) - 0.5f;
        float field = Smooth01((r - Flat) / 30f) * (n * 1.8f + n2 * 0.35f + (r - Flat) * 0.004f);
        float rim = r > RimStart ? Mathf.Pow((r - RimStart) / 80f, 2f) * 34f : 0f;
        float rimNoise = r > RimStart ? n * 6f * Smooth01((r - RimStart) / 40f) : 0f;
        return field + rim + rimNoise;
    }

    static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    static Mesh GroundMesh(Vector3 center, float seedX, float seedZ)
    {
        var verts = new List<Vector3>();
        var colors = new List<Color>();
        var tris = new List<int>();
        verts.Add(center + Vector3.up * -0.02f);
        colors.Add(new Color(0.16f, 0.15f, 0.13f));
        for (int ring = 1; ring <= Rings; ring++)
        {
            float t = ring / (float)Rings;
            float r = Mathf.Lerp(0f, Outer, t * t * 0.55f + t * 0.45f);
            for (int s = 0; s < Segments; s++)
            {
                float a = s / (float)Segments * Mathf.PI * 2f;
                float x = center.x + Mathf.Sin(a) * r;
                float z = center.z + Mathf.Cos(a) * r;
                float y = center.y - 0.02f + Height(x, z, center, seedX, seedZ);
                verts.Add(new Vector3(x, y, z));
                colors.Add(GroundColor(x, z, r, a, y - center.y, seedX, seedZ));
            }
        }
        for (int s = 0; s < Segments; s++)
        {
            tris.Add(0);
            tris.Add(1 + s);
            tris.Add(1 + (s + 1) % Segments);
        }
        for (int ring = 1; ring < Rings; ring++)
        {
            int a0 = 1 + (ring - 1) * Segments;
            int b0 = 1 + ring * Segments;
            for (int s = 0; s < Segments; s++)
            {
                int s1 = (s + 1) % Segments;
                tris.Add(a0 + s); tris.Add(b0 + s); tris.Add(b0 + s1);
                tris.Add(a0 + s); tris.Add(b0 + s1); tris.Add(a0 + s1);
            }
        }
        var mesh = new Mesh();
        mesh.name = "ArenaGround";
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // A tall open cylinder, faces pointing inward, from well below the rim to far above it.
    static Mesh WallMesh(Vector3 center)
    {
        const int segments = 96;
        var verts = new List<Vector3>();
        var tris = new List<int>();
        float bottom = center.y - 20f;
        float top = center.y + 160f;
        for (int s = 0; s < segments; s++)
        {
            float a = s / (float)segments * Mathf.PI * 2f;
            Vector3 p = center + new Vector3(Mathf.Sin(a) * WallRadius, 0f, Mathf.Cos(a) * WallRadius);
            verts.Add(new Vector3(p.x, bottom, p.z));
            verts.Add(new Vector3(p.x, top, p.z));
        }
        for (int s = 0; s < segments; s++)
        {
            int a0 = s * 2;
            int b0 = ((s + 1) % segments) * 2;
            tris.Add(a0); tris.Add(a0 + 1); tris.Add(b0 + 1);
            tris.Add(a0); tris.Add(b0 + 1); tris.Add(b0);
        }
        var mesh = new Mesh();
        mesh.name = "ArenaWall";
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Color GroundColor(float x, float z, float r, float angle, float height, float seedX, float seedZ)
    {
        float n = Mathf.PerlinNoise(x * 0.05f + seedZ, z * 0.05f + seedX);
        float patch = Mathf.PerlinNoise(x * 0.012f + seedX, z * 0.012f + seedZ);
        Color grass = Color.Lerp(new Color(0.11f, 0.24f, 0.1f), new Color(0.18f, 0.33f, 0.13f), n);
        grass = Color.Lerp(grass, new Color(0.14f, 0.27f, 0.17f), Mathf.Clamp01((patch - 0.55f) * 3f));
        // Four dirt lanes run in from the rim, so the flock's approach reads at a glance.
        float lane = 99f;
        for (int i = 0; i < 4; i++)
        {
            float laneAngle = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
            float wobble = (Mathf.PerlinNoise(r * 0.04f + i * 10f, seedX) - 0.5f) * 8f;
            float diff = Mathf.DeltaAngle(angle * Mathf.Rad2Deg, laneAngle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            lane = Mathf.Min(lane, Mathf.Abs(Mathf.Sin(diff) * r + wobble) + (Mathf.Abs(diff) > Mathf.PI * 0.5f ? 999f : 0f));
        }
        float laneWidth = Mathf.Lerp(1.4f, 3.2f, Mathf.Clamp01(r / FieldEnd));
        float dirt = Mathf.Clamp01((laneWidth - lane) / 1.2f) * Mathf.Clamp01((r - 6f) / 4f) * Mathf.Clamp01((FieldEnd + 10f - r) / 20f);
        Color soil = Color.Lerp(new Color(0.27f, 0.22f, 0.15f), new Color(0.35f, 0.28f, 0.19f), n);
        Color c = Color.Lerp(grass, soil, dirt * 0.85f);
        // Worn ring around the plaza.
        float worn = Mathf.Clamp01(1f - Mathf.Abs(r - 5.2f) / 1.4f);
        c = Color.Lerp(c, new Color(0.17f, 0.15f, 0.11f), worn * 0.7f);
        // The rim: darker, cooler grass, with rock showing where it is steep.
        if (r > RimStart)
        {
            float up = Mathf.Clamp01((r - RimStart) / 70f);
            c = Color.Lerp(c, new Color(0.08f, 0.17f, 0.11f), up * 0.7f);
            float rocky = Mathf.Clamp01((height - 14f) / 20f) * Mathf.PerlinNoise(x * 0.09f, z * 0.09f);
            c = Color.Lerp(c, new Color(0.22f, 0.23f, 0.26f), rocky);
        }
        return c;
    }

    // Concentric flagstones, slightly raised, with a medallion in the middle.
    static Mesh PlazaMesh(Vector3 center, System.Random rng)
    {
        var verts = new List<Vector3>();
        var colors = new List<Color>();
        var tris = new List<int>();
        float y = center.y + 0.015f;
        Disc(verts, colors, tris, center, y + 0.006f, 0.95f, 24, new Color(0.36f, 0.34f, 0.32f));
        Disc(verts, colors, tris, center, y + 0.008f, 0.55f, 24, new Color(0.3f, 0.27f, 0.24f));
        float inner = 1.05f;
        int ring = 0;
        while (inner < 4.5f)
        {
            float outer = inner + 0.75f;
            int stones = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * (inner + outer) * 0.5f / 0.85f));
            float offset = ring % 2 == 0 ? 0f : 0.5f;
            for (int s = 0; s < stones; s++)
            {
                float a0 = (s + offset + 0.04f) / stones * Mathf.PI * 2f;
                float a1 = (s + offset + 0.96f) / stones * Mathf.PI * 2f;
                float r0 = inner + 0.04f;
                float r1 = outer - 0.04f;
                float shade = 0.27f + (float)rng.NextDouble() * 0.1f;
                Color stone = new Color(shade, shade * 0.97f, shade * 0.94f);
                int b = verts.Count;
                int steps = 3;
                for (int k = 0; k <= steps; k++)
                {
                    float a = Mathf.Lerp(a0, a1, k / (float)steps);
                    verts.Add(center + new Vector3(Mathf.Sin(a) * r0, y - center.y, Mathf.Cos(a) * r0));
                    verts.Add(center + new Vector3(Mathf.Sin(a) * r1, y - center.y, Mathf.Cos(a) * r1));
                    colors.Add(stone * 0.92f);
                    colors.Add(stone);
                }
                for (int k = 0; k < steps; k++)
                {
                    int i0 = b + k * 2;
                    tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 3);
                    tris.Add(i0); tris.Add(i0 + 3); tris.Add(i0 + 2);
                }
            }
            inner = outer;
            ring++;
        }
        var mesh = new Mesh();
        mesh.name = "ArenaPlaza";
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void Disc(List<Vector3> verts, List<Color> colors, List<int> tris, Vector3 center, float y, float radius, int segments, Color color)
    {
        int c = verts.Count;
        verts.Add(new Vector3(center.x, y, center.z));
        colors.Add(color);
        for (int s = 0; s < segments; s++)
        {
            float a = s / (float)segments * Mathf.PI * 2f;
            verts.Add(new Vector3(center.x + Mathf.Sin(a) * radius, y, center.z + Mathf.Cos(a) * radius));
            colors.Add(color * 0.92f);
        }
        for (int s = 0; s < segments; s++)
        {
            tris.Add(c);
            tris.Add(c + 1 + s);
            tris.Add(c + 1 + (s + 1) % segments);
        }
    }

    static Mesh Load(string name)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ArtFolder + "/" + name + ".fbx");
        if (model == null)
            return null;
        MeshFilter filter = model.GetComponentInChildren<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
    }

    static void Scatter(Dictionary<int, List<CombineInstance>> buckets, Vector3 center, System.Random rng, float seedX, float seedZ)
    {
        Mesh[] pines = { Load("Pine_A"), Load("Pine_B"), Load("Pine_C") };
        Mesh broadleaf = Load("Broadleaf_A");
        Mesh[] rocks = { Load("Rock_A"), Load("Rock_B"), Load("Rock_C") };
        Mesh boulder = Load("Boulder");
        Mesh bush = Load("Bush_A");
        Mesh[] grass = { Load("Grass_A"), Load("Grass_B") };
        Mesh[] flowers = { Load("Flowers_White"), Load("Flowers_Blue"), Load("Flowers_Gold") };

        // Rim forest: thick enough to read as a wall of trees from the field.
        for (int i = 0; i < 900; i++)
        {
            float r = Mathf.Lerp(158f, 262f, Mathf.Pow((float)rng.NextDouble(), 0.8f));
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            Mesh mesh = rng.NextDouble() < 0.12 ? broadleaf : pines[rng.Next(pines.Length)];
            Add(buckets, 0, mesh, center, r, a, 0.85f + (float)rng.NextDouble() * 0.8f, rng, seedX, seedZ, -0.3f);
        }
        // Boulders and rocks where the meadow turns into the rim.
        for (int i = 0; i < 90; i++)
        {
            float r = Mathf.Lerp(150f, 215f, (float)rng.NextDouble());
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            Mesh mesh = rng.NextDouble() < 0.35 ? boulder : rocks[rng.Next(rocks.Length)];
            Add(buckets, 0, mesh, center, r, a, 0.8f + (float)rng.NextDouble() * 1.2f, rng, seedX, seedZ, -0.6f);
        }
        for (int i = 0; i < 70; i++)
        {
            float r = Mathf.Lerp(146f, 175f, (float)rng.NextDouble());
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            Add(buckets, 0, bush, center, r, a, 1f + (float)rng.NextDouble() * 1.4f, rng, seedX, seedZ, -0.1f);
        }
        // The field: only soft things birds can walk through.
        for (int i = 0; i < 2600; i++)
        {
            float r = Mathf.Lerp(7f, 165f, Mathf.Sqrt((float)rng.NextDouble()));
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            Add(buckets, 100, grass[rng.Next(grass.Length)], center, r, a, 0.8f + (float)rng.NextDouble() * 0.9f, rng, seedX, seedZ, 0f);
        }
        for (int i = 0; i < 650; i++)
        {
            // Flowers gather in drifts.
            float drift = (float)rng.Next(24);
            float a = drift / 24f * Mathf.PI * 2f + ((float)rng.NextDouble() - 0.5f) * 0.18f;
            float r = Mathf.Lerp(18f, 140f, Mathf.PerlinNoise(drift * 3.1f, 0.5f)) + ((float)rng.NextDouble() - 0.5f) * 14f;
            Add(buckets, 100, flowers[(int)drift % flowers.Length], center, r, a, 0.9f + (float)rng.NextDouble() * 0.6f, rng, seedX, seedZ, 0f);
        }
    }

    static void Add(Dictionary<int, List<CombineInstance>> buckets, int layerBase, Mesh mesh, Vector3 center, float r, float a, float scale, System.Random rng, float seedX, float seedZ, float sink)
    {
        if (mesh == null)
            return;
        float x = center.x + Mathf.Sin(a) * r;
        float z = center.z + Mathf.Cos(a) * r;
        float y = center.y + Height(x, z, center, seedX, seedZ) + sink * scale;
        Quaternion spin = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
        int sector = ((Mathf.FloorToInt((a / (Mathf.PI * 2f)) * 12f) % 12) + 12) % 12;
        int key = layerBase + sector;
        List<CombineInstance> list;
        if (!buckets.TryGetValue(key, out list))
        {
            list = new List<CombineInstance>();
            buckets[key] = list;
        }
        list.Add(new CombineInstance
        {
            mesh = mesh,
            transform = Matrix4x4.TRS(new Vector3(x, y, z), spin, Vector3.one * scale)
        });
    }

    static void SaveMesh(Mesh mesh, string name)
    {
        string path = GeneratedFolder + "/" + name + ".asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
            AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
    }
}
