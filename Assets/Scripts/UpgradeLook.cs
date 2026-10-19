using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Each shop rank shows up on the hill: a mark you can see, not only a number in the menu.
public class UpgradeLook : MonoBehaviour {
    static float nextStride;

    public static string LoadoutLine() {
        string line = "";
        for (int i = 0; i < MetaProgress.Catalog.Length; i++) {
            UpgradeInfo info = MetaProgress.Catalog[i];
            int rank = MetaProgress.Rank(info.Id);
            if (rank <= 0) {
                continue;
            }
            if (line.Length > 0) {
                line += "   ·   ";
            }
            line += info.Name + " " + rank;
        }
        return line;
    }

    public static void AnnounceLoadout() {
        string line = LoadoutLine();
        if (string.IsNullOrEmpty(line)) {
            return;
        }
        var go = new GameObject("Loadout");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 190;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var textGo = new GameObject("Line");
        textGo.transform.SetParent(go.transform, false);
        var text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 32;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.86f, 0.38f);
        text.text = "You brought\n" + line;
        var rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -150f);
        rect.sizeDelta = new Vector2(1400f, 90f);
        if (UiFit.Compact) {
            UiFit.Apply(scaler);
            text.fontSize = 26;
            rect.anchoredPosition = new Vector2(0f, UiFit.Mode == UiMode.Tall ? -260f : -200f);
            rect.sizeDelta = new Vector2(UiFit.Size.x - 48f, 90f);
        }
        go.AddComponent<LoadoutBanner>();
    }

    // The bat wears what you picked: each bat boon and both bat shop upgrades leave a mark on it (or on Wallit).
    public static void DressBat(Transform bat, Transform owner) {
        if (bat == null || bat.GetComponent<BatMarks>() != null) {
            return;
        }
        bat.gameObject.AddComponent<BatMarks>().Owner = owner;
    }

    // What the bat shows right now, for the playtest ("aluminum (2 dents), flames: Pine Tar, ...").
    public static string BatLine() {
        return BatMarks.Instance != null ? BatMarks.Instance.Line : "";
    }

    // A brass hex bolt, unlit like the pickups. Returns the part to spin.
    public static Transform BuildBolt(Transform parent, float size) {
        var spin = new GameObject("Bolt").transform;
        spin.SetParent(parent, false);
        spin.localScale = Vector3.one * size;
        Part(spin, PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), Quaternion.identity, new Vector3(0.3f, 0.06f, 0.3f), new Color(1f, 0.82f, 0.4f));
        Part(spin, PrimitiveType.Cylinder, new Vector3(0f, -0.08f, 0f), Quaternion.identity, new Vector3(0.12f, 0.17f, 0.12f), new Color(0.78f, 0.6f, 0.28f));
        return spin;
    }

    public static GameObject MarkPart(Transform parent, PrimitiveType type, Vector3 localPos, Quaternion localRot, Vector3 localScale, Color color) {
        return Part(parent, type, localPos, localRot, localScale, color);
    }

    // See-through, for glows and shells on the bat.
    public static GameObject GlassPart(Transform parent, PrimitiveType type, Vector3 localPos, Quaternion localRot, Vector3 localScale, Color color) {
        GameObject go = Part(parent, type, localPos, localRot, localScale, color);
        Tint(go, color);
        return go;
    }

    static GameObject Part(Transform parent, PrimitiveType type, Vector3 localPos, Quaternion localRot, Vector3 localScale, Color color) {
        GameObject go = GameObject.CreatePrimitive(type);
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) {
            Object.Destroy(collider);
        }
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = localScale;
        Paint(go, color);
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null) {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        return go;
    }

    public static void Blast(Vector3 point, float radius) {
        int rank = MetaProgress.Rank(UpgradeId.Blast);
        Fx.Explosion(point, Mathf.Max(2.4f, radius), new Color(1f, 0.55f, 0.18f));
        GameAudio.Play("launch", 0.45f + 0.08f * rank, Random.Range(0.9f, 1.1f));
    }

    public static void Coins(Vector3 point, int count) {
        Fx.Ring(point + Vector3.up * 0.1f, 1.8f, new Color(1f, 0.82f, 0.3f, 0.8f), 0.4f);
    }

    public static void Stride(Vector3 feet) {
        int rank = MetaProgress.Rank(UpgradeId.Stride);
        if (rank <= 0 || Time.time < nextStride) {
            return;
        }
        nextStride = Time.time + Mathf.Lerp(0.22f, 0.08f, rank / 3f);
        Fx.Footstep(feet, 1f + 0.3f * rank);
    }

    public static void Bolt(Vector3 from, Vector3 to) {
        int rank = MetaProgress.Rank(UpgradeId.Towers);
        if (rank <= 0) {
            return;
        }
        var go = new GameObject("TowerShot");
        var line = go.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.startWidth = 0.04f + 0.015f * rank;
        line.endWidth = 0.01f;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) {
            line.material = new Material(shader);
        }
        Color gold = Color.Lerp(new Color(1f, 0.75f, 0.3f), new Color(1f, 0.95f, 0.6f), rank / 3f);
        line.startColor = gold;
        line.endColor = gold;
        Flourish flourish = go.AddComponent<Flourish>();
        flourish.Begin(Vector3.zero, 0.12f, Vector3.one);
        flourish.hideLine = true;
    }

    public static void Spawn(Vector3 point, Vector3 scale, Color color, float life, Vector3 velocity, float endScale) {
        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(body.GetComponent<Collider>());
        body.transform.position = point;
        body.transform.localScale = scale;
        Paint(body, color);
        Flourish flourish = body.AddComponent<Flourish>();
        flourish.Begin(velocity, life, Vector3.one * endScale);
    }

    // Like Spawn, but see-through and fading, so big blasts never hide the fight.
    public static void Glow(Vector3 point, Vector3 scale, Color color, float life, Vector3 velocity, float endScale) {
        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(body.GetComponent<Collider>());
        body.transform.position = point;
        body.transform.localScale = scale;
        Tint(body, color);
        Flourish flourish = body.AddComponent<Flourish>();
        flourish.Begin(velocity, life, Vector3.one * endScale);
        flourish.Fade();
    }

    static void Tint(GameObject go, Color color) {
        Renderer renderer = go.GetComponent<Renderer>();
        Shader shader = Shader.Find("Sprites/Default");
        if (renderer == null || shader == null) {
            Paint(go, color);
            return;
        }
        renderer.sharedMaterial = new Material(shader);
        renderer.sharedMaterial.color = color;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    public static bool HitConfirm { get { return Time.time < hitUntil; } }
    static float hitUntil;

    public static void MarkHit() {
        hitUntil = Time.time + 0.14f;
    }

    public static void Spark(Vector3 point) {
        Fx.Hit(point, 1f, false);
    }

    public static void Number(Vector3 point, int amount) {
        Number(point, amount, false, DamageKind.Plain);
    }

    static int numbersThisFrame;
    static int numberFrame;

    // Damage numbers. Crits are big and gold, fire is orange, lightning is blue.
    public static void Number(Vector3 point, float amount, bool crit, DamageKind kind) {
        if (numberFrame != Time.frameCount) {
            numberFrame = Time.frameCount;
            numbersThisFrame = 0;
        }
        if (++numbersThisFrame > 14 && !crit) {
            return;
        }
        var go = new GameObject("Damage");
        Vector3 jitter = new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(0f, 0.3f), Random.Range(-0.35f, 0.35f));
        go.transform.position = point + Vector3.up * 0.4f + jitter;
        TextMesh mesh = go.AddComponent<TextMesh>();
        int shown = Mathf.Max(1, Mathf.RoundToInt(amount));
        mesh.text = crit ? shown + "!" : shown.ToString();
        mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mesh.fontSize = 64;
        mesh.fontStyle = crit ? FontStyle.Bold : FontStyle.Normal;
        float far = Camera.main != null ? Mathf.Clamp(Vector3.Distance(Camera.main.transform.position, point) / 14f, 1f, 4f) : 1f;
        mesh.characterSize = (crit ? 0.075f : kind == DamageKind.Burn ? 0.035f : 0.045f) * far;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.color = NumberColor(crit, kind);
        Renderer meshRenderer = mesh.GetComponent<Renderer>();
        if (meshRenderer != null && mesh.font != null && mesh.font.material != null) {
            meshRenderer.material = mesh.font.material;
        }
        go.AddComponent<FloatUp>();
    }

    static Color NumberColor(bool crit, DamageKind kind) {
        if (crit) {
            return new Color(1f, 0.82f, 0.2f);
        }
        switch (kind) {
            case DamageKind.Burn: return new Color(1f, 0.55f, 0.2f);
            case DamageKind.Lightning: return new Color(0.55f, 0.85f, 1f);
            case DamageKind.Blast: return new Color(1f, 0.7f, 0.45f);
            case DamageKind.Blade: return new Color(0.75f, 0.95f, 1f);
            case DamageKind.Pulse: return new Color(1f, 0.9f, 0.6f);
            default: return new Color(1f, 0.96f, 0.82f);
        }
    }

    public static void Blast(Vector3 point, float radius, Color color) {
        Fx.Explosion(point, Mathf.Max(2f, radius), color);
        GameAudio.Play("launch", 0.35f, Random.Range(0.9f, 1.15f));
    }

    // A jagged bolt between two points.
    public static void Zap(Vector3 from, Vector3 to) {
        Zap(from, to, 0.14f, true);
    }

    // Width in meters at the start; flash: a soft flash where it lands.
    public static void Zap(Vector3 from, Vector3 to, float width, bool flash) {
        var go = new GameObject("Zap");
        var line = go.AddComponent<LineRenderer>();
        int points = 7;
        line.positionCount = points;
        Vector3 side = Vector3.Cross((to - from).normalized, Vector3.up);
        float jag = Mathf.Min(1f, width / 0.14f);
        for (int i = 0; i < points; i++) {
            float t = i / (float)(points - 1);
            Vector3 p = Vector3.Lerp(from, to, t);
            if (i > 0 && i < points - 1) {
                p += (side * Random.Range(-0.5f, 0.5f) + Vector3.up * Random.Range(-0.4f, 0.4f)) * jag;
            }
            line.SetPosition(i, p);
        }
        line.startWidth = width;
        line.endWidth = width * 0.43f;
        if (flash) {
            Fx.Emit(FxKind.Flash, to, 1, new Color(0.55f, 0.8f, 1f, 0.9f), 1.1f, 0f);
        }
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) {
            line.material = new Material(shader);
        }
        line.startColor = new Color(0.8f, 0.95f, 1f);
        line.endColor = new Color(0.4f, 0.7f, 1f);
        Flourish flourish = go.AddComponent<Flourish>();
        flourish.Begin(Vector3.zero, 0.14f, Vector3.one);
        flourish.hideLine = true;
    }

    // A flat ring that expands from a point: encore, second wind.
    public static void Ring(Vector3 point, float radius, Color color, float life) {
        Fx.Ring(point, radius * 2f, new Color(color.r, color.g, color.b, 0.8f), life);
        Fx.Ring(point, radius * 1.6f, new Color(color.r, color.g, color.b, 0.4f), life * 0.8f);
    }

    // A shower of plates, like a bot shaking loose as it leaves.
    public static void Scrap(Vector3 point, int count, bool boss) {
        Color plate = boss ? new Color(1f, 0.9f, 0.55f) : new Color(0.75f, 0.72f, 0.66f);
        Fx.Emit(FxKind.Debris, point + Vector3.up * 0.6f, count, plate, boss ? 0.42f : 0.3f, boss ? 4.5f : 2.6f, 1.5f);
    }

    public static void WaveCleared(int wave) {
        Say("Wave " + wave + " cleared", 2.2f, 150f);
    }

    public static void Arrive(string bot) {
        Say(bot + " arrives", 2.6f);
    }

    public static void PlaceDust(Vector3 point) {
        Fx.Emit(FxKind.Dust, point + Vector3.up * 0.15f, 8, new Color(0.7f, 0.64f, 0.52f, 0.6f), 0.9f, 1.6f, 2f);
    }

    public static void Crack(Vector3 point) {
        Fx.Emit(FxKind.Dust, point + Vector3.up * 0.3f, 2, new Color(0.6f, 0.52f, 0.42f, 0.5f), 0.5f, 1f);
    }

    public static void BreakBurst(Vector3 point) {
        Fx.Emit(FxKind.Dust, point + Vector3.up * 0.4f, 10, new Color(0.5f, 0.42f, 0.34f, 0.7f), 1.2f, 2.5f, 2f);
    }

    public static void Say(string line, float seconds, float y = 80f) {
        var go = new GameObject("Callout");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var textGo = new GameObject("Line");
        textGo.transform.SetParent(go.transform, false);
        var text = textGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 42;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 0.92f, 0.72f);
        text.text = line;
        var rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(1200f, 80f);
        if (UiFit.Compact) {
            UiFit.Apply(scaler);
            text.fontSize = 32;
            rect.sizeDelta = new Vector2(UiFit.Size.x - 48f, 80f);
        }
        LoadoutBanner banner = go.AddComponent<LoadoutBanner>();
        banner.SetLife(seconds);
    }

    public static void Paint(GameObject go, Color color) {
        Renderer renderer = go.GetComponent<Renderer>();
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null || renderer == null) {
            return;
        }
        renderer.sharedMaterial = new Material(shader);
        renderer.sharedMaterial.color = color;
    }
}

public class Flourish : MonoBehaviour {
    public bool hideLine;
    Vector3 velocity;
    float life = 0.3f;
    float age;
    Vector3 endScale;
    bool armed;
    bool fade;
    Material faded;
    float startAlpha;

    public void Fade() {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null || renderer.sharedMaterial == null) {
            return;
        }
        fade = true;
        faded = renderer.sharedMaterial;
        startAlpha = faded.color.a;
    }

    void OnDestroy() {
        if (faded != null) {
            Destroy(faded);
        }
    }

    public void Begin(Vector3 velocity, float life, Vector3 endScale) {
        this.velocity = velocity;
        this.life = Mathf.Max(0.05f, life);
        this.endScale = endScale;
        armed = true;
    }

    void Update() {
        if (!armed) {
            return;
        }
        age += Time.deltaTime;
        transform.position += velocity * Time.deltaTime;
        float u = Mathf.Clamp01(age / life);
        if (!hideLine && endScale.sqrMagnitude > 0.0001f) {
            transform.localScale = Vector3.Lerp(transform.localScale, endScale, Time.deltaTime * 8f);
        }
        if (fade && faded != null) {
            Color c = faded.color;
            c.a = startAlpha * (1f - u * u);
            faded.color = c;
        }
        if (u >= 1f) {
            Destroy(gameObject);
        }
    }
}

public class FloatUp : MonoBehaviour {
    float left = 0.7f;

    void Update() {
        transform.position += Vector3.up * Time.deltaTime * 1.8f;
        if (Camera.main != null) {
            transform.rotation = Camera.main.transform.rotation;
        }
        left -= Time.deltaTime;
        if (left <= 0f) {
            Destroy(gameObject);
        }
    }
}

public class LoadoutBanner : MonoBehaviour {
    float left = 4.2f;

    public void SetLife(float seconds) {
        left = seconds;
    }

    void Update() {
        left -= Time.deltaTime;
        if (left <= 0f) {
            Destroy(gameObject);
        }
    }
}

// The marks on the bat, rebuilt whenever a boon or a bat shop rank changes. The stand-in bat runs up its local y:
// the handle from about -0.08 to 0.42, the barrel from 0.3 to 1.02 (0.11 across). Soft looks only (glow shells,
// flame tongues, rings, a thin arc now and then): no spark or ember particles.
public class BatMarks : MonoBehaviour {
    public static BatMarks Instance { get; private set; }
    public Transform Owner;
    public string Line { get; private set; } = "";

    static readonly Color Wood = new Color(0.5f, 0.36f, 0.22f);
    static readonly Color Grip = new Color(0.12f, 0.09f, 0.08f);
    static readonly Color Aluminum = new Color(0.76f, 0.79f, 0.84f);

    Transform marks;
    Transform pouch;
    Transform trailTip;
    int stamp = -1;
    float zapIn;
    readonly List<Transform> flames = new List<Transform>();
    readonly List<Material> shells = new List<Material>();
    readonly List<float> shellAlpha = new List<float>();

    void Awake() {
        Instance = this;
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
        if (pouch != null) {
            Destroy(pouch.gameObject);
        }
    }

    void Update() {
        int now = Stamp();
        if (now != stamp) {
            stamp = now;
            Rebuild();
        }
        float t = Time.time;
        for (int i = 0; i < flames.Count; i++) {
            if (flames[i] != null) {
                Vector3 size = flames[i].localScale;
                size.y = 0.13f * (1f + 0.3f * Mathf.Sin(t * 13f + i * 2.1f));
                flames[i].localScale = size;
            }
        }
        for (int i = 0; i < shells.Count; i++) {
            if (shells[i] != null) {
                Color c = shells[i].color;
                c.a = shellAlpha[i] * (0.75f + 0.25f * Mathf.Sin(t * 5f + i));
                shells[i].color = c;
            }
        }
        // Live Wire: a thin arc crawls up the barrel now and then.
        int wire = RunBoons.Rank(BoonId.Chain);
        bool thunder = RunBoons.Has(BoonId.StormQuiver);
        if (wire > 0 || thunder) {
            zapIn -= Time.deltaTime;
            if (zapIn <= 0f) {
                zapIn = thunder ? Random.Range(0.25f, 0.5f) : Random.Range(0.9f, 1.5f) / wire;
                float size = transform.lossyScale.y;
                Vector3 from = transform.TransformPoint(new Vector3(Random.Range(-0.05f, 0.05f), 0.4f, 0f));
                Vector3 to = transform.TransformPoint(new Vector3(Random.Range(-0.05f, 0.05f), 1f, Random.Range(-0.05f, 0.05f)));
                UpgradeLook.Zap(from, to, 0.025f * size, false);
            }
        }
        if (pouch != null && Owner != null) {
            float grown = PlayerGrowth.Instance != null ? PlayerGrowth.Instance.Scale : 1f;
            float unit = Mathf.Max(0.0001f, Mathf.Abs(Owner.lossyScale.x));
            pouch.localPosition = new Vector3(-0.3f, 0.95f, -0.12f) * grown / unit;
            pouch.localScale = Vector3.one * grown / unit;
        }
    }

    static int Stamp() {
        int h = 17;
        for (int i = 0; i < RunBoons.All.Length; i++) {
            h = h * 7 + RunBoons.Rank((BoonId)i);
        }
        h = h * 5 + MetaProgress.Rank(UpgradeId.Keen);
        h = h * 5 + MetaProgress.Rank(UpgradeId.Blast);
        return h;
    }

    void Rebuild() {
        flames.Clear();
        shells.Clear();
        shellAlpha.Clear();
        if (marks != null) {
            Destroy(marks.gameObject);
        }
        if (pouch != null) {
            Destroy(pouch.gameObject);
            pouch = null;
        }
        marks = new GameObject("Marks").transform;
        marks.SetParent(transform, false);
        var said = new List<string>();

        int metal = MetaProgress.Rank(UpgradeId.Blast);
        int corked = RunBoons.Rank(BoonId.Heavy);
        bool tar = RunBoons.Has(BoonId.Firebrand) || RunBoons.Has(BoonId.Phoenix);
        bool blizzard = RunBoons.Has(BoonId.Hailstorm);
        bool thunder = RunBoons.Has(BoonId.StormQuiver);

        // The bat itself: wood, or aluminum from the shop. Pine Tar darkens wood; the evolutions tint it.
        Color barrel = metal > 0 ? Aluminum : Wood;
        if (tar && metal == 0) {
            barrel = Color.Lerp(barrel, new Color(0.22f, 0.13f, 0.07f), 0.55f);
        }
        if (blizzard) {
            barrel = Color.Lerp(barrel, new Color(0.9f, 0.96f, 1f), 0.5f);
        }
        if (thunder) {
            barrel = Color.Lerp(barrel, new Color(0.32f, 0.38f, 0.55f), 0.4f);
        }
        Transform barrelPart = transform.Find("Barrel");
        Transform handlePart = transform.Find("Handle");
        if (barrelPart != null) {
            UpgradeLook.Paint(barrelPart.gameObject, barrel);
            // Corked Bat: a fatter barrel each rank.
            float thick = 0.11f * (1f + 0.07f * corked);
            barrelPart.localScale = new Vector3(thick, 0.36f, thick);
        }
        if (handlePart != null) {
            UpgradeLook.Paint(handlePart.gameObject, metal > 0 ? new Color(0.06f, 0.06f, 0.07f) : Grip);
        }
        float r = 0.055f * (1f + 0.07f * corked);
        said.Add(metal > 0 ? "aluminum bat" : "wood bat");
        if (corked > 0) {
            said.Add("Corked Bat: barrel " + Mathf.RoundToInt(7f * corked) + "% fatter");
        }
        if (metal > 0) {
            // Dents, more with rank, and a bright band where the metal catches the light.
            for (int i = 0; i < 1 + metal; i++) {
                float y = 0.5f + i * 0.17f;
                float a = i * 2.3f;
                Vector3 at = new Vector3(Mathf.Sin(a) * r * 0.85f, y, Mathf.Cos(a) * r * 0.85f);
                UpgradeLook.MarkPart(marks, PrimitiveType.Sphere, at, Quaternion.LookRotation(new Vector3(at.x, 0f, at.z)), new Vector3(0.05f, 0.05f, 0.022f), new Color(0.48f, 0.5f, 0.55f));
            }
            Band(0.97f, r, 0.02f, new Color(0.95f, 0.97f, 1f));
            said.Add("Aluminum! Not wood!: " + (1 + metal) + " dents");
        }

        // Corked Core (shop): a cork plug in the end of the barrel, wider each rank.
        int core = MetaProgress.Rank(UpgradeId.Keen);
        if (core > 0) {
            float plug = r * (0.9f + 0.25f * core);
            UpgradeLook.MarkPart(marks, PrimitiveType.Cylinder, new Vector3(0f, 1.02f, 0f), Quaternion.identity, new Vector3(plug, 0.025f, plug), new Color(0.78f, 0.6f, 0.38f));
            said.Add("Corked Core: cork plug in the end");
        }

        // Quick Hands: white grip tape, a wrap per rank.
        int quick = RunBoons.Rank(BoonId.QuickDraw);
        for (int i = 0; i < quick; i++) {
            UpgradeLook.MarkPart(marks, PrimitiveType.Cylinder, new Vector3(0f, 0.02f + i * 0.07f, 0f), Quaternion.Euler(12f, 0f, 0f), new Vector3(0.068f, 0.014f, 0.068f), new Color(0.95f, 0.95f, 0.92f));
        }
        if (quick > 0) {
            said.Add("Quick Hands: grip tape");
        }

        // Follow Through: brass bands round the barrel.
        int follow = RunBoons.Rank(BoonId.Pierce);
        for (int i = 0; i < follow; i++) {
            Band(0.46f + i * 0.06f, r, 0.016f, new Color(0.95f, 0.75f, 0.3f));
        }
        if (follow > 0) {
            said.Add("Follow Through: " + follow + " brass band" + (follow == 1 ? "" : "s"));
        }

        // Sweet Spot: a red ring where the sweet spot starts, brighter with rank.
        int sweet = RunBoons.Rank(BoonId.EagleEye);
        if (sweet > 0) {
            Band(0.8f, r, 0.03f, Color.Lerp(new Color(0.7f, 0.15f, 0.12f), new Color(1f, 0.3f, 0.25f), sweet / 5f));
            said.Add("Sweet Spot: red ring on the tip");
        }

        // Line Drive: a chalk stripe along the barrel.
        int line = RunBoons.Rank(BoonId.Ricochet);
        if (line > 0) {
            UpgradeLook.MarkPart(marks, PrimitiveType.Cube, new Vector3(r * 0.92f, 0.66f, 0f), Quaternion.identity, new Vector3(0.012f, 0.42f, 0.02f + 0.008f * line), new Color(0.98f, 0.98f, 0.96f));
            said.Add("Line Drive: chalk stripe");
        }

        // Grand Slam: a gold knob.
        int slam = RunBoons.Rank(BoonId.Volley);
        if (slam > 0) {
            UpgradeLook.MarkPart(marks, PrimitiveType.Sphere, new Vector3(0f, -0.09f, 0f), Quaternion.identity, Vector3.one * (0.08f + 0.012f * slam), new Color(1f, 0.82f, 0.3f));
            said.Add("Grand Slam: gold knob");
        }

        // Called Shot: a red ribbon off the knob.
        int called = RunBoons.Rank(BoonId.Mark);
        if (called > 0) {
            UpgradeLook.MarkPart(marks, PrimitiveType.Cube, new Vector3(0.03f, -0.18f, 0f), Quaternion.Euler(0f, 0f, 12f), new Vector3(0.012f, 0.16f + 0.03f * called, 0.04f), new Color(0.9f, 0.16f, 0.14f));
            said.Add("Called Shot: red ribbon");
        }

        // Wide Arc: the bat leaves a pale swoosh through the air, wider with rank.
        int wide = RunBoons.Rank(BoonId.SplitShot);
        Trail(wide, blizzard);
        if (wide > 0) {
            said.Add("Wide Arc: swing trail");
        }

        // Pine Tar: flame tongues licking the barrel; Phoenix Bat makes them bigger and redder.
        if (tar) {
            bool phoenix = RunBoons.Has(BoonId.Phoenix);
            Color flame = phoenix ? new Color(1f, 0.32f, 0.08f, 0.75f) : new Color(1f, 0.55f, 0.15f, 0.65f);
            Shell(0.74f, r * 2.4f, 0.27f, new Color(1f, 0.5f, 0.15f, phoenix ? 0.3f : 0.2f));
            int tongues = phoenix ? 6 : 4;
            for (int i = 0; i < tongues; i++) {
                float a = i * Mathf.PI * 2f / tongues;
                Vector3 at = new Vector3(Mathf.Sin(a) * r * 1.1f, 0.62f + (i % 2) * 0.16f, Mathf.Cos(a) * r * 1.1f);
                GameObject tongue = UpgradeLook.GlassPart(marks, PrimitiveType.Capsule, at, Quaternion.identity, new Vector3(0.05f, 0.13f, 0.05f) * (phoenix ? 1.3f : 1f), flame);
                flames.Add(tongue.transform);
            }
            said.Add(phoenix ? "Phoenix Bat: big flames on the barrel" : "Pine Tar: flames on the barrel");
        }

        // Ice Bat: a frost shell and ice crystals; Blizzard Bat adds icicles.
        if (RunBoons.Has(BoonId.Frost) || blizzard) {
            Shell(0.7f, r * 2.3f, 0.3f, new Color(0.7f, 0.9f, 1f, blizzard ? 0.35f : 0.22f));
            for (int i = 0; i < 4; i++) {
                float a = i * 1.7f + 0.4f;
                Vector3 at = new Vector3(Mathf.Sin(a) * r, 0.5f + i * 0.13f, Mathf.Cos(a) * r);
                UpgradeLook.MarkPart(marks, PrimitiveType.Cube, at, Quaternion.Euler(45f, a * Mathf.Rad2Deg, 45f), Vector3.one * 0.035f, new Color(0.85f, 0.95f, 1f));
            }
            if (blizzard) {
                for (int i = 0; i < 5; i++) {
                    float a = i * 1.26f;
                    UpgradeLook.MarkPart(marks, PrimitiveType.Cube, new Vector3(Mathf.Sin(a) * r * 1.1f, 0.85f - i * 0.08f, Mathf.Cos(a) * r * 1.1f), Quaternion.identity, new Vector3(0.018f, 0.09f, 0.018f), new Color(0.92f, 0.98f, 1f));
                }
            }
            said.Add(blizzard ? "Blizzard Bat: frosted white with icicles" : "Ice Bat: frost on the barrel");
        }

        // Live Wire: a copper coil and a blue glow (and the arcs from Update); Thunder Bat glows brighter.
        int wire = RunBoons.Rank(BoonId.Chain);
        if (wire > 0 || thunder) {
            for (int i = 0; i < 3 + wire; i++) {
                UpgradeLook.MarkPart(marks, PrimitiveType.Cylinder, new Vector3(0f, 0.4f + i * 0.09f, 0f), Quaternion.Euler(18f, 0f, 0f), new Vector3(r * 2.1f, 0.008f, r * 2.1f), new Color(0.85f, 0.5f, 0.25f));
            }
            Shell(0.66f, r * 2.6f, 0.36f, new Color(0.55f, 0.8f, 1f, thunder ? 0.32f : 0.16f));
            said.Add(thunder ? "Thunder Bat: crackling blue glow" : "Live Wire: copper coil and arcs");
        }

        // Long Ball: a pouch of bolts on Wallit's hip.
        int longBall = RunBoons.Rank(BoonId.Homing);
        if (longBall > 0 && Owner != null) {
            pouch = new GameObject("BoltPouch").transform;
            pouch.SetParent(Owner, false);
            UpgradeLook.MarkPart(pouch, PrimitiveType.Cube, Vector3.zero, Quaternion.identity, new Vector3(0.16f, 0.2f, 0.12f), new Color(0.42f, 0.28f, 0.16f));
            for (int i = 0; i < 1 + longBall; i++) {
                Transform bolt = UpgradeLook.BuildBolt(pouch, 0.35f);
                bolt.localPosition = new Vector3(-0.04f + i * 0.05f, 0.13f, 0f);
            }
            said.Add("Long Ball: bolt pouch on the hip");
        }

        Line = string.Join(", ", said.ToArray());
    }

    void Band(float y, float r, float height, Color color) {
        UpgradeLook.MarkPart(marks, PrimitiveType.Cylinder, new Vector3(0f, y, 0f), Quaternion.identity, new Vector3(r * 2.12f, height, r * 2.12f), color);
    }

    void Shell(float y, float width, float height, Color color) {
        GameObject shell = UpgradeLook.GlassPart(marks, PrimitiveType.Capsule, new Vector3(0f, y, 0f), Quaternion.identity, new Vector3(width, height, width), color);
        Renderer skin = shell.GetComponent<Renderer>();
        if (skin != null && skin.sharedMaterial != null) {
            shells.Add(skin.sharedMaterial);
            shellAlpha.Add(color.a);
        }
    }

    // A swoosh behind the tip while the bat moves.
    void Trail(int wide, bool frost) {
        if (trailTip != null) {
            Destroy(trailTip.gameObject);
            trailTip = null;
        }
        if (wide <= 0) {
            return;
        }
        trailTip = new GameObject("SwingTrail").transform;
        trailTip.SetParent(transform, false);
        trailTip.localPosition = new Vector3(0f, 0.95f, 0f);
        TrailRenderer trail = trailTip.gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.16f + 0.03f * wide;
        trail.widthMultiplier = 1f;
        trail.startWidth = (0.12f + 0.04f * wide) * Mathf.Max(1f, transform.lossyScale.y);
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.04f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) {
            trail.material = new Material(shader);
        }
        Color c = frost ? new Color(0.8f, 0.94f, 1f, 0.55f) : new Color(1f, 0.97f, 0.88f, 0.45f);
        trail.startColor = c;
        trail.endColor = new Color(c.r, c.g, c.b, 0f);
    }
}
