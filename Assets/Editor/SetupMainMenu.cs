using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Builds the menu scene once and puts it first in the build list.
[InitializeOnLoad]
static class SetupMainMenu {
    const string ScenePath = "Assets/MainMenu.unity";
    static int tries;

    static SetupMainMenu() {
        EditorApplication.update += Tick;
    }

    static void Tick() {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) {
            return;
        }
        if (tries++ > 12) {
            EditorApplication.update -= Tick;
            return;
        }

        bool hasScene = File.Exists(ScenePath);
        bool listed = BuildHasMenu();
        if (hasScene && listed) {
            EditorApplication.update -= Tick;
            return;
        }

        if (!hasScene) {
            CreateScene();
            AssetDatabase.Refresh();
        }
        EnsureBuild();
        if (File.Exists(ScenePath) && BuildHasMenu()) {
            EditorApplication.update -= Tick;
            Debug.Log("Main menu is ready. It is the first scene in the build.");
        }
    }

    static void CreateScene() {
        Scene previous = EditorSceneManager.GetActiveScene();
        Scene menu = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
        foreach (GameObject root in menu.GetRootGameObjects()) {
            Camera camera = root.GetComponent<Camera>();
            if (camera == null) {
                continue;
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
        }

        var host = new GameObject("Main Menu");
        host.AddComponent<MainMenu>();
        EditorSceneManager.MoveGameObjectToScene(host, menu);
        EditorSceneManager.SaveScene(menu, ScenePath);
        EditorSceneManager.CloseScene(menu, true);
        if (previous.IsValid() && previous.isLoaded) {
            EditorSceneManager.SetActiveScene(previous);
        }
    }

    static void EnsureBuild() {
        var scenes = new EditorBuildSettingsScene[2];
        scenes[0] = new EditorBuildSettingsScene(ScenePath, true);
        scenes[1] = new EditorBuildSettingsScene("Assets/scene2.unity", true);
        EditorBuildSettings.scenes = scenes;
        AssetDatabase.SaveAssets();
    }

    static bool BuildHasMenu() {
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes) {
            if (scene.path == ScenePath && scene.enabled) {
                return true;
            }
        }
        return false;
    }
}
