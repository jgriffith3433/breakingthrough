using System.Collections.Generic;
using UnityEngine;

// Every gameplay read of the keyboard and mouse goes through here, so the playtest bot can press the same
// keys a player does. With no bot pressing anything it is exactly UnityEngine.Input. The touch controls
// press keys here too, on their own channel (Pad...), so the bot letting go never lifts a finger.
public static class PlayInput {
    static readonly HashSet<KeyCode> held = new HashSet<KeyCode>();
    static readonly Dictionary<KeyCode, int> downFrame = new Dictionary<KeyCode, int>();
    static readonly Dictionary<string, float> axes = new Dictionary<string, float>();
    static int anyDownFrame = -1;

    static readonly HashSet<KeyCode> padHeld = new HashSet<KeyCode>();
    static readonly Dictionary<KeyCode, int> padDownFrame = new Dictionary<KeyCode, int>();
    static readonly Dictionary<string, float> padAxes = new Dictionary<string, float>();
    static int padAnyFrame = -1;
    static Vector3 padPointer;
    static bool padPointing;

    public static bool GetKey(KeyCode key) {
        return Input.GetKey(key) || held.Contains(key) || padHeld.Contains(key);
    }

    public static bool GetKeyDown(KeyCode key) {
        return Input.GetKeyDown(key)
            || (downFrame.TryGetValue(key, out int frame) && frame == Time.frameCount)
            || (padDownFrame.TryGetValue(key, out int padFrame) && padFrame == Time.frameCount);
    }

    public static bool GetMouseButton(int button) {
        return Input.GetMouseButton(button) || held.Contains(KeyCode.Mouse0 + button) || padHeld.Contains(KeyCode.Mouse0 + button);
    }

    public static bool GetMouseButtonDown(int button) {
        return Input.GetMouseButtonDown(button) || GetKeyDown(KeyCode.Mouse0 + button);
    }

    public static float GetAxis(string name) {
        float best = Input.GetAxis(name);
        if (axes.TryGetValue(name, out float bot) && Mathf.Abs(bot) > Mathf.Abs(best)) {
            best = bot;
        }
        if (padAxes.TryGetValue(name, out float pad) && Mathf.Abs(pad) > Mathf.Abs(best)) {
            best = pad;
        }
        return best;
    }

    public static bool AnyKeyDown {
        get { return Input.anyKeyDown || anyDownFrame == Time.frameCount || padAnyFrame == Time.frameCount; }
    }

    // Where a click lands: the mouse, or the last tap of the touch controls.
    public static Vector3 MousePosition {
        get { return padPointing ? padPointer : Input.mousePosition; }
    }

    // The bot's side. A press counts as "down" on the next frame, whatever the script order.
    public static void Hold(KeyCode key, bool down) {
        if (down && held.Add(key)) {
            downFrame[key] = Time.frameCount + 1;
            anyDownFrame = Time.frameCount + 1;
        } else if (!down) {
            held.Remove(key);
        }
    }

    public static void Tap(KeyCode key) {
        downFrame[key] = Time.frameCount + 1;
        anyDownFrame = Time.frameCount + 1;
    }

    public static void Axis(string name, float value) {
        axes[name] = Mathf.Clamp(value, -1f, 1f);
    }

    public static void Release() {
        held.Clear();
        downFrame.Clear();
        axes.Clear();
        anyDownFrame = -1;
    }

    // The touch controls' side, the same rules.
    public static void PadHold(KeyCode key, bool down) {
        if (down && padHeld.Add(key)) {
            padDownFrame[key] = Time.frameCount + 1;
        } else if (!down) {
            padHeld.Remove(key);
        }
    }

    public static void PadTap(KeyCode key) {
        padDownFrame[key] = Time.frameCount + 1;
    }

    public static void PadAny() {
        padAnyFrame = Time.frameCount + 1;
    }

    // Mouse X and Y take a per-frame look delta; Horizontal and Vertical the stick, -1..1.
    public static void PadAxis(string name, float value) {
        padAxes[name] = value;
    }

    public static void PadPoint(Vector3 screen) {
        padPointer = screen;
        padPointing = true;
    }

    // Back to the mouse for where clicks land.
    public static void PadStopPointing() {
        padPointing = false;
    }

    public static void PadRelease() {
        padHeld.Clear();
        padDownFrame.Clear();
        padAxes.Clear();
        padAnyFrame = -1;
        padPointing = false;
    }
}
