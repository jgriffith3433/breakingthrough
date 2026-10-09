using UnityEditor;

// IAP 5 asks to install the Microsoft GDK so a Windows build can use the Xbox store.
// This project does not ship on Xbox. Saving the "don't install" choice keeps that prompt off.
[InitializeOnLoad]
static class DisableXboxGdkPrompt
{
    const string InstallDecisionKey = "IAP_GDKPackageInstall";

    static DisableXboxGdkPrompt()
    {
        EditorPrefs.SetBool(InstallDecisionKey, true);
    }
}
