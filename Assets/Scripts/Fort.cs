using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum FortKind {
    Wall,
    Spike,
    Tower
}

public static class FortStock {
    public static int Walls;
    public static int Spikes;
    public static int Towers;

    public static void BeginRun() {
        Walls = 6 + MetaProgress.Rank(UpgradeId.Walls) * 2;
        Spikes = 3 + MetaProgress.Rank(UpgradeId.Spikes);
        Towers = 1 + MetaProgress.Rank(UpgradeId.Towers);
    }

    public static int Count(FortKind kind) {
        switch (kind) {
            case FortKind.Spike: return Spikes;
            case FortKind.Tower: return Towers;
            default: return Walls;
        }
    }

    public static int Price(FortKind kind) {
        switch (kind) {
            case FortKind.Spike: return 2;
            case FortKind.Tower: return 3;
            default: return 1;
        }
    }

    public static bool Take(FortKind kind) {
        switch (kind) {
            case FortKind.Spike:
                if (Spikes <= 0) return false;
                Spikes--;
                return true;
            case FortKind.Tower:
                if (Towers <= 0) return false;
                Towers--;
                return true;
            default:
                if (Walls <= 0) return false;
                Walls--;
                return true;
        }
    }

    public static void Add(FortKind kind) {
        switch (kind) {
            case FortKind.Spike: Spikes++; break;
            case FortKind.Tower: Towers++; break;
            default: Walls++; break;
        }
    }
}

public class DefencePiece : MonoBehaviour {
    public const float KeepOut = 6.5f;
    public const float OuterRadius = 12.5f;
    public const float SpawnMin = 17f;
    public const float SpawnMax = 24f;
    public const int WallStops = 8;
    public const int SpikeKills = 4;
    public const int TowerKills = 6;
    public const int TowerStops = 8;

    public FortKind Kind;
    public bool Blocks = true;
    public bool Alive { get { return !spent; } }

    int stopsLeft = 1;
    int stopMax = 1;
    int killsLeft = 1;
    int killMax = 1;
    bool spent;
    float spikeTick;
    float shotTick;
    Renderer[] skins;
    Color baseColor;
    Dictionary<int, int> presses = new Dictionary<int, int>();

    public static DefencePiece Place(FortKind kind, Vector3 position, Quaternion rotation) {
        var go = new GameObject(kind.ToString());
        go.transform.SetPositionAndRotation(position, rotation);
        DefencePiece piece = go.AddComponent<DefencePiece>();
        piece.Kind = kind;
        piece.Build();
        return piece;
    }

    void Build() {
        if (Kind == FortKind.Wall) {
            stopsLeft = stopMax = MetaProgress.WallStops();
            Blocks = true;
            baseColor = new Color(0.45f, 0.32f, 0.2f);
            AddBox(new Vector3(1.7f, 1.15f, 0.4f), new Vector3(0f, 0.58f, 0f), baseColor, false);
        } else if (Kind == FortKind.Spike) {
            killsLeft = killMax = MetaProgress.SpikeKills();
            Blocks = false;
            baseColor = new Color(0.35f, 0.36f, 0.4f);
            AddBox(new Vector3(1.35f, 0.12f, 1.35f), new Vector3(0f, 0.06f, 0f), baseColor, true);
            AddBox(new Vector3(0.12f, 0.45f, 0.12f), new Vector3(0.35f, 0.28f, 0.35f), new Color(0.75f, 0.76f, 0.8f), true);
            AddBox(new Vector3(0.12f, 0.45f, 0.12f), new Vector3(-0.35f, 0.28f, 0.35f), new Color(0.75f, 0.76f, 0.8f), true);
            AddBox(new Vector3(0.12f, 0.45f, 0.12f), new Vector3(0.35f, 0.28f, -0.35f), new Color(0.75f, 0.76f, 0.8f), true);
            AddBox(new Vector3(0.12f, 0.45f, 0.12f), new Vector3(-0.35f, 0.28f, -0.35f), new Color(0.75f, 0.76f, 0.8f), true);
        } else {
            stopsLeft = stopMax = MetaProgress.TowerStops();
            killsLeft = killMax = MetaProgress.TowerKills();
            Blocks = true;
            baseColor = new Color(0.32f, 0.34f, 0.38f);
            AddBox(new Vector3(0.55f, 1.5f, 0.55f), new Vector3(0f, 0.75f, 0f), baseColor, false);
            AddBox(new Vector3(0.85f, 0.28f, 0.85f), new Vector3(0f, 1.55f, 0f), new Color(0.55f, 0.42f, 0.22f), false);
        }
        skins = GetComponentsInChildren<Renderer>();
    }

    public static string PlaceLesson(FortKind kind) {
        switch (kind) {
            case FortKind.Spike:
                return "Kills " + MetaProgress.SpikeKills() + " turkeys, then breaks.";
            case FortKind.Tower:
                return "Kills " + MetaProgress.TowerKills() + " turkeys. Falls after " + MetaProgress.TowerStops() + " reach it.";
            default:
                return "Stops " + MetaProgress.WallStops() + " turkeys, then breaks.";
        }
    }

    void AddBox(Vector3 size, Vector3 center, Color color, bool trigger) {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "Block";
        box.transform.SetParent(transform, false);
        box.transform.localPosition = center;
        box.transform.localScale = size;
        Collider collider = box.GetComponent<Collider>();
        if (collider != null) {
            collider.isTrigger = trigger;
        }
        Renderer renderer = box.GetComponent<Renderer>();
        Shader shader = Shader.Find("Unlit/Color");
        if (shader != null && renderer != null) {
            renderer.sharedMaterial = new Material(shader);
            renderer.sharedMaterial.color = color;
        }
    }

    // Each new turkey costs one stop. A bird that stays and keeps pecking costs another every third peck.
    public void Stop(Enemy enemy) {
        if (spent || stopMax <= 1 || enemy == null) {
            return;
        }
        int id = enemy.GetInstanceID();
        int n;
        if (!presses.TryGetValue(id, out n)) {
            n = 0;
        }
        n++;
        presses[id] = n;
        if (n == 1 || n % 3 == 0) {
            stopsLeft--;
            PaintWear();
            if (stopsLeft <= 0) {
                Break();
            }
        }
    }

    public void NoteKill() {
        if (spent || killMax <= 1) {
            return;
        }
        killsLeft--;
        PaintWear();
        if (killsLeft <= 0) {
            Break();
        }
    }

    void PaintWear() {
        if (skins == null) {
            return;
        }
        float stopRatio = stopMax <= 1 ? 1f : stopsLeft / (float)stopMax;
        float killRatio = killMax <= 1 ? 1f : killsLeft / (float)killMax;
        Color worn = Color.Lerp(new Color(0.62f, 0.18f, 0.14f), baseColor, Mathf.Min(stopRatio, killRatio));
        foreach (Renderer skin in skins) {
            if (skin != null && skin.sharedMaterial != null) {
                skin.sharedMaterial.color = worn;
            }
        }
    }

    void Break() {
        if (spent) {
            return;
        }
        spent = true;
        foreach (Collider collider in GetComponentsInChildren<Collider>()) {
            collider.enabled = false;
        }
        Destroy(gameObject, 0.35f);
    }

    void Update() {
        if (!Alive) {
            transform.position += Vector3.down * Time.deltaTime * 0.8f;
            return;
        }
        if (Kind == FortKind.Spike) {
            TickSpikes();
        } else if (Kind == FortKind.Tower) {
            TickTower();
        }
    }

    void TickSpikes() {
        spikeTick -= Time.deltaTime;
        if (spikeTick > 0f) {
            return;
        }
        spikeTick = 0.4f;
        Collider[] caught = Physics.OverlapBox(transform.position + Vector3.up * 0.4f, new Vector3(0.7f, 0.55f, 0.7f), transform.rotation, ~0, QueryTriggerInteraction.Collide);
        foreach (Collider collider in caught) {
            Enemy enemy = collider.GetComponentInParent<Enemy>();
            if (enemy != null && enemy.Alive) {
                enemy.Hit(1f);
                if (!enemy.Alive) {
                    NoteKill();
                }
                if (!Alive) {
                    return;
                }
            }
        }
    }

    void TickTower() {
        shotTick -= Time.deltaTime;
        if (shotTick > 0f) {
            return;
        }
        Enemy best = null;
        float bestDist = 9.5f;
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy enemy in enemies) {
            if (enemy == null || !enemy.Alive) {
                continue;
            }
            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            if (dist < bestDist) {
                bestDist = dist;
                best = enemy;
            }
        }
        if (best == null) {
            return;
        }
        shotTick = 0.8f;
        best.Hit(1f);
        if (!best.Alive) {
            NoteKill();
        }
        GameAudio.Play("shot", 0.28f);
    }
}

// The build window. Mouse is free, the hotbar is the inventory, click drops a piece.
public class BuildMode : MonoBehaviour {
    public static BuildMode Instance { get; private set; }

    public FortKind Selected { get { return selected; } }
    public bool IsPlacing { get { return ghost != null && ghost.activeSelf; } }

    FortKind selected = FortKind.Wall;
    Image[] cards = new Image[3];
    float yawExtra;
    GameObject ghost;
    GameObject ring;
    GameObject bar;
    Text[] labels = new Text[3];
    Text[] prices = new Text[3];
    bool ghostOk;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        if (SceneManager.GetActiveScene().name != MetaProgress.GameScene) {
            return;
        }
        if (FindAnyObjectByType<BuildMode>() != null) {
            return;
        }
        var go = new GameObject("Build Mode");
        go.AddComponent<BuildMode>();
    }

    void Awake() {
        Instance = this;
    }

    void Start() {
        BuildBar();
        SetBar(false);
    }

    void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    void Update() {
        bool open = WaveDirector.IsBuilding;
        SetBar(open);
        if (!open) {
            HideGhost();
            if (ring != null) {
                ring.SetActive(false);
            }
            return;
        }

        ShowRing();
        if (Input.GetKeyDown(KeyCode.Tab)) {
            selected = selected == FortKind.Wall ? FortKind.Spike
                : selected == FortKind.Spike ? FortKind.Tower
                : FortKind.Wall;
        }
        if (Input.GetKeyDown(KeyCode.Q)) yawExtra -= 45f;
        if (Input.GetKeyDown(KeyCode.E)) yawExtra += 45f;

        RefreshLabels();
        Vector3 spot;
        Quaternion rotation;
        bool aimed = Aim(out spot, out rotation);
        ghostOk = aimed && Legal(spot);
        if (aimed) {
            ShowGhost(spot, rotation, ghostOk);
        } else {
            HideGhost();
        }

        bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (aimed && ghostOk && !overUi && Input.GetMouseButtonDown(0) && FortStock.Take(selected)) {
            Vector3 grounded = spot;
            grounded.y = Ground(spot);
            DefencePiece.Place(selected, grounded, rotation);
            GameAudio.Play("stomp", 0.35f);
            RefreshLabels();
        }
    }

    public void BuySelected() {
        Buy(selected);
    }

    public void Buy(FortKind kind) {
        if (!WaveDirector.IsBuilding) {
            return;
        }
        if (!MetaProgress.TrySpend(FortStock.Price(kind))) {
            return;
        }
        FortStock.Add(kind);
        GameAudio.Play("launch", 0.35f);
        RefreshLabels();
    }

    bool Aim(out Vector3 spot, out Quaternion rotation) {
        spot = Vector3.zero;
        rotation = Quaternion.identity;
        Camera camera = Camera.main;
        Musician musician = Musician.Instance;
        if (camera == null || musician == null) {
            return false;
        }
        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore)) {
            return false;
        }
        if (hit.collider != null && hit.collider.GetComponentInParent<Enemy>() != null) {
            return false;
        }
        Vector3 flat = hit.point - musician.transform.position;
        flat.y = 0f;
        flat.x = Mathf.Round(flat.x / 0.85f) * 0.85f;
        flat.z = Mathf.Round(flat.z / 0.85f) * 0.85f;
        spot = musician.transform.position + flat;
        spot.y = hit.point.y;
        float face = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
        float yaw = Mathf.Round((face + 90f + yawExtra) / 45f) * 45f;
        rotation = Quaternion.Euler(0f, yaw, 0f);
        return true;
    }

    bool Legal(Vector3 spot) {
        Musician musician = Musician.Instance;
        if (musician == null || FortStock.Count(selected) <= 0) {
            return false;
        }
        Vector3 flat = spot - musician.transform.position;
        flat.y = 0f;
        float dist = flat.magnitude;
        if (dist < DefencePiece.KeepOut || dist > DefencePiece.OuterRadius) {
            return false;
        }
        Collider[] crowded = Physics.OverlapBox(spot + Vector3.up * 0.5f, new Vector3(0.55f, 0.45f, 0.55f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider collider in crowded) {
            if (collider.GetComponentInParent<DefencePiece>() != null) {
                return false;
            }
        }
        return true;
    }

    static float Ground(Vector3 spot) {
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(spot.x, 40f, spot.z), Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore);
        float y = spot.y;
        float best = float.MaxValue;
        foreach (RaycastHit hit in hits) {
            if (hit.collider == null) continue;
            if (hit.collider.GetComponentInParent<Enemy>() != null) continue;
            if (hit.collider.GetComponentInParent<DefencePiece>() != null) continue;
            float delta = Mathf.Abs(hit.point.y - spot.y);
            if (delta < best) {
                best = delta;
                y = hit.point.y;
            }
        }
        return y;
    }

    void ShowGhost(Vector3 spot, Quaternion rotation, bool ok) {
        if (ghost == null) {
            ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ghost.name = "Ghost";
            Collider collider = ghost.GetComponent<Collider>();
            if (collider != null) {
                Destroy(collider);
            }
            Renderer renderer = ghost.GetComponent<Renderer>();
            Shader shader = Shader.Find("Unlit/Color");
            if (shader != null && renderer != null) {
                renderer.sharedMaterial = new Material(shader);
            }
        }
        ghost.SetActive(true);
        Vector3 size = selected == FortKind.Wall ? new Vector3(1.7f, 1.15f, 0.4f)
            : selected == FortKind.Spike ? new Vector3(1.35f, 0.2f, 1.35f)
            : new Vector3(0.7f, 1.6f, 0.7f);
        ghost.transform.SetPositionAndRotation(spot + Vector3.up * (size.y * 0.5f), rotation);
        ghost.transform.localScale = size;
        Renderer skin = ghost.GetComponent<Renderer>();
        if (skin != null && skin.sharedMaterial != null) {
            skin.sharedMaterial.color = ok ? new Color(0.55f, 0.9f, 0.5f) : new Color(0.9f, 0.28f, 0.22f);
        }
    }

    void HideGhost() {
        if (ghost != null) {
            ghost.SetActive(false);
        }
    }

    void ShowRing() {
        Musician musician = Musician.Instance;
        if (musician == null) {
            return;
        }
        if (ring == null) {
            ring = new GameObject("Build Ring");
            Shader shader = Shader.Find("Unlit/Color");
            Material material = shader != null ? new Material(shader) : null;
            if (material != null) {
                material.color = new Color(0.95f, 0.82f, 0.4f);
            }
            Material inner = shader != null ? new Material(shader) : null;
            if (inner != null) {
                inner.color = new Color(0.85f, 0.32f, 0.26f);
            }
            for (int i = 0; i < 32; i++) {
                float angle = i / 32f * Mathf.PI * 2f;
                Peg(ring.transform, angle, DefencePiece.OuterRadius, material);
                if (i % 2 == 0) {
                    Peg(ring.transform, angle, DefencePiece.KeepOut, inner);
                }
            }
        }
        ring.SetActive(true);
        ring.transform.position = musician.transform.position;
    }

    static void Peg(Transform parent, float angle, float radius, Material material) {
        var peg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Collider collider = peg.GetComponent<Collider>();
        if (collider != null) {
            Destroy(collider);
        }
        peg.transform.SetParent(parent, false);
        peg.transform.localPosition = new Vector3(Mathf.Sin(angle) * radius, 0.12f, Mathf.Cos(angle) * radius);
        peg.transform.localScale = new Vector3(0.28f, 0.08f, 0.28f);
        Renderer renderer = peg.GetComponent<Renderer>();
        if (renderer != null && material != null) {
            renderer.sharedMaterial = material;
        }
    }

    void BuildBar() {
        var canvasGo = new GameObject("Build Bar");
        bar = canvasGo;
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 160;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();
        if (FindAnyObjectByType<EventSystem>() == null) {
            var events = new GameObject("Event System");
            events.AddComponent<EventSystem>();
            events.AddComponent<StandaloneInputModule>();
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        FortKind[] kinds = { FortKind.Wall, FortKind.Spike, FortKind.Tower };
        for (int i = 0; i < kinds.Length; i++) {
            float x = -340f + i * 250f;
            var card = new GameObject(kinds[i].ToString());
            card.transform.SetParent(canvasGo.transform, false);
            var image = card.AddComponent<Image>();
            image.color = new Color(0.1f, 0.09f, 0.08f, 0.92f);
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, 28f);
            rect.sizeDelta = new Vector2(230f, 118f);

            cards[i] = image;
            labels[i] = Make(card.transform, font, "", 22, new Vector2(0f, -8f), new Vector2(210f, 70f));
            FortKind captured = kinds[i];
            var pick = card.AddComponent<Button>();
            pick.targetGraphic = image;
            pick.navigation = new Navigation { mode = Navigation.Mode.None };
            pick.onClick.AddListener(() => selected = captured);
            var buttonGo = new GameObject("Buy");
            buttonGo.transform.SetParent(card.transform, false);
            var buttonImage = buttonGo.AddComponent<Image>();
            buttonImage.color = new Color(0.28f, 0.22f, 0.12f, 1f);
            var button = buttonGo.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => Buy(captured));
            var buttonRect = buttonImage.rectTransform;
            buttonRect.anchorMin = new Vector2(0.5f, 0f);
            buttonRect.anchorMax = new Vector2(0.5f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0f);
            buttonRect.anchoredPosition = new Vector2(0f, 8f);
            buttonRect.sizeDelta = new Vector2(180f, 32f);
            prices[i] = Make(buttonGo.transform, font, "Buy", 18, Vector2.zero, new Vector2(180f, 32f));
            prices[i].rectTransform.anchorMin = Vector2.zero;
            prices[i].rectTransform.anchorMax = Vector2.one;
            prices[i].rectTransform.offsetMin = Vector2.zero;
            prices[i].rectTransform.offsetMax = Vector2.zero;
            prices[i].alignment = TextAnchor.MiddleCenter;
        }
    }

    void SetBar(bool open) {
        if (bar != null && bar.activeSelf != open) {
            bar.SetActive(open);
        }
    }

    void RefreshLabels() {
        FortKind[] kinds = { FortKind.Wall, FortKind.Spike, FortKind.Tower };
        string[] names = { "Wall", "Spikes", "Tower" };
        for (int i = 0; i < labels.Length; i++) {
            if (labels[i] == null) continue;
            bool on = kinds[i] == selected;
            labels[i].text = names[i] + "   x" + FortStock.Count(kinds[i]) + (on ? "  selected" : "");
            if (cards[i] != null) {
                cards[i].color = on
                    ? new Color(0.24f, 0.17f, 0.1f, 0.96f)
                    : new Color(0.1f, 0.09f, 0.08f, 0.92f);
            }
            if (prices[i] != null) {
                prices[i].text = "Buy  " + FortStock.Price(kinds[i]);
            }
        }
    }

    static Text Make(Transform parent, Font font, string value, int size, Vector2 position, Vector2 sizeDelta) {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAnchor.UpperCenter;
        text.color = new Color(1f, 0.95f, 0.84f);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        var rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;
        return text;
    }
}
