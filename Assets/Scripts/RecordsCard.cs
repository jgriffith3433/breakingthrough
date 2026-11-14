using UnityEngine;

// The page's Records card ("Opening your file...") must clear within seconds once Unity runs. Nothing here waits on a save
// read, a file-system sync or a download: the card is taken down the moment the first scene is live (MainMenu.Start, or
// the run's scene when a link skips the menu), and the page script also drops it 5 s after Unity's first frame, so a
// slow step can never hold it (hawkes-load-quiet.js). Reports records.openSeconds once, from the page opening.
public static class RecordsCard {
    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void HawkesLoaderReady();

    public const float LimitSeconds = 5f;
    static bool done;
    public static float OpenSeconds { get; private set; } = -1f;

    public static void Opened(string where) {
        if (done) {
            return;
        }
        done = true;
        OpenSeconds = Time.realtimeSinceStartup;
#if UNITY_WEBGL && !UNITY_EDITOR
        try { HawkesLoaderReady(); } catch (System.Exception) { }
#endif
        Debug.Log("[Records] card cleared at " + OpenSeconds.ToString("0.0") + " s (" + where + ")");
        Playtest.RecordsOpen(OpenSeconds, where);
    }
}
