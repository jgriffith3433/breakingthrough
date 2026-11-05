using UnityEngine;

// Comfort options for the campaign's scenes: subtitle size, high contrast subtitles and reduced motion for camera moves.
// Saved as JSON { size, contrast, calm } under "hawkes.access" (PlayerPrefs; tooling and tests in a sandbox write nothing).
// Cutscene reads them: the dialogue box and the reel subtitles scale and darken their backing, and Calm turns off the
// cutscene camera sweeps, the reels' Ken Burns pan and zoom, parallax and the cross-fade slide. Arcade has no scenes, so
// none of it reaches it. SettingsPanel has the three buttons; Stamp changes whenever one is pressed so open screens redo layout.
public static class ComfortOptions {
    const string Key = "hawkes.access";

    [System.Serializable]
    class Saved {
        public int size;        // 0 Normal, 1 Large, 2 Largest
        public bool contrast;
        public bool calm;
    }

    public const int SizeMax = 2;
    static Saved now = new Saved();
    static bool loaded;

    public static int Stamp { get; private set; }

    public static int SubtitleSize {
        get { Load(); return now.size; }
    }

    public static float SubtitleScale {
        get { Load(); return now.size == 1 ? 1.25f : now.size >= 2 ? 1.5f : 1f; }
    }

    public static bool HighContrast {
        get { Load(); return now.contrast; }
    }

    public static bool ReducedMotion {
        get { Load(); return now.calm; }
    }

    public static string SizeName(int size) {
        return size <= 0 ? "Normal" : size == 1 ? "Large" : "Largest";
    }

    public static void CycleSize() {
        Load();
        now.size = now.size >= SizeMax ? 0 : now.size + 1;
        Changed();
    }

    public static void SetSize(int size) {
        Load();
        now.size = Mathf.Clamp(size, 0, SizeMax);
        Changed();
    }

    public static void SetContrast(bool on) {
        Load();
        now.contrast = on;
        Changed();
    }

    public static void SetReducedMotion(bool on) {
        Load();
        now.calm = on;
        Changed();
    }

    // A gamepad is plugged in: the scenes' hints name its buttons.
    public static bool PadConnected {
        get {
            string[] names = Input.GetJoystickNames();
            for (int i = 0; i < names.Length; i++) {
                if (!string.IsNullOrEmpty(names[i])) {
                    return true;
                }
            }
            return false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
        loaded = false;
        now = new Saved();
        Stamp = 0;
    }

    static void Changed() {
        Stamp++;
        if (!MetaProgress.Sandbox) {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(now));
            PlayerPrefs.Save();
        }
    }

    static void Load() {
        if (loaded) {
            return;
        }
        loaded = true;
        string json = PlayerPrefs.GetString(Key, "");
        if (string.IsNullOrEmpty(json)) {
            return;
        }
        try {
            Saved read = JsonUtility.FromJson<Saved>(json);
            if (read != null) {
                now.size = Mathf.Clamp(read.size, 0, SizeMax);
                now.contrast = read.contrast;
                now.calm = read.calm;
            }
        } catch (System.Exception) {
            now = new Saved();
        }
    }
}
