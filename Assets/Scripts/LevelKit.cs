using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Small pieces the campaign levels (Level*.cs) share: flat-coloured blocks that may be solid, floor decals, pinned text,
// the bark gate, and the level's own HUD. Docs/CampaignDesign.md: a level is its own scene with real colliders, placed
// encounters and no timer; nothing here reads the Arcade's wave flow.
public static class LevelKit {
    static readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();

    public static Material Flat(Color c) {
        int key = ((int)(c.r * 255f) << 24) ^ ((int)(c.g * 255f) << 16) ^ ((int)(c.b * 255f) << 8) ^ (int)(c.a * 255f);
        Material m;
        if (materials.TryGetValue(key, out m) && m != null) {
            return m;
        }
        Shader shader = SafeShader.For(c);
        m = new Material(shader) { color = c };
        materials[key] = m;
        return m;
    }

    static Mesh decal;

    // A flat, two-triangle decal mesh (ink, marks on the floor): not a primitive quad, a named mesh of its own.
    public static Mesh DecalMesh() {
        if (decal != null) {
            return decal;
        }
        decal = new Mesh { name = "Decal" };
        decal.SetVertices(new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) });
        decal.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
        decal.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) });
        decal.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
        return decal;
    }

    // A round flat disc or a pale column as a lathe mesh of its own (the objective marker), never a Unity cylinder.
    public static Mesh DiscMesh(string name, float radius, float height) {
        var km = new KitMesh { Name = name };
        km.Lathe(new List<Vector2> { new Vector2(0f, 0f), new Vector2(radius, 0f), new Vector2(radius, height), new Vector2(0f, height) }, 18, false);
        return km.ToMesh();
    }

    public static GameObject Solid(Transform parent, string name, Mesh mesh, Vector3 pos) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    // A flat colour mark lying on the ground (the stamp's telegraph and print). Not solid, casts nothing.
    public static GameObject Decal(Transform parent, string name, Vector3 pos, Vector2 size, Color c) {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = new Vector3(size.x, 1f, size.y);
        go.AddComponent<MeshFilter>().sharedMesh = DecalMesh();
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = Flat(c);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go;
    }

    // Text lying flat on the ground or standing on a post.
    public static TextMesh Text(Transform parent, string text, Vector3 pos, float size, Color c, bool flat) {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = flat ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
        var t = go.AddComponent<TextMesh>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        go.GetComponent<MeshRenderer>().sharedMaterial = t.font.material;
        t.text = text;
        t.anchor = TextAnchor.MiddleCenter;
        t.alignment = TextAlignment.Center;
        t.characterSize = size;
        t.fontSize = 48;
        t.color = c;
        return t;
    }

    public static Vector3 Flat(Vector3 v) {
        v.y = 0f;
        return v;
    }
}

// The bark gate. A bark is one line said by one speaker for a reason, never during a fight, never over another voice,
// never closer than GapSeconds to the last. Everything said is written to Log ("<speaker>|<why>|<line>") for the playtest.
public static class LevelBarks {
    public const float GapSeconds = 5.5f;
    public static readonly List<string> Log = new List<string>();
    public static readonly HashSet<string> Speakers = new HashSet<string>();
    public static int Overlaps { get; private set; }
    public static bool Quiet;   // set by the level while a fight is on
    static float lastAt = -99f;

    public static void Reset() {
        Log.Clear();
        Speakers.Clear();
        Overlaps = 0;
        Quiet = false;
        lastAt = -99f;
        LastSeconds = 0f;
    }

    public static float LastSeconds { get; private set; }
    // A line is still on screen (and being said): nothing else starts over it.
    public static bool Busy { get { return Time.time < lastAt + LastSeconds; } }

    // Says a line if the moment allows it. Returns whether it was said. `force` is for story beats that own the floor
    // (they wait their turn themselves; one that has to talk over a line counts as an overlap).
    public static bool Say(char who, string line, string why, bool force = false) {
        if (Quiet && !force) {
            return false;
        }
        if (Cutscene.IsOpen || LevelUpScreen.IsOpen || PauseMenu.IsOpen) {
            return false;
        }
        if (!force && (Time.time - lastAt < GapSeconds || Busy || !WallitVoice.FloorFree)) {
            return false;
        }
        if (force && Busy) {
            Overlaps++;
        }
        lastAt = Time.time;
        LastSeconds = Mathf.Clamp(0.9f + line.Length * 0.055f, 2f, 4.2f);
        ChapterRun.Say(who + "|" + line, LastSeconds);
        Log.Add(who + "|" + why + "|" + line);
        Speakers.Add(who.ToString());
        return true;
    }
}

// The level's HUD: Wallit's health, the objective line, the E prompt and a one-line receipt. Built in code like the other
// panels; every overlay canvas fits the phone layouts through UiFit.
public class LevelHud : MonoBehaviour {
    public Text Objective { get; private set; }
    public Text Prompt { get; private set; }
    public Text Health { get; private set; }
    public Text Receipt { get; private set; }
    float receiptUntil;
    CanvasScaler scaler;
    int stamp = -1;
    RectTransform objectiveRect;
    RectTransform promptRect;
    RectTransform healthRect;
    RectTransform receiptRect;
    RectTransform tapRect;
    // A finger on the prompt line counts as E (phones have no key for it); the level reads it once with TakeTap.
    bool tapped;
    public bool TakeTap() {
        bool t = tapped;
        tapped = false;
        return t;
    }

    public static LevelHud Make(Transform parent) {
        var go = new GameObject("Level HUD");
        go.transform.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        var hud = go.AddComponent<LevelHud>();
        hud.scaler = go.AddComponent<CanvasScaler>();
        UiFit.Apply(hud.scaler);
        hud.Objective = hud.MakeText("Objective", 28, TextAnchor.UpperLeft, new Color(0.95f, 0.9f, 0.75f), out hud.objectiveRect);
        hud.Health = hud.MakeText("Health", 30, TextAnchor.UpperRight, new Color(0.95f, 0.45f, 0.4f), out hud.healthRect);
        hud.Prompt = hud.MakeText("Prompt", 34, TextAnchor.MiddleCenter, new Color(1f, 0.93f, 0.7f), out hud.promptRect);
        hud.Receipt = hud.MakeText("Receipt", 30, TextAnchor.MiddleCenter, new Color(0.8f, 0.92f, 1f), out hud.receiptRect);
        var tap = new GameObject("PromptTap");
        tap.transform.SetParent(go.transform, false);
        var img = tap.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        var btn = tap.AddComponent<Button>();
        btn.onClick.AddListener(() => hud.tapped = true);
        hud.tapRect = img.rectTransform;
        hud.Layout();
        return hud;
    }

    Text MakeText(string name, int size, TextAnchor anchor, Color color, out RectTransform rect) {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = anchor;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(2f, -2f);
        rect = t.rectTransform;
        return t;
    }

    void Layout() {
        stamp = UiFit.Stamp;
        float w = UiFit.Compact ? UiFit.Size.x - 48f : 1500f;
        objectiveRect.anchorMin = objectiveRect.anchorMax = objectiveRect.pivot = new Vector2(0f, 1f);
        objectiveRect.anchoredPosition = new Vector2(32f, -28f);
        objectiveRect.sizeDelta = new Vector2(Mathf.Min(w, 1100f), 120f);
        healthRect.anchorMin = healthRect.anchorMax = healthRect.pivot = new Vector2(1f, 1f);
        healthRect.anchoredPosition = new Vector2(-32f, -28f);
        healthRect.sizeDelta = new Vector2(520f, 60f);
        // The prompt (and the invisible tap behind it) must stay clear of the touch pads, which draw on a higher canvas
        // (TouchControls, sorting 190): in portrait the pads climb the right side to about 650 units, so the prompt goes
        // above them; in landscape it stays low but narrow, between the stick and the Dodge/Jump/Lock arc.
        float promptY = 130f;
        float promptW = Mathf.Min(w, 1300f);
        if (UiFit.Mode == UiMode.Tall) {
            promptY = 700f;
        } else if (UiFit.Compact) {
            promptY = 170f;
            promptW = Mathf.Min(w, 520f);
        }
        promptRect.anchorMin = promptRect.anchorMax = promptRect.pivot = new Vector2(0.5f, 0f);
        promptRect.anchoredPosition = new Vector2(0f, promptY);
        promptRect.sizeDelta = new Vector2(promptW, 120f);
        tapRect.anchorMin = tapRect.anchorMax = tapRect.pivot = new Vector2(0.5f, 0f);
        tapRect.anchoredPosition = new Vector2(0f, UiFit.Compact ? promptY - 10f : 100f);
        tapRect.sizeDelta = UiFit.Compact ? new Vector2(promptW, 140f) : new Vector2(900f, 150f);
        receiptRect.anchorMin = receiptRect.anchorMax = receiptRect.pivot = new Vector2(0.5f, 1f);
        receiptRect.anchoredPosition = new Vector2(0f, -190f);
        receiptRect.sizeDelta = new Vector2(Mathf.Min(w, 1300f), 60f);
    }

    public void ShowReceipt(string text, float seconds) {
        Receipt.text = text;
        receiptUntil = Time.unscaledTime + seconds;
    }

    void Update() {
        if (stamp != UiFit.Stamp) {
            Layout();
        }
        tapRect.gameObject.SetActive(Prompt.text.Length > 0 && UiFit.Touch);
        if (Receipt.text.Length > 0 && Time.unscaledTime > receiptUntil) {
            Receipt.text = "";
        }
    }
}
