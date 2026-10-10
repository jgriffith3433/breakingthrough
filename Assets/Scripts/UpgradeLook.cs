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
        go.AddComponent<LoadoutBanner>();
    }

    public static void KeenArrow(GameObject shot) {
        int rank = MetaProgress.Rank(UpgradeId.Keen);
        if (rank <= 0 || shot == null) {
            return;
        }
        shot.transform.localScale *= 1f + 0.08f * rank;
        TrailRenderer trail = shot.AddComponent<TrailRenderer>();
        trail.time = 0.16f + 0.03f * rank;
        trail.startWidth = 0.05f + 0.018f * rank;
        trail.endWidth = 0f;
        trail.numCapVertices = 2;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) {
            trail.material = new Material(shader);
        }
        Color gold = new Color(1f, 0.82f, 0.28f, 1f);
        trail.startColor = gold;
        trail.endColor = new Color(gold.r, gold.g, gold.b, 0f);
    }

    public static void BowMark(Transform bow) {
        int rank = MetaProgress.Rank(UpgradeId.Draw);
        if (rank <= 0 || bow == null) {
            return;
        }
        var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(band.GetComponent<Collider>());
        band.name = "QuickDraw";
        band.transform.SetParent(bow, false);
        band.transform.localPosition = new Vector3(0f, 0.12f, 0.05f);
        band.transform.localScale = new Vector3(0.9f, 0.12f, 0.28f);
        Paint(band, Color.Lerp(new Color(0.85f, 0.55f, 0.2f), new Color(1f, 0.9f, 0.35f), rank / 4f));
    }

    public static void Blast(Vector3 point, float radius) {
        int rank = MetaProgress.Rank(UpgradeId.Blast);
        float size = Mathf.Max(1.2f, radius) * (0.55f + 0.12f * rank);
        Spawn(point, Vector3.one * 0.35f, new Color(1f, 0.55f, 0.18f), 0.28f, Vector3.up * 0.2f, size);
        if (rank > 0) {
            Spawn(point, Vector3.one * 0.2f, new Color(1f, 0.9f, 0.4f), 0.34f, Vector3.up * 1.2f, size * 0.45f);
        }
        GameAudio.Play("launch", 0.45f + 0.08f * rank);
    }

    public static void Coins(Vector3 point, int count) {
        int n = Mathf.Max(1, count);
        for (int i = 0; i < n; i++) {
            Vector3 toss = new Vector3((i - (n - 1) * 0.5f) * 1.4f, 4.5f, 0.2f);
            Spawn(point + Vector3.up * 0.8f, Vector3.one * 0.34f, new Color(1f, 0.82f, 0.22f), 0.9f, toss, 0.08f);
        }
    }

    public static void Grow(Vector3 point, float body) {
        int rank = MetaProgress.Rank(UpgradeId.Growth);
        if (rank <= 0) {
            return;
        }
        float radius = body * (0.8f + 0.15f * rank);
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ring.GetComponent<Collider>());
        ring.name = "Growth";
        ring.transform.position = point + Vector3.up * 0.08f;
        ring.transform.localScale = new Vector3(radius, 0.04f, radius);
        Paint(ring, new Color(0.55f, 0.95f, 0.45f));
        Flourish flourish = ring.AddComponent<Flourish>();
        flourish.Begin(Vector3.zero, 0.45f, ring.transform.localScale * (1.3f + 0.1f * rank));
    }

    public static void Stride(Vector3 feet) {
        int rank = MetaProgress.Rank(UpgradeId.Stride);
        if (rank <= 0 || Time.time < nextStride) {
            return;
        }
        nextStride = Time.time + Mathf.Lerp(0.22f, 0.1f, rank / 5f);
        Spawn(feet + Vector3.up * 0.05f, new Vector3(0.35f, 0.06f, 0.35f), new Color(0.75f, 0.68f, 0.48f), 0.28f, Vector3.up * 0.4f, 0.7f + 0.08f * rank);
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
        Color gold = Color.Lerp(new Color(1f, 0.75f, 0.3f), new Color(1f, 0.95f, 0.6f), rank / 4f);
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

    public static bool HitConfirm { get { return Time.time < hitUntil; } }
    static float hitUntil;

    public static void MarkHit() {
        hitUntil = Time.time + 0.14f;
    }

    public static void Spark(Vector3 point) {
        Spawn(point, Vector3.one * 0.12f, new Color(1f, 0.92f, 0.55f), 0.18f, Vector3.up * 1.5f, 0.35f);
        Spawn(point, Vector3.one * 0.08f, new Color(1f, 0.55f, 0.2f), 0.22f, new Vector3(0.8f, 1.2f, 0f), 0.2f);
    }

    public static void Number(Vector3 point, int amount) {
        var go = new GameObject("Damage");
        go.transform.position = point + Vector3.up * 0.4f;
        TextMesh mesh = go.AddComponent<TextMesh>();
        mesh.text = amount.ToString();
        mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mesh.fontSize = 64;
        mesh.characterSize = 0.045f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.color = new Color(1f, 0.96f, 0.82f);
        Renderer meshRenderer = mesh.GetComponent<Renderer>();
        if (meshRenderer != null && mesh.font != null && mesh.font.material != null) {
            meshRenderer.material = mesh.font.material;
        }
        go.AddComponent<FloatUp>();
    }

    public static void Feathers(Vector3 point, int count, bool boss) {
        Color feather = boss ? new Color(1f, 0.9f, 0.55f) : new Color(0.9f, 0.7f, 0.4f);
        for (int i = 0; i < count; i++) {
            Vector3 toss = Random.insideUnitSphere;
            toss.y = Mathf.Abs(toss.y) + 0.6f;
            toss *= boss ? 4.5f : 2.4f;
            Spawn(point + Vector3.up * 0.6f, new Vector3(0.16f, 0.05f, 0.08f), feather, boss ? 0.7f : 0.45f, toss, 0.04f);
        }
    }

    public static void WaveCleared(int wave, Vector3 point) {
        Say("Wave " + wave + " cleared", 2.2f, 150f);
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Collider solid = ring.GetComponent<Collider>();
        if (solid != null) {
            Object.DestroyImmediate(solid);
        }
        ring.name = "WaveClear";
        ring.transform.position = point + Vector3.up * 0.08f;
        ring.transform.localScale = new Vector3(2.2f, 0.04f, 2.2f);
        Paint(ring, new Color(1f, 0.84f, 0.35f));
        Flourish flourish = ring.AddComponent<Flourish>();
        flourish.Begin(Vector3.zero, 0.7f, new Vector3(8f, 0.04f, 8f));
    }

    public static void Arrive(string bird) {
        Say(bird + " arrives", 2.6f);
    }

    public static void PlaceDust(Vector3 point) {
        Spawn(point + Vector3.up * 0.15f, new Vector3(0.5f, 0.08f, 0.5f), new Color(0.8f, 0.72f, 0.5f), 0.28f, Vector3.up * 0.6f, 1.4f);
    }

    public static void Crack(Vector3 point) {
        Spawn(point + Vector3.up * 0.4f, Vector3.one * 0.1f, new Color(0.85f, 0.55f, 0.3f), 0.2f, Vector3.up * 1.4f, 0.28f);
    }

    public static void BreakBurst(Vector3 point) {
        for (int i = 0; i < 6; i++) {
            Vector3 toss = Random.insideUnitSphere;
            toss.y = Mathf.Abs(toss.y) + 0.4f;
            Spawn(point + Vector3.up * 0.4f, Vector3.one * 0.16f, new Color(0.55f, 0.4f, 0.28f), 0.45f, toss * 3f, 0.05f);
        }
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
