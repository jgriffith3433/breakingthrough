using System.Collections.Generic;
using Hawkes.Playtest;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The game's own Unity UI, measured. On a touch device (or while a playtest runs) it looks at every live button a few times a
// second and keeps it a fair target: the hit area is padded to 44 points (the art keeps its size, `Graphic.raycastPadding`), a
// control that sits under the notch, Dynamic Island, home bar or off an edge is moved in, and a button that lacks the menu's
// pressed feel gets it (`MenuButtonFx`: sink, a stamp thunk). Under a playtest it also tells the Hawkes UI probe where every
// control is (`HawkesUiRects.Report`, one `ui-layout` per change of screen) and when one is pressed (`HawkesUiRects.Press`).
// The touch pads (`TouchControls`) report through the same call. Desktop with no playtest does nothing.
public class UiRects : MonoBehaviour {
    const float MinPoints = 44f;
    const float ScanEvery = 0.25f;

    public static UiRects Instance { get; private set; }
    // The last layout reported, for the ui-walk scenario: controls, and the issues the probe found (small, inset, offscreen).
    public static int LastControls { get; private set; }
    public static int LastIssues { get; private set; }
    public static string LastJson { get; private set; }
    public static int Presses { get; private set; }
    public static int Reports { get; private set; }

    float nextScan;
    // Where each button stood at the last scan: a control is only moved in once it has held still (a panel sliding in is not).
    readonly Dictionary<Button, Rect> lastSeen = new Dictionary<Button, Rect>();
    string reported = "";
    string pending = "";
    int pendingScans;
    readonly List<HawkesUiRects.UiControl> controls = new List<HawkesUiRects.UiControl>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install() {
        if (Instance != null) {
            return;
        }
        var go = new GameObject("Ui Rects");
        DontDestroyOnLoad(go);
        go.AddComponent<UiRects>();
    }

    void Awake() {
        Instance = this;
    }

    // Screen pixels per point: a phone is 390 points on its short side.
    public static float PixelsPerPoint {
        get { return Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) / 390f); }
    }

    // Screen.safeArea, pulled in by the simulated iPhone insets (notch 47 and home bar 34 points in portrait, 47 each side and
    // 21 below in landscape) when the device reports less. Everything a thumb must reach sits inside it.
    public static Rect SafeArea() {
        float w = Screen.width;
        float h = Screen.height;
        float ppp = PixelsPerPoint;
        bool portrait = h >= w;
        Rect sa = Screen.safeArea;
        if (sa.width < 1f || sa.height < 1f) {
            sa = new Rect(0f, 0f, w, h);
        }
        float top = Mathf.Max(portrait ? 47f * ppp : 0f, h - sa.yMax);
        float bottom = Mathf.Max((portrait ? 34f : 21f) * ppp, sa.yMin);
        float left = Mathf.Max(portrait ? 0f : 47f * ppp, sa.xMin);
        float right = Mathf.Max(portrait ? 0f : 47f * ppp, w - sa.xMax);
        return Rect.MinMaxRect(left, bottom, w - right, h - top);
    }

    static bool Active {
        get { return UiFit.Touch || Playtest.Wanted; }
    }

    void Update() {
        if (!Active || Time.unscaledTime < nextScan) {
            return;
        }
        nextScan = Time.unscaledTime + ScanEvery;
        Scan();
    }

    static string LabelOf(Button button) {
        Text text = button.GetComponentInChildren<Text>(true);
        string label = text != null ? text.text : "";
        label = label.Replace("\n", " ").Trim();
        return label.Length > 0 ? label : button.name;
    }

    // False for a button a player cannot press now: hidden, faded out, in a group that blocks, or off a world-space canvas.
    public static bool IsLive(Button button) {
        if (button == null || !button.isActiveAndEnabled || button.targetGraphic == null || !button.targetGraphic.isActiveAndEnabled) {
            return false;
        }
        Canvas canvas = button.targetGraphic.canvas;
        if (canvas == null || !canvas.isActiveAndEnabled || canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay) {
            return false;
        }
        foreach (CanvasGroup group in button.GetComponentsInParent<CanvasGroup>(false)) {
            if (group.alpha < 0.05f || !group.blocksRaycasts || !group.interactable) {
                return false;
            }
        }
        return button.targetGraphic.raycastTarget;
    }

    static Rect ScreenRect(RectTransform rt) {
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
    }

    void Scan() {
        controls.Clear();
        Rect safe = SafeArea();
        float need = MinPoints * PixelsPerPoint;
        var seen = new List<Button>();
        foreach (Button button in FindObjectsByType<Button>(FindObjectsSortMode.None)) {
            if (!IsLive(button)) {
                continue;
            }
            seen.Add(button);
            if (button.GetComponent<MenuButtonFx>() == null) {
                MenuButtonFx.Add(button);
            }
            if (button.GetComponent<UiPressProbe>() == null) {
                button.gameObject.AddComponent<UiPressProbe>().Label = LabelOf(button);
            }
            RectTransform rt = button.targetGraphic.rectTransform;
            Rect r = ScreenRect(rt);
            if (r.width < 2f || r.height < 2f) {
                continue;
            }
            // Keep the control inside the safe area: slide it in by what sticks out (art and hit area together).
            Rect hit = Pad(button.targetGraphic, r, need);
            Vector2 shift = Inward(hit, safe);
            bool still = lastSeen.TryGetValue(button, out Rect before) && Mathf.Abs(before.x - r.x) < 0.5f && Mathf.Abs(before.y - r.y) < 0.5f;
            lastSeen[button] = r;
            if (still && shift != Vector2.zero && !(rt.parent != null && rt.parent.GetComponent<LayoutGroup>() != null)) {
                float scale = Mathf.Max(0.0001f, rt.lossyScale.x);
                rt.anchoredPosition += shift / scale;
                r = ScreenRect(rt);
                lastSeen[button] = r;
                hit = Pad(button.targetGraphic, r, need);
            }
            controls.Add(HawkesUiRects.Control(LabelOf(button), rt));
            var padded = controls[controls.Count - 1];
            padded.Screen = hit;
            controls[controls.Count - 1] = padded;
        }
        TouchControls.Report(controls);
        if (lastSeen.Count > 400) {
            lastSeen.Clear();
        }

        if (!Playtest.Running && !Playtest.Wanted) {
            return;
        }
        string sig = Signature();
        if (sig != pending) {
            pending = sig;
            pendingScans = 0;
            return;
        }
        // The layout has held for two scans (a panel finished sliding in).
        if (++pendingScans < 2 || sig == reported) {
            return;
        }
        reported = sig;
        if (controls.Count == 0) {
            return;
        }
        LastJson = HawkesUiRects.Report(controls, SceneName(seen));
        Reports++;
        LastControls = controls.Count;
        LastIssues = LastJson.Split(new[] { "\"kind\":" }, System.StringSplitOptions.None).Length - 1;
    }

    // Grow a graphic's raycast area to the minimum and return the screen rect of that hit area.
    static Rect Pad(Graphic graphic, Rect r, float need) {
        float padX = Mathf.Max(0f, (need - r.width) * 0.5f);
        float padY = Mathf.Max(0f, (need - r.height) * 0.5f);
        float scale = Mathf.Max(0.0001f, graphic.rectTransform.lossyScale.x);
        graphic.raycastPadding = new Vector4(-padX / scale, -padY / scale, -padX / scale, -padY / scale);
        return Rect.MinMaxRect(r.xMin - padX, r.yMin - padY, r.xMax + padX, r.yMax + padY);
    }

    static Vector2 Inward(Rect r, Rect safe) {
        if (r.width > safe.width || r.height > safe.height) {
            return Vector2.zero;
        }
        float dx = r.xMin < safe.xMin ? safe.xMin - r.xMin : r.xMax > safe.xMax ? safe.xMax - r.xMax : 0f;
        float dy = r.yMin < safe.yMin ? safe.yMin - r.yMin : r.yMax > safe.yMax ? safe.yMax - r.yMax : 0f;
        return new Vector2(dx, dy);
    }

    string Signature() {
        var sb = new System.Text.StringBuilder();
        sb.Append(Screen.width).Append('x').Append(Screen.height).Append(':');
        foreach (HawkesUiRects.UiControl c in controls) {
            sb.Append(c.Label).Append('@').Append(Mathf.RoundToInt(c.Screen.x / 4f)).Append(',').Append(Mathf.RoundToInt(c.Screen.y / 4f)).Append(',')
              .Append(Mathf.RoundToInt(c.Screen.width / 4f)).Append(',').Append(Mathf.RoundToInt(c.Screen.height / 4f)).Append(';');
        }
        return sb.ToString();
    }

    // The screen's name for the report: the canvas that sits on top of the others.
    static string SceneName(List<Button> buttons) {
        int best = int.MinValue;
        string name = "hud";
        foreach (Button b in buttons) {
            Canvas root = b.targetGraphic.canvas.rootCanvas;
            if (root.sortingOrder > best) {
                best = root.sortingOrder;
                name = root.name;
            }
        }
        return name;
    }

    // Forget what was reported (a new screen is about to be walked), so the next stable layout reports again.
    public static void Rearm() {
        if (Instance != null) {
            Instance.reported = "";
            Instance.pending = "";
            Instance.pendingScans = 0;
        }
    }

    // One press: the game's own pressed state and sound answered (MenuButtonFx), so the probe counts it as answered.
    public static void Pressed(string label, bool answered) {
        Presses++;
        HawkesUiRects.Press(label, answered, Time.unscaledDeltaTime * 1000f);
    }
}

// A button's press, told to the probe. The pressed feel itself is `MenuButtonFx` (UiRects adds it to every button that lacks one),
// which sinks the button and plays the stamp thunk on the same frame as the finger.
public class UiPressProbe : MonoBehaviour, IPointerDownHandler {
    public string Label = "";

    public void OnPointerDown(PointerEventData eventData) {
        Button button = GetComponent<Button>();
        if (button == null || !button.interactable) {
            return;
        }
        bool feel = GetComponent<MenuButtonFx>() != null;
        UiRects.Pressed(Label, feel);
    }
}
