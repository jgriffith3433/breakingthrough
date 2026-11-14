using System.Collections.Generic;
using UnityEngine;

// A cutscene, boss entrance, act change or ending is a film: the combat HUD and the touch pads fade out the moment one
// takes the camera and come back when control returns. One self-made component owns it, so no HUD class needs its own
// check. It switches off the Canvas (not the object) of each combat HUD canvas listed in Names, and puts back only the
// ones it switched off. Subtitles, the cinematic's own cards, the pause menu and the run-end sheet are not in the list.
public class CinematicHud : MonoBehaviour {
    // Canvas object names of the combat HUD (see RunHud, HudPaper, Musician, AbilityList, WaveRoster, Fort, ControlsHelp,
    // SliceCallouts, LevelKit's LevelHud, ChapterRun, UpgradeLook).
    static readonly string[] Names = {
        "Run Canvas", "Paper Canvas", "Musician Health", "Abilities", "Roster", "Build Bar", "Lesson",
        "Slice Callouts", "Level HUD", "Chapter Run Canvas", "Loadout", "Callout"
    };

    public static CinematicHud Instance { get; private set; }
    public static bool Hiding { get; private set; }
    // Frames in which a cinematic was up and a HUD canvas or touch pad was still drawn (the playtest wants 0).
    public static int HudVisibleFrames { get; private set; }
    public static int CinematicFrames { get; private set; }
    // Seconds from a cinematic ending to the touch pads being drawn again (last one measured).
    public static float PadsBackSeconds { get; private set; }

    readonly List<Canvas> held = new List<Canvas>();
    readonly List<Canvas> known = new List<Canvas>();
    float nextScan;
    bool wasHiding;
    float endedAt = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() {
        if (Instance != null) {
            return;
        }
        var go = new GameObject("Cinematic HUD");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<CinematicHud>();
    }

    public static void ResetCounters() {
        HudVisibleFrames = 0;
        CinematicFrames = 0;
        PadsBackSeconds = 0f;
    }

    // The same test the touch pads use, so both go together.
    public static bool Wanted() {
        return Cutscene.IsOpen || BossSetPiece.EntranceUp;
    }

    void LateUpdate() {
        bool hide = Wanted();
        float now = Time.unscaledTime;
        if (hide || held.Count > 0 || now >= nextScan) {
            if (now >= nextScan) {
                nextScan = now + 0.2f;
                Rescan();
            }
        }
        if (hide) {
            for (int i = 0; i < known.Count; i++) {
                Canvas canvas = known[i];
                if (canvas != null && canvas.enabled) {
                    canvas.enabled = false;
                    held.Add(canvas);
                }
            }
            Verify();
        } else if (held.Count > 0) {
            for (int i = 0; i < held.Count; i++) {
                if (held[i] != null) {
                    held[i].enabled = true;
                }
            }
            held.Clear();
        }
        if (wasHiding && !hide) {
            endedAt = now;
        }
        if (endedAt >= 0f && PadsShown()) {
            PadsBackSeconds = now - endedAt;
            endedAt = -1f;
        }
        wasHiding = hide;
        Hiding = hide;
    }

    void Rescan() {
        known.Clear();
        Canvas[] all = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++) {
            if (all[i] != null && all[i].isRootCanvas && System.Array.IndexOf(Names, all[i].gameObject.name) >= 0) {
                known.Add(all[i]);
            }
        }
        // Canvases we hold off are inactive-by-enabled only, so they are still found; keep them known.
        for (int i = 0; i < held.Count; i++) {
            if (held[i] != null && !known.Contains(held[i])) {
                known.Add(held[i]);
            }
        }
    }

    // After the pass: does anything of the HUD still draw?
    static void Verify() {
        CinematicFrames++;
        Instance.CountVisible();
    }

    void CountVisible() {
        bool visible = PadsShown();
        for (int i = 0; i < known.Count && !visible; i++) {
            if (known[i] != null && known[i].enabled && known[i].gameObject.activeInHierarchy) {
                visible = true;
            }
        }
        if (visible) {
            HudVisibleFrames++;
        }
    }

    static bool PadsShown() {
        TouchControls pads = FindFirstObjectByType<TouchControls>();
        return pads != null && pads.PadsDrawn;
    }
}

// Where debug and developer-style UI is allowed: nowhere in a public build. It is on in the editor, in a development
// build, or when the page address carries ?devui=1. Everything that reads like a programmer's overlay (raw XP numbers,
// key labels) checks On, so the flag is the only place to turn it back on.
public static class DevUi {
    public static bool On {
        get {
#if UNITY_EDITOR
            return true;
#else
            if (Playtest.Running) {
                return false;   // a playtest plays the public build: the shown count must be what a player sees
            }
            string url = Application.absoluteURL ?? "";
            return Debug.isDebugBuild && url.IndexOf("devui=1", System.StringComparison.OrdinalIgnoreCase) >= 0;
#endif
        }
    }

    // How many developer-style elements are drawn right now (ftue.devUiVisible). 0 whenever the flag is off.
    public static int Visible { get; set; }
}
