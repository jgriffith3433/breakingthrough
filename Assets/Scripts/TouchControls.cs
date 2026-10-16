using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// On-screen controls for phones and tablets: a floating left stick (WASD), a right-side drag to aim, and
// Fire, Lock, Jump and Next buttons. They press keys through PlayInput, so nothing else in the game
// needs to know a finger is involved. Touches are read directly (not through the EventSystem) so the
// stick, the aim drag and a button can all be held at once.
public class TouchControls : MonoBehaviour {
    const float LookScale = 0.07f;
    const float StickRadius = 70f;

    // Show the touch layout and the "Tap" prompts.
    public static bool Wanted {
        get { return Application.isMobilePlatform || Input.touchSupported; }
    }

    enum Role { None, Stick, Look, Fire, Lock, Jump, Next }

    class Finger {
        public int Id;
        public Role Role;
        public Vector2 Start;
        public Vector2 Last;
    }

    class Pad {
        public Role Role;
        public RectTransform Rect;
        public Image Face;
        public Text Label;
        public int Finger = -1;
    }

    readonly System.Collections.Generic.List<Finger> fingers = new System.Collections.Generic.List<Finger>();
    readonly System.Collections.Generic.List<Pad> buttons = new System.Collections.Generic.List<Pad>();
    GameObject root;
    RectTransform stickBase;
    RectTransform stickKnob;
    Pad next;
    bool locked;
    float pxPerUnit = 1f;
    Font font;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Ensure();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        Ensure();
    }

    static void Ensure() {
        if (!Wanted || SceneManager.GetActiveScene().name != MetaProgress.GameScene) {
            return;
        }
        if (FindAnyObjectByType<TouchControls>() != null) {
            return;
        }
        new GameObject("Touch Controls").AddComponent<TouchControls>();
    }

    void Start() {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvasGo = new GameObject("Touch Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 135;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        UiSkin.Reference(scaler);
        root = canvasGo;
        pxPerUnit = UiSkin.Portrait ? Screen.width / UiSkin.CanvasWidth : Screen.height / 720f;
        pxPerUnit = Mathf.Max(0.05f, pxPerUnit);

        var rt = canvasGo.transform;
        stickBase = Disc(rt, "Stick", U(150f), new Color(1f, 1f, 1f, 0.12f));
        stickKnob = Disc(stickBase, "Knob", U(70f), new Color(1f, 1f, 1f, 0.3f));
        stickKnob.anchorMin = stickKnob.anchorMax = new Vector2(0.5f, 0.5f);
        stickKnob.anchoredPosition = Vector2.zero;
        RestStick();

        float margin = 72f;
        float bottom = 84f;
        float fire = 104f;
        float small = 72f;
        Add(rt, Role.Fire, "Fire", new Vector2(1f, 0f), new Vector2(-margin - fire * 0.5f, bottom + fire * 0.5f), fire, 24);
        Add(rt, Role.Lock, "Lock", new Vector2(1f, 0f), new Vector2(-margin - fire - 16f - small * 0.5f, bottom + small * 0.5f), small, 18);
        Add(rt, Role.Jump, "Jump", new Vector2(1f, 0f), new Vector2(-margin - small * 0.5f - 16f, bottom + fire + 16f + small * 0.5f), small, 18);
        next = Add(rt, Role.Next, "Next wave", new Vector2(0.5f, 0f), new Vector2(0f, 110f + 28f), 0f, 20);
        next.Rect.sizeDelta = new Vector2(U(190f), U(60f));
        next.Rect.pivot = new Vector2(0.5f, 0.5f);
        next.Face.sprite = UiSkin.Pill;
    }

    float U(float px) {
        return px / pxPerUnit;
    }

    RectTransform Disc(Transform parent, string name, float size, Color color) {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.sprite = UiSkin.Pill;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        return rect;
    }

    // Position is in screen pixels from the anchor, size in pixels (0 keeps the caller's own sizing).
    Pad Add(Transform parent, Role role, string label, Vector2 anchor, Vector2 pixelPosition, float pixelSize, int fontPx) {
        var button = new Pad { Role = role };
        var go = new GameObject(label, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        button.Face = go.AddComponent<Image>();
        button.Face.sprite = UiSkin.Pill;
        button.Face.type = Image.Type.Sliced;
        button.Face.color = new Color(1f, 1f, 1f, 0.2f);
        button.Face.raycastTarget = false;
        button.Rect = button.Face.rectTransform;
        button.Rect.anchorMin = button.Rect.anchorMax = anchor;
        button.Rect.pivot = new Vector2(0.5f, 0.5f);
        button.Rect.anchoredPosition = new Vector2(U(pixelPosition.x), U(pixelPosition.y));
        button.Rect.sizeDelta = new Vector2(U(pixelSize), U(pixelSize));

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        button.Label = textGo.AddComponent<Text>();
        button.Label.font = font;
        button.Label.text = label;
        button.Label.fontSize = Mathf.RoundToInt(U(fontPx));
        button.Label.fontStyle = FontStyle.Bold;
        button.Label.alignment = TextAnchor.MiddleCenter;
        button.Label.color = new Color(1f, 0.97f, 0.9f, 0.95f);
        button.Label.raycastTarget = false;
        var textRect = button.Label.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        buttons.Add(button);
        return button;
    }

    void RestStick() {
        stickBase.anchoredPosition = new Vector2(U(48f + StickRadius + 20f), U(70f + StickRadius + 20f));
        stickKnob.anchoredPosition = Vector2.zero;
        SetAlpha(stickBase, 0.1f);
    }

    static void SetAlpha(RectTransform rect, float alpha) {
        var image = rect.GetComponent<Image>();
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }

    void OnDisable() {
        ReleaseAll();
    }

    void ReleaseAll() {
        fingers.Clear();
        for (int i = 0; i < buttons.Count; i++) {
            buttons[i].Finger = -1;
        }
        locked = false;
        PlayInput.TouchHold(KeyCode.Mouse0, false);
        PlayInput.TouchHold(KeyCode.Space, false);
        PlayInput.TouchHold(KeyCode.LeftShift, false);
        PlayInput.TouchMove(0f, 0f);
        PlayInput.TouchLook(0f, 0f);
    }

    void Update() {
        if (root == null) {
            return;
        }
        bool building = WaveDirector.IsBuilding;
        bool hidden = LevelUpScreen.IsOpen || WaveDirector.IsLost;
        bool showNext = !hidden && WaveDirector.CanSkipBreak;
        if (root.activeSelf == hidden) {
            root.SetActive(!hidden);
        }
        if (hidden || building) {
            // Menus and the build bar take taps themselves.
            if (fingers.Count > 0 || locked) {
                ReleaseAll();
                RestStick();
            }
        }
        for (int i = 0; i < buttons.Count; i++) {
            Pad b = buttons[i];
            bool show = b.Role == Role.Next ? showNext : !hidden && !building;
            if (b.Rect.gameObject.activeSelf != show) {
                b.Rect.gameObject.SetActive(show);
            }
        }
        stickBase.gameObject.SetActive(!hidden && !building);
        if (hidden) {
            return;
        }

        float lookX = 0f;
        float lookY = 0f;
        for (int t = 0; t < Input.touchCount; t++) {
            Touch touch = Input.GetTouch(t);
            Finger finger = FindFinger(touch.fingerId);
            if (touch.phase == TouchPhase.Began) {
                if (finger == null) {
                    finger = new Finger { Id = touch.fingerId, Start = touch.position, Last = touch.position };
                    fingers.Add(finger);
                    Claim(finger);
                }
            } else if (finger == null) {
                continue;
            }
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) {
                Drop(finger);
                continue;
            }
            Vector2 delta = touch.position - finger.Last;
            finger.Last = touch.position;
            if (finger.Role == Role.Look) {
                lookX += delta.x * LookScale;
                lookY += delta.y * LookScale;
            } else if (finger.Role == Role.Stick) {
                MoveStick(finger);
            }
        }
        PlayInput.TouchLook(lookX, lookY);

        for (int i = 0; i < buttons.Count; i++) {
            Pad b = buttons[i];
            bool down = b.Finger >= 0;
            switch (b.Role) {
                case Role.Fire:
                    PlayInput.TouchHold(KeyCode.Mouse0, down);
                    break;
                case Role.Jump:
                    PlayInput.TouchHold(KeyCode.Space, down);
                    break;
                case Role.Lock:
                    PlayInput.TouchHold(KeyCode.LeftShift, locked);
                    down = locked;
                    break;
            }
            Color c = b.Face.color;
            c.a = down ? 0.5f : 0.2f;
            b.Face.color = c;
        }
    }

    Finger FindFinger(int id) {
        for (int i = 0; i < fingers.Count; i++) {
            if (fingers[i].Id == id) {
                return fingers[i];
            }
        }
        return null;
    }

    void Claim(Finger finger) {
        if (!WaveDirector.IsBuilding) {
            for (int i = 0; i < buttons.Count; i++) {
                Pad b = buttons[i];
                if (!b.Rect.gameObject.activeSelf || b.Finger >= 0 && b.Role != Role.Lock && b.Role != Role.Next) {
                    continue;
                }
                if (RectTransformUtility.RectangleContainsScreenPoint(b.Rect, finger.Start, null)) {
                    finger.Role = b.Role;
                    if (b.Role == Role.Lock) {
                        locked = !locked;
                    } else if (b.Role == Role.Next) {
                        PlayInput.TouchTap(KeyCode.Return);
                    } else {
                        b.Finger = finger.Id;
                    }
                    return;
                }
            }
            if (finger.Start.x < Screen.width * 0.45f && finger.Start.y < Screen.height * 0.7f) {
                finger.Role = Role.Stick;
                stickBase.anchoredPosition = new Vector2(finger.Start.x / pxPerUnit, finger.Start.y / pxPerUnit);
                stickKnob.anchoredPosition = Vector2.zero;
                SetAlpha(stickBase, 0.2f);
            } else if (finger.Start.x >= Screen.width * 0.45f) {
                finger.Role = Role.Look;
            }
        }
    }

    void MoveStick(Finger finger) {
        Vector2 offset = (finger.Last - finger.Start) / StickRadius;
        if (offset.magnitude > 1f) {
            offset.Normalize();
        }
        stickKnob.anchoredPosition = offset * U(StickRadius);
        if (offset.magnitude < 0.12f) {
            offset = Vector2.zero;
        }
        PlayInput.TouchMove(offset.x, offset.y);
    }

    void Drop(Finger finger) {
        fingers.Remove(finger);
        for (int i = 0; i < buttons.Count; i++) {
            if (buttons[i].Finger == finger.Id) {
                buttons[i].Finger = -1;
            }
        }
        if (finger.Role == Role.Stick) {
            PlayInput.TouchMove(0f, 0f);
            RestStick();
        }
    }
}
