using UnityEditor;
using UnityEngine;

// Stopping play while a level-up card or the loss screen has the game paused can leave the saved
// time scale at 0, and the next Play starts frozen. Put it back whenever play mode ends.
[InitializeOnLoad]
static class ResetTimeScale
{
    static ResetTimeScale()
    {
        EditorApplication.playModeStateChanged -= OnChanged;
        EditorApplication.playModeStateChanged += OnChanged;
    }

    static void OnChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode && Time.timeScale != 1f)
            Time.timeScale = 1f;
    }
}
