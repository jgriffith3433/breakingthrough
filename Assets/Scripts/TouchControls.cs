using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// On-screen controls for phones and tablets: a stick on the left, Swing (tap swings at the bot the camera soft-locked, hold
// it to wind up a home run; there is no Lock button) and Jump on the right, one Ability button in a chapter (tap: the
// readiest ability; long press: all three), at most four buttons in a fight, Shake once birds climb, Next wave in breaks, and Turn in the build window. The campaign adds Interact (the
// context button: it appears while something can be talked to, picked up or opened, and presses E), Dodge and the ability
// slots in a chapter, Journal, and Menu in the hub and the levels; the hub (walk, talk, doors, board) has them without the bat. They press PlayInput's keys, so gameplay reads
// them exactly like the keyboard and mouse. A drag that starts off the buttons turns the camera (a finger on a button never does); in the build
// window a drag shows the piece and letting go drops it. Hidden unless the device has touch.
public class TouchControls : MonoBehaviour {
    public static TouchControls Instance { get; private set; }
    // The pads are up (a phone, in play): the boss entrance check reads it.
    public bool IsShown { get { return shown; } }

    const float Margin = 28f;
    const float StickSize = 250f;
    const float KnobSize = 110f;
    const int BotFingers = 100;
    const float LongPress = 0.4f;

    enum Role {
        None,
        Stick,
        Look,
        Place,
        Swing,
        Ability,
        Jump,
        Shake,
        Next,
        TurnLeft,
        TurnRight,
        Revive,
        Interact,
        Dodge,
        Ability1,
        Ability2,
        Ability3,
        Ability4,
        Journal,
        Menu
    }

    class Spots : Dictionary<int, Vector2> { }

    class Finger {
        public Role Role;
        public Vector2 Start;
        public Vector2 Last;
        public float Born;
    }

    class Pad {
        public Role Role;
        public GameObject Go;
        public RectTransform Rect;
        public Image Image;
        public Text Label;
        public Color Rest;
    }

    static readonly Spots botFingers = new Spots();

    readonly Dictionary<int, Finger> fingers = new Dictionary<int, Finger>();
    readonly Spots seen = new Spots();
    readonly List<int> gone = new List<int>();
    readonly List<Pad> pads = new List<Pad>();
    Font font;
    CanvasScaler scaler;
    GameObject root;
    RectTransform safe;
    RectTransform stickBase;
    RectTransform knob;
    Vector2 stickHome;
    Pad fire;
    Pad ability;
    Pad jump;
    Pad shake;
    Pad next;
    Pad turnLeft;
    Pad turnRight;
    Pad revive;
    Pad interact;
    Pad dodge;
    readonly Pad[] abilities = new Pad[4];
    Pad journal;
    Pad menu;
    bool locked;   // the Lock button is gone on touch (the camera soft-locks the nearest bot); stays false
    float expandUntil;
    bool shown;
    UiMode laidOut;
    int laidStamp = -1;
    Rect laidSafe;

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
        botFingers.Clear();
        string active = SceneManager.GetActiveScene().name;
        // The Campaign scene holds the hub (walk, talk, doors); its case files and cards are buttons and keep them off the screen.
        if (active != MetaProgress.GameScene && active != CampaignSave.Scene && !active.StartsWith("Level_")) {
            return;
        }
        if (FindAnyObjectByType<TouchControls>() != null) {
            return;
        }
        new GameObject("Touch Controls").AddComponent<TouchControls>();
    }

    // The playtest bot's finger: down at a screen point, moved by calling again, lifted with down false.
    public static void BotFinger(int id, Vector2 screen, bool down) {
        if (down) {
            botFingers[BotFingers + id] = screen;
        } else {
            botFingers.Remove(BotFingers + id);
        }
    }

    // Where a control's middle is on screen, for the bot: "Stick", "Swing", "Ability", "Jump", "Shake", "Next".
    public static bool Spot(string name, out Vector2 screen) {
        screen = Vector2.zero;
        TouchControls touch = Instance;
        if (touch == null || !touch.shown) {
            return false;
        }
        if (name == "Stick") {
            screen = touch.ScreenOf(touch.stickBase);
            return touch.stickBase.gameObject.activeSelf;
        }
        foreach (Pad pad in touch.pads) {
            if (pad.Role.ToString() == name && pad.Go.activeSelf) {
                screen = touch.ScreenOf(pad.Rect);
                return true;
            }
        }
        return false;
    }

    public static void LiftBotFingers() {
        botFingers.Clear();
    }

    // The controls on screen now, for the playtest's layout check.
    public static void Shown(List<RectTransform> into) {
        TouchControls touch = Instance;
        if (touch == null || !touch.shown) {
            return;
        }
        if (touch.stickBase.gameObject.activeInHierarchy) {
            into.Add(touch.stickBase);
        }
        foreach (Pad pad in touch.pads) {
            if (pad.Go.activeInHierarchy) {
                into.Add(pad.Rect);
            }
        }
    }

    // The pads on screen, for the Hawkes UI probe (UiRects): the stick and every pad that is up, labelled by role.
    public static void Report(List<Hawkes.Playtest.HawkesUiRects.UiControl> into) {
        TouchControls touch = Instance;
        if (touch == null || !touch.shown) {
            return;
        }
        if (touch.stickBase.gameObject.activeInHierarchy) {
            into.Add(Hawkes.Playtest.HawkesUiRects.Control("Stick", touch.stickBase));
        }
        foreach (Pad pad in touch.pads) {
            if (pad.Go.activeInHierarchy) {
                into.Add(Hawkes.Playtest.HawkesUiRects.Control(pad.Role.ToString(), pad.Rect));
            }
        }
    }

    public static bool Locked {
        get { return Instance != null && Instance.locked; }
    }

    // How far the stick reaches on screen, in pixels.
    public static float StickReach {
        get { return Instance != null && Instance.stickBase != null ? StickSize * 0.5f * Instance.stickBase.lossyScale.x : 100f; }
    }

    void Awake() {
        Instance = this;
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
        PlayInput.PadRelease();
    }

    void Start() {
        font = UiSkin.Font(UiSkin.FontRole.Label);
        Build();
        root.SetActive(false);
    }

    void Update() {
        UiFit.Notice();
        bool touch = UiFit.Touch;
        if (touch) {
            Input.simulateMouseWithTouches = false;
        }
        Gather();

        bool lost = WaveDirector.IsLost;
        ChapterHub hubNow = ChapterHub.Instance;
        bool hub = hubNow != null && hubNow.Shown && !hubNow.BoardOpen && !(ChapterScene.Instance != null && ChapterScene.Instance.CardUp);
        bool level = LevelScene.Instance != null;
        bool show = touch && (WaveDirector.Instance != null || level || hub) && !lost && !LevelUpScreen.IsOpen && !PauseMenu.IsOpen && !Cutscene.IsOpen && !JournalPanel.IsOpen && !LorePanel.IsOpen;
        if (show != shown) {
            shown = show;
            root.SetActive(show);
            if (!show) {
                LetGoAll();
            }
        }
        if (UiFit.Stamp != laidStamp || UiRects.SafeArea() != laidSafe) {
            Layout();
        }

        if (!show) {
            // After the musician falls, any tap goes back, like any key.
            if (touch && lost) {
                foreach (KeyValuePair<int, Vector2> pair in seen) {
                    if (!fingers.ContainsKey(pair.Key)) {
                        PlayInput.PadAny();
                    }
                }
            }
            fingers.Clear();
            foreach (KeyValuePair<int, Vector2> pair in seen) {
                fingers[pair.Key] = new Finger { Role = Role.None, Start = pair.Value, Last = pair.Value };
            }
            return;
        }

        bool building = WaveDirector.IsBuilding;
        // The build window keeps the player still and gives the stick's corner to the build cards.
        if (stickBase.gameObject.activeSelf == building) {
            stickBase.gameObject.SetActive(!building);
            foreach (Finger finger in fingers.Values) {
                if (finger.Role == Role.Stick) {
                    Lift(finger);
                    finger.Role = Role.None;
                }
            }
        }
        bool combat = !building && !hub;
        SetShown(fire, combat);
        // In a fight: Swing plus at most three more, in this order of need. Interact is there only while something can be
        // used; a downed companion's Revive takes its place. The campaign's three abilities are one Ability button.
        int owned = 0;
        int readiest = ReadiestAbility(combat, out owned, out float readyWait);
        bool reviveWanted = combat && Companion.Instance != null && Companion.Instance.ReviveReady;
        bool useful = Interact.Offered && !reviveWanted;
        bool dodgeWanted = combat && CampaignMoves.Active;
        bool abilityWanted = owned > 0;
        bool shakeWanted = combat && !useful && WaveDirector.Wave >= Tuning.ClimbWave;
        int room = 3;
        room = Fit(revive, reviveWanted, room);
        room = Fit(interact, useful, room);
        room = Fit(ability, abilityWanted, room);
        room = Fit(dodge, dodgeWanted, room);
        room = Fit(jump, combat, room);
        Fit(shake, shakeWanted, room);
        if (abilityWanted) {
            ability.Label.text = "Ability" + (readyWait > 0f ? "\n" + Mathf.CeilToInt(readyWait) : "");
        }
        SetShown(next, WaveDirector.Waiting && !hub);
        SetShown(turnLeft, building);
        SetShown(turnRight, building);
        // The campaign's moves: Dodge from the first step of a chapter, each ability once its level is reached.
        bool expanded = combat && Time.unscaledTime < expandUntil;
        for (int a = 0; a < abilities.Length; a++) {
            int slot = a + 1;
            int fill = CampaignProgress.SlotFill[slot];
            bool has = expanded && CampaignProgress.Active && fill >= 0 && CampaignProgress.Has(fill);
            SetShown(abilities[a], has);
            if (has) {
                float wait = CampaignProgress.Wait(fill);
                string name = fill == 1 ? CampaignProgress.RefundName : CampaignProgress.Abilities[fill].Name;
                abilities[a].Label.text = Short(name) + (wait > 0f ? "\n" + Mathf.CeilToInt(wait) : "");
            }
        }
        SetShown(journal, hub);   // in a chapter the Journal is on the pause menu
        SetShown(menu, hub || level);
        menu.Label.text = hub ? "Menu" : "Hub";
        if (!building) {
            PlayInput.PadStopPointing();
        }

        // Fingers that lifted.
        gone.Clear();
        foreach (KeyValuePair<int, Finger> pair in fingers) {
            if (!seen.ContainsKey(pair.Key)) {
                gone.Add(pair.Key);
            }
        }
        foreach (int id in gone) {
            Lift(fingers[id]);
            fingers.Remove(id);
        }

        // New fingers take a role; the others move.
        Vector2 look = Vector2.zero;
        foreach (KeyValuePair<int, Vector2> pair in seen) {
            if (!fingers.TryGetValue(pair.Key, out Finger finger)) {
                finger = new Finger { Start = pair.Value, Last = pair.Value, Born = Time.unscaledTime };
                finger.Role = Claim(pair.Key, pair.Value);
                fingers[pair.Key] = finger;
                Press(finger);
                continue;
            }
            Vector2 delta = pair.Value - finger.Last;
            finger.Last = pair.Value;
            if (finger.Role == Role.Ability && Time.unscaledTime - finger.Born > LongPress) {
                expandUntil = Time.unscaledTime + 4f;   // held: show all three
            }
            if (finger.Role == Role.Look) {   // only a finger that began off the buttons turns the camera
                look += delta;
            } else if (finger.Role == Role.Place) {
                PlayInput.PadPoint(pair.Value);
            }
        }

        Stick();
        float css = 1f / UiFit.PixelRatio;
        PlayInput.PadAxis("Mouse X", look.x * css * 0.075f);
        PlayInput.PadAxis("Mouse Y", look.y * css * 0.07f);
        Paint();
    }

    void Gather() {
        seen.Clear();
        for (int i = 0; i < Input.touchCount; i++) {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled) {
                seen[touch.fingerId] = touch.position;
            }
        }
        foreach (KeyValuePair<int, Vector2> pair in botFingers) {
            seen[pair.Key] = pair.Value;
        }
    }

    Role Claim(int id, Vector2 at) {
        foreach (Pad pad in pads) {
            if (pad.Go.activeSelf && Inside(pad.Rect, at, 1.15f)) {
                return pad.Role;
            }
        }
        // A finger on another UI (the build cards) belongs to that UI.
        if (id < BotFingers && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(id)) {
            return Role.None;
        }
        bool leftSide = at.x < Screen.width * 0.42f && at.y < Screen.height * 0.75f;
        if (leftSide && stickBase.gameObject.activeSelf && !Busy(Role.Stick)) {
            return Role.Stick;
        }
        if (WaveDirector.IsBuilding) {
            return Role.Place;
        }
        return Busy(Role.Look) ? Role.None : Role.Look;
    }

    bool Busy(Role role) {
        foreach (Finger finger in fingers.Values) {
            if (finger.Role == role) {
                return true;
            }
        }
        return false;
    }

    void Press(Finger finger) {
        switch (finger.Role) {
            case Role.Stick:
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, finger.Start, null, out local);
                float half = StickSize * 0.5f;
                local.x = Mathf.Clamp(local.x, half, Mathf.Max(half, safe.rect.width - half));
                local.y = Mathf.Clamp(local.y, half, Mathf.Max(half, safe.rect.height - half));
                stickBase.anchoredPosition = local;
                break;
            case Role.Place:
                PlayInput.PadPoint(finger.Start);
                break;
            case Role.Swing:
                PlayInput.PadHold(KeyCode.Mouse0, true);
                break;
            case Role.Jump:
                PlayInput.PadHold(KeyCode.Space, true);
                break;
            case Role.Shake:
                PlayInput.PadTap(KeyCode.F);
                break;
            case Role.Next:
                PlayInput.PadTap(KeyCode.Return);
                break;
            case Role.TurnLeft:
                PlayInput.PadTap(KeyCode.Q);
                break;
            case Role.TurnRight:
                PlayInput.PadTap(KeyCode.E);
                break;
            case Role.Revive:
                PlayInput.PadHold(KeyCode.E, true);   // held, like E: she comes up while the finger stays
                break;
            case Role.Interact:
                PlayInput.PadTap(KeyCode.E);
                break;
            case Role.Dodge:
                PlayInput.PadTap(CampaignMoves.DodgeKey);
                break;
            case Role.Ability1:
            case Role.Ability2:
            case Role.Ability3:
            case Role.Ability4:
                PlayInput.PadTap(CampaignProgress.SlotKeys[finger.Role - Role.Ability1 + 1]);
                expandUntil = 0f;
                break;
            case Role.Journal:
                PlayInput.PadTap(KeyCode.J);
                break;
            case Role.Menu:
                PlayInput.PadTap(KeyCode.Escape);
                break;
        }
    }

    void Lift(Finger finger) {
        switch (finger.Role) {
            case Role.Stick:
                stickBase.anchoredPosition = stickHome;
                knob.anchoredPosition = Vector2.zero;
                PlayInput.PadAxis("Horizontal", 0f);
                PlayInput.PadAxis("Vertical", 0f);
                break;
            case Role.Place:
                // A tap or a drag drops the piece where the finger left the ground.
                PlayInput.PadPoint(finger.Last);
                PlayInput.PadTap(KeyCode.Mouse0);
                break;
            case Role.Swing:
                PlayInput.PadHold(KeyCode.Mouse0, false);
                break;
            case Role.Jump:
                PlayInput.PadHold(KeyCode.Space, false);
                break;
            case Role.Revive:
                PlayInput.PadHold(KeyCode.E, false);
                break;
            case Role.Ability:
                // A tap fires the readiest ability; a long press has already shown all three.
                if (Time.unscaledTime - finger.Born <= LongPress) {
                    int owned;
                    float wait;
                    int slot = ReadiestAbility(true, out owned, out wait);
                    if (slot >= 0) {
                        PlayInput.PadTap(CampaignProgress.SlotKeys[slot + 1]);
                    }
                }
                break;
        }
    }

    // The ability that is ready (or soonest ready), by slot order; owned counts those the case file has.
    int ReadiestAbility(bool combat, out int owned, out float wait) {
        owned = 0;
        wait = 0f;
        int best = -1;
        float bestWait = float.MaxValue;
        if (!combat || !CampaignProgress.Active) {
            return -1;
        }
        for (int a = 0; a < abilities.Length; a++) {
            int fill = CampaignProgress.SlotFill[a + 1];
            if (fill < 0 || !CampaignProgress.Has(fill)) {
                continue;
            }
            owned++;
            float w = CampaignProgress.Wait(fill);
            if (w < bestWait) {
                bestWait = w;
                best = a;
            }
        }
        wait = best >= 0 ? bestWait : 0f;
        return best;
    }

    // Shows a fight button if it is wanted and there is room under the four-button cap; returns the room left.
    int Fit(Pad pad, bool wanted, int room) {
        bool on = wanted && room > 0;
        SetShown(pad, on);
        return on ? room - 1 : room;
    }

    void LetGoAll() {
        fingers.Clear();
        locked = false;
        PlayInput.PadRelease();
        if (stickBase != null) {
            stickBase.anchoredPosition = stickHome;
            knob.anchoredPosition = Vector2.zero;
        }
    }

    void Stick() {
        Vector2 offset = Vector2.zero;
        foreach (Finger finger in fingers.Values) {
            if (finger.Role != Role.Stick) {
                continue;
            }
            float reach = StickSize * 0.5f * stickBase.lossyScale.x;
            offset = (finger.Last - ScreenOf(stickBase)) / Mathf.Max(1f, reach);
            break;
        }
        if (offset.magnitude > 1f) {
            offset.Normalize();
        }
        knob.anchoredPosition = offset * StickSize * 0.5f;
        Vector2 move = offset.magnitude < 0.12f ? Vector2.zero : offset;
        PlayInput.PadAxis("Horizontal", move.x);
        PlayInput.PadAxis("Vertical", move.y);
    }

    void SetShown(Pad pad, bool on) {
        if (pad.Go.activeSelf == on) {
            return;
        }
        pad.Go.SetActive(on);
        if (!on) {
            foreach (Finger finger in fingers.Values) {
                if (finger.Role == pad.Role) {
                    if (pad.Role != Role.Ability) {   // hidden mid-press: no tap
                        Lift(finger);
                    }
                    finger.Role = Role.None;
                }
            }
        }
    }

    void Paint() {
        foreach (Pad pad in pads) {
            bool held = Busy(pad.Role);
            pad.Image.color = held ? Color.Lerp(pad.Rest, new Color(1f, 0.85f, 0.4f, 0.95f), 0.6f) : pad.Rest;
        }
    }

    // Ability names are long for a thumb-sized disc: the first word, or "Refund" for Refund in Full.
    static string Short(string name) {
        int space = name.IndexOf(' ');
        return name.StartsWith("Refund") ? "Refund" : name.StartsWith("Coin") ? "Coin\nToss" : name.StartsWith("Stamp") ? "Stamp" : space > 0 ? name.Substring(0, space) : name;
    }

    Vector2 ScreenOf(RectTransform rect) {
        return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
    }

    bool Inside(RectTransform rect, Vector2 screen, float slack) {
        if (Mathf.Abs(rect.rect.width - rect.rect.height) > 1f) {
            return RectTransformUtility.RectangleContainsScreenPoint(rect, screen, null);
        }
        float radius = Mathf.Max(rect.rect.width, rect.rect.height) * 0.5f * rect.lossyScale.x * slack;
        return (screen - ScreenOf(rect)).magnitude <= radius;
    }

    void Build() {
        root = new GameObject("Touch Canvas");
        root.transform.SetParent(transform, false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 190;
        scaler = root.AddComponent<CanvasScaler>();
        UiFit.Apply(scaler);

        var safeGo = new GameObject("Safe Area", typeof(RectTransform));
        safeGo.transform.SetParent(root.transform, false);
        safe = (RectTransform)safeGo.transform;
        safe.pivot = Vector2.zero;

        var baseImage = Disc(safe, "Stick", StickSize, new Color(0.05f, 0.06f, 0.1f, 0.42f));
        stickBase = baseImage.rectTransform;
        stickBase.anchorMin = Vector2.zero;
        stickBase.anchorMax = Vector2.zero;
        var knobImage = Disc(stickBase, "Knob", KnobSize, new Color(1f, 0.94f, 0.8f, 0.6f));
        knob = knobImage.rectTransform;
        knob.anchorMin = new Vector2(0.5f, 0.5f);
        knob.anchorMax = new Vector2(0.5f, 0.5f);

        fire = MakePad(Role.Swing, "Swing", 190f, 34, new Color(0.55f, 0.16f, 0.1f, 0.6f));
        jump = MakePad(Role.Jump, "Jump", 130f, 26, new Color(0.12f, 0.2f, 0.32f, 0.6f));
        ability = MakePad(Role.Ability, "Ability", 130f, 24, new Color(0.3f, 0.2f, 0.4f, 0.62f));
        shake = MakePad(Role.Shake, "Shake", 120f, 24, new Color(0.3f, 0.24f, 0.1f, 0.6f));
        turnLeft = MakePad(Role.TurnLeft, "Turn\nleft", 130f, 24, new Color(0.28f, 0.22f, 0.12f, 0.7f));
        turnRight = MakePad(Role.TurnRight, "Turn\nright", 130f, 24, new Color(0.28f, 0.22f, 0.12f, 0.7f));

        revive = MakePad(Role.Revive, "Revive", 170f, 30, new Color(0.14f, 0.34f, 0.3f, 0.7f));
        interact = MakePad(Role.Interact, "Interact", 160f, 28, new Color(0.62f, 0.44f, 0.08f, 0.78f));
        dodge = MakePad(Role.Dodge, "Dodge", 120f, 24, new Color(0.1f, 0.3f, 0.34f, 0.6f));
        for (int a = 0; a < abilities.Length; a++) {
            abilities[a] = MakePad(Role.Ability1 + a, "", 104f, 20, new Color(0.3f, 0.2f, 0.4f, 0.62f));
        }
        journal = MakePad(Role.Journal, "Journal", 104f, 20, new Color(0.26f, 0.22f, 0.12f, 0.7f));
        menu = MakePad(Role.Menu, "Menu", 104f, 22, new Color(0.14f, 0.14f, 0.2f, 0.72f));
        next = MakePad(Role.Next, "Next wave", 0f, 30, new Color(0.22f, 0.38f, 0.22f, 0.9f));
        UiSkin.Round(next.Image, 0.6f);
        next.Rect.SetParent(root.transform, false);
        next.Rect.anchorMin = new Vector2(0.5f, 1f);
        next.Rect.anchorMax = new Vector2(0.5f, 1f);
        next.Rect.pivot = new Vector2(0.5f, 1f);
        next.Rect.sizeDelta = new Vector2(320f, 92f);
        Layout();
    }

    // Corners of the safe area, a margin in; Next wave sits under the wave line at the top.
    void Layout() {
        laidOut = UiFit.Mode;
        laidStamp = UiFit.Stamp;
        laidSafe = UiRects.SafeArea();
        if (scaler != null) {
            UiFit.Apply(scaler);
        }
        float w = Mathf.Max(1, Screen.width);
        float h = Mathf.Max(1, Screen.height);
        Rect room = UiRects.SafeArea();
        safe.anchorMin = new Vector2(room.xMin / w, room.yMin / h);
        safe.anchorMax = new Vector2(room.xMax / w, room.yMax / h);
        safe.offsetMin = new Vector2(Margin, Margin);
        safe.offsetMax = new Vector2(-Margin, -Margin);

        stickHome = new Vector2(StickSize * 0.5f + 12f, StickSize * 0.5f + 12f);
        stickBase.anchoredPosition = stickHome;
        Corner(fire, new Vector2(-105f, 105f));
        Corner(revive, laidOut == UiMode.Tall ? new Vector2(-105f, 440f) : new Vector2(-440f, 250f));
        Corner(turnLeft, new Vector2(-260f, 90f));
        Corner(turnRight, new Vector2(-90f, 90f));
        bool tall = laidOut == UiMode.Tall;
        if (tall) {
            // Portrait is too narrow for the arc: Jump goes above Fire, Lock and Shake stack beside them.
            Corner(jump, new Vector2(-105f, 290f));
            Corner(ability, new Vector2(-260f, 205f));
            Corner(shake, new Vector2(-260f, 350f));
        } else {
            Corner(jump, new Vector2(-285f, 70f));
            Corner(ability, new Vector2(-255f, 235f));
            Corner(shake, new Vector2(-75f, 290f));
        }
        // Interact: by the Swing thumb, where the shake pad would sit (the two never show together); portrait keeps Revive's spot.
        Corner(interact, tall ? new Vector2(-105f, 440f) : new Vector2(-105f, 290f));
        Corner(dodge, tall ? new Vector2(-260f, 70f) : new Vector2(-430f, 95f));
        for (int a = 0; a < abilities.Length; a++) {
            Corner(abilities[a], tall ? new Vector2(-75f - 124f * a, 600f) : new Vector2(-190f - 124f * a, 430f));
        }
        // Journal and Menu hang under the top-right corner, below the health line.
        Anchored(journal, new Vector2(-62f, -200f));
        Anchored(menu, new Vector2(-62f, -88f));
        // Next wave: above the controls in portrait, under the wave line otherwise.
        float edge = tall ? 0f : 1f;
        next.Rect.anchorMin = new Vector2(0.5f, edge);
        next.Rect.anchorMax = new Vector2(0.5f, edge);
        next.Rect.pivot = new Vector2(0.5f, edge);
        next.Rect.anchoredPosition = new Vector2(0f, tall ? 510f : laidOut == UiMode.Wide ? -200f : -250f);
    }

    static void Anchored(Pad pad, Vector2 center) {
        pad.Rect.anchorMin = new Vector2(1f, 1f);
        pad.Rect.anchorMax = new Vector2(1f, 1f);
        pad.Rect.pivot = new Vector2(0.5f, 0.5f);
        pad.Rect.anchoredPosition = center;
    }

    static void Corner(Pad pad, Vector2 center) {
        pad.Rect.anchorMin = new Vector2(1f, 0f);
        pad.Rect.anchorMax = new Vector2(1f, 0f);
        pad.Rect.pivot = new Vector2(0.5f, 0.5f);
        pad.Rect.anchoredPosition = center;
    }

    Pad MakePad(Role role, string label, float size, int fontSize, Color rest) {
        var image = Disc(safe, role.ToString(), size, rest);
        var pad = new Pad { Role = role, Go = image.gameObject, Rect = image.rectTransform, Image = image, Rest = rest };
        var textGo = new GameObject("Label");
        textGo.transform.SetParent(image.transform, false);
        pad.Label = textGo.AddComponent<Text>();
        pad.Label.font = font;
        pad.Label.text = label;
        pad.Label.fontSize = fontSize;
        pad.Label.fontStyle = FontStyle.Bold;
        pad.Label.alignment = TextAnchor.MiddleCenter;
        pad.Label.color = new Color(1f, 0.96f, 0.86f, 0.95f);
        pad.Label.raycastTarget = false;
        pad.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
        pad.Label.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = textGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
        RectTransform textRect = pad.Label.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        pads.Add(pad);
        return pad;
    }

    static Image Disc(Transform parent, string name, float size, Color color) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        if (size > 0f) {
            UiSkin.Disc(image, size);
        }
        image.color = color;
        // The controls hit-test fingers themselves, so they never block the build cards or the level-up cards.
        image.raycastTarget = false;
        var rect = image.rectTransform;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        return image;
    }
}
