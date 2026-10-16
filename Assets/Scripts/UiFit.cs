using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

public enum UiMode {
    Desktop,
    Wide,
    Tall
}

// Which layout the screens use. Desktop is the 1920x1080 layout every canvas was drawn for, untouched. Phones and
// tablets get their own: Wide (landscape: under 500 CSS px tall, or a touch tablet) and Tall (portrait), on a
// canvas at least 660 units across its short side, so text keeps a readable size and a button 88 units tall is
// about 50 px on a phone and more on a tablet.
public static class UiFit {
    const float CompactBelow = 500f;
    const float TabletBelow = 900f;

    static bool forcedTouch;
    static bool sawTouch;
    static int stampWidth;
    static int stampHeight;
    static int stamp;

    // Changes whenever the screen does (a turn, a resize, a phone's toolbar sliding away): layouts redo then.
    public static int Stamp {
        get {
            if (Screen.width != stampWidth || Screen.height != stampHeight) {
                stampWidth = Screen.width;
                stampHeight = Screen.height;
                stamp++;
            }
            return stamp;
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern float HawkesPixelRatio();

    [DllImport("__Internal")]
    static extern int HawkesCoarsePointer();
#else
    static float HawkesPixelRatio() {
        return 1f;
    }

    static int HawkesCoarsePointer() {
        return 0;
    }
#endif

    public static UiMode Mode {
        get {
            int w = Screen.width;
            int h = Screen.height;
            if (w <= 0 || h <= 0) {
                return UiMode.Desktop;
            }
            if (h > w) {
                return UiMode.Tall;
            }
            float tall = h / PixelRatio;
            bool tablet = tall < TabletBelow && (Application.isMobilePlatform || HawkesCoarsePointer() != 0);
            return tall < CompactBelow || tablet ? UiMode.Wide : UiMode.Desktop;
        }
    }

    // The compact canvas's short side in units: 660 on a phone, about 0.9 CSS px a unit on a tablet.
    public static float Short {
        get {
            float shortSide = Mathf.Min(Screen.width, Screen.height) / PixelRatio;
            return Mathf.Max(660f, shortSide * 1.1f);
        }
    }

    // Device pixels per CSS pixel. Screen.width counts device pixels in the browser.
    public static float PixelRatio {
        get { return Mathf.Clamp(HawkesPixelRatio(), 1f, 4f); }
    }

    public static bool Compact {
        get { return Mode != UiMode.Desktop; }
    }

    // The canvas size in units for a compact mode (Apply's reference resolution, stretched to the screen).
    public static Vector2 Size {
        get {
            float w = Mathf.Max(1, Screen.width);
            float h = Mathf.Max(1, Screen.height);
            switch (Mode) {
                case UiMode.Tall:
                    return new Vector2(Short, Short * h / w);
                case UiMode.Wide:
                    return new Vector2(Short * w / h, Short);
                default:
                    return new Vector2(1920f, 1920f * h / w);
            }
        }
    }

    // Phones and tablets, a finger seen on the screen, or a playtest that asks for touch.
    public static bool Touch {
        get { return forcedTouch || sawTouch || Application.isMobilePlatform || HawkesCoarsePointer() != 0; }
    }

    public static void ForceTouch(bool on) {
        forcedTouch = on;
    }

    public static void Notice() {
        if (!sawTouch && Input.touchCount > 0) {
            sawTouch = true;
        }
    }

    // desktopMatch is the canvas's own matchWidthOrHeight, kept as it was on desktop.
    public static void Apply(CanvasScaler scaler, float desktopMatch = 0f) {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        switch (Mode) {
            case UiMode.Tall:
                scaler.referenceResolution = new Vector2(Short, Short * 16f / 9f);
                scaler.matchWidthOrHeight = 0f;
                break;
            case UiMode.Wide:
                scaler.referenceResolution = new Vector2(Short * 16f / 9f, Short);
                scaler.matchWidthOrHeight = 1f;
                break;
            default:
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = desktopMatch;
                break;
        }
    }
}
