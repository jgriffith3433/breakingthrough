using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Turns Gen robot FBX (YoBot.fbx / SpiderBot.fbx under Resources/Models/Bots) into gameplay prefabs
// BotYo and BotSpider with part names Enemy.FindPart expects. Menu: Tools → Bots → Build Prefabs.
// Also runs on load once the FBX files exist. Does not edit scene2.
[InitializeOnLoad]
static class SetupBots
{
    const string Folder = "Assets/Resources/Models/Bots";
    const int YoBudget = 4000;
    const int SpiderBudget = 8000;
    const int TexMax = 1024;
    const int MaxRetries = 8;
    static int retries;

    struct BotSpec
    {
        public string Fbx;
        public string Prefab;
        public int TriangleBudget;
        public Dictionary<string, string> Rename;
    }

    // Gen robot template roles → gameplay part names (AGENTS.md / Enemy.BindParts).
    static readonly BotSpec Yo = new BotSpec
    {
        Fbx = Folder + "/YoBot.fbx",
        Prefab = Folder + "/BotYo.prefab",
        TriangleBudget = YoBudget,
        Rename = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "body", "Body" },
            { "head", "Head" },
            { "arm_l", "ClawL" }, { "ArmL", "ClawL" },
            { "arm_r", "ClawR" }, { "ArmR", "ClawR" },
            { "leg_fl", "LegFL" },
            { "leg_fr", "LegFR" },
            { "leg_bl", "LegBL" },
            { "leg_br", "LegBR" },
        }
    };

    static readonly BotSpec Spider = new BotSpec
    {
        Fbx = Folder + "/SpiderBot.fbx",
        Prefab = Folder + "/BotSpider.prefab",
        TriangleBudget = SpiderBudget,
        Rename = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "body", "Body" },
            { "head", "Head" },
            { "leg1", "LegL1" }, { "leg2", "LegR1" },
            { "leg3", "LegL2" }, { "leg4", "LegR2" },
            { "leg5", "LegL3" }, { "leg6", "LegR3" },
        }
    };

    static SetupBots()
    {
        EditorApplication.delayCall += Run;
    }

    [MenuItem("Tools/Bots/Build Prefabs")]
    static void MenuBuild()
    {
        retries = 0;
        Build(force: true);
    }

    static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += Run;
            return;
        }
        if (retries >= MaxRetries)
            return;
        if (!File.Exists(Yo.Fbx) && !File.Exists(Spider.Fbx))
            return;
        if (!Build(force: false))
        {
            retries++;
            EditorApplication.delayCall += Run;
        }
    }

    static bool Build(bool force)
    {
        if (!EnsureFolder(Folder))
            return false;

        bool ok = true;
        if (File.Exists(Yo.Fbx))
            ok &= BuildOne(Yo, force);
        if (File.Exists(Spider.Fbx))
            ok &= BuildOne(Spider, force);
        AssetDatabase.SaveAssets();
        return ok;
    }

    static bool BuildOne(BotSpec spec, bool force)
    {
        if (!EnsureImport(spec.Fbx))
            return false;

        GameObject modelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(spec.Fbx);
        if (modelRoot == null)
            return false;

        string existing = AssetDatabase.AssetPathToGUID(spec.Prefab);
        if (!force && !string.IsNullOrEmpty(existing))
        {
            // Still re-check budgets when forced off; skip rewrite if prefab is current enough.
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(modelRoot);
        instance.name = Path.GetFileNameWithoutExtension(spec.Prefab);

        foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
            Object.DestroyImmediate(animator);

        RenameParts(instance.transform, spec.Rename);

        int tris = CountTriangles(instance);
        if (tris > spec.TriangleBudget)
            Debug.LogWarning(spec.Prefab + " has " + tris + " triangles (budget " + spec.TriangleBudget + ").");
        else
            Debug.Log(spec.Prefab + " ready (" + tris + " tris, budget " + spec.TriangleBudget + ").");

        PrefabUtility.SaveAsPrefabAsset(instance, spec.Prefab);
        Object.DestroyImmediate(instance);
        return true;
    }

    static bool EnsureImport(string fbxPath)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
            return false;

        bool ok = importer.isReadable
            && importer.animationType == ModelImporterAnimationType.Generic
            && importer.importAnimation
            && importer.materialImportMode != ModelImporterMaterialImportMode.None;

        foreach (TextureImporter tex in FindModelTextures(fbxPath))
        {
            if (tex.maxTextureSize > TexMax)
            {
                tex.maxTextureSize = TexMax;
                tex.SaveAndReimport();
                ok = false;
            }
        }

        if (ok)
            return true;

        importer.isReadable = true;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.animationCompression = ModelImporterAnimationCompression.Optimal;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.importNormals = ModelImporterNormals.Calculate;
        importer.normalSmoothingAngle = 60f;
        importer.SaveAndReimport();
        return false;
    }

    static TextureImporter[] FindModelTextures(string modelPath)
    {
        var list = new List<TextureImporter>();
        string folder = Path.GetDirectoryName(modelPath).Replace('\\', '/');
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
                list.Add(importer);
        }
        return list.ToArray();
    }

    static void RenameParts(Transform root, Dictionary<string, string> map)
    {
        // Deepest first so parents keep stable paths while children rename.
        var all = new List<Transform>(root.GetComponentsInChildren<Transform>(true));
        all.Sort((a, b) => Depth(b).CompareTo(Depth(a)));
        foreach (Transform t in all)
        {
            string mapped;
            if (map.TryGetValue(t.name, out mapped) && t.name != mapped)
                t.name = mapped;
        }
    }

    static int Depth(Transform t)
    {
        int d = 0;
        while (t != null)
        {
            d++;
            t = t.parent;
        }
        return d;
    }

    static int CountTriangles(GameObject root)
    {
        int tris = 0;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh != null)
                tris += filter.sharedMesh.triangles.Length / 3;
        }
        foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skin.sharedMesh != null)
                tris += skin.sharedMesh.triangles.Length / 3;
        }
        return tris;
    }

    static bool EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return true;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent) && parent.StartsWith("Assets/"))
        {
            if (!EnsureFolder(parent))
                return false;
        }
        return !string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, name));
    }

}
