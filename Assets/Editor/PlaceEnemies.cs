using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Waves spawn their own turkeys. The old row on the knoll is cleared once.
[InitializeOnLoad]
static class PlaceEnemies
{
    const string RootName = "Enemies";

    static PlaceEnemies()
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
        GameObject existing = GameObject.Find(RootName);
        if (existing == null)
            return;

        var scene = existing.scene;
        Object.DestroyImmediate(existing);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Cleared the old turkeys. Waves spawn when you press Play.");
    }
}
