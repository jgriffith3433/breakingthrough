using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Turns the scene2 greybox into a night grassy knoll the first time that scene is open.
// Existing components and control scripts stay; only meshes, materials, and placement change.
[InitializeOnLoad]
static class DressGrassyKnoll
{
    const string MarkerName = "KnollDressing";
    const string ArtFolder = "Assets/Art/Knoll";
    const float GroundPadding = 0.08f;

    static int _attempts;

    static DressGrassyKnoll()
    {
        EditorApplication.delayCall += TryDress;
    }

    [MenuItem("Tools/Knoll/Dress Scene")]
    static void DressFromMenu()
    {
        RemoveIfPresent(MarkerName);
        RemoveIfPresent("ElfArcher");
        Dress();
    }

    static void TryDress()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryDress;
            return;
        }

        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "scene2")
        {
            if (_attempts++ < 30)
                EditorApplication.delayCall += TryDress;
            return;
        }

        if (GameObject.Find(MarkerName) != null)
            return;

        Dress();
    }

    static void Dress()
    {
        if (Shader.Find("Knoll/VertexColorLit") == null || Shader.Find("Knoll/NightSky") == null)
        {
            if (_attempts++ < 40)
                EditorApplication.delayCall += TryDress;
            return;
        }

        EnsureFolder();
        Material lit = SaveMaterial("KnollLit.mat", "Knoll/VertexColorLit");
        Material sky = SaveMaterial("NightSky.mat", "Knoll/NightSky");

        Mesh knoll = SaveMesh("Knoll", BuildKnoll());
        Mesh oak = SaveMesh("Oak", CenterPivot(BuildOak()));
        Mesh pine = SaveMesh("Pine", CenterPivot(BuildPine()));
        Mesh tuft = SaveMesh("GrassTuft", BuildTuft());
        Mesh rock = SaveMesh("Rock", CenterPivot(BuildRock()));

        GameObject floor = FindOne("Floor");
        if (floor != null)
        {
            floor.name = "Knoll";
            floor.transform.position = Vector3.zero;
            floor.transform.rotation = Quaternion.identity;
            floor.transform.localScale = Vector3.one;
            AssignMesh(floor, knoll, lit);
            MeshCollider ground = floor.GetComponent<MeshCollider>();
            if (ground != null)
            {
                ground.sharedMesh = null;
                ground.convex = false;
                ground.sharedMesh = knoll;
            }
        }

        Mesh[] trees = { oak, pine, oak, pine };
        string[] treeNames = { "Sphere", "Cylinder", "Red Cube", "Cube (3)" };
        for (int i = 0; i < treeNames.Length; i++)
        {
            GameObject target = FindOne(treeNames[i]);
            if (target == null)
                continue;

            bool keepLockTag = target.CompareTag("enemy") || target.CompareTag("CanLockOn");
            target.name = "Tree";
            if (!keepLockTag)
                target.tag = "CanLockOn";

            Rigidbody body = target.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
            }

            target.transform.localScale = Vector3.one;
            target.transform.rotation = Quaternion.Euler(0f, 20f * i, 0f);
            AssignMesh(target, trees[i], lit);
            FitCollider(target, trees[i]);
            Seat(target, trees[i]);
        }

        GameObject nudged = FindTreeNear(new Vector3(0f, 0f, 1f), 1.5f);
        if (nudged != null)
        {
            Vector3 aside = new Vector3(2.6f, 0f, 3.1f);
            nudged.transform.position = new Vector3(aside.x, 0f, aside.z);
            Seat(nudged, nudged.GetComponent<MeshFilter>().sharedMesh);
        }

        foreach (GameObject wall in FindAll("Wall"))
        {
            wall.name = "House";
            wall.transform.localScale = Vector3.one;
            Vector3 flat = wall.transform.position;
            flat.y = 0f;
            wall.transform.position = flat;
            Vector3 face = Vector3.zero - flat;
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f)
                wall.transform.rotation = Quaternion.LookRotation(face.normalized, Vector3.up);
        }

        RefreshHouseMeshes();

        GameObject dressing = new GameObject(MarkerName);
        System.Random random = new System.Random(7);
        Vector2[] extraTrees =
        {
            new Vector2(9f, 7f), new Vector2(-11f, 5f), new Vector2(12f, -8f),
            new Vector2(-8f, -12f), new Vector2(4f, 14f), new Vector2(-15f, -2f),
            new Vector2(16f, 2f), new Vector2(-3f, -16f), new Vector2(7f, -15f),
            new Vector2(-13f, 12f)
        };
        for (int i = 0; i < extraTrees.Length; i++)
        {
            Mesh mesh = i % 2 == 0 ? pine : oak;
            GameObject tree = CreateProp("Tree", mesh, lit, dressing.transform);
            float yaw = (float)random.NextDouble() * 360f;
            float scale = 0.75f + (float)random.NextDouble() * 0.7f;
            tree.transform.localScale = Vector3.one * scale;
            tree.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            tree.transform.position = new Vector3(extraTrees[i].x, 0f, extraTrees[i].y);
            Seat(tree, mesh);
            tree.tag = "CanLockOn";
            if (tree.GetComponent<Collider>() == null)
                tree.AddComponent<CapsuleCollider>();
            FitCollider(tree, mesh);
        }

        for (int i = 0; i < 46; i++)
        {
            float x = Lerp(-28f, 28f, (float)random.NextDouble());
            float z = Lerp(-28f, 28f, (float)random.NextDouble());
            if (x * x + z * z < 6f)
                continue;
            GameObject clump = CreateProp("Grass", tuft, lit, dressing.transform);
            clump.transform.position = new Vector3(x, 0f, z);
            clump.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            clump.transform.localScale = Vector3.one * (0.7f + (float)random.NextDouble() * 0.8f);
            Seat(clump, tuft);
        }

        for (int i = 0; i < 7; i++)
        {
            float x = Lerp(-22f, 22f, (float)random.NextDouble());
            float z = Lerp(-22f, 22f, (float)random.NextDouble());
            GameObject stone = CreateProp("Rock", rock, lit, dressing.transform);
            stone.transform.position = new Vector3(x, 0f, z);
            stone.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            stone.transform.localScale = new Vector3(1f, 0.6f, 1f) * (0.5f + (float)random.NextDouble());
            Seat(stone, rock);
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Vector3 position = player.transform.position;
            position.y = Hill(position.x, position.z) - GroundPadding;
            player.transform.position = position;
        }

        ApplyNight(sky);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        SceneView.RepaintAll();
        Debug.Log("Scene2 is dressed as a night grassy knoll. Controls and scripts are unchanged.");
    }

    static void ApplyNight(Material sky)
    {
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.32f, 0.36f, 0.48f);
        RenderSettings.ambientEquatorColor = new Color(0.28f, 0.3f, 0.34f);
        RenderSettings.ambientGroundColor = new Color(0.16f, 0.16f, 0.14f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.22f, 0.26f, 0.34f);
        RenderSettings.fogDensity = 0.0035f;
        DynamicGI.UpdateEnvironment();

        Light moon = Object.FindFirstObjectByType<Light>();
        if (moon != null)
        {
            Vector3 moonDirection = new Vector3(0.45f, 0.62f, 0.55f).normalized;
            moon.transform.rotation = Quaternion.LookRotation(-moonDirection, Vector3.up);
            moon.color = new Color(0.82f, 0.88f, 1f);
            moon.intensity = 1.55f;
            moon.shadows = LightShadows.Soft;
        }

        Camera camera = Camera.main;
        if (camera != null)
            camera.clearFlags = CameraClearFlags.Skybox;
    }

    public static float TerrainHeight(float x, float z)
    {
        return Hill(x, z);
    }

    public static void RefreshHouseMeshes()
    {
        EnsureFolder();
        Material lit = SaveMaterial("KnollLit.mat", "Knoll/VertexColorLit");
        List<GameObject> houses = FindAll("House");
        for (int i = 0; i < houses.Count; i++)
        {
            string assetName = i == 0 ? "House" : "House" + i;
            Mesh mesh = SaveMesh(assetName, BuildHouse(i));
            houses[i].transform.localScale = Vector3.one;
            AssignMesh(houses[i], mesh, lit);
            FitCollider(houses[i], mesh);
            Seat(houses[i], mesh);
        }
    }

    static float Hill(float x, float z)
    {
        float noise = Mathf.PerlinNoise(x * 0.045f + 2.2f, z * 0.045f + 8.1f);
        float detail = Mathf.PerlinNoise(x * 0.11f + 5f, z * 0.11f + 1.3f);
        float crown = Mathf.Exp(-(x * x + z * z) / 980f);
        return 0.05f + noise * 0.85f + detail * 0.22f + crown * 1.35f;
    }

    static void Seat(GameObject go, Mesh mesh)
    {
        if (go == null || mesh == null)
            return;
        Vector3 position = go.transform.position;
        float lift = -mesh.bounds.min.y * go.transform.localScale.y;
        position.y = Hill(position.x, position.z) + lift;
        go.transform.position = position;
    }

    static Mesh BuildKnoll()
    {
        const int n = 56;
        const float extent = 38f;
        var builder = new MeshBuilder();
        Color[] samples = new Color[(n + 1) * (n + 1)];
        Vector3[] samplesPos = new Vector3[(n + 1) * (n + 1)];
        for (int z = 0; z <= n; z++)
        {
            for (int x = 0; x <= n; x++)
            {
                float u = x / (float)n;
                float v = z / (float)n;
                float px = Mathf.Lerp(-extent, extent, u);
                float pz = Mathf.Lerp(-extent, extent, v);
                float y = Hill(px, pz);
                float shade = Mathf.PerlinNoise(px * 0.2f, pz * 0.2f);
                Color grass = Color.Lerp(new Color(0.10f, 0.24f, 0.09f), new Color(0.28f, 0.42f, 0.14f), shade);
                if (y < 0.55f)
                    grass = Color.Lerp(grass, new Color(0.16f, 0.22f, 0.10f), 0.45f);
                int index = z * (n + 1) + x;
                samplesPos[index] = new Vector3(px, y, pz);
                samples[index] = grass;
            }
        }

        for (int z = 0; z < n; z++)
        {
            for (int x = 0; x < n; x++)
            {
                int a = z * (n + 1) + x;
                int b = a + 1;
                int c = a + (n + 1);
                int d = c + 1;
                builder.AddTriangle(samplesPos[a], samplesPos[c], samplesPos[d], samples[a], samples[c], samples[d]);
                builder.AddTriangle(samplesPos[a], samplesPos[d], samplesPos[b], samples[a], samples[d], samples[b]);
            }
        }

        return builder.ToMesh("Knoll");
    }

    static Mesh BuildOak()
    {
        var builder = new MeshBuilder();
        Color bark = new Color(0.32f, 0.20f, 0.09f);
        Color leaf = new Color(0.13f, 0.36f, 0.12f);
        Color leafLight = new Color(0.24f, 0.46f, 0.16f);
        builder.AddCylinder(new Vector3(0f, 0f, 0f), new Vector3(0f, 1.35f, 0f), 0.18f, 0.13f, 7, bark);
        builder.AddSphere(new Vector3(0f, 2.05f, 0f), 0.95f, 6, 8, leaf);
        builder.AddSphere(new Vector3(-0.45f, 1.85f, 0.2f), 0.62f, 5, 7, leafLight);
        builder.AddSphere(new Vector3(0.5f, 1.9f, -0.15f), 0.58f, 5, 7, leaf);
        builder.AddSphere(new Vector3(0.1f, 2.65f, 0.05f), 0.55f, 5, 7, leafLight);
        return builder.ToMesh("Oak");
    }

    static Mesh BuildPine()
    {
        var builder = new MeshBuilder();
        Color bark = new Color(0.28f, 0.17f, 0.08f);
        Color needle = new Color(0.08f, 0.28f, 0.12f);
        Color tip = new Color(0.16f, 0.34f, 0.15f);
        builder.AddCylinder(Vector3.zero, new Vector3(0f, 1.2f, 0f), 0.16f, 0.1f, 6, bark);
        builder.AddCone(new Vector3(0f, 1.05f, 0f), 1.35f, 1.15f, 8, needle);
        builder.AddCone(new Vector3(0f, 1.9f, 0f), 1.25f, 0.82f, 8, tip);
        builder.AddCone(new Vector3(0f, 2.7f, 0f), 0.95f, 0.48f, 7, needle);
        return builder.ToMesh("Pine");
    }

    static Mesh BuildHouse(int variant = 0)
    {
        var builder = new MeshBuilder();
        bool slate = variant % 2 == 1;
        float w = slate ? 1.95f : 2.45f;
        float d = slate ? 1.75f : 2.05f;
        float wallH = slate ? 2.85f : 2.55f;
        float rise = slate ? 1.7f : 1.5f;
        const float thick = 0.2f;
        const float foundation = 0.46f;

        Color plaster = slate ? new Color(0.58f, 0.60f, 0.54f) : new Color(0.72f, 0.64f, 0.50f);
        Color plasterShade = slate ? new Color(0.46f, 0.50f, 0.46f) : new Color(0.60f, 0.50f, 0.38f);
        Color timber = new Color(0.28f, 0.15f, 0.07f);
        Color timberDark = new Color(0.16f, 0.08f, 0.04f);
        Color roof = slate ? new Color(0.24f, 0.27f, 0.30f) : new Color(0.46f, 0.16f, 0.12f);
        Color roofDark = slate ? new Color(0.16f, 0.18f, 0.20f) : new Color(0.30f, 0.10f, 0.08f);
        Color door = new Color(0.22f, 0.11f, 0.05f);
        Color window = new Color(1f, 0.80f, 0.32f);
        Color stone = new Color(0.42f, 0.40f, 0.36f);
        Color stoneDark = new Color(0.26f, 0.25f, 0.23f);
        Color interior = new Color(0.08f, 0.05f, 0.03f);

        float doorHalf = 0.46f;
        float winHalf = 0.32f;
        float winX = w * 0.58f;
        float winBottom = 1.22f;
        float winTop = 2.02f;
        float doorTop = 2.02f;
        float frontZ = d - thick * 0.5f;

        builder.AddBox(new Vector3(0f, foundation * 0.5f, 0f), new Vector3(w * 2f + 0.55f, foundation, d * 2f + 0.5f), Quaternion.identity, stone);
        builder.AddBox(new Vector3(0f, foundation * 0.28f, d + 0.28f), new Vector3(1.15f, foundation * 0.55f, 0.42f), Quaternion.identity, stoneDark);

        void Slab(float x0, float x1, float y0, float y1, float z, float depth, Color color)
        {
            if (x1 - x0 < 0.02f || y1 - y0 < 0.02f)
                return;
            builder.AddBox(new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, z), new Vector3(x1 - x0, y1 - y0, depth), Quaternion.identity, color);
        }

        Slab(-w, -doorHalf, foundation, winBottom, frontZ, thick, plaster);
        Slab(doorHalf, w, foundation, winBottom, frontZ, thick, plasterShade);
        Slab(-w, -winX - winHalf, winBottom, winTop, frontZ, thick, plaster);
        Slab(-winX + winHalf, -doorHalf, winBottom, doorTop, frontZ, thick, plasterShade);
        Slab(doorHalf, winX - winHalf, winBottom, doorTop, frontZ, thick, plaster);
        Slab(winX + winHalf, w, winBottom, winTop, frontZ, thick, plasterShade);
        Slab(-w, w, doorTop, wallH, frontZ, thick, plaster);

        Slab(-winX - winHalf, -winX + winHalf, winBottom, winTop, 0f, 0.08f, interior);
        Slab(winX - winHalf, winX + winHalf, winBottom, winTop, 0f, 0.08f, interior);
        Slab(-doorHalf, doorHalf, foundation, doorTop, d - 0.55f, 0.08f, interior);

        builder.AddBox(new Vector3(0f, (foundation + doorTop) * 0.5f, d - 0.16f), new Vector3(doorHalf * 2f - 0.12f, doorTop - foundation - 0.08f, 0.08f), Quaternion.identity, door);
        builder.AddBox(new Vector3(0f, (foundation + doorTop) * 0.5f, d - 0.1f), new Vector3(0.08f, doorTop - foundation, 0.06f), Quaternion.identity, timberDark);
        builder.AddBox(new Vector3(-0.16f, foundation + 0.95f, d - 0.1f), new Vector3(0.08f, 0.08f, 0.08f), Quaternion.identity, new Color(0.72f, 0.58f, 0.22f));

        void Window(float x)
        {
            float z = d + 0.015f;
            builder.AddBox(new Vector3(x, (winBottom + winTop) * 0.5f, z), new Vector3(winHalf * 2f - 0.08f, winTop - winBottom - 0.08f, 0.04f), Quaternion.identity, window);
            builder.AddBox(new Vector3(x, winTop + 0.05f, z + 0.03f), new Vector3(winHalf * 2f + 0.16f, 0.1f, 0.08f), Quaternion.identity, timber);
            builder.AddBox(new Vector3(x, winBottom - 0.05f, z + 0.03f), new Vector3(winHalf * 2f + 0.16f, 0.1f, 0.08f), Quaternion.identity, timber);
            builder.AddBox(new Vector3(x - winHalf - 0.02f, (winBottom + winTop) * 0.5f, z + 0.03f), new Vector3(0.1f, winTop - winBottom + 0.16f, 0.08f), Quaternion.identity, timber);
            builder.AddBox(new Vector3(x + winHalf + 0.02f, (winBottom + winTop) * 0.5f, z + 0.03f), new Vector3(0.1f, winTop - winBottom + 0.16f, 0.08f), Quaternion.identity, timber);
            builder.AddBox(new Vector3(x, (winBottom + winTop) * 0.5f, z + 0.04f), new Vector3(0.06f, winTop - winBottom, 0.05f), Quaternion.identity, timberDark);
        }

        Window(-winX);
        Window(winX);

        float sideX = w - thick * 0.5f;
        builder.AddBox(new Vector3(-sideX, (foundation + wallH) * 0.5f, 0f), new Vector3(thick, wallH - foundation, d * 2f), Quaternion.identity, plasterShade);
        builder.AddBox(new Vector3(sideX, (foundation + wallH) * 0.5f, 0f), new Vector3(thick, wallH - foundation, d * 2f), Quaternion.identity, plaster);
        builder.AddBox(new Vector3(0f, (foundation + wallH) * 0.5f, -d + thick * 0.5f), new Vector3(w * 2f, wallH - foundation, thick), Quaternion.identity, plasterShade);

        void SideWindow(float sign, float z)
        {
            float frameX = sign * (w + 0.04f);
            float paneX = sign * (w + 0.1f);
            builder.AddBox(new Vector3(frameX, 1.62f, z), new Vector3(0.08f, 0.8f, 0.68f), Quaternion.identity, timber);
            builder.AddBox(new Vector3(paneX, 1.62f, z), new Vector3(0.04f, 0.58f, 0.46f), Quaternion.identity, window);
        }

        SideWindow(-1f, 0.15f);
        SideWindow(1f, -0.35f);

        float post = 0.16f;
        void Post(Vector3 at)
        {
            builder.AddBox(new Vector3(at.x, (foundation + wallH) * 0.5f, at.z), new Vector3(post, wallH - foundation + 0.08f, post), Quaternion.identity, timber);
        }

        Post(new Vector3(-w, 0f, d));
        Post(new Vector3(w, 0f, d));
        Post(new Vector3(-w, 0f, -d));
        Post(new Vector3(w, 0f, -d));
        builder.AddBox(new Vector3(0f, wallH - 0.06f, d + 0.02f), new Vector3(w * 2f + 0.08f, 0.12f, 0.14f), Quaternion.identity, timberDark);
        builder.AddBox(new Vector3(0f, foundation + 0.08f, d + 0.02f), new Vector3(w * 2f, 0.12f, 0.1f), Quaternion.identity, timber);

        float porch = d + 0.85f;
        float postTop = wallH - 0.34f;
        float postHeight = postTop - foundation;
        builder.AddBox(new Vector3(-0.72f, foundation + postHeight * 0.5f, porch), new Vector3(0.12f, postHeight, 0.12f), Quaternion.identity, timber);
        builder.AddBox(new Vector3(0.72f, foundation + postHeight * 0.5f, porch), new Vector3(0.12f, postHeight, 0.12f), Quaternion.identity, timber);
        builder.AddBox(new Vector3(0f, wallH - 0.28f, d + 0.4f), new Vector3(1.85f, 0.1f, 1.05f), Quaternion.Euler(8f, 0f, 0f), roofDark);

        float overhang = d + 0.48f;
        float halfW = w + 0.42f;
        float baseY = wallH - 0.02f;
        float peakY = wallH + rise;
        Vector3 leftEave = new Vector3(-halfW, baseY, 0f);
        Vector3 rightEave = new Vector3(halfW, baseY, 0f);
        Vector3 ridge = new Vector3(0f, peakY, 0f);
        float leftAngle = Mathf.Atan2(rise, halfW) * Mathf.Rad2Deg;
        AddSlope(builder, leftEave, ridge, overhang * 2f, leftAngle, roof, roofDark);
        AddSlope(builder, rightEave, ridge, overhang * 2f, -leftAngle, roof, roofDark);
        builder.AddBox(new Vector3(0f, peakY + 0.05f, 0f), new Vector3(0.2f, 0.12f, overhang * 2f + 0.08f), Quaternion.identity, roofDark);

        void Gable(float z, Color color)
        {
            Vector3 l = new Vector3(-w + 0.05f, wallH, z);
            Vector3 r = new Vector3(w - 0.05f, wallH, z);
            Vector3 p = new Vector3(0f, peakY - 0.12f, z);
            builder.AddTriangle(l, p, r, color);
            builder.AddTriangle(r, p, l, color);
        }

        Gable(d - 0.04f, plaster);
        Gable(-(d - 0.04f), plasterShade);

        float chimneyX = halfW * 0.4f;
        float chimneyY = baseY + rise * (1f - chimneyX / halfW);
        float chimneyZ = -d * 0.15f;
        builder.AddBox(new Vector3(chimneyX, chimneyY + 0.6f, chimneyZ), new Vector3(0.5f, 1.25f, 0.5f), Quaternion.identity, stone);
        builder.AddBox(new Vector3(chimneyX, chimneyY + 1.28f, chimneyZ), new Vector3(0.66f, 0.14f, 0.66f), Quaternion.identity, stoneDark);

        Mesh mesh = builder.ToMesh(slate ? "CottageTall" : "Cottage");
        return mesh;
    }

    static void AddSlope(MeshBuilder builder, Vector3 eave, Vector3 ridge, float depth, float angle, Color roof, Color roofDark)
    {
        Vector3 normal = Quaternion.Euler(0f, 0f, angle) * Vector3.up;
        Vector3 mid = (eave + ridge) * 0.5f;
        float length = Vector3.Distance(eave, ridge);
        builder.AddBox(mid, new Vector3(length, 0.12f, depth), Quaternion.Euler(0f, 0f, angle), roofDark);
        const int rows = 5;
        for (int i = 0; i < rows; i++)
        {
            float t0 = i / (float)rows;
            float t1 = (i + 0.78f) / rows;
            Vector3 a = Vector3.Lerp(eave, ridge, t0);
            Vector3 b = Vector3.Lerp(eave, ridge, t1);
            Vector3 center = (a + b) * 0.5f + normal * 0.07f;
            builder.AddBox(center, new Vector3(Vector3.Distance(a, b), 0.045f, depth - 0.12f), Quaternion.Euler(0f, 0f, angle), i % 2 == 0 ? roof : roofDark);
        }
    }

    static Mesh BuildElf()
    {
        var builder = new MeshBuilder();
        Color skin = new Color(0.93f, 0.76f, 0.62f);
        Color hair = new Color(0.72f, 0.55f, 0.22f);
        Color tunic = new Color(0.10f, 0.32f, 0.18f);
        Color trim = new Color(0.62f, 0.48f, 0.18f);
        Color boot = new Color(0.22f, 0.12f, 0.07f);
        Color bow = new Color(0.40f, 0.24f, 0.10f);
        Color stringColor = new Color(0.75f, 0.7f, 0.55f);
        Color arrow = new Color(0.55f, 0.42f, 0.22f);

        builder.AddCylinder(new Vector3(-0.09f, 0.08f, 0f), new Vector3(-0.09f, 0.72f, 0f), 0.07f, 0.06f, 6, skin);
        builder.AddCylinder(new Vector3(0.09f, 0.08f, 0f), new Vector3(0.09f, 0.72f, 0f), 0.07f, 0.06f, 6, skin);
        builder.AddBox(new Vector3(-0.09f, 0.06f, 0.03f), new Vector3(0.1f, 0.1f, 0.18f), Quaternion.identity, boot);
        builder.AddBox(new Vector3(0.09f, 0.06f, 0.03f), new Vector3(0.1f, 0.1f, 0.18f), Quaternion.identity, boot);

        builder.AddCone(new Vector3(0f, 0.62f, 0f), 0.42f, 0.34f, 8, tunic);
        builder.AddBox(new Vector3(0f, 1.05f, 0f), new Vector3(0.34f, 0.42f, 0.18f), Quaternion.identity, tunic);
        builder.AddBox(new Vector3(0f, 0.86f, 0.02f), new Vector3(0.36f, 0.06f, 0.2f), Quaternion.identity, trim);
        builder.AddBox(new Vector3(0f, 1.22f, 0.1f), new Vector3(0.16f, 0.08f, 0.04f), Quaternion.identity, trim);

        builder.AddSphere(new Vector3(0f, 1.48f, 0f), 0.13f, 6, 8, skin);
        builder.AddBox(new Vector3(-0.12f, 1.5f, 0.02f), new Vector3(0.08f, 0.035f, 0.16f), Quaternion.Euler(0f, 18f, -8f), skin);
        builder.AddBox(new Vector3(0.12f, 1.5f, 0.02f), new Vector3(0.08f, 0.035f, 0.16f), Quaternion.Euler(0f, -18f, 8f), skin);
        builder.AddBox(new Vector3(0f, 1.5f, -0.06f), new Vector3(0.2f, 0.28f, 0.12f), Quaternion.identity, hair);
        builder.AddBox(new Vector3(0f, 1.22f, -0.1f), new Vector3(0.16f, 0.42f, 0.08f), Quaternion.identity, hair);
        builder.AddBox(new Vector3(-0.08f, 1.62f, 0.02f), new Vector3(0.16f, 0.06f, 0.16f), Quaternion.identity, hair);
        builder.AddBox(new Vector3(0.08f, 1.62f, 0.02f), new Vector3(0.16f, 0.06f, 0.16f), Quaternion.identity, hair);

        Quaternion leftArm = Quaternion.Euler(20f, 0f, 28f);
        Quaternion rightArm = Quaternion.Euler(55f, -20f, -18f);
        builder.AddBox(new Vector3(-0.28f, 1.12f, 0.08f), new Vector3(0.08f, 0.38f, 0.08f), leftArm, skin);
        builder.AddBox(new Vector3(0.3f, 1.08f, 0.16f), new Vector3(0.08f, 0.36f, 0.08f), rightArm, tunic);

        Vector3 hand = new Vector3(-0.34f, 0.98f, 0.18f);
        for (int i = 0; i <= 7; i++)
        {
            float t = Mathf.Lerp(-1f, 1f, i / 7f);
            Vector3 point = hand + new Vector3(-0.02f, t * 0.42f, 0.16f * (1f - t * t));
            builder.AddSphere(point, 0.028f, 3, 4, bow);
        }

        builder.AddCylinder(hand + new Vector3(0f, 0.42f, 0.02f), hand + new Vector3(0f, -0.42f, 0.02f), 0.008f, 0.008f, 3, stringColor);
        builder.AddCylinder(hand + new Vector3(0.02f, 0.02f, 0.02f), hand + new Vector3(0.02f, 0.02f, 0.48f), 0.012f, 0.012f, 3, arrow);
        builder.AddBox(hand + new Vector3(0.02f, 0.02f, 0.56f), new Vector3(0.035f, 0.035f, 0.1f), Quaternion.identity, trim);
        return builder.ToMesh("Elf");
    }

    static Mesh BuildTuft()
    {
        var builder = new MeshBuilder();
        Color a = new Color(0.18f, 0.4f, 0.12f);
        Color b = new Color(0.30f, 0.48f, 0.16f);
        builder.AddBlade(new Vector3(0f, 0f, 0f), 0.55f, 0.16f, 0f, a);
        builder.AddBlade(new Vector3(0.05f, 0f, 0.02f), 0.4f, 0.12f, 70f, b);
        builder.AddBlade(new Vector3(-0.04f, 0f, 0.03f), 0.48f, 0.12f, -55f, a);
        return builder.ToMesh("GrassTuft");
    }

    static Mesh BuildRock()
    {
        var builder = new MeshBuilder();
        builder.AddSphere(Vector3.zero, 0.45f, 4, 6, new Color(0.32f, 0.33f, 0.34f));
        builder.AddSphere(new Vector3(0.28f, -0.05f, 0.1f), 0.28f, 3, 5, new Color(0.25f, 0.26f, 0.28f));
        return builder.ToMesh("Rock");
    }

    static Mesh CenterPivot(Mesh mesh)
    {
        Vector3 center = mesh.bounds.center;
        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] -= center;
        mesh.vertices = vertices;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    static void AssignMesh(GameObject go, Mesh mesh, Material material)
    {
        MeshFilter filter = go.GetComponent<MeshFilter>();
        if (filter == null)
            filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null)
            renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.enabled = true;
    }

    static void FitCollider(GameObject go, Mesh mesh)
    {
        Bounds bounds = mesh.bounds;
        BoxCollider box = go.GetComponent<BoxCollider>();
        if (box != null)
        {
            box.center = bounds.center;
            box.size = bounds.size;
        }

        SphereCollider sphere = go.GetComponent<SphereCollider>();
        if (sphere != null)
        {
            sphere.center = bounds.center;
            sphere.radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        }

        CapsuleCollider capsule = go.GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            capsule.direction = 1;
            capsule.center = bounds.center;
            capsule.height = Mathf.Max(bounds.size.y, 0.2f);
            capsule.radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
        }
    }

    static GameObject CreateProp(string name, Mesh mesh, Material material, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Art"))
            AssetDatabase.CreateFolder("Assets", "Art");
        if (!AssetDatabase.IsValidFolder(ArtFolder))
            AssetDatabase.CreateFolder("Assets/Art", "Knoll");
    }

    static Mesh SaveMesh(string name, Mesh mesh)
    {
        string path = ArtFolder + "/" + name + ".asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        existing.Clear();
        existing.vertices = mesh.vertices;
        existing.colors = mesh.colors;
        existing.triangles = mesh.triangles;
        existing.RecalculateNormals();
        existing.RecalculateBounds();
        existing.name = mesh.name;
        EditorUtility.SetDirty(existing);
        Object.DestroyImmediate(mesh);
        return existing;
    }

    static Material SaveMaterial(string file, string shaderName)
    {
        string path = ArtFolder + "/" + file;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find(shaderName);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }

        return material;
    }

    static GameObject FindOne(string name)
    {
        List<GameObject> all = FindAll(name);
        return all.Count > 0 ? all[0] : null;
    }

    static List<GameObject> FindAll(string name)
    {
        var list = new List<GameObject>();
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            Collect(root.transform, name, list);
        return list;
    }

    static void Collect(Transform transform, string name, List<GameObject> list)
    {
        if (transform.name == name)
            list.Add(transform.gameObject);
        for (int i = 0; i < transform.childCount; i++)
            Collect(transform.GetChild(i), name, list);
    }

    static GameObject FindTreeNear(Vector3 position, float radius)
    {
        foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != "Tree")
                continue;
            Vector3 flat = root.transform.position - position;
            flat.y = 0f;
            if (flat.magnitude <= radius)
                return root;
        }

        return null;
    }

    static void RemoveIfPresent(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go != null)
            Object.DestroyImmediate(go);
    }

    static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    sealed class MeshBuilder
    {
        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Color> _colors = new List<Color>();
        readonly List<int> _triangles = new List<int>();

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            AddTriangle(a, b, c, color, color, color);
        }

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
        {
            int index = _vertices.Count;
            _vertices.Add(a);
            _vertices.Add(b);
            _vertices.Add(c);
            _colors.Add(ca);
            _colors.Add(cb);
            _colors.Add(cc);
            _triangles.Add(index);
            _triangles.Add(index + 1);
            _triangles.Add(index + 2);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            AddTriangle(a, b, c, color);
            AddTriangle(a, c, d, color);
        }

        public void AddBox(Vector3 center, Vector3 size, Quaternion rotation, Color color)
        {
            Vector3 h = size * 0.5f;
            Vector3[] p =
            {
                Local(-h.x, -h.y, -h.z), Local(-h.x, -h.y, h.z), Local(-h.x, h.y, -h.z), Local(-h.x, h.y, h.z),
                Local(h.x, -h.y, -h.z), Local(h.x, -h.y, h.z), Local(h.x, h.y, -h.z), Local(h.x, h.y, h.z)
            };
            AddQuad(p[0], p[2], p[3], p[1], color);
            AddQuad(p[4], p[5], p[7], p[6], color);
            AddQuad(p[0], p[1], p[5], p[4], color);
            AddQuad(p[2], p[6], p[7], p[3], color);
            AddQuad(p[0], p[4], p[6], p[2], color);
            AddQuad(p[1], p[3], p[7], p[5], color);

            Vector3 Local(float x, float y, float z)
            {
                return center + rotation * new Vector3(x, y, z);
            }
        }

        public void AddCylinder(Vector3 bottom, Vector3 top, float bottomRadius, float topRadius, int slices, Color color)
        {
            Vector3 axis = top - bottom;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, axis.sqrMagnitude < 0.0001f ? Vector3.up : axis.normalized);
            for (int i = 0; i < slices; i++)
            {
                float a0 = i / (float)slices * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)slices * Mathf.PI * 2f;
                Vector3 b0 = Ring(bottom, rotation, bottomRadius, a0);
                Vector3 b1 = Ring(bottom, rotation, bottomRadius, a1);
                Vector3 t0 = Ring(top, rotation, topRadius, a0);
                Vector3 t1 = Ring(top, rotation, topRadius, a1);
                AddQuad(b0, t0, t1, b1, color);
                AddTriangle(bottom, b1, b0, color);
                AddTriangle(top, t0, t1, color);
            }
        }

        public void AddCone(Vector3 baseCenter, float height, float radius, int slices, Color color)
        {
            Vector3 tip = baseCenter + Vector3.up * height;
            for (int i = 0; i < slices; i++)
            {
                float a0 = i / (float)slices * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)slices * Mathf.PI * 2f;
                Vector3 p0 = baseCenter + new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                Vector3 p1 = baseCenter + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                AddTriangle(p0, tip, p1, color);
                AddTriangle(baseCenter, p1, p0, color);
            }
        }

        public void AddSphere(Vector3 center, float radius, int lat, int lon, Color color)
        {
            for (int y = 0; y < lat; y++)
            {
                float v0 = y / (float)lat;
                float v1 = (y + 1) / (float)lat;
                float y0 = Mathf.Cos(v0 * Mathf.PI);
                float y1 = Mathf.Cos(v1 * Mathf.PI);
                float r0 = Mathf.Sin(v0 * Mathf.PI);
                float r1 = Mathf.Sin(v1 * Mathf.PI);
                for (int x = 0; x < lon; x++)
                {
                    float u0 = x / (float)lon * Mathf.PI * 2f;
                    float u1 = (x + 1) / (float)lon * Mathf.PI * 2f;
                    Vector3 p00 = center + new Vector3(Mathf.Cos(u0) * r0, y0, Mathf.Sin(u0) * r0) * radius;
                    Vector3 p10 = center + new Vector3(Mathf.Cos(u1) * r0, y0, Mathf.Sin(u1) * r0) * radius;
                    Vector3 p01 = center + new Vector3(Mathf.Cos(u0) * r1, y1, Mathf.Sin(u0) * r1) * radius;
                    Vector3 p11 = center + new Vector3(Mathf.Cos(u1) * r1, y1, Mathf.Sin(u1) * r1) * radius;
                    AddTriangle(p00, p01, p11, color);
                    AddTriangle(p00, p11, p10, color);
                }
            }
        }

        public void AddRoof(float halfWidth, float halfDepth, float baseY, float peakY, Color color)
        {
            Vector3 leftEaveA = new Vector3(-halfWidth, baseY, -halfDepth);
            Vector3 leftEaveB = new Vector3(-halfWidth, baseY, halfDepth);
            Vector3 rightEaveA = new Vector3(halfWidth, baseY, -halfDepth);
            Vector3 rightEaveB = new Vector3(halfWidth, baseY, halfDepth);
            Vector3 ridgeA = new Vector3(0f, peakY, -halfDepth);
            Vector3 ridgeB = new Vector3(0f, peakY, halfDepth);
            AddQuad(leftEaveA, ridgeA, ridgeB, leftEaveB, color);
            AddQuad(rightEaveB, ridgeB, ridgeA, rightEaveA, color);
            AddTriangle(leftEaveB, ridgeB, rightEaveB, color);
            AddTriangle(rightEaveA, ridgeA, leftEaveA, color);
        }

        public void AddBlade(Vector3 basePoint, float height, float width, float yaw, Color color)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 right = rotation * Vector3.right * width;
            Vector3 up = Vector3.up * height;
            Vector3 lean = rotation * Vector3.forward * 0.08f;
            AddTriangle(basePoint - right, basePoint + right, basePoint + up + lean, color);
            AddTriangle(basePoint + right, basePoint - right, basePoint + up + lean, color);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh();
            mesh.name = name;
            mesh.SetVertices(_vertices);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Vector3 Ring(Vector3 center, Quaternion rotation, float radius, float angle)
        {
            Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            return center + rotation * offset;
        }
    }
}
