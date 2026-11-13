using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Keyboard and gamepad for every menu panel. Attach it to a panel's root: while that root is active (and no panel
// with a higher priority is), arrows / WASD / the stick or d-pad move a gold ring between its buttons and sliders,
// Enter / Space / the pad's A press the one under the ring, Left and Right turn a slider, and Escape or the pad's B is
// Back (panels read MenuNav.Back for it). It reads PlayInput, so the playtest bot presses the same keys, and it
// switches off the event system's own navigation so one press never moves twice.
public class MenuNav : MonoBehaviour {
    const float FirstRepeat = 0.4f;
    const float Repeat = 0.16f;
    const float SliderStep = 0.1f;

    static readonly List<MenuNav> live = new List<MenuNav>();

    // For the playtest bot.
    public static int Moves { get; private set; }
    public static int Submits { get; private set; }
    public static int SliderTurns { get; private set; }

    // Escape or the pad's B: step back out of the panel.
    public static bool Back {
        get { return PlayInput.GetKeyDown(KeyCode.Escape) || PlayInput.GetKeyDown(KeyCode.JoystickButton1); }
    }

    // The pad's Start button: pause.
    public static bool PadStart {
        get { return PlayInput.GetKeyDown(KeyCode.JoystickButton7); }
    }

    public static GameObject Selected {
        get { return EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null; }
    }

    public int Priority { get; private set; }

    Vector2Int held;
    float nextAt;
    int enabledFrame;
    Outline ring;
    Graphic ringOn;

    // Puts the navigation on a panel root. Priority is the canvas's sorting order: the panel on top wins.
    public static MenuNav Attach(GameObject root, int priority) {
        var nav = root.GetComponent<MenuNav>();
        if (nav == null) {
            nav = root.AddComponent<MenuNav>();
        }
        nav.Priority = priority;
        return nav;
    }

    void OnEnable() {
        if (!live.Contains(this)) {
            live.Add(this);
        }
        held = Vector2Int.zero;
        enabledFrame = Time.frameCount;
    }

    void OnDisable() {
        live.Remove(this);
        Unring();
    }

    bool IsTop() {
        foreach (MenuNav other in live) {
            if (other != this && other.Priority > Priority) {
                return false;
            }
        }
        return true;
    }

    void Update() {
        EventSystem system = EventSystem.current;
        if (system == null) {
            return;
        }
        if (!IsTop()) {
            Unring();
            return;
        }
        // Our own navigation below replaces the module's (which the bot cannot press anyway).
        system.sendNavigationEvents = false;

        Selectable current = Current(system);
        if (current == null) {
            current = First();
            Select(system, current);
        }
        Ring(current);
        if (current == null) {
            return;
        }

        Vector2Int dir = Direction();
        if (dir != held) {
            held = dir;
            nextAt = Time.unscaledTime + FirstRepeat;
            if (dir != Vector2Int.zero) {
                Step(system, current, dir);
            }
        } else if (dir != Vector2Int.zero && Time.unscaledTime >= nextAt) {
            nextAt = Time.unscaledTime + Repeat;
            Step(system, current, dir);
        }

        // Not on the frame the panel opened: the Enter that opened it must not also press its first button.
        if (Time.frameCount > enabledFrame && (PlayInput.GetKeyDown(KeyCode.Return) || PlayInput.GetKeyDown(KeyCode.KeypadEnter) || PlayInput.GetKeyDown(KeyCode.Space) || PlayInput.GetKeyDown(KeyCode.JoystickButton0))) {
            Selectable now = Current(system);
            if (now != null && !(now is Slider)) {
                Submits++;
                ExecuteEvents.Execute(now.gameObject, new BaseEventData(system), ExecuteEvents.submitHandler);
            }
        }
    }

    // The selected thing, if it still belongs to this panel and can be used.
    Selectable Current(EventSystem system) {
        GameObject go = system.currentSelectedGameObject;
        if (go == null || !go.activeInHierarchy || !go.transform.IsChildOf(transform)) {
            return null;
        }
        Selectable selectable = go.GetComponent<Selectable>();
        return selectable != null && selectable.IsInteractable() ? selectable : null;
    }

    // The first usable thing in reading order (top, then left).
    Selectable First() {
        Selectable best = null;
        float bestScore = float.MaxValue;
        foreach (Selectable s in GetComponentsInChildren<Selectable>(false)) {
            if (!s.IsInteractable() || !s.gameObject.activeInHierarchy) {
                continue;
            }
            Vector3 p = s.transform.position;
            float score = -p.y * 1000f + p.x;
            if (score < bestScore) {
                bestScore = score;
                best = s;
            }
        }
        return best;
    }

    // Held, or tapped this frame (the playtest bot taps).
    static bool Pressed(KeyCode key) {
        return PlayInput.GetKey(key) || PlayInput.GetKeyDown(key);
    }

    static Vector2Int Direction() {
        int x = 0;
        int y = 0;
        if (Pressed(KeyCode.LeftArrow) || Pressed(KeyCode.A) || PlayInput.GetAxis("Horizontal") < -0.6f) {
            x -= 1;
        }
        if (Pressed(KeyCode.RightArrow) || Pressed(KeyCode.D) || PlayInput.GetAxis("Horizontal") > 0.6f) {
            x += 1;
        }
        if (Pressed(KeyCode.UpArrow) || Pressed(KeyCode.W) || PlayInput.GetAxis("Vertical") > 0.6f) {
            y += 1;
        }
        if (Pressed(KeyCode.DownArrow) || Pressed(KeyCode.S) || PlayInput.GetAxis("Vertical") < -0.6f) {
            y -= 1;
        }
        // One axis at a time: vertical wins a diagonal.
        if (y != 0) {
            x = 0;
        }
        return new Vector2Int(x, y);
    }

    void Step(EventSystem system, Selectable current, Vector2Int dir) {
        if (dir.x != 0 && current is Slider slider) {
            float before = slider.value;
            slider.value = Mathf.Clamp(Mathf.Round((slider.value + dir.x * SliderStep) / SliderStep) * SliderStep, slider.minValue, slider.maxValue);
            if (!Mathf.Approximately(before, slider.value)) {
                SliderTurns++;
            }
            return;
        }
        Selectable next = Nearest(current, dir);
        if (next != null) {
            Moves++;
            Select(system, next);
            Ring(next);
        }
    }

    // The closest usable thing in that direction, by where the rects' centres are; a sideways drift counts against it.
    Selectable Nearest(Selectable from, Vector2Int dir) {
        Vector3 origin = Center(from);
        var along = new Vector3(dir.x, dir.y, 0f);
        Selectable best = null;
        float bestScore = float.MaxValue;
        foreach (Selectable s in GetComponentsInChildren<Selectable>(false)) {
            if (s == from || !s.IsInteractable() || !s.gameObject.activeInHierarchy) {
                continue;
            }
            Vector3 d = Center(s) - origin;
            float ahead = Vector3.Dot(d, along);
            if (ahead <= 1f) {
                continue;
            }
            float side = Mathf.Abs(Vector3.Dot(d, new Vector3(-dir.y, dir.x, 0f)));
            float score = ahead + side * 3f;
            if (score < bestScore) {
                bestScore = score;
                best = s;
            }
        }
        return best;
    }

    static Vector3 Center(Selectable s) {
        var rect = s.transform as RectTransform;
        return rect != null ? rect.TransformPoint(rect.rect.center) : s.transform.position;
    }

    static void Select(EventSystem system, Selectable target) {
        system.SetSelectedGameObject(target != null ? target.gameObject : null);
    }

    // A gold frame round what is selected. Buttons wear it on their own face, sliders on their knob.
    void Ring(Selectable target) {
        Graphic want = target != null ? (target.targetGraphic != null ? target.targetGraphic : target.GetComponent<Graphic>()) : null;
        if (want == ringOn) {
            return;
        }
        Unring();
        if (want == null) {
            return;
        }
        ring = want.gameObject.AddComponent<Outline>();
        ring.effectColor = new Color(1f, 0.86f, 0.38f, 1f);
        ring.effectDistance = new Vector2(5f, -5f);
        ring.useGraphicAlpha = false;
        ringOn = want;
        GameAudio.PlayUI("hit", 0.06f, 2f);
    }

    void Unring() {
        if (ring != null) {
            Destroy(ring);
        }
        ring = null;
        ringOn = null;
    }
}
