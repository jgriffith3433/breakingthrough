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

    public static GameObject BuildRocket() {
        var root = new GameObject("Rocket");
        Quaternion along = Quaternion.Euler(-90f, 0f, 0f);
        Color body = new Color(0.72f, 0.16f, 0.12f);
        Color metal = new Color(0.92f, 0.86f, 0.74f);
        Part(root.transform, PrimitiveType.Cylinder, Vector3.zero, along, new Vector3(0.16f, 0.36f, 0.16f), body);
        Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 0f, 0.46f), Quaternion.identity, Vector3.one * 0.2f, metal);
        Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.08f), along, new Vector3(0.2f, 0.045f, 0.2f), new Color(0.95f, 0.78f, 0.28f));
        Part(root.transform, PrimitiveType.Cube, new Vector3(0.14f, 0f, -0.28f), Quaternion.identity, new Vector3(0.16f, 0.03f, 0.14f), body);
        Part(root.transform, PrimitiveType.Cube, new Vector3(-0.14f, 0f, -0.28f), Quaternion.identity, new Vector3(0.16f, 0.03f, 0.14f), body);
        Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.14f, -0.28f), Quaternion.identity, new Vector3(0.03f, 0.16f, 0.14f), body);
        Part(root.transform, PrimitiveType.Cube, new Vector3(0f, -0.14f, -0.28f), Quaternion.identity, new Vector3(0.03f, 0.16f, 0.14f), body);
        Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 0f, -0.46f), Quaternion.identity, Vector3.one * 0.1f, new Color(1f, 0.62f, 0.16f));
        return root;
    }

    public static void LightRocket(GameObject shot, Vector3 longLocal) {
        if (shot == null) {
            return;
        }
        int rank = Mathf.Max(1, MetaProgress.Rank(UpgradeId.Blast));
        var flame = new GameObject("Flame");
        flame.transform.SetParent(shot.transform, false);
        Vector3 tail = longLocal.sqrMagnitude > 0.0001f ? -longLocal.normalized : Vector3.back;
        flame.transform.localPosition = tail * 0.36f;
        TrailRenderer trail = flame.AddComponent<TrailRenderer>();
        trail.time = 0.2f + 0.05f * rank;
        trail.startWidth = 0.07f + 0.03f * rank;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.05f;
        trail.numCapVertices = 4;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) {
            trail.material = new Material(shader);
        }
        trail.startColor = new Color(1f, 0.55f, 0.12f, 1f);
        trail.endColor = new Color(1f, 0.28f, 0.05f, 0f);
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

    public static void KeenArrow(GameObject shot) {
        int rank = MetaProgress.Rank(UpgradeId.Keen);
        if (rank <= 0 || shot == null) {
            return;
        }
        shot.transform.localScale *= 1f + 0.22f * rank;
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
        var go = new GameObject("Zap");
        var line = go.AddComponent<LineRenderer>();
        int points = 7;
        line.positionCount = points;
        Vector3 side = Vector3.Cross((to - from).normalized, Vector3.up);
        for (int i = 0; i < points; i++) {
            float t = i / (float)(points - 1);
            Vector3 p = Vector3.Lerp(from, to, t);
            if (i > 0 && i < points - 1) {
                p += side * Random.Range(-0.5f, 0.5f) + Vector3.up * Random.Range(-0.4f, 0.4f);
            }
            line.SetPosition(i, p);
        }
        line.startWidth = 0.14f;
        line.endWidth = 0.06f;
        Fx.Emit(FxKind.Flash, to, 1, new Color(0.55f, 0.8f, 1f, 0.9f), 1.1f, 0f);
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

    // Arrows show what they carry: fire, frost, lightning.
    public static void BoonTrail(GameObject shot, bool volley) {
        if (shot == null || MetaProgress.Rank(UpgradeId.Keen) > 0) {
            return;
        }
        Color tint;
        if (RunBoons.Has(BoonId.Phoenix) || RunBoons.Has(BoonId.Firebrand)) {
            tint = new Color(1f, 0.5f, 0.15f);
        } else if (RunBoons.Has(BoonId.Frost) || (volley && RunBoons.Has(BoonId.Hailstorm))) {
            tint = new Color(0.6f, 0.9f, 1f);
        } else if (RunBoons.Has(BoonId.Chain) || RunBoons.Has(BoonId.StormQuiver)) {
            tint = new Color(0.55f, 0.75f, 1f);
        } else if (volley) {
            tint = new Color(1f, 1f, 1f);
        } else {
            return;
        }
        TrailRenderer trail = shot.AddComponent<TrailRenderer>();
        trail.time = 0.14f;
        trail.startWidth = 0.08f;
        trail.endWidth = 0f;
        trail.numCapVertices = 2;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) {
            trail.material = new Material(shader);
        }
        trail.startColor = tint;
        trail.endColor = new Color(tint.r, tint.g, tint.b, 0f);
    }

    public static void Feathers(Vector3 point, int count, bool boss) {
        Color feather = boss ? new Color(1f, 0.9f, 0.55f) : new Color(0.9f, 0.7f, 0.4f);
        Fx.Emit(FxKind.Feather, point + Vector3.up * 0.6f, count, feather, boss ? 0.5f : 0.3f, boss ? 4.5f : 2.6f, 1.5f);
    }

    public static void WaveCleared(int wave) {
        Say("Wave " + wave + " cleared", 2.2f, 150f);
    }

    public static void Arrive(string bird) {
        Say(bird + " arrives", 2.6f);
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
