using System.IO;
using UnityEditor;
using UnityEngine;

// Puts the bow and arrow textures on the materials those models already reference.
[InitializeOnLoad]
static class EquipBow
{
    const string BowPath = "Assets/Art/BowAndArrow/Bow.FBX";
    const string ArrowPath = "Assets/Art/BowAndArrow/Arrow.fbx";
    const string BowFolder = "Assets/Art/BowAndArrow/BowTextures";
    const string ArrowFolder = "Assets/Art/BowAndArrow/ArrowTextures";
    const string PackedPath = ArrowFolder + "/Arrow_MetallicSmooth.png";

    static int _passes;

    static EquipBow()
    {
        Schedule();
    }

    static void Schedule()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        EditorApplication.update -= Tick;
        Run();
    }

    static void Run()
    {

        if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name != "scene2")
            return;

        if (_passes > 8)
        {
            Debug.LogWarning("Bow texture assignment stopped after several attempts.");
            return;
        }

        if (AssignTextures())
        {
            _passes++;
            Schedule();
            return;
        }

        HookPlayer();
    }

    static bool AssignTextures()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BowPath) == null
            || AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPath) == null)
        {
            Debug.LogWarning("Bow and arrow models are not imported yet.");
            return true;
        }

        if (EnsureLinear(ArrowFolder + "/Arrow_Metallic.png") || EnsureLinear(ArrowFolder + "/Arrow_Roughness.png"))
            return true;

        if (!File.Exists(PackedPath))
        {
            if (!WriteMetallicSmooth())
                return true;
            AssetDatabase.ImportAsset(PackedPath, ImportAssetOptions.ForceUpdate);
            return true;
        }

        if (EnsureLinear(PackedPath))
            return true;

        bool changed = false;
        changed |= EnsureBowMaterials();
        changed |= EnsureArrowMaterial();
        if (changed)
        {
            AssetDatabase.SaveAssets();
            return true;
        }

        return false;
    }

    static bool EnsureBowMaterials()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(BowPath);
        bool changed = PrepareImporter(importer);
        changed |= Remap(importer, "02 - Default", MaterialFor(BowFolder + "/02 - Default.mat", LoadTex(BowFolder, "Wood-textures-wallpaper"), 0f, 0.28f, null));
        changed |= Remap(importer, "08 - Default", MaterialFor(BowFolder + "/08 - Default.mat", LoadTex(BowFolder, "wood_006"), 0f, 0.28f, null));
        changed |= Remap(importer, "03 - Default", MaterialFor(BowFolder + "/03 - Default.mat", LoadTex(BowFolder, "old-rusty-iron"), 0.75f, 0.32f, null));
        changed |= Remap(importer, "09 - Default", MaterialFor(BowFolder + "/09 - Default.mat", LoadTex(BowFolder, "Pitted_cast_iron"), 0.8f, 0.3f, null));
        changed |= Remap(importer, "01 - Default", MaterialFor(BowFolder + "/01 - Default.mat", LoadTex(BowFolder, "brown-cloth"), 0f, 0.12f, null));
        changed |= Remap(importer, "04 - Default", MaterialFor(BowFolder + "/04 - Default.mat", LoadTex(BowFolder, "vyr"), 0.85f, 0.4f, null));
        changed |= Remap(importer, "Material #60", StringMaterial());
        if (changed)
            importer.SaveAndReimport();
        return changed;
    }

    static bool EnsureArrowMaterial()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(ArrowPath);
        bool changed = PrepareImporter(importer);
        Texture2D baseColor = LoadTex(ArrowFolder, "Arrow_BaseColor");
        Texture2D packed = AssetDatabase.LoadAssetAtPath<Texture2D>(PackedPath);
        changed |= Remap(importer, "Arrow", MaterialFor(ArrowFolder + "/Arrow.mat", baseColor, 1f, 0.45f, packed));
        if (changed)
            importer.SaveAndReimport();
        return changed;
    }

    static bool PrepareImporter(ModelImporter importer)
    {
        bool changed = false;
        if (importer.materialName != ModelImporterMaterialName.BasedOnMaterialName)
        {
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            changed = true;
        }

        if (importer.materialLocation != ModelImporterMaterialLocation.External)
        {
            importer.materialLocation = ModelImporterMaterialLocation.External;
            changed = true;
        }

        return changed;
    }

    static Material StringMaterial()
    {
        string path = BowFolder + "/Material #60.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null && mat.mainTexture == null && mat.color == new Color(0.72f, 0.68f, 0.55f))
            return mat;

        bool create = mat == null;
        if (create)
            mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.72f, 0.68f, 0.55f);
        mat.mainTexture = null;
        mat.SetFloat("_Metallic", 0.15f);
        mat.SetFloat("_Glossiness", 0.35f);
        mat.DisableKeyword("_METALLICGLOSSMAP");
        if (create)
            AssetDatabase.CreateAsset(mat, path);
        else
            EditorUtility.SetDirty(mat);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    static Material MaterialFor(string path, Texture2D albedo, float metallic, float smoothness, Texture2D metallicMap)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool ready = mat != null
            && mat.mainTexture == albedo
            && mat.GetTexture("_MetallicGlossMap") == metallicMap
            && Mathf.Approximately(mat.GetFloat("_Metallic"), metallic);
        if (ready)
            return mat;

        bool create = mat == null;
        if (create)
            mat = new Material(Shader.Find("Standard"));
        mat.color = Color.white;
        mat.mainTexture = albedo;
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Glossiness", smoothness);
        if (metallicMap != null)
        {
            mat.SetTexture("_MetallicGlossMap", metallicMap);
            mat.SetFloat("_GlossMapScale", 1f);
            mat.EnableKeyword("_METALLICGLOSSMAP");
        }
        else
        {
            mat.SetTexture("_MetallicGlossMap", null);
            mat.DisableKeyword("_METALLICGLOSSMAP");
        }

        if (create)
            AssetDatabase.CreateAsset(mat, path);
        else
            EditorUtility.SetDirty(mat);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    static bool Remap(ModelImporter importer, string materialName, Material mat)
    {
        if (mat == null)
            return false;
        var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), materialName);
        var map = importer.GetExternalObjectMap();
        if (map.TryGetValue(id, out Object existing) && existing == mat)
            return false;
        importer.AddRemap(id, mat);
        return true;
    }

    static bool EnsureLinear(string path)
    {
        var textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
        if (textureImporter == null)
            return false;
        if (!textureImporter.sRGBTexture && textureImporter.textureType == TextureImporterType.Default)
            return false;
        textureImporter.textureType = TextureImporterType.Default;
        textureImporter.sRGBTexture = false;
        textureImporter.SaveAndReimport();
        return true;
    }

    static bool WriteMetallicSmooth()
    {
        Texture2D metallic = LoadTex(ArrowFolder, "Arrow_Metallic");
        Texture2D roughness = LoadTex(ArrowFolder, "Arrow_Roughness");
        if (metallic == null || roughness == null)
            return false;

        int width = metallic.width;
        int height = metallic.height;
        Color[] metal = ReadLinear(metallic, width, height);
        Color[] rough = ReadLinear(roughness, width, height);
        var packed = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        var pixels = new Color[metal.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            float roughValue = rough[i].grayscale;
            pixels[i] = new Color(metal[i].r, metal[i].g, metal[i].b, 1f - roughValue);
        }

        packed.SetPixels(pixels);
        packed.Apply();
        File.WriteAllBytes(PackedPath, packed.EncodeToPNG());
        Object.DestroyImmediate(packed);
        return true;
    }

    static Color[] ReadLinear(Texture2D source, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(source, rt);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var copy = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        copy.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        Color[] pixels = copy.GetPixels();
        Object.DestroyImmediate(copy);
        return pixels;
    }

    static Texture2D LoadTex(string folder, string namePart)
    {
        foreach (string guid in AssetDatabase.FindAssets(namePart + " t:texture2D", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path).IndexOf(namePart, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        return null;
    }

    static void HookPlayer()
    {
        GameObject bow = AssetDatabase.LoadAssetAtPath<GameObject>(BowPath);
        GameObject arrow = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPath);
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (bow == null || arrow == null || player == null)
            return;

        LogMaterials(bow, "bow");
        LogMaterials(arrow, "arrow");

        BowEquipment equipment = player.GetComponent<BowEquipment>();
        if (equipment == null)
            equipment = player.AddComponent<BowEquipment>();

        SerializedObject so = new SerializedObject(equipment);
        SerializedProperty bowProp = so.FindProperty("bowModel");
        SerializedProperty arrowProp = so.FindProperty("arrowModel");
        if (bowProp.objectReferenceValue == bow && arrowProp.objectReferenceValue == arrow)
        {
            Debug.Log("Bow and arrow textures are on the models.");
            return;
        }

        bowProp.objectReferenceValue = bow;
        arrowProp.objectReferenceValue = arrow;
        so.ApplyModifiedPropertiesWithoutUndo();
        var scene = player.scene;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Debug.Log("Bow and arrow textures are on the models.");
    }

    static void LogMaterials(GameObject model, string label)
    {
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            foreach (Material mat in renderer.sharedMaterials)
            {
                string tex = mat != null && mat.mainTexture != null ? mat.mainTexture.name : "none";
                Debug.Log(label + " material " + (mat != null ? mat.name : "null") + " texture " + tex);
            }
        }
    }
}
