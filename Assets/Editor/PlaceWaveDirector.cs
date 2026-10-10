using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// WaveDirector lives on an object in scene2. This adds that object if the scene was saved without it.
[InitializeOnLoad]
static class PlaceWaveDirector {
    const string ScenePath = "Assets/scene2.unity";

    static PlaceWaveDirector() {
        EditorApplication.delayCall += Run;
    }

    static void Run() {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) {
            EditorApplication.delayCall += Run;
            return;
        }

        Scene scene = default;
        bool opened = false;
        for (int i = 0; i < SceneManager.sceneCount; i++) {
            Scene loaded = EditorSceneManager.GetSceneAt(i);
            if (loaded.path == ScenePath) {
                scene = loaded;
            }
        }
        if (!scene.IsValid()) {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            opened = true;
        }

        bool found = false;
        foreach (GameObject root in scene.GetRootGameObjects()) {
            if (root.GetComponent<WaveDirector>() != null) {
                found = true;
            }
        }
        if (!found) {
            var go = new GameObject("Wave Director");
            EditorSceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<WaveDirector>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Wave Director is saved in scene2.");
        }
        if (opened) {
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
