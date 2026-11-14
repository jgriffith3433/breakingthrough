using System.Runtime.InteropServices;
using UnityEngine;

// The page going away and coming back (a text, a locked phone, another tab). The page's own events call in through
// HawkesPageLifecycle.jslib (visibilitychange, pagehide; no polling). A hide pauses through PauseMenu, writes the case
// files and the shop save once and syncs the browser's file system once; a show only logs, because Resume is the
// player's to press.
public class PageLifecycle : MonoBehaviour {
    const string ObjectName = "PageLifecycle";

    public static int Hides { get; private set; }
    public static int Shows { get; private set; }
    // Saves written by the last hide (the playtest reads these as save.writes).
    public static int LastHideWrites { get; private set; }
    public static bool LastHidePaused { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void HawkesPageLifecycleInit(string objectName);
    [DllImport("__Internal")]
    static extern void HawkesFsSync();
#else
    static void HawkesPageLifecycleInit(string objectName) { }
    static void HawkesFsSync() { }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        if (GameObject.Find(ObjectName) != null) {
            return;
        }
        var go = new GameObject(ObjectName);
        DontDestroyOnLoad(go);
        go.AddComponent<PageLifecycle>();
        HawkesPageLifecycleInit(ObjectName);
    }

    // Called by the page (SendMessage) and by the playtest.
    public void OnPageHidden(string why) {
        Hide(why);
    }

    public void OnPageShown(string why) {
        Show(why);
    }

    public static void Hide(string why) {
        Hides++;
        int before = CampaignSave.Writes + MetaProgress.Writes;
        LastHidePaused = false;
        if (!PauseMenu.IsOpen) {
            PauseMenu.Open();
        }
        LastHidePaused = PauseMenu.IsOpen;
        CampaignSave.Flush();
        MetaProgress.Flush();
        HawkesFsSync();
        LastHideWrites = CampaignSave.Writes + MetaProgress.Writes - before;
        Debug.Log("[PageLifecycle] hidden (" + why + "): paused=" + LastHidePaused + " save.writes=" + LastHideWrites);
    }

    public static void Show(string why) {
        Shows++;
        Debug.Log("[PageLifecycle] shown (" + why + "): pause menu " + (PauseMenu.IsOpen ? "up" : "not up"));
    }
}
