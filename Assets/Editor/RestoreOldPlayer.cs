using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Removes the stand-in elf and turns the original hero mesh back on.
[InitializeOnLoad]
static class RestoreOldPlayer
{
    static RestoreOldPlayer()
    {
        EditorApplication.delayCall += Restore;
    }

    static void Restore()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += Restore;
            return;
        }

        if (EditorSceneManager.GetActiveScene().name != "scene2")
            return;

        GameObject elf = GameObject.Find("ElfArcher");
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (elf == null && player == null)
            return;

        bool changed = false;
        if (player != null)
        {
            foreach (Animator animator in player.GetComponentsInChildren<Animator>(true))
            {
                if (animator.gameObject == player)
                    continue;
                foreach (Renderer renderer in animator.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.enabled)
                        continue;
                    renderer.enabled = true;
                    changed = true;
                }
            }
        }

        if (elf != null)
        {
            Object.DestroyImmediate(elf);
            changed = true;
        }

        if (!changed)
            return;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("The original player character is visible again.");
    }
}
