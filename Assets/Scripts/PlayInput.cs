using System.Collections.Generic;
using UnityEngine;

// Every gameplay read of the keyboard and mouse goes through here, so the playtest bot can press the same
// keys a player does. With no bot pressing anything it is exactly UnityEngine.Input.
public static class PlayInput {
    static readonly HashSet<KeyCode> held = new HashSet<KeyCode>();
    static readonly Dictionary<KeyCode, int> downFrame = new Dictionary<KeyCode, int>();
    static readonly Dictionary<string, float> axes = new Dictionary<string, float>();
    static int anyDownFrame = -1;

    public static bool GetKey(KeyCode key) {
        return Input.GetKey(key) || held.Contains(key);
    }

    public static bool GetKeyDown(KeyCode key) {
        return Input.GetKeyDown(key) || (downFrame.TryGetValue(key, out int frame) && frame == Time.frameCount);
    }

    public static bool GetMouseButton(int button) {
        return Input.GetMouseButton(button) || held.Contains(KeyCode.Mouse0 + button);
    }

    public static bool GetMouseButtonDown(int button) {
        return Input.GetMouseButtonDown(button) || GetKeyDown(KeyCode.Mouse0 + button);
    }

    public static float GetAxis(string name) {
        float real = Input.GetAxis(name);
        return axes.TryGetValue(name, out float bot) && Mathf.Abs(bot) > Mathf.Abs(real) ? bot : real;
    }

    public static bool AnyKeyDown {
        get { return Input.anyKeyDown || anyDownFrame == Time.frameCount; }
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
}
