using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Puts a few lock-on trees in front of the player so the bow can be aimed and tested.
[InitializeOnLoad]
static class PlacePracticeTargets
{
    const string RootName = "PracticeTargets";

    static PlacePracticeTargets()
    {
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (EditorSceneManager.GetActiveScene().name != "scene2")
            return;

        EditorApplication.update -= Tick;
        if (GameObject.Find(RootName) != null)
            return;

        Mesh oak = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Knoll/Oak.asset");
        Mesh pine = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Art/Knoll/Pine.asset");
        Material lit = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Knoll/KnollLit.mat");
        if (oak == null || pine == null || lit == null)
        {
            Debug.LogWarning("Practice targets are waiting for the knoll meshes.");
            EditorApplication.update += Tick;
            return;
        }

        var root = new GameObject(RootName);
        Place(root.transform, "Target", oak, lit, new Vector3(0f, 0f, 3.2f), 0.85f);
        Place(root.transform, "Target", pine, lit, new Vector3(1.5f, 0f, 2.8f), 0.75f);
        Place(root.transform, "Target", oak, lit, new Vector3(-1.6f, 0f, 3.5f), 0.95f);

        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
        Debug.Log("Practice targets are in front of the player. Press Space to lock on, and click to shoot them.");
    }

    static void Place(Transform parent, string name, Mesh mesh, Material material, Vector3 flat, float scale)
    {
        var tree = new GameObject(name);
        tree.transform.SetParent(parent, false);
        tree.tag = "CanLockOn";
        tree.transform.localScale = Vector3.one * scale;
        tree.transform.rotation = Quaternion.Euler(0f, flat.x * 40f, 0f);
        tree.transform.position = flat;
        tree.AddComponent<MeshFilter>().sharedMesh = mesh;
        tree.AddComponent<MeshRenderer>().sharedMaterial = material;
        CapsuleCollider capsule = tree.AddComponent<CapsuleCollider>();
        capsule.direction = 1;
        capsule.center = mesh.bounds.center;
        capsule.height = Mathf.Max(mesh.bounds.size.y, 0.2f);
        capsule.radius = Mathf.Max(mesh.bounds.extents.x, mesh.bounds.extents.z);

        Vector3 position = tree.transform.position;
        float lift = -mesh.bounds.min.y * scale;
        position.y = DressGrassyKnoll.TerrainHeight(position.x, position.z) + lift;
        tree.transform.position = position;
    }
}
