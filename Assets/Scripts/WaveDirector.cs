using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Infinite turkey waves. The clock is the only way to lose.
public class WaveDirector : MonoBehaviour {
    public static int Wave { get; private set; } = 1;
    public static bool IsBuilding { get; private set; }

    enum Phase {
        Intro,
        Combat,
        Break,
        Build,
        Lost
    }

    Phase phase = Phase.Intro;
    float phaseLeft = 1.6f;
    float spawnIn;
    float spawnAngle;
    readonly List<FowlKind> pending = new List<FowlKind>();
    int cleared;
    int spawnSerial;
    bool pausedGame;
    bool record;

    Text timerText;
    Text waveText;
    Text detailText;
    Text coinText;
    GameObject lostPanel;
    Text lostBody;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() {
        if (SceneManager.GetActiveScene().name != MetaProgress.GameScene) {
            return;
        }
        if (FindAnyObjectByType<WaveDirector>() != null) {
            return;
        }

        var go = new GameObject("Wave Director");
        go.AddComponent<WaveDirector>();
    }

    void Start() {
        Enemy[] already = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy enemy in already) {
            Destroy(enemy.gameObject);
        }

        BuildHud();
        MetaProgress.BeginRun();
        FortStock.BeginRun();
        Wave = 1;
        BeginIntro();
    }

    void OnDestroy() {
        if (pausedGame) {
            Time.timeScale = 1f;
        }
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Equals)) {
            GoToWave(Wave + 1);
            return;
        }
        if (Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.Minus)) {
            GoToWave(Mathf.Max(1, Wave - 1));
            return;
        }

        if (phase == Phase.Lost) {
            if (Input.GetKeyDown(KeyCode.R)) {
                Time.timeScale = 1f;
                pausedGame = false;
                SceneManager.LoadScene(MetaProgress.GameScene);
            } else if (Input.GetKeyDown(KeyCode.M) && Application.CanStreamedLevelBeLoaded(MetaProgress.MenuScene)) {
                Time.timeScale = 1f;
                pausedGame = false;
                SceneManager.LoadScene(MetaProgress.MenuScene);
            }
            return;
        }

        if (phase == Phase.Intro) {
            phaseLeft -= Time.deltaTime;
            if (phaseLeft <= 0f) {
                BeginCombat();
            }
        } else if (phase == Phase.Break || phase == Phase.Build) {
            phaseLeft -= Time.deltaTime;
            IsBuilding = phase == Phase.Build;
            if (phaseLeft <= 0f) {
                IsBuilding = false;
                BeginCombat();
            }
        } else {
            TickCombat();
        }

        MetaProgress.TickToast();
        RefreshHud();
    }

    void BeginIntro() {
        phase = Phase.Intro;
        phaseLeft = Wave == 1 ? 1.8f : 1.35f;
        pending.Clear();
        IsBuilding = false;
        spawnAngle = Random.Range(0f, 360f);
        if (PlayerGrowth.Instance != null) {
            PlayerGrowth.Instance.SyncToWave(Wave);
        }
    }

    void BeginCombat() {
        phase = Phase.Combat;
        FillPending(Wave);
        spawnIn = 0f;
        spawnSerial = 0;
        IsBuilding = false;
        if (PlayerGrowth.Instance != null) {
            PlayerGrowth.Instance.SyncToWave(Wave);
        }
    }

    void TickCombat() {
        int cap = Mathf.Clamp(8 + Wave / 2, 8, 32);
        if (pending.Count > 0 && AliveCount() < cap) {
            spawnIn -= Time.deltaTime;
            if (spawnIn <= 0f) {
                SpawnOne();
                spawnIn = pending.Count > 20 ? 0.08f : 0.22f;
            }
        }

        if (pending.Count == 0 && !AnyAlive()) {
            cleared = Wave;
            record = Wave > MetaProgress.BestWave;
            if (record) {
                MetaProgress.SetBest(Wave);
            }
            Wave++;
            if (IsFortWave(Wave)) {
                phase = Phase.Build;
                phaseLeft = 30f;
                IsBuilding = true;
            } else {
                phase = Phase.Break;
                phaseLeft = 5f;
                IsBuilding = false;
            }
            if (PlayerGrowth.Instance != null) {
                PlayerGrowth.Instance.SyncToWave(Wave);
            }
            return;
        }
    }

    public void MusicianDown() {
        if (phase == Phase.Lost) {
            return;
        }
        Lose();
    }

    void GoToWave(int wave) {
        Time.timeScale = 1f;
        pausedGame = false;
        if (lostPanel != null) {
            lostPanel.SetActive(false);
        }
        Wave = Mathf.Max(1, wave);
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy enemy in enemies) {
            if (enemy != null) {
                Destroy(enemy.gameObject);
            }
        }
        if (IsFortWave(Wave)) {
            phase = Phase.Build;
            phaseLeft = 30f;
            pending.Clear();
            IsBuilding = true;
            if (PlayerGrowth.Instance != null) {
                PlayerGrowth.Instance.SyncToWave(Wave);
            }
            return;
        }
        BeginCombat();
    }

    public static bool IsFortWave(int wave) {
        return wave >= 5 && (wave - 5) % 10 == 0;
    }

    void Lose() {
        phase = Phase.Lost;
        IsBuilding = false;
        pausedGame = true;
        Time.timeScale = 0f;
        lostPanel.SetActive(true);
        string coins = MetaProgress.CoinsThisRun == 0
            ? "No new coins."
            : "+" + MetaProgress.CoinsThisRun + (MetaProgress.CoinsThisRun == 1 ? " coin." : " coins.");
        string shop = Application.CanStreamedLevelBeLoaded(MetaProgress.MenuScene)
            ? "\n\nPress R to try again\nPress M for the shop"
            : "\n\nPress R to try again";
        if (cleared <= 0) {
            string best = MetaProgress.BestWave > 0 ? "\n\nBest cleared: " + MetaProgress.BestWave : "";
            lostBody.text = "The musician fell." + best + "\n\n" + coins + shop;
        } else {
            string waves = cleared == 1 ? "1 wave" : cleared + " waves";
            string bestLine = record ? "New best." : "Best cleared: " + MetaProgress.BestWave;
            lostBody.text = "You cleared " + waves + ".\n\n" + bestLine + "\n\n" + coins + shop;
        }
    }

    void SpawnOne() {
        Transform player = null;
        if (Musician.Instance != null) {
            player = Musician.Instance.transform;
        } else {
            GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null) {
                player = playerGo.transform;
            }
        }

        Vector3 origin = player != null ? player.position : Vector3.zero;
        spawnSerial++;
        if (pending.Count == 0) {
            return;
        }
        FowlKind kind = pending[0];
        pending.RemoveAt(0);
        FowlDef def = FowlRoster.ForSpawn(kind, Wave);
        bool turkey = kind == FowlKind.Turkey;
        float radiusMin = turkey ? 26f : DefencePiece.SpawnMin;
        float radiusMax = turkey ? 36f : DefencePiece.SpawnMax;
        float jitter = turkey ? 70f : 18f;
        Vector3 spot = origin;
        for (int attempt = 0; attempt < 8; attempt++) {
            float spread = (spawnAngle + spawnSerial * 137.5f + Random.Range(-jitter, jitter)) * Mathf.Deg2Rad;
            float radius = Random.Range(radiusMin, radiusMax) + Mathf.Max(0f, def.Scale - 1f) * 0.5f;
            spot = origin + new Vector3(Mathf.Sin(spread), 0f, Mathf.Cos(spread)) * radius;
            if (ClearOfFlock(spot, turkey ? 7f : 4f)) {
                break;
            }
        }
        spot.y = GroundNear(spot, origin.y) + 0.55f + def.Scale * 0.08f;

        var go = new GameObject(def.Name);
        go.tag = "enemy";
        go.transform.position = spot;
        Vector3 look = origin - spot;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f) {
            go.transform.rotation = Quaternion.LookRotation(look);
        }

        Enemy enemy = go.AddComponent<Enemy>();
        enemy.ApplyFowl(def);
    }

    static bool ClearOfFlock(Vector3 spot, float gap) {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        float gapSqr = gap * gap;
        foreach (Enemy enemy in enemies) {
            if (enemy == null) {
                continue;
            }
            Vector3 flat = enemy.transform.position - spot;
            flat.y = 0f;
            if (flat.sqrMagnitude < gapSqr) {
                return false;
            }
        }
        return true;
    }

    void FillPending(int wave) {
        pending.Clear();
        for (int tier = 100; tier >= 10; tier -= 10) {
            int count = wave - (tier - 1);
            if (count <= 0) {
                continue;
            }
            FowlKind kind = FowlRoster.ForTier(tier).Kind;
            for (int i = 0; i < count; i++) {
                pending.Add(kind);
            }
        }
        for (int i = 0; i < wave; i++) {
            pending.Add(FowlKind.Turkey);
        }
    }

    static string Flavor(int wave) {
        if (wave <= 1) {
            return "";
        }
        string line = FowlRoster.Intro(wave);
        int auto = MetaProgress.AutoWave();
        if (wave == auto) {
            line += " Your bow fires on its own now.";
        } else if (wave > auto) {
            line += "  Auto-shoot is on.";
        }
        return line;
    }

    bool AnyAlive() {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy enemy in enemies) {
            if (enemy != null && enemy.Alive) {
                return true;
            }
        }
        return false;
    }

    bool GobblerAlive() {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy enemy in enemies) {
            if (enemy != null && enemy.Alive && enemy.IsGobbler) {
                return true;
            }
        }
        return false;
    }

    static float GroundNear(Vector3 spot, float preferY) {
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(spot.x, 40f, spot.z), Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        float y = preferY;
        bool found = false;
        foreach (RaycastHit hit in hits) {
            if (hit.collider == null || hit.collider.GetComponentInParent<Enemy>() != null) {
                continue;
            }
            float delta = Mathf.Abs(hit.point.y - preferY);
            if (delta < best) {
                best = delta;
                y = hit.point.y;
                found = true;
            }
        }
        return found ? y : preferY;
    }

    void RefreshHud() {
        timerText.text = "";

        if (phase == Phase.Intro) {
            waveText.text = WaveLine();
            detailText.text = Flavor(Wave);
        } else if (phase == Phase.Build) {
            waveText.text = "Build  ·  " + Mathf.CeilToInt(Mathf.Max(phaseLeft, 0f)) + "s";
            detailText.text = Wave <= 5
                ? "It's level 5, prepare."
                : "It's level " + Wave + ", time to prepare.";
        } else if (phase == Phase.Break) {
            waveText.text = "Next wave in " + Mathf.CeilToInt(Mathf.Max(phaseLeft, 0f));
            detailText.text = Flavor(Wave);
        } else if (phase == Phase.Lost) {
            waveText.text = "The musician fell";
            detailText.text = "";
        } else if (!string.IsNullOrEmpty(PlayerGrowth.Banner)) {
            waveText.text = WaveLine();
            detailText.text = PlayerGrowth.Banner;
        } else {
            waveText.text = WaveLine();
            detailText.text = Flavor(Wave);
        }

        detailText.color = new Color(1f, 0.96f, 0.88f, 0.92f);
        if (coinText != null) {
            coinText.text = MetaProgress.StatusLine();
            coinText.color = string.IsNullOrEmpty(MetaProgress.Toast)
                ? new Color(1f, 0.86f, 0.4f, 0.95f)
                : new Color(1f, 0.95f, 0.55f, 1f);
        }
    }

    string WaveLine() {
        string size = "";
        if (PlayerGrowth.Instance != null) {
            size = "   ·   Size " + PlayerGrowth.Instance.Scale.ToString("0.00") + "   ·   Arrow " + PlayerGrowth.Instance.ArrowDamage;
        }
        if (phase == Phase.Intro) {
            return "Wave " + Wave + size;
        }
        int left = AliveCount() + pending.Count;
        return "Wave " + Wave + "   ·   " + left + " left" + size;
    }

    int AliveCount() {
        int count = 0;
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
        foreach (Enemy enemy in enemies) {
            if (enemy != null && enemy.Alive) {
                count++;
            }
        }
        return count;
    }

    void BuildHud() {
        var canvasGo = new GameObject("Wave Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        timerText = MakeText(canvasGo.transform, font, "22", 84, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(420f, 100f));
        waveText = MakeText(canvasGo.transform, font, "Wave 1", 32, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -108f), new Vector2(1500f, 46f));
        detailText = MakeText(canvasGo.transform, font, "", 24, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -154f), new Vector2(1400f, 72f));
        coinText = MakeText(canvasGo.transform, font, "", 26, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(36f, -24f), new Vector2(860f, 40f));
        coinText.rectTransform.pivot = new Vector2(0f, 1f);

        lostPanel = new GameObject("Lost");
        lostPanel.transform.SetParent(canvasGo.transform, false);
        var panel = lostPanel.AddComponent<Image>();
        panel.color = new Color(0.05f, 0.04f, 0.04f, 0.82f);
        panel.raycastTarget = false;
        var panelRect = lostPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(760f, 520f);

        var title = MakeText(lostPanel.transform, font, "The music stopped", 58, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(680f, 90f));
        title.color = new Color(1f, 0.45f, 0.35f);
        lostBody = MakeText(lostPanel.transform, font, "", 30, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(660f, 320f));
        lostPanel.SetActive(false);
    }

    static Text MakeText(Transform parent, Font font, string value, int size, TextAnchor anchor, Vector2 anchorPoint, Vector2 position, Vector2 sizeDelta) {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = new Color(1f, 0.97f, 0.9f, 0.96f);
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.88f);
        outline.effectDistance = new Vector2(1.4f, -1.4f);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorPoint;
        rect.anchorMax = anchorPoint;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;
        return text;
    }
}
