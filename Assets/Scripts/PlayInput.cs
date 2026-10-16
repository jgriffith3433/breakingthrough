using System.Collections.Generic;
using UnityEngine;

// Every gameplay read of the keyboard and mouse goes through here, so the playtest bot can press the same
// keys a player does. With no bot pressing anything it is exactly UnityEngine.Input.
public static class PlayInput {
    static readonly HashSet<KeyCode> held = new HashSet<KeyCode>();
    static readonly Dictionary<KeyCode, int> downFrame = new Dictionary<KeyCode, int>();
    static readonly Dictionary<string, float> axes = new Dictionary<string, float>();
    static int anyDownFrame = -1;

    // The on-screen controls (TouchControls) press keys and move the axes through their own tables.
    static readonly HashSet<KeyCode> touchHeld = new HashSet<KeyCode>();
    static readonly Dictionary<KeyCode, int> touchDown = new Dictionary<KeyCode, int>();
    static float touchHori, touchVert, touchLookX, touchLookY;

    public static bool GetKey(KeyCode key) {
        return Input.GetKey(key) || held.Contains(key) || touchHeld.Contains(key);
    }

    public static bool GetKeyDown(KeyCode key) {
        return Input.GetKeyDown(key)
            || (downFrame.TryGetValue(key, out int frame) && frame == Time.frameCount)
            || (touchDown.TryGetValue(key, out int touchFrame) && touchFrame == Time.frameCount);
    }

    // With the touch layer up, a finger's simulated mouse press is not a shot: Fire is the only trigger.
    public static bool GetMouseButton(int button) {
        bool real = Input.GetMouseButton(button) && !(button == 0 && Input.touchCount > 0);
        return real || held.Contains(KeyCode.Mouse0 + button) || touchHeld.Contains(KeyCode.Mouse0 + button);
    }

    public static bool GetMouseButtonDown(int button) {
        return Input.GetMouseButtonDown(button) || GetKeyDown(KeyCode.Mouse0 + button);
    }

    public static float GetAxis(string name) {
        float real = Input.GetAxis(name);
        float touch = 0f;
        switch (name) {
            case "Horizontal": touch = touchHori; break;
            case "Vertical": touch = touchVert; break;
            case "Mouse X": touch = touchLookX; break;
            case "Mouse Y": touch = touchLookY; break;
        }
        if (Input.touchCount > 0 && name.StartsWith("Mouse")) {
            real = 0f;
        }
        if (Mathf.Abs(touch) > Mathf.Abs(real)) {
            real = touch;
        }
        return axes.TryGetValue(name, out float bot) && Mathf.Abs(bot) > Mathf.Abs(real) ? bot : real;
    }

    public static bool AnyKeyDown {
        get { return Input.anyKeyDown || anyDownFrame == Time.frameCount || touchDown.ContainsValue(Time.frameCount); }
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

    // The touch layer's side.
    public static void TouchHold(KeyCode key, bool down) {
        if (down && touchHeld.Add(key)) {
            touchDown[key] = Time.frameCount + 1;
        } else if (!down) {
            touchHeld.Remove(key);
        }
    }

    public static void TouchTap(KeyCode key) {
        touchDown[key] = Time.frameCount + 1;
    }

    public static void TouchMove(float horizontal, float vertical) {
        touchHori = Mathf.Clamp(horizontal, -1f, 1f);
        touchVert = Mathf.Clamp(vertical, -1f, 1f);
    }

    // One frame's drag, already in mouse-axis units.
    public static void TouchLook(float x, float y) {
        touchLookX = x;
        touchLookY = y;
    }

    public static void Release() {
        held.Clear();
        downFrame.Clear();
        axes.Clear();
        anyDownFrame = -1;
    }
}
