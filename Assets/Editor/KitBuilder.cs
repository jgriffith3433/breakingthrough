using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Kit: bakes the Model Kit (Docs/ModelKit.md) into Assets/Kit/<palette>/ as real assets: one mesh .asset and one
// .prefab per piece, plus the shared textures and materials. The runtime builds the same pieces from KitCatalog, so the
// baked files are for editor use (dragging a prefab into a scene) and for the validation menu; nothing in the game loads them.
// Validate Kit prints every piece's triangles and texture memory and fails on a primitive mesh or a broken budget.
// Batch: Unity -batchmode -quit -projectPath . -executeMethod KitBuilder.BuildAll   (and KitBuilder.ValidateBatch)
[InitializeOnLoad]
public static class KitBuilder {
    const string Root = "Assets/Kit";
    static int tries;

    static KitBuilder() {
        EditorApplication.delayCall += AutoBake;
    }

    static void AutoBake() {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) {
            if (++tries < 8) {
                EditorApplication.delayCall += AutoBake;
            }
            return;
        }
        if (!File.Exists(Root + "/street/st_shopfront.prefab")) {
            try {
                BuildAll();
            } catch (System.Exception e) {
                Debug.LogWarning("[Kit] automatic bake failed (Tools > Kit > Build Kit Assets runs it again): " + e.Message);
            }
        }
    }

    static void Folder(string parent, string name) {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    static T Save<T>(T obj, string path) where T : Object {
        if (File.Exists(path)) {
            AssetDatabase.DeleteAsset(path);
        }
        AssetDatabase.CreateAsset(obj, path);
        return obj;
    }

    [MenuItem("Tools/Kit/Build Kit Assets")]
    public static void BuildAll() {
        KitCatalog.Init();
        Folder("Assets", "Kit");
        Folder(Root, "Textures");
        Folder(Root, "Materials");
        foreach (string set in KitTextures.Sets) {
            string a = Root + "/Textures/Kit_" + set + "_albedo.asset", n = Root + "/Textures/Kit_" + set + "_normal.asset";
            if (!AssetDatabase.Contains(KitTextures.Albedo(set))) {
                Save(KitTextures.Albedo(set), a);
            }
            if (!AssetDatabase.Contains(KitTextures.Normal(set))) {
                Save(KitTextures.Normal(set), n);
            }
        }
        int count = 0;
        foreach (KitDef d in KitCatalog.All) {
            Folder(Root, d.Palette);
            Mesh mesh = KitCatalog.MeshOf(d.Id);
            if (!AssetDatabase.Contains(mesh)) {
                Save(mesh, Root + "/" + d.Palette + "/" + d.Id + ".asset");
            }
            foreach (Material mat in KitCatalog.MaterialsOf(d, mesh)) {
                if (!AssetDatabase.Contains(mat)) {
                    Save(mat, Root + "/Materials/" + mat.name + ".mat");
                }
            }
            GameObject go = KitCatalog.Make(d.Id);
            PrefabUtility.SaveAsPrefabAsset(go, Root + "/" + d.Palette + "/" + d.Id + ".prefab");
            Object.DestroyImmediate(go);
            count++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Kit] baked " + count + " pieces into " + Root);
    }

    [MenuItem("Tools/Kit/Validate Kit")]
    public static void Validate() {
        KitReport report = KitValidation.Run();
        Debug.Log("[Kit] validation report\n" + report.Text());
        if (!report.Ok) {
            Debug.LogError("[Kit] validation FAILED: " + report.Failures.Count + " problem(s)");
        }
    }

    public static void ValidateBatch() {
        KitReport report = KitValidation.Run();
        Debug.Log("[Kit] validation report\n" + report.Text());
        EditorApplication.Exit(report.Ok ? 0 : 1);
    }
}
