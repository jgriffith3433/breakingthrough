using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Mixamo FBX files ship with embedded textures, and the pirate rig was imported
// with a foot bone stripped out, so the piano clip could not play.
[InitializeOnLoad]
static class FixMixamoImport
{
    const string WarrokPath = "Assets/Art/Warrok W Kurniawan.fbx";
    const string PiratePath = "Assets/Art/PianoPlayer/Pirate By P. Konstantinov.fbx";
    const string ClipPath = "Assets/Art/PianoPlayer/Piano Playing.fbx";
    const string ControllerPath = "Assets/Art/PianoPlayer/PianoPlayer.controller";

    static readonly string[][] HumanMap =
    {
        new[] { "Hips", "Hips" },
        new[] { "Spine", "Spine" },
        new[] { "Chest", "Spine1" },
        new[] { "UpperChest", "Spine2" },
        new[] { "Neck", "Neck" },
        new[] { "Head", "Head" },
        new[] { "LeftShoulder", "LeftShoulder" },
        new[] { "LeftUpperArm", "LeftArm" },
        new[] { "LeftLowerArm", "LeftForeArm" },
        new[] { "LeftHand", "LeftHand" },
        new[] { "RightShoulder", "RightShoulder" },
        new[] { "RightUpperArm", "RightArm" },
        new[] { "RightLowerArm", "RightForeArm" },
        new[] { "RightHand", "RightHand" },
        new[] { "LeftUpperLeg", "LeftUpLeg" },
        new[] { "LeftLowerLeg", "LeftLeg" },
        new[] { "LeftFoot", "LeftFoot" },
        new[] { "LeftToes", "LeftToeBase" },
        new[] { "RightUpperLeg", "RightUpLeg" },
        new[] { "RightLowerLeg", "RightLeg" },
        new[] { "RightFoot", "RightFoot" },
        new[] { "RightToes", "RightToeBase" }
    };

    static int _passes;

    static FixMixamoImport()
    {
        EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += Run;
            return;
        }

        if (EditorSceneManager.GetActiveScene().name != "scene2")
            return;

        if (_passes++ > 8)
        {
            Debug.LogWarning("Mixamo import fix stopped after several attempts.");
            return;
        }

        bool busy = false;
        busy |= PrepareModel(WarrokPath, false);
        busy |= PrepareModel(PiratePath, true);
        busy |= PrepareClip();
        if (busy)
        {
            EditorApplication.delayCall += Run;
            return;
        }

        if (!AvatarOk(PiratePath))
        {
            if (ApplyHumanMap(PiratePath))
            {
                EditorApplication.delayCall += Run;
                return;
            }
        }

        if (!AvatarOk(ClipPath))
            ApplyHumanMap(ClipPath);

        if (AssignTextures())
        {
            EditorApplication.delayCall += Run;
            return;
        }

        FinishPirate();
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log(Report());
    }

    static bool AssignTextures()
    {
        bool changed = false;
        changed |= AssignSet(WarrokPath, "Assets/Art/WarrokTextures");
        changed |= AssignSet(PiratePath, "Assets/Art/PirateTextures");
        return changed;
    }

    static bool AssignSet(string modelPath, string folder)
    {
        string diffusePath = FindTexture(folder, "diffuse");
        if (diffusePath == null)
            return false;

        if (AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath) == null)
        {
            AssetDatabase.ImportAsset(diffusePath, ImportAssetOptions.ForceUpdate);
            return true;
        }

        string normalPath = FindTexture(folder, "normal");
        string specPath = FindTexture(folder, "specular");
        string aoPath = FindTexture(folder, "ambientocclusion");
        if (MarkNormal(normalPath))
            return true;

        Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        Texture2D spec = AssetDatabase.LoadAssetAtPath<Texture2D>(specPath);
        Texture2D ao = AssetDatabase.LoadAssetAtPath<Texture2D>(aoPath);
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        bool changed = false;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
        {
            Material source = asset as Material;
            if (source == null)
                continue;

            string matPath = folder + "/" + source.name + ".mat";
            Material external = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (external != null && external.mainTexture == diffuse)
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), external);
                continue;
            }

            external = new Material(Shader.Find("Standard"));
            external.name = source.name;
            external.color = Color.white;
            external.mainTexture = diffuse;
            if (normal != null)
            {
                external.SetTexture("_BumpMap", normal);
                external.EnableKeyword("_NORMALMAP");
            }

            if (spec != null)
            {
                external.SetTexture("_SpecGlossMap", spec);
                external.SetColor("_SpecColor", Color.white);
                external.EnableKeyword("_SPECGLOSSMAP");
            }

            if (ao != null)
                external.SetTexture("_OcclusionMap", ao);

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing == null)
                AssetDatabase.CreateAsset(external, matPath);
            else
                EditorUtility.CopySerialized(external, existing);
            external = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name), external);
            changed = true;
        }

        if (changed)
            importer.SaveAndReimport();
        return changed;
    }

    static bool MarkNormal(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        var textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
        if (textureImporter == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return true;
        }

        if (textureImporter.textureType == TextureImporterType.NormalMap)
            return false;
        textureImporter.textureType = TextureImporterType.NormalMap;
        textureImporter.SaveAndReimport();
        return true;
    }

    static string ToAssetPath(string file)
    {
        file = file.Replace('\\', '/');
        string data = Application.dataPath.Replace('\\', '/');
        if (file.StartsWith(data, System.StringComparison.OrdinalIgnoreCase))
            return "Assets" + file.Substring(data.Length);
        int assets = file.IndexOf("/Assets/", System.StringComparison.OrdinalIgnoreCase);
        if (assets >= 0)
            return file.Substring(assets + 1);
        return file;
    }

    static string FindTexture(string folder, string token)
    {
        if (!Directory.Exists(folder))
            return null;
        foreach (string file in Directory.GetFiles(folder))
        {
            string name = Path.GetFileName(file);
            if (name.EndsWith(".meta"))
                continue;
            if (name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return ToAssetPath(file);
        }

        return null;
    }

    static bool PrepareModel(string path, bool fixRig)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
            return false;

        bool materialsReady = importer.materialLocation == ModelImporterMaterialLocation.InPrefab
            && importer.materialImportMode == ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        bool rigReady = !fixRig || !importer.optimizeBones;
        if (materialsReady && rigReady)
            return false;

        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        importer.materialSearch = ModelImporterMaterialSearch.Local;
        if (fixRig)
        {
            importer.optimizeBones = false;
            importer.optimizeGameObjects = false;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        }

        importer.SaveAndReimport();
        return true;
    }

    static bool PrepareClip()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(ClipPath);
        AnimationClip clip = LoadClip(ClipPath);
        if (importer.optimizeBones == false
            && importer.animationType == ModelImporterAnimationType.Human
            && importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel
            && clip != null)
            return false;

        importer.optimizeBones = false;
        importer.optimizeGameObjects = false;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.sourceAvatar = null;
        ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
        if (takes == null || takes.Length == 0)
            takes = importer.clipAnimations;
        if (takes != null)
        {
            for (int i = 0; i < takes.Length; i++)
            {
                takes[i].loopTime = true;
                takes[i].lockRootRotation = true;
                takes[i].lockRootHeightY = true;
                takes[i].lockRootPositionXZ = true;
                takes[i].keepOriginalOrientation = true;
                takes[i].keepOriginalPositionY = true;
                takes[i].keepOriginalPositionXZ = true;
            }

            importer.clipAnimations = takes;
        }

        importer.SaveAndReimport();
        return true;
    }

    static bool ApplyHumanMap(string path)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        if (model == null || importer == null)
            return false;

        var human = new List<HumanBone>();
        for (int i = 0; i < HumanMap.Length; i++)
        {
            string bone = FindBone(model.transform, HumanMap[i][1]);
            if (bone == null)
                continue;
            human.Add(new HumanBone
            {
                humanName = HumanMap[i][0],
                boneName = bone,
                limit = new HumanLimit { useDefaultValues = true }
            });
        }

        if (human.Count < 15)
            return false;

        var skeleton = new List<SkeletonBone>();
        foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
        {
            skeleton.Add(new SkeletonBone
            {
                name = bone.name,
                position = bone.localPosition,
                rotation = bone.localRotation,
                scale = bone.localScale
            });
        }

        HumanDescription description = importer.humanDescription;
        description.human = human.ToArray();
        description.skeleton = skeleton.ToArray();
        importer.humanDescription = description;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.optimizeBones = false;
        importer.SaveAndReimport();
        return true;
    }

    static void FinishPirate()
    {
        GameObject pirate = GameObject.Find("Pirate");
        if (pirate == null)
            return;

        Animator animator = pirate.GetComponent<Animator>();
        if (animator == null)
            animator = pirate.AddComponent<Animator>();
        Avatar avatar = LoadAvatar(PiratePath);
        if (avatar != null)
            animator.avatar = avatar;
        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (controller != null)
            animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.enabled = true;

        pirate.transform.localScale = Vector3.one;
        float height = BoundsOf(pirate).size.y;
        if (height < 1.3f || height > 2.3f)
            pirate.transform.localScale = Vector3.one * (1.75f / Mathf.Max(height, 0.01f));

        Transform root = pirate.transform.parent != null ? pirate.transform.parent : pirate.transform;
        float ground = root.position.y;
        float lift = ground - BoundsOf(pirate).min.y;
        pirate.transform.position += new Vector3(0f, lift, 0f);
        EditorUtility.SetDirty(pirate);
    }

    static string Report()
    {
        AnimationClip clip = LoadClip(ClipPath);
        Avatar avatar = LoadAvatar(PiratePath);
        return "Mixamo textures assigned. Pirate avatar "
            + (avatar != null && avatar.isValid ? "valid" : "still invalid")
            + ", piano clip "
            + (clip != null && clip.isHumanMotion ? "humanoid" : "not humanoid")
            + ". Warrok " + ExternalTexture("Assets/Art/WarrokTextures")
            + ", pirate " + ExternalTexture("Assets/Art/PirateTextures") + ".";
    }

    static string ExternalTexture(string folder)
    {
        if (!Directory.Exists(folder))
            return "missing";
        foreach (string file in Directory.GetFiles(folder, "*.mat"))
        {
            string relative = ToAssetPath(file);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(relative);
            if (material == null)
                return "material not loaded";
            return material.name + "=" + (material.mainTexture != null ? material.mainTexture.name : "none");
        }

        return "no material";
    }

    static string TextureNames(string path)
    {
        var names = new List<string>();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            Material material = asset as Material;
            if (material == null)
                continue;
            Texture texture = material.mainTexture;
            names.Add(material.name + "=" + (texture != null ? texture.name : "none"));
        }

        return names.Count == 0 ? "no materials" : string.Join(", ", names.ToArray());
    }

    static bool HasTexture(string path)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            Material material = asset as Material;
            if (material != null && material.mainTexture != null)
                return true;
        }

        return false;
    }

    static bool AvatarOk(string path)
    {
        Avatar avatar = LoadAvatar(path);
        return avatar != null && avatar.isValid && avatar.isHuman;
    }

    static Avatar LoadAvatar(string path)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            Avatar avatar = asset as Avatar;
            if (avatar != null)
                return avatar;
        }

        return null;
    }

    static AnimationClip LoadClip(string path)
    {
        AnimationClip best = null;
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            AnimationClip clip = asset as AnimationClip;
            if (clip == null || clip.name.StartsWith("__preview"))
                continue;
            best = clip;
        }

        return best;
    }

    static string FindBone(Transform root, string suffix)
    {
        foreach (Transform bone in root.GetComponentsInChildren<Transform>(true))
        {
            string name = bone.name;
            int colon = name.LastIndexOf(':');
            string shortName = colon >= 0 ? name.Substring(colon + 1) : name;
            if (shortName == suffix)
                return bone.name;
        }

        return null;
    }

    static Bounds BoundsOf(GameObject model)
    {
        Bounds bounds = new Bounds(model.transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            if (!any)
            {
                bounds = renderer.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }
}
